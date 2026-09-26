using System.Collections.Immutable;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>
/// One flat polygon the 3D view paints and picks against: a planar face of a solid, or one chord
/// strip of a curved one.
/// </summary>
/// <param name="Box">The part it belongs to.</param>
/// <param name="Of">
/// The face of the box it lies on, as <see cref="SolidFace.Of"/> says — <see langword="null"/> for a
/// face a cut made.
/// </param>
/// <param name="Points">The corners, in inches, winding counter-clockwise seen from outside.</param>
/// <param name="Normal">The outward unit normal, from the winding.</param>
/// <param name="EdgeDrawn">
/// For each edge, <c>Points[i]</c> to <c>Points[i + 1]</c>, whether it is a real edge of the solid
/// and gets a line: the rulings between two strips of one curved face do not.
/// </param>
public sealed record ScenePolygon(
    EntityId Box,
    BoxFace? Of,
    ImmutableArray<Vector3d> Points,
    Vector3d Normal,
    ImmutableArray<bool> EdgeDrawn)
{
    /// <summary>Whether the polygon faces the eye of a camera — what survives the cull.</summary>
    public bool FacesTowards(Camera camera) => camera.IsPerspective
        ? Vector3d.Dot(Normal, camera.Eye - Points[0]) > 1e-9
        : Vector3d.Dot(Normal, camera.TowardViewer) > 1e-9;

    /// <summary>
    /// This polygon as a camera can draw it: itself when every corner is in front of the eye, what is
    /// left of it after the near plane cuts it when some are not, and <see langword="null"/> when none
    /// are. Only a perspective camera has a near plane. An edge the cut makes is not a real edge and is
    /// not drawn.
    /// </summary>
    public ScenePolygon? ClippedFor(Camera camera)
    {
        if (!camera.IsPerspective || Points.All(point => camera.BeyondNearPlane(point) >= 0))
        {
            return this;
        }

        List<Vector3d> kept = [];
        List<bool> drawn = [];
        for (int i = 0; i < Points.Length; i++)
        {
            Vector3d start = Points[i];
            Vector3d end = Points[(i + 1) % Points.Length];
            double a = camera.BeyondNearPlane(start);
            double b = camera.BeyondNearPlane(end);

            if (a >= 0)
            {
                kept.Add(start);
                drawn.Add(EdgeDrawn[i]);
                if (b < 0)
                {
                    // Leaving: the corner where the edge meets the near plane, then along the plane.
                    kept.Add(start + ((end - start) * (a / (a - b))));
                    drawn.Add(false);
                }
            }
            else if (b >= 0)
            {
                // Entering: the rest of this edge is real.
                kept.Add(start + ((end - start) * (a / (a - b))));
                drawn.Add(EdgeDrawn[i]);
            }
        }

        return kept.Count < 3 ? null : this with { Points = [.. kept], EdgeDrawn = [.. drawn] };
    }

    /// <summary>The furthest any of its corners lies along the camera's view: the painter's sort key.</summary>
    public double FurthestDepth(Camera camera)
    {
        double deepest = double.NegativeInfinity;
        foreach (Vector3d point in Points)
        {
            deepest = Math.Max(deepest, camera.DepthOf(point));
        }

        return deepest;
    }
}

/// <summary>
/// Every visible part of a drawing as flat polygons in <see cref="double"/>: what the 3D view paints
/// and what its picker tests a ray against (<c>docs/design/assembly-model.md</c> &#xA7;8.2, &#xA7;8.4).
/// </summary>
/// <remarks>
/// <para>
/// <strong>One tessellation, two readers.</strong> The painter and the picker read the same
/// polygons, so a curved face is picked exactly where it is drawn — &#xA7;8.2 step 3's "curved
/// patches through the same chord tessellation the renderer draws with". A plane face of a
/// <see cref="Solid"/> is one polygon, its arcs replaced by chords; a curved side is one quad per
/// chord, each flat, so that culling and sorting per polygon is culling and sorting per piece of
/// surface.
/// </para>
/// <para>
/// <strong>Chords from exact ends.</strong> Every arc is tessellated in <see cref="double"/> from its
/// exact endpoints and exact centre (or third point), always walked in one canonical direction so
/// that the cap and the side that share an arc share its chord points bit for bit. Nothing here is
/// stored and nothing is compared against the model.
/// </para>
/// </remarks>
public sealed class ModelScene
{
    /// <summary>The largest angle one chord may span, in degrees.</summary>
    public const double DegreesPerChord = 7.5;

    ModelScene(ImmutableArray<ScenePolygon> polygons) => Polygons = polygons;

    /// <summary>Every polygon, box by box in id order, each box's faces in its solid's order.</summary>
    public ImmutableArray<ScenePolygon> Polygons { get; }

    /// <summary>A scene of given polygons: a Parts view cell's one solid (parts-view §3), with no sketch built for it.</summary>
    public static ModelScene Of(IEnumerable<ScenePolygon> polygons)
    {
        ArgumentNullException.ThrowIfNull(polygons);
        return new ModelScene([.. polygons]);
    }

    /// <summary>The scene of a sketch: every box's solid.</summary>
    public static ModelScene Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        ImmutableArray<ScenePolygon>.Builder polygons = ImmutableArray.CreateBuilder<ScenePolygon>();
        foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            // A room is an outline in the plan, not a solid (renovation-sketches §4.2).
            if (Napkin.Modules.Building.Room.Is(sketch, box))
            {
                continue;
            }

            polygons.AddRange(PolygonsOf(box.Id, box.Solid()));
        }

        // An angled part, from its own solid: six planar faces, none of them a named face of a box
        // (assembly-model §3a.7).
        foreach (Strut strut in sketch.Entities.Values.OfType<Strut>().OrderBy(strut => strut.Id))
        {
            polygons.AddRange(PolygonsOf(strut));
        }

        return new ModelScene(polygons.ToImmutable());
    }

    /// <summary>
    /// The polygons a camera can see, in the order to paint them, furthest first: every one facing
    /// away culled, the rest sorted by the furthest point of each along the view (&#xA7;8.4), then put
    /// right wherever two that overlap on the screen are provably the other way round. Per face,
    /// not per box, so that a part standing in front of another draws over it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Why the sort alone is not enough.</strong> The furthest-point key orders small faces
    /// well and large ones badly: a table top's upper face reaches far back, so on the key alone it
    /// is painted before the aprons under it, and they show through it. So the key is the starting
    /// order and a tie-break, and for each pair of polygons whose screen boxes overlap the classic
    /// plane tests decide which is behind — one lies wholly on the far side of the other's plane, or
    /// the other wholly on the near side of its. Those are the painter's own tests (Newell, Newell
    /// and Sancha), not a depth buffer: still one pass of flat polygons over Avalonia's 2D drawing.
    /// </para>
    /// <para>
    /// The painter's algorithm still fails on cyclic overlap and on solids that pass through each
    /// other, and &#xA7;8.4 accepts that: two parts sharing space are not detected by design
    /// (&#xA7;6). A cycle is broken at its furthest polygon, and the drawing is honestly wrong in the
    /// overlap. Every choice falls back to the scene's own order, so the same drawing paints the
    /// same way every time.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ScenePolygon> BackToFront(Camera camera)
    {
        List<Painted> visible =
        [
            .. Polygons
                .Select((polygon, index) => (polygon: polygon.ClippedFor(camera), index))
                .Where(entry => entry.polygon is not null)
                .Select(entry => (polygon: entry.polygon!, entry.index))
                .Where(entry => entry.polygon.FacesTowards(camera))
                .OrderByDescending(entry => entry.polygon.FurthestDepth(camera))
                .ThenBy(entry => entry.index)
                .Select(entry => new Painted(entry.polygon, ScreenBox(camera, entry.polygon))),
        ];

        int count = visible.Count;
        List<int>[] after = [.. Enumerable.Range(0, count).Select(_ => new List<int>())];
        int[] waiting = new int[count];
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (!visible[i].Screen.Overlaps(visible[j].Screen))
                {
                    continue;
                }

                switch (Behind(visible[i].Polygon, visible[j].Polygon))
                {
                    case < 0:
                        after[i].Add(j);
                        waiting[j]++;
                        break;

                    case > 0:
                        after[j].Add(i);
                        waiting[i]++;
                        break;
                }
            }
        }

        // Kahn's topological order, always taking the earliest ready polygon in the sorted order, and
        // breaking a cycle — should there be one — at the earliest one left.
        List<ScenePolygon> order = new(count);
        bool[] done = new bool[count];
        SortedSet<int> ready = [.. Enumerable.Range(0, count).Where(i => waiting[i] == 0)];
        int earliestLeft = 0;
        while (order.Count < count)
        {
            int next;
            if (ready.Count > 0)
            {
                next = ready.Min;
                ready.Remove(next);
            }
            else
            {
                while (done[earliestLeft])
                {
                    earliestLeft++;
                }

                next = earliestLeft;
            }

            done[next] = true;
            order.Add(visible[next].Polygon);
            foreach (int later in after[next])
            {
                if (--waiting[later] == 0 && !done[later])
                {
                    ready.Add(later);
                }
            }
        }

        return order;
    }

    /// <summary>
    /// Which of two polygons both facing the eye is behind the other: negative when the first is,
    /// positive when the second is, zero when the plane tests cannot tell — coplanar, or crossing.
    /// </summary>
    /// <remarks>
    /// For a polygon facing the eye its outward normal points towards the eye, so the positive side
    /// of its plane is the near side.
    /// </remarks>
    public static int Behind(ScenePolygon first, ScenePolygon second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        bool firstFirst = AllOnSide(first, second, near: false) || AllOnSide(second, first, near: true);
        bool secondFirst = AllOnSide(second, first, near: false) || AllOnSide(first, second, near: true);
        return (firstFirst, secondFirst) switch
        {
            (true, false) => -1,
            (false, true) => 1,
            _ => 0,
        };
    }

    /// <summary>Whether every corner of one polygon is on the near (or far) side of another's plane, or on it.</summary>
    static bool AllOnSide(ScenePolygon polygon, ScenePolygon plane, bool near)
    {
        const double Tolerance = 1e-7;
        Vector3d origin = plane.Points[0];
        foreach (Vector3d point in polygon.Points)
        {
            double side = Vector3d.Dot(plane.Normal, point - origin);
            if (near ? side < -Tolerance : side > Tolerance)
            {
                return false;
            }
        }

        return true;
    }

    static ScreenRect ScreenBox(Camera camera, ScenePolygon polygon)
    {
        double left = double.PositiveInfinity, top = double.PositiveInfinity;
        double right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        foreach (Vector3d point in polygon.Points)
        {
            Avalonia.Point at = camera.Project(point);
            left = Math.Min(left, at.X);
            right = Math.Max(right, at.X);
            top = Math.Min(top, at.Y);
            bottom = Math.Max(bottom, at.Y);
        }

        return new ScreenRect(left, top, right, bottom);
    }

    readonly record struct ScreenRect(double Left, double Top, double Right, double Bottom)
    {
        // Strictly: polygons that only touch along an edge do not hide one another.
        public bool Overlaps(ScreenRect other) =>
            Left < other.Right - 1e-6 && other.Left < Right - 1e-6
            && Top < other.Bottom - 1e-6 && other.Top < Bottom - 1e-6;
    }

    readonly record struct Painted(ScenePolygon Polygon, ScreenRect Screen);

    /// <summary>The polygons of one solid.</summary>
    public static IEnumerable<ScenePolygon> PolygonsOf(EntityId box, Solid solid)
    {
        ArgumentNullException.ThrowIfNull(solid);

        foreach (SolidFace face in solid.Faces)
        {
            if (IsCurvedSide(face))
            {
                foreach (ScenePolygon strip in Strips(box, face))
                {
                    yield return strip;
                }

                continue;
            }

            List<Vector3d> points = [];
            foreach (SolidSegment segment in face.Boundary)
            {
                IReadOnlyList<Vector3d> chord = ChordPoints(segment);
                for (int i = 0; i < chord.Count - 1; i++)
                {
                    points.Add(chord[i]);
                }
            }

            yield return Polygon(box, face.Of, points, drawn: null);
        }
    }

    /// <summary>A strut's six faces as the 3D view paints and picks them.</summary>
    public static IEnumerable<ScenePolygon> PolygonsOf(Strut strut)
    {
        ArgumentNullException.ThrowIfNull(strut);
        return StrutSolid.Of(strut).Select(face => Polygon(
            strut.Id,
            null,
            [.. face.Corners.Select(corner => new Vector3d(corner.X, corner.Y, corner.Z))],
            drawn: null));
    }

    /// <summary>
    /// The points an arc is drawn through, from its start to its end, both included; a straight
    /// segment is its two ends.
    /// </summary>
    public static IReadOnlyList<Vector3d> ChordPoints(SolidSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);

        if (segment is StraightSegment3)
        {
            return [Vector3d.From(segment.From), Vector3d.From(segment.To)];
        }

        // One direction for every arc, so that a cap and a side sharing it share its points exactly.
        if (Precedes(segment.To, segment.From))
        {
            List<Vector3d> reversed = [.. ChordPoints(Reversed(segment))];
            reversed.Reverse();
            return reversed;
        }

        return segment switch
        {
            ArcByCenter3 rounded => ArcAboutCenter(rounded),
            ArcThrough3 curve => ArcThroughPoints(curve),
            _ => [Vector3d.From(segment.From), Vector3d.From(segment.To)],
        };
    }

    /// <summary>
    /// A curved side's boundary is its low arc, a ruling up, its high arc walked back, and a ruling
    /// down (<see cref="SolidFace"/>).
    /// </summary>
    static bool IsCurvedSide(SolidFace face) =>
        face.Of is not (BoxFace.Top or BoxFace.Bottom)
        && face.Boundary.Length == 4
        && face.Boundary[0] is ArcByCenter3 or ArcThrough3;

    static IEnumerable<ScenePolygon> Strips(EntityId box, SolidFace face)
    {
        IReadOnlyList<Vector3d> low = ChordPoints(face.Boundary[0]);
        List<Vector3d> high = [.. ChordPoints(face.Boundary[2])];
        high.Reverse();

        int count = Math.Min(low.Count, high.Count) - 1;
        for (int i = 0; i < count; i++)
        {
            // a₀ b₀ b_D a_D, as the whole face winds: along the low arc, up, back along the high
            // arc, down. Only the first and last rulings are edges of the solid.
            yield return Polygon(
                box,
                face.Of,
                [low[i], low[i + 1], high[i + 1], high[i]],
                [true, i == count - 1, true, i == 0]);
        }
    }

    static ScenePolygon Polygon(EntityId box, BoxFace? of, List<Vector3d> points, bool[]? drawn)
    {
        ImmutableArray<bool> edges = drawn is null
            ? [.. Enumerable.Repeat(true, points.Count)]
            : [.. drawn];
        return new ScenePolygon(box, of, [.. points], Newell(points), edges);
    }

    /// <summary>
    /// The outward unit normal of a polygon by Newell's method: exact for a planar polygon however
    /// its corners are spaced, and the right-hand rule's direction for its winding.
    /// </summary>
    public static Vector3d Newell(IReadOnlyList<Vector3d> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        double x = 0, y = 0, z = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3d a = points[i];
            Vector3d b = points[(i + 1) % points.Count];
            x += (a.Y - b.Y) * (a.Z + b.Z);
            y += (a.Z - b.Z) * (a.X + b.X);
            z += (a.X - b.X) * (a.Y + b.Y);
        }

        return new Vector3d(x, y, z).Normalized();
    }

    /// <summary>
    /// A rounded corner's arc: a quarter turn at most, so always the short way round, interpolated
    /// by angle between the two exact radii.
    /// </summary>
    static IReadOnlyList<Vector3d> ArcAboutCenter(ArcByCenter3 arc)
    {
        Vector3d center = Vector3d.From(arc.Center);
        Vector3d from = Vector3d.From(arc.From);
        Vector3d to = Vector3d.From(arc.To);
        Vector3d u = from - center;
        Vector3d v = to - center;

        double angle = Math.Atan2(Vector3d.Cross(u, v).Length, Vector3d.Dot(u, v));
        int chords = ChordCount(angle);
        List<Vector3d> points = [from];
        double sine = Math.Sin(angle);
        for (int i = 1; i < chords; i++)
        {
            double t = (double)i / chords;
            Vector3d offset = sine > 1e-12
                ? ((u * Math.Sin((1 - t) * angle)) + (v * Math.Sin(t * angle))) / sine
                : (u * (1 - t)) + (v * t);
            points.Add(center + offset);
        }

        points.Add(to);
        return points;
    }

    /// <summary>
    /// A curved edge's arc: the circle through its three exact points, fitted in
    /// <see cref="double"/>, walked from the first through the second to the third.
    /// </summary>
    static IReadOnlyList<Vector3d> ArcThroughPoints(ArcThrough3 arc)
    {
        Vector3d a = Vector3d.From(arc.From);
        Vector3d b = Vector3d.From(arc.Through);
        Vector3d c = Vector3d.From(arc.To);
        Vector3d ab = b - a;
        Vector3d ac = c - a;
        Vector3d normal = Vector3d.Cross(ab, ac);
        double twiceArea = normal.Length;

        // Three points on a line describe no arc; the chord is the honest answer if one ever comes.
        if (twiceArea < 1e-12)
        {
            return [a, c];
        }

        double normalSquared = twiceArea * twiceArea;
        Vector3d center = a + (((Vector3d.Cross(normal, ab) * Vector3d.Dot(ac, ac))
                                + (Vector3d.Cross(ac, normal) * Vector3d.Dot(ab, ab))) / (2 * normalSquared));

        // The triangle a→b→c turns counter-clockwise about its own normal, so the arc from a
        // through b to c does too: measure c's angle that way round from a.
        Vector3d axis = normal / twiceArea;
        Vector3d e1 = (a - center).Normalized();
        Vector3d e2 = Vector3d.Cross(axis, e1);
        double radius = ((a - center).Length + (b - center).Length + (c - center).Length) / 3;
        double sweep = Math.Atan2(Vector3d.Dot(c - center, e2), Vector3d.Dot(c - center, e1));
        if (sweep <= 0)
        {
            sweep += 2 * Math.PI;
        }

        int chords = ChordCount(sweep);
        List<Vector3d> points = [a];
        for (int i = 1; i < chords; i++)
        {
            double angle = sweep * i / chords;
            points.Add(center + (((e1 * Math.Cos(angle)) + (e2 * Math.Sin(angle))) * radius));
        }

        points.Add(c);
        return points;
    }

    static int ChordCount(double radians) =>
        Math.Clamp((int)Math.Ceiling(radians * 180 / Math.PI / DegreesPerChord), 2, 96);

    static SolidSegment Reversed(SolidSegment segment) => segment switch
    {
        ArcByCenter3 arc => new ArcByCenter3(arc.To, arc.From, arc.Center),
        ArcThrough3 arc => new ArcThrough3(arc.To, arc.Through, arc.From),
        _ => new StraightSegment3(segment.To, segment.From),
    };

    /// <summary>A total order on exact points, so that one of an arc's two directions is canonical.</summary>
    static bool Precedes(Point3 a, Point3 b) =>
        a.X.Units != b.X.Units ? a.X.Units < b.X.Units
        : a.Y.Units != b.Y.Units ? a.Y.Units < b.Y.Units
        : a.Z.Units < b.Z.Units;
}

using Avalonia;
using Napkin.App.Editing;
using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>What a click in the 3D view landed on.</summary>
/// <param name="Box">The part.</param>
/// <param name="Feature">
/// The face, edge or vertex of the part's blank that was picked, named in its local frame — the
/// same vocabulary a <see cref="FeatureRef"/> uses.
/// </param>
/// <param name="Face">
/// The face of the part the ray entered through, or the one of <paramref name="Feature"/>'s faces
/// that most nearly faces the eye when the pick was an edge or a vertex the ray only passed near;
/// <see langword="null"/> for a face a cut made.
/// </param>
/// <param name="NormalAxis">
/// The world axis the picked surface is perpendicular to — for a cut face, the one its normal lies
/// most nearly along. A drag on the part's body moves in the plane across this axis (&#xA7;8.3).
/// </param>
/// <param name="Distance">How far along the ray the pick is, in inches; smaller is nearer the eye.</param>
/// <param name="Point">Where on the part the pick is, in inches.</param>
public sealed record ModelPick(
    EntityId Box,
    BoxFeature Feature,
    BoxFace? Face,
    Axis NormalAxis,
    double Distance,
    Vector3d Point);

/// <summary>
/// Which part, and which face, edge or vertex of it, is under a screen point in the 3D view
/// (<c>docs/design/assembly-model.md</c> &#xA7;8.2).
/// </summary>
/// <remarks>
/// <para>
/// <strong>A ray against exact geometry, resolved in <see cref="double"/>.</strong> This is a UI
/// hit-test and nothing stored depends on it — the stance shaped-parts &#xA7;2.4 takes for a point
/// in an outline with a curved edge.
/// </para>
/// <list type="number">
/// <item>The camera gives the ray through the pixel.</item>
/// <item>
/// For a plain box, the ray is taken into the box's own frame — the anchor subtracted, then the
/// orientation undone, which for the 24 orientations is a signed permutation and exact even in
/// <see cref="double"/> — and met with the standard slab test against
/// [0, W] &#xD7; [0, H] &#xD7; [0, D]. The entry distance orders candidates; the slab the ray
/// entered through names the face.
/// </item>
/// <item>
/// For a box with cuts, the ray is met with the <see cref="ModelScene"/>'s polygons of its solid
/// instead — the same chords the painter draws, so a curved face is picked where it is drawn, and a
/// click in a clipped-off corner picks whatever is behind it.
/// </item>
/// <item>
/// The nearest hit wins. Vertices and edges are picked by how near their projections are to the
/// pointer, in pixels, vertices before edges before faces — <see cref="BoxGeometry.GripAt"/>'s rule,
/// one dimension up — as long as they are on the face the ray hit, or the ray hit nothing (#89).
/// </item>
/// </list>
/// </remarks>
public static class ModelPicker
{
    static readonly BoxCorner[] Corners = [BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest];
    static readonly BoxEdge[] Sides = [BoxEdge.South, BoxEdge.East, BoxEdge.North, BoxEdge.West];

    /// <summary>What is under a screen point, or <see langword="null"/> when nothing is.</summary>
    /// <param name="sketch">The drawing.</param>
    /// <param name="scene">The drawing's polygons, for the parts that have cuts.</param>
    /// <param name="camera">How the drawing is being looked at.</param>
    /// <param name="screen">The pointer, in the view's pixels.</param>
    /// <param name="tolerancePixels">How near a vertex or an edge the pointer has to be to pick it.</param>
    public static ModelPick? Pick(Sketch sketch, ModelScene scene, Camera camera, Point screen, double tolerancePixels)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(scene);

        (Vector3d origin, Vector3d direction) = camera.Ray(screen);
        SurfaceHit? surface = NearestSurface(sketch, scene, origin, direction);

        // A cut face has no name to test a feature against, so on one a feature counts by depth: no
        // further behind the surface than a few grab distances.
        double slack = (3 * tolerancePixels / Math.Max(camera.PixelsPerInch, 1e-9)) + 1e-6;

        FeatureHit? best = null;
        foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            if (!box.Orientation.IsExact)
            {
                continue;
            }

            foreach (FeatureHit candidate in FeaturesNear(box, camera, screen, tolerancePixels))
            {
                if (Counts(candidate, surface, slack) && Better(candidate, best))
                {
                    best = candidate;
                }
            }
        }

        if (best is { } feature)
        {
            Box owner = sketch.Find<Box>(feature.Box)!;
            BoxFace? face = surface is { } onIt && onIt.Box == feature.Box
                ? onIt.Face
                : MostFacing(owner, feature.Feature, camera);
            Axis axis = surface is { } same && same.Box == feature.Box
                ? same.NormalAxis
                : face is { } named ? owner.Orientation.Normal(named).Axis : Axis.Z;
            return new ModelPick(feature.Box, feature.Feature, face, axis, feature.Depth, feature.Point);
        }

        if (surface is not { } nearest)
        {
            return null;
        }

        BoxFeature picked = nearest.Face is { } entered
            ? BoxFeature.Face(entered)
            : BoxFeature.Face(sketch.Find<Box>(nearest.Box)!.FaceUp);
        return new ModelPick(nearest.Box, picked, nearest.Face, nearest.NormalAxis, nearest.Distance, nearest.Point);
    }

    /// <summary>
    /// The face the ray through a screen point meets first — no edge or vertex, only a surface a
    /// part could be set down on (#74) — or <see langword="null"/> when it meets none.
    /// </summary>
    /// <remarks>
    /// <see cref="ModelPick.Face"/> is <see langword="null"/> for a face a cut made: not a plane of
    /// the blank, so nothing is placed on it.
    /// </remarks>
    public static ModelPick? SurfaceAt(Sketch sketch, ModelScene scene, Camera camera, Point screen)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(scene);

        (Vector3d origin, Vector3d direction) = camera.Ray(screen);
        if (NearestSurface(sketch, scene, origin, direction) is not { } hit)
        {
            return null;
        }

        BoxFeature feature = hit.Face is { } face ? BoxFeature.Face(face) : BoxFeature.Face(sketch.Find<Box>(hit.Box)!.FaceUp);
        return new ModelPick(hit.Box, feature, hit.Face, hit.NormalAxis, hit.Distance, hit.Point);
    }

    /// <summary>
    /// Where the ray through a screen point meets the floor — the plan datum, Z = 0 — seen from above
    /// it; <see langword="null"/> when the eye is level with the floor or under it.
    /// </summary>
    public static Vector3d? FloorAt(Camera camera, Point screen)
    {
        (Vector3d origin, Vector3d direction) = camera.Ray(screen);
        if (direction.Z > -1e-9)
        {
            return null;
        }

        double t = -origin.Z / direction.Z;
        return origin + (direction * t);
    }

    /// <summary>
    /// Where the ray through a screen point meets the plane across a world axis at a coordinate, in
    /// inches; <see langword="null"/> when the ray runs along the plane.
    /// </summary>
    public static Vector3d? PlaneAt(Camera camera, Point screen, Axis axis, double coordinate)
    {
        (Vector3d origin, Vector3d direction) = camera.Ray(screen);
        double along = direction.Component(axis);
        if (Math.Abs(along) < 1e-9)
        {
            return null;
        }

        return origin + (direction * ((coordinate - origin.Component(axis)) / along));
    }

    /// <summary>
    /// Where a ray, taken as a line, first enters a plain box: the standard slab test in the box's
    /// own frame. Null when it misses.
    /// </summary>
    /// <returns>The distance along the ray, and the face it entered through.</returns>
    public static (double Distance, BoxFace Face)? SlabEntry(Box box, Vector3d origin, Vector3d direction)
    {
        ArgumentNullException.ThrowIfNull(box);

        Vector3d localOrigin = ToLocal(box, origin - Vector3d.From(box.Anchor));
        Vector3d localDirection = ToLocal(box, direction);
        double[] sizes = [box.Width.ToInches(), box.Height.ToInches(), box.Depth.ToInches()];
        double[] from = [localOrigin.X, localOrigin.Y, localOrigin.Z];
        double[] along = [localDirection.X, localDirection.Y, localDirection.Z];

        double entry = double.NegativeInfinity;
        double exit = double.PositiveInfinity;
        int enteredAxis = -1;
        bool enteredAtFar = false;

        for (int axis = 0; axis < 3; axis++)
        {
            if (Math.Abs(along[axis]) < 1e-15)
            {
                // Parallel to this slab: inside it everywhere or nowhere.
                if (from[axis] < 0 || from[axis] > sizes[axis])
                {
                    return null;
                }

                continue;
            }

            double atNear = (0 - from[axis]) / along[axis];
            double atFar = (sizes[axis] - from[axis]) / along[axis];
            double near = Math.Min(atNear, atFar);
            double far = Math.Max(atNear, atFar);

            if (near > entry)
            {
                entry = near;
                enteredAxis = axis;
                enteredAtFar = atFar < atNear;
            }

            exit = Math.Min(exit, far);
        }

        if (enteredAxis < 0 || entry > exit)
        {
            return null;
        }

        BoxFace face = (enteredAxis, enteredAtFar) switch
        {
            (0, false) => BoxFace.West,
            (0, true) => BoxFace.East,
            (1, false) => BoxFace.South,
            (1, true) => BoxFace.North,
            (_, false) => BoxFace.Bottom,
            (_, true) => BoxFace.Top,
        };

        return (entry, face);
    }

    /// <summary>
    /// Where a ray, taken as a line, first meets one of a set of polygons from the front. Null when
    /// it meets none.
    /// </summary>
    public static (double Distance, ScenePolygon Polygon)? PolygonEntry(
        IEnumerable<ScenePolygon> polygons,
        Vector3d origin,
        Vector3d direction)
    {
        ArgumentNullException.ThrowIfNull(polygons);

        (double Distance, ScenePolygon Polygon)? best = null;
        foreach (ScenePolygon polygon in polygons)
        {
            double facing = Vector3d.Dot(polygon.Normal, direction);
            if (facing >= -1e-12)
            {
                // Seen from behind, or edge-on: a ray going in meets the front of a face.
                continue;
            }

            double distance = Vector3d.Dot(polygon.Normal, polygon.Points[0] - origin) / facing;
            Vector3d at = origin + (direction * distance);
            if (Contains(polygon, at) && (best is null || distance < best.Value.Distance))
            {
                best = (distance, polygon);
            }
        }

        return best;
    }

    /// <summary>A world direction or offset in a box's own frame: the orientation undone.</summary>
    /// <remarks>
    /// For the 24 orientations each local axis lands on one world axis, one way or the other
    /// (<see cref="Orientation.Image"/>), so undoing it is picking components and signs — no
    /// trigonometry, and exact in <see cref="double"/>.
    /// </remarks>
    public static Vector3d ToLocal(Box box, Vector3d world)
    {
        ArgumentNullException.ThrowIfNull(box);

        Orientation orientation = box.Orientation;
        return new Vector3d(Component(orientation.Image(Axis.X)), Component(orientation.Image(Axis.Y)), Component(orientation.Image(Axis.Z)));

        double Component((Axis Axis, bool Positive) image) =>
            image.Positive ? world.Component(image.Axis) : -world.Component(image.Axis);
    }

    static SurfaceHit? NearestSurface(Sketch sketch, ModelScene scene, Vector3d origin, Vector3d direction)
    {
        SurfaceHit? nearest = null;
        foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            SurfaceHit? hit = null;
            if (box.Cuts.IsEmpty && box.Orientation.IsExact)
            {
                if (SlabEntry(box, origin, direction) is { } entry)
                {
                    hit = new SurfaceHit(
                        box.Id,
                        entry.Face,
                        box.Orientation.Normal(entry.Face).Axis,
                        entry.Distance,
                        origin + (direction * entry.Distance));
                }
            }
            else if (PolygonEntry(scene.Polygons.Where(polygon => polygon.Box == box.Id), origin, direction) is { } entry)
            {
                hit = new SurfaceHit(
                    box.Id,
                    entry.Polygon.Of,
                    entry.Polygon.Normal.DominantAxis().Axis,
                    entry.Distance,
                    origin + (direction * entry.Distance));
            }

            if (hit is { } found && (nearest is null || found.Distance < nearest.Value.Distance))
            {
                nearest = found;
            }
        }

        return nearest;
    }

    /// <summary>
    /// The vertices and edges of a box's blank that are really on its shape and whose projection is
    /// within the tolerance of the pointer.
    /// </summary>
    static IEnumerable<FeatureHit> FeaturesNear(Box box, Camera camera, Point screen, double tolerance)
    {
        foreach (BoxCorner corner in Corners)
        {
            if (BlankShape.IsVirtualCorner(box, corner))
            {
                // A corner a cut took away is not there to click (§8.2 step 3).
                continue;
            }

            foreach (BoxLevel level in (BoxLevel[])[BoxLevel.Bottom, BoxLevel.Top])
            {
                Vector3d at = Vector3d.From(box.Vertex(corner, level));
                double pixels = Distance(camera.Project(at), screen);
                if (pixels <= tolerance)
                {
                    yield return new FeatureHit(box.Id, BoxFeature.Vertex(corner, level), 0, pixels, camera.DepthOf(at), at);
                }
            }

            // The upright at the corner: the local-Z edge.
            Vector3d bottom = Vector3d.From(box.Vertex(corner, BoxLevel.Bottom));
            Vector3d top = Vector3d.From(box.Vertex(corner, BoxLevel.Top));
            if (NearSegment(camera, screen, bottom, top, tolerance) is { } upright)
            {
                yield return upright with { Box = box.Id, Feature = BoxFeature.LocalUpright(corner) };
            }
        }

        foreach (BoxEdge side in Sides)
        {
            if (BlankShape.CutAt(box, CutSite.Edge(side)) is CurvedEdge)
            {
                continue;
            }

            (BoxCorner fromCorner, BoxCorner toCorner) = Box.Ends(side);
            Vector2 from = box.LocalOffset(fromCorner);
            Vector2 to = box.LocalOffset(toCorner);

            // What is left of the edge line once the cuts at its two ends have taken their setbacks.
            Vector2 low = Toward(from, to, BlankShape.SetbackAlong(box, fromCorner, side));
            Vector2 high = Toward(to, from, BlankShape.SetbackAlong(box, toCorner, side));

            foreach (BoxLevel level in (BoxLevel[])[BoxLevel.Bottom, BoxLevel.Top])
            {
                Length z = level == BoxLevel.Bottom ? Length.Zero : box.Depth;
                Vector3d a = Vector3d.From(box.World(new Vector3(low.Dx, low.Dy, z)));
                Vector3d b = Vector3d.From(box.World(new Vector3(high.Dx, high.Dy, z)));
                if (NearSegment(camera, screen, a, b, tolerance) is { } edge)
                {
                    yield return edge with
                    {
                        Box = box.Id,
                        Feature = BoxFeature.Edge(FaceOf(side), level == BoxLevel.Bottom ? BoxFace.Bottom : BoxFace.Top),
                    };
                }
            }
        }
    }

    // A point moved an exact distance along an axis-aligned edge, from one of its ends towards the other.
    static Vector2 Toward(Vector2 from, Vector2 to, Length amount) => new(
        from.Dx + (amount * Math.Sign((to.Dx - from.Dx).Units)),
        from.Dy + (amount * Math.Sign((to.Dy - from.Dy).Units)));

    static FeatureHit? NearSegment(Camera camera, Point screen, Vector3d a, Vector3d b, double tolerance)
    {
        Point pa = camera.Project(a);
        Point pb = camera.Project(b);
        Vector along = pb - pa;
        double lengthSquared = (along.X * along.X) + (along.Y * along.Y);
        double t = lengthSquared < 1e-12
            ? 0
            : Math.Clamp((((screen.X - pa.X) * along.X) + ((screen.Y - pa.Y) * along.Y)) / lengthSquared, 0, 1);
        Point nearest = pa + (along * t);
        double pixels = Distance(nearest, screen);
        if (pixels > tolerance)
        {
            return null;
        }

        Vector3d at = a + ((b - a) * t);
        return new FeatureHit(default, default, 1, pixels, camera.DepthOf(at), at);
    }

    /// <summary>
    /// Whether a vertex or an edge near the pointer is what the click is aimed at (#89).
    /// </summary>
    /// <remarks>
    /// <para>
    /// With nothing under the pointer, it is: a silhouette edge just off a part — a rail's lower
    /// edge over the floor, assembly-model &#xA7;3a.7 — is exactly what is being aimed at.
    /// </para>
    /// <para>
    /// With a part under the pointer, the part is what is being clicked, and only its own features
    /// count, and only those on the face the ray went in through: an edge or a vertex of that face.
    /// Another part's edge a few pixels away used to win — a click on an apron's face just under
    /// the table top's front edge selected the top — and so did an edge on the far side of a thin
    /// board, which the ray could only reach by passing through the board.
    /// </para>
    /// </remarks>
    static bool Counts(FeatureHit candidate, SurfaceHit? surface, double slack)
    {
        if (surface is not { } hit)
        {
            return true;
        }

        if (candidate.Box != hit.Box)
        {
            return false;
        }

        return hit.Face is { } face
            ? candidate.Feature.Faces.Contains(face)
            : candidate.Depth <= hit.Distance + slack;
    }

    static bool Better(FeatureHit candidate, FeatureHit? current)
    {
        if (current is not { } best)
        {
            return true;
        }

        if (candidate.Dimension != best.Dimension)
        {
            return candidate.Dimension < best.Dimension;
        }

        return Math.Abs(candidate.Pixels - best.Pixels) > 1e-9
            ? candidate.Pixels < best.Pixels
            : candidate.Depth < best.Depth;
    }

    static BoxFace? MostFacing(Box box, BoxFeature feature, Camera camera)
    {
        BoxFace? best = null;
        double facing = double.NegativeInfinity;
        foreach (BoxFace face in feature.Faces)
        {
            (Axis axis, bool positive) = box.Orientation.Normal(face);
            double towards = Vector3d.Dot(Vector3d.Along(axis, positive), camera.TowardViewer);
            if (towards > facing)
            {
                facing = towards;
                best = face;
            }
        }

        return best;
    }

    static bool Contains(ScenePolygon polygon, Vector3d point)
    {
        // Drop the axis the polygon faces most nearly along and test in the other two: a crossing
        // count, which is all a convex strip or a planar cap needs.
        (Axis drop, _) = polygon.Normal.DominantAxis();
        (double X, double Y) Flat(Vector3d v) => drop switch
        {
            Axis.X => (v.Y, v.Z),
            Axis.Y => (v.Z, v.X),
            _ => (v.X, v.Y),
        };

        (double px, double py) = Flat(point);
        bool inside = false;
        int count = polygon.Points.Length;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            (double xi, double yi) = Flat(polygon.Points[i]);
            (double xj, double yj) = Flat(polygon.Points[j]);
            if ((yi > py) != (yj > py) && px < ((xj - xi) * (py - yi) / (yj - yi)) + xi)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    static double Distance(Point a, Point b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    static BoxFace FaceOf(BoxEdge side) => side switch
    {
        BoxEdge.South => BoxFace.South,
        BoxEdge.East => BoxFace.East,
        BoxEdge.North => BoxFace.North,
        _ => BoxFace.West,
    };

    readonly record struct SurfaceHit(EntityId Box, BoxFace? Face, Axis NormalAxis, double Distance, Vector3d Point);

    readonly record struct FeatureHit(EntityId Box, BoxFeature Feature, int Dimension, double Pixels, double Depth, Vector3d Point);
}

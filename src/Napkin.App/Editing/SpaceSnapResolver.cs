using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// One axis of a snap in space: where the moving part landed along it, and what it caught there.
/// </summary>
/// <param name="Axis">The world axis this snap holds.</param>
/// <param name="Coordinate">Where, along that axis, the two faces are coplanar — or the grid line.</param>
/// <param name="Kind">What was caught: the grid, a face, or — two or three axes on one part — a corner.</param>
/// <param name="Target">The part snapped to, or null for the grid.</param>
/// <param name="TargetFace">The target's face that was caught, in its own frame; null for the grid.</param>
/// <param name="MovingFace">The moving part's face that caught it, in its own frame; null for the grid.</param>
public sealed record SpaceSnapHit(
    Axis Axis,
    Length Coordinate,
    SnapKind Kind,
    EntityId? Target,
    BoxFace? TargetFace,
    BoxFace? MovingFace);

/// <summary>
/// Where a part dragged in the 3D view should land, and the relationships the drawing would store
/// if it is dropped there. <see cref="SnapPlan"/> with a third axis.
/// </summary>
/// <param name="Anchor">Where the moving box's anchor goes. Axes that were not dragged are where they were.</param>
/// <param name="Hits">What was caught, one per dragged axis.</param>
/// <param name="Relationships">What dropping here would state — candidates, until the editor asks the updater.</param>
public sealed record SpaceSnapPlan(Point3 Anchor, ImmutableList<SpaceSnapHit> Hits, ImmutableList<Relationship> Relationships)
{
    /// <summary>Whether anything other than the grid was caught.</summary>
    public bool CaughtSomething => Hits.Any(hit => hit.Kind != SnapKind.Grid);
}

/// <summary>
/// Works out where a part dragged in the 3D view lands: on the grid, or with one of its faces
/// coplanar with a face of another part (<c>docs/design/assembly-model.md</c> &#xA7;8.3).
/// </summary>
/// <remarks>
/// <para>
/// <strong><see cref="SnapResolver"/>'s rule, over three axes.</strong> For each axis being dragged,
/// a face of another part perpendicular to that axis, within the snap radius of a face of the
/// moving part perpendicular to it too, beats the grid — when the two parts overlap on the other
/// two axes, so that a board lines up with what it is actually against and not with something
/// across the room that happens to share a height. The plan's resolver is untouched: the plan
/// canvas keeps its own, and this is the 3D view's.
/// </para>
/// <para>
/// <strong>Ties.</strong> The nearest face wins. When two are equally near — an apron's top rising
/// to meet the table top's underside at the very height the legs' tops are — the face the moving
/// face would <em>meet</em>, its normal opposite, wins over one it would merely be level with, and
/// then the part it overlaps more; so the apron is set under the top, not level with a leg.
/// </para>
/// <para>
/// <strong>What a drop says.</strong> One axis caught: <c>Flush(target face, moving face)</c>, the
/// target first because <c>Flush(a, b)</c> reads "b follows a". Two or three axes caught on the same
/// part: the faces meet at an edge or a vertex on each part, and one <see cref="Coincident"/> says
/// so — the plan's corner-on-corner rule, one dimension up. The 3D view never drags three axes from
/// one pointer (&#xA7;8.3), so it asks for one or two; the resolver answers for any number.
/// </para>
/// <para>
/// Pure, exact, and in model units throughout: faces are read off each box's eight exact vertices,
/// which for the 24 orientations bound its blank exactly. A box turned by anything but a quarter
/// turn — reachable only from the solver — is neither snapped nor snapped to.
/// </para>
/// </remarks>
public static class SpaceSnapResolver
{
    static readonly BoxFace[] AllFaces = [BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West, BoxFace.Bottom, BoxFace.Top];
    static readonly Axis[] AllAxes = [Axis.X, Axis.Y, Axis.Z];

    /// <summary>Where a box being dragged along some axes should land.</summary>
    /// <param name="sketch">The drawing.</param>
    /// <param name="moving">The box being dragged, as it is now.</param>
    /// <param name="wantedAnchor">Where the pointer puts the box's anchor, on the dragged axes.</param>
    /// <param name="dragged">The axes being dragged; the others stay where <paramref name="moving"/> has them.</param>
    /// <param name="gridStepInches">The grid step in force at this zoom.</param>
    /// <param name="radius">How near a face has to be to catch.</param>
    public static SpaceSnapPlan Resolve(
        Sketch sketch,
        Box moving,
        Point3 wantedAnchor,
        IReadOnlyCollection<Axis> dragged,
        double gridStepInches,
        Length radius)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(moving);
        ArgumentNullException.ThrowIfNull(dragged);

        Point3 wanted = moving.Anchor;
        foreach (Axis axis in dragged)
        {
            wanted = wanted.WithComponent(axis, wantedAnchor.Component(axis));
        }

        Dictionary<Axis, Candidate> caught = [];
        if (moving.Orientation.IsExact)
        {
            (Point3 myLow, Point3 myHigh) = Extent(moving with { Anchor = wanted });
            foreach (Box other in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
            {
                if (other.Id == moving.Id || !other.Orientation.IsExact)
                {
                    continue;
                }

                (Point3 theirLow, Point3 theirHigh) = Extent(other);
                foreach (Axis axis in AllAxes.Where(dragged.Contains))
                {
                    if (!OverlapsAcross(axis, myLow, myHigh, theirLow, theirHigh, radius, out double area))
                    {
                        continue;
                    }

                    foreach ((Length mine, bool myPositive) in Faces(axis, myLow, myHigh))
                    {
                        foreach ((Length theirs, bool theirPositive) in Faces(axis, theirLow, theirHigh))
                        {
                            Length shift = theirs - mine;
                            if (Length.Abs(shift) > radius)
                            {
                                continue;
                            }

                            Candidate candidate = new(
                                axis,
                                shift,
                                theirs,
                                other.Id,
                                FaceFacing(other, axis, theirPositive),
                                FaceFacing(moving, axis, myPositive),
                                Mating: myPositive != theirPositive,
                                area);

                            caught[axis] = caught.TryGetValue(axis, out Candidate? current) ? Better(current, candidate) : candidate;
                        }
                    }
                }
            }
        }

        Point3 anchor = wanted;
        foreach (Axis axis in AllAxes.Where(dragged.Contains))
        {
            anchor = anchor.WithComponent(
                axis,
                caught.TryGetValue(axis, out Candidate? hit)
                    ? wanted.Component(axis) + hit.Shift
                    : SnapGrid.Snap(wanted.Component(axis), gridStepInches));
        }

        ImmutableList<SpaceSnapHit>.Builder hits = ImmutableList.CreateBuilder<SpaceSnapHit>();
        ImmutableList<Relationship>.Builder statements = ImmutableList.CreateBuilder<Relationship>();

        List<Candidate> onParts = [.. AllAxes.Where(caught.ContainsKey).Select(axis => caught[axis])];
        bool corner = onParts.Count >= 2 && onParts.All(hit => hit.Target == onParts[0].Target);

        foreach (Axis axis in AllAxes.Where(dragged.Contains))
        {
            hits.Add(caught.TryGetValue(axis, out Candidate? hit)
                ? new SpaceSnapHit(axis, hit.Coordinate, corner ? SnapKind.Corner : SnapKind.Edge, hit.Target, hit.TargetFace, hit.MovingFace)
                : new SpaceSnapHit(axis, anchor.Component(axis), SnapKind.Grid, null, null, null));
        }

        if (corner)
        {
            // Two or three faces of one part caught at once meet at an edge or a vertex, on each part:
            // one place sitting on another, said in one statement rather than two or three.
            statements.Add(new Coincident(
                RelationshipId.New(),
                new FeatureRef(onParts[0].Target, Meeting([.. onParts.Select(hit => hit.TargetFace)])),
                new FeatureRef(moving.Id, Meeting([.. onParts.Select(hit => hit.MovingFace)]))));
        }
        else
        {
            foreach (Candidate hit in onParts)
            {
                statements.Add(new Flush(
                    RelationshipId.New(),
                    new FeatureRef(hit.Target, BoxFeature.Face(hit.TargetFace)),
                    new FeatureRef(moving.Id, BoxFeature.Face(hit.MovingFace))));
            }
        }

        return new SpaceSnapPlan(anchor, hits.ToImmutable(), statements.ToImmutable());
    }

    /// <summary>
    /// The box's world extent: the least and greatest coordinate of its eight vertices on each axis.
    /// For the 24 orientations its six faces lie exactly on these six planes.
    /// </summary>
    public static (Point3 Low, Point3 High) Extent(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        Point3? low = null, high = null;
        foreach (BoxCorner corner in (BoxCorner[])[BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
        {
            foreach (BoxLevel level in (BoxLevel[])[BoxLevel.Bottom, BoxLevel.Top])
            {
                Point3 vertex = box.Vertex(corner, level);
                low = low is { } l
                    ? new Point3(Length.Min(l.X, vertex.X), Length.Min(l.Y, vertex.Y), Length.Min(l.Z, vertex.Z))
                    : vertex;
                high = high is { } h
                    ? new Point3(Length.Max(h.X, vertex.X), Length.Max(h.Y, vertex.Y), Length.Max(h.Z, vertex.Z))
                    : vertex;
            }
        }

        return (low!.Value, high!.Value);
    }

    /// <summary>The face of a box whose outward normal points along a world axis, the given way.</summary>
    public static BoxFace FaceFacing(Box box, Axis axis, bool positive)
    {
        ArgumentNullException.ThrowIfNull(box);

        foreach (BoxFace face in AllFaces)
        {
            if (box.Orientation.Normal(face) == (axis, positive))
            {
                return face;
            }
        }

        throw new InvalidOperationException($"No face of {box.Id} faces {(positive ? "+" : "−")}{axis}; its orientation is not one of the 24.");
    }

    static IEnumerable<(Length Coordinate, bool Positive)> Faces(Axis axis, Point3 low, Point3 high) =>
        [(low.Component(axis), false), (high.Component(axis), true)];

    static bool OverlapsAcross(Axis axis, Point3 myLow, Point3 myHigh, Point3 theirLow, Point3 theirHigh, Length slack, out double area)
    {
        area = 1;
        foreach (Axis across in AllAxes)
        {
            if (across == axis)
            {
                continue;
            }

            Length mineLow = myLow.Component(across), mineHigh = myHigh.Component(across);
            Length theirsLow = theirLow.Component(across), theirsHigh = theirHigh.Component(across);
            if (mineLow - slack > theirsHigh || theirsLow - slack > mineHigh)
            {
                area = 0;
                return false;
            }

            long shared = (Length.Min(mineHigh, theirsHigh) - Length.Max(mineLow, theirsLow)).Units;
            area *= Math.Max(shared, 0);
        }

        return true;
    }

    static Candidate Better(Candidate current, Candidate candidate)
    {
        long currentShift = Math.Abs(current.Shift.Units);
        long candidateShift = Math.Abs(candidate.Shift.Units);
        if (candidateShift != currentShift)
        {
            return candidateShift < currentShift ? candidate : current;
        }

        if (candidate.Mating != current.Mating)
        {
            return candidate.Mating ? candidate : current;
        }

        return candidate.Area > current.Area ? candidate : current;
    }

    static BoxFeature Meeting(BoxFace[] faces) => faces.Length switch
    {
        2 => BoxFeature.Edge(faces[0], faces[1]),
        3 => BoxFeature.Vertex(faces[0], faces[1], faces[2]),
        _ => BoxFeature.Face(faces[0]),
    };

    sealed record Candidate(
        Axis Axis,
        Length Shift,
        Length Coordinate,
        EntityId Target,
        BoxFace TargetFace,
        BoxFace MovingFace,
        bool Mating,
        double Area);
}

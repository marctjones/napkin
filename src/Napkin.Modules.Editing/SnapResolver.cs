using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>What a snap caught on.</summary>
public enum SnapKind
{
    /// <summary>The drawing grid.</summary>
    Grid,

    /// <summary>Another part's edge.</summary>
    Edge,

    /// <summary>Another part's corner — both axes on the same part at once.</summary>
    Corner,
}

/// <summary>
/// One axis of a snap: where the moving part landed, and what the drawing would then be able to
/// say about it.
/// </summary>
/// <param name="Axis">The axis this snap holds.</param>
/// <param name="Coordinate">Where, along that axis, the two things line up.</param>
/// <param name="Kind">What was caught.</param>
/// <param name="Target">The part snapped to, or null for the grid.</param>
/// <param name="From">One end of the indicator line, along the other axis.</param>
/// <param name="To">The other end of the indicator line.</param>
public sealed record SnapHit(
    Axis Axis,
    Length Coordinate,
    SnapKind Kind,
    EntityId? Target,
    Length From,
    Length To);

/// <summary>
/// Where a dragged part should land, and the relationships the drawing would store if it is
/// dropped there.
/// </summary>
/// <remarks>
/// The relationships are <em>candidates</em>, not facts: nothing is stored until the drop, and
/// the editor still asks the updater whether it can hold each one before offering it
/// (docs/design/geometry-model.md &#xA7;3.2 — relationships are stored, never inferred).
/// </remarks>
/// <param name="Anchor">Where the moving box's anchor goes, in the plan; its Z is not the plan's to change.</param>
/// <param name="Hits">What was caught, at most one per axis.</param>
/// <param name="Relationships">What dropping here would state, in the order it would be stated.</param>
public sealed record SnapPlan(Point2 Anchor, ImmutableList<SnapHit> Hits, ImmutableList<Relationship> Relationships)
{
    /// <summary>Whether anything other than the grid was caught.</summary>
    public bool CaughtSomething => Hits.Any(hit => hit.Kind != SnapKind.Grid);
}

/// <summary>
/// Works out where a dragged part lands: on the grid, or lined up with a part already drawn.
/// </summary>
/// <remarks>
/// <para>
/// Pure, and in model units throughout. The canvas decides how close is close enough — a snap
/// radius in pixels turned into a <see cref="Length"/> at the current zoom — and this decides
/// what that catches. Per axis, an edge of another part wins over the grid when it is within the
/// radius, because lining two parts up is what a person was trying to do and landing on a
/// quarter inch is only what happens otherwise.
/// </para>
/// <para>
/// Two edges only count as candidates when they overlap along the other axis. A shelf lines up
/// with the side it touches, not with a leg three feet away that happens to share an X.
/// </para>
/// <para>
/// Everything is read through each box's <see cref="Footprint"/>, and what the drop would state is
/// named through it too (<c>docs/design/assembly-model.md</c> &#xA7;7.2): a plan side is the face
/// <see cref="Footprint.FaceAt"/> says, a plan corner the upright <see cref="Footprint.UprightAt"/>
/// says. For a box lying as drawn those are the blank's own edge and corner, so the relationships
/// are the ones the plan snap always stated; for a box stood on a side they are whichever faces
/// and edge the plan sees there, so a turned part snapped in the plan is held as it would be in the
/// 3D view (#70) rather than landed with nothing said.
/// </para>
/// </remarks>
public static class SnapResolver
{
    /// <summary>
    /// Where a box being moved should land, given where the pointer says it wants to be.
    /// </summary>
    /// <param name="sketch">The drawing.</param>
    /// <param name="moving">The box being moved, in its current size and rotation.</param>
    /// <param name="wantedAnchor">Where the pointer puts the box's anchor.</param>
    /// <param name="gridStepInches">The grid step in force at this zoom.</param>
    /// <param name="radius">How near a target has to be to catch.</param>
    /// <param name="ignoring">Parts that are not to be snapped to — the others moving with it (#87).</param>
    public static SnapPlan Resolve(
        Sketch sketch,
        Box moving,
        Point2 wantedAnchor,
        double gridStepInches,
        Length radius,
        IReadOnlyCollection<EntityId>? ignoring = null)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(moving);

        Box proposed = moving with { Anchor = moving.Anchor with { X = wantedAnchor.X, Y = wantedAnchor.Y } };
        List<EdgeLine> movingEdges = [.. BoxGeometry.AxisAlignedEdges(proposed)];

        Candidate? bestX = null;
        Candidate? bestY = null;

        foreach (Box other in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            if (other.Id == moving.Id || (ignoring?.Contains(other.Id) ?? false))
            {
                continue;
            }

            foreach (EdgeLine target in BoxGeometry.AxisAlignedEdges(other))
            {
                foreach (EdgeLine mine in movingEdges)
                {
                    if (mine.NormalAxis != target.NormalAxis || !Overlaps(mine, target, radius))
                    {
                        continue;
                    }

                    Length shift = target.Coordinate - mine.Coordinate;
                    if (Length.Abs(shift) > radius)
                    {
                        continue;
                    }

                    Candidate candidate = new(
                        target.NormalAxis,
                        shift,
                        target.Coordinate,
                        other.Id,
                        target.Edge,
                        mine.Edge,
                        Length.Min(mine.Low, target.Low),
                        Length.Max(mine.High, target.High));

                    if (target.NormalAxis == Axis.X)
                    {
                        bestX = Better(bestX, candidate);
                    }
                    else
                    {
                        bestY = Better(bestY, candidate);
                    }
                }
            }
        }

        // A strut that leans one way keeps two faces square to the room (angled-parts §3.2, §3.3):
        // seen from above they are two more edge lines, and a box's side flush to one is exact.
        foreach (Strut strut in sketch.Entities.Values.OfType<Strut>().OrderBy(strut => strut.Id))
        {
            if (ignoring?.Contains(strut.Id) ?? false)
            {
                continue;
            }

            foreach ((StrutFace face, EdgeLine target) in StrutFaceLines(strut))
            {
                foreach (EdgeLine mine in movingEdges)
                {
                    Length shift = target.Coordinate - mine.Coordinate;
                    if (mine.NormalAxis != target.NormalAxis || !Overlaps(mine, target, radius) || Length.Abs(shift) > radius)
                    {
                        continue;
                    }

                    Candidate candidate = new(
                        target.NormalAxis, shift, target.Coordinate, strut.Id, target.Edge, mine.Edge,
                        Length.Min(mine.Low, target.Low), Length.Max(mine.High, target.High))
                    {
                        StrutFace = face,
                    };
                    if (target.NormalAxis == Axis.X)
                    {
                        bestX = Better(bestX, candidate);
                    }
                    else
                    {
                        bestY = Better(bestY, candidate);
                    }
                }
            }
        }

        Length anchorX = bestX is { } x ? wantedAnchor.X + x.Shift : SnapGrid.Snap(wantedAnchor.X, gridStepInches);
        Length anchorY = bestY is { } y ? wantedAnchor.Y + y.Shift : SnapGrid.Snap(wantedAnchor.Y, gridStepInches);
        Point2 anchor = new(anchorX, anchorY);
        Box landed = moving with { Anchor = moving.Anchor with { X = anchor.X, Y = anchor.Y } };

        ImmutableList<SnapHit>.Builder hits = ImmutableList.CreateBuilder<SnapHit>();
        ImmutableList<Relationship>.Builder statements = ImmutableList.CreateBuilder<Relationship>();

        // Both axes caught on the same part, on edges that meet at a corner: that is one corner
        // sitting on another, and Coincident says it in one statement instead of two.
        if (bestX is { StrutFace: null } cornerX && bestY is { StrutFace: null } cornerY
            && cornerX.Target == cornerY.Target
            && SharedCorner(cornerX.TargetEdge, cornerY.TargetEdge) is { } theirCorner
            && SharedCorner(cornerX.MovingEdge, cornerY.MovingEdge) is { } myCorner)
        {
            hits.Add(HitOf(cornerX, SnapKind.Corner));
            hits.Add(HitOf(cornerY, SnapKind.Corner));

            // Plan corners: the edges each footprint stands up from there, whatever its face-up.
            if (sketch.Find<Box>(cornerX.Target) is { } target)
            {
                statements.Add(new Coincident(
                    RelationshipId.New(),
                    new FeatureRef(cornerX.Target, target.Footprint().UprightAt(theirCorner)),
                    new FeatureRef(moving.Id, landed.Footprint().UprightAt(myCorner))));
            }

            return new SnapPlan(anchor, hits.ToImmutable(), statements.ToImmutable());
        }

        foreach (Candidate? candidate in (Candidate?[])[bestX, bestY])
        {
            if (candidate is not { } caught)
            {
                continue;
            }

            hits.Add(HitOf(caught, SnapKind.Edge));

            if (caught.StrutFace is { } strutFace)
            {
                statements.Add(new Flush(
                    RelationshipId.New(),
                    new StrutFaceRef(caught.Target, strutFace),
                    new FeatureRef(moving.Id, BoxFeature.Face(landed.Footprint().FaceAt(caught.MovingEdge)))));
                continue;
            }

            // The moving part is the one that follows, so it is the second edge: Flush(a, b)
            // reads "b follows a" (docs/design/geometry-model.md §3.2). Each plan side is the face
            // its own footprint sees there.
            if (sketch.Find<Box>(caught.Target) is { } target)
            {
                statements.Add(new Flush(
                    RelationshipId.New(),
                    new FeatureRef(caught.Target, BoxFeature.Face(target.Footprint().FaceAt(caught.TargetEdge))),
                    new FeatureRef(moving.Id, BoxFeature.Face(landed.Footprint().FaceAt(caught.MovingEdge)))));
            }
        }

        if (bestX is null)
        {
            hits.Add(GridHit(Axis.X, anchor.X, landed));
        }

        if (bestY is null)
        {
            hits.Add(GridHit(Axis.Y, anchor.Y, landed));
        }

        return new SnapPlan(anchor, hits.ToImmutable(), statements.ToImmutable());
    }

    /// <summary>
    /// The corner the two edges of one box share, or null when they are parallel.
    /// </summary>
    public static BoxCorner? SharedCorner(BoxEdge first, BoxEdge second)
    {
        (BoxCorner a, BoxCorner b) = Box.Ends(first);
        (BoxCorner c, BoxCorner d) = Box.Ends(second);

        foreach (BoxCorner corner in (BoxCorner[])[a, b])
        {
            if (corner == c || corner == d)
            {
                return corner;
            }
        }

        return null;
    }

    static SnapHit HitOf(Candidate candidate, SnapKind kind) => new(
        candidate.Axis,
        candidate.Coordinate,
        kind,
        candidate.Target,
        candidate.Low,
        candidate.High);

    static SnapHit GridHit(Axis axis, Length coordinate, Box landed)
    {
        // The indicator for a grid snap runs across the part itself, which is the only thing the
        // grid line has to do with.
        Footprint footprint = landed.Footprint();
        IEnumerable<Point2> corners = ((BoxCorner[])
            [BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
            .Select(footprint.Corner);
        Axis other = axis switch
        {
            Axis.X => Axis.Y,
            Axis.Y => Axis.X,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "The plan snaps along X and Y only."),
        };
        List<Length> across = [.. corners.Select(corner => corner.Component(other))];
        return new SnapHit(axis, coordinate, SnapKind.Grid, Target: null, across.Min(), across.Max());
    }

    static Candidate? Better(Candidate? current, Candidate candidate) =>
        current is null || Length.Abs(candidate.Shift) < Length.Abs(current.Shift) ? candidate : current;

    static bool Overlaps(EdgeLine a, EdgeLine b, Length slack) =>
        a.Low - slack <= b.High && b.Low - slack <= a.High;

    /// <summary>
    /// The faces of a strut a plan snap can catch: those square to X or Y (a one-way lean's two side
    /// faces), each as a line over the strut's plan extent along the other axis.
    /// </summary>
    public static IEnumerable<(StrutFace Face, EdgeLine Line)> StrutFaceLines(Strut strut)
    {
        ArgumentNullException.ThrowIfNull(strut);
        IReadOnlyList<(double X, double Y)> outline = StrutSolid.PlanOutline(strut);
        foreach (StrutFace face in Enum.GetValues<StrutFace>())
        {
            if (strut.FacePlane(face) is not (var axis, var at) || axis == Axis.Z)
            {
                continue;
            }

            IEnumerable<double> along = outline.Select(point => axis == Axis.X ? point.Y : point.X);
            // A strut face has no box edge; the edge named here is never read for a strut target.
            yield return (face, new EdgeLine(
                BoxEdge.South,
                axis,
                at,
                Length.FromInches(along.Min(), Rounding.HalfToEven),
                Length.FromInches(along.Max(), Rounding.HalfToEven)));
        }
    }

    sealed record Candidate(
        Axis Axis,
        Length Shift,
        Length Coordinate,
        EntityId Target,
        BoxEdge TargetEdge,
        BoxEdge MovingEdge,
        Length Low,
        Length High)
    {
        /// <summary>The strut face caught, when the target is a strut rather than a box.</summary>
        public StrutFace? StrutFace { get; init; }
    }
}

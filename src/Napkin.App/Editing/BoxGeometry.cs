using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// What a person grabbed on a selected part. Corners and edges are the <em>footprint's</em>, named
/// in the plan before the spin (<c>docs/design/assembly-model.md</c> &#xA7;7.1); for a box lying as
/// drawn they are the blank's own.
/// </summary>
public enum BoxGrip
{
    /// <summary>The body of the part: a move.</summary>
    Body,

    /// <summary>The handle on the part's own south-west corner.</summary>
    SouthWest,

    /// <summary>The handle on the part's own south-east corner.</summary>
    SouthEast,

    /// <summary>The handle on the part's own north-east corner.</summary>
    NorthEast,

    /// <summary>The handle on the part's own north-west corner.</summary>
    NorthWest,

    /// <summary>The handle in the middle of the part's own south edge.</summary>
    South,

    /// <summary>The handle in the middle of the part's own east edge.</summary>
    East,

    /// <summary>The handle in the middle of the part's own north edge.</summary>
    North,

    /// <summary>The handle in the middle of the part's own west edge.</summary>
    West,
}

/// <summary>
/// One axis-aligned side of a box's footprint, as a line the rest of the drawing can be measured
/// against.
/// </summary>
/// <param name="Edge">Which side of the footprint, in its own frame before the spin; <see cref="BoxGeometry.LocalEdge"/> says which edge of the blank that is.</param>
/// <param name="NormalAxis">The axis the edge's position varies along — X for a vertical edge.</param>
/// <param name="Coordinate">Where the edge sits along <paramref name="NormalAxis"/>.</param>
/// <param name="Low">The lower end of the edge along the other axis.</param>
/// <param name="High">The upper end of the edge along the other axis.</param>
public sealed record EdgeLine(BoxEdge Edge, Axis NormalAxis, Length Coordinate, Length Low, Length High);

/// <summary>
/// Where a box's corners, edges and handles are, and what a point on the canvas has hold of.
/// </summary>
/// <remarks>
/// <para>
/// Every method here is a pure function of a <see cref="Box"/> and exact <see cref="Length"/>s:
/// nothing in this file knows about pixels, and nothing in it produces a <see cref="Sketch"/>.
/// The canvas converts a pointer position to a model point and a tolerance to a model length, and
/// asks these questions in the model's own units.
/// </para>
/// <para>
/// What the plan has of a box is its <see cref="Footprint"/>, and that is all this reads: never
/// the box's anchor, sizes or rotation directly (<c>docs/design/assembly-model.md</c> &#xA7;7.2).
/// For a box lying as drawn the footprint is the box's own rectangle, field for field, so nothing
/// here behaves differently for one.
/// </para>
/// </remarks>
public static class BoxGeometry
{
    /// <summary>The corner grips, in the order handles are drawn.</summary>
    public static readonly ImmutableArray<BoxGrip> CornerGrips =
        [BoxGrip.SouthWest, BoxGrip.SouthEast, BoxGrip.NorthEast, BoxGrip.NorthWest];

    /// <summary>The edge grips, in the order handles are drawn.</summary>
    public static readonly ImmutableArray<BoxGrip> EdgeGrips =
        [BoxGrip.South, BoxGrip.East, BoxGrip.North, BoxGrip.West];

    /// <summary>Whether a grip resizes rather than moves.</summary>
    public static bool IsResize(BoxGrip grip) => grip != BoxGrip.Body;

    /// <summary>Where a grip's handle is drawn, in model coordinates.</summary>
    public static Point2 GripPoint(Box box, BoxGrip grip)
    {
        ArgumentNullException.ThrowIfNull(box);

        Footprint footprint = box.Footprint();
        return grip switch
        {
            BoxGrip.Body => footprint.Center,
            BoxGrip.SouthWest => footprint.Corner(BoxCorner.SouthWest),
            BoxGrip.SouthEast => footprint.Corner(BoxCorner.SouthEast),
            BoxGrip.NorthEast => footprint.Corner(BoxCorner.NorthEast),
            BoxGrip.NorthWest => footprint.Corner(BoxCorner.NorthWest),
            BoxGrip.South => Midpoint(footprint, BoxEdge.South),
            BoxGrip.East => Midpoint(footprint, BoxEdge.East),
            BoxGrip.North => Midpoint(footprint, BoxEdge.North),
            BoxGrip.West => Midpoint(footprint, BoxEdge.West),
            _ => throw new ArgumentOutOfRangeException(nameof(grip), grip, "Unknown grip."),
        };
    }

    /// <summary>
    /// The edge of the blank a side of the footprint is, or <see langword="null"/> when the plan
    /// sees that side as the blank's top or bottom — a box standing on a side. A resize handle
    /// wants <see cref="Footprint.FaceAt"/> instead, which names that top or bottom too.
    /// </summary>
    public static BoxEdge? LocalEdge(Box box, BoxEdge planSide)
    {
        ArgumentNullException.ThrowIfNull(box);

        return box.Footprint().FaceAt(planSide) switch
        {
            BoxFace.South => BoxEdge.South,
            BoxFace.East => BoxEdge.East,
            BoxFace.North => BoxEdge.North,
            BoxFace.West => BoxEdge.West,
            _ => null,
        };
    }

    /// <summary>
    /// The corner of the blank a corner of the footprint is — the one whose local upright is the
    /// footprint's plan upright there — or <see langword="null"/> when that plan upright is not a
    /// local one, which is the case for every corner of a box standing on a side.
    /// </summary>
    public static BoxCorner? LocalCorner(Box box, BoxCorner planCorner)
    {
        ArgumentNullException.ThrowIfNull(box);

        BoxFeature upright = box.Footprint().UprightAt(planCorner);
        foreach (BoxCorner corner in (BoxCorner[])[BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
        {
            if (BoxFeature.LocalUpright(corner) == upright)
            {
                return corner;
            }
        }

        return null;
    }

    /// <summary>The footprint's size across a side: its plan width for east and west, its plan height for north and south.</summary>
    public static Length SizeAcross(Box box, BoxEdge planSide)
    {
        ArgumentNullException.ThrowIfNull(box);

        Footprint footprint = box.Footprint();
        return planSide is BoxEdge.East or BoxEdge.West ? footprint.PlanWidth : footprint.PlanHeight;
    }

    /// <summary>
    /// The edges a grip drags. A corner drags two, which is why a corner resize is one
    /// <see cref="Batch"/> of two <see cref="DragFace"/>s rather than two separate edits.
    /// </summary>
    public static ImmutableArray<BoxEdge> EdgesOf(BoxGrip grip) => grip switch
    {
        BoxGrip.South => [BoxEdge.South],
        BoxGrip.East => [BoxEdge.East],
        BoxGrip.North => [BoxEdge.North],
        BoxGrip.West => [BoxEdge.West],
        BoxGrip.SouthWest => [BoxEdge.South, BoxEdge.West],
        BoxGrip.SouthEast => [BoxEdge.South, BoxEdge.East],
        BoxGrip.NorthEast => [BoxEdge.North, BoxEdge.East],
        BoxGrip.NorthWest => [BoxEdge.North, BoxEdge.West],
        _ => [],
    };

    /// <summary>
    /// How far an edge has to move along its own outward normal for the handle on it to follow a
    /// displacement of the pointer.
    /// </summary>
    /// <remarks>
    /// The displacement arrives in world coordinates and the side is named in the footprint's
    /// frame, so it is un-spun into that frame first. For the right-angle rotations this beta
    /// allows, that is an exact swap of components and never rounds.
    /// </remarks>
    public static Length OutwardDelta(Box box, BoxEdge edge, Vector2 worldDelta)
    {
        ArgumentNullException.ThrowIfNull(box);

        Vector2 local = box.Footprint().ToLocal(worldDelta);
        return edge switch
        {
            BoxEdge.East => local.Dx,
            BoxEdge.West => -local.Dx,
            BoxEdge.North => local.Dy,
            BoxEdge.South => -local.Dy,
            _ => Length.Zero,
        };
    }

    /// <summary>
    /// The sides of the box's footprint as lines — the faces whose world normal is horizontal —
    /// skipping any that is not axis-aligned. Every side of a box rotated by a right-angle multiple
    /// is axis-aligned, so in this beta that is all four, in the order south, east, north, west.
    /// </summary>
    public static IEnumerable<EdgeLine> AxisAlignedEdges(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        Footprint footprint = box.Footprint();
        foreach (BoxEdge edge in (BoxEdge[])[BoxEdge.South, BoxEdge.East, BoxEdge.North, BoxEdge.West])
        {
            (BoxCorner fromCorner, BoxCorner toCorner) = Box.Ends(edge);
            Point2 from = footprint.Corner(fromCorner);
            Point2 to = footprint.Corner(toCorner);

            if (from.X == to.X && from.Y != to.Y)
            {
                yield return new EdgeLine(edge, Axis.X, from.X, Length.Min(from.Y, to.Y), Length.Max(from.Y, to.Y));
            }
            else if (from.Y == to.Y && from.X != to.X)
            {
                yield return new EdgeLine(edge, Axis.Y, from.Y, Length.Min(from.X, to.X), Length.Max(from.X, to.X));
            }
        }
    }

    /// <summary>Whether a point is inside the box's footprint, corners included.</summary>
    public static bool Contains(Box box, Point2 point)
    {
        ArgumentNullException.ThrowIfNull(box);

        Footprint footprint = box.Footprint();
        Vector2 local = footprint.ToLocal(point);
        return local.Dx >= Length.Zero
               && local.Dx <= footprint.PlanWidth
               && local.Dy >= Length.Zero
               && local.Dy <= footprint.PlanHeight;
    }

    /// <summary>
    /// Whether a point is on what is left of the blank after its cuts: the blank itself for a
    /// plain rectangle, the derived outline for a shaped part
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.4).
    /// </summary>
    /// <remarks>
    /// Grips and snap targets deliberately stay on the blank; only "is this part under the
    /// pointer?" moves to the outline, so that a click in a corner somebody cut off picks whatever
    /// is really there.
    /// </remarks>
    public static bool ContainsShape(Box box, Point2 point)
    {
        ArgumentNullException.ThrowIfNull(box);

        return PlanShape.Contains(box, point);
    }

    /// <summary>
    /// Whether a point is on the shape, or near enough to it to count as a grab.
    /// </summary>
    /// <remarks>
    /// A plain rectangle can answer this exactly, because the distance to a rectangle is a
    /// two-line calculation. A shaped part's boundary is arcs as well as lines, and the distance
    /// to an arc is not worth a page of trigonometry for a click tolerance: the shape is asked
    /// about the point and about four points one tolerance away along each axis instead, which
    /// grabs a boundary from either side without pretending to a precision a few pixels of slop
    /// does not have.
    /// </remarks>
    /// <param name="box">The part.</param>
    /// <param name="point">Where the pointer is, in model coordinates.</param>
    /// <param name="tolerance">How far outside the shape still counts.</param>
    public static bool IsWithinShape(Box box, Point2 point, Length tolerance)
    {
        ArgumentNullException.ThrowIfNull(box);

        if (box.Cuts.IsEmpty || !PlanShape.ShowsCap(box))
        {
            return DistanceOutside(box, point) <= tolerance;
        }

        if (PlanShape.Contains(box, point))
        {
            return true;
        }

        if (tolerance <= Length.Zero)
        {
            return false;
        }

        foreach (Vector2 nudge in (Vector2[])
                 [
                     new(tolerance, Length.Zero),
                     new(-tolerance, Length.Zero),
                     new(Length.Zero, tolerance),
                     new(Length.Zero, -tolerance),
                 ])
        {
            if (PlanShape.Contains(box, point + nudge))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>How far a point is outside the box's footprint; zero when it is inside.</summary>
    public static Length DistanceOutside(Box box, Point2 point)
    {
        ArgumentNullException.ThrowIfNull(box);

        Footprint footprint = box.Footprint();
        Vector2 local = footprint.ToLocal(point);
        Length overX = Length.Max(-local.Dx, local.Dx - footprint.PlanWidth);
        Length overY = Length.Max(-local.Dy, local.Dy - footprint.PlanHeight);
        return Length.Max(Length.Max(overX, Length.Zero), Length.Max(overY, Length.Zero));
    }

    /// <summary>The area of a box's footprint, as a count of square 1/1024&#x2033; units.</summary>
    public static double Area(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        Footprint footprint = box.Footprint();
        return (double)footprint.PlanWidth.Units * footprint.PlanHeight.Units;
    }

    /// <summary>
    /// What a point has hold of on a selected box: a corner handle, then an edge handle, then the
    /// body, then nothing.
    /// </summary>
    /// <remarks>
    /// Corners win over edges, and handles win over the body, because a handle is smaller than
    /// what it sits on and the smaller target is always the one a person aimed at.
    /// </remarks>
    public static BoxGrip? GripAt(Box box, Point2 point, Length tolerance)
    {
        ArgumentNullException.ThrowIfNull(box);

        foreach (BoxGrip grip in CornerGrips)
        {
            if (Within(GripPoint(box, grip), point, tolerance))
            {
                return grip;
            }
        }

        foreach (BoxGrip grip in EdgeGrips)
        {
            if (Within(GripPoint(box, grip), point, tolerance))
            {
                return grip;
            }
        }

        // The body is the shape, not the blank: pressing where a cut took the material away is
        // pressing on the paper, even when the part it belonged to is the one selected (§2.4).
        return ContainsShape(box, point) ? BoxGrip.Body : null;
    }

    static bool Within(Point2 handle, Point2 point, Length tolerance) =>
        Length.Abs(point.X - handle.X) <= tolerance && Length.Abs(point.Y - handle.Y) <= tolerance;

    static Point2 Midpoint(Footprint footprint, BoxEdge edge)
    {
        (BoxCorner fromCorner, BoxCorner toCorner) = Box.Ends(edge);
        Point2 from = footprint.Corner(fromCorner);
        Point2 to = footprint.Corner(toCorner);
        return new Point2(
            from.X + (to.X - from.X).Divide(2, Rounding.HalfToEven),
            from.Y + (to.Y - from.Y).Divide(2, Rounding.HalfToEven));
    }
}

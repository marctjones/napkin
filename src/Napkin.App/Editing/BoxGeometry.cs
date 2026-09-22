using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>What a person grabbed on a selected part.</summary>
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
/// One axis-aligned edge of a box, as a line the rest of the drawing can be measured against.
/// </summary>
/// <param name="Edge">Which edge of the box, in the box's own frame.</param>
/// <param name="NormalAxis">The axis the edge's position varies along — X for a vertical edge.</param>
/// <param name="Coordinate">Where the edge sits along <paramref name="NormalAxis"/>.</param>
/// <param name="Low">The lower end of the edge along the other axis.</param>
/// <param name="High">The upper end of the edge along the other axis.</param>
public sealed record EdgeLine(BoxEdge Edge, Axis NormalAxis, Length Coordinate, Length Low, Length High);

/// <summary>
/// Where a box's corners, edges and handles are, and what a point on the canvas has hold of.
/// </summary>
/// <remarks>
/// Every method here is a pure function of a <see cref="Box"/> and exact <see cref="Length"/>s:
/// nothing in this file knows about pixels, and nothing in it produces a <see cref="Sketch"/>.
/// The canvas converts a pointer position to a model point and a tolerance to a model length, and
/// asks these questions in the model's own units.
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

        return grip switch
        {
            BoxGrip.Body => box.Center,
            BoxGrip.SouthWest => box.Corner(BoxCorner.SouthWest),
            BoxGrip.SouthEast => box.Corner(BoxCorner.SouthEast),
            BoxGrip.NorthEast => box.Corner(BoxCorner.NorthEast),
            BoxGrip.NorthWest => box.Corner(BoxCorner.NorthWest),
            BoxGrip.South => Midpoint(box, BoxEdge.South),
            BoxGrip.East => Midpoint(box, BoxEdge.East),
            BoxGrip.North => Midpoint(box, BoxEdge.North),
            BoxGrip.West => Midpoint(box, BoxEdge.West),
            _ => throw new ArgumentOutOfRangeException(nameof(grip), grip, "Unknown grip."),
        };
    }

    /// <summary>
    /// The edges a grip drags. A corner drags two, which is why a corner resize is one
    /// <see cref="Batch"/> of two <see cref="DragEdge"/>s rather than two separate edits.
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
    /// The displacement arrives in world coordinates and the edge is named in the box's frame, so
    /// it is rotated into that frame first. For the right-angle rotations this beta allows, that
    /// rotation is an exact swap of components and never rounds.
    /// </remarks>
    public static Length OutwardDelta(Box box, BoxEdge edge, Vector2 worldDelta)
    {
        ArgumentNullException.ThrowIfNull(box);

        Vector2 local = worldDelta.Rotate(-box.Rotation);
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
    /// The box's edges as lines, skipping any that is not axis-aligned. Every edge of a box rotated
    /// by a right-angle multiple is axis-aligned, so in this beta that is all four.
    /// </summary>
    public static IEnumerable<EdgeLine> AxisAlignedEdges(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        foreach (BoxEdge edge in (BoxEdge[])[BoxEdge.South, BoxEdge.East, BoxEdge.North, BoxEdge.West])
        {
            (BoxCorner fromCorner, BoxCorner toCorner) = Box.Ends(edge);
            Point2 from = box.Corner(fromCorner);
            Point2 to = box.Corner(toCorner);

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

    /// <summary>Whether a point is inside the box, corners included.</summary>
    public static bool Contains(Box box, Point2 point)
    {
        ArgumentNullException.ThrowIfNull(box);

        Vector2 local = (point - box.Anchor).Rotate(-box.Rotation);
        return local.Dx >= Length.Zero
               && local.Dx <= box.Width
               && local.Dy >= Length.Zero
               && local.Dy <= box.Height;
    }

    /// <summary>How far a point is outside the box; zero when it is inside.</summary>
    public static Length DistanceOutside(Box box, Point2 point)
    {
        ArgumentNullException.ThrowIfNull(box);

        Vector2 local = (point - box.Anchor).Rotate(-box.Rotation);
        Length overX = Length.Max(-local.Dx, local.Dx - box.Width);
        Length overY = Length.Max(-local.Dy, local.Dy - box.Height);
        return Length.Max(Length.Max(overX, Length.Zero), Length.Max(overY, Length.Zero));
    }

    /// <summary>The area of a box, as a count of square 1/1024&#x2033; units.</summary>
    public static double Area(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);
        return (double)box.Width.Units * box.Height.Units;
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

        return Contains(box, point) ? BoxGrip.Body : null;
    }

    static bool Within(Point2 handle, Point2 point, Length tolerance) =>
        Length.Abs(point.X - handle.X) <= tolerance && Length.Abs(point.Y - handle.Y) <= tolerance;

    static Point2 Midpoint(Box box, BoxEdge edge)
    {
        (BoxCorner fromCorner, BoxCorner toCorner) = Box.Ends(edge);
        Point2 from = box.Corner(fromCorner);
        Point2 to = box.Corner(toCorner);
        return new Point2(
            from.X + (to.X - from.X).Divide(2, Rounding.HalfToEven),
            from.Y + (to.Y - from.Y).Divide(2, Rounding.HalfToEven));
    }
}

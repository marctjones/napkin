using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// What the drawing says about itself, in words a person who has never used CAD can read.
/// </summary>
/// <remarks>
/// <para>
/// Relationships are stored, never inferred (docs/design/geometry-model.md &#xA7;3.2), which is
/// only worth anything if the person can see what got stored. This turns each one into a
/// sentence: "Part 2's left edge is flush with Part 1's right edge", not
/// <c>Flush(BoxEdgeRef(01a0…, East), BoxEdgeRef(01a1…, West))</c>.
/// </para>
/// <para>
/// Edges and corners are named in the part's own frame, which is what the model stores. For a
/// part that has not been turned — everything in this beta unless someone rotates one — that
/// frame is the page's, so "left edge" means the left edge on screen.
/// </para>
/// </remarks>
public static class RelationshipText
{
    /// <summary>One relationship, as a sentence.</summary>
    /// <param name="sketch">The drawing it belongs to, for reading values out of.</param>
    /// <param name="relationship">The relationship.</param>
    /// <param name="nameOf">What to call an entity.</param>
    /// <param name="format">How to write a length.</param>
    public static string Describe(
        Sketch sketch,
        Relationship relationship,
        Func<EntityId, string> nameOf,
        LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(nameOf);
        ArgumentNullException.ThrowIfNull(format);

        return relationship switch
        {
            Anchored anchored => $"{nameOf(anchored.Entity)} is pinned where it is.",

            Coincident coincident =>
                $"{Point(coincident.B, nameOf)} is at {Point(coincident.A, nameOf)}.",

            Flush flush =>
                $"{Edge(flush.B, nameOf)} is flush with {Edge(flush.A, nameOf)}.",

            ParamValue value =>
                $"{Size(value.Param, nameOf)} is {value.Value.Format(format).Text}.",

            EqualParam equal =>
                $"{Size(equal.B, nameOf)} is the same as {Size(equal.A, nameOf)}.",

            AxisDistance distance =>
                $"{Point(distance.To, nameOf)} is {Length.Abs(distance.Distance).Format(format).Text} "
                + $"{Direction(distance.Axis, distance.Distance)} {Point(distance.From, nameOf)}.",

            Centered centered =>
                $"{Point(centered.Middle, nameOf)} is centred between {Point(centered.A, nameOf)} "
                + $"and {Point(centered.B, nameOf)}, {Across(centered.Axis)}.",

            Horizontal horizontal => $"{Edge(horizontal.Edge, nameOf)} is level.",

            Vertical vertical => $"{Edge(vertical.Edge, nameOf)} is plumb.",

            // The kinds reserved for the solver (§3.2, table 2). A file can carry one; this build
            // cannot hold it, and saying which one is better than saying nothing.
            _ => $"{Kind(relationship)}, which this build cannot hold yet.",
        };
    }

    /// <summary>A point reference, as a phrase: "Part 2's bottom-left corner".</summary>
    public static string Point(PointRef reference, Func<EntityId, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(nameOf);

        return reference switch
        {
            CornerRef corner => $"{nameOf(corner.Box)}'s {CornerName(corner.Corner)} corner",
            CenterRef centre => $"{nameOf(centre.Box)}'s centre",
            NodeRef node => nameOf(node.Node),
            _ => "a point",
        };
    }

    /// <summary>An edge reference, as a phrase: "Part 2's left edge".</summary>
    public static string Edge(EdgeRef reference, Func<EntityId, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(nameOf);

        return reference switch
        {
            BoxEdgeRef edge => $"{nameOf(edge.Box)}'s {EdgeName(edge.Edge)} edge",
            SegmentRef segment => nameOf(segment.Segment),
            _ => "an edge",
        };
    }

    /// <summary>A size reference, as a phrase: "Part 1's width".</summary>
    public static string Size(ParamRef reference, Func<EntityId, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(nameOf);

        return reference switch
        {
            BoxWidthRef width => $"{nameOf(width.Box)}'s width",
            BoxHeightRef height => $"{nameOf(height.Box)}'s height",
            SegmentLengthRef length => $"{nameOf(length.Segment)}'s length",
            _ => "a size",
        };
    }

    /// <summary>The name of a relationship kind, in words.</summary>
    public static string Kind(Relationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        return relationship switch
        {
            Napkin.Core.Geometry.Parallel => "two edges running the same way",
            Perpendicular => "two edges at a right angle",
            AngleBetween => "two edges at a stated angle",
            Distance => "a straight-line distance between two points",
            PointOnEdge => "a point on an edge",
            Symmetric => "two points mirrored across an edge",
            Tangent => "two edges touching",
            Radius => "an arc's radius",
            _ => relationship.GetType().Name,
        };
    }

    static string CornerName(BoxCorner corner) => corner switch
    {
        BoxCorner.SouthWest => "bottom-left",
        BoxCorner.SouthEast => "bottom-right",
        BoxCorner.NorthEast => "top-right",
        BoxCorner.NorthWest => "top-left",
        _ => corner.ToString(),
    };

    static string EdgeName(BoxEdge edge) => edge switch
    {
        BoxEdge.South => "bottom",
        BoxEdge.East => "right",
        BoxEdge.North => "top",
        BoxEdge.West => "left",
        _ => edge.ToString(),
    };

    // Three ways, not two: in the plan "above" is +Y, so Z needs words of its own, and reading a Z
    // as a Y here would describe a height as a place on the page.
    static string Direction(Axis axis, Length distance) => axis switch
    {
        Axis.X => distance >= Length.Zero ? "right of" : "left of",
        Axis.Y => distance >= Length.Zero ? "above" : "below",
        Axis.Z => distance >= Length.Zero ? "higher than" : "lower than",
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    static string Across(Axis axis) => axis switch
    {
        Axis.X => "left to right",
        Axis.Y => "top to bottom",
        Axis.Z => "bottom to top",
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };
}

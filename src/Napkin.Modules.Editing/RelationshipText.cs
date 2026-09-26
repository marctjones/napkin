using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// What the drawing says about itself, in words a person who has never used CAD can read.
/// </summary>
/// <remarks>
/// <para>
/// Relationships are stored, never inferred (docs/design/geometry-model.md &#xA7;3.2), which is
/// only worth anything if the person can see what got stored. This turns each one into a
/// sentence: "Part 2's left edge is flush with Part 1's right edge", not
/// <c>Flush(FeatureRef(01a0…, Face(East)), FeatureRef(01a1…, Face(West)))</c>.
/// </para>
/// <para>
/// Faces, edges and corners are named by the way they face now, in compass words with up and down
/// only for height (<see cref="WorldWords"/>, #81): "Part 2's west face is flush with Part 1's
/// east face". The model stores them in the part's own frame; the words follow the part when it is
/// turned, so they always describe what is on the screen, in the plan (north up) and in 3D alike.
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
                $"{Place(sketch, coincident.B, nameOf)} is at {Place(sketch, coincident.A, nameOf)}.",

            Flush flush =>
                $"{Place(sketch, flush.B, nameOf)} is flush with {Place(sketch, flush.A, nameOf)}.",

            ParamValue value =>
                $"{Size(value.Param, nameOf)} is {value.Value.Format(format).Text}.",

            EqualParam equal =>
                $"{Size(equal.B, nameOf)} is the same as {Size(equal.A, nameOf)}.",

            AxisDistance distance =>
                $"{Place(sketch, distance.To, nameOf)} is {Length.Abs(distance.Distance).Format(format).Text} "
                + $"{WorldWords.Direction(distance.Axis, distance.Distance)} {Place(sketch, distance.From, nameOf)}.",

            Centered centered =>
                $"{Place(sketch, centered.Middle, nameOf)} is centred between {Place(sketch, centered.A, nameOf)} "
                + $"and {Place(sketch, centered.B, nameOf)}, {WorldWords.Across(centered.Axis)}.",

            Horizontal horizontal => $"{Place(sketch, horizontal.Edge, nameOf)} is level.",

            Vertical vertical => $"{Place(sketch, vertical.Edge, nameOf)} is plumb.",

            Joint joint => Napkin.Modules.Furniture.JointTooltip.Of(sketch, joint, nameOf),

            // The kinds reserved for the solver (§3.2, table 2). A file can carry one; this build
            // cannot hold it, and saying which one is better than saying nothing.
            _ => $"{Kind(relationship)}, which this build cannot hold yet.",
        };
    }

    /// <summary>A place, as a phrase: "Part 2's north-west corner", "Part 1's top face".</summary>
    public static string Place(Sketch sketch, PlaceRef reference, Func<EntityId, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(nameOf);

        return reference switch
        {
            FeatureRef feature => $"{nameOf(feature.Box)}'s {WorldWords.Feature(sketch.Find<Box>(feature.Box), feature.Feature)}",
            CenterRef centre => $"{nameOf(centre.Box)}'s centre",
            NodeRef node => nameOf(node.Node),
            SegmentRef segment => nameOf(segment.Segment),
            StrutEndRef end => $"{nameOf(end.Strut)}'s {(end.End == StrutEnd.From ? "first" : "second")} end",
            StrutFaceRef face => $"{nameOf(face.Strut)}'s {face.Face.ToString().ToLowerInvariant()} face",
            StrutEndFaceRef endFace => $"{nameOf(endFace.Strut)}'s {(endFace.End == StrutEnd.From ? "first" : "second")} end face",
            _ => "a point",
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
            BoxDepthRef depth => $"{nameOf(depth.Box)}'s depth",
            SegmentLengthRef length => $"{nameOf(length.Segment)}'s length",
            StrutHeightRef height => $"{nameOf(height.Strut)}'s height",
            StrutDepthRef depth => $"{nameOf(depth.Strut)}'s depth",
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
}

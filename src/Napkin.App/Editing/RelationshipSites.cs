using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// Which corners and edge lines of a blank a relationship holds on to.
/// </summary>
/// <remarks>
/// <see cref="Relationship.References"/> names the entities a relationship touches, which is what
/// validation, the remove cascade and the propagator need. The canvas needs one step finer —
/// <em>which</em> corner, <em>which</em> edge — so that it can mark the ones a cut has taken away
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.1). Reading it here rather than adding it to
/// the kernel keeps a drawing concern out of the model; the price is that a new relationship kind
/// carrying references has to be added to the two switches below, which is why every kind that
/// exists is listed rather than defaulted.
/// </remarks>
public static class RelationshipSites
{
    /// <summary>The blank corners a relationship refers to.</summary>
    public static IEnumerable<CornerRef> CornersOf(Relationship relationship) =>
        PointsOf(relationship).OfType<CornerRef>();

    /// <summary>The blank edge lines a relationship refers to.</summary>
    public static IEnumerable<BoxEdgeRef> EdgesOf(Relationship relationship) =>
        LinesOf(relationship).OfType<BoxEdgeRef>();

    static IEnumerable<PointRef> PointsOf(Relationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        return relationship switch
        {
            Coincident coincident => [coincident.A, coincident.B],
            AxisDistance distance => [distance.From, distance.To],
            Centered centered => [centered.Middle, centered.A, centered.B],
            Distance distance => [distance.A, distance.B],
            PointOnEdge on => [on.Point],
            Symmetric symmetric => [symmetric.A, symmetric.B],
            _ => [],
        };
    }

    static IEnumerable<EdgeRef> LinesOf(Relationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        return relationship switch
        {
            Flush flush => [flush.A, flush.B],
            Horizontal horizontal => [horizontal.Edge],
            Vertical vertical => [vertical.Edge],
            Napkin.Core.Geometry.Parallel parallel => [parallel.A, parallel.B],
            Perpendicular perpendicular => [perpendicular.A, perpendicular.B],
            AngleBetween angle => [angle.A, angle.B],
            PointOnEdge on => [on.Edge],
            Symmetric symmetric => [symmetric.Mirror],
            Tangent tangent => [tangent.A, tangent.B],
            _ => [],
        };
    }
}

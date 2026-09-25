using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// Which corners and edge lines of a blank a relationship holds on to.
/// </summary>
/// <remarks>
/// <see cref="Relationship.References"/> names the entities a relationship touches, which is what
/// validation, the remove cascade and the propagator need. The canvas needs one step finer —
/// <em>which</em> corner, <em>which</em> edge — so that it can mark the ones a cut has taken away
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.1). Reading it here rather than adding it to
/// the kernel keeps a drawing concern out of the model; the price is that a new relationship kind
/// carrying references has to be added to the switch below, which is why every kind that exists
/// is listed rather than defaulted. A corner is a local upright and an edge a side face
/// (<see cref="LocalFeatures"/>); any other feature — a vertex, a top face — is not something a cut
/// in the plan takes away, and is not listed.
/// </remarks>
public static class RelationshipSites
{
    /// <summary>The blank corners a relationship refers to.</summary>
    public static IEnumerable<(EntityId Box, BoxCorner Corner)> CornersOf(Relationship relationship)
    {
        foreach (FeatureRef feature in PlacesOf(relationship).OfType<FeatureRef>())
        {
            if (LocalFeatures.TryCorner(feature.Feature, out BoxCorner corner))
            {
                yield return (feature.Box, corner);
            }
        }
    }

    /// <summary>The blank edge lines a relationship refers to.</summary>
    public static IEnumerable<(EntityId Box, BoxEdge Edge)> EdgesOf(Relationship relationship)
    {
        foreach (FeatureRef feature in PlacesOf(relationship).OfType<FeatureRef>())
        {
            if (LocalFeatures.TryEdge(feature.Feature, out BoxEdge edge))
            {
                yield return (feature.Box, edge);
            }
        }
    }

    /// <summary>
    /// Every box feature a relationship refers to, whatever its dimension — the 3D view's question,
    /// which marks vertices and edges a cut took away as well as the plan's corners.
    /// </summary>
    public static IEnumerable<FeatureRef> FeaturesOf(Relationship relationship) =>
        PlacesOf(relationship).OfType<FeatureRef>();

    static IEnumerable<PlaceRef> PlacesOf(Relationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        return relationship switch
        {
            Coincident coincident => [coincident.A, coincident.B],
            AxisDistance distance => [distance.From, distance.To],
            Centered centered => [centered.Middle, centered.A, centered.B],
            Distance distance => [distance.A, distance.B],
            PointOnEdge on => [on.Point, on.Edge],
            Symmetric symmetric => [symmetric.A, symmetric.B, symmetric.Mirror],
            Flush flush => [flush.A, flush.B],
            Horizontal horizontal => [horizontal.Edge],
            Vertical vertical => [vertical.Edge],
            Napkin.Core.Geometry.Parallel parallel => [parallel.A, parallel.B],
            Perpendicular perpendicular => [perpendicular.A, perpendicular.B],
            AngleBetween angle => [angle.A, angle.B],
            Tangent tangent => [tangent.A, tangent.B],
            _ => [],
        };
    }
}

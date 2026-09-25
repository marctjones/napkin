using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// The blank's corners and edges as the plan canvas has always named them, as the features the
/// model now refers to (<c>docs/design/assembly-model.md</c> &#xA7;2.2): a corner of the blank is its
/// local upright, and an edge of the blank is the side face of the same name.
/// </summary>
/// <remarks>
/// This is the blank's own frame, which is the plan's only for a box lying as drawn — the only kind
/// the direct updater holds a positional relationship on before &#xA7;10 step 4. Going from a plan
/// corner of a tipped box to a feature is <see cref="Footprint.UprightAt"/>'s business, which is
/// how the canvas's snap names what it states (#70).
/// </remarks>
public static class LocalFeatures
{
    static readonly BoxCorner[] Corners = [BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest];
    static readonly BoxEdge[] Edges = [BoxEdge.South, BoxEdge.East, BoxEdge.North, BoxEdge.West];

    /// <summary>A corner of the blank, as a reference: its local upright.</summary>
    public static FeatureRef Corner(EntityId box, BoxCorner corner) => new(box, BoxFeature.LocalUpright(corner));

    /// <summary>An edge of the blank, as a reference: the side face of the same name.</summary>
    public static FeatureRef Edge(EntityId box, BoxEdge edge) => new(box, BoxFeature.Face(FaceOf(edge)));

    /// <summary>The corner of the blank whose local upright this feature is.</summary>
    public static bool TryCorner(BoxFeature feature, out BoxCorner corner)
    {
        foreach (BoxCorner candidate in Corners)
        {
            if (BoxFeature.LocalUpright(candidate) == feature)
            {
                corner = candidate;
                return true;
            }
        }

        corner = default;
        return false;
    }

    /// <summary>The edge of the blank whose side face this feature is.</summary>
    public static bool TryEdge(BoxFeature feature, out BoxEdge edge)
    {
        foreach (BoxEdge candidate in Edges)
        {
            if (BoxFeature.Face(FaceOf(candidate)) == feature)
            {
                edge = candidate;
                return true;
            }
        }

        edge = default;
        return false;
    }

    static BoxFace FaceOf(BoxEdge edge) => edge switch
    {
        BoxEdge.South => BoxFace.South,
        BoxEdge.East => BoxFace.East,
        BoxEdge.North => BoxFace.North,
        _ => BoxFace.West,
    };
}

using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// What a blank has lost at each of its sites, for the canvas to draw over
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.1).
/// </summary>
/// <remarks>
/// Every relationship binds to the blank, so a <see cref="Flush"/> can hold an edge line a curve
/// replaced and a <see cref="Coincident"/> can hold a corner a roundover took off. The canvas
/// says so by marking the corner and drawing the missing edge faintly, and these are the
/// questions it has to ask to do it. All of them are pure reads of <see cref="Box.Cuts"/> — the
/// derived <see cref="Outline"/> answers where the material is, and this answers where it is not.
/// </remarks>
public static class BlankShape
{
    /// <summary>The cut at one site, or <see langword="null"/> when the site is untouched.</summary>
    public static Cut? CutAt(Box box, CutSite site)
    {
        ArgumentNullException.ThrowIfNull(box);

        foreach (Cut cut in box.Cuts)
        {
            if (cut.Site == site)
            {
                return cut;
            }
        }

        return null;
    }

    /// <summary>The two edges of a blank that meet at a corner.</summary>
    public static ImmutableArray<BoxEdge> EdgesAt(BoxCorner corner) => corner switch
    {
        BoxCorner.SouthWest => [BoxEdge.South, BoxEdge.West],
        BoxCorner.SouthEast => [BoxEdge.South, BoxEdge.East],
        BoxCorner.NorthEast => [BoxEdge.North, BoxEdge.East],
        BoxCorner.NorthWest => [BoxEdge.North, BoxEdge.West],
        _ => throw new ArgumentOutOfRangeException(nameof(corner), corner, "Unknown corner."),
    };

    /// <summary>
    /// Whether a corner of the blank is no longer a corner of the shape.
    /// </summary>
    /// <remarks>
    /// A corner cut or a roundover takes it off directly. An <see cref="Bow.Outward"/> curve on
    /// either edge that meets there takes it off too, because the curve starts short of the
    /// corner on both sides; an <see cref="Bow.Inward"/> one runs corner to corner and keeps them
    /// (&#xA7;1.5).
    /// </remarks>
    public static bool IsVirtualCorner(Box box, BoxCorner corner)
    {
        ArgumentNullException.ThrowIfNull(box);

        if (CutAt(box, CutSite.Corner(corner)) is not null)
        {
            return true;
        }

        foreach (BoxEdge edge in EdgesAt(corner))
        {
            if (CutAt(box, CutSite.Edge(edge)) is CurvedEdge { Bow: Bow.Outward })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// How much of one edge the cuts have taken off its end at a given corner: zero when the
    /// blank's edge still reaches that corner.
    /// </summary>
    /// <remarks>
    /// A <see cref="CornerCut"/> claims the setback that runs along this edge, a
    /// <see cref="RoundedCorner"/> claims its radius, and an outward curve on the <em>other</em>
    /// edge at this corner claims its depth along this one — which is invariant 8's budget read
    /// from the other side (&#xA7;1.6).
    /// </remarks>
    public static Length SetbackAlong(Box box, BoxCorner corner, BoxEdge edge)
    {
        ArgumentNullException.ThrowIfNull(box);

        switch (CutAt(box, CutSite.Corner(corner)))
        {
            case CornerCut cut:
                return RunsAlongX(edge) ? cut.AlongX : cut.AlongY;

            case RoundedCorner rounded:
                return rounded.Radius;
        }

        ImmutableArray<BoxEdge> meeting = EdgesAt(corner);
        BoxEdge other = edge == meeting[0] ? meeting[1] : meeting[0];
        return CutAt(box, CutSite.Edge(other)) is CurvedEdge { Bow: Bow.Outward } curve
            ? curve.Depth
            : Length.Zero;
    }

    /// <summary>Whether an edge runs along the blank's local X.</summary>
    static bool RunsAlongX(BoxEdge edge) => edge is BoxEdge.South or BoxEdge.North;
}

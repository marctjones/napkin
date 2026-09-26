using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// What to do to a blank, in the words a person uses at a bench
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;4.4).
/// </summary>
/// <remarks>
/// <para>
/// This lives beside <see cref="CutList"/> rather than in the geometry kernel because the
/// vocabulary of ends, edges and radii is the furniture module's: the kernel stores a
/// <see cref="Cut"/> and derives an outline from it, and says nothing about how to mark one out.
/// </para>
/// <para>
/// Corners and edges are named by <strong>compass, in the part's own frame as drawn</strong> —
/// "the north-east corner", "the north edge" — because that is the one frame the file, the
/// references and the canvas already share, and the person at the bench holds the part the way
/// they drew it.
/// </para>
/// <para>
/// Every length is rendered by <see cref="CutListCsv.Text"/>, so a size that is not exact at
/// 1/16&#x2033; carries the same &#x2248; marker the rest of the list uses. A derived angle or
/// radius carries it too, unless the derivation came out exact.
/// </para>
/// </remarks>
public static class CutDescription
{
    /// <summary>
    /// One sentence per cut, or per group of cuts that read the same at different sites.
    /// </summary>
    /// <param name="cuts">The blank's cuts, in site order. Empty gives no sentences at all.</param>
    /// <param name="size">
    /// The part's three finished dimensions &#x2014; the blank's, never a bounding box
    /// (&#xA7;4.1). With <paramref name="planAxes"/> these give the length of each edge a cut is
    /// measured along, which is what tells a mitre from a clipped corner and what a curve's radius
    /// is worked out from.
    /// </param>
    /// <param name="planAxes">
    /// Which of the three dimensions the box's stored width and height are, so that a sentence can
    /// say which edge is which and whether the cut goes through the thickness.
    /// </param>
    public static ImmutableArray<string> Describe(
        ImmutableArray<Cut> cuts,
        FinishedSize size,
        PlanAxes planAxes)
        => cuts.IsDefaultOrEmpty ? [] : Describe(cuts, size, planAxes, setbacksExact: true, compound: []);

    /// <summary>
    /// The same, for a blank derived from a strut (<c>docs/design/angled-parts.md</c> &#xA7;2.2&#x2013;&#xA7;2.3):
    /// its plain mitres are <paramref name="cuts"/>, marked &#x2248; unless their setbacks were proven
    /// exact, and its compound ends are said after them, west first.
    /// </summary>
    /// <param name="cuts">The blank's plain cuts, in site order.</param>
    /// <param name="size">The part's three finished dimensions.</param>
    /// <param name="planAxes">Which dimension is the blank's local X, Y and Z.</param>
    /// <param name="setbacksExact">Whether every setback was proven to be on the grid; false marks each one &#x2248;.</param>
    /// <param name="compound">The ends cut at a mitre and a bevel, west first.</param>
    public static ImmutableArray<string> Describe(
        ImmutableArray<Cut> cuts,
        FinishedSize size,
        PlanAxes planAxes,
        bool setbacksExact,
        ImmutableArray<DerivedCompoundEnd> compound)
    {
        if (cuts.IsEmpty && compound.IsEmpty)
        {
            return [];
        }

        Length planWidth = Value(planAxes.X, size);
        Length planHeight = Value(planAxes.Y, size);
        PartDimension outOfPlane = planAxes.OutOfPlane;

        // A cut is square through the plan (§1.4), so when the dimension it runs through is the
        // thickness there is nothing to add: a cut through the thickness is what "cut" means. When
        // it is not — a rounded corner on a leg drawn as its footprint — the sentence has to say
        // that the cut runs the whole of the other dimension, or it reads as a nick in the end.
        string through = outOfPlane == PartDimension.Thickness
            ? string.Empty
            : $", for the full {CutListCsv.Text(Value(outOfPlane, size))} {Word(outOfPlane)}";

        // Like cuts are said once: a sentence is built with a placeholder where its sites go, and
        // cuts whose sentences are otherwise identical share one.
        List<(string Template, List<string> Sites)> grouped = [];
        foreach (Cut cut in cuts)
        {
            (string template, string site) = Sentence(cut, planWidth, planHeight, through, setbacksExact);

            int at = grouped.FindIndex(group => string.Equals(group.Template, template, StringComparison.Ordinal));
            if (at < 0)
            {
                grouped.Add((template, [site]));
            }
            else
            {
                grouped[at].Sites.Add(site);
            }
        }

        return
        [
            .. grouped.Select(group => group.Template.Contains(SitesPlaceholder, StringComparison.Ordinal)
                ? group.Template.Replace(SitesPlaceholder, Sites(group.Sites), StringComparison.Ordinal)
                : group.Template),
            .. Compound(compound, through),
        ];
    }

    /// <summary>
    /// The compound ends, in the words of &#xA7;2.2: the mitre across the wide face and the bevel
    /// through it, both off square to the nearest half degree, with the face the board lies on and
    /// where the long point is. Two ends cut alike are said once, naming both long points.
    /// </summary>
    private static IEnumerable<string> Compound(ImmutableArray<DerivedCompoundEnd> ends, string through)
    {
        string Setting(DerivedCompoundEnd end) => end.Mitre is { Degrees: 0, Exact: true }
            ? $"bevel {AngleText(end.Bevel.Degrees, end.Bevel.Exact)}"
            : $"mitre {AngleText(end.Mitre.Degrees, end.Mitre.Exact)}, bevel {AngleText(end.Bevel.Degrees, end.Bevel.Exact)}";

        if (ends is [var west, var east] && string.Equals(Setting(west), Setting(east), StringComparison.Ordinal))
        {
            yield return $"Cut both ends at a compound angle: {Setting(west)}, with the top face on the saw table; "
                + $"long point at the {LongPoint(west.LongPoint)} of the west end and the {LongPoint(east.LongPoint)} "
                + $"of the east end{through}.";
            yield break;
        }

        foreach (DerivedCompoundEnd end in ends)
        {
            yield return $"Cut the {(end.End == BlankEnd.West ? "west" : "east")} end at a compound angle: {Setting(end)}, "
                + $"with the top face on the saw table; long point at the {LongPoint(end.LongPoint)}{through}.";
        }
    }

    /// <summary>
    /// A long point in the blank's own compass: "south-bottom corner", or "bottom edge" when the cut
    /// has no mitre and the long point runs the whole width. A compound end always tilts through the
    /// depth, so it always has a bottom or a top.
    /// </summary>
    private static string LongPoint(StrutCorner corner)
    {
        string through = corner.Z < 0 ? "bottom" : "top";
        return corner.Y switch
        {
            < 0 => $"south-{through} corner",
            > 0 => $"north-{through} corner",
            _ => $"{through} edge",
        };
    }

    /// <summary>Where a sentence's list of sites goes, when the sentence can hold more than one.</summary>
    private const string SitesPlaceholder = "{sites}";

    /// <summary>One cut as a sentence, with the site it was made at kept separate for grouping.</summary>
    /// <remarks>
    /// The kernel's <see cref="Cut"/> hierarchy is closed — three kinds, and a constructor no
    /// fourth can be added to from outside the kernel — so what is left after the first two is the
    /// curved edge. A fourth kind would fail loudly here rather than be described wrongly.
    /// </remarks>
    private static (string Template, string Site) Sentence(
        Cut cut, Length planWidth, Length planHeight, string through, bool setbacksExact)
    {
        if (cut is RoundedCorner rounded)
        {
            return (
                $"Round {SitesPlaceholder} to a {CutListCsv.Text(rounded.Radius)} radius{through}.",
                CornerName(rounded.Corner));
        }

        if (cut is CornerCut corner)
        {
            return Straight(corner, planWidth, planHeight, through, setbacksExact);
        }

        CurvedEdge curve = (CurvedEdge)cut;
        return (Curve(curve, planWidth, planHeight, through), EdgeName(curve.Edge));
    }

    /// <summary>
    /// A straight cut across a corner: a clipped corner, a mitred end, a taper, or the diagonal.
    /// </summary>
    /// <remarks>
    /// Which of the three it is, is read off the setbacks rather than stored: a setback that is the
    /// whole of its edge means that edge is gone, which is what a mitre or a taper is, and both
    /// setbacks whole means the cut runs corner to corner.
    /// </remarks>
    private static (string Template, string Site) Straight(
        CornerCut cut, Length planWidth, Length planHeight, string through, bool setbacksExact = true)
    {
        BoxEdge alongX = XEdge(cut.Corner);
        BoxEdge alongY = YEdge(cut.Corner);
        bool xIsWhole = cut.AlongX == planWidth;
        bool yIsWhole = cut.AlongY == planHeight;

        if (xIsWhole && yIsWhole)
        {
            return (
                $"Cut corner to corner, from the {CornerName(FarEnd(cut.Corner, alongX))} corner "
                + $"to the {CornerName(FarEnd(cut.Corner, alongY))} corner{through}.",
                CornerName(cut.Corner));
        }

        if (xIsWhole || yIsWhole)
        {
            // The whole setback names the end that is cut away; the short one is the mark the cut
            // is laid out from, on the edge that survives.
            BoxEdge end = xIsWhole ? alongX : alongY;
            BoxEdge from = xIsWhole ? alongY : alongX;
            Length setback = xIsWhole ? cut.AlongY : cut.AlongX;
            Length whole = xIsWhole ? planWidth : planHeight;

            // A derived setback that was rounded onto the grid is marked whatever it reads as, and
            // so is the angle worked back from it (angled-parts §2.4): only a proven 45° is exact.
            return (
                $"Mitre the {EdgeName(end)} end: from {Marked(setback, setbacksExact)} in along the "
                + $"{EdgeName(from)} edge to the {CornerName(FarEnd(cut.Corner, end))} corner "
                + $"({OffSquare(setback, whole, setbacksExact)} off square){through}.",
                CornerName(cut.Corner));
        }

        // A square clip reads the same at every corner, so several of them are one sentence; an
        // uneven one has to name which edge carries which mark, and so stands alone.
        return cut.AlongX == cut.AlongY
            ? (
                $"Cut off {SitesPlaceholder}: mark {CutListCsv.Text(cut.AlongX)} along each edge "
                + $"from the corner, and cut between the marks{through}.",
                CornerName(cut.Corner))
            : (
                $"Cut off the {CornerName(cut.Corner)} corner: mark {CutListCsv.Text(cut.AlongX)} "
                + $"along the {EdgeName(alongX)} edge and {CutListCsv.Text(cut.AlongY)} along the "
                + $"{EdgeName(alongY)} edge, and cut between the marks{through}.",
                CornerName(cut.Corner));
    }

    /// <summary>One whole edge replaced by the curve a jigsaw and a thin batten produce.</summary>
    private static string Curve(CurvedEdge curve, Length planWidth, Length planHeight, string through)
    {
        Length edge = RunsAlongX(curve.Edge) ? planWidth : planHeight;
        string radius = $"({ArcRadius(edge, curve.Depth)} radius)";

        return curve.Bow == Bow.Outward
            ? $"Curve the {EdgeName(curve.Edge)} edge: mark {CutListCsv.Text(curve.Depth)} in from "
              + $"each end on the {Adjacent(curve.Edge)} edges, draw a fair curve from mark to mark "
              + $"through the middle of the {EdgeName(curve.Edge)} edge, and cut it {radius}{through}."
            : $"Scallop the {EdgeName(curve.Edge)} edge: mark {CutListCsv.Text(curve.Depth)} in at "
              + $"the middle, draw a fair curve from corner to corner through the mark, and cut it "
              + $"{radius}{through}.";
    }

    /// <summary>
    /// The angle a mitre is cut at, off square, to the nearest half degree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cut is laid out from two marks and the angle is derived from them &#x2014; the choice
    /// &#xA7;1.3 made, since only one of the two can be exact on the grid. So the angle is marked
    /// &#x2248; unless it is exact, and equal setbacks are the only case that is: by Niven's
    /// theorem the tangent of any other whole or half degree is irrational, so no other pair of
    /// exact lengths lands on one.
    /// </para>
    /// <para>
    /// This is the one place the module leaves the grid for <see cref="double"/>. Nothing stored
    /// depends on the answer; it is a number on a saw's scale.
    /// </para>
    /// </remarks>
    /// <param name="setback">The short setback: the mark on the edge that survives.</param>
    /// <param name="whole">The edge the cut runs the whole of.</param>
    /// <param name="exact">Whether the setback was proven exact; a rounded one is never 45° exactly.</param>
    private static string OffSquare(Length setback, Length whole, bool exact)
        => AngleText(Math.Atan2(setback.Units, whole.Units) * 180 / Math.PI, exact && setback == whole);

    /// <summary>
    /// A derived angle on a saw's scale: the nearest half degree, a half rounded away from zero, and
    /// &#x2248; unless the angle was proven exact (<c>docs/design/angled-parts.md</c> &#xA7;2.4). The one
    /// place the rule is written; a box's mitre and a strut's mitre and bevel all read it.
    /// </summary>
    /// <param name="degrees">The angle, worked out in <see cref="double"/>; display only.</param>
    /// <param name="exact">Whether it was proven exact in integers.</param>
    public static string AngleText(double degrees, bool exact)
    {
        double halves = Math.Round(degrees * 2, MidpointRounding.AwayFromZero) / 2;
        string text = halves.ToString(halves == Math.Floor(halves) ? "0" : "0.0", CultureInfo.InvariantCulture) + "°";
        return exact ? text : CutListCsv.Approximately + text;
    }

    /// <summary>A length as <see cref="CutListCsv.Text"/> says it, marked &#x2248; as well when it was rounded onto the grid.</summary>
    private static string Marked(Length length, bool exact)
    {
        string text = CutListCsv.Text(length);
        return exact || text.StartsWith(CutListCsv.Approximately, StringComparison.Ordinal) ? text : CutListCsv.Approximately + text;
    }

    /// <summary>
    /// The radius of the arc a curved edge is: the circle through the two ends of the chord and the
    /// point <c>depth</c> off its middle.
    /// </summary>
    /// <remarks>
    /// With half the chord <c>a</c> and the rise <c>d</c>, <c>R = (a&#xB2; + d&#xB2;) / 2d</c>,
    /// which on the unit grid is <c>(L&#xB2; + 4d&#xB2;) / 8d</c> with no division until the last
    /// step. Both bows have the same chord &#x2014; the edge's own length &#x2014; and the same
    /// rise, so one formula does for both. It is exact surprisingly often (a 4&#x2032; edge bowed
    /// 1&#x2033; is exactly 288&#xBD;&#x2033;), and it says so when it is.
    /// </remarks>
    /// <param name="edge">The length of the edge the curve replaces.</param>
    /// <param name="depth">How far the curve reaches off the chord.</param>
    private static string ArcRadius(Length edge, Length depth)
    {
        Int128 chord = edge.Units;
        Int128 rise = depth.Units;
        Int128 numerator = (chord * chord) + (4 * rise * rise);
        Int128 denominator = 8 * rise;

        Int128 units = numerator / denominator;
        Int128 remainder = numerator - (units * denominator);
        if (2 * remainder >= denominator)
        {
            units++;
        }

        FormattedLength radius = new Length((long)units).Format(LengthFormat.Default);
        return remainder == 0 && radius.IsExact
            ? radius.Text
            : CutListCsv.Approximately + radius.Text;
    }

    /// <summary>The sites of one sentence, as the phrase it reads them out in.</summary>
    private static string Sites(IReadOnlyList<string> sites) => sites.Count switch
    {
        1 => $"the {sites[0]} corner",
        4 => "all four corners",
        _ => $"the {string.Join(", ", sites.Take(sites.Count - 1))} and {sites[^1]} corners",
    };

    /// <summary>
    /// What the out-of-plane dimension is called in a sentence. Only ever asked when it is not the
    /// thickness, because that is the one case the sentence says nothing about.
    /// </summary>
    private static string Word(PartDimension dimension)
        => dimension == PartDimension.Length ? "length" : "width";

    /// <summary>The value of one of the three named dimensions.</summary>
    private static Length Value(PartDimension dimension, FinishedSize size) => dimension switch
    {
        PartDimension.Length => size.Length,
        PartDimension.Width => size.Width,
        _ => size.Thickness,
    };

    /// <summary>A corner, by compass.</summary>
    private static string CornerName(BoxCorner corner) => corner switch
    {
        BoxCorner.SouthWest => "south-west",
        BoxCorner.SouthEast => "south-east",
        BoxCorner.NorthEast => "north-east",
        BoxCorner.NorthWest => "north-west",
        _ => throw new ArgumentOutOfRangeException(nameof(corner), corner, "Unknown corner."),
    };

    /// <summary>An edge, by compass.</summary>
    private static string EdgeName(BoxEdge edge) => edge switch
    {
        BoxEdge.South => "south",
        BoxEdge.East => "east",
        BoxEdge.North => "north",
        BoxEdge.West => "west",
        _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Unknown edge."),
    };

    /// <summary>The two edges a curved edge's marks are made on: the ones it meets at its ends.</summary>
    private static string Adjacent(BoxEdge edge)
        => RunsAlongX(edge) ? "east and west" : "north and south";

    /// <summary>Whether an edge runs along the box's local X.</summary>
    private static bool RunsAlongX(BoxEdge edge) => edge is BoxEdge.South or BoxEdge.North;

    /// <summary>The corner's edge that runs along local X, which its <c>AlongX</c> setback is on.</summary>
    private static BoxEdge XEdge(BoxCorner corner)
        => corner is BoxCorner.SouthWest or BoxCorner.SouthEast ? BoxEdge.South : BoxEdge.North;

    /// <summary>The corner's edge that runs along local Y, which its <c>AlongY</c> setback is on.</summary>
    private static BoxEdge YEdge(BoxCorner corner)
        => corner is BoxCorner.SouthWest or BoxCorner.NorthWest ? BoxEdge.West : BoxEdge.East;

    /// <summary>The other end of one of a corner's edges: where a whole-edge setback lands.</summary>
    private static BoxCorner FarEnd(BoxCorner corner, BoxEdge edge)
    {
        (BoxCorner from, BoxCorner to) = Box.Ends(edge);
        return corner == from ? to : from;
    }
}

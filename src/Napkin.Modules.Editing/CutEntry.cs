using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>What the shape workshop's cut fields say, typed by a person.</summary>
/// <param name="First">The first field: a setback, a radius or a depth.</param>
/// <param name="Second">The second setback, for a corner cut.</param>
/// <param name="Angle">The angle off square, for a corner cut.</param>
public sealed record TypedCut(string? First, string? Second, string? Angle);

/// <summary>What the shape workshop's cut fields show for one cut, before anyone types.</summary>
/// <param name="Headline">Which cut this is, in words.</param>
/// <param name="FirstCaption">The first field's caption.</param>
/// <param name="First">The first field's value.</param>
/// <param name="SecondCaption">The second field's caption, for a corner cut; otherwise null.</param>
/// <param name="Second">The second field's value, for a corner cut; otherwise null.</param>
/// <param name="Angle">The angle the corner cut reads as, for a corner cut; otherwise null.</param>
/// <param name="Readout">A sentence on what the numbers mean.</param>
public sealed record CutFieldsText(
    string Headline,
    string FirstCaption,
    string First,
    string? SecondCaption,
    string? Second,
    string? Angle,
    string Readout)
{
    /// <summary>Whether this is a corner cut, which has a second setback, an angle and a full mitre.</summary>
    public bool IsCorner => Second is not null;
}

/// <summary>What typing into the cut fields comes to: a changed cut, or why nothing changed.</summary>
public abstract record CutEntryOutcome
{
    private protected CutEntryOutcome()
    {
    }
}

/// <summary>The cut as the fields now say, and the sentence undo and the status line use.</summary>
/// <param name="Cut">The changed cut.</param>
/// <param name="What">What changed, in words.</param>
public sealed record CutEdit(Cut Cut, string What) : CutEntryOutcome;

/// <summary>The fields did not say a cut; nothing changes, and this is why.</summary>
/// <param name="Why">The explanation, for the workshop to show.</param>
public sealed record CutEntryProblem(string Why) : CutEntryOutcome;

/// <summary>
/// The shape workshop's words and its reading of the cut fields, with no window in them
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;7.1, &#xA7;7.2).
/// </summary>
/// <remarks>
/// A corner cut has two descriptions and only one of them can be exact (&#xA7;1.3), so the fields
/// offer both and one of them wins: when the angle has been edited it is the angle, converted to the
/// setback it implies with one explicit rounding, keeping the longer setback as the edge that angle
/// is measured against; otherwise it is the two lengths, taken as typed. Text that is not a length
/// or an angle is explained and changes nothing at all.
/// </remarks>
public static class CutEntry
{
    /// <summary>What the hint line says while the drawing has nothing more particular to say.</summary>
    public const string DefaultHint =
        "Drag a corner to clip it, Shift for 45°, Alt to round it; drag an edge's middle to "
        + "curve it. Delete takes the selected cut off.";

    /// <summary>The hint line: the drawing's own hint as a sentence, or <see cref="DefaultHint"/>.</summary>
    public static string Hint(string drawingHint) =>
        drawingHint.Length > 0 ? drawingHint + "." : DefaultHint;

    /// <summary>The workshop's headline: which part, and the blank it is cut from.</summary>
    public static string Headline(string partName, Box blank, LengthFormat format) =>
        $"Shaping {partName} — {Text(blank.Width, format)} × {Text(blank.Height, format)} blank";

    /// <summary>The cut list's headline, with how many cuts there are.</summary>
    public static string CutsHeadline(int count) => $"Cuts — {count}";

    /// <summary>One cut as the workshop's list writes it: the site, the kind, and the numbers.</summary>
    public static string Summary(Cut cut, LengthFormat format) => cut switch
    {
        CornerCut clip =>
            $"{BlankShape.Words(cut.Site)} — clip {Text(clip.AlongX, format)} × {Text(clip.AlongY, format)}",
        RoundedCorner rounded =>
            $"{BlankShape.Words(cut.Site)} — round, {Text(rounded.Radius, format)} radius",
        CurvedEdge { Bow: Bow.Inward } scallop =>
            $"{BlankShape.Words(cut.Site)} — scallop {Text(scallop.Depth, format)} deep",
        _ => $"{BlankShape.Words(cut.Site)} — curve {Text(((CurvedEdge)cut).Depth, format)} deep",
    };

    /// <summary>What the cut fields show for <paramref name="cut"/>.</summary>
    public static CutFieldsText Fields(Cut cut, LengthFormat format)
    {
        string headline = $"Cut at the {BlankShape.Words(cut.Site)}";
        return cut switch
        {
            CornerCut clip => new CutFieldsText(
                headline,
                $"{BlankShape.Compass(XEdge(clip.Corner))} edge",
                clip.AlongX.Format(format).Text,
                $"{BlankShape.Compass(YEdge(clip.Corner))} edge",
                clip.AlongY.Format(format).Text,
                AngleOf(clip),
                "Two marks, one on each edge. Typing an angle instead keeps the longer "
                + "mark and works the shorter one out from it, rounding once."),
            RoundedCorner rounded => new CutFieldsText(
                headline,
                "Radius",
                rounded.Radius.Format(format).Text,
                null,
                null,
                null,
                "A quarter circle, tangent to both edges that meet here."),
            _ => CurveFields(headline, (CurvedEdge)cut, format),
        };
    }

    /// <summary>
    /// Reads the typed fields as a change to <paramref name="cut"/> on <paramref name="blank"/>.
    /// </summary>
    public static CutEntryOutcome Read(Box blank, Cut cut, TypedCut typed, LengthFormat format) => cut switch
    {
        CornerCut clip => ReadCorner(blank, clip, typed, format),
        RoundedCorner rounded => ReadLength(typed.First) is Length radius
            ? new CutEdit(rounded with { Radius = radius }, $"Set {blank.Name}'s {cut.Site} to a {Text(radius, format)} radius")
            : Problem("A radius"),
        _ => ReadLength(typed.First) is Length depth
            ? new CutEdit((CurvedEdge)cut with { Depth = depth }, $"Set {blank.Name}'s {cut.Site} to {Text(depth, format)} deep")
            : Problem("A depth"),
    };

    /// <summary>Both setbacks at the blank's width, once: the full mitre at <paramref name="corner"/>.</summary>
    public static CutEdit FullMitre(Box blank, BoxCorner corner, LengthFormat format)
    {
        CornerCut mitre = CutAngle.FullMitre(blank, corner);
        return new CutEdit(mitre, $"Mitred {blank.Name}'s {mitre.Site} the full {Text(mitre.AlongX, format)}");
    }

    /// <summary>The corner's edge that runs along the blank's local X.</summary>
    public static BoxEdge XEdge(BoxCorner corner) =>
        corner is BoxCorner.SouthWest or BoxCorner.SouthEast ? BoxEdge.South : BoxEdge.North;

    /// <summary>The corner's edge that runs along the blank's local Y.</summary>
    public static BoxEdge YEdge(BoxCorner corner) =>
        corner is BoxCorner.SouthWest or BoxCorner.NorthWest ? BoxEdge.West : BoxEdge.East;

    /// <summary>A length as the workshop writes it: marked <c>&#x2248;</c> unless it is exact.</summary>
    public static string Text(Length length, LengthFormat format)
    {
        FormattedLength formatted = length.Format(format);
        return formatted.IsExact ? formatted.Text : CutAngle.Approximately + formatted.Text;
    }

    static CutFieldsText CurveFields(string headline, CurvedEdge curve, LengthFormat format) => new(
        headline,
        "Depth",
        curve.Depth.Format(format).Text,
        null,
        null,
        null,
        curve.Bow == Bow.Inward
            ? "A scallop: the corners stay and the middle goes in by this much."
            : "A bow: the middle stays and the corners come in by this much.");

    static CutEntryOutcome ReadCorner(Box blank, CornerCut clip, TypedCut typed, LengthFormat format)
    {
        bool angleEdited = !string.Equals((typed.Angle ?? string.Empty).Trim(), AngleOf(clip), StringComparison.Ordinal);
        if (angleEdited && CutAngle.TryParseDegrees(typed.Angle, out double degrees))
        {
            Length longer = Length.Max(clip.AlongX, clip.AlongY);
            if (CutAngle.SetbackFor(longer, degrees) is not { } shorter)
            {
                return new CutEntryProblem("A cut is made at more than 0° and less than 90° off square.");
            }

            CornerCut angled = clip.AlongX >= clip.AlongY
                ? clip with { AlongY = shorter }
                : clip with { AlongX = shorter };
            return new CutEdit(angled, $"Set {blank.Name}'s {clip.Site} to {CutAngle.Text(shorter, longer)} off square");
        }

        if (ReadLength(typed.First) is not Length alongX || ReadLength(typed.Second) is not Length alongY)
        {
            return Problem("A setback");
        }

        return new CutEdit(
            clip with { AlongX = alongX, AlongY = alongY },
            $"Set {blank.Name}'s {clip.Site} to {Text(alongX, format)} by {Text(alongY, format)}");
    }

    static string AngleOf(CornerCut clip) => CutAngle.Text(
        Length.Min(clip.AlongX, clip.AlongY),
        Length.Max(clip.AlongX, clip.AlongY));

    /// <summary>A length out of a cut field, or nothing when it does not read as one greater than zero.</summary>
    static Length? ReadLength(string? field) =>
        DimensionEntry.Interpret(field) is ReadableLength readable && readable.Value > Length.Zero
            ? readable.Value
            : null;

    static CutEntryProblem Problem(string what) =>
        new($"{what} has to be a length greater than zero, like 3/4\" or 1' 4 1/4\".");
}

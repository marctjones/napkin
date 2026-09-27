using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.Interop.Pdf;

/// <summary>How a scale says itself in words, after its ratio.</summary>
public enum ScaleWords
{
    /// <summary>An inch of paper and the length it stands for, in the canvas's words: 1" = 1'-0".</summary>
    Ratio,

    /// <summary>An architect scale: paper per foot of the design, 1/4" = 1'-0".</summary>
    Architect,

    /// <summary>An engineer (civil) scale: feet of the design per inch of paper, 1" = 20'.</summary>
    Engineer,
}

/// <summary>
/// A drawing scale, 1:<see cref="Denominator"/>: one inch on the sheet stands for
/// <see cref="Denominator"/> inches of the design. A part measured on the printed sheet and
/// multiplied by the denominator gives its true length (#25).
/// </summary>
/// <param name="Denominator">Inches of design per inch of paper; 1 or more.</param>
/// <param name="Words">How it says itself after its ratio.</param>
public readonly record struct SheetScale(int Denominator, ScaleWords Words = ScaleWords.Ratio)
{
    /// <summary>PDF user space: 72 points to the inch, as Excise.Core's <c>PageSize</c> states it ("1 pt = 1/72 inch").</summary>
    public const double PointsPerPaperInch = 72;

    /// <summary>
    /// The ratios a sheet picks from, largest first — napkin's own list, not a standard's: full, half,
    /// quarter and eighth size for furniture, then 1:12, 1:16, 1:24, 1:48 and 1:96 (one inch, three
    /// quarters, a half, a quarter and an eighth of an inch of paper to the foot) for rooms and
    /// buildings. Past the last, each further scale halves the one before.
    /// </summary>
    public static IReadOnlyList<SheetScale> Ratios { get; } =
        [new(1), new(2), new(4), new(8), new(12), new(16), new(24), new(48), new(96)];

    /// <summary>
    /// The architect scales, largest first, as USFA/FEMA's "Using Engineer and Architect Scales"
    /// (usfa.fema.gov/downloads/pdf/nfa/engineer-architect-scales.pdf, p. 2, read 2026-09-27) lists
    /// them: 1 1/2", 1", 3/4", 1/2", 3/8", 1/4", 3/16", 1/8" and 3/32" of paper to the foot — 1:8, 1:12,
    /// 1:16, 1:24, 1:32, 1:48, 1:64, 1:96 and 1:128. For structures and buildings (the same page).
    /// </summary>
    public static IReadOnlyList<SheetScale> Architect { get; } =
        [.. ((int[])[8, 12, 16, 24, 32, 48, 64, 96, 128]).Select(denominator => new SheetScale(denominator, ScaleWords.Architect))];

    /// <summary>
    /// The engineer (civil) scales, largest first, from the same page: 1" of paper to 10, 20, 30, 40, 50
    /// and 60 feet — 1:120 to 1:720. For roads, water mains and topographical features (the same page):
    /// a site plan.
    /// </summary>
    public static IReadOnlyList<SheetScale> Engineer { get; } =
        [.. ((int[])[120, 240, 360, 480, 600, 720]).Select(denominator => new SheetScale(denominator, ScaleWords.Engineer))];

    /// <summary>Points on the sheet for one inch of the design.</summary>
    public double PointsPerInch => PointsPerPaperInch / Denominator;

    /// <summary>What the title block prints first, e.g. 1:12.</summary>
    public string Label => "1:" + Denominator.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The scale said in words: an inch of paper and what it stands for in the canvas's own words
    /// (1" = 1'-0" at 1:12), or an architect's paper per foot (1/4" = 1'-0"), or an engineer's feet per
    /// inch (1" = 20').
    /// </summary>
    /// <param name="format">How lengths are written, as the canvas writes them.</param>
    public string InWords(LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return Words switch
        {
            ScaleWords.Architect => $"{PaperPerFoot()}\" = 1'-0\"",
            ScaleWords.Engineer => $"1\" = {(Denominator / 12.0).ToString("0.##", CultureInfo.InvariantCulture)}'",
            _ => $"{Length.Inches(1).Format(format).Text} = {Length.Inches(Denominator).Format(format).Text}",
        };
    }

    /// <summary>Inches of paper to a foot of the design, as a mixed fraction: 12 / denominator, 1 1/2 at 1:8, 3/32 at 1:128.</summary>
    string PaperPerFoot()
    {
        int whole = 12 / Denominator, rest = 12 % Denominator, divisor = Gcd(rest, Denominator);
        string fraction = rest == 0 ? string.Empty : $"{rest / divisor}/{Denominator / divisor}";
        return whole == 0 ? fraction : rest == 0 ? $"{whole}" : $"{whole} {fraction}";
    }

    static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    /// <summary>
    /// The largest scale no larger than <paramref name="pointsPerInch"/>: the first of
    /// <see cref="Ratios"/> that fits, or the last halved until it does.
    /// </summary>
    /// <param name="pointsPerInch">The most points of paper an inch of the design may take.</param>
    public static SheetScale AtMost(double pointsPerInch)
    {
        if (!(pointsPerInch > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(pointsPerInch), pointsPerInch, "A scale needs a positive size to fit.");
        }

        foreach (SheetScale scale in Ratios)
        {
            if (scale.PointsPerInch <= pointsPerInch)
            {
                return scale;
            }
        }

        SheetScale smaller = Ratios[^1];
        while (smaller.PointsPerInch > pointsPerInch)
        {
            smaller = new SheetScale(checked(smaller.Denominator * 2));
        }

        return smaller;
    }

    /// <summary>
    /// The largest of a list of scales no larger than <paramref name="pointsPerInch"/>; null when even
    /// the smallest is too large — a drawing that does not fit the paper at any scale on the list.
    /// </summary>
    /// <param name="scales">The scales to choose from, in any order.</param>
    /// <param name="pointsPerInch">The most points of paper an inch of the design may take.</param>
    public static SheetScale? Largest(IEnumerable<SheetScale> scales, double pointsPerInch)
    {
        ArgumentNullException.ThrowIfNull(scales);
        SheetScale[] fitting = [.. scales.Where(scale => scale.PointsPerInch <= pointsPerInch).OrderBy(scale => scale.Denominator)];
        return fitting.Length == 0 ? null : fitting[0];
    }
}

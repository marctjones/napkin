using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.Interop.Pdf;

/// <summary>
/// A drawing scale, 1:<see cref="Denominator"/>: one inch on the sheet stands for
/// <see cref="Denominator"/> inches of the design. A part measured on the printed sheet and
/// multiplied by the denominator gives its true length (#25).
/// </summary>
/// <param name="Denominator">Inches of design per inch of paper; 1 or more.</param>
public readonly record struct SheetScale(int Denominator)
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

    /// <summary>Points on the sheet for one inch of the design.</summary>
    public double PointsPerInch => PointsPerPaperInch / Denominator;

    /// <summary>What the title block prints first, e.g. 1:12.</summary>
    public string Label => "1:" + Denominator.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The scale said as a length of paper and what it stands for, in the canvas's own words:
    /// 1" = 1'-0" at 1:12.
    /// </summary>
    /// <param name="format">How lengths are written, as the canvas writes them.</param>
    public string InWords(LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return $"{Length.Inches(1).Format(format).Text} = {Length.Inches(Denominator).Format(format).Text}";
    }

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
}

using System.Globalization;

namespace Napkin.Interop.Pdf;

/// <summary>
/// A drawing scale, 1:<see cref="Denominator"/>: one inch on the sheet stands for
/// <see cref="Denominator"/> inches of the design. A part measured on the printed sheet and
/// multiplied by the denominator gives its true length (#25).
/// </summary>
/// <param name="Denominator">Inches of design per inch of paper; 1 or more.</param>
public readonly record struct SheetScale(int Denominator)
{
    /// <summary>PDF user space: 72 points to the inch.</summary>
    public const double PointsPerPaperInch = 72;

    /// <summary>
    /// The scales a sheet picks from, largest first: full, half, quarter and eighth size for
    /// furniture, then the 1″ = 1′, ¾″ = 1′, ½″ = 1′, ¼″ = 1′ and ⅛″ = 1′ ratios for
    /// rooms and buildings. Past the last, each further scale halves the one before.
    /// </summary>
    public static IReadOnlyList<SheetScale> Standard { get; } =
        [new(1), new(2), new(4), new(8), new(12), new(16), new(24), new(48), new(96)];

    /// <summary>Points on the sheet for one inch of the design.</summary>
    public double PointsPerInch => PointsPerPaperInch / Denominator;

    /// <summary>What the title block prints, e.g. 1:8.</summary>
    public string Label => "1:" + Denominator.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The largest scale at which every extent fits the space, in points. One scale serves every
    /// view, so the sheets of a set can be read against each other.
    /// </summary>
    /// <param name="extents">Each view's extent, in inches of the design; empty views are skipped.</param>
    /// <param name="width">The width available, in points.</param>
    /// <param name="height">The height available, in points.</param>
    public static SheetScale Fit(IEnumerable<DrawingExtent> extents, double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        DrawingExtent[] all = [.. extents];
        if (all.Any(extent => !double.IsFinite(extent.Width) || !double.IsFinite(extent.Height)))
        {
            throw new ArgumentException("A view reaches infinitely far; no scale fits it.", nameof(extents));
        }

        bool Fits(SheetScale scale) => all.All(extent =>
            extent.Width * scale.PointsPerInch <= width && extent.Height * scale.PointsPerInch <= height);

        foreach (SheetScale scale in Standard)
        {
            if (Fits(scale))
            {
                return scale;
            }
        }

        SheetScale smaller = Standard[^1];
        while (!Fits(smaller))
        {
            smaller = new SheetScale(checked(smaller.Denominator * 2));
        }

        return smaller;
    }
}

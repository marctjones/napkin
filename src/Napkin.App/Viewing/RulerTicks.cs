using System.Collections.Immutable;
using System.Globalization;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>One mark on a ruler.</summary>
/// <param name="Units">Where it is, in 1/1024&#x2033; units: exact, so a tick is never off the grid.</param>
/// <param name="Major">Whether it is a step of the heavier grid, drawn longer.</param>
/// <param name="Label">What it reads, in feet, inches and fractions; <see langword="null"/> when there is no room for it.</param>
public readonly record struct RulerTick(long Units, bool Major, string? Label)
{
    /// <summary>Where it is, in inches.</summary>
    public double Inches => (double)Units / Length.UnitsPerInch;
}

/// <summary>
/// Where the marks on a ruler go and what they say: pure, so a test reads them without a window.
/// </summary>
/// <remarks>
/// <para>
/// <strong>A ruler is the grid, written down.</strong> Its marks are the plan's grid lines —
/// <see cref="SnapGrid.StepInches"/> for the fine ones and <see cref="SnapGrid.CoarserStepInches"/>
/// for the heavy — so a tick, a grid line and the step a drag lands on are always the same number
/// (<c>SnapGrid</c>'s own stance). They are worked out in whole 1/1024&#x2033; units, so a mark at 1/4&#x2033;
/// is exactly there and a label never shows a rounding artefact.
/// </para>
/// <para>
/// <strong>Labels only where they fit.</strong> The heavy marks are labelled, left to right, and one is
/// skipped when it would sit closer than <see cref="DefaultLabelGapPixels"/> to the last one written.
/// </para>
/// </remarks>
public static class RulerTicks
{
    /// <summary>The closest two labels are written, in pixels.</summary>
    public const double DefaultLabelGapPixels = 64;

    /// <summary>More marks than this in one ruler is a mistake in the caller, not a ruler.</summary>
    const int MostTicks = 4000;

    /// <summary>The marks of a ruler that shows <paramref name="lowInches"/> to <paramref name="highInches"/>.</summary>
    /// <param name="lowInches">The value at the ruler's low end, in inches.</param>
    /// <param name="highInches">The value at the high end, in inches.</param>
    /// <param name="pixelsPerInch">The zoom; it picks the step.</param>
    /// <param name="labelGapPixels">The closest two labels may be.</param>
    public static ImmutableArray<RulerTick> Ticks(double lowInches, double highInches, double pixelsPerInch, double labelGapPixels = DefaultLabelGapPixels)
    {
        if (!double.IsFinite(lowInches) || !double.IsFinite(highInches) || highInches <= lowInches || pixelsPerInch <= 0)
        {
            return [];
        }

        double minor = SnapGrid.StepInches(pixelsPerInch);
        double major = SnapGrid.CoarserStepInches(minor, pixelsPerInch);
        long minorUnits = SnapGrid.UnitsPerStep(minor);
        long majorUnits = major > 0 ? SnapGrid.UnitsPerStep(major) : 0;

        long first = (long)Math.Ceiling(lowInches * Length.UnitsPerInch / minorUnits);
        long last = (long)Math.Floor(highInches * Length.UnitsPerInch / minorUnits);
        if (last - first > MostTicks)
        {
            return [];
        }

        ImmutableArray<RulerTick>.Builder ticks = ImmutableArray.CreateBuilder<RulerTick>((int)Math.Max(0, last - first + 1));
        double lastLabelled = double.NegativeInfinity;
        for (long index = first; index <= last; index++)
        {
            long units = index * minorUnits;
            bool isMajor = majorUnits > 0 && units % majorUnits == 0;

            // With no heavy step on the ladder at this zoom, the fine marks are all there is to label.
            bool labelled = isMajor || majorUnits == 0;
            string? label = null;
            double at = units * pixelsPerInch / Length.UnitsPerInch;
            if (labelled && at - lastLabelled >= labelGapPixels)
            {
                label = Label(units);
                lastLabelled = at;
            }

            ticks.Add(new RulerTick(units, isMajor, label));
        }

        return ticks.ToImmutable();
    }

    /// <summary>How a mark reads: the display format of a length, at 1/16&#x2033;.</summary>
    public static string Label(long units) => new Length(units).Format(LengthFormat.Default).Text;
}

/// <summary>
/// The scale bar of an orthographic 3D view: a length on the ladder, and how wide it is drawn.
/// </summary>
/// <remarks>
/// A ruler is meaningless in perspective, where an inch is a different size at every distance, so the 3D
/// view has a scale bar instead, and only when it is orthographic — where an inch along an axis is the same
/// number of pixels anywhere (<c>docs/design/assembly-model.md</c> §11 decision 10).
/// <strong>The bar is true across the screen</strong>, at <c>PixelsPerInch</c>: a length along a world axis
/// is foreshortened by the view (about 0.82 in the isometric one), so it looks shorter than the bar for the
/// same length. Axis-aligned marks would fix that and are not built.
/// </remarks>
public static class ScaleBar
{
    /// <summary>The narrowest a bar is drawn, in pixels.</summary>
    public const double MinimumPixels = 60;

    /// <summary>The bar for a zoom: the shortest step on the grid ladder that is at least <see cref="MinimumPixels"/> wide.</summary>
    /// <returns>Its length in inches, its width in pixels, and its label.</returns>
    public static (double Inches, double Pixels, string Label) Choose(double pixelsPerInch)
    {
        double inches = SnapGrid.Ladder[^1];
        foreach (double step in SnapGrid.Ladder)
        {
            if (step * pixelsPerInch >= MinimumPixels)
            {
                inches = step;
                break;
            }
        }

        return (inches, inches * pixelsPerInch, RulerTicks.Label(SnapGrid.UnitsPerStep(inches)));
    }

    /// <summary>The label alone, for a test that reads what is on screen.</summary>
    public static string LabelFor(double pixelsPerInch) => Choose(pixelsPerInch).Label;

    /// <summary>The width in pixels as text, for a failure message.</summary>
    public static string Describe(double pixelsPerInch) =>
        string.Create(CultureInfo.InvariantCulture, $"{Choose(pixelsPerInch).Label} = {Choose(pixelsPerInch).Pixels:0.#}px");
}

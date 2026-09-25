using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// The drawing grid: which step the canvas is on at a given zoom, and how a point lands on it.
/// </summary>
/// <remarks>
/// <para>
/// One ladder serves both jobs, which is the point of this type. The lines you can see and the
/// positions a drag lands on are the same numbers, so "it snapped to the grid" means "it snapped
/// to the grid you are looking at" — a grid that snapped to a step it did not draw would be a
/// drawing tool lying about where things are.
/// </para>
/// <para>
/// Steps are plain numbers a person would use: a quarter inch, a half, an inch, three, six, a
/// foot, and so on up to a thousand feet. The step in force is the finest one whose lines are
/// still <see cref="MinimumSpacingPixels"/> apart, so zooming in gives a finer grid without
/// anyone choosing one.
/// </para>
/// </remarks>
public static class SnapGrid
{
    /// <summary>The closest two grid lines are allowed to be drawn, in pixels.</summary>
    public const double MinimumSpacingPixels = 14;

    /// <summary>The grid steps, in inches, finest first.</summary>
    public static readonly ImmutableArray<double> Ladder =
    [
        0.25, 0.5, 1, 3, 6, 12, 24, 48, 96, 144, 288, 600, 1200, 2400, 6000, 12000,
    ];

    /// <summary>The grid step in force at a zoom, in inches.</summary>
    public static double StepInches(double pixelsPerInch)
    {
        foreach (double step in Ladder)
        {
            if (step * pixelsPerInch >= MinimumSpacingPixels)
            {
                return step;
            }
        }

        return Ladder[^1];
    }

    /// <summary>
    /// The heavier grid step drawn over the finer one, or zero when nothing on the ladder is far
    /// enough apart to be worth drawing.
    /// </summary>
    public static double CoarserStepInches(double minorStepInches, double pixelsPerInch)
    {
        foreach (double step in Ladder)
        {
            if (step >= minorStepInches * 4 && step * pixelsPerInch >= MinimumSpacingPixels * 4)
            {
                return step;
            }
        }

        return 0;
    }

    /// <summary>A grid step as a whole number of 1/1024&#x2033; units, so snapping never rounds.</summary>
    public static long UnitsPerStep(double stepInches) =>
        Math.Max(1, (long)Math.Round(stepInches * Length.UnitsPerInch));

    /// <summary>The nearest grid multiple to a length, half away from zero.</summary>
    public static Length Snap(Length value, double stepInches) =>
        new(RoundToMultiple(value.Units, UnitsPerStep(stepInches)));

    /// <summary>The nearest grid intersection to a point.</summary>
    public static Point2 Snap(Point2 point, double stepInches) =>
        new(Snap(point.X, stepInches), Snap(point.Y, stepInches));

    static long RoundToMultiple(long units, long step)
    {
        long remainder = units % step;
        if (remainder == 0)
        {
            return units;
        }

        long down = units - remainder;
        long magnitude = Math.Abs(remainder);

        // Half away from zero: the display rule (docs/design/geometry-model.md §1.4), applied here
        // because a snapped position is a position a person reads off the drawing.
        if (magnitude * 2 < step)
        {
            return down;
        }

        return units < 0 ? down - step : down + step;
    }
}

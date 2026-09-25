using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>A grid line of a standard view, at a world coordinate.</summary>
/// <param name="World">Where it is, in inches of the world axis it is measured along.</param>
/// <param name="Major">Whether it is drawn with the heavier pen.</param>
public readonly record struct GridLine(double World, bool Major);

/// <summary>
/// The rulers and the grid of a standard view (docs/design/standard-views.md §5.2), as numbers:
/// which world values a stretch of the screen shows along each axis, where a world value sits on the
/// screen's axis, and the grid lines on the plan's ladder, with the floor in an elevation always heavy.
/// </summary>
/// <remarks>
/// A ruler carries world values, so where the screen runs against the world axis — Back's and Left's
/// horizontal, Bottom's vertical — its numbers count down as the screen coordinate grows. That is the
/// projection being honest; only the placement mirrors, never the values.
/// </remarks>
public static class ViewGrid
{
    /// <summary>More lines than this along one axis is a caller's mistake, not a grid.</summary>
    const int MostLines = 4000;

    /// <summary>
    /// The world values a stretch of a view axis shows, low to high, given the view coordinates at its
    /// two ends (a view coordinate being the world one times the axis's sign).
    /// </summary>
    public static (double Low, double High) WorldSpan(SignedAxis axis, double from, double to)
    {
        double a = axis.Sign * from, b = axis.Sign * to;
        return (Math.Min(a, b), Math.Max(a, b));
    }

    /// <summary>Where a world value sits along a view axis, in view coordinates.</summary>
    public static double InView(SignedAxis axis, double world) => axis.Sign * world;

    /// <summary>
    /// The grid lines along a world axis between two world values, at every step of the plan's ladder
    /// for this zoom (<see cref="SnapGrid.StepInches"/>), heavy on the next step up
    /// (<see cref="SnapGrid.CoarserStepInches"/>). In an elevation the line at z = 0 is heavy whatever the
    /// ladder says: it is the floor, the cheapest depth cue there is (§5.2).
    /// </summary>
    public static IReadOnlyList<GridLine> Lines(StandardView view, Axis axis, double low, double high, double pixelsPerInch)
    {
        if (!double.IsFinite(low) || !double.IsFinite(high) || high < low || pixelsPerInch <= 0)
        {
            return [];
        }

        double minor = SnapGrid.StepInches(pixelsPerInch);
        double major = SnapGrid.CoarserStepInches(minor, pixelsPerInch);
        long first = (long)Math.Ceiling(low / minor), last = (long)Math.Floor(high / minor);
        if (last - first > MostLines)
        {
            return [];
        }

        bool floor = axis == Axis.Z && StandardViewFrame.IsElevation(view);
        List<GridLine> lines = [];
        for (long index = first; index <= last; index++)
        {
            double world = index * minor;
            bool isMajor = (major > 0 && IsMultiple(world, major)) || (floor && index == 0);
            lines.Add(new GridLine(world, isMajor));
        }

        return lines;
    }

    static bool IsMultiple(double value, double step)
    {
        double ratio = value / step;
        return Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
    }
}

using System.Collections.Immutable;

namespace Napkin.Modules.Editing;

/// <summary>One level of the grid as drawn: its step, how strongly it is drawn, and whether it is the heavier pen.</summary>
/// <param name="StepInches">The step between its lines, in inches.</param>
/// <param name="Opacity">How strongly it is drawn, 0 to 1.</param>
/// <param name="Major">Whether it is drawn with the heavier pen.</param>
public readonly record struct GridLevel(double StepInches, double Opacity, bool Major)
{
    /// <summary>How far apart its lines are on the screen at a zoom, in pixels.</summary>
    public double SpacingPixels(double pixelsPerInch) => StepInches * pixelsPerInch;
}

/// <summary>
/// Which grid levels are drawn at a zoom, and how strongly (#114): never more than two adjacent levels
/// of the one ladder the snap uses (<see cref="SnapGrid.Ladder"/>), the minor one fading in as it opens
/// up so that zooming never makes a wall of lines pop into view.
/// </summary>
/// <remarks>
/// The minor level is always <see cref="SnapGrid.StepInches"/>, never a finer or coarser one: what a drag
/// lands on and what is drawn stay the same numbers. It is drawn faint when its lines are close, at
/// <see cref="SnapGrid.MinimumSpacingPixels"/>, and at full strength once they are
/// <see cref="FullSpacingPixels"/> apart; it never fades out entirely, because a grid a drag snaps to
/// but nobody can see would lie about where things land. The major level, one step up, is always full.
/// </remarks>
public static class GridLevels
{
    /// <summary>The spacing at which the minor level reaches full strength, in pixels.</summary>
    public const double FullSpacingPixels = SnapGrid.MinimumSpacingPixels * 2;

    /// <summary>The faintest the minor level is drawn, at its closest.</summary>
    public const double FaintestOpacity = 0.35;

    /// <summary>The levels to draw at a zoom, minor first; one only at the top of the ladder.</summary>
    /// <param name="pixelsPerInch">The zoom.</param>
    public static ImmutableArray<GridLevel> Of(double pixelsPerInch)
    {
        if (!double.IsFinite(pixelsPerInch) || pixelsPerInch <= 0)
        {
            return [];
        }

        double minor = SnapGrid.StepInches(pixelsPerInch);
        GridLevel fine = new(minor, Fade(minor * pixelsPerInch), Major: false);
        double major = SnapGrid.CoarserStepInches(minor, pixelsPerInch);
        return major > 0 ? [fine, new GridLevel(major, 1, Major: true)] : [fine];
    }

    /// <summary>The minor level's strength at a spacing: faint at the closest, full from twice that.</summary>
    /// <param name="spacingPixels">How far apart its lines are.</param>
    public static double Fade(double spacingPixels)
    {
        double t = (spacingPixels - SnapGrid.MinimumSpacingPixels) / (FullSpacingPixels - SnapGrid.MinimumSpacingPixels);
        return FaintestOpacity + ((1 - FaintestOpacity) * Math.Clamp(t, 0, 1));
    }

    /// <summary>
    /// How strongly a ground-grid line in 3D is drawn at a distance from the grid's centre: full at the
    /// centre, fading to <see cref="FaintestOpacity"/> at the grid's edge, so the floor reads as a floor
    /// and not as a cage around the drawing.
    /// </summary>
    /// <param name="distance">The line's distance from the centre.</param>
    /// <param name="radius">The distance from the centre to the grid's edge.</param>
    public static double GroundFade(double distance, double radius)
    {
        if (radius <= 0)
        {
            return 1;
        }

        return 1 - ((1 - FaintestOpacity) * Math.Clamp(Math.Abs(distance) / radius, 0, 1));
    }
}

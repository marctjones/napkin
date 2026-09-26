namespace Napkin.Modules.Editing;

/// <summary>
/// One pane of the sheet (docs/design/standard-views.md §11): which view it shows and where, in
/// pixels from the sheet's top-left corner, y down.
/// </summary>
/// <param name="View">The standard view it is locked to; null for the free 3D pane.</param>
/// <param name="Left">Its left edge.</param>
/// <param name="Top">Its top edge.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
public sealed record SheetPane(StandardView? View, double Left, double Top, double Width, double Height)
{
    /// <summary>Its right edge.</summary>
    public double Right => Left + Width;

    /// <summary>Its bottom edge.</summary>
    public double Bottom => Top + Height;
}

/// <summary>
/// Where the sheet's panes go and the one scale its three drawings share (standard-views §11.2,
/// §11.4): third-angle, Top above Front and Right to the right of Front, with the free 3D view in
/// the spare top-right corner.
/// </summary>
/// <remarks>
/// Pixels are <c>double</c>s: this lays out a window, never measures the design. The PDF sheet (#25)
/// can take its arrangement from here; first-angle, if it is ever built, is a second arrangement here.
/// </remarks>
public static class SheetLayout
{
    /// <summary>The space between panes, in pixels.</summary>
    public const double Gutter = 8;

    /// <summary>
    /// The four panes of a sheet the given size, in reading order — Top, 3D, Front, Right — each a
    /// quarter of what the gutters leave.
    /// </summary>
    /// <param name="width">The sheet's width.</param>
    /// <param name="height">The sheet's height.</param>
    /// <param name="gutter">The space between panes.</param>
    public static IReadOnlyList<SheetPane> Panes(double width, double height, double gutter = Gutter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(gutter);

        double paneWidth = Math.Max((width - gutter) / 2, 0), paneHeight = Math.Max((height - gutter) / 2, 0);
        double rightColumn = paneWidth + gutter, lowerRow = paneHeight + gutter;
        return
        [
            new SheetPane(StandardView.Top, 0, 0, paneWidth, paneHeight),
            new SheetPane(null, rightColumn, 0, paneWidth, paneHeight),
            new SheetPane(StandardView.Front, 0, lowerRow, paneWidth, paneHeight),
            new SheetPane(StandardView.Right, rightColumn, lowerRow, paneWidth, paneHeight),
        ];
    }

    /// <summary>
    /// The one scale, in pixels per inch, at which every drawing fits its pane with a margin all round:
    /// the smallest of the scales each would fit at alone, so the three line up (§11.4).
    /// </summary>
    /// <param name="needs">Each locked pane with the design's extent across and up it, in inches.</param>
    /// <param name="marginFraction">How much of each edge to leave empty, as a fraction of the pane.</param>
    /// <returns>The shared scale; null when nothing has an extent to fit.</returns>
    public static double? SharedScale(IEnumerable<(SheetPane Pane, double AcrossInches, double UpInches)> needs, double marginFraction)
    {
        ArgumentNullException.ThrowIfNull(needs);
        double? shared = null;
        foreach ((SheetPane pane, double across, double up) in needs)
        {
            double usableWidth = Math.Max(pane.Width * (1 - (2 * marginFraction)), 1);
            double usableHeight = Math.Max(pane.Height * (1 - (2 * marginFraction)), 1);
            double scale = Math.Min(
                across > 1e-9 ? usableWidth / across : double.PositiveInfinity,
                up > 1e-9 ? usableHeight / up : double.PositiveInfinity);
            if (double.IsFinite(scale))
            {
                shared = shared is { } smaller ? Math.Min(smaller, scale) : scale;
            }
        }

        return shared;
    }
}

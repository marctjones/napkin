using Napkin.Modules.Editing;

namespace Napkin.Interop.Pdf;

/// <summary>
/// A point of a standard view's plane, in inches of the design: <paramref name="Along"/> the view's
/// screen-right axis and <paramref name="Across"/> its screen-up axis — the frame the hidden-edge
/// split and the view dimensions already share (docs/design/standard-views.md §2.3, §3.2).
/// </summary>
/// <param name="Along">Inches along screen right.</param>
/// <param name="Across">Inches along screen up.</param>
public readonly record struct DrawingPoint(double Along, double Across);

/// <summary>One line of a view, drawn as the line table (#134) says its kind is drawn.</summary>
/// <param name="Kind">Visible or hidden: an edge of a part.</param>
/// <param name="From">One end.</param>
/// <param name="To">The other end.</param>
/// <param name="Dashed">
/// A visible outline whose layer's style is dashed — an opening, which is a cut rather than a part —
/// drawn in <see cref="DrawingLines.DashedOutline"/>, as the standard views draw it.
/// </param>
public readonly record struct DrawingLine(LineKind Kind, DrawingPoint From, DrawingPoint To, bool Dashed = false);

/// <summary>
/// A dimension as a view lays it out: what is measured, where its line runs, and the label the
/// canvas shows for it, unchanged, so the screen and the sheet cannot read differently.
/// </summary>
/// <param name="Label">What the dimension reads, e.g. 2'-0".</param>
/// <param name="From">One measured end.</param>
/// <param name="To">The other measured end.</param>
/// <param name="LineFrom">Where the dimension line starts.</param>
/// <param name="LineTo">Where it ends.</param>
public sealed record DrawingDimension(
    string Label,
    DrawingPoint From,
    DrawingPoint To,
    DrawingPoint LineFrom,
    DrawingPoint LineTo);

/// <summary>One standard view of a design, ready for paper: which view, its lines and its dimensions.</summary>
/// <param name="View">Which of the six it is.</param>
/// <param name="Lines">Every edge it draws, hidden ones included when they are shown.</param>
/// <param name="Dimensions">Every dimension it shows.</param>
public sealed record DrawingView(StandardView View, IReadOnlyList<DrawingLine> Lines, IReadOnlyList<DrawingDimension> Dimensions)
{
    /// <summary>What the pane's caption calls it: the view's own name.</summary>
    public string Name => StandardViewFrame.Name(View);

    /// <summary>
    /// The smallest rectangle, in inches of the design, holding every line and every dimension's
    /// ends and line; null for a view with nothing in it.
    /// </summary>
    public DrawingExtent? Extent()
    {
        DrawingExtent? extent = null;
        foreach (DrawingLine line in Lines)
        {
            extent = DrawingExtent.Grow(DrawingExtent.Grow(extent, line.From), line.To);
        }

        foreach (DrawingDimension dimension in Dimensions)
        {
            foreach (DrawingPoint point in (DrawingPoint[])[dimension.From, dimension.To, dimension.LineFrom, dimension.LineTo])
            {
                extent = DrawingExtent.Grow(extent, point);
            }
        }

        return extent;
    }
}

/// <summary>A rectangle of a view's plane, in inches of the design.</summary>
/// <param name="Left">The least <see cref="DrawingPoint.Along"/>.</param>
/// <param name="Bottom">The least <see cref="DrawingPoint.Across"/>.</param>
/// <param name="Right">The greatest <see cref="DrawingPoint.Along"/>.</param>
/// <param name="Top">The greatest <see cref="DrawingPoint.Across"/>.</param>
public readonly record struct DrawingExtent(double Left, double Bottom, double Right, double Top)
{
    /// <summary>Inches from left to right.</summary>
    public double Width => Right - Left;

    /// <summary>Inches from bottom to top.</summary>
    public double Height => Top - Bottom;

    /// <summary>An extent grown to hold a point; a point's own extent when there was none.</summary>
    public static DrawingExtent Grow(DrawingExtent? extent, DrawingPoint point) => extent is { } grown
        ? new DrawingExtent(
            Math.Min(grown.Left, point.Along),
            Math.Min(grown.Bottom, point.Across),
            Math.Max(grown.Right, point.Along),
            Math.Max(grown.Top, point.Across))
        : new DrawingExtent(point.Along, point.Across, point.Along, point.Across);
}

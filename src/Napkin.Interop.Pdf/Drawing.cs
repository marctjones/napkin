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
/// <param name="Kind">Visible, hidden, dimension, extension or centre.</param>
/// <param name="From">One end.</param>
/// <param name="To">The other end.</param>
public readonly record struct DrawingLine(LineKind Kind, DrawingPoint From, DrawingPoint To);

/// <summary>
/// A dimension as a view lays it out: what is measured, where its line runs, and the label the
/// canvas shows for it, unchanged, so the screen and the sheet cannot read differently.
/// </summary>
/// <param name="Label">What the dimension reads, e.g. 2′ 1 1⁄2″.</param>
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

/// <summary>One standard view of a design, ready for paper: its name, its lines and its dimensions.</summary>
/// <param name="Name">What the title block calls it, e.g. Front.</param>
/// <param name="Lines">Every line the view draws.</param>
/// <param name="Dimensions">Every dimension the view shows.</param>
public sealed record DrawingView(string Name, IReadOnlyList<DrawingLine> Lines, IReadOnlyList<DrawingDimension> Dimensions)
{
    /// <summary>
    /// The smallest rectangle, in inches of the design, holding every line and every dimension's
    /// line; null for a view with nothing in it.
    /// </summary>
    public DrawingExtent? Extent()
    {
        IEnumerable<DrawingPoint> points = Lines.SelectMany(line => (DrawingPoint[])[line.From, line.To])
            .Concat(Dimensions.SelectMany(dimension =>
                (DrawingPoint[])[dimension.From, dimension.To, dimension.LineFrom, dimension.LineTo]));
        DrawingExtent? extent = null;
        foreach (DrawingPoint point in points)
        {
            extent = extent is { } grown
                ? new DrawingExtent(
                    Math.Min(grown.Left, point.Along),
                    Math.Min(grown.Bottom, point.Across),
                    Math.Max(grown.Right, point.Along),
                    Math.Max(grown.Top, point.Across))
                : new DrawingExtent(point.Along, point.Across, point.Along, point.Across);
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
}

/// <summary>What every sheet's title block says (DESIGN.md §7: the disclaimer travels with the sheet).</summary>
/// <param name="ProjectName">The design's name.</param>
/// <param name="Date">The day the sheet was made.</param>
/// <param name="Disclaimer">The tool's scope disclaimer, printed in full.</param>
public sealed record TitleBlock(string ProjectName, DateOnly Date, string Disclaimer);

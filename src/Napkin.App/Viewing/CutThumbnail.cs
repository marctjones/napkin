using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Napkin.App.Designs;
using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// The little picture beside a cut-list row: the shape that row describes, drawn small
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;4.4).
/// </summary>
/// <remarks>
/// <para>
/// The bench vocabulary names corners and edges by compass in the part's own frame, and a
/// sentence like "round the north-east corner to a 1&#x2033; radius" is easier to trust with the
/// shape beside it — the magazine's "see drawing". So a row with cuts draws one, and a plain
/// rectangle draws nothing: there is nothing about a rectangle a picture would add.
/// </para>
/// <para>
/// It is a static rendering of the same <see cref="Outline"/> the canvas draws, through the same
/// <see cref="OutlineDrawing"/> builder, so the thumbnail and the drawing cannot disagree about
/// the shape. The part is shown in its own frame, unrotated — the frame the sentences use — for
/// the same reason the shape workshop will (&#xA7;7.1).
/// </para>
/// </remarks>
public sealed class CutThumbnail : Control
{

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            InvalidateVisual();
        }
    }

    /// <summary>How wide the picture is, in pixels.</summary>
    public const double CellWidth = 44;

    /// <summary>How tall the picture is, in pixels.</summary>
    public const double CellHeight = 28;

    /// <summary>The breathing space around the shape, in pixels.</summary>
    const double Inset = 3;

    readonly Outline _outline;
    readonly double _blankWidth;
    readonly double _blankHeight;

    /// <summary>A picture of one blank's shape.</summary>
    /// <param name="blank">The blank, in its own frame: anchored at the origin and unrotated.</param>
    public CutThumbnail(Box blank)
    {
        ArgumentNullException.ThrowIfNull(blank);

        _outline = blank.Outline();
        _blankWidth = blank.Width.ToInches();
        _blankHeight = blank.Height.ToInches();

        Width = CellWidth;
        Height = CellHeight;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_blankWidth <= 0 || _blankHeight <= 0)
        {
            return;
        }

        // The blank fits the cell whatever its proportions: a 48" x 24" top and a 2 1/2" square
        // leg both end up legible, which they would not at a shared scale.
        double scale = Math.Min(
            (CellWidth - (2 * Inset)) / _blankWidth,
            (CellHeight - (2 * Inset)) / _blankHeight);
        double left = (CellWidth - (_blankWidth * scale)) / 2;
        double top = (CellHeight - (_blankHeight * scale)) / 2;

        // North is up, as it is on the canvas, so the picture and the words agree about which
        // corner is which.
        Point ToCell(Point2 point) => new(
            left + (point.X.ToInches() * scale),
            top + ((_blankHeight - point.Y.ToInches()) * scale));

        EntityStyle style = CanvasPalette.For(ActualThemeVariant).StyleFor(DesignLayers.Parts);
        context.DrawGeometry(
            new SolidColorBrush(style.Fill),
            new Pen(new SolidColorBrush(style.Stroke), 1) { LineJoin = PenLineJoin.Miter },
            OutlineDrawing.GeometryOf(_outline, ToCell));
    }
}

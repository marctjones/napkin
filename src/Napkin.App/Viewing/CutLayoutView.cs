using System.Collections.Immutable;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Napkin.App.Designs;
using Napkin.Modules.Furniture;

namespace Napkin.App.Viewing;

/// <summary>
/// One drawn board of the cut layout (issue #138): a bar as long as the board, the pieces as
/// segments in cutting order, a dark mark at each kerf, and the offcut hatched.
/// </summary>
/// <remarks>
/// Every board in a view is drawn to one scale, so a 6' board is visibly shorter than a 16' one.
/// Colours come from the canvas palette's parts style, so the bar reads in both themes.
/// </remarks>
public sealed class CutLayoutBar : Control
{
    /// <summary>How tall a bar is, in pixels.</summary>
    public const double BarHeight = 18;

    private readonly PlannedBoard _board;
    private readonly double _pixelsPerUnit;

    /// <summary>A bar for one board.</summary>
    /// <param name="board">The board.</param>
    /// <param name="pixelsPerUnit">The scale: pixels per 1/1024 inch, the same for every bar in a view.</param>
    public CutLayoutBar(PlannedBoard board, double pixelsPerUnit)
    {
        ArgumentNullException.ThrowIfNull(board);
        _board = board;
        _pixelsPerUnit = pixelsPerUnit;
        Width = (board.StockLength.Units * pixelsPerUnit) + 1;
        Height = BarHeight;
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    /// <summary>The board this bar draws.</summary>
    public PlannedBoard Board => _board;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        EntityStyle style = CanvasPalette.For(ActualThemeVariant).StyleFor(DesignLayers.Parts);
        IBrush fill = new SolidColorBrush(style.Fill);
        Pen outline = new(new SolidColorBrush(style.Stroke), 1);
        Pen kerfMark = new(new SolidColorBrush(style.Stroke), 2);
        Pen hatch = new(new SolidColorBrush(style.Stroke, 0.55), 1);

        double x = 0.5;
        foreach (PlacedPiece piece in _board.Pieces)
        {
            double width = piece.Length.Units * _pixelsPerUnit;
            context.DrawRectangle(fill, outline, new Rect(x, 0.5, width, BarHeight - 1));
            x += width;

            // The kerf after a piece is drawn at least two pixels wide so it can be seen at any scale.
            double kerf = Math.Max(_board.Kerf.Units * _pixelsPerUnit, 0);
            if (kerf > 0 && _board.Cuts > 0)
            {
                context.DrawLine(kerfMark, new Point(x + (kerf / 2), 0), new Point(x + (kerf / 2), BarHeight));
            }

            x += kerf;
        }

        double end = 0.5 + (_board.StockLength.Units * _pixelsPerUnit);
        if (end > x)
        {
            // The offcut: hatched, so it reads as what is left rather than as a piece.
            Rect offcut = new(x, 0.5, end - x, BarHeight - 1);
            context.DrawRectangle(null, outline, offcut);
            using (context.PushClip(offcut))
            {
                for (double hx = x - BarHeight; hx < end; hx += 5)
                {
                    context.DrawLine(hatch, new Point(hx, BarHeight), new Point(hx + BarHeight, 0));
                }
            }
        }
    }
}

/// <summary>
/// The cut layout as it is read: one text line per board, the drawn bar under it, and the refusals
/// (issue #138). The text of each line is <see cref="CutLayout.Line"/> of the very fields
/// <see cref="CutLayout.ToCsv"/> writes, so the screen and the file cannot disagree.
/// </summary>
public sealed class CutLayoutView : StackPanel
{
    /// <summary>The widest a bar is drawn, in pixels, for the longest board in the view.</summary>
    private const double WidestBar = 560;

    private ImmutableArray<CutLayoutRow> _rows = [];

    /// <summary>An empty view.</summary>
    public CutLayoutView() => Spacing = 2;

    /// <summary>The rows shown, in order.</summary>
    public ImmutableArray<CutLayoutRow> Rows
    {
        get => _rows;
        set
        {
            _rows = value;
            Rebuild();
        }
    }

    /// <summary>The text lines on screen, one per row, in order.</summary>
    public ImmutableArray<string> LinesOnScreen
        => [.. Children.OfType<TextBlock>().Select(text => text.Text ?? string.Empty)];

    /// <summary>The bars drawn, one per board, in order.</summary>
    public ImmutableArray<CutLayoutBar> Bars => [.. Children.OfType<CutLayoutBar>()];

    private void Rebuild()
    {
        Children.Clear();

        long longest = _rows.Where(row => row.Planned is not null).Select(row => row.Planned!.StockLength.Units).DefaultIfEmpty(1).Max();
        double pixelsPerUnit = WidestBar / longest;

        foreach (CutLayoutRow row in _rows)
        {
            Children.Add(new TextBlock
            {
                Text = CutLayout.Line(CutLayout.Fields(row)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                FontStyle = row.Planned is null ? FontStyle.Italic : FontStyle.Normal,
            });

            if (row.Planned is not null)
            {
                Children.Add(new CutLayoutBar(row.Planned, pixelsPerUnit));
            }
        }
    }
}

using System.Collections.Immutable;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Napkin.Modules.Furniture;
using Napkin.Modules.Editing;

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
/// One drawn sheet of the cut layout (issue #26): the sheet with its long side across the screen,
/// each piece where <see cref="SheetLayout"/> puts it, and everything that is not a piece — offcuts
/// and kerf — hatched, as a board's offcut is.
/// </summary>
/// <remarks>
/// Drawn to the same scale as the boards in its view, so an 8' sheet is as long as an 8' board.
/// The pieces are not labelled here: the line above the drawing names them, strip by strip, in the
/// order they lie. The printed diagram, with labels, is #211.
/// </remarks>
public sealed class CutLayoutSheet : Control
{
    private readonly PlannedSheet _sheet;
    private readonly double _pixelsPerUnit;

    /// <summary>A drawing of one sheet.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="pixelsPerUnit">The scale: pixels per 1/1024 inch, the same as the view's bars.</param>
    public CutLayoutSheet(PlannedSheet sheet, double pixelsPerUnit)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        _sheet = sheet;
        _pixelsPerUnit = pixelsPerUnit;
        Width = (sheet.Long.Units * pixelsPerUnit) + 1;
        Height = (sheet.Short.Units * pixelsPerUnit) + 1;
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    /// <summary>The sheet this draws.</summary>
    public PlannedSheet Sheet => _sheet;

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
        Pen hatch = new(new SolidColorBrush(style.Stroke, 0.55), 1);

        Rect whole = new(0.5, 0.5, _sheet.Long.Units * _pixelsPerUnit, _sheet.Short.Units * _pixelsPerUnit);
        Rect[] pieces =
        [
            .. _sheet.Pieces.Select(piece => new Rect(
                whole.Left + (piece.X.Units * _pixelsPerUnit),
                whole.Top + (piece.Y.Units * _pixelsPerUnit),
                piece.Along.Units * _pixelsPerUnit,
                piece.Across.Units * _pixelsPerUnit)),
        ];

        // The waste — the sheet less its pieces — hatched; the pieces' fill is translucent, so the
        // hatch is clipped out of them rather than drawn under them.
        GeometryGroup cut = new() { FillRule = FillRule.NonZero };
        foreach (Rect piece in pieces)
        {
            cut.Children.Add(new RectangleGeometry(piece));
        }

        context.DrawRectangle(null, outline, whole);
        using (context.PushGeometryClip(new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(whole), cut)))
        {
            for (double hx = whole.Left - whole.Height; hx < whole.Right; hx += 5)
            {
                context.DrawLine(hatch, new Point(hx, whole.Bottom), new Point(hx + whole.Height, whole.Top));
            }
        }

        foreach (Rect piece in pieces)
        {
            context.DrawRectangle(fill, outline, piece);
        }
    }
}

/// <summary>
/// The cut layout as it is read: one text line per board or sheet, the drawn bar or sheet under it,
/// and the refusals (issues #138, #26). The text of each line is <see cref="CutLayout.Line"/> of the
/// very fields <see cref="CutLayout.ToCsv"/> writes, so the screen and the file cannot disagree.
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

    /// <summary>The sheets drawn, one per sheet, in order.</summary>
    public ImmutableArray<CutLayoutSheet> Sheets => [.. Children.OfType<CutLayoutSheet>()];

    private void Rebuild()
    {
        Children.Clear();

        // One scale for boards and sheets alike, set by the longest of them.
        long longest = _rows.Select(row => row.Planned?.StockLength.Units ?? row.Sheet?.Long.Units ?? 0).DefaultIfEmpty(0).Max();
        double pixelsPerUnit = WidestBar / Math.Max(longest, 1);

        foreach (CutLayoutRow row in _rows)
        {
            Children.Add(new TextBlock
            {
                Text = CutLayout.Line(CutLayout.Fields(row)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                FontStyle = row.Planned is null && row.Sheet is null ? FontStyle.Italic : FontStyle.Normal,
            });

            if (row.Planned is not null)
            {
                Children.Add(new CutLayoutBar(row.Planned, pixelsPerUnit));
            }
            else if (row.Sheet is not null)
            {
                Children.Add(new CutLayoutSheet(row.Sheet, pixelsPerUnit));
            }
        }
    }
}

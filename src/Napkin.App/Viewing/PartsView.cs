using System.Collections.Immutable;
using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.Core.Materials;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

namespace Napkin.App.Viewing;

/// <summary>
/// The Parts view (docs/design/parts-view.md): every distinct piece of the design drawn once, to
/// one scale, with its count — the cut list's rows as a wrapped grid of cells, in the main canvas
/// beside the plan and the 3D view. It scrolls down, zooms 50–300 %, and moves a focus cell with
/// the keyboard; selecting in the model from it is slice D (#125).
/// </summary>
public sealed class PartsView : Control
{
    /// <summary>What the view says with no design open (parts-view §4.5).</summary>
    public const string NoDesign = "No design is open.";

    const double ZoomStep = 1.25;
    const double WheelStep = 48;

    DesignEditor? _editor;
    ImmutableArray<PartsCell> _cells = [];
    IReadOnlyList<PartsPlacement> _placed = [];
    double _sheetScale = 1;
    double _zoom = 1;
    double _scroll;
    int? _focused;

    /// <summary>A Parts view on no editor yet.</summary>
    public PartsView()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>Raised when the cells change: the design changed or another one opened.</summary>
    public event EventHandler? CellsChanged;

    /// <summary>Raised when the zoom or the scroll changes.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>Raised when a view key asks for another view (1–7).</summary>
    public event EventHandler<DesignView>? ViewRequested;

    /// <summary>The editor whose design this view shows.</summary>
    public DesignEditor? Editor
    {
        get => _editor;
        set
        {
            if (_editor is { } old)
            {
                old.DesignChanged -= OnDesignChanged;
                old.DesignOpened -= OnDesignOpened;
            }

            _editor = value;
            if (value is { } now)
            {
                now.DesignChanged += OnDesignChanged;
                now.DesignOpened += OnDesignOpened;
            }

            Rebuild();
        }
    }

    /// <summary>The cells, in the sheet's order.</summary>
    public ImmutableArray<PartsCell> Cells => _cells;

    /// <summary>Where each cell is, in the view's pixels before scrolling.</summary>
    public IReadOnlyList<PartsPlacement> Placements => _placed;

    /// <summary>How many cells a row holds at this width and zoom.</summary>
    public int Columns => PartsSheetLayout.Columns(Bounds.Width, _zoom);

    /// <summary>What each cell reads as, in the sheet's order: "Leg, ×4, …" (§6).</summary>
    public ImmutableArray<string> CellsOnScreen => [.. _cells.Select(PartsCellText.AutomationName)];

    /// <summary>What the view says instead of cells when there are none; null when there are some.</summary>
    public string? EmptyMessage => !_cells.IsEmpty ? null : _editor is { } editor ? CutList.WhyEmpty(editor.Sketch) : NoDesign;

    /// <summary>The cell the keyboard is on, or null.</summary>
    public PartsCell? FocusedCell => _focused is { } index ? _cells[index] : null;

    /// <summary>The zoom, 0.5 to 3; 1 is 100 %.</summary>
    public double Zoom => _zoom;

    /// <summary>The zoom as the status bar says it.</summary>
    public double ZoomPercent => _zoom * 100;

    /// <summary>How far down the sheet is scrolled, in pixels.</summary>
    public double ScrollOffset => _scroll;

    /// <summary>Where a cell is drawn, in the view's own pixels, as scrolled; null when the cell is not placed.</summary>
    public Rect? CellRectangle(PartsCell cell) =>
        _placed.FirstOrDefault(placement => Equals(placement.Cell, cell)) is { } at
            ? new Rect(at.Left, at.Top - _scroll, at.Width, at.Height)
            : null;

    /// <summary>
    /// Does what a view key asks (<see cref="KeyMaps.View"/>): a number shows that view; the zoom keys
    /// zoom the sheet, fit is 100 % at the top (§4.3); the arrows move the focus cell (§6).
    /// </summary>
    /// <returns><see langword="true"/> when the Parts view has a meaning for it.</returns>
    public bool Apply(ViewCommand command)
    {
        if (KeyInput.ViewFor(command) is { } asked)
        {
            ViewRequested?.Invoke(this, asked);
            return true;
        }

        switch (command)
        {
            case ViewCommand.ZoomToFit or ViewCommand.ResetView:
                ZoomToFit();
                return true;
            case ViewCommand.ZoomIn:
                ZoomBy(ZoomStep);
                return true;
            case ViewCommand.ZoomOut:
                ZoomBy(1 / ZoomStep);
                return true;
            case ViewCommand.Left or ViewCommand.FarLeft:
                MoveFocus(-1);
                return true;
            case ViewCommand.Right or ViewCommand.FarRight:
                MoveFocus(1);
                return true;
            case ViewCommand.Up or ViewCommand.FarUp:
                MoveFocus(-Columns);
                return true;
            case ViewCommand.Down or ViewCommand.FarDown:
                MoveFocus(Columns);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Back to 100 %, at the top of the sheet (§4.3).</summary>
    public void ZoomToFit()
    {
        _zoom = 1;
        _scroll = 0;
        Relayout();
    }

    /// <summary>A zoom step in or out, within 50–300 %.</summary>
    public void ZoomBy(double factor)
    {
        _zoom = Math.Clamp(_zoom * factor, PartsSheetLayout.MinZoom, PartsSheetLayout.MaxZoom);
        Relayout();
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        // The sheet's own keys first (§6): a Home or an End that a view map would read as "fit" means
        // the first or last cell here, and a page is a viewport. Tab is left alone: it leaves.
        bool handled = e.KeyModifiers == KeyModifiers.None && e.Key switch
        {
            Key.Home => FocusAt(0),
            Key.End => FocusAt(_cells.Length - 1),
            Key.PageDown => MoveFocus(Columns * RowsPerPage),
            Key.PageUp => MoveFocus(-Columns * RowsPerPage),
            _ => false,
        };
        if (!handled && KeyInput.From(e.Key, e.KeyModifiers) is { } key && KeyMaps.View.Find(key) is { } command)
        {
            handled = Apply(command);
        }

        e.Handled = handled;
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        Point at = e.GetPosition(this);
        int index = _placed.Select((placement, i) => (placement, i))
            .Where(pair => pair.placement.Cell is not null && new Rect(pair.placement.Left, pair.placement.Top - _scroll, pair.placement.Width, pair.placement.Height).Contains(at))
            .Select(pair => _cells.IndexOf(pair.placement.Cell!))
            .DefaultIfEmpty(-1)
            .First();
        if (index >= 0)
        {
            FocusAt(index);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // Ctrl+wheel zooms (§4.3); Cmd too, where a Mac person reaches for it.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ZoomBy(e.Delta.Y > 0 ? ZoomStep : 1 / ZoomStep);
        }
        else
        {
            ScrollTo(_scroll - (e.Delta.Y * WheelStep));
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Relayout();
    }

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
    protected override AutomationPeer OnCreateAutomationPeer() => new PartsViewAutomationPeer(this);

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        context.FillRectangle(new SolidColorBrush(palette.Background), new Rect(Bounds.Size));
        if (EmptyMessage is { } empty)
        {
            FormattedText said = Text(empty, 13, palette.Label, FontWeight.Normal);
            said.MaxTextWidth = Math.Max(Bounds.Width - 64, 64);
            context.DrawText(said, new Point(32, 32));
            return;
        }

        EntityStyle parts = palette.StyleFor(DesignLayers.Parts);
        IBrush fill = new SolidColorBrush(parts.Fill), ink = new SolidColorBrush(palette.Dimension);
        Pen outline = new(new SolidColorBrush(parts.Stroke), Math.Max(parts.StrokeThickness, 1));
        Pen frame = new(new SolidColorBrush(palette.Dimension, 0.35), 1), dimension = new(ink, 0.8), ring = new(ink, 1.5);
        foreach (PartsPlacement placement in _placed)
        {
            Rect at = new(placement.Left, placement.Top - _scroll, placement.Width, placement.Height);
            if (at.Bottom < 0 || at.Top > Bounds.Height)
            {
                continue;
            }

            if (placement.Title is { } title)
            {
                context.DrawText(Text(title, 13 * _zoom, palette.Label, FontWeight.SemiBold), at.TopLeft);
                continue;
            }

            PartsCell cell = placement.Cell!;
            context.DrawRectangle(null, frame, at, 4, 4);
            if (Equals(FocusedCell, cell))
            {
                context.DrawRectangle(null, ring, at.Deflate(2), 4, 4);
            }

            using DrawingContext.PushedState placed = context.PushTransform(Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(at.Left, at.Top));
            PartsCellDrawn drawn = PartsCellDrawing.Build(cell, _sheetScale, new Point(0, 0));
            context.DrawGeometry(fill, outline, drawn.Outline);
            DrawDimension(context, dimension, palette.Dimension, drawn.Length, vertical: false);
            DrawDimension(context, dimension, palette.Dimension, drawn.Width, vertical: true);
            foreach (PartsText text in drawn.Texts)
            {
                DrawText(context, palette, cell, text);
            }
        }
    }

    int RowsPerPage => Math.Max(1, (int)Math.Floor(Bounds.Height / ((PartsSheetLayout.CellHeight + PartsSheetLayout.Gutter) * _zoom)));

    void OnDesignChanged(object? sender, EventArgs e) => Rebuild();

    void OnDesignOpened(object? sender, EventArgs e)
    {
        _scroll = 0;
        _focused = null;
        Rebuild();
    }

    /// <summary>The cut list again, the sheet from it, the focus kept on the same row if it is still there (§4.6).</summary>
    void Rebuild()
    {
        PartsCell? focused = FocusedCell;
        _cells = _editor is { } editor ? PartsSheet.Of(CutList.Of(editor.Sketch, MaterialsLibrary.Shipped)) : [];
        _sheetScale = PartsScale.Sheet(_cells.Select(PartsPicture.Of)) ?? 1;
        int kept = focused is null ? -1 : _cells.IndexOf(focused);
        _focused = kept >= 0 ? kept : null;
        Relayout();
        CellsChanged?.Invoke(this, EventArgs.Empty);
    }

    void Relayout()
    {
        _placed = PartsSheetLayout.Arrange(_cells, Bounds.Width, _zoom);
        ScrollTo(_scroll);
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    void ScrollTo(double offset)
    {
        double most = Math.Max(0, PartsSheetLayout.Height(_placed, _zoom) - Bounds.Height);
        double clamped = Math.Clamp(offset, 0, most);
        if (clamped != _scroll)
        {
            _scroll = clamped;
            InvalidateVisual();
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    bool MoveFocus(int by)
    {
        if (_cells.IsEmpty)
        {
            return true;
        }

        return FocusAt(_focused is { } index ? Math.Clamp(index + by, 0, _cells.Length - 1) : 0);
    }

    bool FocusAt(int index)
    {
        if (_cells.IsEmpty)
        {
            return true;
        }

        _focused = Math.Clamp(index, 0, _cells.Length - 1);
        if (CellRectangle(_cells[_focused.Value]) is { } at)
        {
            // Scrolled into view (§4.2): up to its top, or down until its bottom shows.
            if (at.Top < 0)
            {
                ScrollTo(_scroll + at.Top);
            }
            else if (at.Bottom > Bounds.Height)
            {
                ScrollTo(_scroll + (at.Bottom - Bounds.Height));
            }
        }

        InvalidateVisual();
        return true;
    }

    static void DrawDimension(DrawingContext context, Pen pen, Color ink, PartsDimension dimension, bool vertical)
    {
        context.DrawLine(pen, dimension.From, dimension.To);
        Vector across = vertical ? new Vector(3, 0) : new Vector(0, 3);
        context.DrawLine(pen, dimension.From - across, dimension.From + across);
        context.DrawLine(pen, dimension.To - across, dimension.To + across);
        FormattedText label = Text(dimension.Label, 10, ink, FontWeight.Normal);
        Point middle = new((dimension.From.X + dimension.To.X) / 2, (dimension.From.Y + dimension.To.Y) / 2);
        context.DrawText(label, vertical ? new Point(middle.X + 4, middle.Y - (label.Height / 2)) : new Point(middle.X - (label.Width / 2), middle.Y + 2));
    }

    static void DrawText(DrawingContext context, CanvasPalette palette, PartsCell cell, PartsText text)
    {
        switch (text.Role)
        {
            case PartsTextRole.Label:
                FormattedText label = Text(text.Text, 12, palette.Label, FontWeight.Bold);
                label.MaxTextWidth = PartsScale.DrawingWidth - 44;
                label.MaxLineCount = 1;
                label.Trimming = TextTrimming.CharacterEllipsis;
                context.DrawText(label, text.At);
                break;
            case PartsTextRole.Badge:
                FormattedText badge = Text(text.Text, 11, palette.Background, FontWeight.SemiBold);
                Rect chip = new(text.At.X - badge.Width - 10, text.At.Y - 1, badge.Width + 10, badge.Height + 2);
                context.DrawRectangle(new SolidColorBrush(palette.Dimension), null, chip, 3, 3);
                context.DrawText(badge, new Point(chip.X + 5, text.At.Y));
                break;
            default:
                // An unresolved stock name reads in the problem colour, as the cut list shows it.
                Color colour = text.Role == PartsTextRole.Details && cell.Row.Unresolved ? palette.Snap : palette.Dimension;
                FormattedText line = Text(text.Text, 10.5, colour, FontWeight.Normal);
                line.MaxTextWidth = PartsScale.DrawingWidth;
                line.MaxLineCount = 1;
                line.Trimming = TextTrimming.CharacterEllipsis;
                context.DrawText(line, text.At);
                break;
        }
    }

    static FormattedText Text(string text, double size, Color colour, FontWeight weight) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, weight),
        size,
        new SolidColorBrush(colour));
}

/// <summary>The Parts view as an automation element: a group of its cells (§6), as the canvas is of its parts.</summary>
public sealed class PartsViewAutomationPeer : ControlAutomationPeer
{
    /// <inheritdoc cref="PartsViewAutomationPeer"/>
    public PartsViewAutomationPeer(PartsView owner)
        : base(owner) => owner.CellsChanged += (_, _) => InvalidateChildren();

    /// <summary>The view, typed.</summary>
    public new PartsView Owner => (PartsView)base.Owner;

    /// <inheritdoc/>
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() =>
        [.. Owner.Cells.Select(cell => new PartsCellAutomationPeer(Owner, cell))];

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    /// <inheritdoc/>
    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : "Parts";
}

/// <summary>
/// One cell of the Parts view, as an automation element: named "Leg, ×4, 16 1/4" × 2 1/2" × 2 1/2"".
/// A <see cref="ControlAutomationPeer"/> over the view for the reason <see cref="PartAutomationPeer"/>
/// gives: a drawn thing has no control of its own, and only this reports a usable rectangle to UIA.
/// </summary>
public sealed class PartsCellAutomationPeer : ControlAutomationPeer
{
    readonly PartsCell _cell;

    /// <inheritdoc cref="PartsCellAutomationPeer"/>
    public PartsCellAutomationPeer(PartsView view, PartsCell cell)
        : base(view) => _cell = cell;

    /// <summary>The view, typed.</summary>
    public new PartsView Owner => (PartsView)base.Owner;

    /// <inheritdoc/>
    protected override string? GetNameCore() => PartsCellText.AutomationName(_cell);

    /// <inheritdoc/>
    protected override string GetClassNameCore() => "PartsCell";

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    /// <inheritdoc/>
    protected override Rect GetBoundingRectangleCore() =>
        Owner.CellRectangle(_cell) is { } at && TopLevel.GetTopLevel(Owner) is { } root && Owner.TransformToVisual(root) is { } transform
            ? at.TransformToAABB(transform)
            : default;

    /// <inheritdoc/>
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

    /// <inheritdoc/>
    protected override bool IsKeyboardFocusableCore() => false;
}

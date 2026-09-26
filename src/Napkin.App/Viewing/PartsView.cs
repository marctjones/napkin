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
using Napkin.Core.Geometry;
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
    ImmutableArray<PartsCell> _shown = [];
    IReadOnlyList<PartsPlacement> _placed = [];
    double _sheetScale = 1;
    double _sheetScale3D = 1;
    bool _isometric;
    bool _grouped;
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

    /// <summary>Raised when grouping by stock turns on or off.</summary>
    public event EventHandler? GroupingChanged;

    /// <summary>Raised when the drawing switches between flat and 3D.</summary>
    public event EventHandler? IsometricChanged;

    /// <summary>Raised when the zoom or the scroll changes.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>
    /// Raised when an editing key is pressed here — R, S, Delete, the tools — for the window to run
    /// as it does for the plan and the 3D view; the arrows are the focus cell's, never a nudge.
    /// </summary>
    public event EventHandler<EditCommandRequest>? CommandRequested;

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
                old.SelectionChanged -= OnSelectionChanged;
            }

            _editor = value;
            if (value is { } now)
            {
                now.DesignChanged += OnDesignChanged;
                now.DesignOpened += OnDesignOpened;
                now.SelectionChanged += OnSelectionChanged;
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

    /// <summary>What each cell reads as, in the order they are on the screen: "Leg, ×4, …" (§6).</summary>
    public ImmutableArray<string> CellsOnScreen => [.. _shown.Select(PartsCellText.AutomationName)];

    /// <summary>The group titles on the screen, in order; empty when the sheet is not grouped.</summary>
    public ImmutableArray<string> GroupTitles => [.. _placed.Where(placement => placement.Title is not null).Select(placement => placement.Title!)];

    /// <summary>
    /// Whether the cells are grouped by stock, each group under its title band (§1.3, §4.4): known
    /// stock in order of first appearance, then names the library does not know, then "No stock".
    /// </summary>
    public bool GroupByStock
    {
        get => _grouped;
        set
        {
            if (_grouped == value)
            {
                return;
            }

            _grouped = value;
            Relayout();
            GroupingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>What the view says instead of cells when there are none; null when there are some.</summary>
    public string? EmptyMessage => !_cells.IsEmpty ? null : _editor is { } editor ? CutList.WhyEmpty(editor.Sketch) : NoDesign;

    /// <summary>The cell the keyboard is on, or null.</summary>
    public PartsCell? FocusedCell => _focused is { } index ? _shown[index] : null;

    /// <summary>How much of a cell the design's selection holds (§5.2): the editor's, read, never stored.</summary>
    public PartsCellSelection SelectionOf(PartsCell cell) =>
        _editor is { } editor ? PartsSheet.SelectionOf(cell, editor.Selection) : PartsCellSelection.None;

    /// <summary>
    /// Whether the cells draw each piece in 3D, isometric and orthographic, rather than flat (§3). The
    /// words are the same either way; 3D draws no dimension lines.
    /// </summary>
    public bool Isometric
    {
        get => _isometric;
        set
        {
            if (_isometric == value)
            {
                return;
            }

            _isometric = value;
            InvalidateVisual();
            IsometricChanged?.Invoke(this, EventArgs.Empty);
        }
    }

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

            // I for isometric (§3): O is the 3D view's projection, and the two are not to be confused.
            case ViewCommand.PartsIsometric:
                return ToggleIsometric();
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
            Key.End => FocusAt(_shown.Length - 1),
            Key.PageDown => MoveFocus(Columns * RowsPerPage),
            Key.PageUp => MoveFocus(-Columns * RowsPerPage),
            Key.Escape => ClearSelection(),
            _ => false,
        };

        // Enter or Space selects the focus cell's parts in the model, Ctrl or Cmd toggles them (§5.1).
        if (!handled && e.Key is Key.Enter or Key.Space && FocusedCell is { } focused)
        {
            bool toggle = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            handled = SelectCell(focused, toggle, add: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
        if (!handled && KeyInput.From(e.Key, e.KeyModifiers) is { } key)
        {
            if (KeyMaps.Edit.Find(key) is { } edit && edit is not (>= EditCommand.NudgeLeft and <= EditCommand.NudgeFarDown))
            {
                EditCommandRequest request = new(edit);
                CommandRequested?.Invoke(this, request);
                handled = request.Handled;
            }

            if (!handled && KeyMaps.View.Find(key) is { } command)
            {
                handled = Apply(command);
            }
        }

        e.Handled = handled;
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        // A cell selects its parts in the model; Ctrl or Cmd toggles them, Shift adds them; the empty
        // sheet clears the selection, as the empty canvas does (§5.1).
        if (CellAt(e.GetPosition(this)) is not { } cell)
        {
            ClearSelection();
            return;
        }

        FocusAt(_shown.IndexOf(cell));
        KeyModifiers held = e.KeyModifiers;
        SelectCell(cell, toggle: held.HasFlag(KeyModifiers.Control) || held.HasFlag(KeyModifiers.Meta), add: held.HasFlag(KeyModifiers.Shift));
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        // A cell whose badge and selection count different things says so (§5.3).
        ToolTip.SetTip(this, CellAt(e.GetPosition(this)) is { } cell ? PartsCellText.PiecesFrom(cell) : null);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ToolTip.SetTip(this, null);
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

            // Selected: a border in the selection colour; partly selected, the same border dashed (§2.2, §5.2).
            PartsCellSelection held = SelectionOf(cell);
            if (held != PartsCellSelection.None)
            {
                Pen selected = new(new SolidColorBrush(palette.Selection), 2)
                {
                    DashStyle = held == PartsCellSelection.Partly ? new DashStyle([4, 3], 0) : null,
                };
                context.DrawRectangle(null, selected, at, 4, 4);
            }

            if (Equals(FocusedCell, cell))
            {
                context.DrawRectangle(null, ring, at.Deflate(2), 4, 4);
            }

            using DrawingContext.PushedState placed = context.PushTransform(Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(at.Left, at.Top));
            if (_isometric)
            {
                PartsIsometricDrawn solid = PartsIsometric.Build(cell, _sheetScale3D, new Point(0, 0));
                Pen edge = new(new SolidColorBrush(parts.Stroke), 0.8);
                foreach (PartsFace face in solid.Faces)
                {
                    StreamGeometry shape = new();
                    using (StreamGeometryContext path = shape.Open())
                    {
                        path.BeginFigure(face.Points[0], isFilled: true);
                        foreach (Point corner in face.Points.Skip(1))
                        {
                            path.LineTo(corner);
                        }

                        path.EndFigure(isClosed: true);
                    }

                    context.DrawGeometry(new SolidColorBrush(ModelView.Tone(palette, parts, face.Normal)), edge, shape);
                }

                foreach (PartsText text in solid.Texts)
                {
                    DrawText(context, palette, cell, text);
                }

                continue;
            }

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

    void OnSelectionChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>The cell under a point of the view, as scrolled; null over the gaps and the margins.</summary>
    PartsCell? CellAt(Point at) => _placed.FirstOrDefault(placement =>
        placement.Cell is not null && new Rect(placement.Left, placement.Top - _scroll, placement.Width, placement.Height).Contains(at))?.Cell;

    /// <summary>Selects a cell's parts in the model, toggles each of them, or adds them.</summary>
    bool SelectCell(PartsCell cell, bool toggle, bool add)
    {
        if (_editor is not { } editor)
        {
            return false;
        }

        SelectionCommands.Pick(editor, cell.Members, toggle, add);
        return true;
    }

    bool ToggleIsometric()
    {
        Isometric = !Isometric;
        return true;
    }

    bool ClearSelection()
    {
        _editor?.ClearSelection();
        return true;
    }

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
        _sheetScale3D = PartsIsometric.Sheet(_cells) ?? 1;
        _focused = null;
        Relayout();
        int kept = focused is null ? -1 : _shown.IndexOf(focused);
        _focused = kept >= 0 ? kept : null;
        CellsChanged?.Invoke(this, EventArgs.Empty);
    }

    void Relayout()
    {
        // The focus follows its cell when the order on screen changes (grouping on or off).
        PartsCell? focused = FocusedCell;
        _placed = _grouped
            ? PartsSheetLayout.Arrange(PartsSheet.GroupedByStock(_cells), Bounds.Width, _zoom)
            : PartsSheetLayout.Arrange(_cells, Bounds.Width, _zoom);
        _shown = [.. _placed.Where(placement => placement.Cell is not null).Select(placement => placement.Cell!)];
        int kept = focused is null ? -1 : _shown.IndexOf(focused);
        _focused = kept >= 0 ? kept : null;
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
        if (_shown.IsEmpty)
        {
            return true;
        }

        return FocusAt(_focused is { } index ? Math.Clamp(index + by, 0, _shown.Length - 1) : 0);
    }

    bool FocusAt(int index)
    {
        if (_shown.IsEmpty)
        {
            return true;
        }

        _focused = Math.Clamp(index, 0, _shown.Length - 1);
        if (CellRectangle(_shown[_focused.Value]) is { } at)
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
        // Clear of the end ticks, which on a thin piece sit either side of the label's middle.
        context.DrawText(label, vertical ? new Point(middle.X + 7, middle.Y - (label.Height / 2)) : new Point(middle.X - (label.Width / 2), middle.Y + 4));
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

using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Napkin.App.Designs;
using Napkin.App.Editing;
using Design = Napkin.App.Designs.Design;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.App.Viewing;

/// <summary>Raised when the canvas wants a dimension opened for typing.</summary>
/// <param name="Box">The part.</param>
/// <param name="Axis">Which of its sizes.</param>
public sealed record DimensionEditRequested(EntityId Box, SizeAxis Axis);

/// <summary>
/// Draws a design in plan view, and lets a person move around it and change it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Every change goes through the updater.</strong> The canvas holds a
/// <see cref="DesignEditor"/> and issues <see cref="Request"/>s to it: a move is
/// <see cref="Drag"/>, a handle is <see cref="DragFace"/> (a corner handle is one
/// <see cref="Batch"/> of two), drawing a part is <see cref="AddEntity"/>, a snap adds a
/// <see cref="Flush"/> or a <see cref="Coincident"/>, and Delete is
/// <see cref="RemoveEntity"/>. There is no code path in this control that builds a
/// <see cref="Sketch"/> (CVS-005).
/// </para>
/// <para>
/// <strong>Controls.</strong> <c>R</c> takes the rectangle tool and <c>S</c> or Escape gives it
/// back; drag on empty paper to pan, click a part to select it, drag a selected part to move it,
/// drag its handles to resize; arrow keys pan, or nudge the selection by one grid step when there
/// is one; Delete removes what is selected; <c>P</c> pins it; <c>D</c> duplicates it a grid step
/// away; <c>X</c>, <c>Y</c> and <c>Z</c> turn it a quarter turn about that axis, Shift the other way;
/// <c>V</c> asks for the 3D view (docs/design/assembly-model.md &#xA7;8). Wheel zooms about the pointer;
/// Shift and the wheel pan; the middle button always pans.
/// </para>
/// <para>
/// <strong>Snapping is live and stored.</strong> While a part is being moved it lands on the grid
/// you can see, or on another part's edges when it is close enough, with an indicator on the line
/// it caught. Dropping it there stores the relationship that says so — a drawing never infers a
/// relationship from where things happen to sit (docs/design/geometry-model.md &#xA7;3.2).
/// </para>
/// <para>
/// <strong>Line weights are in pixels, not inches.</strong> A 1.4-pixel outline is 1.4 pixels at
/// every zoom, so the drawing reads the same framed on a wall or zoomed to a single joint.
/// </para>
/// <para>
/// <strong>What is drawn is also readable.</strong> Because everything here is pixels, a script or
/// an accessibility client would see one opaque rectangle and nothing else. <see cref="CanvasView"/>
/// therefore returns a <see cref="CanvasAutomationPeer"/>, which exposes one element per part,
/// reading this class's own view of the sketch (issue #64).
/// </para>
/// </remarks>
public sealed class CanvasView : Control
{
    /// <summary>How much one wheel notch zooms.</summary>
    const double ZoomPerWheelNotch = 1.15;

    /// <summary>How much the +/- keys zoom.</summary>
    const double ZoomPerKeyPress = 1.25;

    /// <summary>How far one wheel notch pans when Shift is held, in pixels.</summary>
    const double PanPixelsPerWheelNotch = 60;

    /// <summary>How much of the viewport an arrow key pans across.</summary>
    const double PanFractionPerKeyPress = 0.1;

    /// <summary>How much of the viewport an arrow key pans across with Shift held.</summary>
    const double FastPanFractionPerKeyPress = 0.5;

    /// <summary>Text size for dimension labels and part names, in pixels.</summary>
    const double LabelTextSize = 12;

    /// <summary>How long an arrowhead is, in pixels.</summary>
    const double ArrowLength = 9;

    /// <summary>Half the width of an arrowhead, in pixels.</summary>
    const double ArrowHalfWidth = 2.75;

    /// <summary>The gap between what is measured and the start of its extension line, in pixels.</summary>
    const double ExtensionGap = 3;

    /// <summary>How far an extension line runs past the dimension line, in pixels.</summary>
    const double ExtensionOvershoot = 5;

    /// <summary>Half the width of a resize handle, in pixels.</summary>
    const double HandleHalfSize = 4;

    /// <summary>How near a handle the pointer has to be to grab it, in pixels.</summary>
    const double HandleGrabPixels = 7;

    /// <summary>How near another part's edge a drag has to land to snap to it, in pixels.</summary>
    const double SnapRadiusPixels = 10;

    /// <summary>How far a selected part's dimension lines sit off it, in pixels.</summary>
    const double SelectionDimensionOffsetPixels = 26;

    /// <summary>Half the width of the cross that marks a corner a cut took away, in pixels.</summary>
    const double VirtualCornerArm = 4;

    /// <summary>
    /// The scale a blank sheet opens at: an inch drawn at sixteen pixels, so about four and a
    /// half feet fits across a laptop window — a table, a bench, a run of cabinets — and the grid
    /// in force is a one-inch grid, which is the step a person drawing furniture wants to land on.
    /// </summary>
    public const double BlankSheetPixelsPerInch = 16;

    bool _fitPending = true;
    bool _panning;
    Point _panFrom;
    Point _pressedAt;
    ViewTransform _view = ViewTransform.Default;
    DesignEditor? _editor;
    IReadOnlyList<EntityId>? _partsLastSeen;

    readonly RectangleTool _rectangle = new();
    readonly StockTool _stock = new();
    EditTool _tool = EditTool.Select;

    Gesture _gesture = Gesture.None;
    EntityId _gestureEntity;
    BoxGrip _gestureGrip = BoxGrip.Body;
    Point2 _gestureAnchorAtPress;
    Point2 _gestureWorldAtPress;
    Box? _gestureBoxAtPress;
    UpdateResult? _gestureRefusal;
    EntityId? _pressedOnPart;
    Point2 _pressedOnPartAt;
    SnapPlan? _snap;
    EntityId? _hovered;
    System.Collections.Immutable.ImmutableHashSet<EntityId> _attention = [];

    static CanvasView() => FocusableProperty.OverrideDefaultValue<CanvasView>(true);

    /// <summary>Raised whenever the view transform changes, so a status bar can follow it.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>
    /// Raised as the pointer moves, with the model point under it, or null when it leaves.
    /// </summary>
    public event EventHandler<Point2?>? PointerWorldPositionChanged;

    /// <summary>Raised when the active tool changes, so a tool control can follow it.</summary>
    public event EventHandler? ToolChanged;

    /// <summary>Raised when a dimension label is clicked, or Tab asks for one.</summary>
    public event EventHandler<DimensionEditRequested>? DimensionEditRequested;

    /// <summary>
    /// Raised when a person asks to shape the selected part — the way into the shape workshop
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;7.1).
    /// </summary>
    /// <remarks>
    /// The canvas does not own the workshop, because the workshop is a mode the <em>window</em> is
    /// in and needs room the canvas does not have. So the keystroke is handled here, beside the
    /// other editing keys, and the window decides what to open.
    /// </remarks>
    public event EventHandler<EntityId>? ShapeRequested;

    /// <summary>
    /// Raised when a person asks for the 3D view — <c>V</c>, the key the 3D view answers with to go
    /// back. The window owns both views and decides which one is showing (assembly-model &#xA7;8.1).
    /// </summary>
    public event EventHandler? ModelViewRequested;

    /// <summary>
    /// Raised when the set of parts on the canvas changes — one drawn, one deleted, a different
    /// design opened — so the automation peer can rebuild its children.
    /// </summary>
    /// <remarks>
    /// Moving or resizing a part is not a change of the set and does not raise this: an element's
    /// name, value and rectangle are all read live from the sketch, so nothing about it is stale.
    /// </remarks>
    public event EventHandler? PartsChanged;

    /// <summary>The drawing being edited. The canvas draws what this holds and nothing else.</summary>
    public DesignEditor? Editor
    {
        get => _editor;
        set
        {
            if (ReferenceEquals(_editor, value))
            {
                return;
            }

            if (_editor is not null)
            {
                _editor.DesignChanged -= OnEditorDesignChanged;
                _editor.DesignOpened -= OnEditorDesignOpened;
                _editor.SelectionChanged -= OnEditorDesignChanged;
            }

            _editor = value;

            if (_editor is not null)
            {
                _editor.DesignChanged += OnEditorDesignChanged;
                _editor.DesignOpened += OnEditorDesignOpened;
                _editor.SelectionChanged += OnEditorDesignChanged;
            }

            OnEditorDesignOpened(this, EventArgs.Empty);
        }
    }

    /// <summary>The design on screen, or null before there is an editor.</summary>
    public Design? Design => _editor?.Design;

    /// <summary>Where the view is. Every navigation gesture changes this and nothing else.</summary>
    public ViewTransform View
    {
        get => _view;
        private set
        {
            if (_view == value)
            {
                return;
            }

            _view = value;
            InvalidateVisual();
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Which tool the pointer is holding.</summary>
    public EditTool Tool
    {
        get => _tool;
        set
        {
            if (_tool == value)
            {
                return;
            }

            _rectangle.Cancel();

            // Leaving the stock tool puts the stock down: the toolbox shows nothing picked, and a
            // later press on the paper cannot place something nobody is holding any more.
            if (value != EditTool.Stock)
            {
                _stock.Arm(null);
            }

            _tool = value;
            Cursor = new Cursor(DrawsOnPress ? StandardCursorType.Cross : StandardCursorType.Arrow);
            InvalidateVisual();
            ToolChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Raised when the part under the pointer changes, so the window can open the relationship
    /// list for a part that has some (#62).
    /// </summary>
    public event EventHandler? HoveredPartChanged;

    /// <summary>
    /// The part the pointer is resting on, or null. It follows the pointer only while nothing is
    /// being dragged, panned or drawn: a gesture's own part is what it is about, not a hover.
    /// </summary>
    public EntityId? HoveredPart => _hovered;

    /// <summary>
    /// How many pixels at the right of this view the window's side panels cover (#90): zoom to fit
    /// frames the drawing in what is left, and a part under them counts as out of view.
    /// </summary>
    public double FitReserveRight { get; set; }

    /// <summary>
    /// Parts to draw attention to, outlined in the problem colour: the ones a conflict or a refused
    /// turn names (#72), or the ones a relationship row under the pointer holds (#77).
    /// </summary>
    public System.Collections.Immutable.ImmutableHashSet<EntityId> Attention
    {
        get => _attention;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_attention.SetEquals(value))
            {
                return;
            }

            _attention = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The stock item the pointer is holding, picked from the toolbox, or null when it holds none.
    /// </summary>
    public StockItem? ArmedStock => _tool == EditTool.Stock ? _stock.Stock : null;

    /// <summary>
    /// Picks up a stock item from the toolbox, so that the next drag on the paper places a part
    /// already cut from it (issue #7's picker, <c>GUI-CUT-02</c>) — or puts it down with null.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the item cannot be placed as a part (a fastener), in which case
    /// the tool the pointer was holding is left as it was.
    /// </returns>
    public bool ArmStock(StockItem? item)
    {
        if (item is null)
        {
            if (_tool == EditTool.Stock)
            {
                Tool = EditTool.Select;
            }

            return true;
        }

        if (!StockTool.CanPlace(item))
        {
            return false;
        }

        bool changed = !ReferenceEquals(_stock.Stock, item) || _tool != EditTool.Stock;
        _stock.Arm(item);
        if (_tool == EditTool.Stock)
        {
            if (changed)
            {
                ToolChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            _rectangle.Cancel();
            _tool = EditTool.Stock;
            Cursor = new Cursor(StandardCursorType.Cross);
            ToolChanged?.Invoke(this, EventArgs.Empty);
        }

        InvalidateVisual();
        return true;
    }

    /// <summary>Whether a press on the paper starts drawing rather than picking or panning.</summary>
    bool DrawsOnPress => _tool is EditTool.Rectangle or EditTool.Stock;

    /// <summary>
    /// The precision dimension labels are shown at. Fixed at 1/16&#x2033;; the per-project picker
    /// is #11.
    /// </summary>
    public LengthFormat LabelFormat { get; } = new FeetInchesFormat(16);

    /// <summary>The grid step in force at this zoom, in inches.</summary>
    public double GridStepInches => SnapGrid.StepInches(_view.PixelsPerInch);

    /// <summary>Whether a part is being drawn or dragged right now.</summary>
    public bool IsEditing => _gesture != Gesture.None || _rectangle.IsDrawing || _stock.IsDrawing;

    /// <summary>What the drag in progress has caught, or null when nothing is being dragged.</summary>
    public SnapPlan? ActiveSnap => _snap;

    /// <summary>The bounding box of everything in the current design.</summary>
    public WorldBounds Extents =>
        Design is { } design ? SketchExtents.Of(design.Sketch) : WorldBounds.Empty;

    /// <summary>
    /// What every dimension in the current design reads, worked out from the geometry now.
    /// </summary>
    /// <remarks>
    /// Nothing is cached: this is the same call <see cref="Render"/> makes, so what a test reads
    /// here is what a person sees on the canvas, and changing the geometry changes both.
    /// </remarks>
    public IReadOnlyList<DimensionMeasurement> Measurements() =>
        Design is { } design ? [.. DimensionLayout.Measure(design.Sketch)] : [];

    /// <summary>
    /// Every part on the canvas, in the order <see cref="Render"/> draws them.
    /// </summary>
    /// <remarks>
    /// The automation peer walks this, so the elements a client enumerates are the parts a person
    /// sees, in the same order, from the same sketch.
    /// </remarks>
    internal IReadOnlyList<EntityId> PartsInOrder() =>
        Design is { } design
            ? [.. design.Sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id).Select(box => box.Id)]
            : [];

    /// <summary>What to call a part, the same name the status line and the messages use.</summary>
    internal string PartName(EntityId id) => _editor?.NameOf(id) ?? id.ToString();

    /// <summary>How a part is turned, as its mark in the plan says it (#82), or null for a part as drawn.</summary>
    internal string? PartStance(EntityId id) =>
        Design?.Sketch.Find<Box>(id) is { } box ? WorldWords.Stance(box) : null;

    /// <summary>
    /// A part's size as text, in the same format as the dimension labels drawn beside it.
    /// </summary>
    internal string? PartSize(EntityId id) =>
        Design?.Sketch.Find<Box>(id)?.Footprint() is { } footprint
            ? $"{Label(footprint.PlanWidth)} × {Label(footprint.PlanHeight)}"
            : null;

    /// <summary>
    /// Where a part is drawn, in canvas pixels: the rectangle <see cref="Outline"/> traces, taken
    /// from the same two corners and the same view transform.
    /// </summary>
    internal Rect? PartRectangle(EntityId id) =>
        Design?.Sketch.Find<Box>(id)?.Footprint() is { } footprint
            ? new Rect(
                _view.ToScreen(footprint.Corner(BoxCorner.SouthWest)),
                _view.ToScreen(footprint.Corner(BoxCorner.NorthEast))).Normalize()
            : null;

    /// <summary>What one dimension reads, found by the label of the design it belongs to.</summary>
    public string? LabelOf(EntityId dimension) => Measurements()
        .FirstOrDefault(measurement => measurement.Dimension.Id == dimension)
        ?.Label(LabelFormat);

    /// <summary>
    /// The width or height dimension shown for a selected part — the same measurement the canvas
    /// draws, so a test reads the label a person sees.
    /// </summary>
    public DimensionMeasurement? SelectionDimension(EntityId box, SizeAxis axis) =>
        Design is { } design
        && SelectionDimensions.TryFor(
            design.Sketch,
            box,
            axis,
            DimensionOffset(),
            out DimensionMeasurement? measurement,
            out _)
            ? measurement
            : null;

    /// <summary>Where a selected part's dimension label is drawn, in canvas pixels.</summary>
    public Point? SelectionDimensionLabelAt(EntityId box, SizeAxis axis) =>
        SelectionDimension(box, axis) is { } measurement
            ? _view.ToScreen(measurement.LabelAnchor)
            : null;

    /// <summary>
    /// Makes sure a box can be seen whole: when any corner of its footprint is off the canvas, the
    /// view zooms to fit the drawing. A duplicate lands beside its original (#71), and a copy of a
    /// large part can land past the edge of the view, where it could not be dragged into place.
    /// </summary>
    public void BringIntoView(EntityId id)
    {
        if (_editor?.Sketch.Find<Box>(id) is not { } box)
        {
            return;
        }

        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(box);
        Rect visible = new(0, 0, Math.Max(Bounds.Width - FitReserveRight, 1), Bounds.Height);
        foreach (Point2 corner in (Point2[])[new(low.X, low.Y), new(high.X, low.Y), new(high.X, high.Y), new(low.X, high.Y)])
        {
            if (!visible.Contains(_view.ToScreen(corner)))
            {
                ZoomToFit();
                return;
            }
        }
    }

    /// <summary>Frames the whole design, with a margin.</summary>
    public void ZoomToFit()
    {
        WorldBounds extents = Extents;
        if (!_view.HasViewport)
        {
            // No viewport to frame anything in yet: fit as soon as there is one.
            _fitPending = true;
            return;
        }

        if (extents.IsEmpty)
        {
            // A blank sheet has nothing to frame, so it is put at the origin at a scale a piece of
            // furniture fits in — about six feet across a laptop window. Leaving the view wherever
            // the last drawing left it would open a new sheet somewhere unknowable.
            _fitPending = false;
            View = new ViewTransform(0, 0, BlankSheetPixelsPerInch, _view.Viewport);
            return;
        }

        _fitPending = false;
        View = _view.FitTo(extents, _view.Viewport, coveredRight: FitReserveRight);
    }

    /// <summary>Zooms in one step, about the centre of the viewport.</summary>
    public void ZoomIn() => View = _view.ZoomAtCenter(ZoomPerKeyPress);

    /// <summary>Zooms out one step, about the centre of the viewport.</summary>
    public void ZoomOut() => View = _view.ZoomAtCenter(1 / ZoomPerKeyPress);

    /// <summary>Pans by a fraction of the viewport: positive x moves the view right.</summary>
    public void PanByFraction(double fractionX, double fractionY) =>
        View = _view.PanByViewportFraction(fractionX, fractionY);

    /// <summary>
    /// Applies a view keystroke, wherever it was received.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the key was a view command, so the caller can leave it alone if
    /// it was not.
    /// </returns>
    public bool HandleViewKey(Key key, KeyModifiers modifiers)
    {
        // Control on Windows, Command on macOS — and Control under the headless platform, which
        // reports no macOS windowing backend. Accepting either means one code path for both.
        bool command = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        double step = modifiers.HasFlag(KeyModifiers.Shift)
            ? FastPanFractionPerKeyPress
            : PanFractionPerKeyPress;

        switch (key)
        {
            case Key.D0 or Key.NumPad0 when command:
                ZoomToFit();
                return true;

            case Key.Left:
                PanByFraction(-step, 0);
                return true;

            case Key.Right:
                PanByFraction(step, 0);
                return true;

            case Key.Up:
                PanByFraction(0, step);
                return true;

            case Key.Down:
                PanByFraction(0, -step);
                return true;

            case Key.Add or Key.OemPlus:
                ZoomIn();
                return true;

            case Key.Subtract or Key.OemMinus:
                ZoomOut();
                return true;

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        Size arranged = base.ArrangeOverride(finalSize);
        if (_view.Viewport != arranged)
        {
            // A resize keeps the model point at the centre of the viewport where it is, and keeps
            // the scale: the window shows more or less of the drawing, not a different size of it.
            View = _view.WithViewport(arranged);
        }

        if (_fitPending)
        {
            ZoomToFit();
        }

        return arranged;
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        Point position = e.GetPosition(this);
        _pressedAt = position;
        Focus();

        if (properties.IsMiddleButtonPressed)
        {
            BeginPan(e.Pointer, position);
            return;
        }

        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_editor is not { } editor)
        {
            BeginPan(e.Pointer, position);
            return;
        }

        if (_tool == EditTool.Rectangle)
        {
            _rectangle.Begin(SnapGrid.Snap(_view.ToWorld(position), GridStepInches));
            e.Pointer.Capture(this);
            InvalidateVisual();
            return;
        }

        if (_tool == EditTool.Stock)
        {
            _stock.Begin(SnapGrid.Snap(_view.ToWorld(position), GridStepInches));
            e.Pointer.Capture(this);
            InvalidateVisual();
            return;
        }

        Point2 world = _view.ToWorld(position);

        // A press on a selected part's grip resizes it, and one on a selected part's body moves it.
        // A press on a part that is not selected is held until the pointer moves (#85): a drag
        // selects it and moves it, and a release where it went down is a click that picks it.
        // Empty paper, the middle button and Shift pan. Picking happens on release, where a click
        // and a drag can still be told apart.
        if (editor.OnlySelectedBox is { } selected
            && BoxGeometry.GripAt(selected, world, ModelLength(HandleGrabPixels)) is { } grip)
        {
            BeginEdit(e.Pointer, selected, grip, world);
            return;
        }

        if (editor.Selection.Count > 0 && PickAt(world) is { } picked && editor.Selection.Contains(picked)
            && editor.Design!.Sketch.Find<Box>(picked) is { } pickedBox)
        {
            BeginEdit(e.Pointer, pickedBox, BoxGrip.Body, world);
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) && PickAt(world) is { } unselected
            && editor.Design!.Sketch.Find<Box>(unselected) is not null)
        {
            _pressedOnPart = unselected;
            _pressedOnPartAt = world;
            e.Pointer.Capture(this);
            return;
        }

        BeginPan(e.Pointer, position);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);

        if (_pressedOnPart is { } held)
        {
            if (Math.Abs(position.X - _pressedAt.X) > 2 || Math.Abs(position.Y - _pressedAt.Y) > 2)
            {
                // The press was a drag after all: pick the part and move it, from where it was grabbed.
                _pressedOnPart = null;
                if (_editor is { } editor && editor.Design!.Sketch.Find<Box>(held) is { } box)
                {
                    editor.Select(held);
                    BeginEdit(e.Pointer, box, BoxGrip.Body, _pressedOnPartAt);
                    ContinueEdit(_view.ToWorld(position));
                }
            }
        }
        else if (_panning)
        {
            View = _view.PanByPixels(position - _panFrom);
            _panFrom = position;
        }
        else if (_rectangle.IsDrawing)
        {
            _rectangle.MoveTo(SnapGrid.Snap(_view.ToWorld(position), GridStepInches));
            InvalidateVisual();
        }
        else if (_stock.IsDrawing)
        {
            _stock.MoveTo(SnapGrid.Snap(_view.ToWorld(position), GridStepInches));
            InvalidateVisual();
        }
        else if (_gesture != Gesture.None)
        {
            ContinueEdit(_view.ToWorld(position));
        }
        else
        {
            Hover(PickAt(_view.ToWorld(position)));
        }

        PointerWorldPositionChanged?.Invoke(this, _view.ToWorld(position));
    }

    void Hover(EntityId? part)
    {
        if (_hovered == part)
        {
            return;
        }

        _hovered = part;
        HoveredPartChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Point position = e.GetPosition(this);

        if (_pressedOnPart is not null)
        {
            // Pressed on a part and let go where it went down: a click, which picks.
            _pressedOnPart = null;
            e.Pointer.Capture(null);
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                PickOn(position, e.KeyModifiers);
            }

            return;
        }

        if (_rectangle.IsDrawing)
        {
            CompleteRectangle();
        }
        else if (_stock.IsDrawing)
        {
            CompleteStock();
        }
        else if (_gesture != Gesture.None)
        {
            CompleteEdit();
        }
        else if (_panning && e.InitialPressMouseButton == MouseButton.Left)
        {
            // Measured from where the button went down, not from the last pointer sample: a drag
            // ends with the pointer standing still, and comparing against the last sample would
            // call every drag a click.
            bool moved = Math.Abs(position.X - _pressedAt.X) > 2 || Math.Abs(position.Y - _pressedAt.Y) > 2;
            EndPan(e.Pointer);

            // A left press that did not move the view is a click, and a click picks. Doing this on
            // release rather than on press is what lets the same button pan and select.
            if (!moved)
            {
                PickOn(position, e.KeyModifiers);
            }

            return;
        }

        EndPan(e.Pointer);
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        if (_rectangle.IsDrawing || _stock.IsDrawing)
        {
            _rectangle.Cancel();
            _stock.Cancel();
            InvalidateVisual();
        }

        if (_gesture != Gesture.None)
        {
            CompleteEdit();
        }

        _pressedOnPart = null;
        EndPan(null);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hover(null);
        PointerWorldPositionChanged?.Invoke(this, null);
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Point position = e.GetPosition(this);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Shift and the wheel pans. A trackpad reports both axes, so it pans in both.
            View = _view.PanByPixels(new Vector(
                e.Delta.X * PanPixelsPerWheelNotch,
                e.Delta.Y * PanPixelsPerWheelNotch));
        }
        else
        {
            double notches = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            View = _view.ZoomAt(position, Math.Pow(ZoomPerWheelNotch, notches));
        }

        PointerWorldPositionChanged?.Invoke(this, _view.ToWorld(position));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || HandleEditKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            return;
        }

        if (HandleViewKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// The editing keystrokes, handled here — with the canvas focused — rather than as window
    /// shortcuts, so that typing an <c>r</c> into a dimension field types an <c>r</c>.
    /// </summary>
    bool HandleEditKey(Key key, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta))
        {
            return false;
        }

        if (_editor is not { } editor)
        {
            return false;
        }

        switch (key)
        {
            case Key.R:
                Tool = EditTool.Rectangle;
                editor.Say(EditSeverity.Hint, "Rectangle tool: drag on the paper to draw a part.");
                return true;

            case Key.S:
                Tool = EditTool.Select;
                return true;

            case Key.Escape:
                if (_rectangle.IsDrawing || _stock.IsDrawing)
                {
                    _rectangle.Cancel();
                    _stock.Cancel();
                    InvalidateVisual();
                    return true;
                }

                if (DrawsOnPress)
                {
                    Tool = EditTool.Select;
                    return true;
                }

                if (editor.Selection.Count > 0)
                {
                    editor.ClearSelection();
                    return true;
                }

                return false;

            case Key.Delete or Key.Back:
                if (editor.Selection.Count == 0)
                {
                    return false;
                }

                SelectionCommands.Delete(editor);
                return true;

            case Key.P:
                if (editor.Selection.Count == 0)
                {
                    return false;
                }

                SelectionCommands.Pin(editor);
                return true;

            case Key.D:
                if (editor.Selection.Count == 0)
                {
                    return false;
                }

                if (SelectionCommands.Duplicate(editor, GridStepInches) is { } copy)
                {
                    BringIntoView(copy);
                }

                InvalidateVisual();
                return true;

            case Key.C:
                if (SelectionCommands.PartToShape(editor) is { } part)
                {
                    ShapeRequested?.Invoke(this, part);
                }

                return true;

            // A quarter turn of the selected part about a world axis, Shift the other way: the same
            // command the 3D view has (docs/design/assembly-model.md §8.3). About Z it is the plan's
            // own turn in place; about X or Y it stands the part on a side, and the footprint shows it.
            case Key.X or Key.Y or Key.Z:
                SelectionTurn.Turn(
                    editor,
                    key switch { Key.X => Axis.X, Key.Y => Axis.Y, _ => Axis.Z },
                    modifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                return true;

            case Key.V:
                ModelViewRequested?.Invoke(this, EventArgs.Empty);
                return true;

            case Key.Tab when editor.OnlySelectedBox is { } forWidth:
                DimensionEditRequested?.Invoke(
                    this,
                    new DimensionEditRequested(forWidth.Id, SizeAxis.Width));
                return true;

            case Key.Left or Key.Right or Key.Up or Key.Down when editor.Selection.Count > 0:
                Nudge(key, modifiers);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Moves the selection by one grid step — the keyboard's version of a drag.</summary>
    void Nudge(Key key, KeyModifiers modifiers)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        double step = GridStepInches * (modifiers.HasFlag(KeyModifiers.Shift) ? 4 : 1);
        Length distance = new(SnapGrid.UnitsPerStep(step));
        Vector2 delta = key switch
        {
            Key.Left => new Vector2(-distance, Length.Zero),
            Key.Right => new Vector2(distance, Length.Zero),
            Key.Up => new Vector2(Length.Zero, distance),
            _ => new Vector2(Length.Zero, -distance),
        };

        List<EntityId> moving = [.. editor.Selection.OrderBy(id => id)];
        string what = moving.Count == 1 ? $"Moved {editor.NameOf(moving[0])}" : $"Moved {moving.Count} parts";

        editor.BeginGesture(what);
        editor.Apply(Batch.Of([.. moving.Select(id => (Request)Drag.InPlan(id, delta))]), what);
        editor.EndGesture();
    }

    void BeginPan(IPointer pointer, Point position)
    {
        _panning = true;
        _panFrom = position;
        pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    void BeginEdit(IPointer pointer, Box box, BoxGrip grip, Point2 world)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        _gesture = grip == BoxGrip.Body ? Gesture.Move : Gesture.Resize;
        _gestureEntity = box.Id;
        _gestureGrip = grip;
        _gestureAnchorAtPress = box.Footprint().Anchor;
        _gestureWorldAtPress = world;
        _gestureBoxAtPress = box;
        _gestureRefusal = null;
        _snap = null;

        editor.BeginGesture(
            _gesture == Gesture.Move
                ? $"Moved {editor.NameOf(box.Id)}"
                : $"Resized {editor.NameOf(box.Id)}");

        pointer.Capture(this);
    }

    void ContinueEdit(Point2 world)
    {
        if (_editor is not { } editor || editor.Design!.Sketch.Find<Box>(_gestureEntity) is not { } box)
        {
            return;
        }

        Vector2 sincePress = world - _gestureWorldAtPress;

        if (_gesture == Gesture.Move)
        {
            SnapPlan plan = SnapResolver.Resolve(
                editor.Design.Sketch,
                box,
                _gestureAnchorAtPress + sincePress,
                GridStepInches,
                ModelLength(SnapRadiusPixels));

            _snap = plan;
            Vector2 delta = plan.Anchor - box.Footprint().Anchor;
            if (delta != Vector2.Zero)
            {
                Remember(editor.ApplyQuietly(Drag.InPlan(_gestureEntity, delta)));
            }

            InvalidateVisual();
            return;
        }

        // A resize is measured from the box as it was when the handle was grabbed, so a blocked
        // edge does not accumulate the difference and jump when it comes free.
        Box atPress = _gestureBoxAtPress!;
        List<Request> requests = [];
        foreach (BoxEdge side in BoxGeometry.EdgesOf(_gestureGrip))
        {
            // The handle is on a side of the footprint; what moves is the face of the blank the
            // plan sees there — for a box standing on a side, possibly its top or bottom, which
            // resizes its depth (docs/design/assembly-model.md §7.2).
            BoxFace face = atPress.Footprint().FaceAt(side);
            Length wantedSize = OutwardSize(atPress, side) + SnappedOutward(atPress, side, sincePress);
            Length delta = wantedSize - OutwardSize(box, side);
            if (delta != Length.Zero)
            {
                requests.Add(new DragFace(_gestureEntity, face, delta));
            }
        }

        if (requests.Count > 0)
        {
            Remember(editor.ApplyQuietly(requests.Count == 1 ? requests[0] : Batch.Of([.. requests])));
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Keeps the last refusal of a live gesture's quiet steps, so the drop can say why a part that
    /// did not move did not — "Top is pinned where it is", with the way out — rather than "Moved Top."
    /// </summary>
    void Remember(UpdateResult result)
    {
        if (result is not Succeeded)
        {
            _gestureRefusal = result;
        }
    }

    void CompleteEdit()
    {
        if (_editor is not { } editor)
        {
            _gesture = Gesture.None;
            return;
        }

        Gesture gesture = _gesture;
        SnapPlan? plan = _snap;
        Box? atPress = _gestureBoxAtPress;
        UpdateResult? refusal = _gestureRefusal;
        _gesture = Gesture.None;
        _snap = null;
        _gestureBoxAtPress = null;
        _gestureRefusal = null;

        string what = gesture == Gesture.Move
            ? $"Moved {editor.NameOf(_gestureEntity)}"
            : $"Resized {editor.NameOf(_gestureEntity)}";

        // A snap is stated only when the part got to where it put it: one it never reached — a
        // pinned part dragged at another — is not a relationship, and stating it would conflict.
        Box? now = editor.Design!.Sketch.Find<Box>(_gestureEntity);
        bool reached = gesture == Gesture.Move && plan is not null && now is not null && plan.Anchor == now.Footprint().Anchor;

        if (atPress is not null && now == atPress && !(reached && plan!.CaughtSomething))
        {
            // Nothing moved or changed size. Say why: the updater's own words when it refused a
            // step, the pin and the way out when the part was wanted somewhere else, or simply that
            // it is as it was.
            if (refusal is not null)
            {
                editor.Report(refusal, what);
            }
            else if (gesture == Gesture.Move && plan is not null && !reached)
            {
                SelectionCommands.SayStayedPut(editor, _gestureEntity, what);
            }
            else
            {
                editor.Say(EditSeverity.Done, $"{editor.NameOf(_gestureEntity)} is {(gesture == Gesture.Resize ? "the size" : "where")} it was.");
            }

            editor.EndGesture();
            InvalidateVisual();
            return;
        }

        List<Request> statements = [];
        if (reached)
        {
            foreach (Relationship candidate in plan!.Relationships)
            {
                if (editor.CanHold(candidate) && !editor.AlreadyStates(candidate))
                {
                    statements.Add(new AddRelationship(candidate));
                }
            }
        }

        if (statements.Count > 0)
        {
            // The drop states what the snap caught. Relationships are stored, never inferred, so
            // if this is not put to the updater the drawing knows nothing about the alignment a
            // person just made (design §3.2).
            UpdateResult result = editor.Apply(
                statements.Count == 1 ? statements[0] : Batch.Of([.. statements]),
                what + " and snapped it");

            if (result is not Succeeded)
            {
                // The move itself already happened; only the statement about it was refused, and
                // the message says which.
                editor.EndGesture();
                InvalidateVisual();
                return;
            }
        }
        else
        {
            editor.Say(EditSeverity.Done, what + ".");
        }

        editor.EndGesture();
        InvalidateVisual();
    }

    void CompleteRectangle()
    {
        if (_editor is not { } editor)
        {
            _rectangle.Cancel();
            return;
        }

        EntityId id = EntityId.New();
        if (!_rectangle.TryComplete(editor.LayerForNewParts(), id, editor.NextPartName(), out Request? request))
        {
            InvalidateVisual();
            editor.Say(EditSeverity.Hint, "Drag to draw a part — a click on its own makes nothing.");
            return;
        }

        editor.BeginGesture("Drew a part");
        UpdateResult result = editor.Apply(request, "Drew a part");
        if (result is Succeeded)
        {
            editor.Select(id);
            editor.Say(EditSeverity.Done, $"Drew {editor.NameOf(id)}, {Size(id)}.");
            Tool = EditTool.Select;
        }

        editor.EndGesture();
        InvalidateVisual();
    }

    /// <summary>
    /// Ends a stock drag: one batch that adds the part and states what the yard fixes about it, so
    /// the part is that stock from the moment it exists (issue #7).
    /// </summary>
    /// <remarks>
    /// Like the rectangle tool, the pointer goes back to Select afterwards, so the next click picks
    /// the part just placed rather than placing another. The toolbox stays open; picking an item
    /// in it again is one click.
    /// </remarks>
    void CompleteStock()
    {
        if (_editor is not { } editor || _stock.Stock is not { } stock)
        {
            _stock.Cancel();
            return;
        }

        EntityId id = EntityId.New();
        if (!_stock.TryComplete(editor.Sketch, editor.LayerForNewParts(), id, editor.NextPartName(), out Request? request))
        {
            InvalidateVisual();
            editor.Say(
                EditSeverity.Hint,
                $"Drag to place a {stock.Name} — its length is the way you drag. A click on its own makes nothing.");
            return;
        }

        string what = $"Placed a {stock.Name}";
        editor.BeginGesture(what);
        UpdateResult result = editor.Apply(request, what);
        if (result is Succeeded)
        {
            editor.Select(id);
            editor.Say(EditSeverity.Done, $"Placed {editor.NameOf(id)}, a {stock.Name}, {Size(id)}.");
            Tool = EditTool.Select;
        }

        editor.EndGesture();
        InvalidateVisual();
    }

    string Size(EntityId id)
    {
        if (_editor?.Design!.Sketch.Find<Box>(id) is not { } box)
        {
            return string.Empty;
        }

        Footprint footprint = box.Footprint();
        return $"{Label(footprint.PlanWidth)} by {Label(footprint.PlanHeight)}";
    }

    /// <summary>
    /// One length as the canvas writes it: at the label precision, with the <c>&#x2248;</c> marker
    /// when the text is not the stored value (docs/design/geometry-model.md &#xA7;1.4).
    /// </summary>
    string Label(Length length)
    {
        FormattedLength formatted = length.Format(LabelFormat);
        return formatted.IsExact ? formatted.Text : "≈" + formatted.Text;
    }

    /// <summary>
    /// Where a resize handle wants its edge, snapped to the grid so a dragged edge lands on a
    /// number a person would type.
    /// </summary>
    Length SnappedOutward(Box atPress, BoxEdge edge, Vector2 sincePress)
    {
        Length raw = BoxGeometry.OutwardDelta(atPress, edge, sincePress);
        Length size = OutwardSize(atPress, edge);
        Length snapped = SnapGrid.Snap(size + raw, GridStepInches);
        return snapped - size;
    }

    static Length OutwardSize(Box box, BoxEdge side) => BoxGeometry.SizeAcross(box, side);

    void PickOn(Point position, KeyModifiers modifiers)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        Point2 world = _view.ToWorld(position);

        if (editor.OnlySelectedBox is { } selected
            && ClickedDimension(selected, world) is { } axis)
        {
            DimensionEditRequested?.Invoke(this, new DimensionEditRequested(selected.Id, axis));
            return;
        }

        EntityId? picked = PickAt(world);
        if (picked is not { } id)
        {
            editor.ClearSelection();
            return;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            editor.ToggleSelected(id);
        }
        else
        {
            editor.Select(id);
            editor.Say(EditSeverity.Done, $"{editor.NameOf(id)} selected, {Size(id)}.");
        }
    }

    /// <summary>Which of a selected part's dimension labels is under a model point, if any.</summary>
    SizeAxis? ClickedDimension(Box box, Point2 world)
    {
        foreach (SizeAxis axis in (SizeAxis[])[SizeAxis.Width, SizeAxis.Height])
        {
            if (SelectionDimension(box.Id, axis) is { } measurement)
            {
                Point label = _view.ToScreen(measurement.LabelAnchor);
                Point at = _view.ToScreen(world);
                if (Math.Abs(label.X - at.X) <= 34 && Math.Abs(label.Y - at.Y) <= 11)
                {
                    return axis;
                }
            }
        }

        return null;
    }

    /// <summary>The smallest part under a model point, or null.</summary>
    /// <remarks>
    /// The test is against the <em>shape</em>, not the blank: a click in a corner that has been
    /// cut off selects whatever is under it, because nothing of this part is there
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.4).
    /// </remarks>
    EntityId? PickAt(Point2 world)
    {
        if (Design is not { } design)
        {
            return null;
        }

        Length tolerance = ModelLength(3);
        Box? best = null;

        foreach (Box box in design.Sketch.Entities.Values.OfType<Box>().OrderBy(entity => entity.Id))
        {
            if (!BoxGeometry.IsWithinShape(box, world, tolerance))
            {
                continue;
            }

            // The smallest part wins: a leg sitting on a table top is the one that was aimed at.
            if (best is null || BoxGeometry.Area(box) < BoxGeometry.Area(best))
            {
                best = box;
            }
        }

        return best?.Id;
    }

    /// <summary>A distance in pixels, as a model length at the current zoom.</summary>
    Length ModelLength(double pixels) =>
        Length.FromInches(pixels / Math.Max(_view.PixelsPerInch, 1e-9), Rounding.HalfAwayFromZero);

    Length DimensionOffset() => ModelLength(SelectionDimensionOffsetPixels);

    void OnEditorDesignChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
        NotePartsIfChanged();
    }

    void OnEditorDesignOpened(object? sender, EventArgs e)
    {
        // A new design is framed the moment there is a viewport to frame it in.
        _fitPending = true;
        ZoomToFit();
        InvalidateVisual();
        NotePartsIfChanged();
        Hover(null);
    }

    /// <summary>
    /// Raises <see cref="PartsChanged"/> when the set of parts is not the one last seen.
    /// </summary>
    /// <remarks>
    /// Nothing is walked while nobody is listening: a peer is only built once a client has asked
    /// the canvas for one, and a drag raises <c>DesignChanged</c> on every pointer sample.
    /// </remarks>
    void NotePartsIfChanged()
    {
        if (PartsChanged is null)
        {
            return;
        }

        IReadOnlyList<EntityId> parts = PartsInOrder();
        if (_partsLastSeen is not null && _partsLastSeen.SequenceEqual(parts))
        {
            return;
        }

        _partsLastSeen = parts;
        PartsChanged.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Recording the parts on the way past is what lets <see cref="NotePartsIfChanged"/> stay
    /// silent while nobody is listening and still announce the first change after somebody starts.
    /// </remarks>
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        _partsLastSeen = PartsInOrder();
        return new CanvasAutomationPeer(this);
    }

    void EndPan(IPointer? pointer)
    {
        if (!_panning)
        {
            pointer?.Capture(null);
            return;
        }

        _panning = false;
        Cursor = new Cursor(DrawsOnPress ? StandardCursorType.Cross : StandardCursorType.Arrow);
        pointer?.Capture(null);
    }

    // -------------------------------------------------------------------------------------
    // Drawing
    // -------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        Rect viewport = new(Bounds.Size);
        context.FillRectangle(new SolidColorBrush(palette.Background), viewport);
        DrawGrid(context, palette, viewport);

        if (Design is not { } design)
        {
            return;
        }

        Sketch sketch = design.Sketch;
        Dictionary<LayerId, string> layerNames = sketch.Layers.ToDictionary(
            layer => layer.Id,
            layer => layer.Name);

        foreach (Entity entity in sketch.Entities.Values.OrderBy(item => item.Id))
        {
            string layerName = layerNames.TryGetValue(entity.Layer, out string? name) ? name : string.Empty;
            switch (entity)
            {
                case Box box:
                    DrawBox(context, palette, design, box, layerName);
                    break;

                case Segment segment
                    when sketch.Find<Node>(segment.Start) is { } start
                         && sketch.Find<Node>(segment.End) is { } end:
                    DrawSegment(context, palette, layerName, start.Position, end.Position);
                    break;

                case Node node:
                    DrawNode(context, palette, node.Position);
                    break;
            }
        }

        // Dimensions last, so a dimension line is never hidden under a part.
        foreach (DimensionMeasurement measurement in DimensionLayout.Measure(sketch))
        {
            DrawDimension(context, palette, measurement, palette.Dimension);
        }

        DrawBlankHints(context, palette, sketch);
        DrawAttention(context, palette, sketch);
        DrawSelection(context, palette, sketch);
        DrawSnapIndicator(context, palette);
        DrawRectanglePreview(context, palette);
        DrawNorth(context, palette);
    }

    /// <summary>
    /// A north arrow in the corner (#81): the relationship list and the Part panel name sides by the
    /// compass, and the plan is drawn north up, so the page says which way that is.
    /// </summary>
    void DrawNorth(DrawingContext context, CanvasPalette palette)
    {
        Point foot = new(24, Bounds.Height - 18);
        Point tip = foot - new Vector(0, 22);
        SolidColorBrush ink = new(palette.Label, 0.75);
        context.DrawLine(new Pen(ink, 1.4), foot, tip);

        StreamGeometry head = new();
        using (StreamGeometryContext figure = head.Open())
        {
            figure.BeginFigure(tip - new Vector(0, 3), isFilled: true);
            figure.LineTo(tip + new Vector(-4, 6));
            figure.LineTo(tip + new Vector(4, 6));
            figure.EndFigure(isClosed: true);
        }

        context.DrawGeometry(ink, null, head);
        FormattedText north = Text("N", ink.Color);
        context.DrawText(north, new Point(foot.X + 6, tip.Y - 2));
    }

    /// <summary>A wide outline in the problem colour round each part in <see cref="Attention"/>, under the selection's.</summary>
    void DrawAttention(DrawingContext context, CanvasPalette palette, Sketch sketch)
    {
        if (_attention.IsEmpty)
        {
            return;
        }

        Pen pen = new(new SolidColorBrush(palette.Snap, 0.85), 4.5) { LineJoin = PenLineJoin.Round };
        foreach (EntityId id in _attention.OrderBy(entity => entity))
        {
            if (sketch.Find<Box>(id) is { } box)
            {
                context.DrawGeometry(null, pen, Outline(box));
            }
        }
    }

    void DrawSelection(DrawingContext context, CanvasPalette palette, Sketch sketch)
    {
        if (_editor is not { } editor || editor.Selection.Count == 0)
        {
            return;
        }

        Pen pen = new(new SolidColorBrush(palette.Selection), 2.2) { LineJoin = PenLineJoin.Miter };

        foreach (EntityId id in editor.Selection.OrderBy(entity => entity))
        {
            if (sketch.Find<Box>(id) is not { } box)
            {
                continue;
            }

            context.DrawGeometry(null, pen, Outline(box));
        }

        if (editor.OnlySelectedBox is not { } only)
        {
            return;
        }

        // The width and height a selected part shows, as dimension graphics: the same lines,
        // arrowheads and text a stored dimension draws, in the selection's colour. Clicking one
        // opens it for typing.
        foreach (SizeAxis axis in (SizeAxis[])[SizeAxis.Width, SizeAxis.Height])
        {
            if (SelectionDimensions.TryFor(
                    sketch,
                    only.Id,
                    axis,
                    DimensionOffset(),
                    out DimensionMeasurement? measurement,
                    out bool annotated)
                && !annotated)
            {
                DrawDimension(context, palette, measurement, palette.Selection);
            }
        }

        SolidColorBrush handleFill = new(palette.Background);
        SolidColorBrush handleEdge = new(palette.Selection);
        Pen handlePen = new(handleEdge, 1.4);

        foreach (BoxGrip grip in BoxGeometry.CornerGrips.Concat(BoxGeometry.EdgeGrips))
        {
            Point at = _view.ToScreen(BoxGeometry.GripPoint(only, grip));
            context.DrawRectangle(
                handleFill,
                handlePen,
                new Rect(
                    at.X - HandleHalfSize,
                    at.Y - HandleHalfSize,
                    HandleHalfSize * 2,
                    HandleHalfSize * 2));
        }
    }

    /// <summary>
    /// Marks the blank where a relationship holds on to something a cut took away
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.1, &#xA7;2.5, &#xA7;7.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every reference binds to the blank, not to the shape (&#xA7;2.1), so a <see cref="Flush"/>
    /// can be flush with an edge line a curve replaced and a <see cref="Coincident"/> can hold a
    /// corner that has been rounded right off. Left undrawn, that looks bound to nothing. So the
    /// corner gets a small cross where it would have been, and the edge the blank has lost is
    /// drawn as a faint line.
    /// </para>
    /// <para>
    /// Both what the drawing already says and what a drag is catching right now are marked, in
    /// the colour each already uses — the stored ones in the dimension ink, the live ones in the
    /// snap's, beside the snap indicator itself, which is what &#xA7;2.5's gusset needs to be
    /// snappable at all.
    /// </para>
    /// </remarks>
    void DrawBlankHints(DrawingContext context, CanvasPalette palette, Sketch sketch)
    {
        DrawBlankHints(context, sketch, sketch.RelationshipsInOrder, palette.Dimension);
        if (_snap is { } plan)
        {
            DrawBlankHints(context, sketch, plan.Relationships, palette.Snap);
        }
    }

    void DrawBlankHints(
        DrawingContext context,
        Sketch sketch,
        IEnumerable<Relationship> relationships,
        Color ink)
    {
        HashSet<(EntityId Box, BoxCorner Corner)> corners = [];
        HashSet<(EntityId Box, BoxEdge Edge)> edges = [];

        foreach (Relationship relationship in relationships)
        {
            corners.UnionWith(RelationshipSites.CornersOf(relationship));
            edges.UnionWith(RelationshipSites.EdgesOf(relationship));
        }

        if (corners.Count == 0 && edges.Count == 0)
        {
            return;
        }

        Pen faint = new(new SolidColorBrush(ink, 0.5), 1) { DashStyle = new DashStyle([3, 3], 0) };
        Pen cross = new(new SolidColorBrush(ink, 0.9), 1.2);

        foreach ((EntityId id, BoxEdge edge) in edges.OrderBy(entry => entry.Box).ThenBy(entry => entry.Edge))
        {
            if (sketch.Find<Box>(id) is not { Cuts.IsEmpty: false } box)
            {
                continue;
            }

            DrawMissingEdge(context, faint, box, edge, nearOnly: null);
        }

        foreach ((EntityId id, BoxCorner corner) in corners.OrderBy(entry => entry.Box).ThenBy(entry => entry.Corner))
        {
            if (sketch.Find<Box>(id) is not { Cuts.IsEmpty: false } box
                || !BlankShape.IsVirtualCorner(box, corner))
            {
                continue;
            }

            foreach (BoxEdge edge in BlankShape.EdgesAt(corner))
            {
                DrawMissingEdge(context, faint, box, edge, nearOnly: corner);
            }

            Point at = _view.ToScreen(box.Corner(corner));
            context.DrawLine(
                cross,
                new Point(at.X - VirtualCornerArm, at.Y - VirtualCornerArm),
                new Point(at.X + VirtualCornerArm, at.Y + VirtualCornerArm));
            context.DrawLine(
                cross,
                new Point(at.X - VirtualCornerArm, at.Y + VirtualCornerArm),
                new Point(at.X + VirtualCornerArm, at.Y - VirtualCornerArm));
        }
    }

    /// <summary>
    /// Draws the parts of one blank edge the cuts took away, and nothing else: a curve replaces
    /// the whole line, a corner cut or a roundover eats a setback off one end.
    /// </summary>
    /// <remarks>
    /// Only the missing parts, so the faint line never runs over an edge the part still has —
    /// which would be one line drawn twice, and would read as a heavier edge rather than as a
    /// note about the blank.
    /// </remarks>
    /// <param name="nearOnly">
    /// When given, only the portion at that corner is drawn: what a reference to <em>that</em>
    /// corner is holding on to.
    /// </param>
    void DrawMissingEdge(DrawingContext context, Pen pen, Box box, BoxEdge edge, BoxCorner? nearOnly)
    {
        (BoxCorner from, BoxCorner to) = Box.Ends(edge);
        Point start = _view.ToScreen(box.Corner(from));
        Point end = _view.ToScreen(box.Corner(to));

        if (BlankShape.CutAt(box, CutSite.Edge(edge)) is CurvedEdge)
        {
            // A curved edge replaced the line entirely, however it bows: the line a Flush is
            // flush with is the blank's, and none of it is drawn.
            context.DrawLine(pen, start, end);
            return;
        }

        foreach ((BoxCorner corner, Point at, Point towards) in
                 (( BoxCorner, Point, Point)[])[(from, start, end), (to, end, start)])
        {
            if (nearOnly is { } only && corner != only)
            {
                continue;
            }

            Length setback = BlankShape.SetbackAlong(box, corner, edge);
            if (setback <= Length.Zero)
            {
                continue;
            }

            Vector along = towards - at;
            double length = along.Length;
            if (length < 1e-6)
            {
                continue;
            }

            double pixels = setback.ToInches() * _view.PixelsPerInch;
            context.DrawLine(pen, at, at + (along / length * pixels));
        }
    }

    void DrawSnapIndicator(DrawingContext context, CanvasPalette palette)
    {
        if (_snap is not { } plan)
        {
            return;
        }

        Pen pen = new(new SolidColorBrush(palette.Snap), 1.4) { DashStyle = new DashStyle([5, 4], 0) };

        foreach (SnapHit hit in plan.Hits)
        {
            if (hit.Kind == SnapKind.Grid)
            {
                continue;
            }

            // A plan snap holds X or Y; Point2.WithComponent refuses anything else rather than
            // drawing a Z snap as a Y one.
            Axis across = hit.Axis == Axis.X ? Axis.Y : Axis.X;
            Point from = _view.ToScreen(Point2.Origin.WithComponent(hit.Axis, hit.Coordinate).WithComponent(across, hit.From));
            Point to = _view.ToScreen(Point2.Origin.WithComponent(hit.Axis, hit.Coordinate).WithComponent(across, hit.To));

            context.DrawLine(pen, from, to);

            Point middle = new((from.X + to.X) / 2, (from.Y + to.Y) / 2);
            DrawDiamond(context, new SolidColorBrush(palette.Snap), middle, 4.5);
        }
    }

    /// <summary>
    /// The part a release would make now, from whichever drawing tool is dragging — for a stock
    /// part, the stock's own width across the drag, not wherever the pointer happens to be across
    /// it, because that is what the release will really make.
    /// </summary>
    bool TryPreview(out Point2 anchor, out Length width, out Length height, out string? stockName)
    {
        stockName = _stock.IsDrawing ? _stock.Stock?.Name : null;
        return _stock.IsDrawing
            ? _stock.TryShape(out anchor, out width, out height, out _)
            : _rectangle.IsDrawing & _rectangle.TryRectangle(out anchor, out width, out height);
    }

    void DrawRectanglePreview(DrawingContext context, CanvasPalette palette)
    {
        if (!TryPreview(out Point2 anchor, out Length width, out Length height, out string? stockName))
        {
            return;
        }

        Point southWest = _view.ToScreen(anchor);
        Point northEast = _view.ToScreen(new Point2(anchor.X + width, anchor.Y + height));
        Rect rectangle = new Rect(southWest, northEast).Normalize();

        Pen pen = new(new SolidColorBrush(palette.Selection), 1.6)
        {
            DashStyle = new DashStyle([4, 3], 0),
        };
        context.DrawRectangle(new SolidColorBrush(palette.PreviewFill), pen, rectangle);

        // The size while the part is still being dragged out: read from the two corners, the same
        // way the part's dimensions will read it a moment later (CVS-007).
        string size = $"{Label(width)} × {Label(height)}";
        FormattedText text = Text(stockName is null ? size : $"{stockName}  {size}", palette.Selection);

        Point at = new(rectangle.Center.X - (text.Width / 2), rectangle.Bottom + 6);
        context.DrawRectangle(
            new SolidColorBrush(palette.Background),
            null,
            new RoundedRect(new Rect(at.X - 4, at.Y - 2, text.Width + 8, text.Height + 4), 2));
        context.DrawText(text, at);
    }

    static void DrawDiamond(DrawingContext context, IBrush brush, Point centre, double radius)
    {
        StreamGeometry diamond = new();
        using (StreamGeometryContext geometry = diamond.Open())
        {
            geometry.BeginFigure(new Point(centre.X, centre.Y - radius), isFilled: true);
            geometry.LineTo(new Point(centre.X + radius, centre.Y));
            geometry.LineTo(new Point(centre.X, centre.Y + radius));
            geometry.LineTo(new Point(centre.X - radius, centre.Y));
            geometry.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, null, diamond);
    }

    /// <summary>
    /// The shape a part is drawn as: its four corners when nothing has been cut off it, and the
    /// boundary <see cref="Box.Outline"/> derives when something has
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;1.5).
    /// </summary>
    /// <remarks>
    /// The plain case is kept as it was rather than routed through the outline builder, so that a
    /// rectangle is four <c>LineTo</c>s today as it was yesterday and a part with no cuts cannot
    /// be drawn differently by accident.
    /// </remarks>
    StreamGeometry Outline(Box box)
    {
        // A shaped part whose cap the plan sees is its cut outline, placed by the orientation; a
        // part on its side is its footprint, on which no cut shows (assembly-model §7.2).
        if (!box.Cuts.IsEmpty && PlanShape.ShowsCap(box))
        {
            return OutlineDrawing.GeometryOf(PlanShape.Outline(box), _view.ToScreen);
        }

        Footprint footprint = box.Footprint();
        StreamGeometry outline = new();
        using (StreamGeometryContext geometry = outline.Open())
        {
            geometry.BeginFigure(_view.ToScreen(footprint.Corner(BoxCorner.SouthWest)), isFilled: true);
            geometry.LineTo(_view.ToScreen(footprint.Corner(BoxCorner.SouthEast)));
            geometry.LineTo(_view.ToScreen(footprint.Corner(BoxCorner.NorthEast)));
            geometry.LineTo(_view.ToScreen(footprint.Corner(BoxCorner.NorthWest)));
            geometry.EndFigure(isClosed: true);
        }

        return outline;
    }

    void DrawGrid(DrawingContext context, CanvasPalette palette, Rect viewport)
    {
        double minor = SnapGrid.StepInches(_view.PixelsPerInch);
        double major = SnapGrid.CoarserStepInches(minor, _view.PixelsPerInch);

        Pen minorPen = new(new SolidColorBrush(palette.GridMinor), 1);
        Pen majorPen = new(new SolidColorBrush(palette.GridMajor), 1);

        (double left, double top) = _view.ToWorldInches(viewport.TopLeft);
        (double right, double bottom) = _view.ToWorldInches(viewport.BottomRight);

        foreach (double x in Steps(left, right, minor))
        {
            double screenX = _view.ToScreen(x, 0).X;
            bool isMajor = major > 0 && IsMultiple(x, major);
            context.DrawLine(
                isMajor ? majorPen : minorPen,
                new Point(screenX, viewport.Top),
                new Point(screenX, viewport.Bottom));
        }

        foreach (double y in Steps(bottom, top, minor))
        {
            double screenY = _view.ToScreen(0, y).Y;
            bool isMajor = major > 0 && IsMultiple(y, major);
            context.DrawLine(
                isMajor ? majorPen : minorPen,
                new Point(viewport.Left, screenY),
                new Point(viewport.Right, screenY));
        }
    }

    void DrawBox(
        DrawingContext context,
        CanvasPalette palette,
        Design design,
        Box box,
        string layerName)
    {
        EntityStyle style = palette.StyleFor(layerName);
        Pen pen = new(new SolidColorBrush(style.Stroke), style.StrokeThickness)
        {
            LineJoin = PenLineJoin.Miter,
            DashStyle = style.Dashed ? new DashStyle([4, 3], 0) : null,
        };
        context.DrawGeometry(new SolidColorBrush(style.Fill), pen, Outline(box));

        Rect drawn = new Rect(
            _view.ToScreen(box.Footprint().Corner(BoxCorner.SouthWest)),
            _view.ToScreen(box.Footprint().Corner(BoxCorner.NorthEast))).Normalize();
        string? stance = WorldWords.Stance(box);
        bool labelled = design.LabelFor(box.Id) is { } label && DrawPartLabel(context, palette, label, drawn, stance is null ? 0 : -7);
        if (stance is not null)
        {
            DrawStance(context, palette, stance, drawn, labelled);
        }
    }

    /// <summary>
    /// The mark a turned part carries in the plan (#82, assembly-model §7.2): which of its sizes
    /// stands up now, under its name, so a leg standing on end and a leg lying down read
    /// differently at a glance. Where the words do not fit, the arrow alone.
    /// </summary>
    void DrawStance(DrawingContext context, CanvasPalette palette, string stance, Rect drawn, bool underALabel)
    {
        FormattedText text = Text(stance, palette.Dimension);
        if (text.Width + 8 > drawn.Width || text.Height + 4 > drawn.Height)
        {
            text = Text(stance[..1], palette.Dimension);
            if (text.Width + 2 > drawn.Width || text.Height + 2 > drawn.Height)
            {
                return;
            }
        }

        double y = drawn.Center.Y - (text.Height / 2) + (underALabel ? 7 : 0);
        context.DrawText(text, new Point(drawn.Center.X - (text.Width / 2), y));
    }

    /// <returns>Whether the name fitted and was drawn.</returns>
    bool DrawPartLabel(
        DrawingContext context,
        CanvasPalette palette,
        string label,
        Rect rectangle,
        double lift)
    {
        FormattedText text = Text(label, palette.Label);
        if (text.Width + 8 > rectangle.Width || text.Height + 4 > rectangle.Height)
        {
            // It does not fit. A part too small for its name is better unlabelled than overdrawn;
            // zooming in brings the name back.
            return false;
        }

        context.DrawText(text, new Point(
            rectangle.Center.X - (text.Width / 2),
            rectangle.Center.Y - (text.Height / 2) + lift));
        return true;
    }

    void DrawSegment(
        DrawingContext context,
        CanvasPalette palette,
        string layerName,
        Point2 start,
        Point2 end)
    {
        EntityStyle style = palette.StyleFor(layerName);
        context.DrawLine(
            new Pen(new SolidColorBrush(style.Stroke), style.StrokeThickness),
            _view.ToScreen(start),
            _view.ToScreen(end));
    }

    void DrawNode(DrawingContext context, CanvasPalette palette, Point2 position)
    {
        Point centre = _view.ToScreen(position);
        context.DrawEllipse(new SolidColorBrush(palette.NodeFill), null, centre, 2.5, 2.5);
    }

    void DrawDimension(
        DrawingContext context,
        CanvasPalette palette,
        DimensionMeasurement measurement,
        Color ink)
    {
        Point from = _view.ToScreen(measurement.From);
        Point to = _view.ToScreen(measurement.To);
        Point lineFrom = _view.ToScreen(measurement.LineFrom);
        Point lineTo = _view.ToScreen(measurement.LineTo);

        Vector along = lineTo - lineFrom;
        double length = along.Length;
        if (length < 2)
        {
            // Zoomed far enough out that the whole dimension is a dot. Drawing it would be ink
            // over ink; the parts still read.
            return;
        }

        SolidColorBrush brush = new(ink);
        Pen pen = new(brush, 1);
        Vector direction = along / length;

        DrawExtensionLine(context, pen, from, lineFrom);
        DrawExtensionLine(context, pen, to, lineTo);

        // A dimension too short for two arrowheads gets them outside, pointing in, and the line
        // stubbed past each end — which is what a draughtsman does with a 1" gap.
        bool tight = length < 3.5 * ArrowLength;
        if (tight)
        {
            context.DrawLine(pen, lineFrom - (direction * ArrowLength), lineTo + (direction * ArrowLength));
        }
        else
        {
            context.DrawLine(pen, lineFrom, lineTo);
        }

        DrawArrowhead(context, brush, lineFrom, tight ? direction : -direction);
        DrawArrowhead(context, brush, lineTo, tight ? -direction : direction);

        FormattedText text = Text(measurement.Label(LabelFormat), ink);
        Point centre = new(
            (lineFrom.X + lineTo.X) / 2,
            (lineFrom.Y + lineTo.Y) / 2);

        // A dimension running up the page reads up the page, as it does on a drawing sheet.
        bool vertical = Math.Abs(direction.Y) > Math.Abs(direction.X);
        using (context.PushTransform(
                   Matrix.CreateRotation(vertical ? -Math.PI / 2 : 0)
                   * Matrix.CreateTranslation(centre.X, centre.Y)))
        {
            Rect chip = new(
                -(text.Width / 2) - 3,
                -(text.Height / 2) - 1,
                text.Width + 6,
                text.Height + 2);

            // The chip breaks the dimension line behind the text, which is what a drawing does.
            context.DrawRectangle(
                new SolidColorBrush(palette.Background),
                null,
                new RoundedRect(chip, 2));
            context.DrawText(text, new Point(-(text.Width / 2), -(text.Height / 2)));
        }
    }

    static void DrawExtensionLine(DrawingContext context, Pen pen, Point measured, Point line)
    {
        Vector away = line - measured;
        double length = away.Length;
        if (length <= ExtensionGap + 1)
        {
            return;
        }

        Vector direction = away / length;
        context.DrawLine(
            pen,
            measured + (direction * ExtensionGap),
            line + (direction * ExtensionOvershoot));
    }

    static void DrawArrowhead(DrawingContext context, IBrush brush, Point tip, Vector pointing)
    {
        Vector back = -pointing * ArrowLength;
        Vector side = new Vector(-pointing.Y, pointing.X) * ArrowHalfWidth;

        StreamGeometry head = new();
        using (StreamGeometryContext geometry = head.Open())
        {
            geometry.BeginFigure(tip, isFilled: true);
            geometry.LineTo(tip + back + side);
            geometry.LineTo(tip + back - side);
            geometry.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, null, head);
    }

    static FormattedText Text(string text, Color color) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        LabelTextSize,
        new SolidColorBrush(color));

    /// <summary>Every multiple of <paramref name="step"/> in a range, lowest first.</summary>
    static IEnumerable<double> Steps(double from, double to, double step)
    {
        double first = Math.Ceiling(from / step) * step;
        double count = Math.Floor((to - first) / step);
        if (!double.IsFinite(count) || count < 0)
        {
            yield break;
        }

        // A guard, not a policy: the ladder keeps lines at least MinimumGridSpacing apart, so this
        // can only be reached by a viewport far larger than a screen.
        int limit = (int)Math.Min(count, 4000);
        for (int i = 0; i <= limit; i++)
        {
            yield return first + (i * step);
        }
    }

    static bool IsMultiple(double value, double step)
    {
        double ratio = value / step;
        return Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
    }

    enum Gesture
    {
        None,
        Move,
        Resize,
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.App.Viewing;

/// <summary>What the 3D view asks the window to do with the selection.</summary>
public enum SelectionCommand
{
    /// <summary>Remove it, relationships and all.</summary>
    Delete,

    /// <summary>Pin it where it is.</summary>
    Pin,

    /// <summary>Copy it, beside it.</summary>
    Duplicate,

    /// <summary>Open it in the shape workshop.</summary>
    Shape,

    /// <summary>Copy it across the drawing's middle, east to west (#87).</summary>
    MirrorEastWest,

    /// <summary>Copy it across the drawing's middle, north to south (#87).</summary>
    MirrorNorthSouth,
}

/// <summary>
/// Draws a design in three dimensions, and lets a person turn, move and resize its parts there
/// (<c>docs/design/assembly-model.md</c> &#xA7;8).
/// </summary>
/// <remarks>
/// <para>
/// <strong>A mode of the window, not a second document.</strong> The 3D view holds the same
/// <see cref="DesignEditor"/> the plan canvas does, so it shares the drawing, the selection and the
/// undo stack with it (&#xA7;8.1, &#xA7;11 decision 13). Every change is a <see cref="Request"/> through
/// that editor — a move is a <see cref="Drag"/> along one axis or two, a resize a
/// <see cref="DragFace"/>, a turn a <see cref="SetOrientation"/>, a snap a <see cref="Flush"/> or a
/// <see cref="Coincident"/> stated on the drop — and nothing here builds a <see cref="Sketch"/>
/// (CVS-005).
/// </para>
/// <para>
/// <strong>Hand-rolled over Avalonia's 2D drawing, no 3D engine</strong> (&#xA7;8.4, &#xA7;11
/// decision 11). Every face of every part's <see cref="Solid"/> is projected by the
/// <see cref="Camera"/>, the ones facing away are culled, the rest are painted back to front — per
/// face, not per part — in three tones by the axis they face, with their edges as lines. Selection,
/// handles, the snap indicator and the markers on referenced features a cut took away are drawn
/// last, on top. The painter's algorithm is wrong where two parts pass through each other, and
/// &#xA7;8.4 accepts that rather than paying for a depth buffer.
/// </para>
/// <para>
/// <strong>Controls.</strong> Drag on empty space to orbit; click a part to select it (Shift adds
/// or removes); drag a part — selected or not (#85) — to slide it in the plane of the face you
/// pressed on; Shift-drag or the middle button pans; the wheel zooms about the pointer. On the
/// selected part: drag one of the three arrows to move it along that world axis, drag the square
/// on a face to resize it across that face. <c>X</c>, <c>Y</c> and <c>Z</c> turn it a
/// quarter turn about that axis, Shift the other way. Arrow keys orbit, +/&#x2212; zoom,
/// Ctrl/Cmd+0 frames the drawing, Home goes back to the isometric view, <c>V</c> goes back to the
/// plan. Delete, <c>P</c>, <c>D</c> and <c>C</c> do what they do on the plan.
/// </para>
/// </remarks>
public sealed class ModelView : Control
{
    const double ZoomPerWheelNotch = 1.15;
    const double ZoomPerKeyPress = 1.25;
    const double OrbitDegreesPerKeyPress = 15;
    const double PanPixelsPerWheelNotch = 60;

    /// <summary>How near a handle the pointer has to be to grab it, in pixels.</summary>
    public const double HandleGrabPixels = 8;

    /// <summary>How near a vertex or an edge the pointer has to be to pick it, in pixels.</summary>
    public const double FeatureGrabPixels = 5;

    /// <summary>How near another part's face a drag has to land to snap to it, in pixels.</summary>
    public const double SnapRadiusPixels = 10;

    const double HandleHalfSize = 4;
    const double VirtualCornerArm = 4;

    /// <summary>The colours of the three world axes: the usual red, green and blue.</summary>
    static readonly Color AxisX = Color.Parse("#D1453B");
    static readonly Color AxisY = Color.Parse("#3D9A4B");
    static readonly Color AxisZ = Color.Parse("#2F6FD0");

    /// <summary>
    /// How the 3D view opens: perspective, to judge how a design looks; orthographic, to measure
    /// with, is one keystroke away (<c>O</c>, View &gt; Orthographic).
    /// </summary>
    public const CameraProjection DefaultProjection = CameraProjection.Perspective;

    Camera _camera = Camera.Isometric() with { Projection = DefaultProjection };
    CameraProjection _projection = DefaultProjection;
    bool _fitPending = true;
    DesignEditor? _editor;
    ModelScene? _scene;
    Sketch? _sceneOf;

    Gesture _gesture = Gesture.None;
    Point _pressedAt;
    Point _lastPointer;
    EntityId _gestureEntity;
    Box? _boxAtPress;
    ModelHandle? _gestureHandle;
    Axis[] _planeAxes = [];
    SpaceSnapPlan? _snap;
    UpdateResult? _gestureRefusal;
    ModelPick? _pressedOnPart;
    readonly System.Text.StringBuilder _typed = new();
    EntityId[] _moving = [];
    Box? _groupAtPress;
    readonly PlacementTool _placement = new();
    PlacementPreview? _preview;
    PlacementFace? _placeFace;
    Point3 _placeFrom;
    EntityId _previewId = EntityId.New();
    Typeable? _typeable;
    EntityId? _hovered;
    System.Collections.Immutable.ImmutableHashSet<EntityId> _attention = [];

    static ModelView() => FocusableProperty.OverrideDefaultValue<ModelView>(true);

    /// <summary>Raised whenever the camera changes, so a status bar can follow it.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>Raised when the part under the pointer changes (#62's relationship list).</summary>
    public event EventHandler? HoveredPartChanged;

    /// <summary>Raised as the pointer moves, with the point on a part under it, or null.</summary>
    public event EventHandler<Vector3d?>? PointerModelPositionChanged;

    /// <summary>Raised when what the view holds to place changes: picked up, put down (#74).</summary>
    public event EventHandler? PlacementChanged;

    /// <summary>Raised when a person asks to go back to the plan.</summary>
    public event EventHandler? PlanRequested;

    /// <summary>
    /// Raised for a keystroke that acts on the selection the same way in both views — the window
    /// runs the plan canvas's own command for it, so there is one of each.
    /// </summary>
    public event EventHandler<SelectionCommand>? SelectionCommandRequested;

    /// <summary>The drawing being edited: the same editor the plan canvas holds.</summary>
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
                _editor.DesignChanged -= OnEditorChanged;
                _editor.SelectionChanged -= OnEditorChanged;
                _editor.DesignOpened -= OnEditorDesignOpened;
            }

            _editor = value;

            if (_editor is not null)
            {
                _editor.DesignChanged += OnEditorChanged;
                _editor.SelectionChanged += OnEditorChanged;
                _editor.DesignOpened += OnEditorDesignOpened;
            }

            OnEditorDesignOpened(this, EventArgs.Empty);
        }
    }

    /// <summary>Where the drawing is being looked at from. Every navigation changes this and nothing else.</summary>
    public Camera Camera
    {
        get => _camera;
        private set
        {
            if (_camera == value)
            {
                return;
            }

            _camera = value;
            InvalidateVisual();
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// How the 3D view turns space into a picture: orthographic to measure, perspective to judge how
    /// it looks. Kept when a design is opened or the view is reset.
    /// </summary>
    public CameraProjection Projection
    {
        get => _projection;
        set
        {
            _projection = value;
            Camera = _camera with { Projection = value };
        }
    }

    /// <summary>The grid step a move or a resize lands on at this zoom, in inches: the plan's ladder.</summary>
    public double GridStepInches => SnapGrid.StepInches(_camera.PixelsPerInch);

    bool _showGrid = true;

    /// <summary>Whether the grid lines are drawn. Only the drawing of them: snapping is <see cref="SnapToGrid"/>.</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (_showGrid != value)
            {
                _showGrid = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Whether a drag or a tool lands on the grid. Independent of whether the grid is drawn.</summary>
    public bool SnapToGrid { get; set; } = true;

    /// <summary>The step a drag or a tool snaps to: the grid's, or, with snapping off, the finest length there is (no rounding).</summary>
    public double SnapStepInches => SnapToGrid ? GridStepInches : 1.0 / Length.UnitsPerInch;

    /// <summary>The person pressed G: show or hide the grid.</summary>
    public event EventHandler? ToggleGridRequested;

    /// <summary>What the drag in progress has caught, or null when nothing is being dragged.</summary>
    public SpaceSnapPlan? ActiveSnap => _snap;

    /// <summary>
    /// What a drag in progress has done so far, in words, as the label by the pointer shows it
    /// (#86): how far the part has moved — "up 2 1/2"" — or how big the face being dragged has made
    /// it, and what the snap has caught — "flush with Top's bottom face". Null when nothing is being
    /// dragged.
    /// </summary>
    public string? LiveReadout
    {
        get
        {
            if (!IsEditing)
            {
                return _typed.Length > 0 ? $"{_typed} — Enter to apply it, Esc to drop it" : null;
            }

            if (_editor is not { } editor || _boxAtPress is not { } atPress
                || editor.Sketch.Find<Box>(_gestureEntity) is not { } box)
            {
                return null;
            }

            LengthFormat format = editor.LabelFormat;
            string done;
            if (_gesture == Gesture.Resize && _gestureHandle is { Face: { } face })
            {
                done = $"{SizeName(box, face)} {ModelHandles.SizeAcross(box, face).Format(format).Text}";
            }
            else
            {
                Vector3 moved = box.Anchor - atPress.Anchor;
                List<string> parts = [];
                foreach (Axis axis in (Axis[])[Axis.X, Axis.Y, Axis.Z])
                {
                    Length along = moved.Component(axis);
                    if (along != Length.Zero)
                    {
                        parts.Add($"{Way(axis, along > Length.Zero)} {Length.Abs(along).Format(format).Text}");
                    }
                }

                done = parts.Count == 0 ? "not moved" : string.Join(", ", parts);
            }

            if (_snap is { } plan && plan.Hits.FirstOrDefault(hit => hit.Kind != SnapKind.Grid) is { Target: { } target, TargetFace: { } targetFace })
            {
                string where = WorldWords.Feature(editor.Sketch.Find<Box>(target), BoxFeature.Face(targetFace));
                done += $" — flush with {editor.NameOf(target)}'s {where}";
            }

            return _typed.Length > 0 ? $"{done} — typed {_typed}, Enter" : done;

            static string Way(Axis axis, bool positive) => (axis, positive) switch
            {
                (Axis.X, true) => "east",
                (Axis.X, false) => "west",
                (Axis.Y, true) => "north",
                (Axis.Y, false) => "south",
                (Axis.Z, true) => "up",
                _ => "down",
            };

            static string SizeName(Box box, BoxFace face) =>
                box.Part is { } part
                    ? SceneWords.Of(face switch
                    {
                        BoxFace.East or BoxFace.West => part.PlanAxes.X,
                        BoxFace.North or BoxFace.South => part.PlanAxes.Y,
                        _ => part.PlanAxes.OutOfPlane,
                    })
                    : face switch
                    {
                        BoxFace.East or BoxFace.West => "Width",
                        BoxFace.North or BoxFace.South => "Height",
                        _ => "Depth",
                    };
        }
    }

    /// <summary>What the view holds to place on a face (#74): a stock size, a plain board, or nothing.</summary>
    public PlacementTool Placement => _placement;

    /// <summary>The part as it would be placed where the pointer is now, or null.</summary>
    public PlacementPreview? PlacementPreview => _placement.IsArmed ? _preview : null;

    /// <summary>Picks up a stock size to place, or puts everything down with null.</summary>
    /// <returns><see langword="false"/> when it cannot be placed — a fastener.</returns>
    public bool Arm(StockItem? item)
    {
        bool armed = _placement.Arm(item);
        AfterPlacementChange();
        return armed;
    }

    /// <summary>Picks up a plain board to place: the rectangle tool, in the 3D view.</summary>
    public void ArmPlainBoard()
    {
        _placement.ArmPlainBoard();
        AfterPlacementChange();
    }

    /// <summary>Puts down whatever is held.</summary>
    public void Disarm()
    {
        if (!_placement.IsArmed)
        {
            return;
        }

        _placement.Disarm();
        AfterPlacementChange();
    }

    void AfterPlacementChange()
    {
        _preview = null;
        _placeFace = null;
        InvalidateVisual();
        PlacementChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The part the pointer is resting on, or null.</summary>
    public EntityId? HoveredPart => _hovered;

    /// <summary>
    /// How many pixels at the right of this view the window's side panels cover (#90): zoom to fit
    /// frames the drawing in what is left, and a part under them counts as out of view.
    /// </summary>
    public double FitReserveRight { get; set; }

    /// <summary>
    /// Parts to draw attention to, their edges in the problem colour: the ones a conflict or a
    /// refused turn names (#72), or the ones a relationship row under the pointer holds (#77).
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

    /// <summary>Whether a part is being moved or resized right now.</summary>
    public bool IsEditing => _gesture is Gesture.MoveAxis or Gesture.MovePlane or Gesture.Resize;

    /// <summary>The drawing's polygons, as they are painted and picked now.</summary>
    public ModelScene Scene
    {
        get
        {
            Sketch sketch = _editor?.Sketch ?? Sketch.Empty;
            if (_scene is null || !ReferenceEquals(_sceneOf, sketch))
            {
                _scene = ModelScene.Of(sketch);
                _sceneOf = sketch;
            }

            return _scene;
        }
    }

    /// <summary>Where a model point is drawn in this control, in pixels.</summary>
    public Point ScreenOf(Point3 point) => _camera.Project(point);

    /// <summary>
    /// The selection's handles as they are drawn now: one part's arrows and face handles, or — for
    /// several parts — arrows from the middle of them all, which move them together (#87).
    /// </summary>
    public IReadOnlyList<ModelHandle> Handles =>
        _editor is not { } editor ? []
        : editor.OnlySelectedBox is { } box ? ModelHandles.Of(box, _camera)
        : GroupOf(editor) is { } group ? ModelHandles.Arrows(group, _camera)
        : [];

    /// <summary>
    /// Several selected parts as one box — their combined extent, lying as drawn — for their arrows
    /// and for snapping them as one; null unless more than one part is selected.
    /// </summary>
    static Box? GroupOf(DesignEditor editor)
    {
        Box[] boxes = SelectionCommands.SelectedBoxes(editor);
        if (boxes.Length < 2 || boxes.Any(box => !box.Orientation.IsExact))
        {
            return null;
        }

        (Point3 low, Point3 high) = GroupCopy.Extent(boxes);
        return new Box(EntityId.New(), LayerId.Default, low, high.X - low.X, high.Y - low.Y, high.Z - low.Z, BoxFace.Top, Angle.Zero);
    }

    /// <summary>The move arrow along a world axis on the selected part, when it is drawn.</summary>
    public ModelHandle? MoveHandle(Axis axis) =>
        Handles.FirstOrDefault(handle => handle.Kind == ModelHandleKind.Move && handle.Axis == axis);

    /// <summary>The resize handle on one face of the selected part, when that face can be seen.</summary>
    public ModelHandle? FaceHandle(BoxFace face) =>
        Handles.FirstOrDefault(handle => handle.Kind == ModelHandleKind.Face && handle.Face == face);

    /// <summary>What is under a point of this control, as a click there would pick it.</summary>
    public ModelPick? PickAt(Point point) => _editor is { } editor
        ? ModelPicker.Pick(editor.Sketch, Scene, _camera, point, FeatureGrabPixels)
        : null;

    EntityId[] _surfaceSelection = [];
    int _surfaceStep = -1;

    /// <summary>
    /// The X, Y or Z quick-snap (#132): look along that world axis and frame the design; again for the
    /// other side. Keeps the projection; only sets the angles, so the user can orbit away.
    /// </summary>
    public void LookAlong(Axis axis)
    {
        Camera = ViewSnap.LookAlong(_camera, axis);
        ZoomToFit();
    }

    /// <summary>
    /// The surface quick-snap (#132): with parts selected, the next face of the first selected part
    /// (<see cref="ViewSnap.FaceOrder"/>) square-on and framed on it; with nothing selected, the next
    /// of the six axis directions framing the whole design. The place in the cycle restarts when the
    /// selection changes.
    /// </summary>
    public void NextSurface()
    {
        Box[] boxes = _editor is { } editor ? SelectionCommands.SelectedBoxes(editor) : [];
        EntityId[] ids = [.. boxes.Select(selected => selected.Id)];
        if (!ids.SequenceEqual(_surfaceSelection))
        {
            _surfaceSelection = ids;
            _surfaceStep = -1;
        }

        _surfaceStep = (_surfaceStep + 1) % 6;
        if (boxes.Length > 0 && _camera.HasViewport)
        {
            Camera = ViewSnap.FaceOn(_camera, boxes[0], ViewSnap.FaceAt(_surfaceStep), FitReserveRight);
            return;
        }

        Camera = ViewSnap.Facing(_camera, ViewSnap.AxisStep(_surfaceStep));
        ZoomToFit();
    }

    /// <summary>Frames the whole drawing from the direction the camera is looking in.</summary>
    public void ZoomToFit()
    {
        if (!_camera.HasViewport)
        {
            _fitPending = true;
            return;
        }

        _fitPending = false;
        Bounds3 bounds = _editor is { } editor ? Bounds3.Of(editor.Sketch) : Bounds3.Empty;
        Camera = bounds.IsEmpty
            ? _camera with { CenterX = 0, CenterY = 0, CenterZ = 0, PixelsPerInch = CanvasView.BlankSheetPixelsPerInch }
            : _camera.FitTo(bounds, _camera.Viewport, coveredRight: FitReserveRight);
    }

    /// <summary>
    /// Makes sure a box can be seen whole: when any of its corners is off the view, the view zooms to
    /// fit the drawing — as the plan canvas does for a duplicate that landed past its edge (#71).
    /// </summary>
    public void BringIntoView(EntityId id)
    {
        if (_editor?.Sketch.Find<Box>(id) is not { } box)
        {
            return;
        }

        Rect visible = new(0, 0, Math.Max(Bounds.Width - FitReserveRight, 1), Bounds.Height);
        foreach (BoxCorner corner in (BoxCorner[])[BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
        {
            foreach (BoxLevel level in (BoxLevel[])[BoxLevel.Bottom, BoxLevel.Top])
            {
                if (!visible.Contains(_camera.Project(box.Vertex(corner, level))))
                {
                    ZoomToFit();
                    return;
                }
            }
        }
    }

    /// <summary>Back to the isometric view, framing the drawing.</summary>
    public void ResetView()
    {
        Camera = Camera.Isometric(_camera.Viewport) with { Projection = _projection };
        ZoomToFit();
    }

    /// <summary>Zooms in one step, about the centre of the view.</summary>
    public void ZoomIn() => Camera = _camera.ZoomAtCenter(ZoomPerKeyPress);

    /// <summary>Zooms out one step, about the centre of the view.</summary>
    public void ZoomOut() => Camera = _camera.ZoomAtCenter(1 / ZoomPerKeyPress);

    /// <summary>Turns the view by some degrees of azimuth and elevation, as the arrow keys do.</summary>
    public void OrbitBy(double azimuthDegrees, double elevationDegrees) =>
        Camera = _camera.OrbitBy(azimuthDegrees, elevationDegrees);

    /// <summary>
    /// Applies a view keystroke, wherever it was received.
    /// </summary>
    /// <returns><see langword="true"/> when the key was a view command.</returns>
    public bool HandleViewKey(Key key, KeyModifiers modifiers)
    {
        bool command = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        double step = OrbitDegreesPerKeyPress * (modifiers.HasFlag(KeyModifiers.Shift) ? 3 : 1);

        switch (key)
        {
            case Key.D0 or Key.NumPad0 when command:
                ZoomToFit();
                return true;

            case Key.Home when !command:
                ResetView();
                return true;

            case Key.O when !command:
                Projection = _projection == CameraProjection.Perspective ? CameraProjection.Orthographic : CameraProjection.Perspective;
                return true;

            case Key.Left:
                OrbitBy(step, 0);
                return true;

            case Key.Right:
                OrbitBy(-step, 0);
                return true;

            case Key.Up:
                OrbitBy(0, step);
                return true;

            case Key.Down:
                OrbitBy(0, -step);
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
        if (_camera.Viewport != arranged)
        {
            Camera = _camera.WithViewport(arranged);
        }

        if (_fitPending && IsVisible)
        {
            ZoomToFit();
        }

        return arranged;
    }

    // -------------------------------------------------------------------------------------
    // Pointer and keys
    // -------------------------------------------------------------------------------------

    readonly JointMarkerLayer _joints = new();

    /// <summary>The joint markers as last drawn, for the GUI suite to find one on the screen.</summary>
    public JointMarkerLayer JointMarkerLayer => _joints;

    /// <summary>Raised when a joint's marker is double-pressed: open it for editing.</summary>
    public event EventHandler<RelationshipId>? JointActivated;

    /// <summary>Raised by J (false) and Shift+J (true): join the selected parts.</summary>
    public event EventHandler<bool>? JoinRequested;

    /// <summary>Raised by Enter and Delete while a joint is selected.</summary>
    public event EventHandler<JointCommand>? JointCommandRequested;

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        Point position = e.GetPosition(this);
        _pressedAt = position;
        _lastPointer = position;
        _typeable = null;
        _typed.Clear();
        Focus();

        // A marker sits over the parts it joins: a press on it picks the joint, a double press opens it.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !_placement.IsArmed
            && _editor is { } jointEditor && _joints.At(position) is { } marker)
        {
            jointEditor.SelectJoint(marker.Marker.Id);
            if (e.ClickCount == 2)
            {
                JointActivated?.Invoke(this, marker.Marker.Id);
            }

            e.Handled = true;
            return;
        }

        // Holding something to place: a press on a face — or on the floor — starts placing it.
        if (_placement.IsArmed && properties.IsLeftButtonPressed && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && _editor is { } placing && FaceUnder(position, placing) is { } under)
        {
            if (under.Face is null)
            {
                placing.Say(EditSeverity.Hint, "That face is a cut, not a flat face of the blank: place it on a flat face, or on the floor.");
                return;
            }

            _placeFace = under.Face;
            _placeFrom = under.Point;
            _preview = Shaped(placing, _placeFrom);
            Begin(e.Pointer, Gesture.Placing);
            return;
        }

        if (properties.IsMiddleButtonPressed
            || (properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !OnSelection(position)))
        {
            Begin(e.Pointer, Gesture.Pan);
            return;
        }

        if (!properties.IsLeftButtonPressed || _editor is not { } editor)
        {
            return;
        }

        if (editor.OnlySelectedBox is { } selected
            && ModelHandles.At(selected, _camera, position, HandleGrabPixels) is { } handle)
        {
            BeginEdit(e.Pointer, selected, handle.Kind == ModelHandleKind.Move ? Gesture.MoveAxis : Gesture.Resize, handle, []);
            return;
        }

        if (GroupOf(editor) is { } group
            && ModelHandles.Nearest(ModelHandles.Arrows(group, _camera), position, HandleGrabPixels) is { } groupArrow
            && SelectionCommands.SelectedBoxes(editor) is [var first, ..])
        {
            BeginEdit(e.Pointer, first, Gesture.MoveAxis, groupArrow, [], group);
            return;
        }

        if (PickAt(position) is { } pick
            && editor.Selection.Contains(pick.Box)
            && editor.Sketch.Find<Box>(pick.Box) is { } body)
        {
            BeginEdit(e.Pointer, body, Gesture.MovePlane, null, PlaneOf(pick));
            return;
        }

        // A part that is not selected is held until the pointer moves (#85): a drag selects it
        // and moves it, a release where it went down is a click that picks it. Empty space orbits.
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) && PickAt(position) is { } unselected)
        {
            _pressedOnPart = unselected;
            Begin(e.Pointer, Gesture.Pending);
            return;
        }

        Begin(e.Pointer, Gesture.Orbit);
    }

    /// <summary>
    /// The two world axes a drag on a part's body moves along: the plane of the face that was
    /// pressed — never three, which would be a guess at depth the pointer cannot make (§8.3).
    /// </summary>
    static Axis[] PlaneOf(ModelPick pick) =>
        [.. ((Axis[])[Axis.X, Axis.Y, Axis.Z]).Where(axis => axis != pick.NormalAxis)];

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);
        if (_gesture == Gesture.None)
        {
            _joints.ShowTip(this, _editor is { } tipEditor ? _joints.TipAt(position, tipEditor.Sketch, tipEditor.NameOf) : null);
        }

        Vector step = position - _lastPointer;
        _lastPointer = position;

        switch (_gesture)
        {
            case Gesture.Orbit:
                Camera = _camera.Orbit(step);
                break;

            case Gesture.Pan:
                Camera = _camera.Pan(step);
                break;

            case Gesture.Pending when _pressedOnPart is { } held
                                      && (Math.Abs(position.X - _pressedAt.X) > 2 || Math.Abs(position.Y - _pressedAt.Y) > 2):
                // The press was a drag after all: pick the part and move it, from where it was grabbed.
                _pressedOnPart = null;
                if (_editor is { } editor && editor.Sketch.Find<Box>(held.Box) is { } box)
                {
                    editor.Select(held.Box);
                    BeginEdit(e.Pointer, box, Gesture.MovePlane, null, PlaneOf(held));
                    ContinueEdit(position);
                }

                break;

            case Gesture.MoveAxis or Gesture.MovePlane or Gesture.Resize:
                ContinueEdit(position);
                break;

            case Gesture.Placing when _editor is { } placing && _placeFace is { } face:
                // A drag along the face states the length, as the plan's stock tool does.
                if (OnFace(position, face) is { } to)
                {
                    _preview = Shaped(placing, to) ?? _preview;
                    InvalidateVisual();
                }

                break;

            default:
                if (_placement.IsArmed && _editor is { } hovering)
                {
                    _preview = FaceUnder(position, hovering) is { Face: { } face } under
                        ? ShapedOn(hovering, face, under.Point, under.Point)
                        : null;
                    InvalidateVisual();
                }

                ModelPick? pick = PickAt(position);
                Hover(pick?.Box);
                PointerModelPositionChanged?.Invoke(this, pick?.Point);
                break;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Point position = e.GetPosition(this);
        Gesture gesture = _gesture;

        if (gesture is Gesture.MoveAxis or Gesture.MovePlane or Gesture.Resize)
        {
            CompleteEdit();
            e.Pointer.Capture(null);
            return;
        }

        if (gesture == Gesture.Placing)
        {
            _gesture = Gesture.None;
            e.Pointer.Capture(null);
            bool dragged = Math.Abs(position.X - _pressedAt.X) > 2 || Math.Abs(position.Y - _pressedAt.Y) > 2;
            PlacementPreview? placed = dragged ? _preview : (_editor is { } editor ? Shaped(editor, _placeFrom) : null);
            if (placed is not null)
            {
                Place(placed);
            }

            _placeFace = null;
            return;
        }

        _gesture = Gesture.None;
        _pressedOnPart = null;
        e.Pointer.Capture(null);

        // A press that did not move the view is a click, and a click picks — measured from where the
        // button went down, as the plan canvas measures it.
        bool moved = Math.Abs(position.X - _pressedAt.X) > 2 || Math.Abs(position.Y - _pressedAt.Y) > 2;
        if (gesture is Gesture.Orbit or Gesture.Pan or Gesture.Pending && !moved && e.InitialPressMouseButton == MouseButton.Left)
        {
            PickOn(position, e.KeyModifiers);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (IsEditing)
        {
            CompleteEdit();
        }

        _gesture = Gesture.None;
        _pressedOnPart = null;
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hover(null);
        PointerModelPositionChanged?.Invoke(this, null);
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Point position = e.GetPosition(this);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            Camera = _camera.Pan(new Vector(e.Delta.X * PanPixelsPerWheelNotch, e.Delta.Y * PanPixelsPerWheelNotch));
        }
        else
        {
            double notches = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            Camera = _camera.ZoomAt(position, Math.Pow(ZoomPerWheelNotch, notches));
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || HandleTypingKey(e.Key) || HandleEditKey(e.Key, e.KeyModifiers) || HandleViewKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// A length typed for the arrow or face handle just dragged, or being dragged (#79): the
    /// characters of a length go into it, Enter applies it, Backspace takes one back, Escape drops it.
    /// </summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || _typeable is null || string.IsNullOrEmpty(e.Text)
            || !e.Text.All(character => char.IsAsciiDigit(character) || " ./'\"".Contains(character, StringComparison.Ordinal)))
        {
            return;
        }

        if (_typed.Length == 0 && string.IsNullOrWhiteSpace(e.Text))
        {
            return;
        }

        _typed.Append(e.Text);
        e.Handled = true;
        InvalidateVisual();
    }

    bool HandleTypingKey(Key key)
    {
        if (key == Key.Escape && _placement.IsArmed)
        {
            string holding = _placement.Holding;
            Disarm();
            _editor?.Say(EditSeverity.Done, $"Put down {holding}.");
            return true;
        }

        if (_typed.Length == 0)
        {
            return false;
        }

        switch (key)
        {
            case Key.Enter:
                if (IsEditing)
                {
                    // Still dragging: the drag ends here, and the typed length replaces where it got to.
                    CompleteEdit();
                }
                else
                {
                    CommitTyped();
                }

                return true;

            case Key.Escape:
                _typed.Clear();
                InvalidateVisual();
                return true;

            case Key.Back:
                _typed.Length--;
                InvalidateVisual();
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Puts the typed length in place of what the last arrow or face-handle drag did, as one undo
    /// step: that drag is undone, and the part is moved exactly that far from where the drag began,
    /// the way it was dragged, or made exactly that size across the face that was dragged (#79).
    /// </summary>
    void CommitTyped()
    {
        string text = _typed.ToString().Trim();
        _typed.Clear();
        InvalidateVisual();
        if (_editor is not { } editor || _typeable is not { } typeable)
        {
            return;
        }

        _typeable = null;
        if (!Length.TryParse(text, out Length typed, out _) || typed < Length.Zero
            || (typeable.Kind == Gesture.Resize && typed == Length.Zero))
        {
            editor.Say(EditSeverity.Problem, $"\"{text}\" is not a length to {(typeable.Kind == Gesture.Resize ? "make it" : "move it")}, like 3 1/2 or 1' 4\".");
            return;
        }

        if (editor.Sketch.Find<Box>(typeable.Id) is not { } now)
        {
            return;
        }

        if (typeable.After is { } after && !ReferenceEquals(after, typeable.Before))
        {
            if (!ReferenceEquals(editor.Design, after))
            {
                editor.Say(EditSeverity.Hint, "The drawing changed after that drag, so the typed length was not applied.");
                return;
            }

            editor.Undo();
        }

        string name = editor.NameOf(typeable.Id);
        LengthFormat format = editor.LabelFormat;
        Request exact;
        string what;
        if (typeable.Kind == Gesture.MoveAxis)
        {
            bool back = now.Anchor.Component(typeable.Axis) < typeable.AtPress.Anchor.Component(typeable.Axis);
            exact = new SetPosition(typeable.Id, typeable.AtPress.Anchor + Vector3.Along(typeable.Axis, back ? -typed : typed));
            what = $"Moved {name} {WorldWords.Facing(typeable.Axis, !back).Replace("top", "up", StringComparison.Ordinal).Replace("bottom", "down", StringComparison.Ordinal)} {typed.Format(format).Text}";
        }
        else
        {
            BoxFace face = typeable.Face!.Value;
            exact = new DragFace(typeable.Id, face, typed - ModelHandles.SizeAcross(typeable.AtPress, face));
            what = $"Resized {name} to {typed.Format(format).Text} across its {WorldWords.Feature(typeable.AtPress, BoxFeature.Face(face))}";
        }

        editor.BeginGesture(what);
        UpdateResult result = editor.Apply(exact, what);
        editor.EndGesture();

        // A resize is best effort: a cut can stop a face short. Say so rather than let the number
        // on the screen pass for the one typed.
        if (result is Succeeded && typeable.Kind == Gesture.Resize
            && editor.Sketch.Find<Box>(typeable.Id) is { } resized
            && ModelHandles.SizeAcross(resized, typeable.Face!.Value) != typed)
        {
            editor.Say(
                EditSeverity.Problem,
                $"{name} could only be made {ModelHandles.SizeAcross(resized, typeable.Face!.Value).Format(format).Text} there, not {typed.Format(format).Text}: something stops that face.");
        }
    }

    /// <summary>
    /// The editing keystrokes, handled here with the view focused, so that typing an <c>x</c> into a
    /// field types an <c>x</c> — the plan canvas's rule.
    /// </summary>
    bool HandleEditKey(Key key, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta) || _editor is not { } editor)
        {
            return false;
        }

        int turns = modifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1;
        if (_placement.IsArmed && key is Key.X or Key.Y or Key.Z)
        {
            // Holding something to place: the keys turn the preview, not the selection (#92).
            TurnPreview(key switch { Key.X => Axis.X, Key.Y => Axis.Y, _ => Axis.Z }, turns);
            return true;
        }

        switch (key)
        {
            case Key.X:
                SelectionTurn.Turn(editor, Axis.X, turns);
                return true;

            case Key.Y:
                SelectionTurn.Turn(editor, Axis.Y, turns);
                return true;

            case Key.Z:
                SelectionTurn.Turn(editor, Axis.Z, turns);
                return true;

            case Key.V:
                PlanRequested?.Invoke(this, EventArgs.Empty);
                return true;

            case Key.G:
                ToggleGridRequested?.Invoke(this, EventArgs.Empty);
                return true;

            case Key.Escape when editor.Selection.Count > 0 || editor.SelectedJoint is not null:
                editor.ClearSelection();
                return true;

            case Key.Delete or Key.Back when editor.SelectedJoint is not null:
                JointCommandRequested?.Invoke(this, JointCommand.Delete);
                return true;

            case Key.Enter when editor.SelectedJoint is not null:
                JointCommandRequested?.Invoke(this, JointCommand.Edit);
                return true;

            case Key.J:
                JoinRequested?.Invoke(this, modifiers.HasFlag(KeyModifiers.Shift));
                return true;

            case Key.Delete or Key.Back when editor.Selection.Count > 0:
                SelectionCommandRequested?.Invoke(this, SelectionCommand.Delete);
                return true;

            case Key.P when editor.Selection.Count > 0:
                SelectionCommandRequested?.Invoke(this, SelectionCommand.Pin);
                return true;

            case Key.D when editor.Selection.Count > 0:
                SelectionCommandRequested?.Invoke(this, SelectionCommand.Duplicate);
                return true;

            case Key.M when editor.Selection.Count > 0:
                SelectionCommandRequested?.Invoke(this, modifiers.HasFlag(KeyModifiers.Shift) ? SelectionCommand.MirrorNorthSouth : SelectionCommand.MirrorEastWest);
                return true;

            case Key.C:
                SelectionCommandRequested?.Invoke(this, SelectionCommand.Shape);
                return true;

            default:
                return false;
        }
    }

    bool OnSelection(Point position) =>
        _editor is { } editor && PickAt(position) is { } pick && editor.Selection.Contains(pick.Box);

    void Begin(IPointer pointer, Gesture gesture)
    {
        _gesture = gesture;
        pointer.Capture(this);
    }

    void BeginEdit(IPointer pointer, Box box, Gesture gesture, ModelHandle? handle, Axis[] plane, Box? group = null)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        // A drag on one of several selected parts, or on their arrows, moves them all (#87).
        _moving = gesture is Gesture.MoveAxis or Gesture.MovePlane && editor.Selection.Contains(box.Id) && editor.Selection.Count > 1
            ? [.. SelectionCommands.SelectedBoxes(editor).Select(selected => selected.Id)]
            : [box.Id];
        _groupAtPress = _moving.Length > 1 ? group : null;

        _gesture = gesture;
        _gestureEntity = box.Id;
        _boxAtPress = box;
        _gestureHandle = handle;
        _planeAxes = plane;
        _snap = null;
        _gestureRefusal = null;

        // An arrow or a face handle can be given an exact length by typing it (#79); a slide in a
        // face's plane has two axes, and no one number says where it goes.
        _typed.Clear();
        _typeable = handle is { } grabbed && gesture is Gesture.MoveAxis or Gesture.Resize && _moving.Length == 1
            ? new Typeable(gesture, box.Id, box, grabbed.Axis, grabbed.Face, editor.Design, null)
            : null;

        editor.BeginGesture(gesture == Gesture.Resize ? $"Resized {editor.NameOf(box.Id)}" : Moved(editor, box.Id));
        pointer.Capture(this);
    }

    void ContinueEdit(Point position)
    {
        if (_editor is not { } editor
            || _boxAtPress is not { } atPress
            || editor.Sketch.Find<Box>(_gestureEntity) is not { } box)
        {
            return;
        }

        Vector sincePress = position - _pressedAt;
        Length radius = ModelLength(SnapRadiusPixels);

        switch (_gesture)
        {
            case Gesture.MoveAxis when _gestureHandle is { } handle:
            {
                if (InchesAlong(handle.Direction, sincePress, ModelHandles.Centre(atPress), position) is not { } inches)
                {
                    return;
                }

                if (_groupAtPress is { } groupAtPress)
                {
                    // Several parts on their shared arrows: snap their combined extent as one box, to
                    // what is not moving with them, and state nothing — no one part's face caught.
                    Box group = groupAtPress with { Anchor = groupAtPress.Anchor + (box.Anchor - atPress.Anchor) };
                    Point3 wantedGroup = groupAtPress.Anchor + Vector3.Along(handle.Axis, ToLength(inches));
                    SpaceSnapPlan groupPlan = SpaceSnapResolver.Resolve(editor.Sketch, group, wantedGroup, [handle.Axis], SnapStepInches, radius, _moving);
                    MoveBy(editor, groupPlan.Anchor - group.Anchor, groupPlan with { Relationships = [] });
                    break;
                }

                Point3 wanted = atPress.Anchor + Vector3.Along(handle.Axis, ToLength(inches));
                MoveTo(editor, box, SpaceSnapResolver.Resolve(editor.Sketch, box, wanted, [handle.Axis], SnapStepInches, radius, _moving));
                break;
            }

            case Gesture.MovePlane when _planeAxes.Length == 2:
            {
                if (InPlane(_planeAxes[0], _planeAxes[1], sincePress, ModelHandles.Centre(atPress), position) is not { } along)
                {
                    return;
                }

                Point3 wanted = atPress.Anchor
                                + Vector3.Along(_planeAxes[0], ToLength(along.First))
                                + Vector3.Along(_planeAxes[1], ToLength(along.Second));
                MoveTo(editor, box, SpaceSnapResolver.Resolve(editor.Sketch, box, wanted, _planeAxes, SnapStepInches, radius, _moving));
                break;
            }

            case Gesture.Resize when _gestureHandle is { Face: { } face } handle:
            {
                if (InchesAlong(handle.Direction, sincePress, ModelHandles.CentreOf(atPress, face), position) is not { } inches)
                {
                    return;
                }

                // Measured from the part as it was when the handle was grabbed: the face follows the
                // pointer along its own outward normal (§8.3), and lands on another part's face
                // within the snap radius (#80) — a leg's top stretched up meets the table's
                // underside — or its size on the grid, as the plan's resize handles do.
                Length atPressSize = ModelHandles.SizeAcross(atPress, face);
                Length faceAtPress = SpaceSnapResolver.FaceCoordinate(atPress, face);
                Length outward = ToLength(inches);
                Length wantedFace = handle.Positive ? faceAtPress + outward : faceAtPress - outward;
                SpaceSnapPlan facePlan = SpaceSnapResolver.ResolveFace(editor.Sketch, atPress, face, wantedFace, radius);
                Length wantedSize = SnapGrid.Snap(atPressSize + outward, SnapStepInches);
                _snap = null;
                if (facePlan.CaughtSomething)
                {
                    Length caughtAt = facePlan.Hits[0].Coordinate;
                    Length caughtSize = atPressSize + (handle.Positive ? caughtAt - faceAtPress : faceAtPress - caughtAt);
                    if (caughtSize > Length.Zero)
                    {
                        wantedSize = caughtSize;
                        _snap = facePlan;
                    }
                }

                Length delta = wantedSize - ModelHandles.SizeAcross(box, face);
                if (delta != Length.Zero)
                {
                    Remember(editor.ApplyQuietly(new DragFace(_gestureEntity, face, delta)));
                }

                InvalidateVisual();
                break;
            }
        }
    }

    void MoveTo(DesignEditor editor, Box box, SpaceSnapPlan plan) => MoveBy(editor, plan.Anchor - box.Anchor, plan);

    /// <summary>Moves the part being dragged — and the others selected with it (#87) — by a displacement.</summary>
    void MoveBy(DesignEditor editor, Vector3 delta, SpaceSnapPlan plan)
    {
        _snap = plan;
        if (delta != Vector3.Zero)
        {
            Remember(editor.ApplyQuietly(_moving.Length > 1
                ? Batch.Of([.. _moving.Select(id => (Request)new Drag(id, delta))])
                : new Drag(_gestureEntity, delta)));
        }

        InvalidateVisual();
    }

    string Moved(DesignEditor editor, EntityId id) =>
        _moving.Length > 1 ? $"Moved {_moving.Length} parts" : $"Moved {editor.NameOf(id)}";

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
        CompleteEditCore();

        // The gesture is over: what it left is what a typed length would replace (#79). A length
        // typed while the button was still down is applied now.
        if (_typeable is { } typeable && _editor is { } editor)
        {
            _typeable = typeable with { After = editor.Design };
            if (_typed.Length > 0)
            {
                CommitTyped();
            }
        }
    }

    void CompleteEditCore()
    {
        Gesture gesture = _gesture;
        SpaceSnapPlan? plan = _snap;
        Box? atPress = _boxAtPress;
        BoxFace? dragged = _gestureHandle?.Face;
        UpdateResult? refusal = _gestureRefusal;
        _gesture = Gesture.None;
        _snap = null;
        _boxAtPress = null;
        _gestureHandle = null;
        _gestureRefusal = null;

        if (_editor is not { } editor)
        {
            return;
        }

        string what = gesture == Gesture.Resize
            ? $"Resized {editor.NameOf(_gestureEntity)}"
            : Moved(editor, _gestureEntity);

        // A group's arrows snap a box that stands for them all, not the part this gesture names.
        if (_groupAtPress is not null && plan is not null)
        {
            plan = plan with { Anchor = editor.Sketch.Find<Box>(_gestureEntity)?.Anchor ?? plan.Anchor };
        }

        _groupAtPress = null;

        // A snap is stated only when the part got to where it put it: one it never reached — a
        // pinned part dragged at another — is not a relationship, and stating it would conflict.
        Box? now = editor.Sketch.Find<Box>(_gestureEntity);
        bool reached = plan is { CaughtSomething: true } && now is not null
                       && (gesture == Gesture.Resize
                           ? dragged is { } face && plan.Hits[0].Coordinate == SpaceSnapResolver.FaceCoordinate(now, face)
                           : plan.Anchor == now.Anchor);

        if (atPress is not null && now == atPress && !reached)
        {
            // Nothing moved or changed size. Say why: the updater's own words when it refused a
            // step, the pin and the way out when the part was wanted somewhere else, or simply that
            // it is as it was.
            if (refusal is not null)
            {
                editor.Report(refusal, what);
            }
            else if (gesture != Gesture.Resize && plan is not null && plan.Anchor != now.Anchor)
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
        if (plan is not null && reached)
        {
            foreach (Relationship candidate in plan.Relationships)
            {
                if (editor.CanHold(candidate) && !editor.AlreadyStates(candidate))
                {
                    statements.Add(new AddRelationship(candidate));
                }
            }
        }

        if (statements.Count > 0)
        {
            // The drop states what the snap caught: relationships are stored, never inferred.
            editor.Apply(statements.Count == 1 ? statements[0] : Batch.Of([.. statements]), what + " and snapped it");
        }
        else
        {
            editor.Say(EditSeverity.Done, what + ".");
        }

        editor.EndGesture();
        InvalidateVisual();
    }

    /// <summary>
    /// How many inches along a world direction a pointer displacement means: its projection onto
    /// that direction's image on the screen. Null when the direction points too nearly at the eye to
    /// be dragged along.
    /// </summary>
    double? InchesAlong(Vector3d direction, Vector sincePress, Vector3d through, Point position)
    {
        if (_camera.IsPerspective)
        {
            // An inch is not the same number of pixels everywhere: the drag is where the eye ray
            // through the pointer comes closest to the axis line, less where it was at the press.
            return ModelPicker.ParameterAlongLine(_camera, position, through, direction) is { } now
                   && ModelPicker.ParameterAlongLine(_camera, _pressedAt, through, direction) is { } then
                ? now - then
                : null;
        }

        Vector along = _camera.ProjectDirection(direction);
        double squared = (along.X * along.X) + (along.Y * along.Y);
        if (Math.Sqrt(squared) < ModelHandles.ShortestUsableFraction * _camera.PixelsPerInch)
        {
            return null;
        }

        return ((sincePress.X * along.X) + (sincePress.Y * along.Y)) / squared;
    }

    /// <summary>
    /// How far along two world axes a pointer displacement means, in the plane they span: the one
    /// pair of amounts whose images add up to it. Null when the plane is seen too nearly edge-on.
    /// </summary>
    (double First, double Second)? InPlane(Axis first, Axis second, Vector sincePress, Vector3d through, Point position)
    {
        if (_camera.IsPerspective)
        {
            // Where the eye ray through the pointer meets the plane the two axes span, now and at the press.
            Axis third = Enum.GetValues<Axis>().Single(axis => axis != first && axis != second);
            double coordinate = through.Component(third);
            if (Math.Abs(_camera.Ray(position).Direction.Component(third)) < ModelHandles.ShortestUsableFraction
                || ModelPicker.PlaneAt(_camera, position, third, coordinate) is not { } now
                || ModelPicker.PlaneAt(_camera, _pressedAt, third, coordinate) is not { } then)
            {
                return null;
            }

            Vector3d moved = now - then;
            return (moved.Component(first), moved.Component(second));
        }

        Vector a = _camera.ProjectDirection(Vector3d.Along(first));
        Vector b = _camera.ProjectDirection(Vector3d.Along(second));
        double determinant = (a.X * b.Y) - (a.Y * b.X);
        if (Math.Abs(determinant) < 0.1 * a.Length * b.Length)
        {
            return null;
        }

        return (
            ((sincePress.X * b.Y) - (sincePress.Y * b.X)) / determinant,
            ((a.X * sincePress.Y) - (a.Y * sincePress.X)) / determinant);
    }

    /// <summary>
    /// The face under a point of the view a part could be placed on, and the point on it — on the
    /// grid across the face, exactly on it along its normal — or the floor, where no part is under
    /// the pointer; null when the pointer is on neither. A face a cut made comes back with no face.
    /// </summary>
    (PlacementFace? Face, Point3 Point)? FaceUnder(Point position, DesignEditor editor)
    {
        if (ModelPicker.SurfaceAt(editor.Sketch, Scene, _camera, position) is { } surface)
        {
            if (surface.Face is not { } hitFace || editor.Sketch.Find<Box>(surface.Box) is not { Orientation.IsExact: true } box)
            {
                return (null, default);
            }

            PlacementFace face = PlacementFace.Of(box, hitFace);
            return (face, OnGrid(face, surface.Point));
        }

        return ModelPicker.FloorAt(_camera, position) is { } floor
            ? (PlacementFace.Floor, OnGrid(PlacementFace.Floor, floor))
            : null;
    }

    /// <summary>Where the pointer is on a face's plane, on the grid across it.</summary>
    Point3? OnFace(Point position, PlacementFace face) =>
        ModelPicker.PlaneAt(_camera, position, face.Normal, face.Coordinate.ToInches()) is { } at ? OnGrid(face, at) : null;

    Point3 OnGrid(PlacementFace face, Vector3d at)
    {
        (Axis u, Axis v) = face.Plane;
        return Point3.Origin
            .WithComponent(u, SnapGrid.Snap(ToLength(at.Component(u)), SnapStepInches))
            .WithComponent(v, SnapGrid.Snap(ToLength(at.Component(v)), SnapStepInches))
            .WithComponent(face.Normal, face.Coordinate);
    }

    PlacementPreview? Shaped(DesignEditor editor, Point3 to) =>
        _placeFace is { } face ? ShapedOn(editor, face, _placeFrom, to) : null;

    PlacementPreview? ShapedOn(DesignEditor editor, PlacementFace face, Point3 from, Point3 to) =>
        _placement.Shape(editor.Sketch, face, from, to, editor.LayerForNewParts(), _previewId, string.Empty, SnapStepInches, ModelLength(SnapRadiusPixels));

    /// <summary>
    /// Turns what is held a quarter turn about a world axis before it is placed (#92), and shows it
    /// turned where the pointer is.
    /// </summary>
    public void TurnPreview(Axis axis, int quarterTurns)
    {
        if (!_placement.IsArmed || _editor is not { } editor)
        {
            return;
        }

        (PlacementFace? Face, Point3 Point)? under = FaceUnder(_lastPointer, editor);
        _placement.Turn(axis, quarterTurns, under?.Face);
        _preview = under is { Face: { } face } over ? ShapedOn(editor, face, over.Point, over.Point) : null;
        editor.Say(
            EditSeverity.Hint,
            $"Turned {_placement.Holding} a quarter turn about {axis}{(quarterTurns < 0 ? ", the other way" : string.Empty)}, to place.");
        InvalidateVisual();
    }

    /// <summary>
    /// Puts the part down (#74): added with its stock, then held by the flush against the face it
    /// rests on and whatever its edges caught — one undo step — and selected. What is held stays
    /// held, for the next one.
    /// </summary>
    void Place(PlacementPreview preview)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        Box named = preview.Box with { Name = editor.NextPartName() };
        preview = preview with { Box = named };
        _previewId = EntityId.New();

        string where = preview.Face is { Target: { } target, TargetFace: { } targetFace }
            ? $"{editor.NameOf(target)}'s {WorldWords.Feature(editor.Sketch.Find<Box>(target), BoxFeature.Face(targetFace))}"
            : "the floor";
        string what = $"Placed {_placement.Holding} on {where}";
        (Request add, System.Collections.Immutable.ImmutableList<Relationship> holds) = PlacementTool.Requests(editor.Sketch, preview);

        editor.BeginGesture(what);
        if (editor.Apply(add, what) is Succeeded)
        {
            int held = 0;
            foreach (Relationship hold in holds)
            {
                if (editor.CanHold(hold) && !editor.AlreadyStates(hold)
                    && editor.Apply(new AddRelationship(hold), what) is Succeeded)
                {
                    held++;
                }
            }

            editor.Select(named.Id);
            editor.Say(EditSeverity.Done, $"{what} as {editor.NameOf(named.Id)}{(held > 0 ? ", held there" : string.Empty)}.");
        }

        editor.EndGesture();
        _preview = null;
        InvalidateVisual();
    }

    void PickOn(Point position, KeyModifiers modifiers)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        if (PickAt(position) is not { } pick)
        {
            editor.ClearSelection();
            return;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            editor.ToggleSelected(pick.Box);
        }
        else
        {
            editor.Select(pick.Box);
            editor.Say(EditSeverity.Done, $"{editor.NameOf(pick.Box)} selected, its {WorldWords.Feature(editor.Sketch.Find<Box>(pick.Box), pick.Feature)}.");
        }
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

    Length ModelLength(double pixels) =>
        Length.FromInches(pixels / Math.Max(_camera.PixelsPerInch, 1e-9), Rounding.HalfAwayFromZero);

    static Length ToLength(double inches) => Length.FromInches(inches, Rounding.HalfAwayFromZero);

    void OnEditorChanged(object? sender, EventArgs e) => InvalidateVisual();

    void OnEditorDesignOpened(object? sender, EventArgs e)
    {
        // A new design is seen from the default direction, framed, the next time there is room to.
        _camera = Camera.Isometric(_camera.Viewport) with { Projection = _projection };
        _fitPending = true;
        if (IsVisible)
        {
            ZoomToFit();
        }

        Hover(null);
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible && _fitPending)
        {
            ZoomToFit();
        }
        else if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            InvalidateVisual();
        }
    }

    // -------------------------------------------------------------------------------------
    // Drawing
    // -------------------------------------------------------------------------------------

    SketchLook _look;

    /// <summary>The sketch look (#142): paper and pencil. Drawing only; nothing it changes is stored or picked.</summary>
    public SketchLook Look
    {
        get => _look;
        set
        {
            if (_look != value)
            {
                _look = value;
                InvalidateVisual();
            }
        }
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant, _look);
        context.FillRectangle(new SolidColorBrush(palette.Background), new Rect(Bounds.Size));
        SketchInk.Paper(context, _look.Paper, new Rect(Bounds.Size));

        if (_editor is not { } editor)
        {
            return;
        }

        Sketch sketch = editor.Sketch;
        if (_showGrid)
        {
            DrawFloor(context, palette, sketch);
        }

        Dictionary<LayerId, string> layerNames = sketch.Layers.ToDictionary(layer => layer.Id, layer => layer.Name);
        foreach (ScenePolygon polygon in Scene.BackToFront(_camera))
        {
            string layer = sketch.Find(polygon.Box) is { } entity ? Napkin.App.Designs.DesignLayers.StyleName(sketch, entity, layerNames) : string.Empty;
            DrawPolygon(context, palette, palette.StyleFor(layer), polygon);
        }

        DrawAttention(context, palette);
        _joints.Update(sketch, _camera.Project, editor.SelectedJoint);
        JointMarkers.Draw(context, _joints.Placed, palette.Dimension, palette.Background, palette.Selection, editor.SelectedJoint);
        DrawSelection(context, palette, editor);
        DrawVirtualFeatures(context, sketch, sketch.RelationshipsInOrder, palette.Dimension);
        if (_snap is { } plan)
        {
            DrawVirtualFeatures(context, sketch, plan.Relationships, palette.Snap);
            DrawSnapIndicator(context, palette, sketch, plan);
        }

        DrawPreview(context, palette);
        DrawHandles(context, palette);
        DrawReadout(context, palette);
        DrawAxes(context);
        DrawScaleBar(context, palette);
    }

    /// <summary>The part as it would be placed, faint, the faces the eye could see (#74).</summary>
    void DrawPreview(DrawingContext context, CanvasPalette palette)
    {
        if (PlacementPreview is not { Box: { } box })
        {
            return;
        }

        SolidColorBrush fill = new(palette.Selection, 0.22);
        Pen edge = new(new SolidColorBrush(palette.Selection, 0.9), 1.2) { LineJoin = PenLineJoin.Round };
        foreach (BoxFace face in (BoxFace[])[BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West, BoxFace.Bottom, BoxFace.Top])
        {
            (Axis axis, bool positive) = box.Orientation.Normal(face);
            Vector3d faceCentre = ModelHandles.CentreOf(box, face);
            if (Vector3d.Dot(Vector3d.Along(axis, positive), _camera.TowardViewerAt(faceCentre)) <= 1e-6
                || !ModelHandles.CornersOf(box, face).All(corner => _camera.IsInFront(Vector3d.From(corner))))
            {
                continue;
            }

            StreamGeometry outline = new();
            using (StreamGeometryContext figure = outline.Open())
            {
                Point3[] corners = [.. ModelHandles.CornersOf(box, face)];
                figure.BeginFigure(_camera.Project(corners[0]), isFilled: true);
                foreach (Point3 corner in corners.Skip(1))
                {
                    figure.LineTo(_camera.Project(corner));
                }

                figure.EndFigure(isClosed: true);
            }

            context.DrawGeometry(fill, edge, outline);
        }
    }

    /// <summary>The live readout, on a small plate just below and right of the pointer (#86).</summary>
    void DrawReadout(DrawingContext context, CanvasPalette palette)
    {
        if (LiveReadout is not { } readout)
        {
            return;
        }

        FormattedText text = new(readout, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, new SolidColorBrush(palette.Label));
        Point at = _lastPointer + new Vector(16, 14);
        at = new Point(Math.Min(at.X, Math.Max(Bounds.Width - FitReserveRight - text.Width - 12, 4)), Math.Min(at.Y, Bounds.Height - text.Height - 8));
        Rect plate = new(at.X - 5, at.Y - 3, text.Width + 10, text.Height + 6);
        context.DrawRectangle(new SolidColorBrush(palette.Background, 0.92), new Pen(new SolidColorBrush(palette.GridMajor), 1), plate, 3, 3);
        context.DrawText(text, at);
    }

    void DrawPolygon(DrawingContext context, CanvasPalette palette, EntityStyle style, ScenePolygon polygon)
    {
        StreamGeometry face = new();
        using (StreamGeometryContext figure = face.Open())
        {
            figure.BeginFigure(_camera.Project(polygon.Points[0]), isFilled: true);
            for (int i = 1; i < polygon.Points.Length; i++)
            {
                figure.LineTo(_camera.Project(polygon.Points[i]));
            }

            figure.EndFigure(isClosed: true);
        }

        context.DrawGeometry(new SolidColorBrush(Tone(palette, style, polygon.Normal)), null, face);

        Pen pen = new(new SolidColorBrush(style.Stroke), Math.Min(style.StrokeThickness, 1.2))
        {
            LineJoin = PenLineJoin.Round,
            DashStyle = style.Dashed ? new DashStyle([4, 3], 0) : null,
        };
        DrawEdges(context, pen, polygon, palette.Look.Line, style);
    }

    void DrawEdges(DrawingContext context, Pen pen, ScenePolygon polygon, SketchLine line = SketchLine.Clean, EntityStyle? style = null)
    {
        int count = polygon.Points.Length;
        for (int i = 0; i < count; i++)
        {
            if (polygon.EdgeDrawn[i] && line != SketchLine.Clean && style is not null)
            {
                Vector3d p = polygon.Points[i], q = polygon.Points[(i + 1) % count];
                SketchInk.Stroke(
                    context,
                    line,
                    style.Stroke,
                    style.Dashed,
                    _camera.Project(p),
                    _camera.Project(q),
                    SketchStroke.SeedOf(p.X + (p.Z * 1.7), p.Y + (p.Z * 0.9), q.X + (q.Z * 1.7), q.Y + (q.Z * 0.9)));
            }
            else if (polygon.EdgeDrawn[i])
            {
                context.DrawLine(pen, _camera.Project(polygon.Points[i]), _camera.Project(polygon.Points[(i + 1) % count]));
            }
        }
    }

    /// <summary>
    /// The three tones of the classic axonometric drawing: the part's own fill, made opaque, light on
    /// a face turned up, a step darker facing along X, darker again along Y. A cut face — a mitre, a
    /// curve — takes the tone of the axis its normal lies most nearly along.
    /// </summary>
    static Color Tone(CanvasPalette palette, EntityStyle style, Vector3d normal)
    {
        double alpha = Math.Max(style.Fill.A / 255.0, 0.45);
        Color background = palette.Background;
        double r = (style.Fill.R * alpha) + (background.R * (1 - alpha));
        double g = (style.Fill.G * alpha) + (background.G * (1 - alpha));
        double b = (style.Fill.B * alpha) + (background.B * (1 - alpha));

        // Up-facing is lifted towards white, the X-facing sides keep the part's own colour, and the
        // Y-facing sides are taken down: light, medium, dark.
        (double lift, double shade) = normal.DominantAxis().Axis switch
        {
            Axis.Z => (0.4, 1.0),
            Axis.X => (0.0, 0.94),
            _ => (0.0, 0.76),
        };

        return Color.FromRgb(
            Channel(((r + ((255 - r) * lift)) * shade)),
            Channel(((g + ((255 - g) * lift)) * shade)),
            Channel(((b + ((255 - b) * lift)) * shade)));

        static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
    }

    /// <summary>
    /// The edges the eye can see of each part in <see cref="Attention"/>, wide and in the problem
    /// colour, under the selection's — on top of what stands in front, as the selection is.
    /// </summary>
    void DrawAttention(DrawingContext context, CanvasPalette palette)
    {
        if (_attention.IsEmpty)
        {
            return;
        }

        Pen pen = new(new SolidColorBrush(palette.Snap, 0.85), 4) { LineJoin = PenLineJoin.Round };
        foreach (ScenePolygon whole in Scene.Polygons)
        {
            if (_attention.Contains(whole.Box) && whole.ClippedFor(_camera) is { } polygon && polygon.FacesTowards(_camera))
            {
                DrawEdges(context, pen, polygon);
            }
        }
    }

    void DrawSelection(DrawingContext context, CanvasPalette palette, DesignEditor editor)
    {
        if (editor.Selection.Count == 0)
        {
            return;
        }

        // On top, so the selected part reads through whatever stands in front of it — but only its
        // edges the eye could see were nothing in the way, so it is not a wire frame.
        Pen pen = new(new SolidColorBrush(palette.Selection), 2.2) { LineJoin = PenLineJoin.Round };
        foreach (ScenePolygon whole in Scene.Polygons)
        {
            if (editor.Selection.Contains(whole.Box) && whole.ClippedFor(_camera) is { } polygon && polygon.FacesTowards(_camera))
            {
                DrawEdges(context, pen, polygon);
            }
        }
    }

    void DrawHandles(DrawingContext context, CanvasPalette palette)
    {
        if (IsEditing && _gestureHandle is null)
        {
            // A body drag hides the handles, which would only be in the way of what is caught.
            return;
        }

        SolidColorBrush paper = new(palette.Background);
        foreach (ModelHandle handle in Handles)
        {
            if (handle.Kind == ModelHandleKind.Face)
            {
                context.DrawLine(new Pen(new SolidColorBrush(palette.Selection, 0.6), 1), handle.Base, handle.At);
                context.DrawRectangle(
                    paper,
                    new Pen(new SolidColorBrush(palette.Selection), 1.4),
                    new Rect(handle.At.X - HandleHalfSize, handle.At.Y - HandleHalfSize, HandleHalfSize * 2, HandleHalfSize * 2));
                continue;
            }

            Color ink = AxisColour(handle.Axis);
            SolidColorBrush brush = new(ink);
            context.DrawLine(new Pen(brush, 2), handle.Base, handle.At);

            Vector along = handle.At - handle.Base;
            double length = along.Length;
            if (length > 1e-6)
            {
                Vector unit = along / length;
                Vector side = new(-unit.Y, unit.X);
                StreamGeometry head = new();
                using (StreamGeometryContext figure = head.Open())
                {
                    figure.BeginFigure(handle.At + (unit * 4), isFilled: true);
                    figure.LineTo(handle.At - (unit * 7) + (side * 4.5));
                    figure.LineTo(handle.At - (unit * 7) - (side * 4.5));
                    figure.EndFigure(isClosed: true);
                }

                context.DrawGeometry(brush, null, head);
            }
        }
    }

    /// <summary>
    /// The snap indicator: the outline of the face that was caught, faint and dashed, the way the
    /// plan canvas draws the edge line it caught (&#xA7;8.3).
    /// </summary>
    void DrawSnapIndicator(DrawingContext context, CanvasPalette palette, Sketch sketch, SpaceSnapPlan plan)
    {
        Pen pen = new(new SolidColorBrush(palette.Snap), 1.4) { DashStyle = new DashStyle([5, 4], 0) };
        SolidColorBrush diamond = new(palette.Snap);

        foreach (SpaceSnapHit hit in plan.Hits)
        {
            if (hit.Kind == SnapKind.Grid || hit.Target is not { } target || hit.TargetFace is not { } face
                || sketch.Find<Box>(target) is not { } box)
            {
                continue;
            }

            if (!ModelHandles.CornersOf(box, face).All(corner => _camera.IsInFront(Vector3d.From(corner))))
            {
                continue;
            }

            Point[] corners = [.. ModelHandles.CornersOf(box, face).Select(_camera.Project)];
            for (int i = 0; i < corners.Length; i++)
            {
                context.DrawLine(pen, corners[i], corners[(i + 1) % corners.Length]);
            }

            Point middle = _camera.Project(ModelHandles.CentreOf(box, face));
            StreamGeometry mark = new();
            using (StreamGeometryContext figure = mark.Open())
            {
                figure.BeginFigure(new Point(middle.X, middle.Y - 4.5), isFilled: true);
                figure.LineTo(new Point(middle.X + 4.5, middle.Y));
                figure.LineTo(new Point(middle.X, middle.Y + 4.5));
                figure.LineTo(new Point(middle.X - 4.5, middle.Y));
                figure.EndFigure(isClosed: true);
            }

            context.DrawGeometry(diamond, null, mark);
        }
    }

    /// <summary>
    /// Marks the blank where a relationship holds on to a vertex or an edge a cut took away, the way
    /// the plan canvas marks a virtual corner (<c>docs/design/assembly-model.md</c> &#xA7;2.5,
    /// shaped-parts &#xA7;2.1): a small cross at each virtual vertex, and the virtual upright between
    /// them as a faint dashed line.
    /// </summary>
    void DrawVirtualFeatures(DrawingContext context, Sketch sketch, IEnumerable<Relationship> relationships, Color ink)
    {
        Pen faint = new(new SolidColorBrush(ink, 0.5), 1) { DashStyle = new DashStyle([3, 3], 0) };
        Pen cross = new(new SolidColorBrush(ink, 0.9), 1.2);

        foreach ((EntityId id, BoxCorner corner, BoxLevel? level) in VirtualSites(sketch, relationships))
        {
            Box box = sketch.Find<Box>(id)!;
            if (level is { } only)
            {
                Cross(box.Vertex(corner, only));
                continue;
            }

            Point bottom = _camera.Project(box.Vertex(corner, BoxLevel.Bottom));
            Point top = _camera.Project(box.Vertex(corner, BoxLevel.Top));
            context.DrawLine(faint, bottom, top);
            Cross(box.Vertex(corner, BoxLevel.Bottom));
            Cross(box.Vertex(corner, BoxLevel.Top));
        }

        void Cross(Point3 point)
        {
            Point at = _camera.Project(point);
            context.DrawLine(cross, new Point(at.X - VirtualCornerArm, at.Y - VirtualCornerArm), new Point(at.X + VirtualCornerArm, at.Y + VirtualCornerArm));
            context.DrawLine(cross, new Point(at.X - VirtualCornerArm, at.Y + VirtualCornerArm), new Point(at.X + VirtualCornerArm, at.Y - VirtualCornerArm));
        }
    }

    /// <summary>
    /// The virtual places relationships refer to: a corner of a blank a cut has taken off, as the
    /// upright there (level null) or one of its two vertices.
    /// </summary>
    public static IEnumerable<(EntityId Box, BoxCorner Corner, BoxLevel? Level)> VirtualSites(
        Sketch sketch,
        IEnumerable<Relationship> relationships)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(relationships);

        HashSet<(EntityId, BoxCorner, BoxLevel?)> seen = [];
        foreach (Relationship relationship in relationships)
        {
            foreach (FeatureRef feature in RelationshipSites.FeaturesOf(relationship))
            {
                if (sketch.Find<Box>(feature.Box) is not { Cuts.IsEmpty: false } box
                    || CornerOf(feature.Feature) is not { } corner
                    || !BlankShape.IsVirtualCorner(box, corner))
                {
                    continue;
                }

                System.Collections.Immutable.ImmutableArray<BoxFace> faces = feature.Feature.Faces;
                BoxLevel? level = faces.Contains(BoxFace.Bottom) ? BoxLevel.Bottom : faces.Contains(BoxFace.Top) ? BoxLevel.Top : null;
                if (seen.Add((box.Id, corner, level)))
                {
                    yield return (box.Id, corner, level);
                }
            }
        }
    }

    /// <summary>The corner of the blank a feature stands at — both side faces that meet there — or null.</summary>
    static BoxCorner? CornerOf(BoxFeature feature)
    {
        System.Collections.Immutable.ImmutableArray<BoxFace> faces = feature.Faces;
        bool south = faces.Contains(BoxFace.South), north = faces.Contains(BoxFace.North);
        bool east = faces.Contains(BoxFace.East), west = faces.Contains(BoxFace.West);
        return (south, north, east, west) switch
        {
            (true, _, _, true) => BoxCorner.SouthWest,
            (true, _, true, _) => BoxCorner.SouthEast,
            (_, true, true, _) => BoxCorner.NorthEast,
            (_, true, _, true) => BoxCorner.NorthWest,
            _ => null,
        };
    }

    /// <summary>
    /// A floor grid at the plan datum, Z = 0, under the drawing: the plan's coarser grid step, so the
    /// eye has something level to read the parts against.
    /// </summary>
    void DrawFloor(DrawingContext context, CanvasPalette palette, Sketch sketch)
    {
        Bounds3 bounds = Bounds3.Of(sketch);
        double minX = -24, maxX = 24, minY = -24, maxY = 24;
        if (!bounds.IsEmpty)
        {
            minX = bounds.Min.X.ToInches() - 12;
            maxX = bounds.Max.X.ToInches() + 12;
            minY = bounds.Min.Y.ToInches() - 12;
            maxY = bounds.Max.Y.ToInches() + 12;
        }

        double minor = SnapGrid.StepInches(_camera.PixelsPerInch);
        double step = SnapGrid.CoarserStepInches(minor, _camera.PixelsPerInch);
        if (step <= 0)
        {
            step = minor;
        }

        // A guard, not a policy: the ladder keeps lines apart on the screen, so this only bites for
        // a drawing far larger than the view.
        while ((maxX - minX) / step > 120 || (maxY - minY) / step > 120)
        {
            step *= 2;
        }

        Pen pen = new(new SolidColorBrush(palette.GridMajor), 1);
        double firstX = Math.Floor(minX / step) * step, lastX = Math.Ceiling(maxX / step) * step;
        double firstY = Math.Floor(minY / step) * step, lastY = Math.Ceiling(maxY / step) * step;
        for (double x = firstX; x <= lastX + 1e-9; x += step)
        {
            DrawGroundLine(context, pen, new Vector3d(x, firstY, 0), new Vector3d(x, lastY, 0));
        }

        for (double y = firstY; y <= lastY + 1e-9; y += step)
        {
            DrawGroundLine(context, pen, new Vector3d(firstX, y, 0), new Vector3d(lastX, y, 0));
        }
    }

    /// <summary>A line on the ground, cut off where it would come nearer than the near plane.</summary>
    void DrawGroundLine(DrawingContext context, Pen pen, Vector3d from, Vector3d to)
    {
        if (_camera.TryClipToNearPlane(ref from, ref to))
        {
            context.DrawLine(pen, _camera.Project(from), _camera.Project(to));
        }
    }

    bool _showScaleBar;

    /// <summary>
    /// Whether an orthographic view shows a scale bar: a length on the grid ladder, drawn as wide as it
    /// is at this zoom. A perspective view never does, because an inch is a different size at every
    /// distance in it; its rulers are the plan's (<c>docs/design/assembly-model.md</c> §11 decision 10).
    /// </summary>
    public bool ShowScaleBar
    {
        get => _showScaleBar;
        set
        {
            if (_showScaleBar == value)
            {
                return;
            }

            _showScaleBar = value;
            InvalidateVisual();
        }
    }

    /// <summary>What the scale bar reads now, or <see langword="null"/> when none is drawn.</summary>
    public string? ScaleBarLabel => _showScaleBar && !_camera.IsPerspective && _camera.HasViewport
        ? ScaleBar.LabelFor(_camera.PixelsPerInch)
        : null;

    void DrawScaleBar(DrawingContext context, CanvasPalette palette)
    {
        if (ScaleBarLabel is not { } label)
        {
            return;
        }

        (_, double pixels, _) = ScaleBar.Choose(_camera.PixelsPerInch);
        Point left = new(84, Bounds.Height - 30);
        Point right = new(left.X + pixels, left.Y);
        Pen pen = new(new SolidColorBrush(palette.Label), 1.4);
        context.DrawLine(pen, left, right);
        context.DrawLine(pen, new Point(left.X, left.Y - 4), new Point(left.X, left.Y + 4));
        context.DrawLine(pen, new Point(right.X, right.Y - 4), new Point(right.X, right.Y + 4));

        FormattedText text = new(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            10,
            new SolidColorBrush(palette.Label));
        context.DrawText(text, new Point(left.X + ((pixels - text.Width) / 2), left.Y - 8 - text.Height));
    }

    /// <summary>Which way X, Y and Z point, in the corner, so a turn about an axis names something visible.</summary>
    void DrawAxes(DrawingContext context)
    {
        Point origin = new(34, Bounds.Height - 34);
        foreach (Axis axis in (Axis[])[Axis.X, Axis.Y, Axis.Z])
        {
            Vector along = _camera.ProjectDirection(Vector3d.Along(axis));
            double length = along.Length;
            if (length < 1e-9)
            {
                continue;
            }

            Point tip = origin + (along / length * 22 * Math.Min(1, length / _camera.PixelsPerInch * 1.25));
            SolidColorBrush brush = new(AxisColour(axis));
            context.DrawLine(new Pen(brush, 1.6), origin, tip);
            // The axis's letter, which the turn keys use, and which way it runs in the words the
            // relationship list and the Part panel use (#81).
            FormattedText text = new(
                axis switch { Axis.X => "X east", Axis.Y => "Y north", _ => "Z up" },
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                10,
                brush);
            context.DrawText(text, tip + new Vector(2, -text.Height / 2));
        }
    }

    static Color AxisColour(Axis axis) => axis switch
    {
        Axis.X => AxisX,
        Axis.Y => AxisY,
        _ => AxisZ,
    };

    /// <summary>What a typed length would apply to: the arrow or face-handle drag just made (#79).</summary>
    sealed record Typeable(Gesture Kind, EntityId Id, Box AtPress, Axis Axis, BoxFace? Face, Napkin.App.Designs.Design Before, Napkin.App.Designs.Design? After);

    enum Gesture
    {
        None,
        Pending,
        Placing,
        Orbit,
        Pan,
        MoveAxis,
        MovePlane,
        Resize,
    }
}

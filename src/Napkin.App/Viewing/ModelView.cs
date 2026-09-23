using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Napkin.App.Editing;
using Napkin.Core.Geometry;

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
/// <strong>Controls.</strong> Drag on empty space, or on a part that is not selected, to orbit;
/// click a part to select it (Shift adds or removes); Shift-drag or the middle button pans; the
/// wheel zooms about the pointer. On the selected part: drag one of the three arrows to move it
/// along that world axis, drag its body to slide it in the plane of the face you pressed on, drag
/// the square on a face to resize it across that face. <c>X</c>, <c>Y</c> and <c>Z</c> turn it a
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

    Camera _camera = Camera.Isometric();
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
    EntityId? _hovered;
    System.Collections.Immutable.ImmutableHashSet<EntityId> _attention = [];

    static ModelView() => FocusableProperty.OverrideDefaultValue<ModelView>(true);

    /// <summary>Raised whenever the camera changes, so a status bar can follow it.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>Raised when the part under the pointer changes (#62's relationship list).</summary>
    public event EventHandler? HoveredPartChanged;

    /// <summary>Raised as the pointer moves, with the point on a part under it, or null.</summary>
    public event EventHandler<Vector3d?>? PointerModelPositionChanged;

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

    /// <summary>The grid step a move or a resize lands on at this zoom, in inches: the plan's ladder.</summary>
    public double GridStepInches => SnapGrid.StepInches(_camera.PixelsPerInch);

    /// <summary>What the drag in progress has caught, or null when nothing is being dragged.</summary>
    public SpaceSnapPlan? ActiveSnap => _snap;

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

    /// <summary>The selected part's handles as they are drawn now: empty unless exactly one part is selected.</summary>
    public IReadOnlyList<ModelHandle> Handles =>
        _editor?.OnlySelectedBox is { } box ? ModelHandles.Of(box, _camera) : [];

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
        Camera = Camera.Isometric(_camera.Viewport);
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

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        Point position = e.GetPosition(this);
        _pressedAt = position;
        _lastPointer = position;
        Focus();

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

        if (PickAt(position) is { } pick
            && editor.Selection.Contains(pick.Box)
            && editor.Sketch.Find<Box>(pick.Box) is { } body)
        {
            // A drag on the body moves in the plane of the face that was pressed — two axes, never
            // three: the third would be a guess at depth the pointer cannot make (§8.3).
            Axis[] plane = [.. ((Axis[])[Axis.X, Axis.Y, Axis.Z]).Where(axis => axis != pick.NormalAxis)];
            BeginEdit(e.Pointer, body, Gesture.MovePlane, null, plane);
            return;
        }

        Begin(e.Pointer, Gesture.Orbit);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);
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

            case Gesture.MoveAxis or Gesture.MovePlane or Gesture.Resize:
                ContinueEdit(position);
                break;

            default:
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

        _gesture = Gesture.None;
        e.Pointer.Capture(null);

        // A press that did not move the view is a click, and a click picks — measured from where the
        // button went down, as the plan canvas measures it.
        bool moved = Math.Abs(position.X - _pressedAt.X) > 2 || Math.Abs(position.Y - _pressedAt.Y) > 2;
        if (gesture is Gesture.Orbit or Gesture.Pan && !moved && e.InitialPressMouseButton == MouseButton.Left)
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
        if (e.Handled || HandleEditKey(e.Key, e.KeyModifiers) || HandleViewKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
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

            case Key.Escape when editor.Selection.Count > 0:
                editor.ClearSelection();
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

    void BeginEdit(IPointer pointer, Box box, Gesture gesture, ModelHandle? handle, Axis[] plane)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        _gesture = gesture;
        _gestureEntity = box.Id;
        _boxAtPress = box;
        _gestureHandle = handle;
        _planeAxes = plane;
        _snap = null;

        editor.BeginGesture(gesture == Gesture.Resize ? $"Resized {editor.NameOf(box.Id)}" : $"Moved {editor.NameOf(box.Id)}");
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
                if (InchesAlong(handle.Direction, sincePress) is not { } inches)
                {
                    return;
                }

                Point3 wanted = atPress.Anchor + Vector3.Along(handle.Axis, ToLength(inches));
                MoveTo(editor, box, SpaceSnapResolver.Resolve(editor.Sketch, box, wanted, [handle.Axis], GridStepInches, radius));
                break;
            }

            case Gesture.MovePlane when _planeAxes.Length == 2:
            {
                if (InPlane(_planeAxes[0], _planeAxes[1], sincePress) is not { } along)
                {
                    return;
                }

                Point3 wanted = atPress.Anchor
                                + Vector3.Along(_planeAxes[0], ToLength(along.First))
                                + Vector3.Along(_planeAxes[1], ToLength(along.Second));
                MoveTo(editor, box, SpaceSnapResolver.Resolve(editor.Sketch, box, wanted, _planeAxes, GridStepInches, radius));
                break;
            }

            case Gesture.Resize when _gestureHandle is { Face: { } face } handle:
            {
                if (InchesAlong(handle.Direction, sincePress) is not { } inches)
                {
                    return;
                }

                // Measured from the part as it was when the handle was grabbed, and landed on the
                // grid, as the plan's resize handles are: the face follows the pointer along its own
                // outward normal (§8.3).
                Length atPressSize = ModelHandles.SizeAcross(atPress, face);
                Length wantedSize = SnapGrid.Snap(atPressSize + ToLength(inches), GridStepInches);
                Length delta = wantedSize - ModelHandles.SizeAcross(box, face);
                if (delta != Length.Zero)
                {
                    editor.ApplyQuietly(new DragFace(_gestureEntity, face, delta));
                }

                InvalidateVisual();
                break;
            }
        }
    }

    void MoveTo(DesignEditor editor, Box box, SpaceSnapPlan plan)
    {
        _snap = plan;
        Vector3 delta = plan.Anchor - box.Anchor;
        if (delta != Vector3.Zero)
        {
            editor.ApplyQuietly(new Drag(_gestureEntity, delta));
        }

        InvalidateVisual();
    }

    void CompleteEdit()
    {
        Gesture gesture = _gesture;
        SpaceSnapPlan? plan = _snap;
        _gesture = Gesture.None;
        _snap = null;
        _boxAtPress = null;
        _gestureHandle = null;

        if (_editor is not { } editor)
        {
            return;
        }

        string what = gesture == Gesture.Resize
            ? $"Resized {editor.NameOf(_gestureEntity)}"
            : $"Moved {editor.NameOf(_gestureEntity)}";

        List<Request> statements = [];
        if (gesture != Gesture.Resize && plan is not null)
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
    double? InchesAlong(Vector3d direction, Vector sincePress)
    {
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
    (double First, double Second)? InPlane(Axis first, Axis second, Vector sincePress)
    {
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
        _camera = Camera.Isometric(_camera.Viewport);
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
    }

    // -------------------------------------------------------------------------------------
    // Drawing
    // -------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        context.FillRectangle(new SolidColorBrush(palette.Background), new Rect(Bounds.Size));

        if (_editor is not { } editor)
        {
            return;
        }

        Sketch sketch = editor.Sketch;
        DrawFloor(context, palette, sketch);

        Dictionary<LayerId, string> layerNames = sketch.Layers.ToDictionary(layer => layer.Id, layer => layer.Name);
        foreach (ScenePolygon polygon in Scene.BackToFront(_camera))
        {
            string layer = sketch.Find(polygon.Box) is { } entity && layerNames.TryGetValue(entity.Layer, out string? name)
                ? name
                : string.Empty;
            DrawPolygon(context, palette, palette.StyleFor(layer), polygon);
        }

        DrawAttention(context, palette);
        DrawSelection(context, palette, editor);
        DrawVirtualFeatures(context, sketch, sketch.RelationshipsInOrder, palette.Dimension);
        if (_snap is { } plan)
        {
            DrawVirtualFeatures(context, sketch, plan.Relationships, palette.Snap);
            DrawSnapIndicator(context, palette, sketch, plan);
        }

        DrawHandles(context, palette);
        DrawAxes(context);
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
        DrawEdges(context, pen, polygon);
    }

    void DrawEdges(DrawingContext context, Pen pen, ScenePolygon polygon)
    {
        int count = polygon.Points.Length;
        for (int i = 0; i < count; i++)
        {
            if (polygon.EdgeDrawn[i])
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
        foreach (ScenePolygon polygon in Scene.Polygons)
        {
            if (_attention.Contains(polygon.Box) && polygon.FacesTowards(_camera))
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
        foreach (ScenePolygon polygon in Scene.Polygons)
        {
            if (editor.Selection.Contains(polygon.Box) && polygon.FacesTowards(_camera))
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
            context.DrawLine(pen, _camera.Project(new Vector3d(x, firstY, 0)), _camera.Project(new Vector3d(x, lastY, 0)));
        }

        for (double y = firstY; y <= lastY + 1e-9; y += step)
        {
            context.DrawLine(pen, _camera.Project(new Vector3d(firstX, y, 0)), _camera.Project(new Vector3d(lastX, y, 0)));
        }
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

    enum Gesture
    {
        None,
        Orbit,
        Pan,
        MoveAxis,
        MovePlane,
        Resize,
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;

using Napkin.App.GuiTests.Harness;

namespace Napkin.Demo;

/// <summary>Thrown out of a scenario to stop it: an expectation failed and the run is not keeping going.</summary>
sealed class ScenarioStoppedException(string message) : Exception(message);

/// <summary>
/// Drives a real, visible window with the same verbs <see cref="AppDriver"/> offers headless, paced
/// so a person can watch (#151).
/// </summary>
/// <remarks>
/// <para>
/// Input goes where the OS backend's own input goes: into the top level's platform
/// implementation's <c>ITopLevelImpl.Input</c> callback (bound in <see cref="RawInput"/>), as <c>RawPointerEventArgs</c>,
/// <c>RawKeyEventArgs</c>, <c>RawTextInputEventArgs</c> and <c>RawMouseWheelEventArgs</c>, exactly
/// as <c>Avalonia.Headless</c>'s window does. Hit-testing, pointer capture, focus, routed events,
/// menus and key bindings therefore run for real. The pointer is a mouse device of its own, so the
/// OS cursor does not move; the overlay draws where it is instead.
/// </para>
/// <para>
/// Every verb paces itself by pumping the dispatcher in a nested frame (<see cref="Pump"/>), never
/// by sleeping, so the window keeps rendering and responding while the scenario waits.
/// </para>
/// </remarks>
sealed class LiveDriver : IGuiDriver
{
    readonly Action<RawInputEventArgs> _input;
    readonly IInputRoot _root;
    readonly IMouseDevice _mouse = RawInput.NewMouse();
    readonly IKeyboardDevice _keyboard = RawInput.Keyboard();
    readonly LiveOverlay _overlay;
    readonly double _speed;
    readonly bool _keepGoing;
    Point _pointer;

    public LiveDriver(Window target, LiveOverlay overlay, double speed, bool keepGoing)
    {
        Target = target;
        _input = RawInput.InputOf(target);
        _root = RawInput.RootOf(target);
        _overlay = overlay;
        _speed = speed;
        _keepGoing = keepGoing;
        _pointer = new Point(target.Bounds.Width * 0.6, target.Bounds.Height * 0.7);
        _overlay.MovePointer(_pointer, pressed: false);
        target.Closed += (_, _) => WindowClosed = true;
    }

    public TopLevel Target { get; }

    /// <summary>How many expectations failed, or how many times the scenario itself threw.</summary>
    public int Failures { get; private set; }

    /// <summary>How many expectations passed.</summary>
    public int Passed { get; private set; }

    /// <summary>The person closed the window part way through.</summary>
    public bool WindowClosed { get; private set; }

    static bool MacOS => OperatingSystem.IsMacOS();

    // ---- Pointer -------------------------------------------------------------------------

    public void MoveTo(Point point, KeyModifiers modifiers = KeyModifiers.None) =>
        Glide(point, ToRaw(modifiers));

    public void Click(Point point, MouseButton button = MouseButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
        RawInputModifiers raw = ToRaw(modifiers);
        Glide(point, raw);
        Pause(150);
        Button(button, down: true, point, raw);
        Pause(90);
        Button(button, down: false, point, raw);
        Pause(250);
    }

    public void RightClick(Point point) => Click(point, MouseButton.Right);

    public void DoubleClick(Point point, MouseButton button = MouseButton.Left)
    {
        Glide(point, RawInputModifiers.None);
        Pause(150);

        // Unscaled and short: both presses must land inside the platform's double-click time.
        Button(button, down: true, point, RawInputModifiers.None);
        Pump(TimeSpan.FromMilliseconds(40));
        Button(button, down: false, point, RawInputModifiers.None);
        Pump(TimeSpan.FromMilliseconds(60));
        Button(button, down: true, point, RawInputModifiers.None);
        Pump(TimeSpan.FromMilliseconds(40));
        Button(button, down: false, point, RawInputModifiers.None);
        Pause(250);
    }

    public void Drag(params Point[] path) => DragWith(KeyModifiers.None, path);

    public void DragWith(KeyModifiers modifiers, params Point[] path)
    {
        if (path.Length < 2)
        {
            throw new ArgumentException("A drag needs a start and at least one further point.", nameof(path));
        }

        RawInputModifiers raw = ToRaw(modifiers);
        Glide(path[0], raw);
        Pause(150);
        Button(MouseButton.Left, down: true, path[0], raw);
        Pause(120);
        foreach (Point next in path.Skip(1))
        {
            Glide(next, raw | RawInputModifiers.LeftMouseButton, slower: true);
        }

        Pause(120);
        Button(MouseButton.Left, down: false, path[^1], raw);
        Pause(250);
    }

    public void PressAt(Point point)
    {
        Glide(point, RawInputModifiers.None);
        Pause(150);
        Button(MouseButton.Left, down: true, point, RawInputModifiers.None);
        Pause(150);
    }

    public void DragTo(Point point)
    {
        Glide(point, RawInputModifiers.LeftMouseButton, slower: true);
        Pause(150);
    }

    public void ReleaseAt(Point point)
    {
        Glide(point, RawInputModifiers.LeftMouseButton, slower: true);
        Button(MouseButton.Left, down: false, point, RawInputModifiers.None);
        Pause(250);
    }

    public void Wheel(Point point, Vector delta, KeyModifiers modifiers = KeyModifiers.None)
    {
        RawInputModifiers raw = ToRaw(modifiers);
        Glide(point, raw);
        Pause(120);
        Inject(RawInput.Wheel(_mouse, _root, point, delta, raw));
        Pause(250);
    }

    // ---- Keyboard ------------------------------------------------------------------------

    public void Press(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        _overlay.ShowKey(LivePacing.KeyCaption(key, modifiers, MacOS));
        RawInputModifiers raw = ToRaw(modifiers);
        Pause(200);
        Inject(RawInput.Key(_keyboard, _root, RawKeyEventType.KeyDown, key, raw));
        Pause(60);
        Inject(RawInput.Key(_keyboard, _root, RawKeyEventType.KeyUp, key, raw));
        Pause(350);
    }

    public void Chord(Key key, KeyModifiers extraModifiers = KeyModifiers.None) =>
        Press(key, AppDriver.CommandModifier | extraModifiers);

    public void Tab(int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            Press(Key.Tab);
        }
    }

    public void ShiftTab(int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            Press(Key.Tab, KeyModifiers.Shift);
        }
    }

    public void Type(string text)
    {
        _overlay.ShowKey("typing");
        foreach ((string piece, TimeSpan before) in LivePacing.TypingSchedule(text, LivePacing.PerCharacter))
        {
            Pump(before / _speed);
            Inject(RawInput.Text(_keyboard, _root, piece));
        }

        Pause(300);
    }

    // ---- Environment ---------------------------------------------------------------------

    public void ResizeWindow(double width, double height)
    {
        Window window = (Window)Target;
        window.Width = width;
        window.Height = height;
        Pause(400);
    }

    public void WaitForIdle() => Pump(TimeSpan.FromMilliseconds(100));

    public void SaveFrame(string step) => Console.WriteLine($"    (frame \"{step}\": headless only)");

    public void Say(string caption)
    {
        Console.WriteLine("  " + caption);
        _overlay.Say(caption);
        Pause(700);
    }

    // ---- Assertions ----------------------------------------------------------------------

    public void Expect(string what, Action assertion)
    {
        Exception? failure = null;
        try
        {
            assertion();
        }
        catch (Exception exception) when (exception is not ScenarioStoppedException)
        {
            failure = exception;
        }

        string caption = LivePacing.ExpectCaption(what, failure);
        _overlay.Expect(caption, failure is null);
        if (failure is null)
        {
            Passed++;
            Console.WriteLine("    " + caption);
            Pause(600);
            return;
        }

        Failures++;
        Console.Error.WriteLine("    " + caption);
        Console.Error.WriteLine(Indent(failure.Message));
        Pause(1500);
        if (!_keepGoing)
        {
            throw new ScenarioStoppedException(caption);
        }
    }

    /// <summary>Records that the scenario itself threw, outside any expectation, and shows it.</summary>
    public void Fail(Exception exception)
    {
        Failures++;
        string caption = LivePacing.ExpectCaption("the scenario stopped", exception);
        _overlay.Expect(caption, passed: false);
        Console.Error.WriteLine("    " + caption);
        Console.Error.WriteLine(Indent(exception.ToString()));
    }

    // ---- Internals -----------------------------------------------------------------------

    void Inject(RawInputEventArgs input)
    {
        if (WindowClosed)
        {
            throw new OperationCanceledException("The window was closed.");
        }

        _input(input);
    }

    void Button(MouseButton button, bool down, Point point, RawInputModifiers modifiers)
    {
        RawPointerEventType type = (button, down) switch
        {
            (MouseButton.Left, true) => RawPointerEventType.LeftButtonDown,
            (MouseButton.Left, false) => RawPointerEventType.LeftButtonUp,
            (MouseButton.Right, true) => RawPointerEventType.RightButtonDown,
            (MouseButton.Right, false) => RawPointerEventType.RightButtonUp,
            (MouseButton.Middle, true) => RawPointerEventType.MiddleButtonDown,
            (MouseButton.Middle, false) => RawPointerEventType.MiddleButtonUp,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Only left, right and middle buttons."),
        };

        Inject(RawInput.Pointer(_mouse, _root, type, point, modifiers));
        _overlay.MovePointer(point, pressed: down);
        if (down)
        {
            _overlay.Ripple(point);
        }
    }

    /// <summary>
    /// Moves the pointer to <paramref name="to"/> along an eased path, one injected move per
    /// frame, so hover effects and drags see every step a hand would make.
    /// </summary>
    void Glide(Point to, RawInputModifiers modifiers, bool slower = false)
    {
        double pixelsPerSecond = LivePacing.DefaultPixelsPerSecond * _speed * (slower ? 0.5 : 1);
        bool pressed = modifiers.HasFlag(RawInputModifiers.LeftMouseButton);
        foreach (Point point in LivePacing.Glide(_pointer, to, pixelsPerSecond, LivePacing.Frame))
        {
            Inject(RawInput.Pointer(_mouse, _root, RawPointerEventType.Move, point, modifiers));
            _pointer = point;
            _overlay.MovePointer(point, pressed);
            Pump(LivePacing.Frame);
        }
    }

    /// <summary>A pause for the eye, scaled by the run's speed.</summary>
    void Pause(double milliseconds) => Pump(TimeSpan.FromMilliseconds(milliseconds / _speed));

    /// <summary>
    /// Lets <paramref name="duration"/> pass while the dispatcher keeps running — input, layout,
    /// rendering, timers — by pushing a nested frame that a one-shot timer ends.
    /// </summary>
    void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, duration > TimeSpan.Zero ? duration : TimeSpan.FromMilliseconds(1));
        Dispatcher.UIThread.PushFrame(frame);
        if (WindowClosed)
        {
            throw new OperationCanceledException("The window was closed.");
        }
    }

    static string Indent(string text) =>
        string.Join(Environment.NewLine, text.Split('\n').Select(line => "      " + line.TrimEnd('\r')));

    static RawInputModifiers ToRaw(KeyModifiers modifiers)
    {
        var raw = RawInputModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            raw |= RawInputModifiers.Alt;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            raw |= RawInputModifiers.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            raw |= RawInputModifiers.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            raw |= RawInputModifiers.Meta;
        }

        return raw;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Napkin.App.GuiTests.Harness;

/// <summary>
/// Drives a running Avalonia top level with simulated keyboard and mouse input, and records every
/// step it takes.
/// </summary>
/// <remarks>
/// <para>
/// Every verb here goes through the headless platform's input simulation, which feeds the same
/// raw-input pipeline the OS backends feed. Hit-testing, pointer capture, focus, routed events and
/// keyboard shortcuts therefore all run for real. Nothing in this class calls a view model, raises
/// a routed event by hand, or sets a property to fake what a user did — if a verb cannot be
/// expressed as input, it is recorded as <see cref="GuiActionKind.Window"/> and does not count
/// towards the workflow rule.
/// </para>
/// <para>
/// Coordinates are in the top level's device-independent pixels, origin at its top-left corner.
/// </para>
/// </remarks>
public sealed class AppDriver
{
    readonly List<GuiAction> _actions = [];
    int _framesSaved;

    AppDriver(TopLevel target, string frameNamePrefix)
    {
        Target = target;
        FrameNamePrefix = frameNamePrefix;
        Probe = new InputProbe(target);
    }

    /// <summary>Attaches a driver to a top level that is already constructed.</summary>
    /// <param name="target">The window (or other top level) to drive.</param>
    /// <param name="frameNamePrefix">Prefix for PNG frames this driver saves.</param>
    public static AppDriver Attach(TopLevel target, string frameNamePrefix) =>
        new(target, frameNamePrefix);

    /// <summary>The top level being driven.</summary>
    public TopLevel Target { get; }

    /// <summary>The input events the application actually received.</summary>
    public InputProbe Probe { get; }

    /// <summary>Every step taken so far, in order.</summary>
    public IReadOnlyList<GuiAction> Actions => _actions;

    /// <summary>How many simulated input actions have been performed.</summary>
    public int InputActionCount => _actions.Count(a => a.IsInput);

    /// <summary>Prefix used for the PNG files this driver writes under artifacts/gui-frames/.</summary>
    public string FrameNamePrefix { get; }

    /// <summary>
    /// The modifier this platform uses for menu shortcuts — Control on Windows and Linux, Meta
    /// (Command) on macOS. Read from Avalonia's platform settings rather than hardcoded, so a
    /// workflow can write <c>Chord(Key.Z)</c> and mean "the undo shortcut on this machine".
    /// </summary>
    public static KeyModifiers CommandModifier =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers
            ?? KeyModifiers.Control;

    // ---- Pointer -------------------------------------------------------------------------

    /// <summary>Moves the pointer to a point, as a hover would.</summary>
    public void MoveTo(Point point, KeyModifiers modifiers = KeyModifiers.None)
    {
        Target.MouseMove(point, ToRaw(modifiers));
        Settle();
        Record(GuiActionKind.Pointer, $"move to {Format(point)}");
    }

    /// <summary>
    /// Clicks at a point: the pointer moves there first, as a real mouse must, then presses and
    /// releases.
    /// </summary>
    public void Click(Point point, MouseButton button = MouseButton.Left,
        KeyModifiers modifiers = KeyModifiers.None)
    {
        var raw = ToRaw(modifiers);
        Target.MouseMove(point, raw);
        Target.MouseDown(point, button, raw);
        Target.MouseUp(point, button, raw);
        Settle();
        Record(GuiActionKind.Pointer, $"{button.ToString().ToLowerInvariant()} click at {Format(point)}"
            + (modifiers == KeyModifiers.None ? "" : $" with {modifiers}"));
    }

    /// <summary>Right-clicks at a point — the gesture that opens a context menu.</summary>
    public void RightClick(Point point) => Click(point, MouseButton.Right);

    /// <summary>
    /// Double-clicks at a point. The two presses are delivered without an intervening move, which
    /// is what makes Avalonia report <c>ClickCount == 2</c> on the second press.
    /// </summary>
    public void DoubleClick(Point point, MouseButton button = MouseButton.Left)
    {
        Target.MouseMove(point);
        Target.MouseDown(point, button);
        Target.MouseUp(point, button);
        Target.MouseDown(point, button);
        Target.MouseUp(point, button);
        Settle();
        Record(GuiActionKind.Pointer, $"double click at {Format(point)}");
    }

    /// <summary>
    /// Presses at the first point, moves through every remaining point in order, and releases at
    /// the last one. The intermediate moves carry the left button down, so the application sees a
    /// real drag — pointer capture, <c>IsLeftButtonPressed</c> and incremental deltas all behave
    /// as they would under a hand.
    /// </summary>
    /// <param name="path">At least two points: where the drag starts, then where it goes.</param>
    public void Drag(params Point[] path)
    {
        if (path.Length < 2)
        {
            throw new ArgumentException(
                "A drag needs a start and at least one further point.", nameof(path));
        }

        Target.MouseMove(path[0]);
        Target.MouseDown(path[0], MouseButton.Left);
        for (var i = 1; i < path.Length; i++)
        {
            Target.MouseMove(path[i], RawInputModifiers.LeftMouseButton);
        }

        Target.MouseUp(path[^1], MouseButton.Left);
        Settle();
        Record(GuiActionKind.Pointer,
            $"drag along {string.Join(" -> ", path.Select(Format))}");
    }

    /// <summary>Turns the mouse wheel at a point. Positive Y scrolls up, as Avalonia reports it.</summary>
    public void Wheel(Point point, Vector delta, KeyModifiers modifiers = KeyModifiers.None)
    {
        Target.MouseMove(point, ToRaw(modifiers));
        Target.MouseWheel(point, delta, ToRaw(modifiers));
        Settle();
        Record(GuiActionKind.Wheel, $"wheel {Format(delta)} at {Format(point)}");
    }

    // ---- Keyboard ------------------------------------------------------------------------

    /// <summary>Presses and releases a key, optionally with modifiers held.</summary>
    public void Press(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var raw = ToRaw(modifiers);
        Target.KeyPress(key, raw, PhysicalKey.None, null);
        Target.KeyRelease(key, raw, PhysicalKey.None, null);
        Settle();
        Record(GuiActionKind.Keyboard,
            modifiers == KeyModifiers.None ? $"press {key}" : $"press {modifiers}+{key}");
    }

    /// <summary>
    /// Presses a key with this platform's command modifier held — Ctrl+key on Windows, Cmd+key on
    /// macOS. See <see cref="CommandModifier"/>.
    /// </summary>
    public void Chord(Key key, KeyModifiers extraModifiers = KeyModifiers.None) =>
        Press(key, CommandModifier | extraModifiers);

    /// <summary>Moves focus forward with Tab, once per <paramref name="times"/>.</summary>
    public void Tab(int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            Press(Key.Tab);
        }
    }

    /// <summary>Moves focus backward with Shift+Tab, once per <paramref name="times"/>.</summary>
    public void ShiftTab(int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            Press(Key.Tab, KeyModifiers.Shift);
        }
    }

    /// <summary>
    /// Types text into whatever has focus. This is a text-input event, which is what a text box
    /// listens to — the same thing a keyboard layout or an IME would produce. Key presses alone do
    /// not put characters in a text box.
    /// </summary>
    public void Type(string text)
    {
        Target.KeyTextInput(text);
        Settle();
        Record(GuiActionKind.Text, $"type \"{text}\"");
    }

    // ---- Environment ---------------------------------------------------------------------

    /// <summary>
    /// Resizes the window. This is a windowing-API call, not simulated input — there is no way to
    /// drag a window's chrome in a headless test — so it is recorded but does not count towards
    /// the workflow rule.
    /// </summary>
    public void ResizeWindow(double width, double height)
    {
        if (Target is not Window window)
        {
            throw new InvalidOperationException("Only a Window can be resized.");
        }

        window.Width = width;
        window.Height = height;
        Settle();
        Record(GuiActionKind.Window, $"resize window to {width}x{height}");
    }

    /// <summary>Lets layout, rendering and queued dispatcher work finish.</summary>
    public void WaitForIdle()
    {
        Settle();
        Record(GuiActionKind.Wait, "wait for layout and render to settle");
    }

    // ---- Assertions ----------------------------------------------------------------------

    /// <summary>
    /// Makes an assertion about observable state and records that it happened. The workflow rule
    /// requires at least one of these <em>after</em> input has changed something, which is what
    /// separates a workflow from a sequence of button mashing.
    /// </summary>
    /// <param name="what">What is being asserted, in words — it appears in the log.</param>
    /// <param name="assertion">The assertion itself; let it throw to fail the test.</param>
    public void Expect(string what, Action assertion)
    {
        assertion();
        Record(GuiActionKind.Expect, what);
    }

    // ---- Frames --------------------------------------------------------------------------

    /// <summary>
    /// Renders the current visual tree and writes it to <c>artifacts/gui-frames/</c> as a PNG.
    /// Frames are CI artifacts for a human to look at; nothing compares them pixel by pixel,
    /// because fonts and theme resolution differ between the Windows and macOS runners.
    /// </summary>
    /// <param name="step">A short name for this moment in the scenario, used in the file name.</param>
    /// <returns>The full path of the file written.</returns>
    public string SaveFrame(string step)
    {
        var frame = CaptureFrame()
            ?? throw new InvalidOperationException("Nothing has been rendered yet.");

        var directory = RepositoryLayout.FramesDirectory;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{FrameNamePrefix}-{++_framesSaved:00}-{step}.png");
        frame.Save(path, new PngBitmapEncoderOptions());
        Record(GuiActionKind.Window, $"save frame \"{step}\"");
        return path;
    }

    /// <summary>Ticks the render timer and returns the frame that was rendered, if any.</summary>
    public Bitmap? CaptureFrame()
    {
        Settle();
        return Target.CaptureRenderedFrame();
    }

    // ---- Internals -----------------------------------------------------------------------

    void Record(GuiActionKind kind, string description) =>
        _actions.Add(new GuiAction(kind, description));

    /// <summary>
    /// Runs queued dispatcher work — including layout and the render pass — so that the next verb
    /// sees the state the previous one produced, exactly as a user's next gesture would.
    /// </summary>
    static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Dispatcher.UIThread.RunJobs();
    }

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

    static string Format(Point point) =>
        $"({point.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
        $"{point.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)})";

    static string Format(Vector vector) =>
        $"({vector.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
        $"{vector.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Napkin.App.GuiTests.Harness;

/// <summary>
/// The verbs a GUI scenario is written against, so one scenario body runs on both hosts (#151):
/// <see cref="AppDriver"/> on the headless platform (CI, the workflow rule, the GUI ratchet), and
/// the live driver in <c>tools/Napkin.Demo</c>, which injects the same input into a real,
/// visible window and paces it so a person can watch.
/// </summary>
/// <remarks>
/// <para>
/// The verbs are synchronous on purpose: a scenario reads the window (controls, bounds, the model)
/// between them, which has to happen on the UI thread. The live host paces a verb by pumping the
/// dispatcher in a nested frame while it animates, so the window stays responsive without the
/// scenario becoming asynchronous; the headless host settles and returns at once.
/// </para>
/// <para>
/// Coordinates are in the top level's device-independent pixels, origin at its top-left corner.
/// </para>
/// </remarks>
public interface IGuiDriver
{
    /// <summary>The top level being driven.</summary>
    TopLevel Target { get; }

    /// <summary>Moves the pointer to a point, as a hover would.</summary>
    void MoveTo(Point point, KeyModifiers modifiers = KeyModifiers.None);

    /// <summary>Moves to a point, then presses and releases a button there.</summary>
    void Click(Point point, MouseButton button = MouseButton.Left, KeyModifiers modifiers = KeyModifiers.None);

    /// <summary>Right-clicks at a point.</summary>
    void RightClick(Point point);

    /// <summary>Double-clicks at a point.</summary>
    void DoubleClick(Point point, MouseButton button = MouseButton.Left);

    /// <summary>Presses at the first point, moves through the rest with the button down, releases at the last.</summary>
    void Drag(params Point[] path);

    /// <summary><see cref="Drag"/> with modifier keys held throughout.</summary>
    void DragWith(KeyModifiers modifiers, params Point[] path);

    /// <summary>Presses the left button at a point and leaves it down.</summary>
    void PressAt(Point point);

    /// <summary>Moves the pointer with the left button still held.</summary>
    void DragTo(Point point);

    /// <summary>Releases the left button at a point.</summary>
    void ReleaseAt(Point point);

    /// <summary>Turns the mouse wheel at a point. Positive Y scrolls up.</summary>
    void Wheel(Point point, Vector delta, KeyModifiers modifiers = KeyModifiers.None);

    /// <summary>Presses and releases a key, optionally with modifiers held.</summary>
    void Press(Key key, KeyModifiers modifiers = KeyModifiers.None);

    /// <summary>Presses a key with this platform's command modifier held.</summary>
    void Chord(Key key, KeyModifiers extraModifiers = KeyModifiers.None);

    /// <summary>Moves focus forward with Tab.</summary>
    void Tab(int times = 1);

    /// <summary>Moves focus backward with Shift+Tab.</summary>
    void ShiftTab(int times = 1);

    /// <summary>Types text into whatever has focus, as text input.</summary>
    void Type(string text);

    /// <summary>Resizes the window through the windowing API (not input).</summary>
    void ResizeWindow(double width, double height);

    /// <summary>Lets layout, rendering and queued dispatcher work finish.</summary>
    void WaitForIdle();

    /// <summary>
    /// Makes an assertion about observable state. Headless, a failure throws and fails the test;
    /// live, it is shown on screen and marks the run failed.
    /// </summary>
    void Expect(string what, Action assertion);

    /// <summary>Saves the current frame for a person to look at (headless); a no-op live.</summary>
    void SaveFrame(string step);

    /// <summary>
    /// Says what the scenario is doing next, in words. Shown in the caption bar live; nothing
    /// headless, and never counted by the workflow rule.
    /// </summary>
    void Say(string caption);
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Napkin.App.GuiTests.Harness;

/// <summary>One input event as the application actually received it.</summary>
/// <param name="Kind">
/// The routed event that arrived: <c>move</c>, <c>press</c>, <c>release</c>, <c>wheel</c>,
/// <c>keydown</c>, <c>keyup</c> or <c>text</c>.
/// </param>
/// <param name="Position">
/// Pointer position in the top level's coordinates; zero for key events.
/// </param>
/// <param name="ClickCount">Click count reported by Avalonia for a press; zero otherwise.</param>
/// <param name="LeftButtonPressed">Whether the left button was held when the event arrived.</param>
/// <param name="Key">
/// The key for keyboard events, <see cref="Avalonia.Input.Key.None"/> otherwise.
/// </param>
/// <param name="Modifiers">Modifier keys reported with the event.</param>
/// <param name="Text">
/// Text for a text-input event or a wheel delta rendered as text; else null.
/// </param>
public sealed record ObservedInput(
    string Kind,
    Point Position = default,
    int ClickCount = 0,
    bool LeftButtonPressed = false,
    Key Key = Key.None,
    KeyModifiers Modifiers = KeyModifiers.None,
    string? Text = null,
    Vector WheelDelta = default);

/// <summary>
/// Watches a top level and records the input events that reach it. The probe listens on the
/// tunnelling route from the top level down, so it sees every event the platform delivered to the
/// window, whatever the visual tree does with it afterwards.
/// </summary>
/// <remarks>
/// This is how a workflow asserts that input was really delivered and in the right order. It is a
/// read-only observer: it never handles an event, so the application sees input exactly as it
/// would without the probe.
/// </remarks>
public sealed class InputProbe
{
    readonly List<ObservedInput> _events = [];
    readonly TopLevel _target;

    public InputProbe(TopLevel target)
    {
        _target = target;
        const RoutingStrategies Route = RoutingStrategies.Tunnel;

        target.AddHandler(
            InputElement.PointerMovedEvent,
            (object? _, PointerEventArgs e) => Add("move", e),
            Route);
        target.AddHandler(
            InputElement.PointerPressedEvent,
            (object? _, PointerPressedEventArgs e) => _events.Add(new ObservedInput(
                "press",
                e.GetPosition(_target),
                e.ClickCount,
                e.GetCurrentPoint(_target).Properties.IsLeftButtonPressed,
                Modifiers: e.KeyModifiers)),
            Route);
        target.AddHandler(
            InputElement.PointerReleasedEvent,
            (object? _, PointerReleasedEventArgs e) => Add("release", e),
            Route);
        target.AddHandler(
            InputElement.PointerWheelChangedEvent,
            (object? _, PointerWheelEventArgs e) => _events.Add(new ObservedInput(
                "wheel",
                e.GetPosition(_target),
                Modifiers: e.KeyModifiers,
                WheelDelta: e.Delta)),
            Route);
        target.AddHandler(
            InputElement.KeyDownEvent,
            (object? _, KeyEventArgs e) => _events.Add(
                new ObservedInput("keydown", Key: e.Key, Modifiers: e.KeyModifiers)),
            Route);
        target.AddHandler(
            InputElement.KeyUpEvent,
            (object? _, KeyEventArgs e) => _events.Add(
                new ObservedInput("keyup", Key: e.Key, Modifiers: e.KeyModifiers)),
            Route);
        target.AddHandler(
            InputElement.TextInputEvent,
            (object? _, TextInputEventArgs e) => _events.Add(
                new ObservedInput("text", Text: e.Text)),
            Route);
    }

    /// <summary>Every input event the application received, oldest first.</summary>
    public IReadOnlyList<ObservedInput> Events => _events;

    /// <summary>
    /// The kinds of the received events, in order — handy for one-line assertions.
    /// </summary>
    public IReadOnlyList<string> Kinds => _events.Select(e => e.Kind).ToList();

    /// <summary>
    /// Forgets everything recorded so far, so a workflow can assert one phase at a time.
    /// </summary>
    public void Clear() => _events.Clear();

    void Add(string kind, PointerEventArgs e) => _events.Add(new ObservedInput(
        kind,
        e.GetPosition(_target),
        LeftButtonPressed: e.GetCurrentPoint(_target).Properties.IsLeftButtonPressed,
        Modifiers: e.KeyModifiers));
}

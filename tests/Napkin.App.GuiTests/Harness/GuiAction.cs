namespace Napkin.App.GuiTests.Harness;

/// <summary>What kind of thing a recorded step was.</summary>
public enum GuiActionKind
{
    /// <summary>Simulated pointer input: move, press, release, click, drag.</summary>
    Pointer,

    /// <summary>Simulated mouse wheel input.</summary>
    Wheel,

    /// <summary>Simulated key down/up, including chords and Tab traversal.</summary>
    Keyboard,

    /// <summary>Simulated text input (what an IME or a keyboard layout would produce).</summary>
    Text,

    /// <summary>
    /// A change made through the windowing API rather than through input — resizing a window, for
    /// instance. Recorded for the log, but it does not count towards the workflow rule.
    /// </summary>
    Window,

    /// <summary>Waiting for layout, rendering or the dispatcher to settle.</summary>
    Wait,

    /// <summary>An assertion about observable state, made through <see cref="AppDriver.Expect"/>.</summary>
    Expect,
}

/// <summary>One recorded step of a scenario, in the order it happened.</summary>
/// <param name="Kind">What kind of step it was.</param>
/// <param name="Description">A human-readable description, used in failure messages.</param>
public sealed record GuiAction(GuiActionKind Kind, string Description)
{
    /// <summary>
    /// True when this step was simulated user input. Only these count towards the five-action
    /// minimum: waiting, resizing a window through the API and asserting are not user input.
    /// </summary>
    public bool IsInput => Kind is GuiActionKind.Pointer or GuiActionKind.Wheel
        or GuiActionKind.Keyboard or GuiActionKind.Text;

    /// <summary>True when this step was pointer-like input (pointer or wheel).</summary>
    public bool IsPointerInput => Kind is GuiActionKind.Pointer or GuiActionKind.Wheel;

    /// <summary>True when this step was keyboard-like input (keys or text).</summary>
    public bool IsKeyboardInput => Kind is GuiActionKind.Keyboard or GuiActionKind.Text;

    public override string ToString() => $"{Kind}: {Description}";
}

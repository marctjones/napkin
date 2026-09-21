using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Napkin.App.GuiTests.Harness;
using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Workflows against napkin's application shell as it exists today.
/// </summary>
/// <remarks>
/// <para>
/// The shell is still the Avalonia scaffold: one window whose content is a line of text, with no
/// menus, no canvas and no focusable controls. These workflows say so honestly — they drive real
/// keyboard and mouse input into the real window and assert what can truthfully be observed: that
/// the window opens, that its content lays out and re-lays-out when the window is resized, that
/// every kind of input reaches the application in order, and that nothing in the shell takes focus
/// yet.
/// </para>
/// <para>
/// Several of them are deliberately written as guards that will fail when the application grows.
/// <see cref="Keyboard_traversal_finds_no_focusable_controls_yet"/> fails the moment a focusable
/// control appears in the shell, which is exactly when a real traversal workflow should replace
/// it. The suite's job is to grow with the product, and a test that has to be rewritten when the
/// product changes is doing its job.
/// </para>
/// </remarks>
public class ShellWorkflows
{
    [GuiWorkflow("GUI-SHELL-01")]
    public void Shell_opens_shows_its_content_and_renders_after_input() => GuiWorkflow.Run(app =>
    {
        var window = (Window)app.Target;

        app.Expect("the shell window is open with napkin's title", () =>
        {
            Assert.True(window.IsVisible);
            Assert.Equal("Napkin.App", window.Title);
        });

        app.MoveTo(new Point(450, 300));
        app.Click(new Point(450, 300));
        app.Type("12 3/4");
        app.Press(Key.Escape);
        app.Wheel(new Point(450, 300), new Vector(0, -2));

        app.Expect("every kind of input reached the application", () =>
        {
            var kinds = app.Probe.Kinds;
            Assert.Contains("move", kinds);
            Assert.Contains("press", kinds);
            Assert.Contains("release", kinds);
            Assert.Contains("text", kinds);
            Assert.Contains("keydown", kinds);
            Assert.Contains("wheel", kinds);
        });

        app.Expect("the shell still shows its content after the sequence", () =>
        {
            var text = window.GetVisualDescendants().OfType<TextBlock>().Single();
            Assert.Equal(window.Content, text.Text);
            Assert.False(string.IsNullOrWhiteSpace(text.Text));
        });

        app.Expect("a frame was rendered at the window's size", () =>
        {
            var frame = app.CaptureFrame();
            Assert.NotNull(frame);
            Assert.Equal((int)window.ClientSize.Width, frame!.PixelSize.Width);
            Assert.Equal((int)window.ClientSize.Height, frame.PixelSize.Height);
        });

        app.SaveFrame("after-input");
    });

    [GuiWorkflow("GUI-SHELL-02")]
    public void Resizing_the_shell_re_lays_out_and_re_renders_its_content() =>
        GuiWorkflow.Run(app =>
        {
            var window = (Window)app.Target;
            var text = window.GetVisualDescendants().OfType<TextBlock>().Single();

            app.Click(new Point(200, 150));
            app.Type("before the resize");

            app.ResizeWindow(640, 400);
            app.Expect("content fills the smaller window and the frame follows it",
                () => AssertLaidOutAt(app, window, text, 640, 400));
            app.SaveFrame("640x400");

            app.MoveTo(new Point(320, 200));
            app.Press(Key.Tab);

            app.ResizeWindow(1024, 720);
            app.Expect("content fills the larger window and the frame follows it",
                () => AssertLaidOutAt(app, window, text, 1024, 720));
            app.SaveFrame("1024x720");

            app.Wheel(new Point(500, 360), new Vector(0, 1));
            app.Expect("the content survived both resizes and the input in between", () =>
            {
                Assert.Same(text, window.GetVisualDescendants().OfType<TextBlock>().Single());
                Assert.Equal(window.Content, text.Text);
            });
        });

    [GuiWorkflow("GUI-SHELL-03")]
    public void Keyboard_traversal_finds_no_focusable_controls_yet() => GuiWorkflow.Run(app =>
    {
        var window = (Window)app.Target;

        app.Click(new Point(450, 300));
        app.Expect("clicking the shell focuses nothing, because there is nothing to focus",
            () => Assert.Null(window.FocusManager?.GetFocusedElement()));

        app.Tab(3);
        app.ShiftTab();
        app.Chord(Key.Z);
        app.Type("24");

        app.Expect("focus is still nowhere after a full traversal", () =>
            // This is a scaffold guard. When the shell gains its first focusable control this
            // assertion fails, and the workflow above it should be replaced by a real traversal.
            Assert.Null(window.FocusManager?.GetFocusedElement()));

        app.Expect("every key still reached the window in the order it was pressed", () =>
        {
            var keys = app.Probe.Events
                .Where(e => e.Kind == "keydown")
                .Select(e => (e.Key, e.Modifiers))
                .ToList();

            Assert.Equal(
                [(Key.Tab, KeyModifiers.None),
                 (Key.Tab, KeyModifiers.None),
                 (Key.Tab, KeyModifiers.None),
                 (Key.Tab, KeyModifiers.Shift),
                 (Key.Z, AppDriver.CommandModifier)],
                keys);
        });

        app.Expect("typing with nothing focused changes nothing in the shell", () =>
        {
            Assert.Contains("text", app.Probe.Kinds);
            Assert.Equal("Welcome to Avalonia!", window.Content);
        });

        app.SaveFrame("after-traversal");
    });

    [GuiWorkflow("GUI-SHELL-04")]
    public void A_pointer_session_is_delivered_to_the_shell_in_order() => GuiWorkflow.Run(app =>
    {
        var window = (Window)app.Target;

        app.MoveTo(new Point(100, 100));
        app.Expect("a hover arrives with no button held", () =>
        {
            var move = Assert.Single(app.Probe.Events);
            Assert.Equal("move", move.Kind);
            Assert.False(move.LeftButtonPressed);
        });

        app.Probe.Clear();
        app.Drag(
            new Point(120, 140),
            new Point(240, 200),
            new Point(360, 260),
            new Point(480, 320));

        app.Expect("the drag arrived as a press, ordered moves with the button held, a release",
            () =>
            {
                Assert.Equal(["move", "press", "move", "move", "move", "release"],
                    app.Probe.Kinds);

                var moves = app.Probe.Events.Where(e => e.Kind == "move").ToList();
                Assert.Equal(
                    [new Point(120, 140), new Point(240, 200), new Point(360, 260),
                     new Point(480, 320)],
                    moves.Select(m => m.Position));
                Assert.All(moves.Skip(1), m => Assert.True(m.LeftButtonPressed));
            });

        app.Probe.Clear();
        app.DoubleClick(new Point(300, 300));
        app.Expect("the second press is reported as a double click", () =>
        {
            var presses = app.Probe.Events.Where(e => e.Kind == "press").ToList();
            Assert.Equal(2, presses.Count);
            Assert.Equal(1, presses[0].ClickCount);
            Assert.Equal(2, presses[1].ClickCount);
        });

        app.Probe.Clear();
        app.Wheel(new Point(300, 300), new Vector(0, 4));
        app.Press(Key.Escape);
        app.Expect("the wheel delta arrived unchanged, and the keyboard still works after it",
            () =>
            {
                var wheel = Assert.Single(app.Probe.Events, e => e.Kind == "wheel");
                Assert.Equal(new Vector(0, 4), wheel.WheelDelta);
                Assert.Contains(app.Probe.Events, e => e.Kind == "keydown" && e.Key == Key.Escape);
            });

        app.Expect("the shell survived the whole session", () =>
            Assert.True(window.IsVisible));

        app.SaveFrame("after-pointer-session");
    });

    /// <summary>
    /// Asserts that the window really took the requested size, that its content was laid out to
    /// fill it, and that the next rendered frame is that size.
    /// </summary>
    static void AssertLaidOutAt(AppDriver app, Window window, TextBlock text,
        double width, double height)
    {
        Assert.Equal(width, window.ClientSize.Width);
        Assert.Equal(height, window.ClientSize.Height);
        Assert.Equal(width, text.Bounds.Width);
        Assert.Equal(height, text.Bounds.Height);

        var frame = app.CaptureFrame();
        Assert.NotNull(frame);
        Assert.Equal((int)width, frame!.PixelSize.Width);
        Assert.Equal((int)height, frame.PixelSize.Height);
    }
}

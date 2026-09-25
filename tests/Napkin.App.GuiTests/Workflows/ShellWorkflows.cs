using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Napkin.App;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.Modules.Editing.Design;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Workflows against napkin's application shell: the menu, the drawing and the status line.
/// </summary>
/// <remarks>
/// <para>
/// These four were written against the Avalonia scaffold — one window whose content was a line of
/// text — and are rewritten here against the shell the M1 viewer ships (#36). Three of them had to
/// be: they asserted the scaffold's single <c>TextBlock</c> filling the window, and
/// <see cref="Keyboard_traversal_reaches_the_shells_controls"/> was a deliberate guard that
/// asserted a full Tab traversal focused <em>nothing</em>, so that it would fail the moment the
/// shell gained its first focusable control. It has. The guard has done its job and is replaced by
/// the real traversal it was waiting for.
/// </para>
/// <para>
/// What they assert now is what is really observable in the new shell: that it opens with a
/// drawing in it, that resizing re-lays-out the canvas and the view follows, that keyboard
/// traversal lands somewhere sensible and typing changes nothing in a read-only viewer, and that a
/// pointer session arrives in order and moves the view it was aimed at.
/// </para>
/// </remarks>
public class ShellWorkflows
{
    [GuiWorkflow("GUI-SHELL-01")]
    public void Shell_opens_shows_its_content_and_renders_after_input() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        app.Expect("the shell window is open, titled for what it has open", () =>
        {
            Assert.True(window.IsVisible);
            Assert.StartsWith("napkin — ", window.Title!, StringComparison.Ordinal);
        });

        app.MoveTo(new Point(450, 300));
        app.Click(new Point(450, 300));
        app.Type("12 3/4");
        app.Press(Key.Escape);
        app.Wheel(new Point(450, 300), new Vector(0, -2));

        app.Expect("every kind of input reached the application", () =>
        {
            IReadOnlyList<string> kinds = app.Probe.Kinds;
            Assert.Contains("move", kinds);
            Assert.Contains("press", kinds);
            Assert.Contains("release", kinds);
            Assert.Contains("text", kinds);
            Assert.Contains("keydown", kinds);
            Assert.Contains("wheel", kinds);
        });

        app.Expect("the shell still has its menu, its drawing and its status line", () =>
        {
            Assert.Single(window.GetVisualDescendants().OfType<Menu>());
            Assert.Same(window.Canvas, window.GetVisualDescendants().OfType<CanvasView>().Single());
            Assert.NotNull(window.CurrentDesign);
            Assert.False(string.IsNullOrWhiteSpace(window.DesignReadout.Text));
            Assert.False(string.IsNullOrWhiteSpace(window.ZoomReadout.Text));
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
            MainWindow window = (MainWindow)app.Target;
            CanvasView canvas = window.Canvas;

            app.Click(new Point(450, 300));
            app.Type("before the resize");

            app.ResizeWindow(640, 400);
            app.Expect("the canvas fills the smaller window and the frame follows it",
                () => AssertLaidOutAt(app, window, 640, 400));
            app.SaveFrame("640x400");

            app.MoveTo(new Point(320, 200));
            app.Press(Key.Tab);

            (double centreX, double centreY) = (canvas.View.CenterXInches, canvas.View.CenterYInches);
            app.ResizeWindow(1024, 720);
            app.Expect("the canvas fills the larger window and the frame follows it",
                () => AssertLaidOutAt(app, window, 1024, 720));
            app.Expect("the model point at the centre of the view stayed at the centre", () =>
            {
                Assert.Equal(centreX, canvas.View.CenterXInches, 6);
                Assert.Equal(centreY, canvas.View.CenterYInches, 6);
            });

            app.Wheel(new Point(500, 360), new Vector(0, 1));
            app.Expect("the drawing survived both resizes and the input in between", () =>
            {
                Assert.Same(canvas, window.GetVisualDescendants().OfType<CanvasView>().Single());
                Assert.NotNull(window.CurrentDesign);
                Assert.False(canvas.Extents.IsEmpty);
            });
        });

    [GuiWorkflow("GUI-SHELL-03")]
    public void Keyboard_traversal_reaches_the_shells_controls() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        app.Click(new Point(450, 300));
        app.Expect("clicking the drawing gives it the keyboard", () =>
            Assert.IsType<CanvasView>(window.FocusManager?.GetFocusedElement()));

        Design opened = window.CurrentDesign!;
        ViewTransform view = window.Canvas.View;

        app.Tab(3);
        app.ShiftTab();
        app.Chord(Key.J);
        app.Type("24");

        app.Expect("focus is on a control of the shell, not nowhere and not outside it", () =>
        {
            object? focused = window.FocusManager?.GetFocusedElement();
            Assert.NotNull(focused);
            Assert.Contains(window.GetVisualDescendants(), visual => ReferenceEquals(visual, focused));
            Assert.True(((InputElement)focused!).Focusable);
        });

        app.Expect("every key still reached the window in the order it was pressed", () =>
        {
            List<(Key, KeyModifiers)> keys = app.Probe.Events
                .Where(e => e.Kind == "keydown")
                .Select(e => (e.Key, e.Modifiers))
                .ToList();

            Assert.Equal(
                [(Key.Tab, KeyModifiers.None),
                 (Key.Tab, KeyModifiers.None),
                 (Key.Tab, KeyModifiers.None),
                 (Key.Tab, KeyModifiers.Shift),
                 (Key.J, AppDriver.CommandModifier)],
                keys);
        });

        app.Expect("typing and an unbound chord change nothing in a read-only viewer", () =>
        {
            Assert.Contains("text", app.Probe.Kinds);
            Assert.Same(opened, window.CurrentDesign);
            Assert.Same(opened.Sketch, window.CurrentDesign!.Sketch);
            Assert.Equal(view, window.Canvas.View);
        });

        app.SaveFrame("after-traversal");
    });

    [GuiWorkflow("GUI-SHELL-04")]
    public void A_pointer_session_is_delivered_to_the_shell_in_order() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        app.MoveTo(new Point(100, 100));
        app.Expect("a hover arrives with no button held", () =>
        {
            ObservedInput move = Assert.Single(app.Probe.Events);
            Assert.Equal("move", move.Kind);
            Assert.False(move.LeftButtonPressed);
        });

        app.Probe.Clear();
        ViewTransform beforeDrag = window.Canvas.View;
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

                List<ObservedInput> moves = app.Probe.Events.Where(e => e.Kind == "move").ToList();
                Assert.Equal(
                    [new Point(120, 140), new Point(240, 200), new Point(360, 260),
                     new Point(480, 320)],
                    moves.Select(m => m.Position));
                Assert.All(moves.Skip(1), m => Assert.True(m.LeftButtonPressed));
            });

        app.Expect("the drag the window received is the drag that moved the view", () =>
        {
            Point wasAt = beforeDrag.ToScreen(Point2.Origin);
            Point isAt = window.Canvas.View.ToScreen(Point2.Origin);
            Assert.Equal(wasAt.X + 360, isAt.X, 6);
            Assert.Equal(wasAt.Y + 180, isAt.Y, 6);
        });

        app.Probe.Clear();
        app.DoubleClick(new Point(300, 300));
        app.Expect("the second press is reported as a double click", () =>
        {
            List<ObservedInput> presses = app.Probe.Events.Where(e => e.Kind == "press").ToList();
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
                ObservedInput wheel = Assert.Single(app.Probe.Events, e => e.Kind == "wheel");
                Assert.Equal(new Vector(0, 4), wheel.WheelDelta);
                Assert.Contains(app.Probe.Events, e => e.Kind == "keydown" && e.Key == Key.Escape);
            });

        app.Expect("the shell survived the whole session", () =>
            Assert.True(window.IsVisible));

        app.SaveFrame("after-pointer-session");
    });

    /// <summary>
    /// Asserts that the window took the requested size, that the canvas was laid out to fill what
    /// the menu and the status line leave of it, that the view transform followed, and that the
    /// next rendered frame is that size.
    /// </summary>
    static void AssertLaidOutAt(AppDriver app, MainWindow window, double width, double height)
    {
        Assert.Equal(width, window.ClientSize.Width);
        Assert.Equal(height, window.ClientSize.Height);

        CanvasView canvas = window.Canvas;
        Assert.Equal(width, canvas.Bounds.Width);
        Assert.Equal(
            height - window.MenuBar.Bounds.Height - window.StatusLine.Bounds.Height,
            canvas.Bounds.Height);

        // The view transform is what the drawing is painted through, so it has to have been told
        // about the new size, not merely the control.
        Assert.Equal(canvas.Bounds.Size, canvas.View.Viewport);

        var frame = app.CaptureFrame();
        Assert.NotNull(frame);
        Assert.Equal((int)width, frame!.PixelSize.Width);
        Assert.Equal((int)height, frame.PixelSize.Height);
    }
}

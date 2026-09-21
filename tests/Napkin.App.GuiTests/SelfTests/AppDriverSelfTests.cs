using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Napkin.App.GuiTests.Harness;
using Xunit;

namespace Napkin.App.GuiTests.SelfTests;

/// <summary>
/// Tests of the harness itself: each one proves that a driver verb produces the input events it
/// claims to produce, against the local <see cref="TestSurface"/>.
/// </summary>
/// <remarks>
/// These are plain <c>[AvaloniaFact]</c> tests, not workflows. They claim no feature id and are not
/// counted in <c>artifacts/gui-metrics.json</c> — they test the tool, not the product.
/// </remarks>
public class AppDriverSelfTests
{
    static (TestSurface Surface, AppDriver Driver) Open()
    {
        var surface = new TestSurface().Open();
        return (surface, AppDriver.Attach(surface, "selftest"));
    }

    [AvaloniaFact]
    public void Click_lands_on_the_control_under_the_point()
    {
        var (surface, driver) = Open();

        driver.Click(TestSurface.FirstButtonCentre);

        Assert.Equal(1, surface.FirstButtonClicks);
        Assert.Equal(0, surface.SecondButtonClicks);
    }

    [AvaloniaFact]
    public void Click_outside_a_control_does_not_reach_it()
    {
        var (surface, driver) = Open();

        driver.Click(TestSurface.InPad(10, 10));

        Assert.Equal(0, surface.FirstButtonClicks);
        Assert.Single(surface.Pad.Presses);
    }

    [AvaloniaFact]
    public void Click_moves_the_pointer_there_first()
    {
        var (surface, driver) = Open();

        driver.Click(TestSurface.InPad(40, 50));

        // A real mouse is somewhere before it is pressed: the hover move must arrive first.
        Assert.Equal(["move", "press", "release"], driver.Probe.Kinds);
        Assert.Single(surface.Pad.HoverMoves);
    }

    [AvaloniaFact]
    public void Double_click_is_reported_as_a_second_click()
    {
        var (surface, driver) = Open();

        driver.DoubleClick(TestSurface.InPad(30, 30));

        Assert.Equal(2, surface.Pad.Presses.Count);
        Assert.Equal(2, surface.Pad.LastClickCount);
    }

    [AvaloniaFact]
    public void Right_click_is_reported_as_the_right_button()
    {
        var (surface, driver) = Open();

        driver.RightClick(TestSurface.InPad(30, 30));

        Assert.Equal(MouseButton.Right, surface.Pad.LastButton);
    }

    [AvaloniaFact]
    public void Drag_delivers_every_intermediate_point_with_the_button_held()
    {
        var (surface, driver) = Open();

        driver.Drag(
            TestSurface.InPad(10, 10),
            TestSurface.InPad(60, 40),
            TestSurface.InPad(120, 80),
            TestSurface.InPad(200, 150));

        Assert.Equal([new Point(10, 10)], surface.Pad.Presses);
        Assert.Equal(
            [new Point(60, 40), new Point(120, 80), new Point(200, 150)],
            surface.Pad.DragMoves);
        Assert.Equal([new Point(200, 150)], surface.Pad.Releases);

        // Every move between press and release must carry the left button, or the application sees
        // a hover and not a drag.
        var moves = driver.Probe.Events.Where(e => e.Kind == "move").ToList();
        Assert.Equal(4, moves.Count);
        Assert.False(moves[0].LeftButtonPressed);
        Assert.All(moves.Skip(1), m => Assert.True(m.LeftButtonPressed));
    }

    [AvaloniaFact]
    public void Drag_needs_somewhere_to_go()
    {
        var (_, driver) = Open();

        Assert.Throws<ArgumentException>(() => driver.Drag(new Point(10, 10)));
    }

    [AvaloniaFact]
    public void Wheel_delivers_the_delta_at_the_point()
    {
        var (surface, driver) = Open();

        driver.Wheel(TestSurface.InPad(100, 100), new Vector(0, -3));

        Assert.Equal([new Vector(0, -3)], surface.Pad.WheelDeltas);
    }

    [AvaloniaFact]
    public void Typing_goes_into_the_focused_text_box()
    {
        var (surface, driver) = Open();

        driver.Click(TestSurface.FieldCentre);
        driver.Type("24 3/8");

        Assert.Same(surface.Field, surface.FocusManager?.GetFocusedElement());
        Assert.Equal("24 3/8", surface.Field.Text);
    }

    [AvaloniaFact]
    public void Tab_and_shift_tab_walk_the_focus_order()
    {
        var (surface, driver) = Open();

        driver.Click(TestSurface.FieldCentre);
        Assert.Same(surface.Field, surface.FocusManager?.GetFocusedElement());

        driver.Tab();
        Assert.Same(surface.FirstButton, surface.FocusManager?.GetFocusedElement());

        driver.Tab();
        Assert.Same(surface.SecondButton, surface.FocusManager?.GetFocusedElement());

        driver.ShiftTab();
        Assert.Same(surface.FirstButton, surface.FocusManager?.GetFocusedElement());
    }

    [AvaloniaFact]
    public void A_chord_carries_this_platforms_command_modifier()
    {
        var (surface, driver) = Open();

        driver.Chord(Key.Z);
        driver.Chord(Key.Z, KeyModifiers.Shift);

        Assert.Equal(
            [(Key.Z, AppDriver.CommandModifier),
             (Key.Z, AppDriver.CommandModifier | KeyModifiers.Shift)],
            surface.KeyChords);
    }

    [AvaloniaFact]
    public void Keys_reach_the_focused_control()
    {
        var (surface, driver) = Open();

        driver.Click(TestSurface.FieldCentre);
        driver.Type("12");
        driver.Press(Key.Back);

        Assert.Equal("1", surface.Field.Text);
    }

    [AvaloniaFact]
    public void Every_verb_is_recorded_with_its_kind()
    {
        var (_, driver) = Open();

        driver.MoveTo(TestSurface.InPad(5, 5));
        driver.Click(TestSurface.InPad(10, 10));
        driver.Wheel(TestSurface.InPad(10, 10), new Vector(0, 1));
        driver.Press(Key.Escape);
        driver.Type("x");
        driver.ResizeWindow(420, 340);
        driver.WaitForIdle();
        driver.Expect("nothing in particular", () => { });

        Assert.Equal(
            [GuiActionKind.Pointer, GuiActionKind.Pointer, GuiActionKind.Wheel,
             GuiActionKind.Keyboard, GuiActionKind.Text, GuiActionKind.Window,
             GuiActionKind.Wait, GuiActionKind.Expect],
            driver.Actions.Select(a => a.Kind));

        // Resizing, waiting and asserting are not input, and do not count towards the rule.
        Assert.Equal(5, driver.InputActionCount);
    }

    [AvaloniaFact]
    public void A_saved_frame_is_a_real_png_of_the_window()
    {
        var (surface, driver) = Open();

        driver.Click(TestSurface.FirstButtonCentre);
        var path = driver.SaveFrame("self-test");

        Assert.True(File.Exists(path));
        var header = File.ReadAllBytes(path)[..8];
        Assert.Equal<byte>([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A], header);

        var frame = driver.CaptureFrame();
        Assert.NotNull(frame);
        Assert.Equal((int)surface.ClientSize.Width, frame!.PixelSize.Width);
        Assert.Equal((int)surface.ClientSize.Height, frame.PixelSize.Height);
    }
}

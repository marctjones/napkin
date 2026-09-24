using System.Reflection;

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;

using Xunit;

namespace Napkin.App.GuiTests.SelfTests;

/// <summary>
/// Tests of the pure parts of the live host (#151): the pointer's glide, the typing schedule, the
/// captions, the exit code, and how scenarios are found. The live window itself is never opened
/// here — CI runs headless only.
/// </summary>
public class LiveScenarioSelfTests
{
    static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    [Fact]
    public void Easing_starts_at_zero_ends_at_one_and_never_goes_back()
    {
        Assert.Equal(0, LivePacing.EaseInOut(0));
        Assert.Equal(1, LivePacing.EaseInOut(1));
        Assert.Equal(0.5, LivePacing.EaseInOut(0.5), 12);
        Assert.Equal(0, LivePacing.EaseInOut(-3));
        Assert.Equal(1, LivePacing.EaseInOut(7));

        double previous = 0;
        for (int i = 1; i <= 100; i++)
        {
            double eased = LivePacing.EaseInOut(i / 100.0);
            Assert.True(eased >= previous, $"eased progress fell at step {i}.");
            previous = eased;
        }

        Assert.True(LivePacing.EaseInOut(0.1) < 0.1, "a glide should start slower than linear.");
        Assert.True(LivePacing.EaseInOut(0.9) > 0.9, "a glide should end slower than linear.");
    }

    [Fact]
    public void A_glide_ends_exactly_on_the_destination_and_moves_steadily_towards_it()
    {
        var from = new Point(10.25, 20.5);
        var to = new Point(610.75, 420.125);

        IReadOnlyList<Point> path = LivePacing.Glide(from, to, 750, Frame);

        Assert.Equal(to, path[^1]);
        double remaining = ((Vector)(to - from)).Length;
        foreach (Point point in path)
        {
            double left = ((Vector)(to - point)).Length;
            Assert.True(left <= remaining + 1e-9, "the pointer moved away from where it is going.");
            remaining = left;

            // On the straight line from start to end.
            Vector a = point - from, b = to - from;
            Assert.Equal(0, (a.X * b.Y) - (a.Y * b.X), 6);
        }
    }

    [Fact]
    public void A_glide_takes_one_step_per_frame_for_as_long_as_the_speed_says()
    {
        var from = new Point(0, 0);
        var to = new Point(750, 0);

        // 750 px at 750 px/s is one second: 63 frames of 16 ms (62.5, rounded up).
        Assert.Equal(63, LivePacing.Glide(from, to, 750, Frame).Count);

        // Twice the speed, half the steps (rounded up).
        Assert.Equal(32, LivePacing.Glide(from, to, 1500, Frame).Count);

        // Already there, or a hair away: one step, straight to the point.
        Assert.Equal([to], LivePacing.Glide(to, to, 750, Frame));
        Assert.Single(LivePacing.Glide(from, new Point(1, 0), 750, Frame));

        Assert.Throws<ArgumentOutOfRangeException>(() => LivePacing.Glide(from, to, 0, Frame));
        Assert.Throws<ArgumentOutOfRangeException>(() => LivePacing.Glide(from, to, 750, TimeSpan.Zero));
    }

    [Fact]
    public void Typing_goes_one_character_at_a_time_with_a_longer_pause_after_a_space()
    {
        var per = TimeSpan.FromMilliseconds(100);

        var schedule = LivePacing.TypingSchedule("2 x4", per);

        Assert.Equal(["2", " ", "x", "4"], schedule.Select(step => step.Text));
        Assert.Equal(
            [TimeSpan.Zero, per, TimeSpan.FromMilliseconds(150), per],
            schedule.Select(step => step.Before));
        Assert.Empty(LivePacing.TypingSchedule("", per));
    }

    [Fact]
    public void Typing_keeps_a_character_that_is_two_code_units_whole()
    {
        // U+1F4CF STRAIGHT RULER is a surrogate pair; a combining acute accent follows its base.
        var schedule = LivePacing.TypingSchedule("a\U0001F4CFé", TimeSpan.FromMilliseconds(10));

        Assert.Equal(["a", "\U0001F4CF", "é"], schedule.Select(step => step.Text));
    }

    [Theory]
    [InlineData(0, false, 0)]
    [InlineData(1, false, 1)]
    [InlineData(4, false, 1)]
    [InlineData(0, true, 2)]
    [InlineData(3, true, 2)]
    public void The_exit_code_says_passed_failed_or_could_not_run(int failures, bool infrastructure, int expected) =>
        Assert.Equal(expected, LivePacing.ExitCode(failures, infrastructure));

    [Fact]
    public void An_expectation_caption_ticks_a_pass_and_gives_the_first_line_of_a_failure()
    {
        Assert.Equal("✓ the leg is taller", LivePacing.ExpectCaption("the leg is taller", null));
        Assert.Equal(
            "✗ the leg is taller: Assert.True() Failure",
            LivePacing.ExpectCaption("the leg is taller", new InvalidOperationException("\n  Assert.True() Failure\nExpected: True\nActual: False")));
        Assert.Equal(
            "✗ it opened: InvalidOperationException",
            LivePacing.ExpectCaption("it opened", new InvalidOperationException("")));
    }

    [Fact]
    public void A_key_caption_names_the_modifiers_the_way_the_platform_does()
    {
        Assert.Equal("Cmd+Z", LivePacing.KeyCaption(Key.Z, KeyModifiers.Meta, macOS: true));
        Assert.Equal("Ctrl+Z", LivePacing.KeyCaption(Key.Z, KeyModifiers.Control, macOS: false));
        Assert.Equal("Shift+Cmd+L", LivePacing.KeyCaption(Key.L, KeyModifiers.Meta | KeyModifiers.Shift, macOS: true));
        Assert.Equal("Ctrl+Alt+Shift+Win+Delete",
            LivePacing.KeyCaption(Key.Delete, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta, macOS: false));
        Assert.Equal("Option+7", LivePacing.KeyCaption(Key.D7, KeyModifiers.Alt, macOS: true));
        Assert.Equal("V", LivePacing.KeyCaption(Key.V, KeyModifiers.None, macOS: true));
    }

    [Fact]
    public void The_application_scenarios_are_found_and_each_one_is_also_a_headless_workflow()
    {
        IReadOnlyList<GuiScenarioInfo> scenarios = GuiScenarios.All;

        Assert.Contains(scenarios, scenario => scenario.Id == "GUI-ASSEM-16");

        HashSet<string> workflows = typeof(GuiScenarios).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttribute<GuiWorkflowAttribute>()?.FeatureId)
            .OfType<string>()
            .ToHashSet();
        Assert.All(scenarios, scenario => Assert.Contains(scenario.Id, workflows));
        Assert.All(scenarios, scenario => Assert.False(string.IsNullOrWhiteSpace(scenario.Title)));
    }

    [Fact]
    public void Discovery_finds_marked_methods_in_id_order_and_ignores_the_rest()
    {
        IReadOnlyList<GuiScenarioInfo> found = GuiScenarios.Discover([typeof(TwoScenarios)]);

        Assert.Equal(["A-01", "B-02"], found.Select(scenario => scenario.Id));
        Assert.Equal("first", found[0].Title);

        var driver = new RecordingDriver();
        found[1].Body(driver);
        Assert.Equal(["second ran"], driver.Said);
    }

    [Fact]
    public void Discovery_refuses_a_scenario_of_the_wrong_shape_or_a_repeated_id()
    {
        var shape = Assert.Throws<InvalidOperationException>(() => GuiScenarios.Discover([typeof(WrongShape)]));
        Assert.Contains("WrongShape.TakesNothing", shape.Message, StringComparison.Ordinal);

        var twice = Assert.Throws<InvalidOperationException>(
            () => GuiScenarios.Discover([typeof(TwoScenarios), typeof(SameIdAgain)]));
        Assert.Contains("A-01", twice.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Saying_something_headless_is_not_a_step()
    {
        var surface = new TestSurface().Open();
        AppDriver driver = AppDriver.Attach(surface, "selftest");

        IGuiDriver shared = driver;
        shared.Say("nobody is watching");
        shared.Click(TestSurface.FirstButtonCentre);
        shared.Say("still nobody");

        Assert.Single(driver.Actions);
        Assert.Equal(GuiActionKind.Pointer, driver.Actions[0].Kind);
    }

    static class TwoScenarios
    {
        [GuiScenario("B-02", "second")]
        public static void Second(IGuiDriver app) => app.Say("second ran");

        [GuiScenario("A-01", "first")]
        static void First(IGuiDriver app) => app.Say("first ran");

        public static void NotAScenario(IGuiDriver app) => app.Say("never");
    }

    static class SameIdAgain
    {
        [GuiScenario("A-01", "again")]
        public static void Again(IGuiDriver app) => app.Say("again");
    }

    static class WrongShape
    {
        [GuiScenario("W-01", "wrong")]
        public static void TakesNothing()
        {
        }
    }

    /// <summary>A driver that only listens, for running a found body without a window.</summary>
    sealed class RecordingDriver : IGuiDriver
    {
        public List<string> Said { get; } = [];

        public Avalonia.Controls.TopLevel Target => throw new NotSupportedException();

        public void Say(string caption) => Said.Add(caption);

        public void MoveTo(Point point, KeyModifiers modifiers = KeyModifiers.None) => throw new NotSupportedException();

        public void Click(Point point, MouseButton button = MouseButton.Left, KeyModifiers modifiers = KeyModifiers.None) => throw new NotSupportedException();

        public void RightClick(Point point) => throw new NotSupportedException();

        public void DoubleClick(Point point, MouseButton button = MouseButton.Left) => throw new NotSupportedException();

        public void Drag(params Point[] path) => throw new NotSupportedException();

        public void DragWith(KeyModifiers modifiers, params Point[] path) => throw new NotSupportedException();

        public void PressAt(Point point) => throw new NotSupportedException();

        public void DragTo(Point point) => throw new NotSupportedException();

        public void ReleaseAt(Point point) => throw new NotSupportedException();

        public void Wheel(Point point, Vector delta, KeyModifiers modifiers = KeyModifiers.None) => throw new NotSupportedException();

        public void Press(Key key, KeyModifiers modifiers = KeyModifiers.None) => throw new NotSupportedException();

        public void Chord(Key key, KeyModifiers extraModifiers = KeyModifiers.None) => throw new NotSupportedException();

        public void Tab(int times = 1) => throw new NotSupportedException();

        public void ShiftTab(int times = 1) => throw new NotSupportedException();

        public void Type(string text) => throw new NotSupportedException();

        public void ResizeWindow(double width, double height) => throw new NotSupportedException();

        public void WaitForIdle() => throw new NotSupportedException();

        public void Expect(string what, Action assertion) => throw new NotSupportedException();

        public void SaveFrame(string step) => throw new NotSupportedException();
    }
}

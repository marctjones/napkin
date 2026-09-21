using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Napkin.App.GuiTests.Harness;
using Xunit;

namespace Napkin.App.GuiTests.SelfTests;

/// <summary>
/// Tests of the workflow rule. The important one is
/// <see cref="A_one_click_scenario_is_rejected"/>: if a single click could pass, the whole suite
/// would be a button-clicking suite wearing a workflow badge.
/// </summary>
public class WorkflowRuleSelfTests
{
    static AppDriver OpenDriver() => AppDriver.Attach(new TestSurface().Open(), "rule-self-test");

    [AvaloniaFact]
    public void A_one_click_scenario_is_rejected()
    {
        var driver = OpenDriver();

        var failure = Assert.Throws<GuiWorkflowRuleException>(() =>
            GuiWorkflow.RunUnrecorded("GUI-SELFTEST-ONECLICK", driver, app =>
            {
                app.Click(TestSurface.FirstButtonCentre);
                app.Expect("the button was clicked", () => { });
            }));

        Assert.Contains("performed 1 input action(s)", failure.Message);
        Assert.Contains("used no keyboard or text input", failure.Message);

        // The failure names what the scenario did, so the author can see why it is not a workflow.
        Assert.Contains("left click at (70, 86)", failure.Message);
    }

    [AvaloniaFact]
    public void A_scenario_that_only_types_is_rejected()
    {
        var driver = OpenDriver();

        var failure = Assert.Throws<GuiWorkflowRuleException>(() =>
            GuiWorkflow.RunUnrecorded("GUI-SELFTEST-KEYSONLY", driver, app =>
            {
                app.Press(Key.Tab);
                app.Press(Key.Tab);
                app.Press(Key.Tab);
                app.Type("abc");
                app.Press(Key.Escape);
                app.Expect("keys were pressed", () => { });
            }));

        Assert.Contains("used no pointer or wheel input", failure.Message);
    }

    [AvaloniaFact]
    public void A_scenario_that_never_checks_anything_is_rejected()
    {
        var driver = OpenDriver();

        var failure = Assert.Throws<GuiWorkflowRuleException>(() =>
            GuiWorkflow.RunUnrecorded("GUI-SELFTEST-NOASSERT", driver, app =>
            {
                app.Click(TestSurface.FieldCentre);
                app.Type("18");
                app.Press(Key.Tab);
                app.Click(TestSurface.FirstButtonCentre);
                app.Wheel(TestSurface.InPad(50, 50), new Vector(0, 1));
            }));

        Assert.Contains("no Expect(...) assertion after", failure.Message);
    }

    [AvaloniaFact]
    public void An_assertion_before_any_input_does_not_satisfy_the_rule()
    {
        var driver = OpenDriver();

        var failure = Assert.Throws<GuiWorkflowRuleException>(() =>
            GuiWorkflow.RunUnrecorded("GUI-SELFTEST-EARLYASSERT", driver, app =>
            {
                app.Expect("the surface is open", () => Assert.True(app.Target.IsVisible));
                app.Click(TestSurface.FieldCentre);
                app.Type("18");
                app.Press(Key.Tab);
                app.Click(TestSurface.FirstButtonCentre);
                app.Wheel(TestSurface.InPad(50, 50), new Vector(0, 1));
            }));

        Assert.Contains("no Expect(...) assertion after", failure.Message);
    }

    [AvaloniaFact]
    public void A_genuine_multi_step_scenario_is_accepted()
    {
        var surface = new TestSurface().Open();
        var driver = AppDriver.Attach(surface, "rule-self-test");

        GuiWorkflow.RunUnrecorded("GUI-SELFTEST-OK", driver, app =>
        {
            app.Click(TestSurface.FieldCentre);
            app.Type("24 3/8");
            app.Tab();
            app.Press(Key.Space);
            app.Wheel(TestSurface.InPad(50, 50), new Vector(0, -2));
            app.Expect("the typed text survived the rest of the sequence",
                () => Assert.Equal("24 3/8", surface.Field.Text));
            app.Expect("the space bar pressed the focused button",
                () => Assert.Equal(1, surface.FirstButtonClicks));
        });

        Assert.Empty(WorkflowRule.Evaluate(driver.Actions));
    }

    [AvaloniaFact]
    public void The_rule_rejects_a_scenario_that_did_nothing_at_all()
    {
        var violations = WorkflowRule.Evaluate([]);

        Assert.Equal(4, violations.Count);
    }
}

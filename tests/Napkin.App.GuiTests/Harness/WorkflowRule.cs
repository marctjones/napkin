namespace Napkin.App.GuiTests.Harness;

/// <summary>Thrown when a scenario claims to be a workflow but does not behave like one.</summary>
public sealed class GuiWorkflowRuleException(string message) : Exception(message);

/// <summary>
/// The rule that makes this suite a <em>workflow</em> suite rather than a click-a-button suite.
/// </summary>
/// <remarks>
/// <para>A scenario must:</para>
/// <list type="number">
///   <item><description>perform at least five simulated input actions;</description></item>
///   <item><description>use both keyboard and pointer input, not one of the two;</description></item>
///   <item><description>
///     assert something through <see cref="AppDriver.Expect"/> after input has already changed the
///     application's state.
///   </description></item>
/// </list>
/// <para>
/// The point is not the numbers. It is that a test which opens a window, clicks once and asserts a
/// label is not evidence that the application works — real use is a sequence, and sequences are
/// where state gets out of step. The rule is enforced by the harness, not by review, so it cannot
/// quietly rot.
/// </para>
/// </remarks>
public static class WorkflowRule
{
    /// <summary>The minimum number of simulated input actions a workflow must perform.</summary>
    public const int MinimumInputActions = 5;

    /// <summary>
    /// Checks a recorded scenario against the rule.
    /// </summary>
    /// <returns>One sentence per violation; empty when the scenario is a workflow.</returns>
    public static IReadOnlyList<string> Evaluate(IReadOnlyList<GuiAction> actions)
    {
        var violations = new List<string>();
        var inputs = actions.Count(a => a.IsInput);
        if (inputs < MinimumInputActions)
        {
            violations.Add(
                $"performed {inputs} input action(s), but a workflow needs at least " +
                $"{MinimumInputActions}");
        }

        if (!actions.Any(a => a.IsPointerInput))
        {
            violations.Add("used no pointer or wheel input");
        }

        if (!actions.Any(a => a.IsKeyboardInput))
        {
            violations.Add("used no keyboard or text input");
        }

        if (!AssertedAfterInput(actions))
        {
            violations.Add(
                "made no Expect(...) assertion after a state-changing input action");
        }

        return violations;
    }

    /// <summary>Throws unless the recorded scenario satisfies the rule.</summary>
    /// <param name="featureId">The feature the scenario claims, for the failure message.</param>
    /// <param name="actions">The recorded scenario.</param>
    public static void Enforce(string featureId, IReadOnlyList<GuiAction> actions)
    {
        var violations = Evaluate(actions);
        if (violations.Count == 0)
        {
            return;
        }

        var log = actions.Count == 0
            ? "    (nothing was recorded)"
            : string.Join(Environment.NewLine, actions.Select((a, i) => $"    {i + 1,2}. {a}"));

        throw new GuiWorkflowRuleException(
            $"{featureId} is not a workflow: {string.Join("; ", violations)}." +
            Environment.NewLine +
            "  A workflow is a multi-step sequence a person could really perform." +
            Environment.NewLine + "  What it did:" + Environment.NewLine + log);
    }

    /// <summary>
    /// True when at least one assertion came after at least one input action — that is, when
    /// something was checked after the application's state had been changed by a user.
    /// </summary>
    static bool AssertedAfterInput(IReadOnlyList<GuiAction> actions)
    {
        var sawInput = false;
        foreach (var action in actions)
        {
            if (action.IsInput)
            {
                sawInput = true;
            }
            else if (sawInput && action.Kind == GuiActionKind.Expect)
            {
                return true;
            }
        }

        return false;
    }
}

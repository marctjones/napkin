using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.v3;

namespace Napkin.App.GuiTests.Harness;

/// <summary>
/// Marks a test as a GUI <em>workflow</em> claiming one feature id, and subjects it to
/// <see cref="WorkflowRule"/>.
/// </summary>
/// <remarks>
/// <para>
/// The body of such a test is a single call to <see cref="GuiWorkflow.Run"/>: that is what starts
/// the application on the headless dispatcher, hands the scenario an <see cref="AppDriver"/>, and
/// checks the rule when the scenario finishes.
/// </para>
/// <para>
/// This attribute derives from xunit's <see cref="FactAttribute"/> rather than from Avalonia's
/// <c>AvaloniaFactAttribute</c>, because that one is sealed in Avalonia 12.1.2. The headless
/// session is entered by <see cref="GuiWorkflow.Run"/> instead, through the same
/// <c>HeadlessUnitTestSession</c> that <c>[AvaloniaFact]</c> uses. A workflow that forgets to call
/// it is failed by <see cref="GuiWorkflowRuleAttribute"/>, so the two halves cannot drift apart.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GuiWorkflowAttribute : FactAttribute, ITraitAttribute
{
    /// <param name="featureId">
    /// The feature this workflow claims, as listed in <c>features/gui-shell.json</c> — for example
    /// <c>GUI-SHELL-01</c>.
    /// </param>
    public GuiWorkflowAttribute(
        string featureId,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber) =>
        FeatureId = featureId;

    /// <summary>The feature id claimed by this workflow.</summary>
    public string FeatureId { get; }

    /// <summary>Publishes the claim as a trait, so a run can be filtered by feature.</summary>
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>("Feature", FeatureId)];
}

/// <summary>
/// Applied to the assembly, this checks every <see cref="GuiWorkflowAttribute"/> test: it opens the
/// ambient workflow slot before the test and, if the test passed, insists that the test really ran
/// a scenario through <see cref="GuiWorkflow.Run"/>.
/// </summary>
/// <remarks>
/// The rule itself is enforced inside <see cref="GuiWorkflow.Run"/>, where the recorded actions
/// live. This hook exists to close the one hole that leaves: a workflow-attributed test that never
/// drives anything at all would otherwise pass silently and claim its feature id.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class GuiWorkflowRuleAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        var workflow = methodUnderTest.GetCustomAttribute<GuiWorkflowAttribute>();
        if (workflow is not null)
        {
            GuiMetrics.BeginRun();
            GuiWorkflowContext.Begin(workflow.FeatureId, methodUnderTest.Name);
        }
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        var workflow = methodUnderTest.GetCustomAttribute<GuiWorkflowAttribute>();
        if (workflow is null)
        {
            return;
        }

        var ranAScenario = GuiWorkflowContext.ScenarioCompleted;
        var inputActions = GuiWorkflowContext.InputActionCount;
        GuiWorkflowContext.End();

        // Never mask a real failure: a test that already failed is left alone, and it is not
        // recorded, so only workflows that genuinely passed can move the ratchet.
        if (TestContext.Current.TestState?.Result != TestResult.Passed)
        {
            return;
        }

        if (!ranAScenario)
        {
            throw new GuiWorkflowRuleException(
                $"{workflow.FeatureId} ({methodUnderTest.Name}) is marked [GuiWorkflow] but never " +
                "ran a scenario. The body of a workflow test is a call to GuiWorkflow.Run(...).");
        }

        GuiMetrics.RecordPassed(workflow.FeatureId, inputActions);
    }
}

/// <summary>
/// The ambient "which workflow is running" slot, set by <see cref="GuiWorkflowRuleAttribute"/> and
/// read by <see cref="GuiWorkflow.Run"/>. Tests in this assembly never run in parallel (see
/// AssemblyInfo.cs), so one static slot is enough and keeps the metrics deterministic.
/// </summary>
static class GuiWorkflowContext
{
    public static string? FeatureId { get; private set; }

    public static string? TestName { get; private set; }

    public static bool ScenarioCompleted { get; private set; }

    public static int InputActionCount { get; private set; }

    public static void Begin(string featureId, string testName)
    {
        FeatureId = featureId;
        TestName = testName;
        ScenarioCompleted = false;
        InputActionCount = 0;
    }

    public static void MarkScenarioCompleted(int inputActions)
    {
        ScenarioCompleted = true;
        InputActionCount = inputActions;
    }

    public static void End()
    {
        FeatureId = null;
        TestName = null;
        ScenarioCompleted = false;
        InputActionCount = 0;
    }
}

using Avalonia.Headless;
using Napkin.App.GuiTests.Harness;
using Xunit;

// Every test in this assembly runs against the real Napkin application on the headless platform.
[assembly: AvaloniaTestApplication(typeof(GuiTestApp))]

// The workflow rule and the metrics recorder share one ambient slot per test, so tests must not
// overlap. This also keeps `actionsExecuted` in artifacts/gui-metrics.json deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Applied to the assembly so that *every* [GuiWorkflow] test is checked, whether or not its author
// remembered anything. See WorkflowRule for what is enforced.
[assembly: GuiWorkflowRule]

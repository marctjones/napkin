using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace Napkin.App.GuiTests.Harness;

/// <summary>Runs a workflow scenario against the real application.</summary>
public static class GuiWorkflow
{
    /// <summary>
    /// The size every workflow's window starts at, so frames and layout are comparable.
    /// </summary>
    public static readonly Size DefaultWindowSize = new(900, 600);

    /// <summary>
    /// Starts the application on the headless dispatcher, opens its main window, hands the scenario
    /// a driver, and then holds the scenario to <see cref="WorkflowRule"/>.
    /// </summary>
    /// <param name="scenario">The workflow: a sequence of driver verbs and expectations.</param>
    /// <remarks>
    /// Call this from the body of a <see cref="GuiWorkflowAttribute"/> test and nowhere else — the
    /// feature id comes from that attribute, so a scenario cannot claim a feature it was not
    /// declared for.
    /// </remarks>
    /// <summary>
    /// A throw-away settings store holding the clean screen look, not the napkin-and-carpenter default,
    /// so colour and pixel assertions written against the plain ground stay about behaviour.
    /// </summary>
    public static Napkin.App.Settings.SettingsStore ScreenStore(string settingsDir)
    {
        MainWindow.BenchTitleBar = false;
        var store = new Napkin.App.Settings.SettingsStore(Path.Combine(settingsDir, Napkin.App.Settings.SettingsStore.FileName));
        store.Update(s => s with { SketchPaper = Napkin.App.Viewing.SketchPaper.Screen, SketchLine = Napkin.App.Viewing.SketchLine.Clean });
        return store;
    }

    /// <param name="scenario">The workflow.</param>
    /// <param name="defaultLook">Whether to keep napkin's default look rather than the clean screen.</param>
    /// <param name="packRoots">
    /// Where the app looks for code packs. None by default, so no workflow depends on packs a
    /// person has installed on the machine running it; a code-check workflow names its own.
    /// </param>
    public static void Run(Action<AppDriver> scenario, bool defaultLook = false, IReadOnlyList<string>? packRoots = null)
    {
        var featureId = GuiWorkflowContext.FeatureId
            ?? throw new GuiWorkflowRuleException(
                "GuiWorkflow.Run was called outside a [GuiWorkflow] test. Mark the test with " +
                "[GuiWorkflow(\"GUI-...\")] so the scenario claims a feature id.");

        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GuiWorkflow).Assembly);
        var inputActions = session.Dispatch(() =>
        {
            // Never the person's real settings: each run gets its own file, gone when it ends.
            string settingsDir = Path.Combine(Path.GetTempPath(), "napkin-gui-settings-" + Guid.NewGuid().ToString("N"));
            var window = new MainWindow(defaultLook ? new Napkin.App.Settings.SettingsStore(Path.Combine(settingsDir, Napkin.App.Settings.SettingsStore.FileName)) : ScreenStore(settingsDir))
            {
                Width = DefaultWindowSize.Width,
                Height = DefaultWindowSize.Height,
                PackRoots = packRoots ?? [],
            };

            var driver = AppDriver.Attach(window, featureId);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

            try
            {
                scenario(driver);
                WorkflowRule.Enforce(featureId, driver.Actions);
                return driver.InputActionCount;
            }
            finally
            {
                window.Close();
                if (Directory.Exists(settingsDir))
                {
                    Directory.Delete(settingsDir, recursive: true);
                }
            }
        }, CancellationToken.None).GetAwaiter().GetResult();

        // The metrics are written by GuiWorkflowRuleAttribute once xunit has decided the test
        // passed, so a workflow cannot be counted because of work it did before failing later.
        GuiWorkflowContext.MarkScenarioCompleted(inputActions);
    }

    /// <summary>
    /// Runs a scenario against a driver the caller supplies and enforces the rule, without touching
    /// the metrics file. This is how the harness's own tests prove the rule bites; application
    /// workflows use <see cref="Run"/>.
    /// </summary>
    internal static void RunUnrecorded(string featureId, AppDriver driver, Action<AppDriver> scenario)
    {
        scenario(driver);
        WorkflowRule.Enforce(featureId, driver.Actions);
    }
}

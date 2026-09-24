using System.Globalization;

using Avalonia;
using Avalonia.Threading;

using Napkin.App;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;

namespace Napkin.Demo;

/// <summary>
/// The live host for GUI scenarios (#151): <c>list</c> prints them, <c>run &lt;id&gt;</c> plays one
/// in a real napkin window, paced to be watched, with the same expectations the headless suite
/// checks. Exit code 0 = every expectation passed, 1 = a failure, 2 = the host could not run it.
/// See docs/testing/live-scenarios.md.
/// </summary>
static class Program
{
    const string Usage =
        "usage: Napkin.Demo list\n" +
        "       Napkin.Demo run <id> [--speed N] [--hold] [--keep-going]";

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            return Dispatch(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("napkin-demo: the live host failed: " + exception);
            return LivePacing.ExitCode(0, infrastructureError: true);
        }
    }

    static int Dispatch(string[] args)
    {
        if (args is ["list"])
        {
            foreach (GuiScenarioInfo scenario in GuiScenarios.All)
            {
                Console.WriteLine($"{scenario.Id,-16}{scenario.Title}");
            }

            return 0;
        }

        if (args is not ["run", var id, .. var options])
        {
            Console.Error.WriteLine(Usage);
            return LivePacing.ExitCode(0, infrastructureError: true);
        }

        double speed = 1;
        bool hold = false, keepGoing = false;
        for (int i = 0; i < options.Length; i++)
        {
            switch (options[i])
            {
                case "--hold":
                    hold = true;
                    break;
                case "--keep-going":
                    keepGoing = true;
                    break;
                case "--speed" when i + 1 < options.Length
                    && double.TryParse(options[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out speed)
                    && speed > 0 && double.IsFinite(speed):
                    i++;
                    break;
                default:
                    Console.Error.WriteLine($"napkin-demo: did not understand `{options[i]}`.\n{Usage}");
                    return LivePacing.ExitCode(0, infrastructureError: true);
            }
        }

        GuiScenarioInfo? chosen = GuiScenarios.All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        if (chosen is null)
        {
            Console.Error.WriteLine($"napkin-demo: no scenario `{id}`. `list` shows them.");
            return LivePacing.ExitCode(0, infrastructureError: true);
        }

        return LiveHost.Run(chosen, speed, hold, keepGoing);
    }
}

/// <summary>Starts the real app on the real platform and plays one scenario in it.</summary>
static class LiveHost
{
    /// <summary>
    /// The shipped app's builder (<c>Napkin.App.Program.BuildAvaloniaApp</c>) with one change:
    /// popups open in the window's overlay layer, as they do headless, so a scenario's menu clicks
    /// land on the same controls at the same window coordinates in both hosts, and the live pointer
    /// can be drawn above an open menu. The shipped app keeps native popups.
    /// </summary>
    static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App.App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .With(new AvaloniaNativePlatformOptions { OverlayPopups = true })
            .With(new Win32PlatformOptions { OverlayPopups = true })
            .With(new X11PlatformOptions { OverlayPopups = true });

    public static int Run(GuiScenarioInfo scenario, double speed, bool hold, bool keepGoing)
    {
        // Set up without a lifetime: napkin's App opens a window on the person's own settings when
        // it has a desktop lifetime, and a demo must never read or write those.
        BuildAvaloniaApp().SetupWithoutStarting();

        using var stop = new CancellationTokenSource();
        int exitCode = LivePacing.ExitCode(0, infrastructureError: true);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                exitCode = Play(scenario, speed, hold, keepGoing, stop);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("napkin-demo: the live host failed: " + exception);
                stop.Cancel();
            }
        });
        Dispatcher.UIThread.MainLoop(stop.Token);
        return exitCode;
    }

    static int Play(GuiScenarioInfo scenario, double speed, bool hold, bool keepGoing, CancellationTokenSource stop)
    {
        // A throw-away settings file, gone when the run ends: the app's defaults (the napkin sheet
        // and carpenter's pencil, the bench title bar on macOS), never the person's own settings.
        string settingsDir = Path.Combine(Path.GetTempPath(), "napkin-demo-settings-" + Guid.NewGuid().ToString("N"));
        var window = new MainWindow(new SettingsStore(Path.Combine(settingsDir, SettingsStore.FileName)))
        {
            Width = GuiWorkflow.DefaultWindowSize.Width * 1.2,
            Height = GuiWorkflow.DefaultWindowSize.Height * 1.2,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
            Title = $"napkin · live scenario {scenario.Id}",
        };

        // Popups are drawn in the window (OverlayPopups), so a tooltip left open by the last item the
        // pointer glided over would sit on top of the next one and eat its click. A real tooltip is
        // a separate window the pointer never lands on; make these the same.
        window.Styles.Add(new Avalonia.Styling.Style(selector => Avalonia.Styling.Selectors.OfType<Avalonia.Controls.ToolTip>(selector))
        {
            Setters = { new Avalonia.Styling.Setter(Avalonia.Input.InputElement.IsHitTestVisibleProperty, false) },
        });

        window.Closed += (_, _) =>
        {
            if (Directory.Exists(settingsDir))
            {
                Directory.Delete(settingsDir, recursive: true);
            }

            stop.Cancel();
        };

        window.Show();
        window.Activate();
        Settle(TimeSpan.FromMilliseconds(800));

        LiveOverlay overlay = LiveOverlay.AttachTo(window, scenario.Id);
        var driver = new LiveDriver(window, overlay, speed, keepGoing);
        Console.WriteLine($"{scenario.Id}: {scenario.Title}");
        bool closedEarly = false;
        try
        {
            scenario.Body(driver);
        }
        catch (ScenarioStoppedException)
        {
            // The failed expectation is already on screen and in the log.
        }
        catch (OperationCanceledException) when (driver.WindowClosed)
        {
            closedEarly = true;
        }
        catch (Exception exception)
        {
            driver.Fail(exception);
        }

        if (closedEarly)
        {
            Console.Error.WriteLine("napkin-demo: the window was closed before the scenario finished.");
            return LivePacing.ExitCode(0, infrastructureError: true);
        }

        bool passed = driver.Failures == 0;
        string summary = passed
            ? $"Done: {driver.Passed} expectations passed."
            : $"Failed: {driver.Failures} failure(s), {driver.Passed} passed.";
        Console.WriteLine(summary);
        overlay.Finish(summary + (hold ? " The window is yours; close it to end." : ""), passed);

        if (!hold)
        {
            DispatcherTimer.RunOnce(window.Close, TimeSpan.FromSeconds(passed ? 2.5 : 6));
        }

        return LivePacing.ExitCode(driver.Failures, infrastructureError: false);
    }

    static void Settle(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, duration);
        Dispatcher.UIThread.PushFrame(frame);
    }
}

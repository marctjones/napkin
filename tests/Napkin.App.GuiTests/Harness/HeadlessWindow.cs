using Avalonia.Headless;
using Avalonia.Threading;

namespace Napkin.App.GuiTests.Harness;

/// <summary>
/// Opens a real <see cref="MainWindow"/> on the headless dispatcher for a test that is not a
/// workflow.
/// </summary>
/// <remarks>
/// <para>
/// A <em>workflow</em> drives the window with simulated input and is held to
/// <see cref="WorkflowRule"/>; this is for the handful of questions that are about the window's own
/// API rather than about a person using it — what happens when the file picker is cancelled, or
/// when it throws. Those have no gesture: the platform's dialog is native, so there is nothing for
/// a headless test to click, and the honest thing is to say so rather than to dress a method call
/// up as input.
/// </para>
/// <para>
/// Nothing here records a feature id or touches <c>artifacts/gui-metrics.json</c>, so the GUI
/// ratchet cannot be moved by a test that never simulated anything.
/// </para>
/// </remarks>
public static class HeadlessWindow
{
    /// <summary>Shows a window, runs the body against it, and closes it.</summary>
    public static void Run(Action<MainWindow> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        HeadlessUnitTestSession session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessWindow).Assembly);

        session.Dispatch(
            () =>
            {
                MainWindow window = new()
                {
                    Width = GuiWorkflow.DefaultWindowSize.Width,
                    Height = GuiWorkflow.DefaultWindowSize.Height,
                };

                window.Show();
                Settle();

                try
                {
                    body(window);
                }
                finally
                {
                    window.Close();
                }

                return true;
            },
            CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Lets layout, rendering and queued dispatcher work finish.</summary>
    public static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Dispatcher.UIThread.RunJobs();
    }
}

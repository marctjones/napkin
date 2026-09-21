using Napkin.App.Designs;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// What <em>File &#x2192; Open&#x2026;</em> does when the dialog itself misbehaves.
/// </summary>
/// <remarks>
/// The rest of the Open path — the shortcut, the menu, the refusal panel, dismissing it and opening
/// a good file afterwards — is driven with real input by <c>GUI-VIEW-05</c>. These two cases have
/// no gesture behind them: cancelling is the <em>absence</em> of a choice in a native dialog the
/// headless platform cannot show, and a picker that throws is a platform fault, not something a
/// person can do. They are asserted against the real window all the same, so "never throws, never
/// crashes, never disturbs the drawing" holds for them too.
/// </remarks>
public class OpenFileTests
{
    [Fact]
    public void Cancelling_the_picker_changes_nothing()
    {
        HeadlessWindow.Run(window =>
        {
            Design? opened = window.CurrentDesign;
            ViewTransform view = window.Canvas.View;
            string? title = window.Title;
            string? status = window.DesignReadout.Text;

            window.FilePicker = new StubPicker(null);
            Open(window);

            Assert.Same(opened, window.CurrentDesign);
            Assert.Equal(view, window.Canvas.View);
            Assert.Equal(title, window.Title);
            Assert.Equal(status, window.DesignReadout.Text);
            Assert.False(window.IsRefusalShowing);
        });
    }

    [Fact]
    public void A_picker_that_fails_says_so_instead_of_taking_the_application_down()
    {
        HeadlessWindow.Run(window =>
        {
            Design? opened = window.CurrentDesign;
            ViewTransform view = window.Canvas.View;

            window.FilePicker = new FailingPicker("the file dialog is not available");
            Open(window);

            Assert.True(window.IsRefusalShowing);
            Assert.Contains(
                "the file dialog is not available",
                Assert.Single(window.RefusalProblems),
                StringComparison.Ordinal);
            Assert.Same(opened, window.CurrentDesign);
            Assert.Equal(view, window.Canvas.View);

            window.DismissRefusal();
            Assert.False(window.IsRefusalShowing);
            Assert.Empty(window.RefusalProblems);
        });
    }

    [Fact]
    public void A_picker_that_answers_with_a_file_opens_it()
    {
        HeadlessWindow.Run(window =>
        {
            string path = BadScenes.Write("picked.scene.json", BadScenes.Good);

            window.FilePicker = new StubPicker(path);
            Open(window);

            Assert.False(window.IsRefusalShowing);
            Assert.Equal("picked.scene.json", window.CurrentDesign?.Name);
            Assert.Equal("napkin — picked.scene.json", window.Title);
            Assert.Contains(
                "picked.scene.json",
                window.DesignReadout.Text!,
                StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Runs the open command to completion. The stub pickers answer synchronously, so the task is
    /// already finished by the time it is awaited and nothing here can deadlock on the dispatcher.
    /// </summary>
    static void Open(MainWindow window)
    {
        window.OpenFileAsync().GetAwaiter().GetResult();
        HeadlessWindow.Settle();
    }

    sealed class StubPicker(string? path) : ISceneFilePicker
    {
        public Task<string?> PickSceneFileAsync() => Task.FromResult(path);
    }

    sealed class FailingPicker(string message) : ISceneFilePicker
    {
        public Task<string?> PickSceneFileAsync() => throw new InvalidOperationException(message);
    }
}

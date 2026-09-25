using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Napkin.App.Designs;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Modules.Furniture;
using Napkin.Modules.Editing;
using Design = Napkin.Modules.Editing.Design;

namespace Napkin.App;

public partial class MainWindow
{
    // ---------------------------------------------------------------------------------------
    // Saving, and not losing what has not been saved
    // ---------------------------------------------------------------------------------------

    /// <summary>The <em>File</em> menu.</summary>
    public MenuItem FileMenuItem => FileMenu;

    /// <summary>The <em>File &#x2192; Exit</em> item.</summary>
    public MenuItem ExitMenuEntry => ExitMenuItem;

    /// <summary>The <em>File &#x2192; Save</em> item.</summary>
    public MenuItem SaveMenuEntry => SaveMenuItem;

    /// <summary>The <em>File &#x2192; Save As&#x2026;</em> item.</summary>
    public MenuItem SaveAsMenuEntry => SaveAsMenuItem;

    /// <summary>
    /// The scene file a plain Save writes to: the file that was opened, or the last one saved to.
    /// Null for a new sheet or a sample that has not been saved anywhere yet.
    /// </summary>
    public string? DocumentPath => _documentPath;

    /// <summary>Whether the drawing has changed since it was opened or last saved.</summary>
    public bool HasUnsavedChanges => Editor.HasUnsavedChanges;

    /// <summary>Whether the window is asking whether to save before it throws changes away.</summary>
    public bool IsAskingToSave => UnsavedBackdrop.IsVisible;

    /// <summary>What the save question says, or empty when it is not being asked.</summary>
    public string SaveQuestionText => IsAskingToSave ? UnsavedHeadline.Text ?? string.Empty : string.Empty;

    /// <summary>The question's Save button: save, then carry on.</summary>
    public Button SaveChangesButton => UnsavedSaveButton;

    /// <summary>The question's Don't save button: carry on, and lose the changes.</summary>
    public Button DiscardChangesButton => UnsavedDiscardButton;

    /// <summary>The question's Cancel button: do neither, and go back to the drawing.</summary>
    public Button KeepEditingButton => UnsavedCancelButton;

    /// <summary>
    /// Saves the drawing to the file it came from or was last saved to; a drawing with no such
    /// file asks where to put it, as <see cref="SaveAsAsync"/> does.
    /// </summary>
    /// <returns>Whether the drawing was written.</returns>
    public Task<bool> SaveAsync() =>
        _documentPath is { } path ? Task.FromResult(SaveTo(path)) : SaveAsAsync();

    /// <summary>
    /// Asks where to save the drawing, writes it there, and makes that the file a plain Save
    /// writes to from then on.
    /// </summary>
    /// <remarks>
    /// Cancelling writes nothing and says so. A picker that throws is reported like any other
    /// failure to save: the one thing this must never do is lose the drawing or the application.
    /// </remarks>
    /// <returns>Whether the drawing was written.</returns>
    public async Task<bool> SaveAsAsync()
    {
        if (_saving || IsAskingToSave || RefusedMidGesture())
        {
            return false;
        }

        _saving = true;
        try
        {
            string? path;
            try
            {
                path = await FilePicker.PickSaveDestinationAsync(SuggestedFileName()).ConfigureAwait(true);
            }
            catch (Exception exception) when (ProjectFile.IsFileException(exception))
            {
                Editor.Say(EditSeverity.Problem, $"Not saved: {exception.Message}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                Editor.Say(EditSeverity.Hint, "Not saved — no file was chosen, so nothing was written.");
                return false;
            }

            return SaveTo(path);
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>Writes the drawing to a file, and says what happened.</summary>
    bool SaveTo(string path)
    {
        if (IsAskingToSave || RefusedMidGesture())
        {
            return false;
        }

        switch (SceneFileSaver.Save(path, Editor.Sketch))
        {
            case SceneSaved saved:
                Editor.MarkSaved();
                _documentPath = saved.Path;
                string fileName = System.IO.Path.GetFileName(saved.Path);
                DesignText.Text = $"{fileName} — {saved.Path}";
                UpdateTitle();
                Editor.Say(
                    EditSeverity.Done,
                    $"Saved {fileName} in {System.IO.Path.GetDirectoryName(saved.Path)}.");
                return true;

            case SceneNotSaved refused:
                Editor.Say(EditSeverity.Problem, "Not saved. " + string.Join(" ", refused.Problems));
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Whether a drag is still under way, and so there is no finished drawing to save yet — said
    /// in the message bar rather than silently ignored.
    /// </summary>
    bool RefusedMidGesture()
    {
        if (!Editor.InGesture)
        {
            return false;
        }

        Editor.Say(EditSeverity.Hint, "Not saved — finish the drag first, then save.");
        return true;
    }

    /// <summary>What Save As offers to call the file: the file it already has, or the design's name.</summary>
    string SuggestedFileName()
    {
        if (_documentPath is { } path)
        {
            return System.IO.Path.GetFileName(path);
        }

        string name = Editor.Design.Name;
        return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : name + ".scene.json";
    }

    /// <summary>
    /// Does something that would throw the drawing's changes away — at once when there are none,
    /// and otherwise only once the person has said whether to save them first.
    /// </summary>
    /// <param name="doing">What is about to happen, for the question: "starting a new sheet".</param>
    /// <param name="then">What to do once it is safe to.</param>
    /// <returns>
    /// The work itself when it ran at once; a finished task when the question is waiting, because
    /// the work then runs when the person answers.
    /// </returns>
    Task WhenChangesAreSafe(string doing, Func<Task> then)
    {
        if (IsAskingToSave)
        {
            return Task.CompletedTask;
        }

        if (!Editor.HasUnsavedChanges)
        {
            return then();
        }

        // A half-typed dimension would sit under the question, out of reach; the question is
        // about the drawing, so the drawing is all that is left showing.
        CloseDimensionEditor(focusCanvas: false);

        UnsavedHeadline.Text = Design.SaveBeforeQuestion(DocumentName, doing);
        _afterAnswer = then;
        UnsavedBackdrop.IsVisible = true;
        UnsavedSaveButton.Focus();
        return Task.CompletedTask;
    }

    /// <summary>The question is answered Save: save, and carry on only if the save worked.</summary>
    async Task SaveThenCarryOnAsync()
    {
        Func<Task>? then = TakeAnswer();
        if (await SaveAsync().ConfigureAwait(true) && then is not null)
        {
            await then().ConfigureAwait(true);
        }
    }

    /// <summary>The question is answered Don't save: carry on, and the changes go.</summary>
    async Task DiscardThenCarryOnAsync()
    {
        if (TakeAnswer() is { } then)
        {
            await then().ConfigureAwait(true);
        }
    }

    /// <summary>The question is answered Cancel: nothing happens, and the drawing is as it was.</summary>
    void KeepEditing()
    {
        TakeAnswer();
        Editor.Say(EditSeverity.Hint, Design.SaveQuestionCancelled);
    }

    /// <summary>Takes the question off the screen and hands back what it was holding.</summary>
    Func<Task>? TakeAnswer()
    {
        Func<Task>? then = _afterAnswer;
        _afterAnswer = null;
        UnsavedBackdrop.IsVisible = false;
        FocusDrawing();
        return then;
    }

    /// <summary>Closes the window without asking again: the person has already answered.</summary>
    Task CloseNow()
    {
        _closeConfirmed = true;
        Close();
        return Task.CompletedTask;
    }

    static Task Now(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Closing the window from its title bar, or quitting the application, asks first when there
    /// are unsaved changes. A close the application asks for itself (<see cref="Window.Close()"/>
    /// in code) does not: every such call has either asked already — <em>File &#x2192; Exit</em>
    /// goes through the same question — or is a test harness tearing the window down.
    /// </remarks>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        bool personAsked = !e.IsProgrammatic || e.CloseReason == WindowCloseReason.ApplicationShutdown;
        if (personAsked && !_closeConfirmed && Editor.HasUnsavedChanges)
        {
            e.Cancel = true;
            _ = WhenChangesAreSafe("closing napkin", CloseNow);
        }

        base.OnClosing(e);
    }

    /// <summary>What the drawing is called in the title and the save question: its file, or its name.</summary>
    string DocumentName =>
        _documentPath is { } path ? System.IO.Path.GetFileName(path) : Editor.Design.Name;

    /// <summary>The title: the file's name, marked with an asterisk while there are unsaved changes.</summary>
    void UpdateTitle()
    {
        Title = Design.WindowTitle(DocumentName, Editor.HasUnsavedChanges);
        TitleBarText.Text = Title;
    }
}

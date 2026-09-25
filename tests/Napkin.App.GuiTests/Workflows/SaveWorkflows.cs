using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.Designs;
using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Core.Project;

using Xunit;
using Napkin.Modules.Editing;

using Design = Napkin.Modules.Editing.Design;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Saving a drawing, and not losing one that has not been saved.
/// </summary>
/// <remarks>
/// The platform's save and open dialogs are native, so a scripted picker stands in for them — the
/// same substitution <c>GUI-VIEW-05</c> makes for Open, and for the same reason: it is the one step
/// with no gesture a headless test can make. Everything else — the keys, the menus, the writer, the
/// reader, the question and its buttons — is the real thing, driven with real input.
/// </remarks>
public class SaveWorkflows
{
    [GuiWorkflow("GUI-DRAW-06")]
    public void Save_a_design_reopen_it_and_find_the_same_design() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        ScriptedFiles files = new();
        window.FilePicker = files;

        // Two parts, one of them with a typed width and the other pinned: parts, a dimension that
        // drives something, and relationships, which is everything a saved design has to carry.
        app.Chord(Key.N);
        EntityId first = DrawAPart(app, window, Point2.Inches(-20, -6), Point2.Inches(-4, 6));
        app.Click(LabelAt(window, first, SizeAxis.Width));
        app.Chord(Key.A);
        app.Type("1'-8 1/2\"");
        app.Press(Key.Enter);
        DrawAPart(app, window, Point2.Inches(6, -6), Point2.Inches(18, 6));
        app.Press(Key.P);

        app.Expect("the new sheet has unsaved changes, and the title says so", () =>
        {
            Assert.True(window.HasUnsavedChanges);
            Assert.Equal("napkin — Untitled*", window.Title);
            Assert.Null(window.DocumentPath);
            Assert.Equal(2, window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Count());
            Assert.NotEmpty(window.CurrentDesign!.Sketch.RelationshipsInOrder);
        });

        // Save: the sheet has never been saved, so it asks where, offering its own name.
        string path = BadScenes.MissingFile($"saved-{Guid.NewGuid():N}.scene.json");
        files.SaveAnswer = path;
        app.Chord(Key.S);
        Sketch saved = window.CurrentDesign!.Sketch;
        string fileName = Path.GetFileName(path);

        app.Expect("the design went to the file that was chosen, and the title is that file's, clean", () =>
        {
            Assert.Equal(["Untitled.scene.json"], files.SuggestedNames);
            Assert.True(File.Exists(path));
            Assert.Equal(Path.GetFullPath(path), window.DocumentPath);
            Assert.False(window.HasUnsavedChanges);
            Assert.Equal($"napkin — {fileName}", window.Title);
            Assert.StartsWith($"Saved {fileName}", window.MessageOnScreen, StringComparison.Ordinal);
        });

        // Close it — a new sheet, with nothing to ask because nothing is unsaved.
        app.Chord(Key.N);
        app.Expect("a new sheet replaced it at once, with no question", () =>
        {
            Assert.False(window.IsAskingToSave);
            Assert.Empty(window.CurrentDesign!.Sketch.Entities);
        });

        // Reopen it from the file dialog.
        files.OpenAnswer = path;
        app.Chord(Key.O);

        app.Expect("every part, dimension and relationship is what was saved", () =>
        {
            Assert.Equal(saved, window.CurrentDesign!.Sketch);
            Assert.Equal(Length.Inches(20, 1, 2).Units, window.CurrentDesign!.Sketch.Find<Box>(first)!.Width.Units);
            Assert.Equal(Path.GetFullPath(path), Path.GetFullPath(window.DocumentPath!));
            Assert.False(window.HasUnsavedChanges);
        });

        // Change it and Save again: straight back to the same file, without asking.
        app.Click(At(window, Part(window, first).Center.XY));
        app.Drag(At(window, Part(window, first).Center.XY), At(window, Part(window, first).Center.XY + new Vector2(Length.Zero, Length.Inches(4))));
        app.Expect("the move is an unsaved change", () => Assert.EndsWith("*", window.Title!, StringComparison.Ordinal));

        app.Click(CentreOf(window, window.FileMenuItem));
        app.Click(CentreOf(window, window.SaveMenuEntry));

        app.Expect("Save wrote to the file it came from, and did not ask", () =>
        {
            Assert.Single(files.SuggestedNames);
            Assert.False(window.HasUnsavedChanges);
            Assert.Equal(window.CurrentDesign!.Sketch, Assert.IsType<Loaded>(SceneReader.ReadFile(path)).Sketch);
        });

        // Save As somewhere else: that becomes the file.
        string copy = BadScenes.MissingFile($"copy-{Guid.NewGuid():N}.scene.json");
        files.SaveAnswer = copy;
        app.Chord(Key.S, KeyModifiers.Shift);

        app.Expect("Save As always asks, offers the current name, and moves the title to the new file", () =>
        {
            Assert.Equal(fileName, files.SuggestedNames[^1]);
            Assert.Equal(Path.GetFullPath(copy), window.DocumentPath);
            Assert.Equal($"napkin — {Path.GetFileName(copy)}", window.Title);
            Assert.Equal(window.CurrentDesign!.Sketch, Assert.IsType<Loaded>(SceneReader.ReadFile(copy)).Sketch);
        });

        app.SaveFrame("saved-as");
    });

    /// <summary>
    /// The question asked before unsaved changes are thrown away — by a new sheet, a sample,
    /// another file, or quitting — and each of its three answers.
    /// </summary>
    /// <remarks>
    /// Claims <c>GUI-SHELL-05</c>, which no feature list defines: nothing written down yet says
    /// "napkin asks before it loses your work", and adding an entry is not this change's call, so
    /// the claim shows as an orphan on the scorecard — which gates nothing — until somebody writes
    /// the feature down.
    /// </remarks>
    [GuiWorkflow("GUI-SHELL-05")]
    public void Unsaved_changes_are_asked_about_before_they_are_lost() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        ScriptedFiles files = new();
        window.FilePicker = files;

        app.Chord(Key.N);
        EntityId drawn = DrawAPart(app, window, Point2.Inches(-10, -5), Point2.Inches(10, 5));
        Design edited = window.CurrentDesign!;

        // New, with a part nobody has saved: the question, not a blank sheet.
        app.Chord(Key.N);
        app.Expect("New asks first, and the drawing is still there under the question", () =>
        {
            Assert.True(window.IsAskingToSave);
            Assert.Equal("Save the changes to Untitled before starting a new sheet?", window.SaveQuestionText);
            Assert.Same(edited, window.CurrentDesign);
        });

        // The drawing under the question cannot be touched: a press on the part goes to the
        // backdrop, and so do keys.
        app.Click(At(window, Part(window, drawn).Center.XY));
        app.Press(Key.Delete);
        app.Expect("nothing behind the question moved or went", () =>
        {
            Assert.True(window.IsAskingToSave);
            Assert.Same(edited, window.CurrentDesign);
        });

        app.Press(Key.Escape);
        app.Expect("Escape is Cancel: the question goes and the drawing is as it was", () =>
        {
            Assert.False(window.IsAskingToSave);
            Assert.Same(edited, window.CurrentDesign);
            Assert.True(window.HasUnsavedChanges);
            Assert.Equal("Nothing was saved and nothing was thrown away.", window.MessageOnScreen);
        });

        // A sample, from the menu with the pointer: asked again, and this time Don't save.
        IDesignSource sample = window.Samples[0];
        OpenSample(app, window, sample.Name);
        app.Expect("opening a sample asks too", () =>
            Assert.Equal($"Save the changes to Untitled before opening {sample.Name}?", window.SaveQuestionText));

        app.Click(CentreOf(window, window.DiscardChangesButton));
        app.Expect("Don't save opens the sample, and the part that was drawn is gone", () =>
        {
            Assert.False(window.IsAskingToSave);
            Assert.Equal(sample.Name, window.CurrentDesign!.Name);
            Assert.Null(window.CurrentDesign!.Sketch.Find(drawn));
            Assert.False(window.HasUnsavedChanges);

            // A sample is not somewhere Save writes back to: it ships with napkin.
            Assert.Null(window.DocumentPath);
        });

        // Edit the sample, then Open another file and answer Save: the sample goes to a new file
        // of its own — it asks where — and only then does the other file open.
        app.Press(Key.R);
        app.Drag(new Point(90, 470), new Point(130, 500), new Point(170, 520));
        Sketch sampleEdited = window.CurrentDesign!.Sketch;
        string keep = BadScenes.MissingFile($"edited-sample-{Guid.NewGuid():N}.scene.json");
        string other = BadScenes.Write($"other-{Guid.NewGuid():N}.scene.json", BadScenes.Good);
        files.SaveAnswer = keep;
        files.OpenAnswer = other;

        app.Chord(Key.O);
        app.Expect("Open asks, naming the sample", () =>
            Assert.Equal($"Save the changes to {sample.Name} before opening another file?", window.SaveQuestionText));

        app.Press(Key.Enter);
        app.Expect("Save, the focused answer, saved the edited sample to a file of its own and then opened the other file", () =>
        {
            Assert.False(window.IsAskingToSave);
            Assert.Equal(sampleEdited, Assert.IsType<Loaded>(SceneReader.ReadFile(keep)).Sketch);
            Assert.Equal(Path.GetFullPath(other), Path.GetFullPath(window.DocumentPath!));
            Assert.Equal(Path.GetFileName(other), window.CurrentDesign!.Name);
            Assert.False(window.HasUnsavedChanges);
        });

        // Edit that, then File → Exit, and think better of it.
        app.Press(Key.R);
        app.Drag(new Point(90, 470), new Point(130, 500), new Point(170, 520));
        Design beforeExit = window.CurrentDesign!;
        app.Click(CentreOf(window, window.FileMenuItem));
        app.Click(CentreOf(window, window.ExitMenuEntry));

        app.Expect("Exit asks before quitting", () =>
            Assert.Equal($"Save the changes to {Path.GetFileName(other)} before quitting?", window.SaveQuestionText));

        app.Click(CentreOf(window, window.KeepEditingButton));
        app.Expect("Cancel keeps the window open with the edit in it", () =>
        {
            Assert.True(window.IsVisible);
            Assert.False(window.IsAskingToSave);
            Assert.Same(beforeExit, window.CurrentDesign);
            Assert.EndsWith("*", window.Title!, StringComparison.Ordinal);
        });

        app.SaveFrame("kept-editing");
    });

    /// <summary>A picker that answers at once with whatever the workflow put in it.</summary>
    sealed class ScriptedFiles : ISceneFilePicker
    {
        public string? OpenAnswer { get; set; }

        public string? SaveAnswer { get; set; }

        public List<string> SuggestedNames { get; } = [];

        public Task<string?> PickSceneFileAsync() => Task.FromResult(OpenAnswer);

        public Task<string?> PickSaveDestinationAsync(string suggestedName)
        {
            SuggestedNames.Add(suggestedName);
            return Task.FromResult(SaveAnswer);
        }
    }

    /// <summary>Draws one part with the pointer, and gives back its id.</summary>
    static EntityId DrawAPart(AppDriver app, MainWindow window, Point2 from, Point2 to)
    {
        HashSet<EntityId> before = [.. window.CurrentDesign!.Sketch.Entities.Keys];
        app.Press(Key.R);
        app.Drag(At(window, from), At(window, to));

        return window.CurrentDesign!.Sketch.Entities.Values
            .OfType<Box>()
            .Single(box => !before.Contains(box.Id))
            .Id;
    }

    /// <summary>Opens a sample through the Samples menu, with the mouse.</summary>
    static void OpenSample(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.SamplesMenuItem));

        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    static Box Part(MainWindow window, EntityId id) =>
        window.CurrentDesign!.Sketch.Find<Box>(id)
        ?? throw new InvalidOperationException($"{id} is not in the drawing.");

    /// <summary>The window coordinate a model point is drawn at.</summary>
    static Point At(MainWindow window, Point2 world) =>
        InWindow(window, window.Canvas.View.ToScreen(world));

    /// <summary>The window coordinate of a selected part's dimension label.</summary>
    static Point LabelAt(MainWindow window, EntityId box, SizeAxis axis) =>
        InWindow(window, window.Canvas.SelectionDimensionLabelAt(box, axis)
            ?? throw new InvalidOperationException($"{box} shows no {axis} dimension."));

    static Point InWindow(MainWindow window, Point onCanvas)
    {
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        Size size = control.Bounds.Size;
        return topLeft + new Point(size.Width / 2, size.Height / 2);
    }
}

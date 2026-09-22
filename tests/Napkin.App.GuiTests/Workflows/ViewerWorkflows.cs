using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Napkin.App;
using Napkin.App.Designs;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The M1 viewer, driven the way a person drives it: open a design, move around it, read what the
/// drawing says, and be told plainly when a file cannot be opened.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The deviations these workflows used to carry are gone.</strong> They opened samples that
/// the viewer built in code and stated their expected dimension strings inline, because the scene
/// reader (#6) and the sample files (#37) were being written in parallel with the viewer. Both have
/// landed: the samples are the files in <c>samples/</c>, shipped inside the build, and the expected
/// strings are read from each fixture's <c>*.expected.json</c> — which is what GUI-VIEW-04's
/// acceptance sentence asked for all along.
/// </para>
/// <para>
/// <strong>The one thing still substituted is the dialog itself.</strong> A native open dialog
/// cannot be driven headless (<c>docs/testing/gui-automation.md</c>, "What headless cannot cover"),
/// so a workflow puts a picker that answers with a path behind
/// <see cref="MainWindow.FilePicker"/> and then presses the real shortcut. Everything after that —
/// the key binding, the command, the reader, the refusal panel, the canvas — is the shipped code.
/// Two load paths are exercised between them: <see cref="Open_a_design_from_a_file_and_move_around_it"/>
/// goes through <em>File &#x2192; Open&#x2026;</em>, and the others through the Samples menu, with
/// the mouse.
/// </para>
/// </remarks>
public class ViewerWorkflows
{
    [GuiWorkflow("GUI-VIEW-01")]
    public void Open_a_design_from_a_file_and_move_around_it() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;
        SampleExpectations expected = SampleExpectations.For("coffee-table");

        // Open the wall first, through the menu, so that opening the coffee table afterwards is
        // really an open and not just "what was already on screen".
        OpenThroughTheMenu(app, window, "Wall with window");
        app.Expect("the wall sample is on screen", () =>
        {
            Assert.Equal("Wall with window", window.CurrentDesign?.Name);
            Assert.Equal("napkin — Wall with window", window.Title);
        });

        // Now the coffee table, the way a person opens a file they were sent: the shortcut, the
        // dialog, the file.
        OpenThroughTheFileDialog(app, window, SampleExpectations.SceneFile("coffee-table"));

        app.Expect("every part of the coffee table is drawn where the fixture says it is", () =>
        {
            Design design = window.CurrentDesign!;
            Assert.Equal("coffee-table.scene.json", design.Name);
            Assert.Equal("napkin — coffee-table.scene.json", window.Title);
            Assert.Contains(
                "coffee-table.scene.json",
                window.DesignReadout.Text!,
                StringComparison.Ordinal);

            Assert.Equal(
                expected.Counts.Boxes,
                design.Sketch.Entities.Values.OfType<Box>().Count());
            foreach (ExpectedBox part in expected.Boxes)
            {
                AssertPartDrawn(window, design, part);
            }
        });

        Sketch asOpened = window.CurrentDesign!.Sketch;
        app.Chord(Key.D0);
        ViewTransform fitted = canvas.View;
        Point2 under = fitted.ToWorld(OnCanvas(window, new Point(450, 300)));

        app.Wheel(new Point(450, 300), new Vector(0, 3));
        app.Expect("the wheel zoomed in about the pointer", () =>
        {
            Assert.True(canvas.View.PixelsPerInch > fitted.PixelsPerInch);
            Point after = canvas.View.ToScreen(under);
            Point wanted = OnCanvas(window, new Point(450, 300));
            Assert.True(
                Math.Abs(after.X - wanted.X) < 1 && Math.Abs(after.Y - wanted.Y) < 1,
                $"the point under the pointer moved to {after}, not {wanted}.");
        });

        app.Drag(new Point(450, 300), new Point(500, 340), new Point(540, 380));
        app.Press(Key.Left);
        app.Expect("moving around the drawing moved nothing in it", () =>
        {
            Assert.Same(asOpened, window.CurrentDesign!.Sketch);
            Assert.Equal(FreshlyRead("coffee-table"), window.CurrentDesign!.Sketch);
            Assert.Matches(@"^Zoom \d+(\.\d)?%$", window.ZoomReadout.Text!);
        });

        app.SaveFrame("coffee-table");
    });

    [GuiWorkflow("GUI-VIEW-02")]
    public void Pan_with_the_wheel_a_drag_and_the_keyboard() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        app.Chord(Key.D1);
        app.Chord(Key.D0);

        Sketch asOpened = window.CurrentDesign!.Sketch;
        ViewTransform fitted = canvas.View;
        Point probe = OnCanvas(window, new Point(450, 300));

        app.Wheel(new Point(450, 300), new Vector(0, 2), KeyModifiers.Shift);
        app.Expect("the wheel panned, at the same zoom, and moved nothing in the design", () =>
        {
            Assert.Equal(fitted.PixelsPerInch, canvas.View.PixelsPerInch, 9);
            Assert.NotEqual(fitted.CenterYInches, canvas.View.CenterYInches);
            AssertNothingMoved(window, asOpened);
        });

        ViewTransform beforeDrag = canvas.View;
        Point2 under = beforeDrag.ToWorld(probe);
        app.Drag(new Point(450, 300), new Point(500, 330), new Point(560, 360));
        app.Expect("the drag moved the drawing by exactly the drag, not by more", () =>
        {
            // The model point that was under the pointer is under it still, 110 across and 60
            // down from where the drag started.
            Point expected = OnCanvas(window, new Point(560, 360));
            Point actual = canvas.View.ToScreen(under);
            Assert.True(
                Math.Abs(actual.X - expected.X) < 1 && Math.Abs(actual.Y - expected.Y) < 1,
                $"the point dragged to {actual}, not to {expected}.");
            AssertNothingMoved(window, asOpened);
        });

        ViewTransform beforeKeys = canvas.View;
        app.Press(Key.Left);
        app.Press(Key.Up);
        app.Expect("the arrow keys panned by a tenth of the viewport each", () =>
        {
            double width = canvas.View.Viewport.Width;
            double height = canvas.View.Viewport.Height;
            Point wasAt = beforeKeys.ToScreen(under);
            Point isAt = canvas.View.ToScreen(under);

            // Left moves the view left, so the drawing moves right; up moves the view up, so the
            // drawing moves down. Each by a tenth of the viewport.
            Assert.Equal(wasAt.X + (0.1 * width), isAt.X, 6);
            Assert.Equal(wasAt.Y + (0.1 * height), isAt.Y, 6);
            Assert.Equal(beforeKeys.PixelsPerInch, canvas.View.PixelsPerInch, 9);
            AssertNothingMoved(window, asOpened);
        });

        app.MoveTo(new Point(300, 250));
        app.Expect("the status line reports where the pointer is, in feet and inches", () =>
        {
            Assert.Matches(@"^x .*[""'].*y .*[""']$", window.CursorReadout.Text!);
            Assert.DoesNotContain("—", window.CursorReadout.Text!, StringComparison.Ordinal);
        });

        app.SaveFrame("panned");
    });

    [GuiWorkflow("GUI-VIEW-03")]
    public void Zoom_about_the_cursor_then_zoom_to_fit() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        app.Chord(Key.D1);
        app.Chord(Key.D0);

        // A corner of the table, rather than the middle of the viewport, so that zooming in about
        // it really does push the rest of the drawing out of sight. The top is 48" x 24", anchored
        // at the origin (samples/coffee-table.expected.json).
        SampleExpectations expected = SampleExpectations.For("coffee-table");
        ExpectedBox top = expected.Box("Top");
        Point2 corner = new(
            new Length(top.AnchorXUnits + top.WidthUnits),
            new Length(top.AnchorYUnits + top.HeightUnits));
        Point onCanvas = canvas.View.ToScreen(corner);
        Point inWindow = InWindow(window, onCanvas);
        ViewTransform fitted = canvas.View;

        app.Wheel(inWindow, new Vector(0, 8));
        app.Expect("the model point under the pointer is still under the pointer", () =>
        {
            Assert.True(canvas.View.PixelsPerInch > fitted.PixelsPerInch * 2);
            Point after = canvas.View.ToScreen(corner);
            Assert.True(
                Math.Abs(after.X - onCanvas.X) < 1 && Math.Abs(after.Y - onCanvas.Y) < 1,
                $"the corner moved from {onCanvas} to {after} while zooming about it.");
        });

        app.Expect("most of the drawing has left the viewport", () =>
            Assert.False(IsWhollyVisible(canvas), "the whole design still fits after zooming in."));

        app.MoveTo(new Point(500, 300));
        app.Press(Key.Right);
        app.Chord(Key.D0);
        app.Expect("the fit frames every entity again, with a margin", () =>
        {
            Assert.True(IsWhollyVisible(canvas), "something is still outside the viewport.");
            AssertMarginAllRound(canvas);
        });

        app.Expect("the zoom readout followed the view back out", () =>
            Assert.Equal(fitted.PixelsPerInch, canvas.View.PixelsPerInch, 6));

        app.SaveFrame("fitted-again");
    });

    [GuiWorkflow("GUI-VIEW-04")]
    public void Read_dimension_labels_in_feet_inches_and_fractions() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;
        SampleExpectations expected = SampleExpectations.For("wall-with-window");

        OpenThroughTheMenu(app, window, "Wall with window");
        app.Chord(Key.D0);

        app.Expect("every dimension reads the string the fixture's expectations file gives", () =>
        {
            Dictionary<string, string> labels = LabelsByName(window, expected);
            Assert.Equal(expected.DimensionLabels.Count, labels.Count);
            foreach (ExpectedLabel label in expected.DimensionLabels)
            {
                Assert.Equal(label.Text, labels[label.Name]);
            }
        });

        app.SaveFrame("wall-fitted");

        // Zoom in on the opening, about its own centre, the way a person checks a rough opening.
        ExpectedBox opening = expected.Box("Opening");
        Point2 middleOfOpening = new(
            new Length(opening.AnchorXUnits + (opening.WidthUnits / 2)),
            new Length(opening.AnchorYUnits + (opening.HeightUnits / 2)));
        app.Wheel(InWindow(window, canvas.View.ToScreen(middleOfOpening)), new Vector(0, 6));
        app.Press(Key.Up);

        ExpectedLabel openingWidth = expected.Label("Opening width");
        app.Expect("zoomed in, the opening's dimension still reads its own number and is on screen", () =>
        {
            Assert.Equal(openingWidth.Text, LabelsByName(window, expected)["Opening width"]);

            DimensionMeasurement measured = Measurement(window, openingWidth);
            Point label = canvas.View.ToScreen(measured.LabelAnchor);
            Assert.True(
                new Rect(canvas.Bounds.Size).Contains(label),
                $"the {openingWidth.Text} label is drawn at {label}, outside the canvas.");
        });

        app.Expect("the labels are the value the fixture states, through Core.Geometry's Format", () =>
        {
            foreach (ExpectedLabel label in expected.DimensionLabels)
            {
                DimensionMeasurement measured = Measurement(window, label);

                // The dimension stores no number: what it reads is worked out from the geometry,
                // and it has to come to the units the fixture's arithmetic says.
                Assert.Equal(label.ValueUnits, measured.Value.Units);
                Assert.Equal(
                    new Length(label.ValueUnits).Format(canvas.LabelFormat).Text,
                    measured.Label(canvas.LabelFormat));
                Assert.True(
                    measured.Format(canvas.LabelFormat).IsExact,
                    $"{label.Name} does not display exactly at this precision.");
            }
        });

        app.SaveFrame("wall-with-window");
    });

    [GuiWorkflow("GUI-VIEW-05")]
    public void A_file_the_app_cannot_read_fails_visibly_and_leaves_the_drawing_alone() =>
        GuiWorkflow.Run(app =>
        {
            MainWindow window = (MainWindow)app.Target;
            CanvasView canvas = window.Canvas;

            // Something is open and the view has been moved off its opening position, so that
            // "untouched" means more than "nothing had happened yet".
            app.Chord(Key.D1);
            app.Chord(Key.D0);
            app.Drag(new Point(430, 300), new Point(480, 330), new Point(520, 350));

            Design opened = window.CurrentDesign!;
            Sketch asOpened = opened.Sketch;
            ViewTransform asFramed = canvas.View;
            string? title = window.Title;
            string? status = window.DesignReadout.Text;

            string several = BadScenes.Write("several-faults.scene.json", BadScenes.SeveralFaults);
            OpenThroughTheFileDialog(app, window, several);

            app.Expect("the refusal names every problem, and the drawing is untouched", () =>
            {
                Assert.True(window.IsRefusalShowing, "nothing was shown for a file that was refused.");
                Assert.Contains(
                    "several-faults.scene.json",
                    window.RefusalHeadlineText,
                    StringComparison.Ordinal);

                // Every problem, not the first: the fixture has three faults of a kind the reader
                // finds in one pass (see BadScenes), and all three have to be readable at once.
                Assert.True(
                    window.RefusalProblems.Count >= 3,
                    $"only {window.RefusalProblems.Count} problem(s) were shown: "
                    + string.Join(" | ", window.RefusalProblems));

                IReadOnlyList<string> onScreen = TextOnThePanel(window);
                Assert.All(
                    window.RefusalProblems,
                    problem => Assert.Contains(problem, onScreen));

                AssertUntouched(window, opened, asOpened, asFramed, title, status);
            });

            app.Press(Key.Escape);
            app.Expect("Escape takes the message away and still nothing has changed", () =>
            {
                Assert.False(window.IsRefusalShowing);
                Assert.Empty(window.RefusalProblems);
                AssertUntouched(window, opened, asOpened, asFramed, title, status);
            });

            // The second of the three faults the catalogue names: a relationship kind the format
            // defines but this build's updater cannot hold.
            string unsupported = BadScenes.Write(
                "solver-only.scene.json",
                BadScenes.UnsupportedRelationshipKind);
            OpenThroughTheFileDialog(app, window, unsupported);
            app.Expect("a relationship kind this build cannot hold is refused, and named", () =>
            {
                Assert.True(window.IsRefusalShowing);
                Assert.Contains(
                    "distance",
                    string.Join(" ", window.RefusalProblems),
                    StringComparison.OrdinalIgnoreCase);
                AssertUntouched(window, opened, asOpened, asFramed, title, status);
            });

            app.Press(Key.Escape);

            // And the third, dismissed with the mouse this time.
            string dangling = BadScenes.Write("dangling.scene.json", BadScenes.DanglingReference);
            OpenThroughTheFileDialog(app, window, dangling);
            app.Expect("a dangling id is refused too, and named", () =>
            {
                Assert.True(window.IsRefusalShowing);
                Assert.Contains(
                    "0192f1a0-0000-4000-8000-0000000000ff",
                    string.Join(" ", window.RefusalProblems),
                    StringComparison.OrdinalIgnoreCase);
                AssertUntouched(window, opened, asOpened, asFramed, title, status);
            });

            app.SaveFrame("refused");
            app.Click(CentreOf(window, window.Refusal));
            app.Expect("clicking the message dismisses it, and the drawing is still there", () =>
            {
                Assert.False(window.IsRefusalShowing);
                AssertUntouched(window, opened, asOpened, asFramed, title, status);
            });

            // And a file that is good replaces the design, framed, with its name on the window.
            OpenThroughTheFileDialog(app, window, SampleExpectations.SceneFile("wall-with-window"));
            app.Expect("a good file opens, replaces the drawing and frames it", () =>
            {
                Assert.False(window.IsRefusalShowing);
                Assert.Equal("wall-with-window.scene.json", window.CurrentDesign?.Name);
                Assert.Equal("napkin — wall-with-window.scene.json", window.Title);
                Assert.NotSame(asOpened, window.CurrentDesign!.Sketch);
                Assert.Equal(FreshlyRead("wall-with-window"), window.CurrentDesign!.Sketch);
                Assert.True(IsWhollyVisible(canvas), "the new drawing was not framed.");
            });

            app.SaveFrame("opened-after-a-refusal");
        });

    /// <summary>Opens a sample the way a person does: the Samples menu, with the mouse.</summary>
    static void OpenThroughTheMenu(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.SamplesMenuItem));

        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    /// <summary>
    /// Opens a file through <em>File &#x2192; Open&#x2026;</em>: the real shortcut, the real
    /// command, with a picker standing in for the platform's dialog.
    /// </summary>
    /// <remarks>
    /// Substituting the picker is not a shortcut around a gesture — it is the one thing in this
    /// path that has no gesture, because a headless test has no native dialog to click. The stub
    /// answers synchronously, so the command finishes inside the key press and the next verb sees
    /// the result.
    /// </remarks>
    static void OpenThroughTheFileDialog(AppDriver app, MainWindow window, string path)
    {
        window.FilePicker = new ScriptedPicker(path);
        app.Chord(Key.O);
    }

    static void AssertUntouched(
        MainWindow window,
        Design opened,
        Sketch asOpened,
        ViewTransform asFramed,
        string? title,
        string? status)
    {
        Assert.Same(opened, window.CurrentDesign);
        Assert.Same(asOpened, window.CurrentDesign!.Sketch);
        Assert.Equal(asFramed, window.Canvas.View);
        Assert.Equal(title, window.Title);
        Assert.Equal(status, window.DesignReadout.Text);
    }

    /// <summary>Every line of text the refusal panel is really showing.</summary>
    static IReadOnlyList<string> TextOnThePanel(MainWindow window) =>
    [
        .. window.Refusal
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty),
    ];

    static Point CentreOf(Visual root, Visual control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)
        ?? throw new InvalidOperationException("The control is not in the window's visual tree.");

    /// <summary>The canvas coordinate under a window coordinate.</summary>
    static Point OnCanvas(MainWindow window, Point inWindow)
    {
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(inWindow.X - origin.X, inWindow.Y - origin.Y);
    }

    /// <summary>The window coordinate of a canvas coordinate — where to send the pointer.</summary>
    static Point InWindow(MainWindow window, Point onCanvas)
    {
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    static void AssertPartDrawn(MainWindow window, Design design, ExpectedBox part)
    {
        Box box = design.Sketch.Find<Box>(part.EntityId)
            ?? throw new InvalidOperationException($"{design.Name} has no part called {part.Name}.");

        // Integer units, compared exactly against the arithmetic committed in the fixture.
        Assert.Equal(part.AnchorXUnits, box.Anchor.X.Units);
        Assert.Equal(part.AnchorYUnits, box.Anchor.Y.Units);
        Assert.Equal(part.WidthUnits, box.Width.Units);
        Assert.Equal(part.HeightUnits, box.Height.Units);

        ViewTransform view = window.Canvas.View;
        Rect drawn = new Rect(
            view.ToScreen(box.Corner(BoxCorner.SouthWest)),
            view.ToScreen(box.Corner(BoxCorner.NorthEast))).Normalize();

        Assert.True(
            new Rect(window.Canvas.Bounds.Size).Contains(drawn),
            $"{part.Name} is drawn at {drawn}, which is not inside the canvas.");
        Assert.True(drawn.Width > 0 && drawn.Height > 0, $"{part.Name} is drawn with no area.");
    }

    static void AssertNothingMoved(MainWindow window, Sketch asOpened)
    {
        // The sketch the canvas holds is still the one that was opened, value for value, and still
        // equal to a fresh read of the same file: no gesture has written to the model.
        Assert.Same(asOpened, window.CurrentDesign!.Sketch);
        Assert.Equal(FreshlyRead("coffee-table"), window.CurrentDesign!.Sketch);
    }

    /// <summary>The fixture as the reader produces it, read again from the shipped file.</summary>
    static Sketch FreshlyRead(string fixture)
    {
        LoadResult result = SceneReader.ReadFile(SampleExpectations.SceneFile(fixture));
        return Assert.IsType<Loaded>(result).Sketch;
    }

    static bool IsWhollyVisible(CanvasView canvas)
    {
        WorldBounds extents = canvas.Extents;
        ViewTransform view = canvas.View;
        Rect drawn = new Rect(
            view.ToScreen(new Point2(extents.MinX, extents.MinY)),
            view.ToScreen(new Point2(extents.MaxX, extents.MaxY))).Normalize();

        return new Rect(canvas.Bounds.Size).Contains(drawn);
    }

    static void AssertMarginAllRound(CanvasView canvas)
    {
        WorldBounds extents = canvas.Extents;
        ViewTransform view = canvas.View;
        Rect drawn = new Rect(
            view.ToScreen(new Point2(extents.MinX, extents.MinY)),
            view.ToScreen(new Point2(extents.MaxX, extents.MaxY))).Normalize();
        Rect viewport = new(canvas.Bounds.Size);

        Assert.True(drawn.Left > 1, $"no margin on the left: {drawn} in {viewport}.");
        Assert.True(drawn.Top > 1, $"no margin at the top: {drawn} in {viewport}.");
        Assert.True(viewport.Right - drawn.Right > 1, $"no margin on the right: {drawn}.");
        Assert.True(viewport.Bottom - drawn.Bottom > 1, $"no margin at the bottom: {drawn}.");
    }

    /// <summary>What every dimension on screen reads, keyed by the name the fixture gives it.</summary>
    static Dictionary<string, string> LabelsByName(MainWindow window, SampleExpectations expected)
    {
        Dictionary<EntityId, string> names = expected.DimensionLabels
            .ToDictionary(label => label.EntityId, label => label.Name);

        return window.Canvas.Measurements().ToDictionary(
            measurement => names.TryGetValue(measurement.Dimension.Id, out string? name)
                ? name
                : measurement.Dimension.Id.ToString(),
            measurement => measurement.Label(window.Canvas.LabelFormat));
    }

    static DimensionMeasurement Measurement(MainWindow window, ExpectedLabel label) =>
        window.Canvas.Measurements().Single(m => m.Dimension.Id == label.EntityId);

    /// <summary>A file picker that answers with the path a workflow chose, at once.</summary>
    sealed class ScriptedPicker(string path) : ISceneFilePicker
    {
        public Task<string?> PickSceneFileAsync() => Task.FromResult<string?>(path);

        public Task<string?> PickSaveDestinationAsync(string suggestedName) => Task.FromResult<string?>(null);
    }
}

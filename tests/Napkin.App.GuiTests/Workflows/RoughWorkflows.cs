using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Napkin.App;
using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;
using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Rough sketching (<c>docs/design/sketch-mode.md</c> &#xA7;7.4): a bench drawn fast with big round
/// steps and nothing stated, then firmed up, in napkin's default look.
/// </summary>
/// <remarks>
/// The quick bench of &#xA7;7.2 is drawn from an origin of whole inches near the middle of the canvas
/// (the canvas is not centred on (0, 0)): Top (0, 16) 48 &#xD7; 2, Leg 1 (2, 0) 4 &#xD7; 16, Leg 2 (42, 0),
/// Stretcher (6, 4) 36 &#xD7; 3, each from that origin.
/// A new sheet opens at about 10.5 px per inch, where the rough step is 6&#x2033;; five presses of +
/// (&#xD7;1.25 each) make it about 32 px per inch, where the precise step is 1/2&#x2033; (16 px) and the rough
/// step the rung above, floored to 1&#x2033;. The window is widened so all 48&#x2033; of the top is on it.
/// </remarks>
public class RoughWorkflows
{
    [GuiWorkflow("GUI-SKETCH-01")]
    public void Sketch_a_quick_bench() => GuiWorkflow.Run(
        app =>
        {
            MainWindow window = (MainWindow)app.Target;
            CanvasView canvas = window.Canvas;

            (long ox, long oy) = SketchTheBench(app, window);
            app.SaveFrame("rough-bench");

            // By name, the order they were drawn in: Part 1 the top, 2 and 3 the legs, 4 the stretcher.
            Box[] parts = Parts(window);
            app.Expect("every part landed on whole inches, is a plank marked rough, and nothing is stated", () =>
            {
                Assert.Equal(4, parts.Length);
                AssertBox(parts[0], ox + 0, oy + 16, 48, 2);
                AssertBox(parts[1], ox + 2, oy + 0, 4, 16);
                AssertBox(parts[2], ox + 42, oy + 0, 4, 16);
                AssertBox(parts[3], ox + 6, oy + 4, 36, 3);
                Assert.All(parts, part =>
                {
                    Assert.NotNull(part.Part);
                    Assert.True(part.Part!.Rough);
                    Assert.Null(part.Part.Stock);
                    Assert.Equal(Length.Inches(0, 3, 4), part.Depth);
                });
                Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder);
                Assert.True(canvas.LastSnap?.CaughtSomething, "moving Leg 1 up to the top caught nothing.");
            });

            // Pick Leg 1 and take the pointer well away: its labels wait. Over it, they show.
            app.Click(At(window, Point2.Inches(ox + 4, oy + 5)));
            app.MoveTo(At(window, Point2.Inches(ox + 24, oy + 11)));
            app.Expect("the selected leg shows no labels while the pointer is away from it", () =>
            {
                Assert.Equal(parts[1].Id, window.Editor.OnlySelected);
                Assert.False(canvas.SelectionLabelsShown);
            });

            app.MoveTo(At(window, Point2.Inches(ox + 4, oy + 9)));
            app.Expect("over the leg its labels show, and the status bar reads its name and three sizes", () =>
            {
                Assert.True(canvas.SelectionLabelsShown);
                // The leg's length is the 16", its width the 4", its thickness the 3/4" depth.
                Assert.Equal("Part 2  1'-4\" × 4\" × 3/4\"", window.SizeReadout);
            });
            app.SaveFrame("hovered-leg");

            // Q again: Precise, where the labels are always on and the readout is gone.
            app.Press(Key.Q);
            app.MoveTo(At(window, Point2.Inches(ox + 24, oy + 11)));
            app.Expect("back in Precise the word says so, the labels show wherever the pointer is, and the readout is gone", () =>
            {
                Assert.Equal("PRECISE", window.EntryModeWord);
                Assert.True(window.PreciseToggle.IsChecked);
                Assert.False(window.RoughToggle.IsChecked);
                Assert.True(canvas.SelectionLabelsShown);
                Assert.Equal(string.Empty, window.SizeReadout);
                Assert.Equal(0.5, canvas.SnapStepInches);
            });
        },
        defaultLook: true);

    [GuiWorkflow("GUI-SKETCH-02")]
    public void Firm_it_up_and_read_the_cut_list() => GuiWorkflow.Run(
        app =>
        {
            MainWindow window = (MainWindow)app.Target;
            SketchTheBench(app, window);
            Box[] parts = Parts(window);

            app.Press(Key.F);
            app.Expect("F opens Firm up over every part: the bench's four contacts, four stock lines and four size lines, all ticked", () =>
            {
                Assert.True(window.IsFirmingUp);
                Assert.Equal(4, window.FirmUpRelationshipTicks.Count);
                Assert.Equal(4, window.FirmUpStockTicks.Count);
                Assert.Equal(4, window.FirmUpSizeTicks.Count);
                Assert.All([.. window.FirmUpRelationshipTicks, .. window.FirmUpStockTicks, .. window.FirmUpSizeTicks], tick => Assert.True(tick.IsChecked));
                Assert.Contains(window.FirmUpLineTexts, text => text.Contains("Part 1's south face against Part 2's north face", StringComparison.Ordinal));
                Assert.Contains(window.FirmUpLineTexts, text => text.Contains("Part 2's east face against Part 4's west face", StringComparison.Ordinal));
            });
            app.SaveFrame("firm-up-sheet");

            // Leave Leg 2 and the stretcher unheld: untick that line with the pointer.
            int legTwoStretcher = window.FirmUpLineTexts.ToList().FindIndex(text => text.Contains("Part 3", StringComparison.Ordinal) && text.Contains("Part 4", StringComparison.Ordinal));
            Assert.InRange(legTwoStretcher, 0, 3);
            app.Click(CentreOf(window, window.FirmUpRelationshipTicks[legTwoStretcher]));
            app.Expect("the Leg 2 – Stretcher line is unticked", () => Assert.False(window.FirmUpRelationshipTicks[legTwoStretcher].IsChecked));

            app.Press(Key.Enter);

            // The stocks, from the shipped library's softwood table: the top, 48 x 2 x 3/4, is 1/2" from
            // a 1x2 (1 1/2 x 3/4) and from a 1x3 (2 1/2 x 3/4), a tie the name breaks: 1x2. A leg,
            // 16 x 4 x 3/4, is 1/2" from a 1x4 (3 1/2 x 3/4), 3/4" in all from a 5/4x4 (3 1/2 x 1), and
            // 1 1/4" from a 2x4: 1x4. The stretcher, 36 x 3 x 3/4, ties a 1x3 and a 1x4 at 1/2": 1x3.
            app.Expect("three Flushes, nothing rough, and each part cut from its nearest stock", () =>
            {
                Assert.False(window.IsFirmingUp);
                Sketch sketch = window.CurrentDesign!.Sketch;
                Assert.Equal(3, sketch.RelationshipsInOrder.OfType<Flush>().Count());
                Box[] now = Parts(window);
                Assert.All(now, part => Assert.False(part.Part!.Rough));
                Assert.Equal(["1x2", "1x4", "1x4", "1x3"], now.Select(part => part.Part!.Stock));
                Assert.StartsWith("Firmed up 4 parts: 3 relationships, 4 stocks, 4 sizes.", window.Editor.LastMessage!.Text, StringComparison.Ordinal);
            });

            app.Chord(Key.Z);
            app.Expect("one undo puts the sketch back: all four rough, no stock, nothing stated", () =>
            {
                Box[] back = Parts(window);
                Assert.All(back, part => Assert.True(part.Part!.Rough));
                Assert.All(back, part => Assert.Null(part.Part!.Stock));
                Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder);
                Assert.Equal(parts.Select(part => part.Width), back.Select(part => part.Width));
            });

            app.Chord(Key.Y);
            app.Click(CentreOf(window, window.FindControl<MenuItem>("ListsMenu")!));
            app.Click(CentreOf(window, window.FindControl<MenuItem>("CutListMenuItem")!));
            app.Expect("the cut list has no rough row, and is the three rows worked out by hand", () =>
            {
                CutListWindow list = window.CutList!;
                Assert.Equal(string.Empty, list.RoughNoteText);
                CutListRow[] rows = [.. list.Rows.Rows];
                Assert.All(rows, row => Assert.False(row.Rough));

                // Largest first: the top 48 x 1 1/2 x 3/4 (1x2), the stretcher 36 x 2 1/2 x 3/4 (1x3),
                // the two legs 16 x 3 1/2 x 3/4 (1x4) as one row of two.
                Assert.Equal(
                    [
                        (1, Length.Inches(48), Length.Inches(1, 1, 2), Length.Inches(0, 3, 4), "1x2"),
                        (1, Length.Inches(36), Length.Inches(2, 1, 2), Length.Inches(0, 3, 4), "1x3"),
                        (2, Length.Inches(16), Length.Inches(3, 1, 2), Length.Inches(0, 3, 4), "1x4"),
                    ],
                    rows.Select(row => (row.Quantity, row.Length, row.Width, row.Thickness, row.Material)).ToArray());
            });
            app.SaveFrame("cut-list");
        },
        defaultLook: true);

    [GuiWorkflow("GUI-SKETCH-03")]
    public void Type_a_size_on_a_rough_part() => GuiWorkflow.Run(
        app =>
        {
            MainWindow window = (MainWindow)app.Target;
            CanvasView canvas = window.Canvas;
            app.Chord(Key.N);
            app.Click(new Point(450, 320));
            app.Press(Key.Q);
            app.Press(Key.R);

            // A new sheet in the default window is at about 16 px per inch: the precise step 1" (16 px),
            // the rough one 3". (-23.2, -5.8) to (23.8, 6.2) lands on (-24, -6) to (24, 6): a plank 48 x 12.
            app.Drag(At(window, Inches(-23.2, -5.8)), At(window, Point2.Inches(0, 0)), At(window, Inches(23.8, 6.2)));
            Box plank = Assert.Single(Parts(window));
            app.MoveTo(At(window, Point2.Inches(0, -13)));
            app.Expect("a rough plank 48 x 12, selected, its labels quiet with the pointer away", () =>
            {
                Assert.Equal(3, canvas.SnapStepInches);
                Assert.Equal(Length.Inches(48), plank.Width);
                Assert.Equal(Length.Inches(12), plank.Height);
                Assert.True(plank.Part!.Rough);
                Assert.Equal(plank.Id, window.Editor.OnlySelected);
                Assert.False(canvas.SelectionLabelsShown);
            });

            app.MoveTo(At(window, Point2.Inches(10, 2)));
            app.Expect("over the plank its width label appears", () => Assert.True(canvas.SelectionLabelsShown));
            app.SaveFrame("label-on-hover");

            app.Click(LabelAt(window, plank.Id, SizeAxis.Width));
            app.Press(Key.A, AppDriver.CommandModifier);
            app.Type("3'-0\"");
            app.Press(Key.Enter);
            app.Expect("the plank is exactly 36\" wide and no longer rough", () =>
            {
                Box typed = Assert.Single(Parts(window));
                Assert.Equal(Length.Inches(36), typed.Width);
                Assert.False(typed.Part!.Rough);
            });
            app.SaveFrame("typed-firm");

            app.Chord(Key.Z);
            app.Expect("one undo restores the width and the rough mark together", () =>
            {
                Box back = Assert.Single(Parts(window));
                Assert.Equal(Length.Inches(48), back.Width);
                Assert.True(back.Part!.Rough);
            });
        },
        defaultLook: true);

    [GuiWorkflow("GUI-SKETCH-04")]
    public void Rough_on_the_cut_list_and_the_mark_by_hand() => GuiWorkflow.Run(
        app =>
        {
            MainWindow window = (MainWindow)app.Target;
            app.Chord(Key.N);
            app.Click(new Point(450, 320));
            app.Press(Key.Q);

            // Two planks of different sizes, so two rows, at the rough step of 3": (-23, -13) to (-1, -1)
            // lands on (-24, -12) to (0, 0), 24 x 12; (5, -7) to (25.4, -0.8) lands on (6, -6) to (24, 0), 18 x 6.
            app.Press(Key.R);
            app.Drag(At(window, Point2.Inches(-23, -13)), At(window, Point2.Inches(-10, -5)), At(window, Point2.Inches(-1, -1)));
            app.Press(Key.R);
            app.Drag(At(window, Point2.Inches(5, -7)), At(window, Point2.Inches(15, -3)), At(window, Inches(25.4, -0.8)));

            app.Chord(Key.L);
            app.Expect("both rows are tagged rough and the footer says two rows are", () =>
            {
                CutListWindow list = window.CutList!;
                Assert.Equal(2, list.Rows.Rows.Length);
                Assert.All(list.Rows.Rows, row => Assert.True(row.Rough));
                Assert.All(list.Rows.LinesOnScreen.Skip(1), line => Assert.Contains(" rough\t", line, StringComparison.Ordinal));
                Assert.Equal("2 rows are rough — sizes as drawn, stock not chosen", list.RoughNoteText);
            });
            app.SaveFrame("rough-cut-list");

            // Back in the plan: pick the 24 x 12 plank, untick Rough in the Part panel, Tab on, Enter.
            window.Activate();
            app.Click(At(window, Point2.Inches(-12, -6)));
            Box first = Parts(window).Single(box => box.Width == Length.Inches(24));
            app.Expect("the 24 x 12 plank is selected and its panel says Rough", () =>
            {
                Assert.Equal(first.Id, window.Editor.OnlySelected);
                Assert.True(window.RoughField.IsChecked);
            });
            app.Click(CentreOf(window, window.RoughField));
            app.Tab();
            app.Press(Key.Enter);
            app.Expect("the plank is firm, the undo step says so, and the cut list has one rough row", () =>
            {
                Assert.False(window.CurrentDesign!.Sketch.Find<Box>(first.Id)!.Part!.Rough);
                Assert.Equal("mark firm", window.Editor.History.UndoWhat);
                CutListWindow list = window.CutList!;
                Assert.Single(list.Rows.Rows, row => row.Rough);
                Assert.Equal("1 row is rough — sizes as drawn, stock not chosen", list.RoughNoteText);

                // The CSV has "yes" on the 18 x 6 row only.
                var lines = CutListCsv.Parse(list.Csv);
                Assert.Equal("Rough", lines[1][6]);
                Assert.Equal(2, lines.Length - 2);
                Assert.Equal("yes", lines.Skip(2).Single(line => line[2] == CutListCsv.Text(Length.Inches(18)))[6]);
                Assert.Equal(string.Empty, lines.Skip(2).Single(line => line[2] == CutListCsv.Text(Length.Inches(24)))[6]);
            });
        },
        defaultLook: true);

    static Point LabelAt(MainWindow window, EntityId box, SizeAxis axis)
    {
        Point onCanvas = window.Canvas.SelectionDimensionLabelAt(box, axis)
            ?? throw new InvalidOperationException($"{box} shows no {axis} dimension.");
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    /// <summary>The design's boxes by name: Part 1, Part 2, …, the order they were drawn in.</summary>
    static Box[] Parts(MainWindow window) =>
        [.. window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Name, StringComparer.Ordinal)];

    /// <summary>
    /// Draws the quick bench roughly, the way a person would, and leaves the sheet in Rough mode
    /// with nothing selected: Q, then each part with R and a drag a little off the round numbers;
    /// Leg 1 is drawn an inch low and dragged up against the top, which catches and states nothing.
    /// </summary>
    /// <returns>Where the bench's (0, 0) is, in whole inches: the round point nearest the middle of the canvas, less (24, 9).</returns>
    internal static (long X, long Y) SketchTheBench(AppDriver app, MainWindow window)
    {
        CanvasView canvas = window.Canvas;
        app.ResizeWindow(2600, 1100);
        app.Chord(Key.N);
        app.Click(new Point(900, 500));
        for (int i = 0; i < 5; i++)
        {
            app.Press(Key.Add);
        }

        app.Press(Key.Q);
        app.Expect("Q puts the editor in Rough: the status bar, the toolbar and the menu all say so", () =>
        {
            Assert.Equal(EntryMode.Rough, window.Editor.EntryMode);
            Assert.Equal("ROUGH", window.EntryModeWord);
            Assert.True(window.RoughToggle.IsChecked);
            Assert.False(window.PreciseToggle.IsChecked);
            Assert.True(window.RoughSketchingMenuItem.IsChecked);
            Assert.InRange(canvas.View.PixelsPerInch, 28, 56);
            Assert.Equal(0.5, SnapGrid.StepInches(canvas.View.PixelsPerInch));
            Assert.Equal(1, canvas.SnapStepInches);
            Assert.EndsWith("Snap 1\"", window.ZoomReadout.Text);
        });

        Point2 middle = canvas.View.ToWorld(new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2));
        long ox = (long)Math.Round(middle.X.ToInches()) - 24;
        long oy = (long)Math.Round(middle.Y.ToInches()) - 9;

        // Top: (0.3, 16.3) to (48.2, 17.8) from the bench's origin lands on (0, 16) to (48, 18).
        DrawRough(app, window, ox, oy, (0.3, 16.3), (24.4, 17.2), (48.2, 17.8));

        // Leg 1, an inch low: (2.2, -0.7) to (5.7, 14.8) lands on (2, -1) to (6, 15).
        DrawRough(app, window, ox, oy, (2.2, -0.7), (4, 7), (5.7, 14.8));

        // Leg 2: (42.4, 0.4) to (45.7, 16.2) lands on (42, 0) to (46, 16).
        DrawRough(app, window, ox, oy, (42.4, 0.4), (44, 9), (45.7, 16.2));

        // Stretcher: (6.2, 4.4) to (41.6, 6.6) lands on (6, 4) to (42, 7).
        DrawRough(app, window, ox, oy, (6.2, 4.4), (24, 5.5), (41.6, 6.6));

        // Leg 1 up against the top: grab it and drag up 0.9"; the grid and the top's edge both say 16.
        app.Drag(At(window, Inches(ox + 4, oy + 7)), At(window, Inches(ox + 4, oy + 7.5)), At(window, Inches(ox + 4, oy + 7.9)));
        app.Press(Key.Escape);
        return (ox, oy);
    }

    static void DrawRough(AppDriver app, MainWindow window, long ox, long oy, (double X, double Y) from, (double X, double Y) via, (double X, double Y) to)
    {
        app.Press(Key.R);
        app.Drag(
            At(window, Inches(ox + from.X, oy + from.Y)),
            At(window, Inches(ox + via.X, oy + via.Y)),
            At(window, Inches(ox + to.X, oy + to.Y)));
    }

    static void AssertBox(Box box, long x, long y, long width, long height)
    {
        Assert.Equal(Length.Inches(x), box.Anchor.X);
        Assert.Equal(Length.Inches(y), box.Anchor.Y);
        Assert.Equal(Length.Inches(width), box.Width);
        Assert.Equal(Length.Inches(height), box.Height);
    }

    static Point2 Inches(double x, double y) =>
        new(Length.FromInches(x, Rounding.HalfToEven), Length.FromInches(y, Rounding.HalfToEven));

    internal static Point At(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Assert.True(new Rect(window.Canvas.Bounds.Size).Contains(onCanvas), $"{world} is off the canvas at {onCanvas}.");
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    internal static Point CentreOf(Visual root, Visual control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)
        ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
}

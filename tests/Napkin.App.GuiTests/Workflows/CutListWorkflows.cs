using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The cut list, driven the way a person drives it: open a design, open the list beside it, read
/// every row, and sort it to find the piece you are about to cut.
/// </summary>
/// <remarks>
/// The rows are compared against <c>samples/coffee-table.expected.json</c> — the same hand-derived
/// answers the reader's tests and the furniture module's tests are held to — so what a person sees
/// on screen is checked against arithmetic a person did, not against what napkin printed.
/// </remarks>
public class CutListWorkflows
{
    [GuiWorkflow("GUI-CUT-03")]
    public void Open_the_cut_list_and_read_it_against_the_fixture() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        SampleExpectations expected = SampleExpectations.For("coffee-table");

        // Open the wall first, so that opening the coffee table afterwards is really an open.
        OpenSample(app, window, "Wall with window");
        app.Chord(Key.L);

        app.Expect("a design with nothing to cut says so rather than showing an empty table", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.Empty(list.Rows.Rows);
            Assert.Contains("nothing to cut", list.Headline, StringComparison.Ordinal);
            Assert.NotEmpty(list.EmptyMessage);
        });

        // Now the coffee table. The list is open, so it follows the drawing.
        OpenSample(app, window, "Coffee table");

        app.Expect("the cut list is the four rows the fixture's expectations state", () =>
        {
            CutListWindow list = window.CutList!;

            Assert.Equal(expected.CutList.Count, list.Rows.Rows.Length);
            Assert.Equal(
                expected.CutList.Select(row => row.OnScreen),
                list.Rows.LinesOnScreen.Skip(1));

            // Four legs drawn as four boxes are one row of four, which is the whole point.
            Assert.Equal([1, 2, 4, 2], list.Rows.Sorted.Select(row => row.Quantity));
            Assert.Contains("9 pieces to cut", list.Headline, StringComparison.Ordinal);
            Assert.Empty(list.EmptyMessage);
        });

        // The cut list is a window of its own, so it gets a driver of its own: the clicks below
        // are real input into the real window, hit-tested against the real header buttons.
        AppDriver list = AppDriver.Attach(window.CutList!, "cut-list");

        list.SaveFrame("coffee-table");

        // Sort by the part's name, by clicking the header the way a person looks for "Leg".
        list.Click(CentreOfHeader(window, "Part"));

        app.Expect("sorting by name reorders the table and changes no number", () =>
        {
            CutListWindow list = window.CutList!;

            Assert.Equal(
                ["Apron, long", "Apron, short", "Leg", "Top"],
                list.Rows.Sorted.Select(row => row.Label));

            // The same rows, in another order: nothing was recomputed.
            Assert.Equal(
                expected.CutList.Select(row => row.Label).Order(StringComparer.Ordinal),
                list.Rows.Sorted.Select(row => row.Label));
            Assert.Equal(9, list.Rows.Sorted.Sum(row => row.Quantity));
        });

        // Clicking the same header again turns the order around.
        list.Click(CentreOfHeader(window, "Part"));

        app.Expect("the second click reverses it", () =>
        {
            Assert.Equal(
                ["Top", "Leg", "Apron, short", "Apron, long"],
                window.CutList!.Rows.Sorted.Select(row => row.Label));
        });

        // The shortcut again brings the one that is open forward rather than opening a second.
        CutListWindow opened = window.CutList!;
        app.Chord(Key.L);
        app.Expect("there is one cut list, however many times it is asked for", () =>
        {
            Assert.Same(opened, window.CutList);
            Assert.True(window.CutList!.IsVisible);
            Assert.Equal(4, window.CutList!.Rows.Rows.Length);
        });

        // And closing it is not the end of it: the shortcut opens a fresh one on the same design.
        window.CutList!.Close();
        app.Chord(Key.L);
        app.Expect("closing the list and asking again gives a new one, showing the same design", () =>
        {
            Assert.NotNull(window.CutList);
            Assert.NotSame(opened, window.CutList);
            Assert.Equal(4, window.CutList!.Rows.Rows.Length);
        });

        app.Expect("the exported CSV is the table as it is being read, in the same order", () =>
        {
            // A fresh window sorts by length again, which is the order a person cuts in, so this
            // is the fixture's own order: Top, Apron long, Leg, Apron short.
            CutListWindow list = window.CutList!;
            string[] lines = list.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(list.Rows.Sorted.Length + 2, lines.Length);
            Assert.Equal(
                expected.CutList.Select(row => row.Label),
                list.Rows.Sorted.Select(row => row.Label));
            Assert.StartsWith("Top,1,", lines[2], StringComparison.Ordinal);
            Assert.StartsWith("\"Apron, long\",2,", lines[3], StringComparison.Ordinal);
        });

        // The list is a reading of the drawing rather than a snapshot of it, so it follows the
        // drawing being replaced and then drawn on.
        app.Chord(Key.N);
        app.Expect("a blank sheet empties the list, with the window still open", () =>
        {
            Assert.NotNull(window.CutList);
            Assert.Empty(window.CutList!.Rows.Rows);
            Assert.Equal("This design has nothing in it to cut.", window.CutList!.EmptyMessage);
        });

        // Back to the drawing — the cut list took the focus when its header was clicked — and draw
        // one box on the blank sheet.
        app.Click(new Point(450, 320));
        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(-12, -9)), At(window, Point2.Inches(0, 0)), At(window, Point2.Inches(12, 9)));

        app.Expect("a box that has been drawn but not made a part says so rather than listing", () =>
        {
            Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
            Assert.Empty(window.CutList!.Rows.Rows);
            Assert.StartsWith(
                "Nothing in this design is a part yet",
                window.CutList!.EmptyMessage,
                StringComparison.Ordinal);
        });
    });

    /// <remarks>
    /// This is <c>GUI-CUT-05</c> and not <c>GUI-CUT-02</c>: the picker GUI-CUT-02 asks for is a
    /// floating toolbox with one icon per category and a text list inside the chosen one, and this
    /// panel is a typed stock name with the library's own hover line under it. The lookup, the
    /// citation and the assignment are real; the icons are not built yet, so the feature they are
    /// the point of stays unclaimed.
    /// </remarks>
    [GuiWorkflow("GUI-CUT-05")]
    public void Make_a_drawn_box_into_a_part_and_watch_it_reach_the_cut_list() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        // A blank sheet and one box: 24" x 4" in plan, which is a rail on edge.
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(0, 0)), At(window, Point2.Inches(12, 2)), At(window, Point2.Inches(24, 4)));

        app.Expect("the box is drawn, selected, and the properties panel is offering to make it a part", () =>
        {
            Box drawn = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
            Assert.Null(drawn.Part);
            Assert.Equal("Part 1", drawn.Name);

            Assert.True(window.IsShowingProperties);
            Assert.Equal("Part 1", window.PartNameField.Text);
            Assert.False(window.IsPartField.IsChecked);
        });

        app.Chord(Key.L);
        app.Expect("a box that is not a part is not on the cut list", () =>
        {
            Assert.Empty(window.CutList!.Rows.Rows);
            Assert.StartsWith(
                "Nothing in this design is a part yet",
                window.CutList!.EmptyMessage,
                StringComparison.Ordinal);
        });

        // Say what it is: a 2x4 rail, two of them, on edge — length across X, width up Y, and the
        // 1 1/2" thickness is the dimension the plan cannot hold.
        app.Click(CentreOf(window, window.PartNameField));
        app.Chord(Key.A);
        app.Type("Rail, front");

        app.Click(CentreOf(window, window.IsPartField));
        app.Expect("ticking the box opens the fields that say what kind of part it is", () =>
        {
            Assert.True(window.IsPartField.IsChecked);
            Assert.True(window.OutOfPlaneField.IsVisible);
        });

        // A "-" typed into a field is text, not a view command: a TextBox leaves KeyDown alone for
        // a character key, so without a guard the window would zoom out on the dash of 1'-4 1/4".
        app.Click(CentreOf(window, window.OutOfPlaneField));
        double zoom = window.Canvas.View.PixelsPerInch;
        app.Press(Key.OemMinus);
        app.Press(Key.OemPlus);
        app.Press(Key.Down);

        app.Expect("typing a dimension does not steer the drawing", () =>
            Assert.Equal(zoom, window.Canvas.View.PixelsPerInch));

        Fill(app, window, window.OutOfPlaneField, "1 1/2\"");
        Fill(app, window, window.QuantityField, "2");
        Fill(app, window, window.StockField, "2 x 4");

        app.Expect("the library says what a 2x4 actually measures, before anything is applied", () =>
        {
            Assert.Contains("actual 1 1/2\" x 3 1/2\"", window.StockReadoutText, StringComparison.Ordinal);
            Assert.Contains("PS 20", window.StockReadoutText, StringComparison.Ordinal);
        });

        app.SaveFrame("properties-panel");
        app.Click(CentreOf(window, window.ApplyPart));

        app.Expect("the box is a part now, and the cut list says what to cut", () =>
        {
            Box part = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
            Assert.Equal("Rail, front", part.Name);
            Assert.Equal("2 x 4", part.Part?.Stock);
            Assert.Equal(2, part.Part?.Quantity);

            CutListRow row = Assert.Single(window.CutList!.Rows.Rows);
            Assert.Equal("Rail, front", row.Label);
            Assert.Equal(2, row.Quantity);

            // The two in-plan dimensions are the box's own, and the third is what was typed.
            Assert.Equal(Length.Inches(24).Units, row.Length.Units);
            Assert.Equal(Length.Inches(4).Units, row.Width.Units);
            Assert.Equal(1536, row.Thickness.Units);

            // "2 x 4" resolved to the library's own name and item, not to the name as typed.
            Assert.Equal("2x4", row.Material);
            Assert.False(row.Unresolved);
            Assert.NotNull(row.Stock);
        });
    });

    /// <summary>Replaces what a field says, the way a person does: select all, then type.</summary>
    static void Fill(AppDriver app, MainWindow window, TextBox field, string text)
    {
        app.Click(CentreOf(window, field));
        app.Chord(Key.A);
        app.Type(text);
    }

    /// <summary>The window coordinate a model point is drawn at.</summary>
    static Point At(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
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

    /// <summary>Where a cut-list column header is, in the cut-list window's own coordinates.</summary>
    static Point CentreOfHeader(MainWindow window, string column)
    {
        CutListTable table = window.CutList!.Rows;
        Button header = table.Children
            .OfType<Button>()
            .Single(candidate => (candidate.Content as TextBlock)?.Text?.StartsWith(
                column, StringComparison.Ordinal) == true);

        return CentreOf(window.CutList!, header);
    }

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        Size size = control.Bounds.Size;
        return topLeft + new Point(size.Width / 2, size.Height / 2);
    }
}

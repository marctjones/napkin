using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
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
            Assert.Contains(CutList.Headline(0, 0), list.Headline, StringComparison.Ordinal);
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
            Assert.Contains(CutList.Headline(4, 9), list.Headline, StringComparison.Ordinal);
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
            Assert.Equal(CutList.NothingToCut, window.CutList!.EmptyMessage);
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

            // Choosing a stock really makes the blank that stock: the rail was drawn 4" across,
            // and a 2x4 is 3 1/2" wide, so the blank is now 3 1/2" wide (parts-and-cut-list.md
            // §1.2). The length is the free dimension, so it stays what was drawn, and the
            // thickness is what was typed — the same 1 1/2" the yard would have fixed it at.
            Assert.True(MaterialsLibrary.Shipped.TryFindLumber("2x4", out LumberStock lumber));
            Assert.Equal(Length.Inches(24).Units, row.Length.Units);
            Assert.Equal(lumber.Width.Units, row.Width.Units);
            Assert.NotEqual(Length.Inches(4).Units, row.Width.Units);
            Assert.Equal(lumber.Thickness.Units, row.Thickness.Units);
            Assert.Equal(1536, row.Thickness.Units);

            // The yard owns that number now, so the resize handle on that edge says so.
            Assert.Equal(
                lumber.Width,
                Assert.Single(
                    window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<ParamValue>(),
                    value => value.Param.Equals(new BoxHeightRef(part.Id))).Value);

            // "2 x 4" resolved to the library's own name and item, not to the name as typed.
            Assert.Equal("2x4", row.Material);
            Assert.False(row.Unresolved);
            Assert.NotNull(row.Stock);
        });
    });

    /// <summary>
    /// The DIY coffee table's cut list, opened with the mouse and read against the rows the joinery
    /// note works out by hand: finished sizes with the allowances in, the sentences that say what to
    /// cut, and two mirrored drawer sides as two rows.
    /// </summary>
    [GuiWorkflow("GUI-CUT-07")]
    public void Open_the_diy_table_and_read_what_its_joints_ask_of_each_part() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        System.Text.Json.JsonElement expected;
        using (FileStream stream = File.OpenRead(Path.Combine(RepositoryLayout.SamplesDirectory, "diy-coffee-table-drawers.expected.json")))
        {
            expected = System.Text.Json.JsonDocument.Parse(stream).RootElement.Clone();
        }

        System.Text.Json.JsonElement[] wantRows = [.. expected.GetProperty("cutList").EnumerateArray()];

        OpenSample(app, window, "DIY coffee table with drawers");
        app.Chord(Key.L);

        app.Expect("the list is the thirteen rows the note works out, twenty-four pieces, with the finished sizes", () =>
        {
            CutListWindow list = window.CutList!;

            Assert.Equal(13, list.Rows.Rows.Length);
            Assert.Contains(CutList.Headline(13, 24), list.Headline, StringComparison.Ordinal);
            Assert.Equal(
                wantRows.Select(row => row.GetProperty("label").GetString()),
                list.Rows.Sorted.Select(row => row.Label));

            // 16 1/8" is the box front's drawn 15 5/8" plus a 1/4" rabbet at each end; the bottom's 16 1/8" x 15 1/2" likewise.
            CutListRow front = Assert.Single(list.Rows.Sorted, row => row.Label == "Drawer box front");
            Assert.Equal(16512, front.Length.Units);
            Assert.Equal("1'-4 1/8\"", CutListCsv.Text(front.Length));
            CutListRow bottom = Assert.Single(list.Rows.Sorted, row => row.Label == "Drawer bottom");
            Assert.Equal(("1'-4 1/8\"", "1'-3 1/2\""), (CutListCsv.Text(bottom.Length), CutListCsv.Text(bottom.Width)));
        });

        app.Expect("what a person sees under each row is the sentences the note fixes, verbatim", () =>
        {
            string[] screen = [.. window.CutList!.Rows.LinesOnScreen];

            foreach (System.Text.Json.JsonElement row in wantRows)
            {
                foreach (System.Text.Json.JsonElement sentence in row.GetProperty("joinery").EnumerateArray())
                {
                    Assert.Contains(sentence.GetString(), screen);
                }
            }

            Assert.Contains("Drill 3 pocket holes in the west end and 3 in the east end, from the south face.", screen);
            Assert.Contains("Drill 3 pocket holes in the north end and 2 in the south end, from the west face.", screen);
            Assert.DoesNotContain(screen, line => line.Contains("joint not satisfied", StringComparison.Ordinal));
        });

        AppDriver list = AppDriver.Attach(window.CutList!, "cut-list");
        list.SaveFrame("diy-coffee-table");

        // Sort by part name with the mouse, the way a person hunts for the drawer sides.
        list.Click(CentreOfHeader(window, "Part"));

        app.Expect("sorting by name puts the mirrored drawer sides next to each other, still two rows of two", () =>
        {
            string[] labels = [.. window.CutList!.Rows.Sorted.Select(row => row.Label)];
            int left = Array.IndexOf(labels, "Drawer side, left");

            Assert.Equal("Drawer side, right", labels[left + 1]);
            Assert.All(
                window.CutList!.Rows.Sorted.Where(row => row.Label.StartsWith("Drawer side", StringComparison.Ordinal)),
                row => Assert.Equal(2, row.Quantity));
            Assert.Equal(24, window.CutList!.Rows.Sorted.Sum(row => row.Quantity));
        });

        app.Expect("the exported file is the expectations' own CSV, the Joinery column and the new header line included", () =>
        {
            // The window is now sorted by name; the CSV is in the order on screen, so compare as sets of lines.
            string[] lines = window.CutList!.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string[] want = [.. expected.GetProperty("cutListCsv").EnumerateArray().Select(line => line.GetString()!)];

            Assert.Equal(want[0], lines[0]);
            Assert.Equal(want[1], lines[1]);
            Assert.EndsWith(",Cuts,Joinery", lines[1], StringComparison.Ordinal);
            Assert.Equal(want.Skip(2).Order(StringComparer.Ordinal), lines.Skip(2).Order(StringComparer.Ordinal));
        });

        // The list follows the drawing: another sample, with no joints, takes the joinery with it, and coming
        // back to the DIY table brings the sentences back.
        OpenSample(app, window, "Coffee table");
        app.Expect("a design with no joints has no joinery sentences and no Joinery text in its CSV", () =>
        {
            Assert.Equal(4, window.CutList!.Rows.Rows.Length);
            Assert.All(window.CutList!.Rows.Rows, row => Assert.Empty(row.JointText));
            Assert.All(
                window.CutList!.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(2),
                line => Assert.EndsWith(",,", line, StringComparison.Ordinal));
        });

        OpenSample(app, window, "DIY coffee table with drawers");
        app.Chord(Key.L);
        app.Expect("the DIY table is back with all thirteen rows and their sentences", () =>
        {
            Assert.Equal(13, window.CutList!.Rows.Rows.Length);
            Assert.Contains(
                "Groove the north face: 1/4\" wide, 1/4\" deep, 1/2\" from the bottom edge, full length.",
                window.CutList!.Rows.LinesOnScreen);
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
        app.Click(CentreOf(window, window.FileMenuItem));
        app.Click(CentreOf(window, window.SamplesMenuItem));

        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    /// <summary>Where a cut-list column header is, in the cut-list window's own coordinates.</summary>
    /// <summary>Where a row's name is in the cut-list window: the first cell of its line.</summary>
    static Point CentreOfRow(MainWindow window, string label)
    {
        CutListTable table = window.CutList!.Rows;
        int line = table.LineOf(table.Rows.Single(row => row.Label == label));
        Control name = table.Children.OfType<Control>().First(cell => Grid.GetRow(cell) == line && Grid.GetColumn(cell) == 0);
        return CentreOf(window.CutList!, name);
    }

    [GuiWorkflow("GUI-CUT-09")]
    public void A_row_of_the_cut_list_selects_its_parts_in_the_drawing() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Coffee table");
        app.Chord(Key.L);
        AppDriver list = AppDriver.Attach(window.CutList!, "cut-list");
        HashSet<EntityId> legs = [.. window.CutList!.Rows.Rows.Single(row => row.Label == "Leg").Members];
        EntityId top = Assert.Single(window.CutList!.Rows.Rows.Single(row => row.Label == "Top").Members);

        list.Click(CentreOfRow(window, "Leg"));
        app.Expect("a click on the Leg row selects the four legs in the drawing", () =>
        {
            Assert.Equal(4, legs.Count);
            Assert.True(legs.SetEquals(window.Editor.Selection));
        });

        list.Click(CentreOfRow(window, "Top"), modifiers: KeyModifiers.Shift);
        app.Expect("Shift adds the top: five parts selected", () =>
            Assert.True(legs.Append(top).ToHashSet().SetEquals(window.Editor.Selection)));

        list.Click(CentreOfRow(window, "Leg"), modifiers: AppDriver.CommandModifier);
        app.Expect("Ctrl or Cmd on the Leg row takes the legs back out: the top alone", () =>
            Assert.Equal(top, Assert.Single(window.Editor.Selection)));

        // The table still sorts from its headers: a header click is not a row.
        list.Click(CentreOfHeader(window, "Part"));
        app.Expect("the Part header sorts and selects nothing new", () =>
        {
            Assert.Equal(["Apron, long", "Apron, short", "Leg", "Top"], window.CutList!.Rows.Sorted.Select(row => row.Label));
            Assert.Equal(top, Assert.Single(window.Editor.Selection));
        });

        list.Click(CentreOfRow(window, "Apron, short"));
        HashSet<EntityId> aprons = [.. window.CutList!.Rows.Rows.Single(row => row.Label == "Apron, short").Members];
        app.Expect("a click on another row replaces the selection with its parts", () =>
            Assert.True(aprons.SetEquals(window.Editor.Selection)));

        // Back in the drawing, it is the drawing's one selection: 3D shows it, Escape clears it.
        app.Press(Key.D7);
        app.Expect("in 3D the short aprons are the selection", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.True(aprons.SetEquals(window.Editor.Selection));
        });

        app.Press(Key.Escape);
        app.Expect("Escape in the drawing clears it", () => Assert.Empty(window.Editor.Selection));
        window.CutList!.Close();
    });

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

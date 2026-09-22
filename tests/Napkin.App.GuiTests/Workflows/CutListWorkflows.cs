using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;

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

        // Back to the drawing, and the list follows an edit to it.
        app.Press(Key.Escape);
        app.Expect("the exported CSV is the table as it is being read, in the same order", () =>
        {
            CutListWindow list = window.CutList!;
            string[] lines = list.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(list.Rows.Sorted.Length + 2, lines.Length);
            Assert.StartsWith("Top,1,", lines[2], StringComparison.Ordinal);
            Assert.Contains("\"Apron, long\"", lines[5], StringComparison.Ordinal);
        });
    });

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

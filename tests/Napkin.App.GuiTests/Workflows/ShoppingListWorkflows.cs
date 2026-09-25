using System.Text.Json;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Modules.Furniture;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The shopping list, driven the way a person drives it: open a stocked design, open its cut list,
/// turn to the shopping list beside it, sort it, and export it.
/// </summary>
/// <remarks>
/// The rows are compared against <c>samples/stocked-bench.expected.json</c>'s <c>shoppingListCsv</c>
/// — the list worked out by hand, not what napkin printed — and the export is compared against the
/// table on screen, row for row.
/// </remarks>
public class ShoppingListWorkflows
{
    [GuiWorkflow("GUI-CUT-04")]
    public void Open_the_shopping_list_read_it_against_the_fixture_sort_it_and_export_it() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        string[][] expected = ExpectedShoppingRows();

        OpenSample(app, window, "Stocked bench");
        app.Chord(Key.L);

        app.Expect("the cut list opens on its own tab, five rows for eleven pieces", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.False(list.IsShowingShoppingList);
            Assert.Equal(5, list.Rows.Rows.Length);
            Assert.Equal(11, list.Rows.Rows.Sum(row => row.Quantity));
        });

        // Turn to the shopping list with the mouse, in the cut-list window itself.
        AppDriver lists = AppDriver.Attach(window.CutList!, "shopping-list");
        lists.Click(CentreOf(window.CutList!, window.CutList!.ShoppingListTabItem));

        app.Expect("the shopping list is the three lines worked out by hand, in that order", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.True(list.IsShowingShoppingList);
            Assert.Equal(
                expected.Select(fields => string.Join("\t", fields)),
                list.ShoppingRows.LinesOnScreen.Skip(1));

            // Five cut-list rows become three lines to buy; the 1x4's four pieces (120 in + 4 cuts of 1/8 in) are one 12' board.
            Assert.Equal(["1x4", "2x4", "3/4 plywood"], list.ShoppingRows.Sorted.Select(row => row.Material));
            Assert.Equal([1, 1, 1], list.ShoppingRows.Sorted.Select(row => row.Count));
        });

        lists.SaveFrame("shopping-list-light");

        // Sort by board feet bought, by clicking its header.
        lists.Click(CentreOfHeader(window, "Bd ft bought"));

        app.Expect("sorting by board feet bought puts the most first and changes no number", () =>
        {
            ShoppingListTable table = window.CutList!.ShoppingRows;
            Assert.Equal(ShoppingListColumn.Bought, table.SortBy);
            Assert.Equal(["2x4", "1x4", "3/4 plywood"], table.Sorted.Select(row => row.Material));
            Assert.Equal(["9.3", "4.0", ""], table.Sorted.Select(row => row.BoughtText));
        });

        app.Expect("the export is the table as it is being read, row for row", () =>
        {
            CutListWindow list = window.CutList!;
            string[] onScreen = [.. list.ShoppingRows.LinesOnScreen];
            var exported = CutListCsv.Parse(list.ShoppingCsv);

            Assert.Equal([ShoppingList.Statement(CutLayout.DefaultKerf)], exported[0]);
            Assert.Equal(onScreen.Length, exported.Length - 1);
            Assert.Equal(onScreen.Skip(1), exported.Skip(2).Select(fields => string.Join("\t", fields)));
        });

        // Close it, and ask for the shopping list straight from the keyboard: the window comes back
        // already turned to it.
        window.CutList!.Close();
        app.Chord(Key.L, KeyModifiers.Shift);

        app.Expect("the shopping-list shortcut opens the window on the shopping list", () =>
        {
            Assert.NotNull(window.CutList);
            Assert.True(window.CutList!.IsShowingShoppingList);
            Assert.Equal(3, window.CutList!.ShoppingRows.Rows.Length);
        });

        // And in the dark theme, for the frame a person checks the table's legibility against.
        PickTheme(app, window, "ThemeDarkMenuItem");
        app.Expect("the lists follow the window into the dark theme", () =>
            Assert.Equal(ThemeVariant.Dark, window.CutList!.ActualThemeVariant));
        AppDriver.Attach(window.CutList!, "shopping-list-dark").SaveFrame("shopping-list-dark");
        PickTheme(app, window, "ThemeSystemMenuItem");
    });

    /// <summary>The hand-worked rows, field by field, from the fixture's own CSV lines.</summary>
    static string[][] ExpectedShoppingRows()
    {
        string path = Path.Combine(RepositoryLayout.SamplesDirectory, "stocked-bench.expected.json");
        using FileStream stream = File.OpenRead(path);
        JsonElement lines = JsonDocument.Parse(stream).RootElement.GetProperty("shoppingListCsv");

        string csv = string.Join("\n", lines.EnumerateArray().Select(line => line.GetString())) + "\n";
        return [.. CutListCsv.Parse(csv).Skip(2).Select(fields => fields.ToArray())];
    }

    static void PickTheme(AppDriver app, MainWindow window, string itemName)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>(itemName)!));
    }

    static void OpenSample(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.SamplesMenuItem));

        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    static Point CentreOfHeader(MainWindow window, string column)
    {
        Button header = window.CutList!.ShoppingRows.Children
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

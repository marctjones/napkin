using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>The Parts view (docs/design/parts-view.md §7.4): the cut list drawn, one cell per distinct piece.</summary>
public class PartsViewWorkflows
{
    static string Focused(MainWindow window) => window.Parts.FocusedCell?.Row.Label ?? "(none)";

    [GuiWorkflow("GUI-PARTS-01")]
    public void Open_the_coffee_table_show_its_parts_and_walk_the_cells_by_keyboard_and_pointer() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Coffee table");

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("PartsViewMenuItem")!));
        app.Expect("View > Parts shows one cell per distinct piece: the top, the legs ×4 and two apron cells ×2", () =>
        {
            Assert.True(window.IsShowingParts);
            Assert.False(window.IsShowingPlan);
            Assert.False(window.IsShowingModel);
            Assert.Equal(DesignView.Parts, window.CurrentView);
            Assert.Equal("Parts", window.ViewReadout);
            Assert.True(window.ViewChip(DesignView.Parts).IsChecked);
            Assert.Equal(StandardViewWords.PartsHint, window.MessageOnScreen);

            Assert.Equal(4, window.Parts.CellsOnScreen.Length);
            Assert.StartsWith("Top, ×1, ", window.Parts.CellsOnScreen[0], StringComparison.Ordinal);
            Assert.Contains(window.Parts.CellsOnScreen, cell => cell.StartsWith("Leg, ×4, ", StringComparison.Ordinal));
            Assert.Equal(2, window.Parts.CellsOnScreen.Count(cell => cell.StartsWith("Apron", StringComparison.Ordinal) && cell.Contains(", ×2, ", StringComparison.Ordinal)));
            Assert.Null(window.Parts.EmptyMessage);
        });
        app.SaveFrame("coffee-table-parts");

        int columns = window.Parts.Columns;
        app.Press(Key.Right);
        app.Press(Key.Right);
        app.Expect("Right twice: onto the first cell, then the next along", () => Assert.Equal(window.Parts.Cells[1].Row.Label, Focused(window)));

        app.Press(Key.Down);
        app.Expect("Down: one row further, the same column", () => Assert.Equal(window.Parts.Cells[Math.Min(1 + columns, 3)].Row.Label, Focused(window)));

        // A click on the legs' cell puts the focus there.
        PartsCell legs = window.Parts.Cells.Single(cell => cell.Row.Label == "Leg");
        Rect onScreen = window.Parts.CellRectangle(legs)!.Value;
        app.Click(window.Parts.TranslatePoint(onScreen.Center, window)!.Value);
        app.Expect("a click on the legs' cell focuses it", () => Assert.Equal("Leg", Focused(window)));

        app.Press(Key.R);
        app.Expect("R draws nothing here, and says where to draw", () =>
        {
            Assert.Equal(StandardViewWords.NotInPartsView, window.MessageOnScreen);
            Assert.True(window.IsShowingParts);
        });

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ZoomInMenuItem")!));
        app.Expect("View > Zoom in zooms the sheet, and the status bar says so", () =>
        {
            Assert.Equal(125, window.Parts.ZoomPercent, 6);
            Assert.StartsWith("Zoom 125%", window.ZoomReadout.Text, StringComparison.Ordinal);
        });

        app.Press(Key.D3);
        app.Expect("3 leaves the Parts view for Front", () =>
        {
            Assert.False(window.IsShowingParts);
            Assert.Equal(DesignView.Front, window.CurrentView);
            Assert.False(window.ViewChip(DesignView.Parts).IsChecked);
        });
    });
}

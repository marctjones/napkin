using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>The Parts view (docs/design/parts-view.md §7.4): the cut list drawn, one cell per distinct piece.</summary>
public class PartsViewWorkflows
{
    static string Focused(MainWindow window) => window.Parts.FocusedCell?.Row.Label ?? "(none)";

    /// <summary>Clicks a cell of the Parts view, where it is on the screen.</summary>
    static void ClickCell(AppDriver app, MainWindow window, string label)
    {
        PartsCell cell = window.Parts.Cells.Single(each => each.Row.Label == label);
        app.Click(window.Parts.TranslatePoint(window.Parts.CellRectangle(cell)!.Value.Center, window)!.Value);
    }

    /// <summary>A point low on the south-west leg, an inch above the floor, where Front shows it in front of everything.</summary>
    static Point LowOnSouthWestLeg(MainWindow window)
    {
        Point2 middle = BoxNamed(window, "Leg, south-west").Footprint().Center;
        return InModel(window, window.Model.Camera.Project(new Vector3d(middle.X.ToInches(), middle.Y.ToInches(), 1)));
    }

    static HashSet<EntityId> Legs(MainWindow window) =>
    [
        .. window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>()
            .Where(box => box.Name is { } name && name.StartsWith("Leg", StringComparison.Ordinal))
            .Select(box => box.Id),
    ];

    static PartsCell LegCell(MainWindow window) => window.Parts.Cells.Single(cell => cell.Row.Label == "Leg");

    static void ShowParts(AppDriver app, MainWindow window)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("PartsViewMenuItem")!));
    }

    [GuiWorkflow("GUI-PARTS-02")]
    public void A_cell_selects_its_parts_in_the_model_and_the_models_selection_shows_on_the_cell() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Coffee table");
        ShowParts(app, window);

        ClickCell(app, window, "Leg");
        app.Expect("a click on the legs' cell selects the four legs in the model, and the cell is selected", () =>
        {
            Assert.True(Legs(window).SetEquals(window.Editor.Selection));
            Assert.Equal(PartsCellSelection.All, window.Parts.SelectionOf(LegCell(window)));
            Assert.Equal(PartsCellSelection.None, window.Parts.SelectionOf(window.Parts.Cells[0]));
        });
        app.SaveFrame("legs-selected");

        app.Press(Key.D7);
        app.Expect("in 3D the four legs are the selection", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.True(Legs(window).SetEquals(window.Editor.Selection));
        });

        app.Click(CentreOf(window, window.ViewChip(DesignView.Parts)));
        app.Expect("back in the Parts view the cell is still selected", () =>
            Assert.Equal(PartsCellSelection.All, window.Parts.SelectionOf(LegCell(window))));

        app.Press(Key.Escape);
        app.Expect("Escape clears the selection", () => Assert.Empty(window.Editor.Selection));

        // The keyboard does what the pointer did: onto the legs' cell (third in the sheet), Enter.
        app.Press(Key.Home);
        app.Press(Key.Right);
        app.Press(Key.Right);
        app.Press(Key.Enter);
        app.Expect("Home, Right, Right, Enter: the legs are selected from the keyboard", () =>
        {
            Assert.Equal("Leg", window.Parts.FocusedCell?.Row.Label);
            Assert.True(Legs(window).SetEquals(window.Editor.Selection));
        });

        // In Front, pick one leg: the cell for four is now partly selected.
        app.Press(Key.D3);
        app.MoveTo(LowOnSouthWestLeg(window));
        app.Expect("in Front the pointer is on the south-west leg", () =>
            Assert.Equal(BoxNamed(window, "Leg, south-west").Id, window.Model.HoveredPart));
        app.Click(LowOnSouthWestLeg(window));
        app.Click(CentreOf(window, window.ViewChip(DesignView.Parts)));
        app.Expect("one leg of four selected: the legs' cell is partly selected", () =>
        {
            Assert.Equal(BoxNamed(window, "Leg, south-west").Id, Assert.Single(window.Editor.Selection));
            Assert.Equal(PartsCellSelection.Partly, window.Parts.SelectionOf(LegCell(window)));
        });
        app.SaveFrame("one-leg-partly");
    });

    [GuiWorkflow("GUI-PARTS-03")]
    public void Deleting_one_leg_from_the_Parts_view_reads_three_and_undo_reads_four_as_the_cut_list_does() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Coffee table");

        app.Press(Key.D3);
        app.Click(LowOnSouthWestLeg(window));
        ShowParts(app, window);
        app.Expect("one leg selected, the legs' cell partly selected and reading ×4", () =>
        {
            Assert.Equal(PartsCellSelection.Partly, window.Parts.SelectionOf(LegCell(window)));
            Assert.Contains(window.Parts.CellsOnScreen, cell => cell.StartsWith("Leg, ×4, ", StringComparison.Ordinal));
        });

        app.Press(Key.Delete);
        app.Expect("Delete in the Parts view takes that one leg: the cell reads ×3", () =>
        {
            Assert.Contains(window.Parts.CellsOnScreen, cell => cell.StartsWith("Leg, ×3, ", StringComparison.Ordinal));
            Assert.Equal(3, Legs(window).Count);
            Assert.True(window.IsShowingParts);
        });

        app.Press(Key.Z, AppDriver.CommandModifier);
        app.Expect("undo puts it back: ×4", () =>
        {
            Assert.Contains(window.Parts.CellsOnScreen, cell => cell.StartsWith("Leg, ×4, ", StringComparison.Ordinal));
            Assert.Equal(4, Legs(window).Count);
        });

        app.Chord(Key.L);
        app.Expect("the cut list opened now agrees: four legs", () =>
            Assert.Equal(4, window.CutList!.Rows.Rows.Single(row => row.Label == "Leg").Quantity));
        window.CutList!.Close();
    });

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

        // V goes back to the last standard view, which the Parts view is not (parts-view §10.5): from
        // Parts, 7 shows 3D, and V from there returns to Top, where the design opened.
        app.Press(Key.D7);
        app.Press(Key.V);
        app.Expect("7 then V: back to Top, not to the Parts view", () =>
        {
            Assert.Equal(DesignView.Top, window.CurrentView);
            Assert.True(window.IsShowingPlan);
        });

        app.Click(CentreOf(window, window.ViewChip(DesignView.Parts)));
        app.Expect("the Parts chip shows it again, the zoom kept", () =>
        {
            Assert.True(window.IsShowingParts);
            Assert.Equal(125, window.Parts.ZoomPercent, 6);
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

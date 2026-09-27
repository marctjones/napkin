using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// A part's species (docs/design/furniture-checks.md slice A, #216): picked from the cited table with
/// the pointer, its Wood Handbook values said beneath it; typed as free text, kept and never
/// interpreted; one undo takes the typing back.
/// </summary>
public class FurnitureCheckWorkflows
{
    [GuiWorkflow("GUI-FURN-01")]
    public void Pick_a_parts_species_from_the_table_type_one_it_does_not_have_and_undo() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Coffee table");
        Box leg = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Leg, south-west");
        app.Click(OnPlan(window, leg.Center.XY));
        app.Expect("the leg is selected and has no species yet", () =>
        {
            Assert.Equal(leg.Id, window.Editor.OnlySelected);
            Assert.Equal(string.Empty, window.SpeciesReadoutText);
        });

        // The picker, with the pointer: open it and choose northern red oak.
        Reveal(app, window, window.SpeciesPickerControl);
        app.Click(CentreOf(window, window.SpeciesPickerControl));
        ComboBoxItem oak = window.GetVisualDescendants().OfType<ComboBoxItem>().Single(item => (item.Content as string) == "Oak, northern red");
        app.Click(CentreOf(window, oak));
        app.Expect("the leg is northern red oak, and the panel says the Handbook's values for it", () =>
        {
            Assert.Equal("Oak, northern red", window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!.Part!.Species);
            Assert.Equal("Specific gravity 0.63 and bending E 1,820,000 lbf/in² at 12 % moisture, clear wood (Wood Handbook, Table 5–3b, p. 5–10).", window.SpeciesReadoutText);
        });

        // Free text, with the keyboard: kept as typed, never matched to a row.
        Reveal(app, window, window.SpeciesField);
        app.Click(CentreOf(window, window.SpeciesField));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type("red oak");
        app.Press(Key.Enter);
        app.Expect("\"red oak\" is kept as typed, the picker shows nothing, and the panel says it is not interpreted", () =>
        {
            Assert.Equal("red oak", window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!.Part!.Species);
            Assert.Null(window.SpeciesPickerControl.SelectedItem);
            Assert.StartsWith("\"red oak\" is not in napkin's species table", window.SpeciesReadoutText, StringComparison.Ordinal);
        });

        // Edit → Undo takes the typing back to the picked species.
        app.Click(CentreOf(window, window.EditMenuItem));
        app.Click(CentreOf(window, window.UndoMenuEntry));
        app.Expect("undo puts northern red oak back", () =>
            Assert.Equal("Oak, northern red", window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!.Part!.Species));
    });

    [GuiWorkflow("GUI-FURN-02")]
    public void A_drawer_bottom_in_grooves_gets_a_sag_estimate_once_it_is_solid_oak_and_a_verdict_once_a_limit_is_typed() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "DIY coffee table with drawers");

        // The drawer's parts sit a quarter inch apart, closer than a click can tell at this zoom: picked by name, as DIY-01 does.
        Box bottom = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Drawer bottom, A");
        window.Editor.Select(bottom.Id);
        app.WaitForIdle();
        app.Expect("grooved at both ends, the bottom is a shelf, and 1/4 plywood is not estimated", () =>
        {
            Assert.True(window.IsShowingShelf);
            Assert.Equal("Shelf sag: not estimated — 1/4 plywood is a sheet good, and napkin has no cited stiffness for panels.", window.ShelfSagLine);
        });

        // Say it is solid wood: clear its stock with the keyboard, then pick northern red oak with the pointer.
        Reveal(app, window, window.StockField);
        app.Click(CentreOf(window, window.StockField));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Press(Key.Back);
        Reveal(app, window, window.SpeciesPickerControl);
        app.Click(CentreOf(window, window.SpeciesPickerControl));
        app.Click(CentreOf(window, window.GetVisualDescendants().OfType<ComboBoxItem>().Single(item => (item.Content as string) == "Oak, northern red")));
        app.Expect("15 5/8\" of 1/4\" oak under KCMA's 15 lb/sq ft: ≈ 0.03\" now, ≈ 0.06\" after years, no verdict", () =>
            Assert.StartsWith("Shelf sag ≈ 0.03\" now, ≈ 0.06\" after years, under 15 lb/sq ft, KCMA's shelf test load", window.ShelfSagLine, StringComparison.Ordinal));

        // A limit of 1/8": within it. Then 40 lb/sq ft: ≈ 0.17" after years, over it.
        Reveal(app, window, window.ShelfControls.Limit);
        app.Click(CentreOf(window, window.ShelfControls.Limit));
        app.Type("1/8\"");
        app.Press(Key.Enter);
        app.Expect("within the 1/8\" typed", () => Assert.Contains("; within the 1/8\" you typed", window.ShelfSagLine, StringComparison.Ordinal));
        app.Click(CentreOf(window, window.ShelfControls.Load));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type("40");
        app.Press(Key.Enter);
        app.Expect("under 40 lb/sq ft it is over the limit", () =>
            Assert.StartsWith("Shelf sag ≈ 0.08\" now, ≈ 0.17\" after years, under 40 lb/sq ft; over the 1/8\" you typed", window.ShelfSagLine, StringComparison.Ordinal));
    });

    /// <summary>Scrolls the Part panel with the mouse wheel, either way, until <paramref name="control"/> is inside it.</summary>
    static void Reveal(AppDriver app, MainWindow window, Control control)
    {
        ScrollViewer scroller = window.FindControl<ScrollViewer>("PropertiesScroller")!;
        double Bottom(Visual v) => v.TranslatePoint(new Point(0, v.Bounds.Height), window)!.Value.Y;
        double Top(Visual v) => v.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        for (int i = 0; i < 30; i++)
        {
            if (Bottom(control) > Bottom(scroller))
            {
                app.Wheel(CentreOf(window, scroller), new Vector(0, -1));
            }
            else if (Top(control) < Top(scroller))
            {
                app.Wheel(CentreOf(window, scroller), new Vector(0, 1));
            }
            else
            {
                return;
            }
        }
    }
}

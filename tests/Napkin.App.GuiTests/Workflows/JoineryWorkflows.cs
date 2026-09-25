using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Joinery on screen (docs/design/joinery-and-fasteners.md &#xA7;10.4): the fastener sizes, the
/// hardware and the supplies, typed and read back the way a person reads them, on the DIY coffee table.
/// </summary>
public class JoineryWorkflows
{
    /// <summary>The fasteners the sample needs, from the counts worked out by hand in its expectations (27, 6, 8, 24, 10).</summary>
    [GuiWorkflow("GUI-JOIN-05")]
    public void Type_a_fastener_size_and_watch_the_shopping_list_change() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "DIY coffee table with drawers");

        // A size typed earlier for a fastener no joint needs now (nails): saving the panel must not lose it.
        FastenerChoice stale = new(FastenerKind.Nail, new Length(768), "8d", 50);
        window.Editor.Apply(new SetFastenerChoices([.. window.CurrentDesign!.Sketch.FastenerChoices, stale]), "set fastener sizes");

        app.Chord(Key.L);
        CutListWindow list = window.CutList!;
        AppDriver lists = AppDriver.Attach(list, "fasteners");
        lists.Click(CentreOf(list, list.ShoppingListTabItem));

        app.Expect("the shopping list has the five fastener lines, sized as the sample's builder typed them", () =>
        {
            string[] lines = [.. list.Extras.LinesOnScreen];
            Assert.Equal("Section\tItem\tSize\tCount\tPack\tPacks\tFor", lines[0]);
            Assert.StartsWith("Fasteners\tPocket screw, 3/4\" stock\t1-1/4 in coarse\t27\t100\t1\t", lines[1], StringComparison.Ordinal);
            Assert.StartsWith("Fasteners\tTabletop clip\tfigure-8, with screws\t10\t8\t2\t", lines[5], StringComparison.Ordinal);
        });

        lists.Click(CentreOf(list, list.SizesTabItem));
        Assert.Equal(5, list.SizeEditorRows.Children.Count);

        // Empty the tabletop clip's size (a clip is the last row); a size not chosen goes to the top of the editor.
        lists.Click(CentreOf(list, list.SizeBox(4)));
        lists.Chord(Key.A);
        lists.Press(Key.Delete);
        lists.Click(CentreOf(list, list.SaveSizesControl));

        app.Expect("with no size chosen the clip line says so and the blank row moves to the top of the editor", () =>
        {
            string[] lines = [.. list.Extras.LinesOnScreen];
            Assert.Contains("Fasteners\tTabletop clip\tsize not chosen\t10\t8\t2\t", lines.Single(line => line.Contains("Tabletop clip", StringComparison.Ordinal)), StringComparison.Ordinal);
            Assert.Equal(string.Empty, list.SizeBox(0).Text);
            Assert.Equal("Pocket screw, 3/4\" stock", ((TextBlock)((StackPanel)list.SizeEditorRows.Children[1]).Children[0]).Text!.Split(" (")[0]);
            Assert.Equal("Tabletop clip (10 needed)", ((TextBlock)((StackPanel)list.SizeEditorRows.Children[0]).Children[0]).Text);
        });

        // Type a size and a pack size, and confirm with the keyboard.
        lists.Click(CentreOf(list, list.SizeBox(0)));
        lists.Type("Z-clip, 1 in");
        lists.Press(Key.Tab);
        lists.Type("20");
        lists.Press(Key.Enter);
        lists.SaveFrame("sizes-typed");

        app.Expect("the size and the pack arithmetic follow: 10 clips in packs of 20 is one pack, and it is one undo", () =>
        {
            string clip = list.Extras.LinesOnScreen.Single(line => line.Contains("Tabletop clip", StringComparison.Ordinal));
            Assert.StartsWith("Fasteners\tTabletop clip\tZ-clip, 1 in\t10\t20\t1\t", clip, StringComparison.Ordinal);
            Assert.Equal("Z-clip, 1 in", window.CurrentDesign!.Sketch.FastenerChoices.Single(choice => choice.Kind == FastenerKind.TabletopClip).Size);
        });

        app.Expect("saving left the nail size nobody needs alone", () =>
            Assert.Contains(stale, window.CurrentDesign!.Sketch.FastenerChoices));

        // A pack size of nothing, or that is not a number, is refused in words, and nothing changes.
        lists.Click(CentreOf(list, list.PackBox(0)));
        lists.Chord(Key.A);
        lists.Type("0");
        lists.Click(CentreOf(list, list.SaveSizesControl));
        Assert.Contains("at least 1", list.SizesMessage, StringComparison.Ordinal);
        lists.Click(CentreOf(list, list.PackBox(0)));
        lists.Chord(Key.A);
        lists.Type("many");
        lists.Click(CentreOf(list, list.SaveSizesControl));

        app.Expect("a pack size that is not a number is refused where it was typed, and the design keeps its 20", () =>
        {
            Assert.Contains("whole number", list.SizesMessage, StringComparison.Ordinal);
            Assert.Equal(20, window.CurrentDesign!.Sketch.FastenerChoices.Single(choice => choice.Kind == FastenerKind.TabletopClip).PackSize);
        });

        // Undo from the drawing takes the typed clip size back to what the file had.
        FocusPaper(app, window);
        app.Chord(Key.Z);
        app.Expect("undo puts the clip size back to none chosen", () =>
            Assert.Equal(string.Empty, window.CurrentDesign!.Sketch.FastenerChoices.Single(choice => choice.Kind == FastenerKind.TabletopClip).Size));
    });

    /// <summary>Hardware typed onto a part, supplies typed onto the list, and the export saying what the screen says.</summary>
    [GuiWorkflow("GUI-JOIN-06")]
    public void Type_hardware_on_a_part_and_supplies_on_the_list_and_export_them() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "DIY coffee table with drawers");
        Box front = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Drawer front, A");
        window.Editor.Select(front.Id);

        app.Expect("the part panel shows the drawer front's pull, one line", () =>
            Assert.Equal("Drawer pull × 1", window.HardwareField.Text));

        // The Part panel scrolls; a person wheels down to the hardware box, and so does the test.
        app.Wheel(CentreOf(window, window.StockField), new Vector(0, -20));
        app.WaitForIdle();
        app.SaveFrame("hardware-field");
        app.Click(CentreOf(window, window.HardwareField));
        app.Chord(Key.A);
        app.Type("Drawer pull x 2");
        app.Press(Key.Enter);
        app.Type("Soft-close bumper × 4");
        app.Chord(Key.Enter);

        app.Expect("the part carries two hardware items, counted per copy of the part", () =>
        {
            Box now = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Id == front.Id);
            Assert.Equal([new HardwareItem("Drawer pull", 2), new HardwareItem("Soft-close bumper", 4)], now.Part!.Hardware);
            Assert.Equal("stock kept", now.Part.Stock is null ? "stock lost" : "stock kept");
        });

        app.Chord(Key.L);
        CutListWindow list = window.CutList!;
        AppDriver lists = AppDriver.Attach(list, "hardware");
        lists.Click(CentreOf(list, list.ShoppingListTabItem));

        app.Expect("the shopping list's hardware section sums it: the pull is 2 here and 1 on drawer B, and the slides stay 2", () =>
        {
            string[] lines = [.. list.Extras.LinesOnScreen];
            Assert.Contains("Hardware\t16 in side-mount drawer slide, pair\t\t2\t\t\tDrawer box front, A, Drawer box front, B", lines);
            Assert.Contains("Hardware\tDrawer pull\t\t3\t\t\tDrawer front, A, Drawer front, B", lines);
            Assert.Contains("Hardware\tSoft-close bumper\t\t4\t\t\tDrawer front, A", lines);
        });

        lists.Click(CentreOf(list, list.SizesTabItem));
        lists.Wheel(CentreOf(list, list.SaveSizesControl), new Vector(0, -20));
        lists.WaitForIdle();
        lists.Click(CentreOf(list, list.SuppliesField));
        lists.Chord(Key.A);
        lists.Type("Wood glue");
        lists.Press(Key.Enter);
        lists.Type("Finish | one quart");
        lists.Click(CentreOf(list, list.SaveSuppliesControl));
        lists.Click(CentreOf(list, list.ShoppingListTabItem));
        lists.SaveFrame("hardware-supplies");

        app.Expect("the supplies are the two typed lines, the note in the For column, and the one line napkin adds", () =>
        {
            string[] supplies = [.. list.Extras.LinesOnScreen.Where(line => line.StartsWith("Supplies", StringComparison.Ordinal))];
            Assert.Equal(
                ["Supplies\tWood glue\t\t\t\t\t", "Supplies\tFinish\t\t\t\t\tone quart", "Supplies\tGlue: 20 of 34 joints\t\t\t\t\t"],
                supplies);
        });

        app.Expect("the export is the rows on screen, field for field, under the header line", () =>
        {
            string[][] parsed = [.. CutListCsv.Parse(list.ExtrasCsv).Select(line => line.ToArray())];

            Assert.Equal(SuppliesList.Statement, parsed[0][0]);
            Assert.Equal(list.Extras.LinesOnScreen.Select(line => line.Split('\t')), parsed.Skip(1));
        });

        // Undo takes the supplies back out, one step; the list follows the drawing.
        FocusPaper(app, window);
        app.Chord(Key.Z);
        app.Expect("one undo removes the typed supplies and leaves the hardware", () =>
        {
            Assert.Equal(3, window.CurrentDesign!.Sketch.Supplies.Count);
            Assert.Contains("Hardware\tSoft-close bumper\t\t4\t\t\tDrawer front, A", list.Extras.LinesOnScreen);
        });
    });

    /// <summary>Clicks empty paper in the drawing's window, so the keyboard is the drawing's and not a text field's.</summary>
    static void FocusPaper(AppDriver app, MainWindow window)
    {
        window.Activate();
        app.Click(new Point(100, 520));
    }

    static void OpenSample(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.SamplesMenuItem));

        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        Size size = control.Bounds.Size;
        return topLeft + new Point(size.Width / 2, size.Height / 2);
    }
}

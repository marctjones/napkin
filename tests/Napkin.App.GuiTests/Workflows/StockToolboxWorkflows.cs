using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The stock toolbox, driven the way a person drives it: pick a category icon off the main toolbar,
/// hover a size to read what it really measures, and drag it onto the paper — then do it again with
/// something else.
/// </summary>
/// <remarks>
/// <para>
/// Issue #7's picker as decided: category icons, a text list, the actual size on hover, and placing
/// an item sets the part's stock reference with its footprint following the stock's cross-section.
/// The point Marc made about the old path is the one this checks hardest: the part that appears is
/// <em>already</em> a 2x4, with no separate assignment step afterwards.
/// </para>
/// <para>
/// The category icons are always on the toolbar beside Select and Rectangle — there is nothing to
/// open first — and each one drops its size list down under itself; clicking it again closes it.
/// </para>
/// <para>
/// Every expected size is read out of the shipped materials library, not typed here.
/// </para>
/// </remarks>
public class StockToolboxWorkflows
{
    [GuiWorkflow("GUI-CUT-02")]
    public void Pick_stock_from_the_toolbox_and_drag_it_onto_the_paper() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        MaterialsLibrary library = MaterialsLibrary.Shipped;
        Assert.True(library.TryFindLumber("2x4", out LumberStock twoByFour));
        Assert.True(library.TryFind(StockCategory.SheetGood, "3/4 plywood", out StockItem plywoodItem));
        PanelStock plywood = Assert.IsType<PanelStock>(plywoodItem);

        // A blank sheet, from the keyboard. Nothing has to be opened to reach the stock.
        app.Chord(Key.N);

        app.Expect("one icon per category is on the main toolbar, and no drawer is open yet", () =>
        {
            Assert.Equal(library.Categories, window.Toolbox.CategoryButtons.Keys);
            Assert.Contains(StockCategory.Fastener, window.Toolbox.CategoryButtons.Keys);
            foreach (ToggleButton icon in window.Toolbox.CategoryButtons.Values)
            {
                Assert.True(icon.IsEffectivelyVisible, "a category icon is not on screen.");
                Assert.Contains(icon, window.ToolControl.GetVisualDescendants());
            }

            Assert.False(window.IsShowingStockSizes, "a drawer is open before any icon was clicked.");
            Assert.Empty(window.Toolbox.ItemButtons);
        });

        // The lumber drawer, with the pointer, straight off the toolbar.
        ToggleButton lumberIcon = window.Toolbox.CategoryButtons[StockCategory.DimensionalLumber];
        app.Click(CentreOf(window, lumberIcon));

        app.Expect("the drawer drops down under its icon, a text list of the sizes in the library's order", () =>
        {
            Assert.True(window.IsShowingStockSizes, "the drawer did not open.");
            Assert.True(lumberIcon.IsChecked);
            Assert.Equal(
                library.InCategory(StockCategory.DimensionalLumber).Select(item => item.Name),
                window.Toolbox.ItemButtons.Select(button => button.Content as string));

            Point icon = lumberIcon.TranslatePoint(new Point(0, lumberIcon.Bounds.Height), window)!.Value;
            Point drawer = window.Toolbox.TranslatePoint(new Point(0, 0), window)!.Value;
            Assert.True(Math.Abs(drawer.X - icon.X) < 1, $"the drawer is at x {drawer.X}, its icon at {icon.X}.");
            Assert.True(drawer.Y >= icon.Y, "the drawer is not below its icon.");
        });

        // Hover the 2x4 before placing it: its actual size and where that came from.
        Button twoByFourButton = window.Toolbox.ButtonFor("2x4")!;
        app.MoveTo(CentreOf(window, twoByFourButton));

        app.Expect("hovering the 2x4 shows its actual size and citation", () =>
        {
            Assert.Equal(twoByFour.HoverText, window.Toolbox.ReadoutText);
            Assert.Contains(twoByFour.ActualSizeText, window.Toolbox.ReadoutText, StringComparison.Ordinal);
            Assert.Contains(twoByFour.Source.ShortForm, window.Toolbox.ReadoutText, StringComparison.Ordinal);
            Assert.Equal(twoByFour.HoverText, ToolTip.GetTip(twoByFourButton));
            Assert.True(ToolTip.GetIsOpen(twoByFourButton), "the hover tooltip did not open.");
        });

        app.SaveFrame("hover-2x4");

        app.Click(CentreOf(window, twoByFourButton));

        app.Expect("the pointer holds the 2x4 and the drawer stays open", () =>
        {
            Assert.Same(twoByFour, window.Canvas.ArmedStock);
            Assert.Equal(EditTool.Stock, window.Canvas.Tool);
            Assert.True(window.IsShowingStockSizes);
            Assert.Empty(window.CurrentDesign!.Sketch.Entities);
        });

        // One drag, mostly along x and leaning an inch across: the board is the drag's length and
        // the stock's width, whatever the pointer did across it. It starts below the drawer, which
        // hangs down over the top of the paper from its icon on the toolbar.
        Point boardFrom = At(window, Point2.Inches(-2, -10));
        Assert.False(OnDrawer(window, boardFrom), "the drag would start on the drawer, not the paper.");
        app.Drag(
            boardFrom,
            At(window, Point2.Inches(10, -10)),
            At(window, Point2.Inches(24, -9)));

        Box board = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());

        app.Expect("the part that appeared already is a 2x4, cut to the dragged length", () =>
        {
            Box placed = window.CurrentDesign!.Sketch.Find<Box>(board.Id)!;
            Assert.Equal("2x4", placed.Part!.Stock);
            Assert.Equal(Length.Inches(26).Units, placed.Width.Units);
            Assert.Equal(twoByFour.Width.Units, placed.Height.Units);
            Assert.Equal(twoByFour.Thickness.Units, placed.Part.OutOfPlane.Units);
            Assert.Equal(new PlanAxes(PartDimension.Length, PartDimension.Width), placed.Part.PlanAxes);

            // The yard's width is stated, so it is driven — a drag on that edge is refused.
            ParamValue driving = Assert.Single(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<ParamValue>());
            Assert.Equal(new BoxHeightRef(board.Id), driving.Param);

            // Placed, selected, and the properties panel says the same sentence the hover did —
            // one vocabulary for the toolbox and the panel that edits the stock afterwards.
            Assert.Equal(board.Id, window.Editor.OnlySelected);
            Assert.Equal("2x4", window.StockField.Text);
            Assert.Equal(twoByFour.HoverText, window.StockReadoutText);

            // And the pointer is back to Select, with the drawer still there for the next one.
            Assert.Equal(EditTool.Select, window.Canvas.Tool);
            Assert.Null(window.Canvas.ArmedStock);
            Assert.True(window.IsShowingStockSizes);
        });

        app.SaveFrame("placed-2x4");

        // A different drawer and a different item, without reopening anything. The plywood sizes
        // are below the OSB ones, so the drawer is scrolled with the wheel to reach them.
        app.Click(CentreOf(window, window.Toolbox.CategoryButtons[StockCategory.SheetGood]));
        Button plywoodButton = window.Toolbox.ButtonFor("3/4 plywood")!;
        app.Wheel(CentreOf(window, window.Toolbox.ItemScroller), new Vector(0, -10));

        app.Expect("the wheel scrolled the drawer until 3/4 plywood is in view", () =>
        {
            ScrollViewer scroller = window.Toolbox.ItemScroller;
            Point top = plywoodButton.TranslatePoint(new Point(0, 0), scroller)!.Value;
            Assert.True(
                top.Y >= 0 && top.Y + plywoodButton.Bounds.Height <= scroller.Bounds.Height,
                $"3/4 plywood is at {top.Y} in a drawer {scroller.Bounds.Height} tall.");
        });

        // The sheet-goods drawer hangs under its own icon, further along the toolbar than the
        // lumber one, so the sheet is dragged out on open paper to the right of it.
        app.Click(CentreOf(window, plywoodButton));
        Point sheetFrom = At(window, Point2.Inches(4, -2));
        Assert.False(OnDrawer(window, sheetFrom), "the drag would start on the drawer, not the paper.");
        app.Drag(
            sheetFrom,
            At(window, Point2.Inches(10, 2)),
            At(window, Point2.Inches(16, 6)));

        app.Expect("the second part is a sheet of 3/4 plywood the size it was dragged", () =>
        {
            Box sheet = Assert.Single(
                window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>(),
                box => box.Id != board.Id);
            Assert.Equal("3/4 plywood", sheet.Part!.Stock);
            Assert.Equal(plywood.Thickness.Units, sheet.Part.OutOfPlane.Units);
            Assert.Equal(Length.Inches(12).Units, sheet.Width.Units);
            Assert.Equal(Length.Inches(8).Units, sheet.Height.Units);
            Assert.Equal(Point2.Inches(4, -2), sheet.Anchor);
            Assert.Equal(sheet.Id, window.Editor.OnlySelected);

            // The board placed first is untouched.
            Assert.Equal(twoByFour.Width.Units, window.CurrentDesign!.Sketch.Find<Box>(board.Id)!.Height.Units);
        });

        // A nail is listed, with its size on hover, but there is nothing to drag.
        app.Click(CentreOf(window, window.Toolbox.CategoryButtons[StockCategory.Fastener]));
        app.Click(CentreOf(window, window.Toolbox.ItemButtons[0]));

        app.Expect("a fastener is not picked up, and the window says why", () =>
        {
            Assert.Null(window.Canvas.ArmedStock);
            Assert.Equal(EditTool.Select, window.Canvas.Tool);
            Assert.Contains("does not place fasteners", window.MessageOnScreen, StringComparison.Ordinal);
            Assert.Contains("does not place fasteners", window.Toolbox.CaptionText, StringComparison.Ordinal);
        });

        // Pick the 2x4 up again and put it down with Escape: nothing is placed.
        app.Click(CentreOf(window, window.Toolbox.CategoryButtons[StockCategory.DimensionalLumber]));
        app.Click(CentreOf(window, window.Toolbox.ButtonFor("2x4")!));
        app.Press(Key.Escape);

        app.Expect("Escape puts the stock down without placing anything", () =>
        {
            Assert.Null(window.Canvas.ArmedStock);
            Assert.Equal(EditTool.Select, window.Canvas.Tool);
            Assert.Equal(2, window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Count());
        });

        // Clicking the open drawer's icon again folds it away; the icons stay on the toolbar.
        app.Click(CentreOf(window, lumberIcon));
        app.Expect("the drawer closes from its icon, and the category row is still there", () =>
        {
            Assert.False(window.IsShowingStockSizes, "the drawer did not close.");
            Assert.False(lumberIcon.IsChecked);
            Assert.All(window.Toolbox.CategoryButtons.Values, icon => Assert.True(icon.IsEffectivelyVisible));
        });

        app.SaveFrame("closed");
    });

    /// <summary>The window coordinate a model point is drawn at.</summary>
    static Point At(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    /// <summary>Whether a window point is on the open drawer rather than on the paper.</summary>
    static bool OnDrawer(MainWindow window, Point point) =>
        window.IsShowingStockSizes
        && new Rect(window.Toolbox.TranslatePoint(new Point(0, 0), window)!.Value, window.Toolbox.Bounds.Size)
            .Contains(point);

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        Size size = control.Bounds.Size;
        return topLeft + new Point(size.Width / 2, size.Height / 2);
    }
}

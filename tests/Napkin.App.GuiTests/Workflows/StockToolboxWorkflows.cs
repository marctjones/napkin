using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;

using Xunit;
using Napkin.Modules.Editing;

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
            Assert.Equal(twoByFour.Thickness.Units, placed.Depth.Units);
            Assert.Equal(new PlanAxes(PartDimension.Length, PartDimension.Width), placed.Part.PlanAxes);

            // The yard's width is stated, so it is driven — a drag on that edge is refused.
            ParamValue driving = Assert.Single(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<ParamValue>(), value => value.Param is not BoxDepthRef);
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
        // lumber one (and, since the wall tool, over the paper right of the origin), so the sheet
        // is dragged out on open paper to the left of it.
        app.Click(CentreOf(window, plywoodButton));
        Point sheetFrom = At(window, Point2.Inches(-26, -2));
        Assert.False(OnDrawer(window, sheetFrom), "the drag would start on the drawer, not the paper.");
        app.Drag(
            sheetFrom,
            At(window, Point2.Inches(-20, 2)),
            At(window, Point2.Inches(-14, 6)));

        app.Expect("the second part is a sheet of 3/4 plywood the size it was dragged", () =>
        {
            Box sheet = Assert.Single(
                window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>(),
                box => box.Id != board.Id);
            Assert.Equal("3/4 plywood", sheet.Part!.Stock);
            Assert.Equal(plywood.Thickness.Units, sheet.Depth.Units);
            Assert.Equal(Length.Inches(12).Units, sheet.Width.Units);
            Assert.Equal(Length.Inches(8).Units, sheet.Height.Units);
            Assert.Equal(Point2.Inches(-26, -2), sheet.Anchor.XY);
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

    /// <summary>
    /// The same stock by menu alone: <em>Draw &#x2192; Stock &#x2192; Dimensional lumber &#x2192;
    /// 2x4</em>, then a drag — and the board that appears is the board the toolbar places, checked
    /// by placing one from the toolbar beside it and comparing the two.
    /// </summary>
    /// <remarks>
    /// Claims <c>GUI-CUT-06</c>, which the catalog does not define: nothing in it says "every
    /// toolbar function is reachable from the menu", and adding an entry is not this change's call,
    /// so the claim shows as an orphan on the scorecard — which gates nothing — until somebody
    /// writes the feature down.
    /// </remarks>
    [GuiWorkflow("GUI-CUT-06")]
    public void Pick_stock_from_the_draw_menu_and_drag_it_onto_the_paper() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        MaterialsLibrary library = MaterialsLibrary.Shipped;
        Assert.True(library.TryFindLumber("2x4", out LumberStock twoByFour));
        StockItem nail = library.InCategory(StockCategory.Fastener).First();

        app.Chord(Key.N);

        app.Expect("Draw → Stock lists every category the toolbar has, in its order, and every size in each", () =>
        {
            List<MenuItem> categories = [.. window.StockMenuItem.Items.OfType<MenuItem>()];
            Assert.Equal(window.Toolbox.CategoryButtons.Keys, categories.Select(item => (StockCategory)item.Tag!));
            foreach (MenuItem category in categories)
            {
                StockCategory which = (StockCategory)category.Tag!;
                Assert.Equal(StockToolbox.Words(which), category.Header);
                Assert.Equal(ToolTip.GetTip(window.Toolbox.CategoryButtons[which]), ToolTip.GetTip(category));
                Assert.Equal(
                    library.InCategory(which).Select(item => item.Name),
                    category.Items.OfType<MenuItem>().Select(item => item.Header as string));
            }
        });

        // Down through the menu with the mouse: Draw, Stock, the lumber, and a hover on the 2x4.
        MenuItem twoByFourItem = OpenStockMenuTo(app, window, StockCategory.DimensionalLumber, twoByFour);
        app.MoveTo(CentreOf(window, twoByFourItem));

        app.Expect("hovering the 2x4 in the menu gives its actual size and citation, as the drawer does", () =>
        {
            Assert.Equal(twoByFour.HoverText, ToolTip.GetTip(twoByFourItem));
            Assert.Contains(twoByFour.ActualSizeText, twoByFour.HoverText, StringComparison.Ordinal);
            Assert.Contains(twoByFour.Source.ShortForm, twoByFour.HoverText, StringComparison.Ordinal);
        });

        app.Click(CentreOf(window, twoByFourItem));

        // Since Draw holds only the tools (#169) the menu is short, and the lumber submenu sits over
        // where the drawer opens: the pointer goes to the paper, where the 2x4 is to be put down,
        // rather than resting on a drawer item whose own size the readout would then show.
        app.MoveTo(At(window, Point2.Inches(-2, -10)));

        app.Expect("the pointer holds the 2x4, the menu is closed, and the drawer shows what is held", () =>
        {
            Assert.Same(twoByFour, window.Canvas.ArmedStock);
            Assert.Equal(EditTool.Stock, window.Canvas.Tool);
            Assert.False(window.DrawMenuItem.IsSubMenuOpen, "the menu is still open.");
            Assert.True(window.IsShowingStockSizes, "the lumber drawer did not open.");
            Assert.Equal(StockCategory.DimensionalLumber, window.Toolbox.Category);
            Assert.Contains("Holding 2x4", window.Toolbox.ReadoutText, StringComparison.Ordinal);
            Assert.Empty(window.CurrentDesign!.Sketch.Entities);
        });

        // A foot-long board below the drawer, leaning an inch across as the toolbar's drag does.
        Point menuFrom = At(window, Point2.Inches(-2, -10));
        Assert.False(OnDrawer(window, menuFrom), "the drag would start on the drawer, not the paper.");
        app.Drag(menuFrom, At(window, Point2.Inches(4, -10)), At(window, Point2.Inches(10, -9)));

        Box byMenu = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());

        app.Expect("the part placed by menu is already a 2x4, cut to the dragged length", () =>
        {
            Assert.Equal("2x4", byMenu.Part!.Stock);
            Assert.Equal(Length.Inches(12).Units, byMenu.Width.Units);
            Assert.Equal(twoByFour.Width.Units, byMenu.Height.Units);
            Assert.Equal(twoByFour.Thickness.Units, byMenu.Depth.Units);
            Assert.Equal(new PlanAxes(PartDimension.Length, PartDimension.Width), byMenu.Part.PlanAxes);
            Assert.Equal(byMenu.Id, window.Editor.OnlySelected);
            Assert.Equal(twoByFour.HoverText, window.StockReadoutText);
            Assert.Equal(EditTool.Select, window.Canvas.Tool);
        });

        // The same board from the toolbar's drawer, which the menu left open, dragged the same way
        // on open paper to the left of the first.
        app.Click(CentreOf(window, window.Toolbox.ButtonFor("2x4")!));
        app.Expect("the drawer's 2x4 is held", () => Assert.Same(twoByFour, window.Canvas.ArmedStock));

        Point toolbarFrom = At(window, Point2.Inches(-24, -10));
        Assert.False(OnDrawer(window, toolbarFrom), "the drag would start on the drawer, not the paper.");
        app.Drag(toolbarFrom, At(window, Point2.Inches(-18, -10)), At(window, Point2.Inches(-12, -9)));

        app.Expect("the toolbar's board and the menu's board are the same board, in different places", () =>
        {
            Box byToolbar = Assert.Single(
                window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>(),
                box => box.Id != byMenu.Id);
            Assert.Equal(byMenu.Part, byToolbar.Part);
            Assert.Equal(byMenu.Width, byToolbar.Width);
            Assert.Equal(byMenu.Height, byToolbar.Height);

            // Each is driven by its stock's stated width, one ParamValue apiece.
            List<ParamValue> driving = [.. window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<ParamValue>().Where(value => value.Param is not BoxDepthRef)];
            Assert.Equal(2, driving.Count);
            Assert.Contains(driving, value => value.Param == new BoxHeightRef(byMenu.Id));
            Assert.Contains(driving, value => value.Param == new BoxHeightRef(byToolbar.Id));
        });

        // A nail by menu: listed with its size on hover, and nothing to drag — as on the toolbar.
        MenuItem nailItem = OpenStockMenuTo(app, window, StockCategory.Fastener, nail);
        app.MoveTo(CentreOf(window, nailItem));
        app.Expect("the nail's menu item gives its size on hover", () =>
            Assert.Equal(nail.HoverText, ToolTip.GetTip(nailItem)));
        app.Click(CentreOf(window, nailItem));

        app.Expect("a fastener picked from the menu is not picked up, and the window says why", () =>
        {
            Assert.Null(window.Canvas.ArmedStock);
            Assert.Equal(EditTool.Select, window.Canvas.Tool);
            Assert.Equal(StockCategory.Fastener, window.Toolbox.Category);
            Assert.Contains("does not place fasteners", window.MessageOnScreen, StringComparison.Ordinal);
            Assert.Equal(2, window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Count());
        });

        app.SaveFrame("menu-fastener");
    });

    /// <summary>
    /// Regression for #184: picking a size from <em>Draw &#x2192; Stock</em> arms it and opens the
    /// drawer right under the pointer's resting position (Draw is short now, #169), which used to
    /// make Avalonia's re-hit-test on layout raise <c>PointerEntered</c> on whatever drawer item
    /// landed there — so the readout showed that item's hover text instead of what was just armed,
    /// until the pointer actually moved. No <c>MoveTo</c> follows the click here, unlike
    /// <see cref="Pick_stock_from_the_draw_menu_and_drag_it_onto_the_paper"/>: the whole point is to
    /// check the readout with the pointer exactly where the click left it.
    /// </summary>
    /// <remarks>
    /// Claims <c>GUI-CUT-09</c>, an orphan the same way <c>GUI-CUT-06</c> is: the catalog has
    /// nothing that names this specific readout behaviour.
    /// </remarks>
    [GuiWorkflow("GUI-CUT-09")]
    public void The_drawer_readout_shows_what_is_held_right_after_picking_from_the_menu_not_the_hovered_item() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        MaterialsLibrary library = MaterialsLibrary.Shipped;
        Assert.True(library.TryFindLumber("2x4", out LumberStock twoByFour));

        app.Chord(Key.N);

        MenuItem twoByFourItem = OpenStockMenuTo(app, window, StockCategory.DimensionalLumber, twoByFour);
        app.Click(CentreOf(window, twoByFourItem));

        app.Expect("armed and the drawer open, right after the click — before the pointer moves again", () =>
        {
            Assert.Same(twoByFour, window.Canvas.ArmedStock);
            Assert.True(window.IsShowingStockSizes, "the lumber drawer did not open.");
        });

        app.Expect("the readout says what is held, not the hover text of whatever the drawer opened under", () =>
            Assert.Contains("Holding 2x4", window.Toolbox.ReadoutText, StringComparison.Ordinal));
    });

    /// <summary>
    /// Opens <em>Draw &#x2192; Stock &#x2192; category</em> with the mouse, one click per level,
    /// and returns the item for a size in it.
    /// </summary>
    static MenuItem OpenStockMenuTo(AppDriver app, MainWindow window, StockCategory category, StockItem item)
    {
        app.Click(CentreOf(window, window.DrawMenuItem));
        app.Click(CentreOf(window, window.StockMenuItem));

        MenuItem submenu = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => candidate.Tag is StockCategory each && each == category);
        app.Click(CentreOf(window, submenu));

        return window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => ReferenceEquals(candidate.Tag, item));
    }

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

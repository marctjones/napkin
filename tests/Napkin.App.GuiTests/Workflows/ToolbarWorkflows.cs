using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The toolbar as icons: hover each one to learn what it is and its key, then use every one of
/// them on a part — and find each on the Draw menu too, doing the same thing under the same name.
/// </summary>
/// <remarks>
/// Marc, on the toolbar that prompted this: "a standard gui toolbar with icons in svg for each of
/// the functions, not just another menu … with all of the functions on the tool bar accessible from
/// the menu." An icon has no words, so its tooltip is where the name and the shortcut live
/// (issue #62's convention), and it is what this claims.
/// </remarks>
public class ToolbarWorkflows
{
    [GuiWorkflow("CVS-011")]
    public void Hover_the_tool_icons_for_their_names_then_use_each_one() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        app.Chord(Key.N);

        app.Expect("every icon's tooltip names its function and key, and the Draw menu has the same function under the same name and key", () =>
        {
            Dictionary<string, MenuItem> menu = window.DrawMenuItem.Items
                .OfType<MenuItem>()
                .Where(item => item.Header is string)
                .ToDictionary(item => Plain((string)item.Header!));

            foreach (Button button in window.ToolButtons)
            {
                string name = AutomationProperties.GetName(button)
                    ?? throw new InvalidOperationException($"{button.Name} has no automation name.");
                string tip = Assert.IsType<string>(ToolTip.GetTip(button));
                Assert.StartsWith(name + ":", tip, StringComparison.Ordinal);

                Assert.True(menu.TryGetValue(name, out MenuItem? item), $"the Draw menu has no {name}.");
                KeyGesture gesture = item!.InputGesture
                    ?? throw new InvalidOperationException($"Draw → {name} shows no key.");
                Assert.EndsWith($"({gesture.Key})", tip, StringComparison.Ordinal);
            }
        });

        app.Expect("with nothing selected, Shape, Pin and Delete are greyed out on the toolbar and the menu alike", () =>
        {
            Assert.False(window.ShapeToolControl.IsEnabled);
            Assert.False(window.PinToolControl.IsEnabled);
            Assert.False(window.DeleteToolControl.IsEnabled);
            Assert.All(
                window.DrawMenuItem.Items.OfType<MenuItem>().Where(item => item.Name is "ShapeMenuItem" or "PinMenuItem" or "DeleteMenuItem"),
                item => Assert.False(item.IsEnabled));
        });

        // Hover the two tools: an icon's tooltip is its label, so it opens as soon as the pointer is
        // on it — for every icon in the row, the stock categories' included.
        app.MoveTo(CentreOf(window, window.SelectToolControl));
        app.Expect("hovering the arrow opens a tooltip saying Select, and S", () =>
        {
            Assert.True(ToolTip.GetIsOpen(window.SelectToolControl), "the Select tooltip did not open.");
            Assert.Equal("Select: pick, move and resize parts (S)", ToolTip.GetTip(window.SelectToolControl));
            Assert.All(window.ToolButtons, button => Assert.Equal(0, ToolTip.GetShowDelay(button)));
            Assert.All(window.Toolbox.CategoryButtons.Values, icon => Assert.Equal(0, ToolTip.GetShowDelay(icon)));
        });

        app.MoveTo(CentreOf(window, window.RectangleToolControl));
        app.Expect("hovering the rectangle opens a tooltip saying Rectangle, and R", () =>
        {
            Assert.True(ToolTip.GetIsOpen(window.RectangleToolControl), "the Rectangle tooltip did not open.");
            Assert.False(ToolTip.GetIsOpen(window.SelectToolControl), "the Select tooltip stayed open.");
            Assert.Equal("Rectangle: drag out a new part (R)", ToolTip.GetTip(window.RectangleToolControl));
        });

        // Take the rectangle tool by its icon and draw a part.
        app.Click(CentreOf(window, window.RectangleToolControl));
        app.Expect("the rectangle icon took the rectangle tool", () =>
        {
            Assert.Equal(EditTool.Rectangle, window.Canvas.Tool);
            Assert.True(window.RectangleToolControl.IsChecked);
            Assert.False(window.SelectToolControl.IsChecked);
        });

        app.Drag(
            At(window, Point2.Inches(-12, -9)),
            At(window, Point2.Inches(0, 0)),
            At(window, Point2.Inches(12, 9)));

        Box original = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());

        app.Expect("with the part selected, the action icons are live", () =>
        {
            Assert.Equal(original.Id, window.Editor.OnlySelected);
            Assert.True(window.ShapeToolControl.IsEnabled);
            Assert.True(window.PinToolControl.IsEnabled);
            Assert.True(window.DeleteToolControl.IsEnabled);
        });

        // Duplicate, by its icon: a copy beside it, selected.
        app.Click(CentreOf(window, window.DuplicateToolControl));

        Box copy = Assert.Single(
            window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>(),
            box => box.Id != original.Id);

        app.Expect("the duplicate icon copied the part and selected the copy", () =>
        {
            Assert.Equal(original.Width, copy.Width);
            Assert.Equal(original.Height, copy.Height);
            Assert.NotEqual(original.Anchor, copy.Anchor);
            Assert.Equal(copy.Id, window.Editor.OnlySelected);
        });

        // Pin, by its icon: the copy is held where it is.
        app.Click(CentreOf(window, window.PinToolControl));
        app.Expect("the pin icon pinned the copy in place", () =>
        {
            Anchored pin = Assert.Single(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Anchored>());
            Assert.Equal(copy.Id, pin.Entity);
        });

        app.SaveFrame("pinned-copy");

        // Delete, by its icon: the copy goes, and its pin with it.
        app.Click(CentreOf(window, window.DeleteToolControl));
        app.Expect("the delete icon removed the copy and its pin, and the actions grey out again", () =>
        {
            Assert.Equal(original.Id, Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>()).Id);
            Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Anchored>());
            Assert.False(window.DeleteToolControl.IsEnabled);
            Assert.False(window.PinToolControl.IsEnabled);
        });

        // Select the part again with the arrow, then Shape by its icon: the workshop opens on it.
        app.Click(CentreOf(window, window.SelectToolControl));
        app.Click(At(window, Point2.Inches(-8, -6)));
        app.Click(CentreOf(window, window.ShapeToolControl));

        app.Expect("the shape icon opened the part in the shape workshop", () =>
        {
            Assert.True(window.IsShapingPart, "the shape workshop did not open.");
            Assert.False(window.ToolControl.IsVisible, "the toolbar is still over the workshop.");
            Assert.False(window.StockMenuItem.IsEnabled, "Draw → Stock is still live over the workshop.");
        });

        app.Press(Key.Escape);
        app.Expect("Escape closes the workshop and the toolbar comes back", () =>
        {
            Assert.False(window.IsShapingPart);
            Assert.True(window.ToolControl.IsVisible);
            Assert.True(window.StockMenuItem.IsEnabled);
        });
    });

    /// <summary>A menu header as it reads: no access-key underscore, no trailing ellipsis.</summary>
    static string Plain(string header) => header.Replace("_", string.Empty, StringComparison.Ordinal).TrimEnd('…');

    /// <summary>The window coordinate a model point is drawn at.</summary>
    static Point At(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        Size size = control.Bounds.Size;
        return topLeft + new Point(size.Width / 2, size.Height / 2);
    }
}

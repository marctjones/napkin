using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Building;
using Napkin.Modules.Furniture;

using Xunit;
using Napkin.Modules.Editing;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Walls and openings (#18 part 1), driven the way a person drives them: take the wall tool, drag a
/// wall, put a window in it, widen the window and watch the framing follow, undo, and read the
/// framing's boards on the shopping list.
/// </summary>
/// <remarks>
/// The counts asserted are worked out by hand in <c>FramingListTests</c>'s comments (16 in on
/// centre, one jack each side), never read back from what napkin printed.
/// </remarks>
public class WallWorkflows
{
    const string SampleFraming =
        "Framing of Wall 1: 8 studs, 2 king studs, 2 jack studs, 2 cripples below, 1 rough sill, 3 plates, 1 header (not yet sized).";

    [GuiWorkflow("GUI-WALL-01")]
    public void Draw_a_wall_put_a_window_in_it_widen_it_undo_and_buy_the_framing() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        NewSheet(app, window);

        // Zoom out with the wheel until a 12 ft wall fits on the paper.
        app.Wheel(At(window, Point2.Inches(0, 0)), new Vector(0, -8));

        app.Press(Key.W);
        app.Expect("the wall tool is taken, with 2x4 studs, and the toolbar says so", () =>
        {
            Assert.Equal(EditTool.Wall, canvas.Tool);
            Assert.True(window.WallToolControl.IsChecked);
            Assert.Equal("2x4", canvas.WallMember!.Name);
        });

        app.Drag(At(window, Point2.Inches(-72, 0)), At(window, Point2.Inches(0, 2)), At(window, Point2.Inches(72, 2)));

        app.Expect("a 12 ft wall 3 1/2 in thick and 8 ft tall is on the Wall layer, selected, with its frame in the panel", () =>
        {
            Box wall = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
            Assert.Equal(Length.Inches(144), wall.Width);
            Assert.Equal(Length.Inches(3, 1, 2), wall.Height);
            Assert.Equal(Length.Inches(96), wall.Depth);
            Assert.Equal("Wall 1", wall.Name);
            Assert.True(Wall.Is(window.CurrentDesign!.Sketch, wall));
            Assert.Equal("Wall", window.CurrentDesign!.Sketch.Layers.Single(layer => layer.Id == wall.Layer).Name);
            Assert.Equal(wall.Id, window.Editor.OnlySelected);

            // 0…128 is 9 layout studs, and the end stud at 142 1/2: 10.
            Assert.Equal("Framing of Wall 1: 10 studs, 3 plates.", window.FramingText);
            Assert.Contains("design default, not a code requirement", window.FramingNotesText, StringComparison.Ordinal);
        });

        app.SaveFrame("wall-drawn");

        // Draw → Window with the pointer, then click the middle of the wall.
        app.Click(CentreOf(window, window.DrawMenuItem));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("WindowToolMenuItem")!));
        app.Expect("the opening tool holds a window", () => Assert.Equal(OpeningKind.Window, canvas.ArmedOpening));

        app.Click(At(window, Point2.Inches(0, 1)));

        EntityId windowId = default;
        app.Expect("a 3 ft window is centred in the wall, bound to it, and the frame has kings, jacks and cripples", () =>
        {
            Sketch sketch = window.CurrentDesign!.Sketch;
            Opening opening = Assert.Single(Opening.In(sketch, Assert.Single(Wall.All(sketch))));
            windowId = opening.Id;
            Assert.Equal("Window 1", opening.Name);
            Assert.Equal(Length.Inches(54), opening.Offset);
            Assert.Equal(Length.Inches(36), opening.Width);
            Assert.Equal(Length.Inches(36), opening.Sill);
            Assert.Single(sketch.Relationships.Values.OfType<AxisDistance>());
            Assert.Equal(opening.Id, window.Editor.OnlySelected);
            Assert.Equal(SampleFraming, window.FramingText.Split('\n')[0]);
            Assert.StartsWith("Window 1: a window in Wall 1", window.FramingHeadlineText, StringComparison.Ordinal);
            Assert.Contains("placeholder until the code check", window.FramingNotesText, StringComparison.Ordinal);
        });

        app.SaveFrame("window-placed");

        // Widen it to 4 ft by typing into its width label.
        app.Click(LabelAt(window, windowId, SizeAxis.Width));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type("4'");
        app.Press(Key.Enter);

        app.Expect("the frame follows: the 48 in opening at 54 takes a third stud and gains a third cripple", () =>
        {
            // Zone [51, 105) takes 64, 80 and 96: 7 studs. Cripples inside [54, 102): 64, 80, 96.
            Assert.Equal(Length.Inches(48), window.CurrentDesign!.Sketch.Find<Box>(windowId)!.Width);
            Assert.Equal(
                "Framing of Wall 1: 7 studs, 2 king studs, 2 jack studs, 3 cripples below, 1 rough sill, 3 plates, 1 header (not yet sized).",
                window.FramingText.Split('\n')[0]);
        });

        app.SaveFrame("window-widened");

        app.Chord(Key.Z);
        app.Expect("undo puts the 3 ft window and its frame back", () =>
        {
            Assert.Equal(Length.Inches(36), window.CurrentDesign!.Sketch.Find<Box>(windowId)!.Width);
            Assert.Equal(SampleFraming, window.FramingText.Split('\n')[0]);
        });

        // The shopping list, from the keyboard: the framing has its own section.
        app.Chord(Key.L, KeyModifiers.Shift);
        app.Expect("the shopping list shows the framing as 2x4s to buy, and the header as not yet sized", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.True(list.IsShowingShoppingList);
            Assert.True(list.IsShowingFraming);
            Assert.Empty(list.ShoppingRows.Rows);

            // Worked by hand in FramingListTests.TheFrameBuysBoardsThroughTheShoppingList.
            ShoppingListRow boards = list.FramingRows.Sorted[0];
            Assert.Equal("2x4", boards.Material);
            Assert.Equal("1 × 14'-0\", 8 × 16'-0\"", boards.BuyText);
            Assert.Equal("header, not yet sized", list.FramingRows.Sorted[1].Material);
            Assert.Contains("placeholder until the code check", list.FramingNoteText, StringComparison.Ordinal);

            string[] onScreen = [.. list.FramingRows.LinesOnScreen];
            var exported = CutListCsv.Parse(list.FramingCsv);
            Assert.Equal(onScreen.Skip(1), exported.Skip(2).Select(fields => string.Join("\t", fields)));
        });

        AppDriver.Attach(window.CutList!, "wall-shopping").SaveFrame("framing-section");
    });

    [GuiWorkflow("GUI-WALL-02")]
    public void Open_the_sample_wall_change_the_spacing_add_a_door_undo_and_look_in_3d() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        app.Click(CentreOf(window, window.SamplesMenuItem));
        MenuItem item = window.GetVisualDescendants().OfType<MenuItem>().Single(candidate => (candidate.Header as string) == "Wall with window");
        app.Click(CentreOf(window, item));

        // Pick the wall away from its window.
        app.Click(At(window, Point2.Inches(20, 2)));
        app.Expect("the sample's wall reads as a 2x6 wall with its window framed", () =>
        {
            Assert.StartsWith("Wall: a wall of 2x6 studs", window.FramingHeadlineText, StringComparison.Ordinal);
            Assert.Equal(
                "Framing of Wall: 8 studs, 2 king studs, 2 jack studs, 2 cripples below, 1 rough sill, 3 plates, 1 header (not yet sized).",
                window.FramingText.Split('\n')[0]);
        });

        // 24 in on centre, picked with the pointer and the keyboard.
        app.Click(CentreOf(window, window.StudSpacingControl));
        app.Press(Key.Down);
        app.Press(Key.Enter);
        app.Expect("at 24 in the wall has 6 studs and one cripple, and the default note is gone", () =>
        {
            // Layout 0…120 is 6, plus the end stud, 7; the window's zone takes 72: 6. Cripple at 72.
            Assert.Equal(Length.Inches(24), Assert.Single(Wall.All(window.CurrentDesign!.Sketch)).Box.WallInputs?.StudSpacing);
            Assert.StartsWith("Framing of Wall: 6 studs, 2 king studs, 2 jack studs, 1 cripple below,", window.FramingText, StringComparison.Ordinal);
            Assert.DoesNotContain("design default", window.FramingNotesText, StringComparison.Ordinal);
        });

        app.SaveFrame("sample-24-oc");

        // A door near the east end, from the Draw menu.
        app.Click(CentreOf(window, window.DrawMenuItem));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("DoorToolMenuItem")!));
        app.Click(At(window, Point2.Inches(120, 2)));
        app.Expect("the door stands on the floor at 102 in, and the wall frames both openings", () =>
        {
            Sketch sketch = window.CurrentDesign!.Sketch;
            Opening door = Opening.In(sketch, Assert.Single(Wall.All(sketch))).Single(opening => opening.Kind == OpeningKind.Door);
            Assert.Equal("Door 1", door.Name);
            Assert.Equal(Length.Inches(102), door.Offset);

            // 0, 24, …, 120 and 142 1/2: the window's zone takes 72, the door's [99, 141) takes 120: 5.
            Assert.StartsWith("Framing of Wall: 5 studs, 4 king studs, 4 jack studs, 1 cripple below, 1 rough sill, 3 plates, 2 headers", window.FramingText, StringComparison.Ordinal);
        });

        app.Press(Key.V);
        app.SaveFrame("sample-with-door-3d");
        app.Press(Key.V);

        app.Chord(Key.Z);
        app.Expect("undo takes the door away", () =>
        {
            Sketch sketch = window.CurrentDesign!.Sketch;
            Assert.Single(Opening.In(sketch, Assert.Single(Wall.All(sketch))));
        });
    });

    static void NewSheet(AppDriver app, MainWindow window)
    {
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        if (window.Editor.Selection.Count > 0)
        {
            app.Press(Key.Escape);
        }
    }

    static Point At(MainWindow window, Point2 world) => InWindow(window, window.Canvas.View.ToScreen(world));

    static Point LabelAt(MainWindow window, EntityId box, SizeAxis axis) =>
        InWindow(window, window.Canvas.SelectionDimensionLabelAt(box, axis)
            ?? throw new InvalidOperationException($"{box} shows no {axis} dimension."));

    static Point InWindow(MainWindow window, Point onCanvas)
    {
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

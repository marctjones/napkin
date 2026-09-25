using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Building;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Renovation sketches (docs/design/renovation-sketches.md), driven the way a person drives them,
/// in napkin's own look: what is already there drawn faint, what comes out dashed and crossed.
/// </summary>
public class RenovationWorkflows
{
    [GuiWorkflow("GUI-RENO-05")]
    public void Mark_a_wall_existing_put_a_new_window_in_it_then_demolish_it_and_undo() => GuiWorkflow.Run(
        app =>
        {
            MainWindow window = (MainWindow)app.Target;
            NewSheet(app, window);
            app.Wheel(At(window, Point2.Inches(0, 0)), new Vector(0, -8));

            app.Press(Key.W);
            app.Drag(At(window, Point2.Inches(-72, 0)), At(window, Point2.Inches(0, 2)), At(window, Point2.Inches(72, 2)));
            Box wall = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());

            // Edit → Phase → Existing, with the pointer.
            app.Click(CentreOf(window, window.EditMenuItem));
            app.Click(CentreOf(window, window.PhaseMenuItem));
            app.Click(CentreOf(window, window.FindControl<MenuItem>("PhaseExistingMenuItem")!));
            app.Expect("the wall is existing, one undo step says so, and the status bar names its phase", () =>
            {
                Assert.Equal(Phase.Existing, window.CurrentDesign!.Sketch.Find(wall.Id)!.Phase);
                Assert.Equal("Marked Wall 1 existing.", window.Editor.LastMessage!.Text);
                Assert.Equal("Wall 1, existing", window.PhaseReadout);
                Assert.Equal("existing", window.PhaseField.SelectedItem);
            });

            // Draw → Window, then the middle of the wall: a new window in an existing wall.
            app.Click(CentreOf(window, window.DrawMenuItem));
            app.Click(CentreOf(window, window.FindControl<MenuItem>("WindowToolMenuItem")!));
            app.Click(At(window, Point2.Inches(0, 1)));
            app.Expect("the window is new and the wall stays existing", () =>
            {
                Sketch sketch = window.CurrentDesign!.Sketch;
                Opening opening = Assert.Single(Opening.In(sketch, Assert.Single(Wall.All(sketch))));
                Assert.Equal(Phase.New, opening.Box.Phase);
                Assert.Equal(Phase.Existing, sketch.Find(wall.Id)!.Phase);
                Assert.Equal(string.Empty, window.PhaseReadout);
            });
            app.SaveFrame("existing-wall-new-window");

            // Pick the wall away from its window, and demolish it from the panel with the keyboard.
            app.Click(At(window, Point2.Inches(-50, 1)));
            app.Click(CentreOf(window, window.PhaseField));
            app.Press(Key.Down);
            app.Press(Key.Enter);
            app.Expect("the wall is to be demolished, its window untouched", () =>
            {
                Sketch sketch = window.CurrentDesign!.Sketch;
                Assert.Equal(Phase.Demolish, sketch.Find(wall.Id)!.Phase);
                Assert.Equal(Phase.New, Assert.Single(sketch.Entities.Values.OfType<Box>(), box => box.Id != wall.Id).Phase);
                Assert.Equal("Wall 1, demolish", window.PhaseReadout);
                Assert.True(window.FindControl<MenuItem>("PhaseDemolishMenuItem")!.IsChecked);
            });
            app.SaveFrame("demolished-wall");

            // The 3D chip with the pointer (the keyboard is still in the Phase picker), and back.
            app.Click(CentreOf(window, window.FindControl<Avalonia.Controls.Primitives.ToggleButton>("ModelViewChip")!));
            app.Click(new Point(120, 470));
            app.Expect("the 3D view is showing, nothing selected", () =>
            {
                Assert.True(window.IsShowingModel);
                Assert.Empty(window.Editor.Selection);
            });
            app.SaveFrame("demolished-wall-3d");
            app.Click(CentreOf(window, window.FindControl<Avalonia.Controls.Primitives.ToggleButton>("TopViewChip")!));

            app.Chord(Key.L, KeyModifiers.Shift);
            app.Expect("the shopping list says only New is bought and counts the wall under Demolition", () =>
            {
                CutListWindow list = window.CutList!;
                Assert.True(list.IsShowingDemolition);
                Assert.Equal(["Wall 1"], list.DemolitionLines.Select(line => line.Text));
                Assert.Equal("Only what is New is listed; 1 item to remove is under Demolition.", list.RenovationNoteText);
                Assert.Equal("Demolition\nItem,Count,Note\nWall 1,1,\n", list.DemolitionCsv);
            });
            AppDriver.Attach(window.CutList!, "reno-shopping").SaveFrame("demolition-section");

            window.Activate();
            app.Chord(Key.Z);
            app.Expect("undo puts the wall back to existing", () =>
            {
                Assert.Equal(Phase.Existing, window.CurrentDesign!.Sketch.Find(wall.Id)!.Phase);
                Assert.False(window.CutList!.IsShowingDemolition);
                Assert.Equal("Only what is New is listed; nothing comes out.", window.CutList!.RenovationNoteText);
            });
        },
        defaultLook: true);

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

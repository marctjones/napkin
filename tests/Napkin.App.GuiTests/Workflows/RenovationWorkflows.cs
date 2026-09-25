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
                // The wall by name, and its whole frame as it is (§6.3): a 12 ft 2x4 wall at 16" has
                // 0…128 (9) and the end stud, 10 studs 96 − 3 × 1 1/2 = 91 1/2" long, and 3 plates.
                Assert.Equal(
                    [
                        "Wall 1",
                        "Wall 1: 1 bottom plate 12'-0\" (2x4) come out (assuming a regular 16\" layout in the existing wall)",
                        "Wall 1: 2 top plates 12'-0\" (2x4) come out (assuming a regular 16\" layout in the existing wall)",
                        "Wall 1: 10 studs 7'-7 1/2\" (2x4) come out (assuming a regular 16\" layout in the existing wall)",
                    ],
                    list.DemolitionLines.Select(line => line.Text));
                Assert.Equal("Only what is New is listed; 14 items to remove are under Demolition.", list.RenovationNoteText);
                Assert.StartsWith("Demolition\nItem,Count,Note\nWall 1,1,\n\"Wall 1: bottom plate 12'-0\"\"\",1,", list.DemolitionCsv, StringComparison.Ordinal);
            });
            AppDriver.Attach(window.CutList!, "reno-shopping").SaveFrame("demolition-section");

            window.Activate();
            app.Chord(Key.Z);
            app.Expect("undo puts the wall back to existing, and only the two studs the window takes come out", () =>
            {
                // The window at 54 in, 36 wide, clears [51, 93): the layout studs at 64 and 80.
                Assert.Equal(Phase.Existing, window.CurrentDesign!.Sketch.Find(wall.Id)!.Phase);
                Assert.Equal(
                    ["Wall 1: 2 studs 7'-7 1/2\" (2x4) come out (assuming a regular 16\" layout in the existing wall)"],
                    window.CutList!.DemolitionLines.Select(line => line.Text));
                Assert.Equal("Only what is New is listed; 2 items to remove are under Demolition.", window.CutList!.RenovationNoteText);
            });
        },
        defaultLook: true);

    [GuiWorkflow("GUI-RENO-01")]
    public void Mark_a_wall_existing_by_menu_and_panel_put_a_window_in_it_and_read_new_and_out() => GuiWorkflow.Run(
        app =>
        {
            MainWindow window = (MainWindow)app.Target;
            NewSheet(app, window);
            app.Wheel(At(window, Point2.Inches(0, 0)), new Vector(0, -8));
            app.Press(Key.W);
            app.Drag(At(window, Point2.Inches(-72, 0)), At(window, Point2.Inches(0, 2)), At(window, Point2.Inches(72, 2)));
            Box wall = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());

            // By the menu, then by the panel's picker: new again, then existing again.
            app.Click(CentreOf(window, window.EditMenuItem));
            app.Click(CentreOf(window, window.PhaseMenuItem));
            app.Click(CentreOf(window, window.FindControl<MenuItem>("PhaseExistingMenuItem")!));
            app.Expect("the menu marked it existing", () => Assert.Equal(Phase.Existing, window.CurrentDesign!.Sketch.Find(wall.Id)!.Phase));
            Pick(app, window, window.PhaseField, Key.Up);
            app.Expect("the panel marked it new", () => Assert.Equal(Phase.New, window.CurrentDesign!.Sketch.Find(wall.Id)!.Phase));
            Pick(app, window, window.PhaseField, Key.Down);
            app.Expect("and existing again, the status bar says so", () =>
            {
                Assert.Equal(Phase.Existing, window.CurrentDesign!.Sketch.Find(wall.Id)!.Phase);
                Assert.Equal("Wall 1, existing", window.PhaseReadout);
            });

            // What the wall is: exterior, bearing.
            Pick(app, window, window.SideControl, Key.Down);
            Pick(app, window, window.BearingControl, Key.Down);

            app.Click(CentreOf(window, window.DrawMenuItem));
            app.Click(CentreOf(window, window.FindControl<MenuItem>("WindowToolMenuItem")!));
            app.Click(At(window, Point2.Inches(0, 1)));

            // The 3 ft window at 54 in, sill 3 ft, 3 ft 6 in tall, no code: one jack and one king each
            // side, the header unsized (so no cripples above), a sill and two cripples below at 64 and 80;
            // the layout studs at 64 and 80 come out.
            const string Diff = "new — 2 king studs, 2 jack studs, header, sill, 2 cripples; out — 2 studs";
            app.Expect("the message bar says the diff with the edit", () =>
                Assert.EndsWith($"In Wall 1 (existing): {Diff}, assuming a regular 16\" layout in the existing wall.", window.Editor.LastMessage!.Text, StringComparison.Ordinal));
            app.SaveFrame("new-window-in-existing-wall");

            app.Click(At(window, Point2.Inches(-50, 1)));
            app.Expect("the wall's panel reads the new and out lines", () =>
                Assert.Contains($"Wall 1 (existing): {Diff}, assuming a regular 16\" layout in the existing wall.", window.FramingText, StringComparison.Ordinal));

            app.Chord(Key.Z);
            app.Expect("undo takes the window out: nothing new, nothing out", () =>
            {
                Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
                Assert.DoesNotContain("(existing): new", window.FramingText, StringComparison.Ordinal);
            });

            // Undo the bearing, the side, the panel's two marks and the menu's: the wall is new again.
            for (int i = 0; i < 5; i++)
            {
                app.Chord(Key.Z);
            }

            app.Expect("five more undos put the wall back to new, as it was drawn", () =>
            {
                Box back = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
                Assert.Equal(Phase.New, back.Phase);
                Assert.Null(back.WallInputs);
                Assert.Equal(string.Empty, window.PhaseReadout);
            });
        },
        defaultLook: true);

    /// <summary>A picker in the Part panel, scrolled into sight: a click, one key, then Enter.</summary>
    static void Pick(AppDriver app, MainWindow window, Control picker, Key key)
    {
        Reveal(app, window, picker);
        app.Click(CentreOf(window, picker));
        app.Press(key);
        app.Press(Key.Enter);
    }

    /// <summary>Scrolls the Part panel with the mouse wheel, either way, until <paramref name="control"/> is inside it.</summary>
    static void Reveal(AppDriver app, MainWindow window, Control control)
    {
        ScrollViewer scroller = window.FindControl<ScrollViewer>("PropertiesScroller")!;
        for (int i = 0; i < 30; i++)
        {
            if (Edge(window, control, bottom: true) > Edge(window, scroller, bottom: true))
            {
                app.Wheel(CentreOf(window, scroller), new Vector(0, -1));
            }
            else if (Edge(window, control, bottom: false) < Edge(window, scroller, bottom: false))
            {
                app.Wheel(CentreOf(window, scroller), new Vector(0, 1));
            }
            else
            {
                return;
            }
        }

        static double Edge(MainWindow window, Control c, bool bottom) => c.TranslatePoint(new Point(0, bottom ? c.Bounds.Height : 0), window)!.Value.Y;
    }

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

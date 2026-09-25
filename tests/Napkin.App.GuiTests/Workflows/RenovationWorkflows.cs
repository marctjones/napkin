using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

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

    [GuiWorkflow("GUI-RENO-02")]
    public void Draw_a_room_type_its_sizes_tick_finishes_and_read_the_area_takeoff() => GuiWorkflow.Run(
        app =>
        {
            MainWindow window = (MainWindow)app.Target;
            // The basement sample, worked by hand in samples/basement-room.design.md: pick its room.
            app.Click(CentreOf(window, window.FileMenuItem));
            app.Click(CentreOf(window, window.SamplesMenuItem));
            app.Click(CentreOf(window, window.GetVisualDescendants().OfType<MenuItem>().Single(item => (item.Header as string) == "Basement room")));
            app.Click(At(window, Point2.Inches(40, 100)));
            app.Expect("the sample's room is bounded by its four walls, with both openings, and takes off 18 sheets and 10 bags", () =>
            {
                Assert.Equal("Bounded by Wall, south, Wall, north, Wall, west, Wall, east; openings: Door 1, Window 1.", window.RoomBoundsLine);
                Assert.Contains("18 sheets 4'-0\" × 8'-0\"", window.RoomTakeoffLines, StringComparison.Ordinal);
                Assert.Contains("exterior walls, 390 sq ft; 10 bags at 40 sq ft", window.RoomTakeoffLines, StringComparison.Ordinal);
            });
            app.SaveFrame("basement-sample");
            app.Press(Key.Escape);
            app.Chord(Key.D0);
            app.SaveFrame("basement-plan");

            NewSheet(app, window);
            app.Wheel(At(window, Point2.Inches(0, 0)), new Vector(0, -8));
            app.Wheel(At(window, Point2.Inches(0, 0)), new Vector(0, -4));

            // Shift+W and a click on bare paper: a room at the starting size, 10'-0" square, 8'-0" tall.
            app.Press(Key.W, KeyModifiers.Shift);
            app.Expect("the room tool is taken", () => Assert.Equal(Napkin.Modules.Editing.EditTool.Room, window.Canvas.Tool));
            // Low on the left of the paper, so a 14 x 12 ft room and its labels stay in sight.
            app.Click(InWindow(window, new Point(window.Canvas.Bounds.Width * 0.12, window.Canvas.Bounds.Height * 0.8)));
            Box room = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
            app.Expect("a 10 ft room on the Room layer, selected, with its block in the panel", () =>
            {
                Assert.Equal((Length.Inches(120), Length.Inches(120), Length.Inches(96)), (room.Width, room.Height, room.Depth));
                Assert.True(Room.Is(window.CurrentDesign!.Sketch, room));
                Assert.Equal(room.Id, window.Editor.OnlySelected);
                Assert.True(window.IsShowingRoom);
                Assert.Equal("No walls bound this room; openings are not subtracted.", window.RoomBoundsLine);
            });

            // Its length and width typed on its labels, the way any dimension is typed.
            TypeOnLabel(app, window, room.Id, Napkin.App.Editing.SizeAxis.Width, "14'");
            TypeOnLabel(app, window, room.Id, Napkin.App.Editing.SizeAxis.Height, "12'");
            app.Expect("the room is 14 ft by 12 ft inside", () =>
                Assert.Equal("Room 1: 14'-0\" × 12'-0\" inside, ceiling 8'-0\".", window.RoomHeadlineText));

            // Drywall on walls and ceiling, a 4 x 8 sheet; the floor, a 20 sq ft box.
            var controls = window.RoomControls;
            Pick(app, window, controls.Drywall, Key.Down, times: 2);
            TypeInto(app, window, controls.Sheet, "4' x 8'");
            Reveal(app, window, controls.Flooring);
            app.Click(CentreOf(window, controls.Flooring));
            TypeInto(app, window, controls.Box, "20");
            app.Expect("the finishes are the room's, one undo step each", () =>
            {
                RoomInputs inputs = window.CurrentDesign!.Sketch.Find<Box>(room.Id)!.Room!;
                Assert.Equal(RoomSurfaces.WallsAndCeiling, inputs.Drywall);
                Assert.Equal(new SheetSize(Length.Inches(48), Length.Inches(96)), inputs.Sheet);
                Assert.True(inputs.Flooring);
                Assert.Equal((10, 20), (inputs.FlooringWaste, inputs.FlooringBox));
            });
            app.SaveFrame("room-with-finishes");

            // P = 2(168 + 144) = 624" = 52'; walls 624 × 96 = 416 sq ft, nothing bounds it so nothing
            // is subtracted; ceiling 168. Drywall 584 / 32 = 18.25 → 19 sheets. Floor 168 × 1.1 = 184.8,
            // shown up to 185; 184.8 / 20 = 9.24 → 10 boxes.
            app.Chord(Key.L, KeyModifiers.Shift);
            app.Expect("the shopping list's Area takeoff says each line, rounded once, and the CSV carries it", () =>
            {
                CutListWindow list = window.CutList!;
                Assert.True(list.IsShowingAreaTakeoff);
                Assert.Equal(
                    [
                        "Surfaces: walls 416 sq ft less openings 0 = 416 sq ft; ceiling 168 sq ft; perimeter 52'-0\" (no walls bound this room; openings are not subtracted)",
                        "Drywall: walls and ceiling, 584 sq ft; 19 sheets 4'-0\" × 8'-0\" (sheets by area — a layout may need more)",
                        "Flooring: 185 sq ft; 10 boxes at 20 sq ft (10 % allowance, napkin's allowance, not a fact about your floor; rounded up)",
                    ],
                    list.TakeoffLines.Select(line => line.Text));
                string[] csv = list.AreaTakeoffCsv.Split('\n');
                Assert.Equal(["Area takeoff", "Room,Finish,Quantity,Count,Note"], csv[..2]);
                Assert.StartsWith("Room 1,Drywall,\"walls and ceiling, 584 sq ft; 19 sheets", csv[3], StringComparison.Ordinal);
                Assert.StartsWith("Room 1,Flooring,185 sq ft; 10 boxes at 20 sq ft,10,", csv[4], StringComparison.Ordinal);
            });
            AppDriver.Attach(window.CutList!, "reno-takeoff").SaveFrame("area-takeoff");

        },
        defaultLook: true);

    static void TypeOnLabel(AppDriver app, MainWindow window, EntityId box, Napkin.App.Editing.SizeAxis axis, string text)
    {
        app.Click(InWindow(window, window.Canvas.SelectionDimensionLabelAt(box, axis)
            ?? throw new InvalidOperationException($"{box} shows no {axis} label.")));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type(text);
        app.Press(Key.Enter);
    }

    /// <summary>A text box in the Part panel, scrolled into sight: a click, the text over what was there, then Enter.</summary>
    static void TypeInto(AppDriver app, MainWindow window, TextBox box, string text)
    {
        Reveal(app, window, box);
        app.Click(CentreOf(window, box));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type(text);
        app.Press(Key.Enter);
    }

    /// <summary>A picker in the Part panel, scrolled into sight: a click, one key, then Enter.</summary>
    static void Pick(AppDriver app, MainWindow window, Control picker, Key key, int times = 1)
    {
        Reveal(app, window, picker);
        app.Click(CentreOf(window, picker));
        for (int i = 0; i < times; i++)
        {
            app.Press(key);
        }

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

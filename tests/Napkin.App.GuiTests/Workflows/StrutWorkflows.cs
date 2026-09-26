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
/// Angled parts in the window (#192, docs/design/angled-parts.md §9.5): the splayed bench of §9.1,
/// drawn with the pointer and the keyboard, and its cut list read back.
/// </summary>
public class StrutWorkflows
{
    [GuiWorkflow("GUI-STRUT-01")]
    public void Draw_the_splayed_bench_by_clicks_and_read_one_row_of_four_legs() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        NewSheet(app, window);

        // Across, so the bench and its feet are all on the paper, clear of the view chips at the top
        // right and the panel at the bottom right, at a zoom whose grid is whole inches.
        app.Press(Key.Up);
        app.Press(Key.Up);
        app.Press(Key.Right);
        app.Press(Key.Right);
        app.Press(Key.Right);
        app.Press(Key.Right);

        app.Expect("the grid snaps to an inch or finer, so the bench's 7-24-25 legs land exactly", () =>
            Assert.Equal(0, 1 % window.Canvas.SnapStepInches, 9));

        // The seat: a 36″ × 12″ rectangle, raised so its underside is 24″ up (§9.1).
        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(0, 0)), At(window, Point2.Inches(18, 6)), At(window, Point2.Inches(36, 12)));
        Box seat = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
        app.Click(CentreOf(window, window.PositionFields.Up));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type("24");
        app.Press(Key.Enter);
        app.Expect("the seat's underside is 24″ above the floor", () =>
            Assert.Equal(Length.Inches(24), window.CurrentDesign!.Sketch.Find<Box>(seat.Id)!.Anchor.Z));

        // A click on the drawing puts the keyboard back on it.
        app.Click(new Point(450, 320));

        // Four legs, each two clicks with the angled-part tool: a foot on the paper 7″ outside the
        // seat's edge, then its top on the seat, 4″ in from an end and 3″ in from an edge. Each is a
        // 2x2, typed into the panel's stock.
        (long X, long FootY, long TopY)[] legs = [(4, -4, 3), (4, 16, 9), (32, -4, 3), (32, 16, 9)];
        foreach ((long x, long footY, long topY) in legs)
        {
            app.Press(Key.L);
            app.Click(At(window, Point2.Inches(x, footY)));
            app.Click(At(window, Point2.Inches(x, topY)));
            app.Click(CentreOf(window, window.StrutFields.Stock));
            app.Type("2x2");
            app.Press(Key.Enter);
        }

        app.Expect("four legs stand from the floor to the seat's underside, each a 2x2", () =>
        {
            Strut[] drawn = [.. window.CurrentDesign!.Sketch.Entities.Values.OfType<Strut>()];
            Assert.Equal(4, drawn.Length);
            Assert.All(drawn, leg =>
            {
                Assert.Equal((Length.Zero, Length.Inches(24)), (leg.From.Z, leg.To.Z));
                Assert.Equal((EndCut.Z, EndCut.Z), (leg.FromCut, leg.ToCut));
                Assert.Equal((Length.Inches(1, 1, 2), Length.Inches(1, 1, 2)), (leg.Height, leg.Depth));
            });
            Assert.Contains("tilt ≈16.5°", window.StrutReadoutText, StringComparison.Ordinal);
        });
        app.SaveFrame("splayed-bench-legs");

        // The cut list: the four legs are one row of four at 25 7/16″, exact (§9.1).
        app.Chord(Key.L);
        app.Expect("the cut list has one row of four legs at 2'-1 7/16″", () =>
        {
            CutListRow legRow = Assert.Single(window.CutList!.Rows.Rows, row => row.Label == "Leg");
            Assert.Equal(4, legRow.Quantity);
            Assert.Equal(26048, legRow.Length.Units);
            Assert.Equal("2'-1 7/16\"", legRow.LengthText);
        });
    });

    [GuiWorkflow("GUI-STRUT-02")]
    public void Turn_a_footstool_legs_wide_face_and_place_a_new_leg_by_its_lean() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Splayed footstool");

        // The south-west leg runs from (0, −1) to (3, 3) in the plan; its middle is on it.
        EntityId legId = new(new Guid("c2000000-0000-4000-8000-000000000010"));
        app.Click(At(window, Point2.Inches(1, 1)));
        app.Expect("the south-west leg is selected and its panel shows", () =>
        {
            Assert.Equal([legId], window.Editor.Selection);
            Assert.True(window.IsShowingStrut);
        });

        // Its wide face turned parallel to the long side: the same four points, a different board
        // (angled-parts §9.2 board 2), whose ends are compound.
        app.Click(CentreOf(window, window.StrutPickers.Reference));
        app.Press(Key.Down);
        app.Press(Key.Enter);
        app.Click(CentreOf(window, window.FindControl<Button>("StrutApplyButton")!));
        app.Expect("the leg keeps its wide face parallel to the long side", () =>
            Assert.Equal(Axis.X, window.CurrentDesign!.Sketch.Find<Strut>(legId)!.Reference));

        // A new leg by its lean: a click on the paper to put the keyboard back on the drawing, a flat
        // brace beside the stool, then its top placed 45° over a 12″ rise,
        // due east — exactly on the grid, so nothing is said about rounding (§9.3 case 14).
        app.Click(At(window, Point2.Inches(16, 8)));
        app.Press(Key.L);
        app.Click(At(window, Point2.Inches(14, 0)));
        app.Click(At(window, Point2.Inches(16, 4)));
        Strut brace = window.CurrentDesign!.Sketch.Entities.Values.OfType<Strut>().Single(strut => strut.Name.StartsWith("Brace", StringComparison.Ordinal));
        app.Click(CentreOf(window, window.StrutLeanFields.Tilt));
        app.Type("45");
        app.Click(CentreOf(window, window.StrutLeanFields.Azimuth));
        app.Type("0");
        app.Click(CentreOf(window, window.StrutLeanFields.Rise));
        app.Type("12");
        app.Click(CentreOf(window, window.StrutLeanFields.Place));
        app.Expect("its top is 12″ east and 12″ up of its foot, exactly", () =>
        {
            Strut placed = window.CurrentDesign!.Sketch.Find<Strut>(brace.Id)!;
            Assert.Equal(brace.From + new Vector3(Length.Inches(12), Length.Zero, Length.Inches(12)), placed.To);
            Assert.DoesNotContain("rounded", window.Editor.LastMessage!.Text, StringComparison.Ordinal);
        });

        // The cut list: three legs are still one board; the turned one is its own row, cut compound.
        app.Chord(Key.L);
        app.Expect("the turned leg leaves the row of four and reads as a compound cut", () =>
        {
            CutListRow[] rows = window.CutList!.Rows.Rows.ToArray();
            Assert.Equal(3, Assert.Single(rows, row => row.Label == "Leg").Quantity);
            CutListRow turned = Assert.Single(rows, row => row.Members.Contains(legId));
            Assert.Equal(1, turned.Quantity);
            Assert.StartsWith("Cut both ends at a compound angle: mitre ≈13.5°, bevel ≈18.5°", turned.CutText[0], StringComparison.Ordinal);
        });
    });

    static void OpenSample(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.FileMenuItem));
        app.Click(CentreOf(window, window.SamplesMenuItem));
        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
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

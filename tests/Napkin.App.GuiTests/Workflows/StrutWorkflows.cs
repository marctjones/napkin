using Avalonia;
using Avalonia.Input;
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

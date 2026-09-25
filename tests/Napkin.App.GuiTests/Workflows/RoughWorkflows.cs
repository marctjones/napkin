using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using Napkin.App;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;
using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Rough sketching (<c>docs/design/sketch-mode.md</c> &#xA7;7.4): a bench drawn fast with big round
/// steps and nothing stated, then firmed up, in napkin's default look.
/// </summary>
/// <remarks>
/// The quick bench of &#xA7;7.2 is drawn from an origin of whole inches near the middle of the canvas
/// (the canvas is not centred on (0, 0)): Top (0, 16) 48 &#xD7; 2, Leg 1 (2, 0) 4 &#xD7; 16, Leg 2 (42, 0),
/// Stretcher (6, 4) 36 &#xD7; 3, each from that origin.
/// A new sheet opens at about 10.5 px per inch, where the rough step is 6&#x2033;; five presses of +
/// (&#xD7;1.25 each) make it about 32 px per inch, where the precise step is 1/2&#x2033; (16 px) and the rough
/// step the rung above, floored to 1&#x2033;. The window is widened so all 48&#x2033; of the top is on it.
/// </remarks>
public class RoughWorkflows
{
    [GuiWorkflow("GUI-SKETCH-01")]
    public void Sketch_a_quick_bench() => GuiWorkflow.Run(
        app =>
        {
            MainWindow window = (MainWindow)app.Target;
            CanvasView canvas = window.Canvas;

            (long ox, long oy) = SketchTheBench(app, window);
            app.SaveFrame("rough-bench");

            Box[] parts = [.. window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
            app.Expect("every part landed on whole inches, is a plank marked rough, and nothing is stated", () =>
            {
                Assert.Equal(4, parts.Length);
                AssertBox(parts[0], ox + 0, oy + 16, 48, 2);
                AssertBox(parts[1], ox + 2, oy + 0, 4, 16);
                AssertBox(parts[2], ox + 42, oy + 0, 4, 16);
                AssertBox(parts[3], ox + 6, oy + 4, 36, 3);
                Assert.All(parts, part =>
                {
                    Assert.NotNull(part.Part);
                    Assert.True(part.Part!.Rough);
                    Assert.Null(part.Part.Stock);
                    Assert.Equal(Length.Inches(0, 3, 4), part.Depth);
                });
                Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder);
                Assert.True(canvas.LastSnap?.CaughtSomething, "moving Leg 1 up to the top caught nothing.");
            });

            // Pick Leg 1 and take the pointer well away: its labels wait. Over it, they show.
            app.Click(At(window, Point2.Inches(ox + 4, oy + 5)));
            app.MoveTo(At(window, Point2.Inches(ox + 24, oy + 11)));
            app.Expect("the selected leg shows no labels while the pointer is away from it", () =>
            {
                Assert.Equal(parts[1].Id, window.Editor.OnlySelected);
                Assert.False(canvas.SelectionLabelsShown);
            });

            app.MoveTo(At(window, Point2.Inches(ox + 4, oy + 9)));
            app.Expect("over the leg its labels show, and the status bar reads its name and three sizes", () =>
            {
                Assert.True(canvas.SelectionLabelsShown);
                // The leg's length is the 16", its width the 4", its thickness the 3/4" depth.
                Assert.Equal("Part 2  1'-4\" × 4\" × 3/4\"", window.SizeReadout);
            });
            app.SaveFrame("hovered-leg");

            // Q again: Precise, where the labels are always on and the readout is gone.
            app.Press(Key.Q);
            app.MoveTo(At(window, Point2.Inches(ox + 24, oy + 11)));
            app.Expect("back in Precise the word says so, the labels show wherever the pointer is, and the readout is gone", () =>
            {
                Assert.Equal("PRECISE", window.EntryModeWord);
                Assert.True(window.PreciseToggle.IsChecked);
                Assert.False(window.RoughToggle.IsChecked);
                Assert.True(canvas.SelectionLabelsShown);
                Assert.Equal(string.Empty, window.SizeReadout);
                Assert.Equal(0.5, canvas.SnapStepInches);
            });
        },
        defaultLook: true);

    /// <summary>
    /// Draws the quick bench roughly, the way a person would, and leaves the sheet in Rough mode
    /// with nothing selected: Q, then each part with R and a drag a little off the round numbers;
    /// Leg 1 is drawn an inch low and dragged up against the top, which catches and states nothing.
    /// </summary>
    /// <returns>Where the bench's (0, 0) is, in whole inches: the round point nearest the middle of the canvas, less (24, 9).</returns>
    internal static (long X, long Y) SketchTheBench(AppDriver app, MainWindow window)
    {
        CanvasView canvas = window.Canvas;
        app.ResizeWindow(2600, 1100);
        app.Chord(Key.N);
        app.Click(new Point(900, 500));
        for (int i = 0; i < 5; i++)
        {
            app.Press(Key.Add);
        }

        app.Press(Key.Q);
        app.Expect("Q puts the editor in Rough: the status bar, the toolbar and the menu all say so", () =>
        {
            Assert.Equal(EntryMode.Rough, window.Editor.EntryMode);
            Assert.Equal("ROUGH", window.EntryModeWord);
            Assert.True(window.RoughToggle.IsChecked);
            Assert.False(window.PreciseToggle.IsChecked);
            Assert.True(window.RoughSketchingMenuItem.IsChecked);
            Assert.InRange(canvas.View.PixelsPerInch, 28, 56);
            Assert.Equal(0.5, SnapGrid.StepInches(canvas.View.PixelsPerInch));
            Assert.Equal(1, canvas.SnapStepInches);
            Assert.EndsWith("Snap 1\"", window.ZoomReadout.Text);
        });

        Point2 middle = canvas.View.ToWorld(new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2));
        long ox = (long)Math.Round(middle.X.ToInches()) - 24;
        long oy = (long)Math.Round(middle.Y.ToInches()) - 9;

        // Top: (0.3, 16.3) to (48.2, 17.8) from the bench's origin lands on (0, 16) to (48, 18).
        DrawRough(app, window, ox, oy, (0.3, 16.3), (24.4, 17.2), (48.2, 17.8));

        // Leg 1, an inch low: (2.2, -0.7) to (5.7, 14.8) lands on (2, -1) to (6, 15).
        DrawRough(app, window, ox, oy, (2.2, -0.7), (4, 7), (5.7, 14.8));

        // Leg 2: (42.4, 0.4) to (45.7, 16.2) lands on (42, 0) to (46, 16).
        DrawRough(app, window, ox, oy, (42.4, 0.4), (44, 9), (45.7, 16.2));

        // Stretcher: (6.2, 4.4) to (41.6, 6.6) lands on (6, 4) to (42, 7).
        DrawRough(app, window, ox, oy, (6.2, 4.4), (24, 5.5), (41.6, 6.6));

        // Leg 1 up against the top: grab it and drag up 0.9"; the grid and the top's edge both say 16.
        app.Drag(At(window, Inches(ox + 4, oy + 7)), At(window, Inches(ox + 4, oy + 7.5)), At(window, Inches(ox + 4, oy + 7.9)));
        app.Press(Key.Escape);
        return (ox, oy);
    }

    static void DrawRough(AppDriver app, MainWindow window, long ox, long oy, (double X, double Y) from, (double X, double Y) via, (double X, double Y) to)
    {
        app.Press(Key.R);
        app.Drag(
            At(window, Inches(ox + from.X, oy + from.Y)),
            At(window, Inches(ox + via.X, oy + via.Y)),
            At(window, Inches(ox + to.X, oy + to.Y)));
    }

    static void AssertBox(Box box, long x, long y, long width, long height)
    {
        Assert.Equal(Length.Inches(x), box.Anchor.X);
        Assert.Equal(Length.Inches(y), box.Anchor.Y);
        Assert.Equal(Length.Inches(width), box.Width);
        Assert.Equal(Length.Inches(height), box.Height);
    }

    static Point2 Inches(double x, double y) =>
        new(Length.FromInches(x, Rounding.HalfToEven), Length.FromInches(y, Rounding.HalfToEven));

    internal static Point At(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Assert.True(new Rect(window.Canvas.Bounds.Size).Contains(onCanvas), $"{world} is off the canvas at {onCanvas}.");
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    internal static Point CentreOf(Visual root, Visual control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)
        ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Napkin.App;
using Napkin.App.Designs;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The M1 viewer, driven the way a person drives it: open a sample, move around it, and read what
/// the drawing says.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Two honest deviations from the catalogue's wording, both because of what M1 ships
/// with.</strong> The catalogue describes opening a sample "through the file dialog" and comparing
/// labels against "the fixture's expectations file". The scene reader (#6) and the sample files
/// (#37) are being written in parallel with this viewer, so M1's samples are built in code behind
/// <see cref="IDesignSource"/> and are opened through the Samples menu — with the mouse, through
/// the real menu, not by calling a method. The expected strings are stated inline here, computed by
/// hand from the same shop numbers the samples are built from. When the reader lands, these two
/// workflows gain a file-dialog step and read their expectations from the fixture; nothing else
/// about them changes.
/// </para>
/// <para>
/// GUI-VIEW-05 — a file the application cannot read failing visibly — is not implemented here at
/// all, because there is no file to fail on yet. <see cref="MainWindow.ShowDesign"/> already has
/// the failure path it needs.
/// </para>
/// </remarks>
public class ViewerWorkflows
{
    [GuiWorkflow("GUI-VIEW-01")]
    public void Open_a_sample_design_and_see_it_drawn() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        // Open the wall first, through the menu, so that opening the coffee table afterwards is
        // really an open and not just "what was already on screen".
        OpenThroughTheMenu(app, window, "Wall with window");
        app.Expect("the wall sample is on screen", () =>
        {
            Assert.Equal("Wall with window", window.CurrentDesign?.Name);
            Assert.Equal("napkin — Wall with window", window.Title);
        });

        OpenThroughTheMenu(app, window, "Coffee table");
        app.Expect("every part of the coffee table is drawn where the design says it is", () =>
        {
            Design design = window.CurrentDesign!;
            Assert.Equal("Coffee table", design.Name);
            Assert.Equal(9, design.Sketch.Entities.Values.OfType<Box>().Count());

            AssertPartDrawn(window, design, "Top", 0, 0, 48, 20);
            AssertPartDrawn(window, design, "Leg, front left", 1, 1, 2.5, 2.5);
            AssertPartDrawn(window, design, "Leg, front right", 44.5, 1, 2.5, 2.5);
            AssertPartDrawn(window, design, "Leg, back left", 1, 16.5, 2.5, 2.5);
            AssertPartDrawn(window, design, "Leg, back right", 44.5, 16.5, 2.5, 2.5);
            AssertPartDrawn(window, design, "Apron, front", 3.5, 1.75, 41, 0.75);
            AssertPartDrawn(window, design, "Apron, back", 3.5, 17.5, 41, 0.75);
            AssertPartDrawn(window, design, "Apron, left", 1.75, 3.5, 0.75, 13);
            AssertPartDrawn(window, design, "Apron, right", 45.5, 3.5, 0.75, 13);
        });

        app.Press(Key.Down);
        app.Chord(Key.D0);
        app.Expect("the status line says what is open and how far in the view is", () =>
        {
            Assert.Contains("Coffee table", window.DesignReadout.Text!, StringComparison.Ordinal);
            Assert.Matches(@"^Zoom \d+(\.\d)?%$", window.ZoomReadout.Text!);
            Assert.True(window.Canvas.View.PixelsPerInch > 1);
        });

        app.SaveFrame("coffee-table");
    });

    [GuiWorkflow("GUI-VIEW-02")]
    public void Pan_with_the_wheel_a_drag_and_the_keyboard() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        app.Chord(Key.D1);
        app.Chord(Key.D0);

        Sketch asOpened = window.CurrentDesign!.Sketch;
        ViewTransform fitted = canvas.View;
        Point probe = OnCanvas(window, new Point(450, 300));

        app.Wheel(new Point(450, 300), new Vector(0, 2), KeyModifiers.Shift);
        app.Expect("the wheel panned, at the same zoom, and moved nothing in the design", () =>
        {
            Assert.Equal(fitted.PixelsPerInch, canvas.View.PixelsPerInch, 9);
            Assert.NotEqual(fitted.CenterYInches, canvas.View.CenterYInches);
            AssertNothingMoved(window, asOpened);
        });

        ViewTransform beforeDrag = canvas.View;
        Point2 under = beforeDrag.ToWorld(probe);
        app.Drag(new Point(450, 300), new Point(500, 330), new Point(560, 360));
        app.Expect("the drag moved the drawing by exactly the drag, not by more", () =>
        {
            // The model point that was under the pointer is under it still, 110 across and 60
            // down from where the drag started.
            Point expected = OnCanvas(window, new Point(560, 360));
            Point actual = canvas.View.ToScreen(under);
            Assert.True(
                Math.Abs(actual.X - expected.X) < 1 && Math.Abs(actual.Y - expected.Y) < 1,
                $"the point dragged to {actual}, not to {expected}.");
            AssertNothingMoved(window, asOpened);
        });

        ViewTransform beforeKeys = canvas.View;
        app.Press(Key.Left);
        app.Press(Key.Up);
        app.Expect("the arrow keys panned by a tenth of the viewport each", () =>
        {
            double width = canvas.View.Viewport.Width;
            double height = canvas.View.Viewport.Height;
            Point wasAt = beforeKeys.ToScreen(under);
            Point isAt = canvas.View.ToScreen(under);

            // Left moves the view left, so the drawing moves right; up moves the view up, so the
            // drawing moves down. Each by a tenth of the viewport.
            Assert.Equal(wasAt.X + (0.1 * width), isAt.X, 6);
            Assert.Equal(wasAt.Y + (0.1 * height), isAt.Y, 6);
            Assert.Equal(beforeKeys.PixelsPerInch, canvas.View.PixelsPerInch, 9);
            AssertNothingMoved(window, asOpened);
        });

        app.MoveTo(new Point(300, 250));
        app.Expect("the status line reports where the pointer is, in feet and inches", () =>
        {
            Assert.Matches(@"^x .*[""'].*y .*[""']$", window.CursorReadout.Text!);
            Assert.DoesNotContain("—", window.CursorReadout.Text!, StringComparison.Ordinal);
        });

        app.SaveFrame("panned");
    });

    [GuiWorkflow("GUI-VIEW-03")]
    public void Zoom_about_the_cursor_then_zoom_to_fit() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        app.Chord(Key.D1);
        app.Chord(Key.D0);

        // A corner of the table, rather than the middle of the viewport, so that zooming in about
        // it really does push the rest of the drawing out of sight.
        Point2 corner = new(Length.Inches(48), Length.Inches(20));
        Point onCanvas = canvas.View.ToScreen(corner);
        Point inWindow = InWindow(window, onCanvas);
        ViewTransform fitted = canvas.View;

        app.Wheel(inWindow, new Vector(0, 8));
        app.Expect("the model point under the pointer is still under the pointer", () =>
        {
            Assert.True(canvas.View.PixelsPerInch > fitted.PixelsPerInch * 2);
            Point after = canvas.View.ToScreen(corner);
            Assert.True(
                Math.Abs(after.X - onCanvas.X) < 1 && Math.Abs(after.Y - onCanvas.Y) < 1,
                $"the corner moved from {onCanvas} to {after} while zooming about it.");
        });

        app.Expect("most of the drawing has left the viewport", () =>
            Assert.False(IsWhollyVisible(canvas), "the whole design still fits after zooming in."));

        app.MoveTo(new Point(500, 300));
        app.Press(Key.Right);
        app.Chord(Key.D0);
        app.Expect("the fit frames every entity again, with a margin", () =>
        {
            Assert.True(IsWhollyVisible(canvas), "something is still outside the viewport.");
            AssertMarginAllRound(canvas);
        });

        app.Expect("the zoom readout followed the view back out", () =>
            Assert.Equal(fitted.PixelsPerInch, canvas.View.PixelsPerInch, 6));

        app.SaveFrame("fitted-again");
    });

    [GuiWorkflow("GUI-VIEW-04")]
    public void Read_dimension_labels_in_feet_inches_and_fractions() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        OpenThroughTheMenu(app, window, "Wall with window");
        app.Chord(Key.D0);

        app.Expect("the wall's dimensions read the numbers it was built from", () =>
        {
            Dictionary<string, string> labels = LabelsByKey(window);
            Assert.Equal("12'-0\"", labels["Wall length"]);
            Assert.Equal("3'-0\"", labels["Opening width"]);
            Assert.Equal("4'-2 1/2\"", labels["To opening"]);
            Assert.Equal("4'-9 1/2\"", labels["Past opening"]);
            Assert.Equal("3 1/2\"", labels["Wall thickness"]);
        });

        app.SaveFrame("wall-fitted");

        // Zoom in on the opening, about its own centre, the way a person checks a rough opening.
        Point2 middleOfOpening = new(Length.FeetInches(5, 8, 1, 2), Length.Inches(1, 3, 4));
        Point inWindow = InWindow(window, canvas.View.ToScreen(middleOfOpening));
        app.Wheel(inWindow, new Vector(0, 6));
        app.Press(Key.Up);

        app.Expect("zoomed in, the opening's dimension still reads 3'-0\" and is on screen", () =>
        {
            Dictionary<string, string> labels = LabelsByKey(window);
            Assert.Equal("3'-0\"", labels["Opening width"]);

            DimensionMeasurement opening = Measurement(window, "Opening width");
            Point label = canvas.View.ToScreen(opening.LabelAnchor);
            Assert.True(
                new Rect(canvas.Bounds.Size).Contains(label),
                $"the 3'-0\" label is drawn at {label}, outside the canvas.");
        });

        app.Expect("the labels are the same strings Core.Geometry's Format produces", () =>
        {
            DimensionMeasurement wall = Measurement(window, "Wall length");
            Assert.Equal(
                Length.Feet(12).Format(new FeetInchesFormat(16)).Text,
                wall.Label(canvas.LabelFormat));
            Assert.True(wall.Format(new FeetInchesFormat(16)).IsExact);
        });

        app.SaveFrame("wall-with-window");
    });

    /// <summary>Opens a sample the way a person does: the Samples menu, with the mouse.</summary>
    static void OpenThroughTheMenu(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.SamplesMenuItem));

        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    static Point CentreOf(Visual root, Visual control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)
        ?? throw new InvalidOperationException("The control is not in the window's visual tree.");

    /// <summary>The canvas coordinate under a window coordinate.</summary>
    static Point OnCanvas(MainWindow window, Point inWindow)
    {
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(inWindow.X - origin.X, inWindow.Y - origin.Y);
    }

    /// <summary>The window coordinate of a canvas coordinate — where to send the pointer.</summary>
    static Point InWindow(MainWindow window, Point onCanvas)
    {
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    static void AssertPartDrawn(
        MainWindow window,
        Design design,
        string key,
        double x,
        double y,
        double width,
        double height)
    {
        Box box = design.Sketch.Find<Box>(DesignBuilder.IdFor(design.Name, key))
            ?? throw new InvalidOperationException($"{design.Name} has no part called {key}.");

        Assert.Equal(x, box.Anchor.X.ToInches());
        Assert.Equal(y, box.Anchor.Y.ToInches());
        Assert.Equal(width, box.Width.ToInches());
        Assert.Equal(height, box.Height.ToInches());

        ViewTransform view = window.Canvas.View;
        Rect drawn = new Rect(
            view.ToScreen(box.Corner(BoxCorner.SouthWest)),
            view.ToScreen(box.Corner(BoxCorner.NorthEast))).Normalize();

        Assert.True(
            new Rect(window.Canvas.Bounds.Size).Contains(drawn),
            $"{key} is drawn at {drawn}, which is not inside the canvas.");
        Assert.True(drawn.Width > 0 && drawn.Height > 0, $"{key} is drawn with no area.");
    }

    static void AssertNothingMoved(MainWindow window, Sketch asOpened)
    {
        // The sketch the canvas holds is still the one that was opened, value for value, and still
        // equal to a fresh load of the same sample: no gesture has written to the model.
        Assert.Same(asOpened, window.CurrentDesign!.Sketch);
        Assert.Equal(BuiltInDesigns.CoffeeTable().Sketch, window.CurrentDesign!.Sketch);
    }

    static bool IsWhollyVisible(CanvasView canvas)
    {
        WorldBounds extents = canvas.Extents;
        ViewTransform view = canvas.View;
        Rect drawn = new Rect(
            view.ToScreen(new Point2(extents.MinX, extents.MinY)),
            view.ToScreen(new Point2(extents.MaxX, extents.MaxY))).Normalize();

        return new Rect(canvas.Bounds.Size).Contains(drawn);
    }

    static void AssertMarginAllRound(CanvasView canvas)
    {
        WorldBounds extents = canvas.Extents;
        ViewTransform view = canvas.View;
        Rect drawn = new Rect(
            view.ToScreen(new Point2(extents.MinX, extents.MinY)),
            view.ToScreen(new Point2(extents.MaxX, extents.MaxY))).Normalize();
        Rect viewport = new(canvas.Bounds.Size);

        Assert.True(drawn.Left > 1, $"no margin on the left: {drawn} in {viewport}.");
        Assert.True(drawn.Top > 1, $"no margin at the top: {drawn} in {viewport}.");
        Assert.True(viewport.Right - drawn.Right > 1, $"no margin on the right: {drawn}.");
        Assert.True(viewport.Bottom - drawn.Bottom > 1, $"no margin at the bottom: {drawn}.");
    }

    static Dictionary<string, string> LabelsByKey(MainWindow window)
    {
        Design design = window.CurrentDesign!;
        return window.Canvas.Measurements().ToDictionary(
            measurement => KeyOf(design, measurement.Dimension.Id),
            measurement => measurement.Label(window.Canvas.LabelFormat));
    }

    static DimensionMeasurement Measurement(MainWindow window, string key)
    {
        EntityId id = DesignBuilder.IdFor(window.CurrentDesign!.Name, key);
        return window.Canvas.Measurements().Single(m => m.Dimension.Id == id);
    }

    /// <summary>Which named dimension of the open design an id belongs to.</summary>
    static string KeyOf(Design design, EntityId id) => WellKnownKeys
        .FirstOrDefault(key => DesignBuilder.IdFor(design.Name, key) == id)
        ?? id.ToString();

    static readonly string[] WellKnownKeys =
    [
        "Overall width", "Overall depth", "Leg inset", "Leg size", "Apron length",
        "Wall length", "Opening width", "To opening", "Past opening", "Wall thickness",
    ];
}

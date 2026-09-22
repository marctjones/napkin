using Avalonia;
using Avalonia.Input;
using Napkin.App;
using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// M2's editing loop, driven the way a person drives it: take the rectangle tool, drag out a part,
/// pick it, move it, pull its handles, and type a size into it.
/// </summary>
/// <remarks>
/// Nothing in these scenarios pokes the model. Every change is made by the same pointer and
/// keyboard input a hand would produce, and every assertion is read back off the real sketch, the
/// real dimension measurements and the real message bar — so a workflow that passes is a person
/// who could have done it.
/// </remarks>
public class DrawWorkflows
{
    [GuiWorkflow("GUI-DRAW-01")]
    public void Draw_a_rectangle_by_dragging() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        NewSheet(app, window);

        app.Press(Key.R);
        app.Expect("the rectangle tool is taken, and the tool control says so", () =>
        {
            Assert.Equal(EditTool.Rectangle, canvas.Tool);
            Assert.True(window.RectangleToolControl.IsChecked);
            Assert.Empty(window.CurrentDesign!.Sketch.Entities);
        });

        // A 24" x 18" table top, drawn around the origin so the whole of it is on a blank sheet.
        // The drag runs through an intermediate point, so the live preview is exercised rather
        // than one press and one release.
        Point start = At(window, Point2.Inches(-12, -9));
        Point middle = At(window, Point2.Inches(0, 0));
        Point end = At(window, Point2.Inches(12, 9));

        app.Drag(start, middle, end);

        app.Expect("a part of the size that was dragged is on the drawing, and it is selected", () =>
        {
            Box drawn = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
            Assert.Equal(Length.Inches(24).Units, drawn.Width.Units);
            Assert.Equal(Length.Inches(18).Units, drawn.Height.Units);
            Assert.Equal(Length.Inches(-12).Units, drawn.Anchor.X.Units);
            Assert.Equal(Length.Inches(-9).Units, drawn.Anchor.Y.Units);
            Assert.Equal(drawn.Id, window.Editor.OnlySelected);
            Assert.Equal(EditTool.Select, canvas.Tool);
        });

        app.Expect("the part's dimensions read the size it was drawn at", () =>
        {
            Box drawn = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single();
            Assert.Equal(
                Length.Inches(24).Format(canvas.LabelFormat).Text,
                canvas.SelectionDimension(drawn.Id, SizeAxis.Width)!.Label(canvas.LabelFormat));
            Assert.Equal(
                Length.Inches(18).Format(canvas.LabelFormat).Text,
                canvas.SelectionDimension(drawn.Id, SizeAxis.Height)!.Label(canvas.LabelFormat));
        });

        app.SaveFrame("drawn");

        // Now move it with the pointer, and watch the dimensions follow the geometry rather than
        // a cached number (CVS-007).
        EntityId id = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single().Id;
        app.Drag(At(window, Point2.Inches(0, 0)), At(window, Point2.Inches(4, 3)), At(window, Point2.Inches(8, 6)));

        app.Expect("the part moved by the drag and kept its size", () =>
        {
            Box moved = window.CurrentDesign!.Sketch.Find<Box>(id)!;
            Assert.Equal(Length.Inches(-4).Units, moved.Anchor.X.Units);
            Assert.Equal(Length.Inches(-3).Units, moved.Anchor.Y.Units);
            Assert.Equal(Length.Inches(24).Units, moved.Width.Units);
            Assert.Equal(Length.Inches(18).Units, moved.Height.Units);
        });

        // And pull a handle: the east edge out by six inches.
        Box before = window.CurrentDesign!.Sketch.Find<Box>(id)!;
        Point handle = At(window, BoxGeometry.GripPoint(before, BoxGrip.East));
        app.Drag(handle, new Point(handle.X + 64, handle.Y), new Point(handle.X + 112, handle.Y));

        app.Expect("the handle resized the part, the far edge stayed put, and the label followed", () =>
        {
            Box resized = window.CurrentDesign!.Sketch.Find<Box>(id)!;
            Assert.True(
                resized.Width > before.Width,
                $"the east handle left the part {resized.Width} wide, no wider than {before.Width}.");
            Assert.Equal(before.Anchor.X.Units, resized.Anchor.X.Units);
            Assert.Equal(before.Height.Units, resized.Height.Units);
            Assert.Equal(
                resized.Width.Format(canvas.LabelFormat).Text,
                canvas.SelectionDimension(id, SizeAxis.Width)!.Label(canvas.LabelFormat));
        });

        app.SaveFrame("moved-and-resized");
    });

    [GuiWorkflow("GUI-DRAW-02")]
    public void Type_a_dimension_in_feet_inch_fraction_text() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        NewSheet(app, window);
        EntityId id = DrawAPart(app, window, Point2.Inches(0, 0), Point2.Inches(20, 12));

        Box before = window.CurrentDesign!.Sketch.Find<Box>(id)!;
        Length anchorX = before.Anchor.X;
        Length anchorY = before.Anchor.Y;

        // Click the width label on the drawing: that is what opens it for typing.
        app.Click(LabelAt(window, id, SizeAxis.Width));
        app.Expect("the width is open for typing, holding what it reads now", () =>
        {
            Assert.True(window.IsEditingDimension);
            Assert.Equal(
                before.Width.Format(canvas.LabelFormat).Text,
                window.DimensionField.Text);
        });

        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type("2'-6 1/2\"");
        app.Press(Key.Enter);

        Length wanted = Length.Inches(30, 1, 2);
        app.Expect("the part is exactly that wide, its anchor corner did not move", () =>
        {
            Box after = window.CurrentDesign!.Sketch.Find<Box>(id)!;
            Assert.Equal(wanted.Units, after.Width.Units);
            Assert.Equal(anchorX.Units, after.Anchor.X.Units);
            Assert.Equal(anchorY.Units, after.Anchor.Y.Units);
            Assert.Equal(before.Height.Units, after.Height.Units);
            Assert.False(window.IsEditingDimension);
        });

        app.Expect("the label reads the value back, and the text reads back as the value", () =>
        {
            string label = canvas.SelectionDimension(id, SizeAxis.Width)!.Label(canvas.LabelFormat);
            Assert.Equal(wanted.Format(canvas.LabelFormat).Text, label);
            Assert.Equal(wanted.Units, Length.Parse(label).Units);
        });

        app.Expect("the number now has an owner the drawing can show", () =>
        {
            ParamValue driving = Assert.Single(
                window.CurrentDesign!.Sketch.Relationships.Values.OfType<ParamValue>());
            Assert.Equal(new BoxWidthRef(id), driving.Param);
            Assert.Equal(wanted.Units, driving.Value.Units);
            Assert.Contains(
                window.RelationshipsOnScreen,
                line => line.Contains("width is 2'-6 1/2\"", StringComparison.Ordinal));
        });

        app.SaveFrame("typed-dimension");
    });

    /// <summary>Starts a blank sheet through the real shortcut.</summary>
    static void NewSheet(AppDriver app, MainWindow window)
    {
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        if (window.Editor.Selection.Count > 0)
        {
            app.Press(Key.Escape);
        }
    }

    /// <summary>Draws one part with the pointer, and gives back its id.</summary>
    static EntityId DrawAPart(AppDriver app, MainWindow window, Point2 from, Point2 to)
    {
        HashSet<EntityId> before = [.. window.CurrentDesign!.Sketch.Entities.Keys];
        app.Press(Key.R);
        app.Drag(
            At(window, from),
            At(window, new Point2(
                from.X + (to.X - from.X).Divide(2, Rounding.HalfToEven),
                from.Y + (to.Y - from.Y).Divide(2, Rounding.HalfToEven))),
            At(window, to));

        return window.CurrentDesign!.Sketch.Entities.Values
            .OfType<Box>()
            .Single(box => !before.Contains(box.Id))
            .Id;
    }

    /// <summary>The window coordinate a model point is drawn at.</summary>
    static Point At(MainWindow window, Point2 world) =>
        InWindow(window, window.Canvas.View.ToScreen(world));

    /// <summary>The window coordinate of a selected part's dimension label.</summary>
    static Point LabelAt(MainWindow window, EntityId box, SizeAxis axis) =>
        InWindow(window, window.Canvas.SelectionDimensionLabelAt(box, axis)
            ?? throw new InvalidOperationException($"{box} shows no {axis} dimension."));

    static Point InWindow(MainWindow window, Point onCanvas)
    {
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }
}

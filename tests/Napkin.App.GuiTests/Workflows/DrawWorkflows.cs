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

    [GuiWorkflow("GUI-DRAW-03")]
    public void Recover_from_invalid_dimension_text() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        NewSheet(app, window);
        EntityId id = DrawAPart(app, window, Point2.Inches(-8, -6), Point2.Inches(8, 6));

        Sketch asDrawn = window.CurrentDesign!.Sketch;
        Box before = asDrawn.Find<Box>(id)!;

        app.Click(LabelAt(window, id, SizeAxis.Width));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type("250mm");
        app.Press(Key.Enter);

        app.Expect("the field says what it could not read, and nothing about the part changed", () =>
        {
            Assert.True(window.IsEditingDimension, "the field closed on text it could not read.");

            string error = window.DimensionFieldError;
            Assert.Contains("250mm", error, StringComparison.Ordinal);
            Assert.Contains("could not read", error, StringComparison.OrdinalIgnoreCase);

            // The explanation has to say what does work, or it is only an insult.
            Assert.Contains("3' 4 1/2\"", error, StringComparison.Ordinal);
            Assert.Contains("feet and inches", error, StringComparison.OrdinalIgnoreCase);

            Assert.Same(asDrawn, window.CurrentDesign!.Sketch);
            Assert.Equal(before.Width.Units, window.CurrentDesign!.Sketch.Find<Box>(id)!.Width.Units);
            Assert.Empty(window.CurrentDesign!.Sketch.Relationships);
        });

        app.SaveFrame("unreadable-text");

        // Correcting it applies as normal: the refused entry left nothing behind.
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type("1' 3 1/4\"");
        app.Press(Key.Enter);

        Length wanted = Length.Inches(15, 1, 4);
        app.Expect("the next entry is read as if the bad one had never happened", () =>
        {
            Assert.False(window.IsEditingDimension);
            Assert.Equal(wanted.Units, window.CurrentDesign!.Sketch.Find<Box>(id)!.Width.Units);
            Assert.Equal(
                wanted.Format(canvas.LabelFormat).Text,
                canvas.SelectionDimension(id, SizeAxis.Width)!.Label(canvas.LabelFormat));
            Assert.Empty(window.DimensionFieldError);
        });

        app.SaveFrame("corrected");
    });

    [GuiWorkflow("GUI-DRAW-04")]
    public void Move_a_part_until_it_snaps_and_see_the_relationship() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        CanvasView canvas = window.Canvas;

        NewSheet(app, window);

        // Two parts, offset vertically so that only the vertical edges are near each other: the
        // snap under test is one edge onto one edge, not a corner onto a corner.
        EntityId left = DrawAPart(app, window, Point2.Inches(-20, -6), Point2.Inches(-10, 6));
        EntityId right = DrawAPart(app, window, Point2.Inches(-4, -2), Point2.Inches(6, 10));

        // Give the left part a typed width, so its right-hand edge is at a half inch and the snap
        // has somewhere to land that the one-inch grid would not have found.
        app.Click(At(window, Point2.Inches(-15, 0)));
        app.Click(LabelAt(window, left, SizeAxis.Width));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type("10 1/2\"");
        app.Press(Key.Enter);

        Length flushAt = window.CurrentDesign!.Sketch.Find<Box>(left)!.Corner(BoxCorner.SouthEast).X;

        app.Click(At(window, Point2.Inches(1, 4)));
        app.Expect("the right-hand part is picked, and the left one is where it was typed", () =>
        {
            Assert.Equal(right, window.Editor.OnlySelected);
            Assert.Equal(Length.Inches(-9, -1, 2).Units, flushAt.Units);
        });

        // Drag it left, stopping a fifth of an inch short of flush — near enough to catch, and
        // not on a grid line, so a grid snap would have landed it somewhere else.
        Point from = At(window, Point2.Inches(1, 4));
        app.PressAt(from);
        app.DragTo(new Point(from.X - 40, from.Y));
        app.DragTo(new Point(from.X - 85, from.Y));

        app.Expect("the snap has caught, and is showing, before the button is let go", () =>
        {
            SnapPlan plan = canvas.ActiveSnap
                ?? throw new InvalidOperationException("nothing was caught during the drag.");
            Assert.True(plan.CaughtSomething, "the drag landed on the grid, not on the other part.");
            Assert.Contains(plan.Hits, hit => hit.Kind == SnapKind.Edge && hit.Target == left);
            Assert.Equal(flushAt.Units, plan.Anchor.X.Units);

            // And the part is already there: the drag is live, not a preview applied at the end.
            Assert.Equal(flushAt.Units, window.CurrentDesign!.Sketch.Find<Box>(right)!.Anchor.X.Units);
        });

        app.SaveFrame("snapped");
        app.ReleaseAt(new Point(from.X - 85, from.Y));

        app.Expect("the parts are flush, and the drawing says so in its own words", () =>
        {
            Box moved = window.CurrentDesign!.Sketch.Find<Box>(right)!;
            Assert.Equal(flushAt.Units, moved.Anchor.X.Units);
            Assert.Equal(
                flushAt.Units,
                window.CurrentDesign!.Sketch.Find<Box>(left)!.Corner(BoxCorner.SouthEast).X.Units);

            Flush stored = Assert.Single(
                window.CurrentDesign!.Sketch.Relationships.Values.OfType<Flush>());
            Assert.Equal(LocalFeatures.Edge(left, BoxEdge.East), stored.A);
            Assert.Equal(LocalFeatures.Edge(right, BoxEdge.West), stored.B);

            // The moved part is selected and has relationships, so the list is open to them.
            Assert.True(window.IsRelationshipListExpanded, "the list stayed a badge with a related part selected.");
            Assert.Contains(
                window.RelationshipsOnScreen,
                line => line.Contains("flush with", StringComparison.Ordinal));
        });

        app.SaveFrame("flush");

        // The list is on demand (#62): with nothing selected it folds to a count, off the drawing's
        // way, and comes back when a part it talks about is picked again.
        int stated = window.CurrentDesign!.Sketch.Relationships.Count;
        app.MoveTo(At(window, Point2.Inches(14, -12)));
        app.Press(Key.Escape);

        app.Expect("nothing selected: the list is a count badge, not a block of sentences", () =>
        {
            Assert.Empty(window.Editor.Selection);
            Assert.False(window.IsRelationshipListExpanded, "the list stayed open with nothing selected.");
            Assert.Empty(window.RelationshipsOnScreen);
            Assert.Equal($"{stated} relationships", window.RelationshipHeadlineText);
        });

        app.SaveFrame("relationships-badge");

        // Resting the pointer on a related part is a quick look: the list opens without selecting
        // anything, and folds again when the pointer moves back onto empty paper.
        app.MoveTo(At(window, Point2.Inches(-4, 4)));
        app.Expect("hovering a part with relationships opens the list, selecting nothing", () =>
        {
            Assert.Equal(right, window.Canvas.HoveredPart);
            Assert.Empty(window.Editor.Selection);
            Assert.True(window.IsRelationshipListExpanded, "hovering a related part did not open the list.");
            Assert.Contains(
                window.RelationshipsOnScreen,
                line => line.Contains("flush with", StringComparison.Ordinal));
        });

        app.MoveTo(At(window, Point2.Inches(14, -12)));
        app.Expect("off the part again, the list folds back to its count", () =>
        {
            Assert.Null(window.Canvas.HoveredPart);
            Assert.False(window.IsRelationshipListExpanded);
        });

        app.Click(At(window, Point2.Inches(-15, 0)));
        app.Expect("selecting a part with relationships opens the list again, same sentences", () =>
        {
            Assert.Equal(left, window.Editor.OnlySelected);
            Assert.True(window.IsRelationshipListExpanded);
            Assert.Equal(
                window.Editor.RelationshipEntries().Select(entry => entry.Text),
                window.RelationshipsOnScreen);
            Assert.Contains(
                window.RelationshipsOnScreen,
                line => line.Contains("flush with", StringComparison.Ordinal));
        });
    });

    [GuiWorkflow("GUI-DRAW-07")]
    public void A_conflict_is_explained_rather_than_a_wrong_number() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        NewSheet(app, window);
        EntityId left = DrawAPart(app, window, Point2.Inches(-20, -6), Point2.Inches(-8, 6));
        EntityId right = DrawAPart(app, window, Point2.Inches(-8, -2), Point2.Inches(4, 10));

        // Snap them together, then pin both: now neither the shared edge nor either part can move,
        // so the left part's width has nowhere to go.
        app.Click(At(window, Point2.Inches(-2, 4)));
        app.Drag(
            At(window, Point2.Inches(-2, 4)),
            At(window, Point2.Inches(-1, 4)),
            At(window, Point2.Inches(-2, 4)));
        app.Press(Key.P);

        app.Click(At(window, Point2.Inches(-14, 0)));
        app.Press(Key.P);

        app.Expect("both parts are pinned and the drawing holds the flush between them", () =>
        {
            Assert.Equal(2, window.CurrentDesign!.Sketch.Relationships.Values.OfType<Anchored>().Count());
            Assert.Single(window.CurrentDesign!.Sketch.Relationships.Values.OfType<Flush>());
            Assert.Equal(left, window.Editor.OnlySelected);
        });

        Sketch before = window.CurrentDesign!.Sketch;
        app.Click(LabelAt(window, left, SizeAxis.Width));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type("2'-6\"");
        app.Press(Key.Enter);

        app.Expect("the conflict is explained, nothing moved, and no wrong number is shown", () =>
        {
            Assert.Same(before, window.CurrentDesign!.Sketch);

            string message = window.MessageOnScreen;
            Assert.Contains("would not hold", message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cannot all be true", message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("flush with", message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pinned", message, StringComparison.OrdinalIgnoreCase);

            // The parts are named, not identified by a GUID.
            Assert.Contains(window.Editor.NameOf(left), message, StringComparison.Ordinal);
            Assert.Contains(window.Editor.NameOf(right), message, StringComparison.Ordinal);
            Assert.DoesNotContain(left.ToString(), message, StringComparison.Ordinal);

            // The part still reads what it really is, not what was typed.
            Assert.Equal(
                Length.Inches(12).Units,
                window.CurrentDesign!.Sketch.Find<Box>(left)!.Width.Units);
            Assert.True(window.IsOfferingToRemoveRelationship, "the conflict offered no way out.");
        });

        app.SaveFrame("conflict");

        // Take the way out, and the same edit goes through.
        app.Click(CentreOf(window, window.RemoveOfferButton));
        app.Expect("removing one of the conflicting relationships leaves the rest alone", () =>
        {
            Assert.True(
                window.CurrentDesign!.Sketch.Relationships.Count < before.Relationships.Count,
                "nothing was removed.");
            Assert.Equal(
                Length.Inches(12).Units,
                window.CurrentDesign!.Sketch.Find<Box>(left)!.Width.Units);
        });

        app.SaveFrame("conflict-resolved");
    });

    static Point CentreOf(Visual root, Visual control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)
        ?? throw new InvalidOperationException("The control is not in the window's visual tree.");

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

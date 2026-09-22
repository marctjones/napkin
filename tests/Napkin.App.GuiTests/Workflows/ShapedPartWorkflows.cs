using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// A part with something cut off it, driven the way a person drives it: click where the material
/// is and where it is not, duplicate it, drag the copy until it snaps, and read the cut list.
/// </summary>
/// <remarks>
/// <para>
/// This is <c>docs/design/shaped-parts-model.md</c> &#xA7;9.3's "duplicate and snap" scenario,
/// which needs nothing from the shape workshop: the shaped part comes from the shipped sample,
/// opened through the Samples menu with the mouse like any other design.
/// </para>
/// <para>
/// It claims no catalogued feature id, because the catalog has no entry for shaped parts on the
/// canvas yet and inventing one is not this session's call.
/// </para>
/// </remarks>
public class ShapedPartWorkflows
{
    [GuiWorkflow("GUI-DRAW-08")]
    public void Pick_a_shaped_part_duplicate_it_and_snap_the_copy() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Rounded-corner table");

        Box top = window.CurrentDesign!.Sketch.Entities.Values
            .OfType<Box>()
            .Single(box => !box.Cuts.IsEmpty);

        // The blank's north-east corner, a sixteenth of an inch inside it on both axes: on the
        // blank, and off the shape, because a 1" roundover took that corner away.
        Length sixteenth = Length.Inches(0, 1, 16);
        Point2 inTheRoundover = new(
            top.Corner(BoxCorner.NorthEast).X - sixteenth,
            top.Corner(BoxCorner.NorthEast).Y - sixteenth);

        app.Click(At(window, inTheRoundover));

        app.Expect("a click in the rounded-off corner picks nothing, though the blank covers it", () =>
        {
            Assert.True(
                BoxGeometry.Contains(top, inTheRoundover),
                "the point should be inside the blank, or the test is not testing anything.");
            Assert.Null(window.Editor.OnlySelected);
        });

        app.Click(At(window, top.Center));

        app.Expect("a click on the shape picks the part", () =>
            Assert.Equal(top.Id, window.Editor.OnlySelected));

        int partsBefore = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Count();
        int statedBefore = window.CurrentDesign!.Sketch.RelationshipsInOrder.Count();

        app.Press(Key.D);

        app.Expect("a copy of the whole part, cuts and all, is on the drawing and is selected", () =>
        {
            Box[] boxes = [.. window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>()];
            Assert.Equal(partsBefore + 1, boxes.Length);

            Box copy = boxes.Single(box => box.Id == window.Editor.OnlySelected);
            Assert.NotEqual(top.Id, copy.Id);
            Assert.Equal(top.Width.Units, copy.Width.Units);
            Assert.Equal(top.Height.Units, copy.Height.Units);
            Assert.Equal(top.Cuts, copy.Cuts);
            Assert.Equal(top.Part, copy.Part);
            Assert.NotEqual(top.Anchor, copy.Anchor);

            // A duplicate is unrelated until it is snapped (§2.6).
            Assert.Equal(statedBefore, window.CurrentDesign!.Sketch.RelationshipsInOrder.Count());

            // And the part it was copied from did not move.
            Assert.Equal(top.Anchor, window.CurrentDesign!.Sketch.Find<Box>(top.Id)!.Anchor);
        });

        app.SaveFrame("duplicated");

        // Drag the copy up until its south edge catches the original's north edge, and well clear
        // of anything in X so the one thing the snap can catch is that edge. The grab starts on
        // the copy's own middle, which is material whatever its corners are doing.
        EntityId copyId = window.Editor.OnlySelected!.Value;
        Box before = window.CurrentDesign!.Sketch.Find<Box>(copyId)!;
        Vector2 move = new Point2(Length.Inches(20), top.Corner(BoxCorner.NorthWest).Y) - before.Anchor;

        Point2 from = before.Center;
        Point2 to = from + move;
        Point2 halfway = new(
            from.X + (move.Dx.Divide(2, Rounding.HalfToEven)),
            from.Y + (move.Dy.Divide(2, Rounding.HalfToEven)));

        app.Drag(At(window, from), At(window, halfway), At(window, to));

        app.Expect("the copy snapped flush to the part it came from, and the drawing says so", () =>
        {
            Flush[] flushes = [.. window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Flush>()];
            Assert.NotEmpty(flushes);
            Assert.Contains(
                flushes,
                flush => flush.References.Contains(copyId) && flush.References.Contains(top.Id));
        });

        // The list a person cuts from: two identical shaped blanks are one row of two, with a
        // picture of the shape beside it (§4.3, §4.4).
        app.Chord(Key.L);

        app.Expect("the cut list groups the copy with the original and draws its shape", () =>
        {
            CutListWindow list = window.CutList!;
            CutListRow shaped = Assert.Single(list.Rows.Rows, row => !row.Cuts.IsEmpty);

            Assert.Equal(2, shaped.Quantity);
            Assert.Equal(top.Cuts, shaped.Cuts);
            Assert.Contains(shaped.Label, list.Rows.RowsWithThumbnails);

            // A plain rectangle gets no picture: there is nothing about one to draw.
            Assert.Equal(
                list.Rows.Rows.Count(row => !row.Cuts.IsEmpty),
                list.Rows.RowsWithThumbnails.Length);
        });

        app.SaveFrame("snapped");

        // The cut list is a window of its own, and the thumbnail is the one thing on it that
        // cannot be read as text, so it is worth a frame of its own too.
        AppDriver.Attach(window.CutList!, "cut-list").SaveFrame("cut-list-with-thumbnail");
    });

    /// <summary>The window coordinate a model point is drawn at.</summary>
    static Point At(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    /// <summary>Opens a sample through the Samples menu, with the mouse.</summary>
    static void OpenSample(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.SamplesMenuItem));

        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        Size size = control.Bounds.Size;
        return topLeft + new Point(size.Width / 2, size.Height / 2);
    }
}

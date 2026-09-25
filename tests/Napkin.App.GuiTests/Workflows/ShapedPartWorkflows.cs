using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

using Xunit;
using Napkin.Modules.Editing;

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

        app.Click(At(window, top.Center.XY));

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

        // Drag the copy until its south edge catches the original's north edge, and well clear
        // of anything in X so the one thing the snap can catch is that edge. The grab starts on
        // the copy's own middle, which is material whatever its corners are doing.
        EntityId copyId = window.Editor.OnlySelected!.Value;
        Box before = window.CurrentDesign!.Sketch.Find<Box>(copyId)!;
        Vector2 move = new Point2(Length.Inches(20), top.Corner(BoxCorner.NorthWest).Y) - before.Anchor.XY;

        Point2 from = before.Center.XY;
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

    [GuiWorkflow("GUI-DRAW-09")]
    public void Shape_one_part_in_the_workshop_and_take_the_cut_off_again() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        // A blank sheet, and a plain rectangle drawn on it with the pointer.
        app.Chord(Key.N);
        app.Press(Key.R);
        app.Drag(
            At(window, Point2.Inches(-18, -4)),
            At(window, Point2.Inches(0, 0)),
            At(window, Point2.Inches(18, 4)));

        EntityId id = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>()).Id;

        app.Expect("a plain rectangle is drawn and selected", () =>
        {
            Box drawn = window.CurrentDesign!.Sketch.Find<Box>(id)!;
            Assert.Empty(drawn.Cuts);
            Assert.Equal(id, window.Editor.OnlySelected);
        });

        // Into the workshop by keyboard, which is the way §7.1 describes entering it: one part
        // selected, one keystroke.
        app.Press(Key.C);

        app.Expect("the workshop is open on the part that was selected", () =>
        {
            Assert.True(window.IsShapingPart, "the workshop did not open.");
            Assert.Equal(id, window.ShapedPart);
            Assert.Contains(window.Editor.NameOf(id), window.WorkshopPanel.IsVisible
                ? WorkshopHeadlineOf(window)
                : string.Empty, StringComparison.Ordinal);
        });

        // Pick the stock. A 1x6 is 5 1/2" wide whatever the box was drawn at, so this really
        // resizes the blank, through the updater (§1.2).
        app.Click(CentreOf(window, window.WorkshopStockField));
        app.Type("1x6");
        app.Click(CentreOf(window, window.WorkshopStockApply));

        app.Expect("the blank really is a 1x6: the yard's width, set through the updater", () =>
        {
            Box blank = window.CurrentDesign!.Sketch.Find<Box>(id)!;
            Assert.Equal("1x6", blank.Part!.Stock);
            Assert.Equal(Length.Inches(5, 1, 2).Units, blank.Height.Units);
            Assert.Equal(Length.Inches(36).Units, blank.Width.Units);
            Assert.Contains("5 1/2", window.WorkshopStockReadoutText, StringComparison.Ordinal);
        });

        app.SaveFrame("workshop-open");

        // Press on the south-west corner and drag inward. The drag is stopped halfway so that the
        // live preview can be looked at: §7.2 says the candidate is applied on every pointer move,
        // so the part really carries a cut before the button comes up.
        Point corner = InWorkshop(window, Point2.Origin);
        app.PressAt(corner);
        app.DragTo(InWorkshop(window, Point2.Inches(1, 1)));

        app.Expect("the cut follows the pointer while the button is still down", () =>
        {
            CornerCut live = Assert.IsType<CornerCut>(
                Assert.Single(window.CurrentDesign!.Sketch.Find<Box>(id)!.Cuts));
            Assert.Equal(BoxCorner.SouthWest, live.Corner);
            Assert.Equal(Length.Inches(1).Units, live.AlongX.Units);
        });

        app.DragTo(InWorkshop(window, Point2.Inches(2, 2)));
        app.ReleaseAt(InWorkshop(window, Point2.Inches(2, 2)));

        app.Expect("the gesture left one corner cut of the size it was dragged to", () =>
        {
            CornerCut clip = Assert.IsType<CornerCut>(
                Assert.Single(window.CurrentDesign!.Sketch.Find<Box>(id)!.Cuts));
            Assert.Equal(BoxCorner.SouthWest, clip.Corner);
            Assert.Equal(Length.Inches(2).Units, clip.AlongX.Units);
            Assert.Equal(Length.Inches(2).Units, clip.AlongY.Units);

            // The cut it just made is the one selected, and its numbers are in the fields.
            Assert.Equal(CutSite.Corner(BoxCorner.SouthWest), window.Workshop.SelectedSite);
            Assert.True(window.IsShowingCutFields, "the typed fields did not appear.");
            Assert.Single(window.WorkshopCutsOnScreen);
        });

        app.SaveFrame("cut");

        // Leave the workshop. The part is where it was drawn, and the canvas draws its real
        // outline rather than the rectangle.
        Box blankBefore = window.CurrentDesign!.Sketch.Find<Box>(id)!;
        app.Press(Key.Escape);

        app.Expect("the canvas is back, the outline changed, and the part did not move", () =>
        {
            Assert.False(window.IsShapingPart, "the workshop did not close.");

            Box shaped = window.CurrentDesign!.Sketch.Find<Box>(id)!;
            Assert.Equal(blankBefore.Anchor, shaped.Anchor);
            Assert.Equal(5, shaped.Outline().Segments.Length);

            // The corner a cut took away is no longer part of the shape, which is what "the
            // outline changed" means where a person clicks.
            Point2 inTheClip = new(
                shaped.Corner(BoxCorner.SouthWest).X + Length.Inches(0, 1, 4),
                shaped.Corner(BoxCorner.SouthWest).Y + Length.Inches(0, 1, 4));
            Assert.True(BoxGeometry.Contains(shaped, inTheClip));
            Assert.False(BoxGeometry.ContainsShape(shaped, inTheClip));
        });

        // And the list a person cuts from says what to do to the blank, in bench words.
        app.Chord(Key.L);

        app.Expect("the cut list shows the row, its shape and the sentence for the cut", () =>
        {
            CutListWindow list = window.CutList!;
            CutListRow row = Assert.Single(list.Rows.Rows);
            Assert.Equal(window.CurrentDesign!.Sketch.Find<Box>(id)!.Cuts, row.Cuts);

            string sentence = Assert.Single(row.CutText);
            Assert.Contains("south-west corner", sentence, StringComparison.Ordinal);
            Assert.Contains(sentence, list.Rows.LinesOnScreen, StringComparer.Ordinal);
            Assert.Contains(row.Label, list.Rows.RowsWithThumbnails);
        });

        AppDriver.Attach(window.CutList!, "cut-list-shaped").SaveFrame("sentence");

        // Back into the workshop, pick the cut, and take it off: RemoveCut, and the rectangle is
        // a rectangle again.
        app.Click(At(window, window.CurrentDesign!.Sketch.Find<Box>(id)!.Center.XY));
        app.Press(Key.C);
        app.Click(InWorkshop(window, Point2.Origin));

        app.Expect("a click on a corner that carries a cut picks it and cuts nothing new", () =>
        {
            Assert.True(window.IsShapingPart, "the workshop did not reopen.");
            Assert.Equal(CutSite.Corner(BoxCorner.SouthWest), window.Workshop.SelectedSite);
            Assert.Single(window.CurrentDesign!.Sketch.Find<Box>(id)!.Cuts);
        });

        // Typing is exact (§7.2): the same cut, set to a number rather than dragged to one.
        app.Click(CentreOf(window, window.CutFirstField));
        app.Chord(Key.A);
        app.Type("3\"");
        app.Click(CentreOf(window, window.ApplyCut));

        app.Expect("the typed setback is one SetCut, and the other mark is left alone", () =>
        {
            CornerCut typed = Assert.IsType<CornerCut>(
                Assert.Single(window.CurrentDesign!.Sketch.Find<Box>(id)!.Cuts));
            Assert.Equal(Length.Inches(3).Units, typed.AlongX.Units);
            Assert.Equal(Length.Inches(2).Units, typed.AlongY.Units);
        });

        // And the full-mitre entry: both setbacks at the blank's width, in one action (§7.2). It
        // keeps nothing related afterwards — §2.5's stated gap, issue #67.
        app.Click(CentreOf(window, window.FullMitre));

        app.Expect("a full mitre is one cut with both setbacks at the 1x6's width", () =>
        {
            CornerCut mitre = Assert.IsType<CornerCut>(
                Assert.Single(window.CurrentDesign!.Sketch.Find<Box>(id)!.Cuts));
            Assert.Equal(Length.Inches(5, 1, 2).Units, mitre.AlongX.Units);
            Assert.Equal(Length.Inches(5, 1, 2).Units, mitre.AlongY.Units);
            Assert.Equal("45°", window.CutAngleField.Text);
        });

        // Back to the drawing to press Delete on it: the keyboard follows the focus, and the last
        // thing clicked was a button in the properties panel.
        app.Click(InWorkshop(window, Point2.Origin));
        app.Press(Key.Delete);

        app.Expect("the cut is off the blank and the part is a plain rectangle again", () =>
        {
            Box plain = window.CurrentDesign!.Sketch.Find<Box>(id)!;
            Assert.Empty(plain.Cuts);
            Assert.Equal(4, plain.Outline().Segments.Length);
            Assert.All(plain.Outline().Segments, segment => Assert.IsType<StraightSegment>(segment));

            // Taking a cut off does not undo the stock: the blank is still a 1x6.
            Assert.Equal(Length.Inches(5, 1, 2).Units, plain.Height.Units);
            Assert.Equal(blankBefore.Anchor, plain.Anchor);
            Assert.Empty(window.WorkshopCutsOnScreen);
        });

        app.SaveFrame("cut-removed");
    });

    static string WorkshopHeadlineOf(MainWindow window) => window.WorkshopPanel
        .GetVisualDescendants()
        .OfType<TextBlock>()
        .Select(text => text.Text ?? string.Empty)
        .FirstOrDefault(text => text.StartsWith("Shaping", StringComparison.Ordinal))
        ?? string.Empty;

    /// <summary>The window coordinate a point of the blank's own frame is drawn at in the workshop.</summary>
    static Point InWorkshop(MainWindow window, Point2 local)
    {
        Point onWorkshop = window.Workshop.ToScreen(local);
        Point origin = window.Workshop.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onWorkshop.X + origin.X, onWorkshop.Y + origin.Y);
    }

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
        app.Click(CentreOf(window, window.FileMenuItem));
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

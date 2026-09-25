using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;

using Xunit;
using Napkin.Modules.Editing;

using Design = Napkin.Modules.Editing.Design;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Undo and redo (#11), driven the way a person drives them: the platform's keys and the Edit menu,
/// over edits made with the pointer and the keyboard.
/// </summary>
/// <remarks>
/// Every state is compared by reference against the design that was on screen at that point. The
/// history holds the very designs each gesture started and ended at, so "back to how it was" here
/// means the same object, not merely one that looks alike.
/// </remarks>
public class EditHistoryWorkflows
{
    [GuiWorkflow("GUI-DRAW-05")]
    public void Undo_and_redo_a_chain_of_edits() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        app.Chord(Key.N);
        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(-12, -9)), At(window, Point2.Inches(0, 0)), At(window, Point2.Inches(12, 9)));

        EntityId id = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>()).Id;
        List<Design> states = [window.CurrentDesign!];

        app.Expect("the Edit menu offers to undo the drawing, and nothing to redo", () =>
        {
            Assert.True(window.UndoMenuEntry.IsEnabled);
            Assert.Equal("_Undo Drew a part", window.UndoMenuEntry.Header);
            Assert.False(window.RedoMenuEntry.IsEnabled);
            Assert.Equal("_Redo", window.RedoMenuEntry.Header);
        });

        // The undo key typed into a dimension field is the field's: it edits the text, and the
        // drawing is left alone. Escape leaves the field without applying anything.
        app.Click(LabelAt(window, id, SizeAxis.Width));
        app.Chord(Key.A);
        app.Type("3'");
        app.Chord(Key.Z);

        app.Expect("the field took the undo key for itself: the part is as drawn, the history untouched", () =>
        {
            Assert.True(window.IsEditingDimension);
            Assert.Same(states[0], window.CurrentDesign);
            Assert.Equal(1, window.Editor.History.UndoCount);
        });

        app.Press(Key.Escape);

        // Five edits, drags and typed dimensions mixed.
        app.Drag(At(window, Centre(window, id)), At(window, Centre(window, id) + new Vector2(Length.Inches(8), Length.Inches(6))));
        Took("the move");

        app.Click(LabelAt(window, id, SizeAxis.Width));
        app.Chord(Key.A);
        app.Type("2'-6\"");
        app.Press(Key.Enter);
        Took("the typed width");

        // The typed width now drives the width, so the handle pulled is the north one.
        Point north = At(window, BoxGeometry.GripPoint(Part(window, id), BoxGrip.North));
        app.Drag(north, new Point(north.X, north.Y - 30), new Point(north.X, north.Y - 60));
        Took("the resize");

        app.Click(LabelAt(window, id, SizeAxis.Height));
        app.Chord(Key.A);
        app.Type("1'-3\"");
        app.Press(Key.Enter);
        Took("the typed height");

        app.Drag(At(window, Centre(window, id)), At(window, Centre(window, id) - new Vector2(Length.Inches(4), Length.Inches(4))));
        Took("the second move");

        app.Expect("all five edits took, each one a new design, and the part is still selected", () =>
        {
            Assert.Equal(6, states.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.Equal(Length.Inches(15).Units, Part(window, id).Height.Units);
            Assert.Equal(id, window.Editor.OnlySelected);
            Assert.Equal(6, window.Editor.History.UndoCount);
            Assert.StartsWith("_Undo Moved", (string)window.UndoMenuEntry.Header!, StringComparison.Ordinal);
        });

        // Five undos: four on the keyboard, one from the Edit menu with the pointer.
        for (int step = 5; step >= 2; step--)
        {
            app.Chord(Key.Z);
            int expected = step - 1;
            app.Expect($"undo {6 - step} puts back the design as it was after edit {expected}, still selected", () =>
            {
                Assert.Same(states[expected], window.CurrentDesign);
                Assert.Equal(id, window.Editor.OnlySelected);
            });
        }

        app.Click(CentreOf(window, window.EditMenuItem));
        app.Click(CentreOf(window, window.UndoMenuEntry));

        app.Expect("the fifth undo, from the menu, is back to the part as drawn — selected, and one step left", () =>
        {
            Assert.Same(states[0], window.CurrentDesign);
            Assert.Equal(id, window.Editor.OnlySelected);
            Assert.Equal("_Undo Drew a part", window.UndoMenuEntry.Header);
            Assert.True(window.RedoMenuEntry.IsEnabled);
            Assert.StartsWith("_Redo Moved", (string)window.RedoMenuEntry.Header!, StringComparison.Ordinal);
            Assert.Equal("Undone: Moved Part 1.", window.MessageOnScreen);
        });

        // Five redos, on the platform's own redo key and once from the menu.
        KeyGesture redo = window.RedoMenuEntry.InputGesture
            ?? throw new InvalidOperationException("Edit → Redo shows no key.");
        for (int step = 1; step <= 4; step++)
        {
            // Every redo key the platform lists is bound, not only the one the menu shows.
            if (step == 2)
            {
                app.Chord(Key.Z, KeyModifiers.Shift);
            }
            else
            {
                app.Press(redo.Key, redo.KeyModifiers);
            }

            int expected = step;
            app.Expect($"redo {step} puts back edit {expected}, still selected", () =>
            {
                Assert.Same(states[expected], window.CurrentDesign);
                Assert.Equal(id, window.Editor.OnlySelected);
            });
        }

        app.Click(CentreOf(window, window.EditMenuItem));
        app.Click(CentreOf(window, window.RedoMenuEntry));

        app.Expect("the fifth redo is the last state again, and there is nothing left to redo", () =>
        {
            Assert.Same(states[5], window.CurrentDesign);
            Assert.Equal(id, window.Editor.OnlySelected);
            Assert.False(window.RedoMenuEntry.IsEnabled);
            Assert.Equal("_Redo", window.RedoMenuEntry.Header);
        });

        app.SaveFrame("redone");

        void Took(string edit)
        {
            Design previous = states[^1];
            states.Add(window.CurrentDesign!);
            app.Expect($"{edit} changed the drawing and is one more step to undo", () =>
            {
                Assert.NotSame(previous, window.CurrentDesign);
                Assert.Equal(states.Count, window.Editor.History.UndoCount);
            });
        }
    });

    /// <summary>
    /// Undo across every kind of edit this build has: stock placed from the toolbox, Duplicate, Pin,
    /// Delete, and a cut made in the shape workshop — undone and redone with the workshop open.
    /// </summary>
    /// <remarks>
    /// Claims <c>GUI-DRAW-10</c>, which the catalog does not define: GUI-DRAW-05 is the chain of
    /// drags and typed sizes, and nothing in the catalog says undo reaches the stock toolbox and the
    /// shape workshop. Adding an entry is not this change's call, so the claim shows as an orphan on
    /// the scorecard — which gates nothing — until somebody writes the feature down.
    /// </remarks>
    [GuiWorkflow("GUI-DRAW-10")]
    public void Undo_reaches_stock_duplicate_pin_delete_and_the_shape_workshop() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        app.Chord(Key.N);
        app.Expect("a new sheet has nothing to undo, and Undo is greyed out", () =>
        {
            Assert.False(window.UndoMenuEntry.IsEnabled);
            Assert.False(window.RedoMenuEntry.IsEnabled);
        });

        // A 2x4 from the toolbox.
        ToggleButton lumber = window.Toolbox.CategoryButtons[StockCategory.DimensionalLumber];
        app.Click(CentreOf(window, lumber));
        app.Click(CentreOf(window, window.Toolbox.ButtonFor("2x4")!));
        // Below the drawer, which stays open over the upper left of the sheet: Plex rows are a little
        // taller than the default font's, and the drawer now reaches the point this drag used to start at.
        app.Drag(At(window, Point2.Inches(-12, -14)), At(window, Point2.Inches(0, -14)), At(window, Point2.Inches(12, -14)));
        app.Click(CentreOf(window, lumber));

        Box board = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
        Design placed = window.CurrentDesign!;

        // Duplicate, pin the copy, delete the copy: three more steps.
        app.Press(Key.D);
        EntityId copy = window.Editor.OnlySelected!.Value;
        Design duplicated = window.CurrentDesign!;
        app.Press(Key.P);
        Design pinned = window.CurrentDesign!;
        app.Press(Key.Delete);

        app.Expect("the copy and its pin are gone, and Undo names the delete", () =>
        {
            Assert.Equal(board.Id, Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>()).Id);
            Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Anchored>());
            Assert.Equal(4, window.Editor.History.UndoCount);
        });

        app.Chord(Key.Z);
        app.Expect("undoing the delete brings the copy back, pinned", () =>
        {
            Assert.Same(pinned, window.CurrentDesign);
            Assert.Equal(copy, Assert.Single(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Anchored>()).Entity);
        });

        app.Chord(Key.Z);
        app.Expect("undoing the pin leaves the copy, free", () =>
        {
            Assert.Same(duplicated, window.CurrentDesign);
            Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Anchored>());
        });

        app.Chord(Key.Z);
        app.Expect("undoing the duplicate leaves the placed 2x4 alone, still a 2x4", () =>
        {
            Assert.Same(placed, window.CurrentDesign);
            Assert.Equal("2x4", Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>()).Part!.Stock);
        });

        // Into the shape workshop on the board, and clip a corner.
        app.Click(At(window, Part(window, board.Id).Center.XY));
        app.Press(Key.C);
        app.Drag(
            InWorkshop(window, Point2.Origin),
            InWorkshop(window, Point2.Inches(1, 1)),
            InWorkshop(window, Point2.Inches(2, 2)));

        app.Expect("the workshop cut the corner, and the redo stack has gone", () =>
        {
            Assert.True(window.IsShapingPart);
            Assert.Single(Part(window, board.Id).Cuts);
            Assert.Single(window.WorkshopCutsOnScreen);
            Assert.False(window.RedoMenuEntry.IsEnabled);
        });

        app.Chord(Key.Z);
        app.Expect("undo in the workshop takes the cut off and leaves the workshop open on the same part", () =>
        {
            Assert.Same(placed, window.CurrentDesign);
            Assert.True(window.IsShapingPart, "undo closed the workshop.");
            Assert.Equal(board.Id, window.ShapedPart);
            Assert.Empty(Part(window, board.Id).Cuts);
            Assert.Empty(window.WorkshopCutsOnScreen);
        });

        KeyGesture redo = window.RedoMenuEntry.InputGesture!;
        app.Press(redo.Key, redo.KeyModifiers);
        app.Expect("redo in the workshop puts the cut back, and the list shows it", () =>
        {
            Assert.True(window.IsShapingPart);
            Assert.Single(Part(window, board.Id).Cuts);
            Assert.Single(window.WorkshopCutsOnScreen);
        });

        app.Chord(Key.Z);
        app.Chord(Key.Z);
        app.Expect("undoing the placement takes the part away, and the workshop with it", () =>
        {
            Assert.Empty(window.CurrentDesign!.Sketch.Entities);
            Assert.False(window.IsShapingPart, "the workshop is still open over a part that is not there.");
            Assert.True(window.ToolControl.IsVisible);
            Assert.False(window.UndoMenuEntry.IsEnabled);
        });

        app.SaveFrame("all-undone");
    });

    static Box Part(MainWindow window, EntityId id) =>
        window.CurrentDesign!.Sketch.Find<Box>(id)
        ?? throw new InvalidOperationException($"{id} is not in the drawing.");

    static Point2 Centre(MainWindow window, EntityId id) => Part(window, id).Center.XY;

    /// <summary>The window coordinate a model point is drawn at.</summary>
    static Point At(MainWindow window, Point2 world) =>
        InWindow(window, window.Canvas.View.ToScreen(world));

    /// <summary>The window coordinate of a selected part's dimension label.</summary>
    static Point LabelAt(MainWindow window, EntityId box, SizeAxis axis) =>
        InWindow(window, window.Canvas.SelectionDimensionLabelAt(box, axis)
            ?? throw new InvalidOperationException($"{box} shows no {axis} dimension."));

    /// <summary>The window coordinate a point of the blank's own frame is drawn at in the workshop.</summary>
    static Point InWorkshop(MainWindow window, Point2 local)
    {
        Point onWorkshop = window.Workshop.ToScreen(local);
        Point origin = window.Workshop.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onWorkshop.X + origin.X, onWorkshop.Y + origin.Y);
    }

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

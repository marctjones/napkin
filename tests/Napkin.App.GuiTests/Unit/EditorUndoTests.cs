using Napkin.App.Designs;
using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Undo and redo through the editor (#11): restoring immutable designs, never inverting a request.
/// </summary>
/// <remarks>
/// Every test here edits through <see cref="DesignEditor.Apply"/> and gestures, exactly as the
/// canvas and the workshop do, and reads back the design the editor holds. The GUI side of the same
/// promise — keys, the Edit menu, a chain of mixed edits — is GUI-DRAW-05.
/// </remarks>
public class EditorUndoTests
{
    static readonly Vector2 FiveInchesEast = new(Length.Inches(5), Length.Zero);

    [Fact]
    [Trait("Feature", "CVS-009")]
    public void Undo_restores_the_previous_design_exactly_and_redo_the_next()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));
        Design opened = editor.Design;

        editor.BeginGesture("Moved Part 1");
        editor.Apply(Drag.InPlan(EditingBuilder.Id(0), FiveInchesEast), "Moved Part 1");
        editor.EndGesture();
        Design moved = editor.Design;

        Assert.True(editor.Undo());
        Assert.Same(opened, editor.Design);
        Assert.Equal("Undone: Moved Part 1.", editor.LastMessage!.Text);

        Assert.True(editor.Redo());
        Assert.Same(moved, editor.Design);
        Assert.Equal("Redone: Moved Part 1.", editor.LastMessage!.Text);
    }

    [Fact]
    [Trait("Feature", "CVS-009")]
    public void One_batch_is_one_undo_step()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12), EditingBuilder.At(40, 0, 10, 10)));
        Design opened = editor.Design;

        editor.BeginGesture("Moved both");
        editor.Apply(
            Batch.Of(
                Drag.InPlan(EditingBuilder.Id(0), FiveInchesEast),
                Drag.InPlan(EditingBuilder.Id(1), FiveInchesEast)),
            "Moved both");
        editor.EndGesture();

        Assert.Equal(1, editor.History.UndoCount);
        editor.Undo();
        Assert.Same(opened, editor.Design);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    [Trait("Feature", "CVS-009")]
    public void Entity_ids_survive_an_undo_so_the_selection_does_too()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));
        editor.Select(EditingBuilder.Id(0));

        editor.Apply(Drag.InPlan(EditingBuilder.Id(0), FiveInchesEast), "Moved Part 1");
        editor.Apply(new DragEdge(EditingBuilder.Id(0), BoxEdge.East, Length.Inches(3)), "Resized Part 1");

        editor.Undo();
        Assert.Equal(EditingBuilder.Id(0), editor.OnlySelected);
        editor.Undo();
        Assert.Equal(EditingBuilder.Id(0), editor.OnlySelected);
        editor.Redo();
        editor.Redo();
        Assert.Equal(EditingBuilder.Id(0), editor.OnlySelected);
        Assert.Equal(Length.Inches(27).Units, editor.Design.Box(0).Width.Units);
    }

    [Fact]
    public void Undoing_a_part_into_existence_takes_it_out_of_the_selection()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design());
        EntityId id = EntityId.New();

        editor.Apply(
            new AddEntity(Box.AsDrawn(id, LayerId.Default, Point2.Origin, Length.Inches(10), Length.Inches(10), Box.DefaultDepth, Angle.Zero)),
            "Drew a part");
        editor.Select(id);

        editor.Undo();

        Assert.Null(editor.Sketch.Find(id));
        Assert.Empty(editor.Selection);

        // Redo brings it back under its own id — but a part that came back is not reselected by
        // itself: what is selected is the person's, and they did not select it again.
        editor.Redo();
        Assert.NotNull(editor.Sketch.Find<Box>(id));
    }

    [Fact]
    [Trait("Feature", "CVS-009")]
    public void A_change_of_layer_is_undoable_like_any_other()
    {
        LayerId parts = LayerId.New();
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(parts, DesignLayers.Parts))
            .WithEntity(Box.AsDrawn(EditingBuilder.Id(0), LayerId.Default, Point2.Origin, Length.Inches(10), Length.Inches(10), Box.DefaultDepth, Angle.Zero));

        DesignEditor editor = new();
        editor.Open(Design.Unlabelled("Layers", sketch));

        editor.Apply(new SetLayer(EditingBuilder.Id(0), parts), "Moved Part 1 to Parts");
        Assert.Equal(parts, editor.Sketch.Find<Box>(EditingBuilder.Id(0))!.Layer);

        editor.Undo();
        Assert.Equal(LayerId.Default, editor.Sketch.Find<Box>(EditingBuilder.Id(0))!.Layer);

        editor.Redo();
        Assert.Equal(parts, editor.Sketch.Find<Box>(EditingBuilder.Id(0))!.Layer);
    }

    [Fact]
    public void Opening_another_design_clears_the_history()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));
        editor.Apply(Drag.InPlan(EditingBuilder.Id(0), FiveInchesEast), "Moved Part 1");
        editor.Apply(Drag.InPlan(EditingBuilder.Id(0), FiveInchesEast), "Moved Part 1");
        editor.Undo();
        Assert.True(editor.History.CanUndo);
        Assert.True(editor.History.CanRedo);

        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 6, 6)));

        Assert.False(editor.History.CanUndo);
        Assert.False(editor.History.CanRedo);
        Assert.False(editor.Undo());
        Assert.Equal("There is nothing to undo.", editor.LastMessage!.Text);
        Assert.False(editor.Redo());
        Assert.Equal("There is nothing to redo.", editor.LastMessage!.Text);
    }

    [Fact]
    public void A_refused_edit_is_not_an_undo_step()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));

        editor.Apply(new RemoveEntity(EntityId.New()), "Deleted something");

        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Nothing_is_undone_in_the_middle_of_a_gesture()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));
        editor.Apply(Drag.InPlan(EditingBuilder.Id(0), FiveInchesEast), "Moved Part 1");

        editor.BeginGesture("Moved Part 1");
        editor.ApplyQuietly(Drag.InPlan(EditingBuilder.Id(0), FiveInchesEast));
        Design midDrag = editor.Design;

        Assert.False(editor.Undo());
        Assert.False(editor.Redo());
        Assert.Same(midDrag, editor.Design);

        editor.EndGesture();
        Assert.Equal(2, editor.History.UndoCount);
    }

    [Fact]
    public void Unsaved_changes_are_measured_from_the_last_open_or_save_by_value()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));
        Assert.False(editor.HasUnsavedChanges);

        editor.Apply(Drag.InPlan(EditingBuilder.Id(0), FiveInchesEast), "Moved Part 1");
        Assert.True(editor.HasUnsavedChanges);

        // Undoing back to what was opened is not a change.
        editor.Undo();
        Assert.False(editor.HasUnsavedChanges);

        editor.Redo();
        Assert.True(editor.HasUnsavedChanges);

        editor.MarkSaved();
        Assert.False(editor.HasUnsavedChanges);

        // Undoing past the save is a change again: the file on disk has the part five inches east.
        editor.Undo();
        Assert.True(editor.HasUnsavedChanges);

        // And a drag there and back again ends where the save did, which is no change at all.
        editor.Redo();
        editor.Apply(Drag.InPlan(EditingBuilder.Id(0), FiveInchesEast), "Moved Part 1");
        editor.Apply(Drag.InPlan(EditingBuilder.Id(0), new Vector2(Length.Inches(-5), Length.Zero)), "Moved Part 1");
        Assert.False(editor.HasUnsavedChanges);
    }
}

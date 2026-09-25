using System.Collections.Immutable;
using Napkin.Modules.Editing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.Modules.Editing.Design;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The selection commands, run without a window (#88), and where a duplicate lands (#71).
/// </summary>
public class SelectionCommandsTests
{
    static readonly EntityId First = EditingBuilder.Id(0);
    static readonly EntityId Second = EditingBuilder.Id(1);

    [Theory]
    [InlineData(BoxFace.Top, 0)]
    [InlineData(BoxFace.Top, 1)]
    [InlineData(BoxFace.North, 0)]
    [InlineData(BoxFace.East, 3)]
    [InlineData(BoxFace.Bottom, 2)]
    public void A_duplicate_lands_clear_of_the_original_and_level_with_it(BoxFace faceUp, int quarters)
    {
        // A leg much bigger than the grid step: a diagonal step would have put the copy inside it.
        Box leg = new(First, LayerId.Default, Point3.Inches(10, 20, 30), Length.Inches(2, 1, 2), Length.Inches(3, 1, 2), Length.Inches(16, 1, 4), faceUp, Angle.Right * quarters);
        DesignEditor editor = EditorWith(leg);

        EntityId copyId = SelectionCommands.Duplicate(editor, gridStepInches: 1)!.Value;

        Box copy = editor.Sketch.Find<Box>(copyId)!;
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(leg);
        (Point3 copyLow, Point3 copyHigh) = SpaceSnapResolver.Extent(copy);
        bool apartInX = copyLow.X >= high.X || copyHigh.X <= low.X;
        bool apartInY = copyLow.Y >= high.Y || copyHigh.Y <= low.Y;
        Assert.True(apartInX || apartInY, "the copy overlaps the original.");
        Assert.Equal(low.Z, copyLow.Z);
        Assert.Equal(leg with { Id = copyId, Anchor = copy.Anchor, Name = copy.Name }, copy);
        Assert.Equal(copyId, editor.OnlySelected);
    }

    [Fact]
    public void A_duplicate_goes_the_short_way_past_the_original_by_one_grid_step()
    {
        // 40" east-west and 3/4" north-south: the copy goes north, 3/4" and one step.
        Box apron = Box.AsDrawn(First, LayerId.Default, Point2.Inches(0, 0), Length.Inches(40), Length.Inches(0, 3, 4), Length.Inches(3, 1, 2), Angle.Zero);
        DesignEditor editor = EditorWith(apron);

        EntityId copyId = SelectionCommands.Duplicate(editor, gridStepInches: 0.5)!.Value;

        Assert.Equal(
            apron.Anchor + Vector3.Along(Axis.Y, Length.Inches(1, 1, 4)),
            editor.Sketch.Find<Box>(copyId)!.Anchor);
    }

    [Fact]
    public void Duplicate_uses_the_grid_step_it_is_given()
    {
        Box leg = Box.AsDrawn(First, LayerId.Default, Point2.Inches(0, 0), Length.Inches(2), Length.Inches(3), Length.Inches(16), Angle.Zero);

        Assert.Equal(Vector3.Along(Axis.X, Length.Inches(3)), SelectionCommands.BesideOffset(leg, 1));
        Assert.Equal(Vector3.Along(Axis.X, Length.Inches(8)), SelectionCommands.BesideOffset(leg, 6));
    }

    [Fact]
    public void Duplicate_takes_one_part_and_says_so_otherwise()
    {
        DesignEditor editor = EditorWith(Box.AsDrawn(First, LayerId.Default, Point2.Inches(0, 0), Length.Inches(2), Length.Inches(3), Length.Inches(1), Angle.Zero));
        editor.ClearSelection();

        Assert.Null(SelectionCommands.Duplicate(editor, 1));
        Assert.Contains("Select a part", editor.LastMessage!.Text, StringComparison.Ordinal);
        Assert.Single(editor.Sketch.Entities);
    }

    [Fact]
    public void Delete_removes_the_whole_selection_in_one_undo_step()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 4, 4), EditingBuilder.At(10, 0, 4, 4)));
        editor.Select(First);
        editor.ToggleSelected(Second);

        SelectionCommands.Delete(editor);

        Assert.Empty(editor.Sketch.Entities.Values.OfType<Box>());
        Assert.True(editor.Undo());
        Assert.Equal(2, editor.Sketch.Entities.Values.OfType<Box>().Count());
    }

    [Fact]
    public void Pin_pins_each_part_once()
    {
        DesignEditor editor = EditorWith(Box.AsDrawn(First, LayerId.Default, Point2.Inches(0, 0), Length.Inches(2), Length.Inches(3), Length.Inches(1), Angle.Zero));

        SelectionCommands.Pin(editor);
        SelectionCommands.Pin(editor);

        Assert.Single(editor.Sketch.RelationshipsInOrder.OfType<Anchored>());
        Assert.Contains("Already pinned", editor.LastMessage!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Shape_names_the_one_part_or_says_why_not()
    {
        DesignEditor editor = EditorWith(Box.AsDrawn(First, LayerId.Default, Point2.Inches(0, 0), Length.Inches(2), Length.Inches(3), Length.Inches(1), Angle.Zero));
        Assert.Equal(First, SelectionCommands.PartToShape(editor));

        editor.ClearSelection();
        Assert.Null(SelectionCommands.PartToShape(editor));
        Assert.Contains("Select a part to shape it", editor.LastMessage!.Text, StringComparison.Ordinal);
    }

    static DesignEditor EditorWith(Box box)
    {
        DesignEditor editor = new();
        editor.Open(new Design("Test", Sketch.Empty.WithEntity(box), ImmutableDictionary<EntityId, string>.Empty.Add(box.Id, "Part")));
        editor.Select(box.Id);
        return editor;
    }
}

/// <summary>
/// The selection commands' quiet cases — nothing selected — and what a drop that went nowhere
/// says, with the way out when a pin is the reason.
/// </summary>
public class SelectionCommandEdgeTests
{
    static readonly EntityId First = EditingBuilder.Id(0);
    static readonly EntityId Second = EditingBuilder.Id(1);

    static DesignEditor TwoParts()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 10, 4), EditingBuilder.At(20, 0, 10, 4)));
        return editor;
    }

    [Fact]
    public void Delete_and_pin_with_nothing_selected_do_nothing_at_all()
    {
        DesignEditor editor = TwoParts();
        Sketch before = editor.Sketch;

        SelectionCommands.Delete(editor);
        SelectionCommands.Pin(editor);

        Assert.Same(before, editor.Sketch);
        Assert.False(editor.History.CanUndo);
        Assert.Null(editor.LastMessage);
    }

    [Fact]
    public void Pinning_two_parts_is_one_undo_step_that_pins_both()
    {
        DesignEditor editor = TwoParts();
        editor.SelectAll([First, Second]);

        SelectionCommands.Pin(editor);

        Assert.Equal([First, Second], editor.Sketch.RelationshipsInOrder.OfType<Anchored>().Select(pin => pin.Entity).Order());
        Assert.True(editor.Undo());
        Assert.Empty(editor.Sketch.RelationshipsInOrder);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Pinning_a_pair_where_one_is_already_pinned_pins_only_the_other()
    {
        DesignEditor editor = TwoParts();
        editor.Select(First);
        SelectionCommands.Pin(editor);
        editor.SelectAll([First, Second]);

        SelectionCommands.Pin(editor);

        Assert.Equal(2, editor.Sketch.RelationshipsInOrder.OfType<Anchored>().Count());
        Assert.Single(editor.Sketch.RelationshipsInOrder.OfType<Anchored>(), pin => pin.Entity == First);
    }

    [Fact]
    public void Mirror_and_duplicate_with_nothing_selected_hint_and_make_nothing()
    {
        DesignEditor editor = TwoParts();

        Assert.Null(SelectionCommands.Mirror(editor, Axis.X));
        Assert.Equal(EditSeverity.Hint, editor.LastMessage!.Severity);
        Assert.Null(SelectionCommands.Duplicate(editor, 1));
        Assert.Equal(EditSeverity.Hint, editor.LastMessage!.Severity);
        Assert.Equal(2, editor.Sketch.Entities.Count);
    }

    [Fact]
    public void A_skewed_part_is_not_mirrored_and_the_problem_names_it()
    {
        DesignEditor editor = new();
        Box skewed = Box.AsDrawn(First, LayerId.Default, Point2.Inches(0, 0), Length.Inches(10), Length.Inches(4), Length.Inches(1), Angle.Degrees(30));
        editor.Open(new Design("Skew", Sketch.Empty.WithEntity(skewed), ImmutableDictionary<EntityId, string>.Empty.Add(First, "Brace")));
        editor.Select(First);

        Assert.Null(SelectionCommands.Mirror(editor, Axis.Y));

        Assert.Equal(EditSeverity.Problem, editor.LastMessage!.Severity);
        Assert.Contains("Brace", editor.LastMessage.Text, StringComparison.Ordinal);
        Assert.Single(editor.Sketch.Entities);
    }

    [Fact]
    public void A_pinned_part_that_stayed_put_is_offered_an_unpin_as_one_undo_step()
    {
        DesignEditor editor = TwoParts();
        editor.Select(First);
        SelectionCommands.Pin(editor);
        Anchored pin = editor.Sketch.RelationshipsInOrder.OfType<Anchored>().Single();

        SelectionCommands.SayStayedPut(editor, First, "Moved Part 1");

        EditMessage message = editor.LastMessage!;
        Assert.Equal(EditSeverity.Problem, message.Severity);
        Assert.StartsWith("Moved Part 1", message.Text, StringComparison.Ordinal);
        Assert.Equal([pin.Id], message.Highlight);
        EditOffer offer = Assert.IsType<EditOffer>(message.Offer);
        Assert.Equal(new RemoveRelationship(pin.Id), offer.Request);

        Assert.IsAssignableFrom<Succeeded>(editor.Apply(offer.Request, offer.What));
        Assert.Empty(editor.Sketch.RelationshipsInOrder);
    }

    [Fact]
    public void An_unpinned_part_that_stayed_put_gets_a_hint_naming_it_and_no_offer()
    {
        DesignEditor editor = TwoParts();
        editor.Select(Second);
        SelectionCommands.Pin(editor);

        // The other part is pinned; this one is not, so its pin is not the reason.
        SelectionCommands.SayStayedPut(editor, First, "Moved Part 1");

        EditMessage message = editor.LastMessage!;
        Assert.Equal(EditSeverity.Hint, message.Severity);
        Assert.Contains(editor.NameOf(First), message.Text, StringComparison.Ordinal);
        Assert.Null(message.Offer);
    }

    [Fact]
    public void The_commands_need_an_editor()
    {
        Assert.Throws<ArgumentNullException>(() => SelectionCommands.Delete(null!));
        Assert.Throws<ArgumentNullException>(() => SelectionCommands.Pin(null!));
        Assert.Throws<ArgumentNullException>(() => SelectionCommands.Duplicate(null!, 1));
        Assert.Throws<ArgumentNullException>(() => SelectionCommands.Mirror(null!, Axis.X));
        Assert.Throws<ArgumentNullException>(() => SelectionCommands.SelectedBoxes(null!));
        Assert.Throws<ArgumentNullException>(() => SelectionCommands.SayStayedPut(null!, First, "x"));
        Assert.Throws<ArgumentNullException>(() => SelectionCommands.PartToShape(null!));
    }
}

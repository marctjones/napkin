using System.Collections.Immutable;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// When the turn command does not turn: nothing or too much selected, a part off the quarter
/// turns, and a part held by its faces — where the updater refuses and the message carries the
/// offer to let go and turn (#76). Also which relationships count as holding a part by a place.
/// </summary>
public class SelectionTurnRefusalTests
{
    static readonly EntityId Leg = EditingBuilder.Id(0);
    static readonly EntityId Apron = EditingBuilder.Id(1);

    // Two 3 x 5 x 7 boxes side by side, both on the floor, their south faces in the same plane
    // (Y = 0), so a Flush between those faces holds as drawn.
    static Box LegBox => new(Leg, LayerId.Default, Point3.Inches(0, 0, 0), Length.Inches(3), Length.Inches(5), Length.Inches(7), BoxFace.Top, Angle.Zero);

    static Box ApronBox => new(Apron, LayerId.Default, Point3.Inches(10, 0, 0), Length.Inches(3), Length.Inches(5), Length.Inches(7), BoxFace.Top, Angle.Zero);

    static Flush SouthFacesFlush(RelationshipId id, EntityId a, EntityId b) =>
        new(id, new FeatureRef(a, BoxFeature.Face(BoxFace.South)), new FeatureRef(b, BoxFeature.Face(BoxFace.South)));

    static DesignEditor EditorWith(params Box[] boxes)
    {
        Sketch sketch = Sketch.Empty;
        ImmutableDictionary<EntityId, string> labels = ImmutableDictionary<EntityId, string>.Empty;
        foreach (Box box in boxes)
        {
            sketch = sketch.WithEntity(box);
            labels = labels.Add(box.Id, box.Id == Leg ? "Leg" : "Apron");
        }

        DesignEditor editor = new();
        editor.Open(new Design("Test", sketch, labels));
        return editor;
    }

    static void Hold(DesignEditor editor, Relationship relationship) =>
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddRelationship(relationship), "hold"));

    [Fact]
    public void With_nothing_selected_the_turn_asks_nothing_of_the_updater_and_hints()
    {
        DesignEditor editor = EditorWith(LegBox);
        Sketch before = editor.Sketch;

        Assert.Null(SelectionTurn.Turn(editor, Axis.X, 1));

        Assert.Equal(EditSeverity.Hint, editor.LastMessage!.Severity);
        Assert.Same(before, editor.Sketch);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void With_two_parts_selected_the_turn_hints_differently_than_with_none()
    {
        DesignEditor editor = EditorWith(LegBox, ApronBox);
        SelectionTurn.Turn(editor, Axis.X, 1);
        string none = editor.LastMessage!.Text;

        editor.SelectAll([Leg, Apron]);
        Assert.Null(SelectionTurn.Turn(editor, Axis.X, 1));

        Assert.Equal(EditSeverity.Hint, editor.LastMessage!.Severity);
        Assert.NotEqual(none, editor.LastMessage.Text);
        Assert.Equal(LegBox, editor.Sketch.Find<Box>(Leg));
    }

    [Fact]
    public void A_part_off_the_quarter_turns_is_refused_as_a_problem_naming_it()
    {
        Box skewed = LegBox with { Rotation = Angle.Degrees(30) };
        DesignEditor editor = EditorWith(skewed);
        editor.Select(Leg);

        Assert.Null(SelectionTurn.Turn(editor, Axis.Z, 1));

        Assert.Equal(EditSeverity.Problem, editor.LastMessage!.Severity);
        Assert.Contains("Leg", editor.LastMessage.Text, StringComparison.Ordinal);
        Assert.Equal(skewed, editor.Sketch.Find<Box>(Leg));
    }

    [Fact]
    public void A_part_held_by_one_face_is_refused_and_offered_a_turn_that_lets_go_of_it()
    {
        DesignEditor editor = EditorWith(LegBox, ApronBox);
        RelationshipId flush = RelationshipId.New();
        Hold(editor, SouthFacesFlush(flush, Leg, Apron));
        editor.Select(Leg);
        Sketch held = editor.Sketch;

        UpdateResult? result = SelectionTurn.Turn(editor, Axis.X, 1);

        Assert.Equal(RejectionReason.OrientationWithRelationships, Assert.IsType<Rejected>(result).Reason);
        Assert.Same(held, editor.Sketch);

        EditMessage message = editor.LastMessage!;
        Assert.Equal(EditSeverity.Problem, message.Severity);
        Assert.Equal([flush], message.Highlight);
        EditOffer offer = Assert.IsType<EditOffer>(message.Offer);

        // The offer is the removal, then the very turn that was refused.
        Batch batch = Assert.IsType<Batch>(offer.Request);
        Assert.Equal(new RemoveRelationship(flush), batch.Requests[0]);
        // A Batch compares its list by reference, so the turn in place is compared request by request.
        Batch turnInPlace = Assert.IsType<Batch>(SelectionTurn.RequestFor(LegBox, LegBox.Orientation.TurnedAbout(Axis.X, 1), pinned: false));
        Assert.Equal(turnInPlace.Requests, Assert.IsType<Batch>(batch.Requests[1]).Requests);
        Assert.Equal(2, batch.Requests.Count);

        // Taking it turns the part and drops the flush, and one undo puts both back.
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(offer.Request, offer.What));
        Assert.Null(editor.Sketch.Find(flush));
        Assert.Equal(LegBox.Orientation.TurnedAbout(Axis.X, 1), editor.Sketch.Find<Box>(Leg)!.Orientation);
        Assert.True(editor.Undo());
        Assert.Equal(held, editor.Sketch);
    }

    [Fact]
    public void A_part_held_by_four_relationships_names_three_counts_the_rest_and_offers_to_let_go_of_all_four()
    {
        Box third = LegBox with { Id = EditingBuilder.Id(2), Anchor = Point3.Inches(20, 0, 0) };
        Box fourth = LegBox with { Id = EditingBuilder.Id(3), Anchor = Point3.Inches(30, 0, 0) };
        Box fifth = LegBox with { Id = EditingBuilder.Id(4), Anchor = Point3.Inches(40, 0, 0) };
        DesignEditor editor = EditorWith(LegBox, ApronBox, third, fourth, fifth);
        RelationshipId[] ids = [RelationshipId.New(), RelationshipId.New(), RelationshipId.New(), RelationshipId.New()];
        EntityId[] others = [Apron, third.Id, fourth.Id, fifth.Id];
        for (int i = 0; i < ids.Length; i++)
        {
            Hold(editor, SouthFacesFlush(ids[i], Leg, others[i]));
        }

        editor.Select(Leg);
        SelectionTurn.Turn(editor, Axis.Y, -1);

        EditMessage message = editor.LastMessage!;
        Assert.Equal(4, message.Highlight.Count);
        Assert.Contains("4", message.Offer!.Text, StringComparison.Ordinal);
        Assert.Contains("1 more", message.Text, StringComparison.Ordinal);
        Batch batch = Assert.IsType<Batch>(message.Offer.Request);
        Assert.Equal(5, batch.Requests.Count);
        Assert.All(batch.Requests.Take(4), request => Assert.IsType<RemoveRelationship>(request));
    }

    [Fact]
    public void Sizes_pins_and_other_parts_relationships_do_not_hold_a_part_by_a_place()
    {
        RelationshipId flush = RelationshipId.New();
        Sketch sketch = Sketch.Empty
            .WithEntity(LegBox)
            .WithEntity(ApronBox)
            .WithRelationship(new Anchored(RelationshipId.New(), Leg))
            .WithRelationship(new ParamValue(RelationshipId.New(), new BoxWidthRef(Leg), Length.Inches(3)))
            .WithRelationship(new EqualParam(RelationshipId.New(), new BoxWidthRef(Leg), new BoxWidthRef(Apron)))
            .WithRelationship(new Anchored(RelationshipId.New(), Apron))
            .WithRelationship(SouthFacesFlush(flush, Leg, Apron));

        Assert.Equal([flush], SelectionTurn.HeldByPlace(sketch, Leg).Select(relationship => relationship.Id));
        Assert.Equal([flush], SelectionTurn.HeldByPlace(sketch, Apron).Select(relationship => relationship.Id));
        Assert.Empty(SelectionTurn.HeldByPlace(sketch, EntityId.New()));
    }

    [Fact]
    public void A_turn_by_no_quarters_asks_only_for_the_orientation_it_already_has()
    {
        Request request = SelectionTurn.RequestFor(LegBox, LegBox.Orientation.TurnedAbout(Axis.Z, 0), pinned: false);

        Assert.Equal(SetOrientation.To(Leg, LegBox.Orientation), request);
    }

    [Fact]
    public void A_pinned_turn_is_the_orientation_alone()
    {
        Orientation turned = LegBox.Orientation.TurnedAbout(Axis.X, 1);

        Assert.Equal(SetOrientation.To(Leg, turned), SelectionTurn.RequestFor(LegBox, turned, pinned: true));
    }

    [Fact]
    public void The_turn_helpers_need_their_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => SelectionTurn.Turn(null!, Axis.X, 1));
        Assert.Throws<ArgumentNullException>(() => SelectionTurn.HeldByPlace(null!, Leg));
        Assert.Throws<ArgumentNullException>(() => SelectionTurn.RequestFor(null!, LegBox.Orientation, false));
    }
}

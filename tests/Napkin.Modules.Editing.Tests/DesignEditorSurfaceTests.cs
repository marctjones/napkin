using System.Collections.Immutable;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The editor's smaller duties beside applying requests: messages and their event, whether the
/// drawing already says or could hold a relationship, joint selection, the relationship list, names
/// for things that are not parts, and where new parts, walls and layers get their names and layers.
/// </summary>
public class DesignEditorSurfaceTests
{
    static readonly EntityId Left = EditingBuilder.Id(0);
    static readonly EntityId Right = EditingBuilder.Id(1);

    static DesignEditor TwoParts()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 10, 4), EditingBuilder.At(20, 0, 10, 4)));
        return editor;
    }

    static Flush SouthFlush(RelationshipId id, EntityId a, EntityId b) =>
        new(id, new FeatureRef(a, BoxFeature.Face(BoxFace.South)), new FeatureRef(b, BoxFeature.Face(BoxFace.South)));

    static Joint ButtJoint(RelationshipId id) => new(
        id,
        new FeatureRef(Left, BoxFeature.Face(BoxFace.East)),
        new FeatureRef(Right, BoxFeature.Face(BoxFace.West)),
        JointType.Butt,
        null,
        Fastening.None,
        false);

    [Fact]
    public void An_editor_needs_an_updater()
    {
        Assert.Throws<ArgumentNullException>(() => new DesignEditor(null!));
    }

    [Fact]
    public void A_message_is_announced_once_and_not_again_when_the_same_one_is_shown()
    {
        DesignEditor editor = TwoParts();
        int raised = 0;
        editor.MessageChanged += (_, _) => raised++;
        EditMessage message = EditMessage.Plain(EditSeverity.Hint, "Hello");

        editor.Show(message);
        editor.Show(message);

        Assert.Same(message, editor.LastMessage);
        Assert.Equal(1, raised);

        editor.ClearMessage();
        Assert.Null(editor.LastMessage);
        Assert.Equal(2, raised);
        Assert.Throws<ArgumentNullException>(() => editor.Show(null!));
    }

    [Fact]
    public void Reporting_a_result_says_what_applying_it_would_have_said()
    {
        DesignEditor applied = TwoParts();
        DesignEditor reported = TwoParts();
        Request refused = new AddRelationship(new Anchored(RelationshipId.New(), EntityId.New()));

        UpdateResult result = applied.Apply(refused, "Pinned nothing");
        reported.Report(result, "Pinned nothing");

        Assert.IsType<Rejected>(result);
        Assert.Equal(EditSeverity.Problem, reported.LastMessage!.Severity);
        Assert.Equal(applied.LastMessage!.Text, reported.LastMessage.Text);
        Assert.Throws<ArgumentNullException>(() => reported.Report(null!, "x"));
    }

    [Fact]
    public void Applying_quietly_changes_the_drawing_but_says_nothing()
    {
        DesignEditor editor = TwoParts();

        UpdateResult result = editor.ApplyQuietly(new SetPosition(Left, Point3.Inches(5, 0, 0)));

        Assert.IsAssignableFrom<Succeeded>(result);
        Assert.Equal(Length.Inches(5), editor.Sketch.Find<Box>(Left)!.Anchor.X);
        Assert.Null(editor.LastMessage);
        Assert.Throws<ArgumentNullException>(() => editor.ApplyQuietly(null!));
        Assert.Throws<ArgumentNullException>(() => editor.Apply(null!, "x"));
    }

    [Fact]
    public void A_gesture_that_changed_nothing_is_not_recorded()
    {
        DesignEditor editor = TwoParts();
        int committed = 0;
        editor.GestureCommitted += (_, _) => committed++;

        editor.BeginGesture("Moved Part 1");
        editor.ApplyQuietly(new SetPosition(Left, Point3.Inches(0, 0, 0)));
        editor.EndGesture();

        Assert.False(editor.History.CanUndo);
        Assert.Equal(0, committed);
    }

    [Fact]
    public void The_drawing_already_says_a_relationship_written_either_way_round()
    {
        DesignEditor editor = TwoParts();
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddRelationship(SouthFlush(RelationshipId.New(), Left, Right)), "Flush"));
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(
            new AddRelationship(new EqualParam(RelationshipId.New(), new BoxWidthRef(Left), new BoxWidthRef(Right))), "Equal"));

        Assert.True(editor.AlreadyStates(SouthFlush(RelationshipId.New(), Left, Right)));
        Assert.True(editor.AlreadyStates(SouthFlush(RelationshipId.New(), Right, Left)));
        Assert.True(editor.AlreadyStates(new EqualParam(RelationshipId.New(), new BoxWidthRef(Right), new BoxWidthRef(Left))));

        // Different places, or a different kind over the same places, are not already said.
        Assert.False(editor.AlreadyStates(new Flush(
            RelationshipId.New(),
            new FeatureRef(Left, BoxFeature.Face(BoxFace.North)),
            new FeatureRef(Right, BoxFeature.Face(BoxFace.North)))));
        Assert.False(editor.AlreadyStates(new Coincident(
            RelationshipId.New(),
            new FeatureRef(Left, BoxFeature.Face(BoxFace.South)),
            new FeatureRef(Right, BoxFeature.Face(BoxFace.South)))));
        Assert.Throws<ArgumentNullException>(() => editor.AlreadyStates(null!));
    }

    [Fact]
    public void Whether_the_updater_could_hold_a_relationship_is_asked_without_changing_the_drawing()
    {
        DesignEditor editor = TwoParts();
        Sketch before = editor.Sketch;

        Assert.True(editor.CanHold(SouthFlush(RelationshipId.New(), Left, Right)));
        Assert.Same(before, editor.Sketch);
        Assert.Throws<ArgumentNullException>(() => editor.CanHold(null!));
    }

    [Fact]
    public void A_relationship_type_the_updater_does_not_support_cannot_be_held()
    {
        DesignEditor editor = new(new NothingSupported());

        Assert.False(editor.CanHold(SouthFlush(RelationshipId.New(), Left, Right)));
    }

    [Fact]
    public void Selecting_a_joint_deselects_the_parts_and_selecting_a_part_deselects_the_joint()
    {
        DesignEditor editor = TwoParts();
        RelationshipId joint = RelationshipId.New();
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddRelationship(ButtJoint(joint)), "Joined"));
        editor.Select(Left);
        int changes = 0;
        editor.SelectionChanged += (_, _) => changes++;

        editor.SelectJoint(joint);
        Assert.Equal(joint, editor.SelectedJoint);
        Assert.Empty(editor.Selection);
        Assert.Equal(1, changes);

        // Asking again for what is already selected says nothing.
        editor.SelectJoint(joint);
        Assert.Equal(1, changes);

        editor.Select(Right);
        Assert.Null(editor.SelectedJoint);
        Assert.Equal([Right], editor.Selection);
    }

    [Fact]
    public void Only_a_joint_can_be_selected_as_a_joint()
    {
        DesignEditor editor = TwoParts();
        RelationshipId flush = RelationshipId.New();
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddRelationship(SouthFlush(flush, Left, Right)), "Flush"));

        editor.SelectJoint(flush);
        Assert.Null(editor.SelectedJoint);

        editor.SelectJoint(RelationshipId.New());
        Assert.Null(editor.SelectedJoint);
    }

    [Fact]
    public void Clearing_the_selection_lets_go_of_a_selected_joint()
    {
        DesignEditor editor = TwoParts();
        RelationshipId joint = RelationshipId.New();
        editor.Apply(new AddRelationship(ButtJoint(joint)), "Joined");
        editor.SelectJoint(joint);

        editor.ClearSelection();

        Assert.Null(editor.SelectedJoint);
    }

    [Fact]
    public void A_joint_undone_out_of_existence_is_no_longer_selected()
    {
        DesignEditor editor = TwoParts();
        RelationshipId joint = RelationshipId.New();
        editor.Apply(new AddRelationship(ButtJoint(joint)), "Joined");
        editor.SelectJoint(joint);

        Assert.True(editor.Undo());

        Assert.Null(editor.SelectedJoint);
    }

    [Fact]
    public void Toggling_adds_a_part_to_the_selection_and_takes_it_out_again()
    {
        DesignEditor editor = TwoParts();
        editor.Select(Left);

        editor.ToggleSelected(Right);
        Assert.Equal([Left, Right], editor.Selection.Order());
        Assert.Null(editor.OnlySelected);
        Assert.Null(editor.OnlySelectedBox);

        editor.ToggleSelected(Left);
        Assert.Equal(Right, editor.OnlySelected);
    }

    [Fact]
    public void Selecting_something_that_is_not_there_selects_nothing()
    {
        DesignEditor editor = TwoParts();
        editor.Select(Left);

        editor.Select(EntityId.New());
        Assert.Empty(editor.Selection);

        editor.SelectAll([Left, EntityId.New()]);
        Assert.Equal([Left], editor.Selection);
        Assert.Throws<ArgumentNullException>(() => editor.SelectAll(null!));
    }

    [Fact]
    public void The_relationship_list_names_each_relationship_once_with_the_parts_it_touches()
    {
        DesignEditor editor = TwoParts();
        RelationshipId flush = RelationshipId.New();
        RelationshipId pin = RelationshipId.New();
        editor.Apply(new AddRelationship(SouthFlush(flush, Left, Right)), "Flush");
        editor.Apply(new AddRelationship(new Anchored(pin, Left)), "Pinned");
        // A relationship whose two places are on one part lists that part once.
        editor.Apply(new AddRelationship(new EqualParam(RelationshipId.New(), new BoxWidthRef(Left), new BoxHeightRef(Left))), "Square");

        IReadOnlyList<RelationshipEntry> entries = editor.RelationshipEntries();

        Assert.Equal(editor.Sketch.RelationshipsInOrder.Select(relationship => relationship.Id), entries.Select(entry => entry.Id));
        RelationshipEntry flushEntry = entries.Single(entry => entry.Id == flush);
        Assert.Equal([Left, Right], flushEntry.Entities.Order());
        Assert.Contains("Part 1", flushEntry.Text, StringComparison.Ordinal);
        Assert.Contains("Part 2", flushEntry.Text, StringComparison.Ordinal);
        Assert.Equal([Left], entries.Single(entry => entry.Id == pin).Entities);
        Assert.All(entries, entry => Assert.Single(entry.Entities.Where(id => id == Left)));
    }

    [Fact]
    public void Things_that_are_not_parts_are_named_by_kind_with_their_id_and_never_blank()
    {
        LayerId layer = LayerId.Default;
        Node start = new(EntityId.New(), layer, Point2.Inches(0, 0));
        Node end = new(EntityId.New(), layer, Point2.Inches(10, 0));
        Segment line = new(EntityId.New(), layer, start.Id, end.Id);
        Dimension dimension = new(EntityId.New(), layer, new ParamMeasurand(new SegmentLengthRef(line.Id)), null, new DimensionPlacement(Length.Inches(1), DimensionSide.North));
        Sketch sketch = Sketch.Empty.WithEntity(start).WithEntity(end).WithEntity(line).WithEntity(dimension);
        DesignEditor editor = new();
        editor.Open(Design.Unlabelled("Lines", sketch));
        EntityId missing = EntityId.New();

        string[] names = [editor.NameOf(start.Id), editor.NameOf(line.Id), editor.NameOf(dimension.Id), editor.NameOf(missing)];
        EntityId[] ids = [start.Id, line.Id, dimension.Id, missing];

        for (int i = 0; i < names.Length; i++)
        {
            Assert.Contains(ids[i].ToString(), names[i], StringComparison.Ordinal);
        }

        // Each kind says which kind it is, so the four fallbacks differ once the id is taken out.
        Assert.Equal(4, names.Select((name, i) => name.Replace(ids[i].ToString(), string.Empty, StringComparison.Ordinal)).Distinct().Count());
    }

    [Fact]
    public void New_parts_go_on_the_layer_called_parts_whatever_its_case_or_else_on_the_first_layer()
    {
        Layer framing = new(LayerId.New(), "Framing");
        Layer parts = new(LayerId.New(), "PARTS");
        DesignEditor editor = new();

        editor.Open(Design.Unlabelled("A", Sketch.Empty.WithLayer(framing).WithLayer(parts)));
        Assert.Equal(parts.Id, editor.LayerForNewParts());

        editor.Open(Design.Unlabelled("B", Sketch.Empty.WithLayer(framing)));
        Assert.Equal(editor.Sketch.Layers[0].Id, editor.LayerForNewParts());
    }

    [Fact]
    public void A_named_layer_that_exists_is_reused_and_one_that_does_not_is_asked_for()
    {
        DesignEditor editor = new();
        editor.Open(NewSheet.Empty());
        LayerId parts = editor.LayerForNewParts();

        Assert.Equal(parts, editor.LayerNamed("parts", out Request? none));
        Assert.Null(none);

        LayerId walls = editor.LayerNamed("Wall", out Request? add);
        AddLayer adding = Assert.IsType<AddLayer>(add);
        Assert.Equal(new Layer(walls, "Wall"), adding.Layer);
        Assert.NotEqual(parts, walls);

        // Asking for it is not making it: until the request is applied there is still no such layer.
        Assert.DoesNotContain(editor.Sketch.Layers, layer => layer.Id == walls);
    }

    [Fact]
    public void The_next_wall_is_one_more_than_the_highest_wall_number_ignoring_other_names()
    {
        Box Named(int x, string name) =>
            Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Inches(x, 0), Length.Inches(1), Length.Inches(1), Length.Inches(1), Angle.Zero) with { Name = name };

        DesignEditor editor = new();
        Assert.Equal("Wall 1", editor.NextName("Wall"));

        editor.Open(Design.Named("Room", Sketch.Empty
            .WithEntity(Named(0, "Wall 1"))
            .WithEntity(Named(2, "Wall 7"))
            .WithEntity(Named(4, "Wall x"))
            .WithEntity(Named(6, "Wallpaper 20"))
            .WithEntity(Named(8, "Opening 12"))));

        Assert.Equal("Wall 8", editor.NextName("Wall"));
        Assert.Equal("Opening 13", editor.NextName("Opening"));
    }

    [Fact]
    public void Part_numbers_continue_after_the_highest_one_the_opened_design_uses()
    {
        DesignEditor editor = new();
        Design design = new(
            "Numbered",
            EditingBuilder.Design(EditingBuilder.At(0, 0, 1, 1)).Sketch,
            ImmutableDictionary<EntityId, string>.Empty.Add(EditingBuilder.Id(0), "Part 41"));

        editor.Open(design);

        Assert.Equal("Part 42", editor.NextPartName());
        Assert.Equal("Part 43", editor.NextPartName());
    }

    [Fact]
    public void Undo_and_redo_with_nothing_to_do_hint_and_change_nothing()
    {
        DesignEditor editor = TwoParts();
        Sketch before = editor.Sketch;

        Assert.False(editor.Undo());
        Assert.Equal(EditSeverity.Hint, editor.LastMessage!.Severity);
        string undoHint = editor.LastMessage.Text;

        Assert.False(editor.Redo());
        Assert.Equal(EditSeverity.Hint, editor.LastMessage!.Severity);
        Assert.NotEqual(undoHint, editor.LastMessage.Text);
        Assert.Same(before, editor.Sketch);
    }

    [Fact]
    public void Nothing_is_redone_in_the_middle_of_a_gesture()
    {
        DesignEditor editor = TwoParts();
        editor.Apply(new SetPosition(Left, Point3.Inches(5, 0, 0)), "Moved Part 1");
        Assert.True(editor.Undo());

        editor.BeginGesture("Dragging");
        Assert.False(editor.Redo());
        editor.EndGesture();

        Assert.True(editor.Redo());
        Assert.Equal(Length.Inches(5), editor.Sketch.Find<Box>(Left)!.Anchor.X);
    }

    /// <summary>An updater that supports no relationship at all, so the type check alone refuses.</summary>
    sealed class NothingSupported : IGeometryUpdater
    {
        public ImmutableHashSet<Type> SupportedRelationships { get; } = [];

        public UpdateResult Apply(Sketch sketch, Request request) =>
            throw new InvalidOperationException("CanHold must not ask when the type is unsupported.");
    }
}

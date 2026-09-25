using System.Collections.Immutable;
using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Duplicating and mirroring several parts at once, with the relationships among them (#87).
/// </summary>
public class GroupCopyTests
{
    // A 48" x 24" top up at 16 1/4", a leg under its south-west corner, and an apron against the leg.
    static readonly Box Top = new(EditingBuilder.Id(0), LayerId.Default, Point3.Inches(0, 0, 0) with { Z = Length.Inches(16, 1, 4) }, Length.Inches(48), Length.Inches(24), Length.Inches(0, 3, 4), BoxFace.Top, Angle.Zero);
    static readonly Box Leg = new(EditingBuilder.Id(1), LayerId.Default, Point3.Inches(1, 1, 0) with { X = Length.Inches(1, 1, 2), Y = Length.Inches(1, 1, 2) }, Length.Inches(2, 1, 2), Length.Inches(2, 1, 2), Length.Inches(16, 1, 4), BoxFace.Top, Angle.Zero);
    static readonly Box Apron = new(EditingBuilder.Id(2), LayerId.Default, new Point3(Length.Inches(4), Length.Inches(1, 1, 2), Length.Inches(12, 3, 4)), Length.Inches(40), Length.Inches(0, 3, 4), Length.Inches(3, 1, 2), BoxFace.Top, Angle.Zero);

    static readonly Flush ApronOnLeg = new(RelationshipId.New(), new FeatureRef(Leg.Id, BoxFeature.Face(BoxFace.East)), new FeatureRef(Apron.Id, BoxFeature.Face(BoxFace.West)));
    static readonly AxisDistance LegInset = new(RelationshipId.New(), new FeatureRef(Top.Id, BoxFeature.LocalUpright(BoxCorner.SouthWest)), new FeatureRef(Leg.Id, BoxFeature.LocalUpright(BoxCorner.SouthWest)), Axis.X, Length.Inches(1, 1, 2));
    static readonly ParamValue ApronLength = new(RelationshipId.New(), new BoxWidthRef(Apron.Id), Length.Inches(40));
    static readonly Anchored TopPinned = new(RelationshipId.New(), Top.Id);

    static DesignEditor Table()
    {
        Sketch sketch = Sketch.Empty.WithEntity(Top).WithEntity(Leg).WithEntity(Apron);
        foreach (Relationship relationship in (Relationship[])[ApronOnLeg, LegInset, ApronLength, TopPinned])
        {
            sketch = sketch.WithRelationship(relationship);
        }

        DesignEditor editor = new();
        editor.Open(new Design("Table", sketch, ImmutableDictionary<EntityId, string>.Empty.Add(Top.Id, "Top").Add(Leg.Id, "Leg").Add(Apron.Id, "Apron")));
        return editor;
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void A_copied_wall_carries_its_bracing_onto_the_copied_openings_and_a_mirror_turns_it_end_for_end()
    {
        // A 12 ft wall on the Wall layer with a window 12"-48" along it: two segments, the first
        // assigned "zz-panel" and the second "zz-board" (SYNTHETIC method ids).
        LayerId walls = LayerId.New();
        LayerId openings = LayerId.New();
        Box wall = new(EditingBuilder.Id(20), walls, Point3.Inches(0, 0, 0), Length.Inches(144), Length.Inches(3, 1, 2), Length.Inches(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Wall 1",
            WallInputs = new WallInputs(null, null, [new BracingAssignment(null, EditingBuilder.Id(21), "zz-panel"), new BracingAssignment(EditingBuilder.Id(21), null, "zz-board")]),
        };
        Box window = new(EditingBuilder.Id(21), openings, new Point3(Length.Inches(12), Length.Zero, Length.Inches(36)), Length.Inches(36), Length.Inches(3, 1, 2), Length.Inches(42), BoxFace.Top, Angle.Zero) { Name = "Window 1" };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(walls, Napkin.Modules.Building.BuildingLayers.Wall))
            .WithLayer(new Layer(openings, Napkin.Modules.Building.BuildingLayers.Opening))
            .WithEntity(wall)
            .WithEntity(window);

        (ImmutableList<Request> copied, ImmutableDictionary<EntityId, EntityId> copies) = GroupCopy.Duplicate(sketch, [wall, window], Vector3.Along(Axis.Y, Length.Inches(48)));
        Sketch withCopy = copied.OfType<AddEntity>().Aggregate(sketch, (s, add) => s.WithEntity(add.Entity));
        Napkin.Modules.Building.WallLine copyLine = Napkin.Modules.Building.WallLine.Of(withCopy, new Napkin.Modules.Building.Wall(withCopy.Find<Box>(copies[wall.Id])!));
        Assert.Equal(["zz-panel", "zz-board"], copyLine.Segments.Select(s => s.Method));
        Assert.Equal(copies[window.Id], copyLine.Segments[0].To);

        // Mirrored across a plane perpendicular to the wall's length: the window lands 96"-132" along
        // the copy, so the short segment is now at the end, and it keeps "zz-panel".
        var mirrored = GroupCopy.Mirror(sketch, [wall, window], Axis.X, Length.Inches(200), out Box? refused);
        Assert.Null(refused);
        Sketch withMirror = mirrored!.Value.Requests.OfType<AddEntity>().Aggregate(sketch, (s, add) => s.WithEntity(add.Entity));
        Napkin.Modules.Building.WallLine mirrorLine = Napkin.Modules.Building.WallLine.Of(withMirror, new Napkin.Modules.Building.Wall(withMirror.Find<Box>(mirrored.Value.Copies[wall.Id])!));
        Assert.Equal([Length.Inches(96), Length.Inches(12)], mirrorLine.Segments.Select(s => s.Length));
        Assert.Equal(["zz-board", "zz-panel"], mirrorLine.Segments.Select(s => s.Method));

        // The wall copied alone has no window: its one segment merges the two, whose methods differ,
        // so it is not braced (never inferred).
        (ImmutableList<Request> alone, ImmutableDictionary<EntityId, EntityId> only) = GroupCopy.Duplicate(sketch, [wall], Vector3.Along(Axis.Y, Length.Inches(48)));
        Sketch withAlone = alone.OfType<AddEntity>().Aggregate(sketch, (s, add) => s.WithEntity(add.Entity));
        Napkin.Modules.Building.WallSegment whole = Assert.Single(Napkin.Modules.Building.WallLine.Of(withAlone, new Napkin.Modules.Building.Wall(withAlone.Find<Box>(only[wall.Id])!)).Segments);
        Assert.Equal(Napkin.Modules.Building.AssignmentOrigin.MergedConflict, whole.Origin);
    }

    [Fact]
    [Trait("Feature", "BLD-001")]
    public void A_duplicated_wall_carries_what_it_supports_and_its_stud_spacing()
    {
        Box wall = new(EditingBuilder.Id(9), LayerId.Default, Point3.Inches(0, 0, 0), Length.Inches(144), Length.Inches(3, 1, 2), Length.Inches(96), BoxFace.Top, Angle.Zero)
        {
            WallInputs = new WallInputs("zz-roof", Length.Inches(24)),
        };
        DesignEditor editor = new();
        editor.Open(new Design("Wall", Sketch.Empty.WithEntity(wall), ImmutableDictionary<EntityId, string>.Empty.Add(wall.Id, "Wall 1")));
        editor.SelectAll([wall.Id]);

        SelectionCommands.Duplicate(editor, gridStepInches: 1);

        Box copy = Assert.Single(editor.Selection.Select(editor.Sketch.Find<Box>).OfType<Box>());
        Assert.NotEqual(wall.Id, copy.Id);
        Assert.Equal(new WallInputs("zz-roof", Length.Inches(24)), copy.WallInputs);

        // Undo takes the copy away and leaves the original's inputs as they were.
        Assert.True(editor.Undo());
        Assert.Equal(wall.WallInputs, editor.Sketch.Find<Box>(wall.Id)!.WallInputs);
        Assert.Null(editor.Sketch.Find<Box>(copy.Id));
    }

    [Fact]
    public void Duplicating_two_parts_brings_the_relationship_between_them_and_nothing_else()
    {
        DesignEditor editor = Table();
        editor.SelectAll([Leg.Id, Apron.Id]);

        SelectionCommands.Duplicate(editor, gridStepInches: 1);

        Sketch after = editor.Sketch;
        Box[] copies = [.. editor.Selection.Select(after.Find<Box>).OfType<Box>()];
        Assert.Equal(2, copies.Length);
        Box legCopy = Assert.Single(copies, copy => copy.Width == Leg.Width);
        Box apronCopy = Assert.Single(copies, copy => copy.Width == Apron.Width);

        // The flush between them, renamed onto the copies; not the inset to the top, not the
        // apron's own typed length.
        Flush copied = Assert.Single(after.RelationshipsInOrder.OfType<Flush>(), flush => flush.References.Contains(legCopy.Id));
        Assert.Equal(new FeatureRef(legCopy.Id, BoxFeature.Face(BoxFace.East)), copied.A);
        Assert.Equal(new FeatureRef(apronCopy.Id, BoxFeature.Face(BoxFace.West)), copied.B);
        Assert.Equal(Table().Sketch.Relationships.Count + 1, after.Relationships.Count);

        // Moved together, level with the originals, and clear of them.
        Assert.Equal(legCopy.Anchor - Leg.Anchor, apronCopy.Anchor - Apron.Anchor);
        Assert.Equal(Length.Zero, (legCopy.Anchor - Leg.Anchor).Dz);

        Assert.True(editor.Undo());
        Assert.Equal(3, editor.Sketch.Entities.Count);
    }

    [Fact]
    public void A_leg_mirrored_east_to_west_lands_where_the_opposite_leg_goes()
    {
        DesignEditor editor = Table();
        editor.Select(Leg.Id);

        EntityId copy = SelectionCommands.Mirror(editor, Axis.X)!.Value;

        // The drawing spans 0 to 48 east-west; the leg's 1 1/2 to 4 reflects to 44 to 46 1/2.
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(editor.Sketch.Find<Box>(copy)!);
        Assert.Equal(new Point3(Length.Inches(44), Length.Inches(1, 1, 2), Length.Zero), low);
        Assert.Equal(Length.Inches(46, 1, 2), high.X);
        Assert.Equal(copy, editor.OnlySelected);
        Assert.Contains("Mirrored Leg east–west", editor.LastMessage!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Mirroring_a_leg_and_its_apron_mirrors_the_flush_between_them_and_it_still_holds()
    {
        DesignEditor editor = Table();
        editor.SelectAll([Leg.Id, Apron.Id]);

        SelectionCommands.Mirror(editor, Axis.X);

        Sketch after = editor.Sketch;
        Box[] copies = [.. editor.Selection.Select(after.Find<Box>).OfType<Box>()];
        Box legCopy = Assert.Single(copies, box => box.Width == Leg.Width);
        Box apronCopy = Assert.Single(copies, box => box.Width == Apron.Width);

        // Across the middle, the apron is west of the leg: the flush is the leg's west face and
        // the apron's east face now, and it holds where the copies are.
        Flush mirrored = Assert.Single(after.RelationshipsInOrder.OfType<Flush>(), flush => flush.References.Contains(legCopy.Id));
        Assert.Equal(new FeatureRef(legCopy.Id, BoxFeature.Face(BoxFace.West)), mirrored.A);
        Assert.Equal(new FeatureRef(apronCopy.Id, BoxFeature.Face(BoxFace.East)), mirrored.B);
        Assert.Equal(SpaceSnapResolver.Extent(legCopy).Low.X, SpaceSnapResolver.Extent(apronCopy).High.X);
    }

    // A butt joint, pocket screws from the apron's south face, glued: the apron's west end against the leg's east face.
    static readonly Joint ApronJoint = new(
        RelationshipId.New(),
        new FeatureRef(Leg.Id, BoxFeature.Face(BoxFace.East)),
        new FeatureRef(Apron.Id, BoxFeature.Face(BoxFace.West)),
        JointType.Butt,
        null,
        new Fastening(FasteningKind.PocketScrews, 3, BoxFace.South),
        true);

    // The leg's top against the underside of the top, held with screws: the top is not copied below.
    static readonly Joint TopJoint = new(
        RelationshipId.New(),
        new FeatureRef(Top.Id, BoxFeature.Face(BoxFace.Bottom)),
        new FeatureRef(Leg.Id, BoxFeature.Face(BoxFace.Top)),
        JointType.Butt,
        null,
        new Fastening(FasteningKind.Screws, null, null),
        false);

    static DesignEditor JoinedTable()
    {
        DesignEditor editor = Table();
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddRelationship(ApronJoint), "join the apron to the leg"));
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddRelationship(TopJoint), "join the leg to the top"));
        return editor;
    }

    [Fact]
    public void Duplicating_two_joined_parts_copies_the_joint_between_them_and_not_the_one_to_a_part_left_behind()
    {
        DesignEditor editor = JoinedTable();
        editor.SelectAll([Leg.Id, Apron.Id]);

        SelectionCommands.Duplicate(editor, gridStepInches: 1);

        Sketch after = editor.Sketch;
        Box[] copies = [.. editor.Selection.Select(after.Find<Box>).OfType<Box>()];
        Box legCopy = Assert.Single(copies, copy => copy.Width == Leg.Width);
        Box apronCopy = Assert.Single(copies, copy => copy.Width == Apron.Width);

        // Two joints before, three after: the apron's joint is copied onto the copies (new id, same type,
        // fastening and glue); the leg-to-top joint is not, because the top was not copied.
        Assert.Equal(3, after.Relationships.Values.OfType<Joint>().Count());
        Joint copied = Assert.Single(after.RelationshipsInOrder.OfType<Joint>(), joint => joint.References.Contains(apronCopy.Id));
        Assert.NotEqual(ApronJoint.Id, copied.Id);
        Assert.Equal(new FeatureRef(legCopy.Id, BoxFeature.Face(BoxFace.East)), copied.Receiving);
        Assert.Equal(new FeatureRef(apronCopy.Id, BoxFeature.Face(BoxFace.West)), copied.Inserted);
        Assert.Equal(ApronJoint.Fastening, copied.Fastening);
        Assert.True(copied.Glue);
        Assert.DoesNotContain(after.RelationshipsInOrder.OfType<Joint>(), joint => joint.References.Contains(legCopy.Id) && joint.References.Contains(Top.Id));
        Assert.True(RelationshipChecker.IsSatisfied(after, copied));
    }

    [Fact]
    public void Mirroring_joined_parts_mirrors_the_joint_including_the_pocket_hole_face()
    {
        DesignEditor editor = JoinedTable();
        editor.SelectAll([Leg.Id, Apron.Id]);

        SelectionCommands.Mirror(editor, Axis.Y);

        Sketch after = editor.Sketch;
        Box[] copies = [.. editor.Selection.Select(after.Find<Box>).OfType<Box>()];
        Box apronCopy = Assert.Single(copies, box => box.Width == Apron.Width);
        Joint mirrored = Assert.Single(after.RelationshipsInOrder.OfType<Joint>(), joint => joint.References.Contains(apronCopy.Id));

        // Reflected across a north-south plane the contact faces (east and west) stay, and the
        // pocket holes that were drilled from the south face are drilled from the north.
        Assert.Equal(BoxFace.East, mirrored.Receiving.Feature.Faces[0]);
        Assert.Equal(BoxFace.West, mirrored.Inserted.Feature.Faces[0]);
        Assert.Equal(BoxFace.North, mirrored.Fastening.PocketFace);
        Assert.True(RelationshipChecker.IsSatisfied(after, mirrored));
    }

    [Fact]
    public void A_joint_survives_a_move_and_undo_and_redo_and_goes_with_its_part()
    {
        DesignEditor editor = JoinedTable();

        // Nothing but the joint holds the apron to the leg: drop the flush that would hold it there.
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new RemoveRelationship(ApronOnLeg.Id), "release the flush"));

        // The apron is moved off the leg: the move is not refused, the joint is still there, unsatisfied.
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new SetPosition(Apron.Id, Apron.Anchor + new Vector3(Length.Inches(2), Length.Zero, Length.Zero)), "move the apron"));
        Assert.False(RelationshipChecker.IsSatisfied(editor.Sketch, (Joint)editor.Sketch.Find(ApronJoint.Id)!));

        Assert.True(editor.Undo());
        Assert.True(RelationshipChecker.IsSatisfied(editor.Sketch, (Joint)editor.Sketch.Find(ApronJoint.Id)!));
        Assert.True(editor.Redo());
        Assert.False(RelationshipChecker.IsSatisfied(editor.Sketch, (Joint)editor.Sketch.Find(ApronJoint.Id)!));

        // Removing the apron takes its joint with it, and undo brings both back.
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new RemoveEntity(Apron.Id), "remove the apron"));
        Assert.Null(editor.Sketch.Find(ApronJoint.Id));
        Assert.NotNull(editor.Sketch.Find(TopJoint.Id));
        Assert.True(editor.Undo());
        Assert.NotNull(editor.Sketch.Find(ApronJoint.Id));
    }

    [Theory]
    [InlineData("Leg", new string[0], "Leg (2)")]
    [InlineData("Leg", new[] { "Leg (2)" }, "Leg (3)")]
    [InlineData("Leg (2)", new[] { "Leg", "Leg (2)" }, "Leg (3)")]
    [InlineData("Leg (9)", new[] { "Leg (9)", "Leg (10)" }, "Leg (11)")]
    [InlineData("", new string[0], "")]
    public void A_copy_is_named_after_its_original_with_the_next_free_number(string name, string[] others, string expected)
    {
        HashSet<string> taken = [name, .. others];

        Assert.Equal(expected, GroupCopy.CopyName(name, taken));
    }

    [Fact]
    public void Duplicated_and_mirrored_copies_have_names_of_their_own()
    {
        DesignEditor editor = Table();
        editor.Select(Leg.Id);

        EntityId duplicate = SelectionCommands.Duplicate(editor, 1)!.Value;
        editor.Select(Leg.Id);
        EntityId mirrored = SelectionCommands.Mirror(editor, Axis.X)!.Value;

        Assert.Equal("Leg (2)", editor.NameOf(duplicate));
        Assert.Equal("Leg (3)", editor.NameOf(mirrored));
    }

    [Fact]
    public void A_part_with_cuts_is_not_mirrored_the_wrong_way_round()
    {
        DesignEditor editor = Table();
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new SetCut(Leg.Id, new CornerCut(BoxCorner.NorthEast, Length.Inches(1), Length.Inches(1))), "cut the leg"));
        editor.SelectAll([Leg.Id, Apron.Id]);
        int before = editor.Sketch.Entities.Count;

        Assert.Null(SelectionCommands.Mirror(editor, Axis.X));
        Assert.Equal(before, editor.Sketch.Entities.Count);
        Assert.Contains("Leg has cuts", editor.LastMessage!.Text, StringComparison.Ordinal);
    }
}

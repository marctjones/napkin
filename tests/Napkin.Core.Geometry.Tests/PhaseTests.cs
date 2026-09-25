using System.Text.RegularExpressions;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Phase and the note (docs/design/renovation-sketches.md §6.1, §7, test 2): every entity is
/// existing, new or demolish; the after and before views drop the right entities and whatever names
/// them, and leave the sketch alone; a note survives every exhaustive switch over entities.
/// </summary>
public class PhaseTests
{
    private static readonly DirectUpdater Updater = new();

    [Fact]
    public void A_new_entity_is_new_and_a_copy_carries_its_phase()
    {
        SketchBuilder builder = new();
        Box box = builder.BoxOf(builder.AddBox(0, 0, 10, 4));

        Assert.Equal(Phase.New, box.Phase);
        Box existing = box with { Phase = Phase.Existing };
        Assert.Equal(Phase.Existing, (existing with { Anchor = Point3.Origin }).Phase);
        Assert.NotEqual(box, existing);
        Assert.NotEqual(box.GetHashCode(), existing.GetHashCode());
        Assert.Equal(Phase.Demolish, new Note(EntityId.New(), LayerId.Default, Point2.Origin, "drain", NoteSymbol.Drain) { Phase = Phase.Demolish }.Phase);
    }

    [Fact]
    public void Setting_a_phase_is_one_exact_request_that_moves_nothing()
    {
        SketchBuilder builder = new();
        EntityId wall = builder.AddBox(0, 0, 144, 4);
        EntityId window = builder.AddBox(54, 0, 36, 4);

        Solved marked = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetPhase(wall, Phase.Existing)));

        Assert.Equal(Phase.Existing, marked.Sketch.Find(wall)!.Phase);
        Assert.Equal(Phase.New, marked.Sketch.Find(window)!.Phase);
        Assert.Equal(builder.BoxOf(wall) with { Phase = Phase.Existing }, marked.Sketch.Find(wall));
        Assert.Equal([wall], marked.Changes.Modified);
        Assert.Empty(marked.Changes.Moved);
        Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetPhase(EntityId.New(), Phase.Demolish)));
    }

    [Fact]
    public void After_keeps_existing_and_new_and_before_keeps_existing_and_demolish()
    {
        SketchBuilder builder = new();
        EntityId existing = builder.AddBox(0, 0, 10, 4);
        EntityId added = builder.AddBox(20, 0, 10, 4);
        EntityId removed = builder.AddBox(40, 0, 10, 4);
        Sketch sketch = builder.Sketch
            .WithEntity(builder.BoxOf(existing) with { Phase = Phase.Existing })
            .WithEntity(builder.BoxOf(removed) with { Phase = Phase.Demolish });

        Assert.Equal([existing, added], sketch.After().Entities.Keys.Order());
        Assert.Equal([existing, removed], sketch.Before().Entities.Keys.Order());

        // The sketch itself is unchanged: views are derived, never stored.
        Assert.Equal(3, sketch.Entities.Count);
    }

    [Fact]
    public void A_view_drops_the_relationships_segments_and_dimensions_that_name_a_dropped_entity()
    {
        SketchBuilder builder = new();
        EntityId kept = builder.AddBox(0, 0, 10, 4);
        EntityId gone = builder.AddBox(10, 0, 10, 4);
        EntityId start = builder.AddNode(0, 20);
        EntityId end = builder.AddNode(10, 20);
        EntityId line = builder.AddSegment(start, end);
        RelationshipId flush = builder.Flush(kept, BoxEdge.East, gone, BoxEdge.West);
        RelationshipId keptWidth = builder.WidthIs(kept, Length.Inches(10));
        RelationshipId goneWidth = builder.WidthIs(gone, Length.Inches(10));
        EntityId measuresGone = builder.AddDimension(new ParamMeasurand(new BoxWidthRef(gone)), goneWidth);
        EntityId drivesKept = builder.AddDimension(new ParamMeasurand(new BoxWidthRef(kept)), keptWidth);
        Sketch sketch = builder.Sketch
            .WithEntity(builder.BoxOf(gone) with { Phase = Phase.Demolish })
            .WithEntity(builder.NodeOf(start) with { Phase = Phase.Demolish });

        Sketch after = sketch.After();

        Assert.Equal([kept, end, drivesKept], after.Entities.Keys.Order());
        Assert.Null(after.Find(line));
        Assert.Null(after.Find(measuresGone));
        Assert.Equal([keptWidth], after.Relationships.Keys);
        Assert.Equal(keptWidth, after.Find<Dimension>(drivesKept)!.Drives);
        Assert.True(after.Validate().IsValid, after.Validate().ToString());
        Assert.NotNull(sketch.Find(flush));
    }

    [Fact]
    public void A_dimension_driven_by_a_dropped_relationship_becomes_a_reference_dimension_in_the_view()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 4);
        EntityId other = builder.AddBox(20, 0, 10, 4);
        RelationshipId equal = builder.EqualWidths(box, other);
        EntityId dimension = builder.AddDimension(new ParamMeasurand(new BoxWidthRef(box)), equal);
        Sketch sketch = builder.Sketch.WithEntity(builder.BoxOf(other) with { Phase = Phase.New });

        Sketch before = sketch.Before();

        Assert.Null(before.Find(other));
        Assert.Null(before.Find<Dimension>(dimension));
        Assert.Same(sketch, sketch.After());

        Sketch existingBox = sketch.WithEntity(builder.BoxOf(box) with { Phase = Phase.Existing })
            .WithEntity(sketch.Find<Dimension>(dimension)! with { Phase = Phase.Existing });
        Sketch view = existingBox.Before();
        Assert.Null(view.Find<Dimension>(dimension)!.Drives);
        Assert.Empty(view.Relationships);
    }

    [Fact]
    public void A_note_moves_where_it_is_put_and_says_what_it_is_told()
    {
        EntityId id = EntityId.New();
        Sketch sketch = Sketch.Empty.WithEntity(new Note(id, LayerId.Default, Point2.Inches(1, 2), "outlet", NoteSymbol.Outlet));

        Solved dragged = Assert.IsType<Solved>(Updater.Apply(sketch, new Drag(id, new Vector3(Length.Inches(3), Length.Inches(4), Length.Inches(5)))));
        Assert.Equal(Point2.Inches(4, 6), dragged.Sketch.Find<Note>(id)!.Position);
        Assert.Equal([id], dragged.Changes.Moved);

        Solved still = Assert.IsType<Solved>(Updater.Apply(sketch, new Drag(id, new Vector3(Length.Zero, Length.Zero, Length.Inches(5)))));
        Assert.Same(sketch, still.Sketch);

        Solved placed = Assert.IsType<Solved>(Updater.Apply(sketch, new SetPosition(id, new Point3(Length.Inches(9), Length.Inches(8), Length.Zero))));
        Assert.Equal(Point2.Inches(9, 8), placed.Sketch.Find<Note>(id)!.Position);
        Solved stays = Assert.IsType<Solved>(Updater.Apply(sketch, new SetPosition(id, new Point3(Length.Inches(1), Length.Inches(2), Length.Zero))));
        Assert.True(stays.Changes.IsEmpty);

        Solved said = Assert.IsType<Solved>(Updater.Apply(sketch, new SetNote(id, "switch", NoteSymbol.Switch)));
        Assert.Equal(("switch", NoteSymbol.Switch), (said.Sketch.Find<Note>(id)!.Text, said.Sketch.Find<Note>(id)!.Symbol));
        Assert.IsType<Rejected>(Updater.Apply(sketch, new SetNote(EntityId.New(), "x", NoteSymbol.None)));

        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 1, 1);
        Assert.Equal(RejectionReason.DanglingReference, Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetNote(box, "x", NoteSymbol.None))).Reason);
    }

    [Fact]
    public void A_note_is_added_removed_and_validated_like_any_entity()
    {
        EntityId id = EntityId.New();
        Note note = new(id, LayerId.Default, Point2.Origin, string.Empty, NoteSymbol.Light);

        Solved added = Assert.IsType<Solved>(Updater.Apply(Sketch.Empty, new AddEntity(note)));
        Assert.True(added.Sketch.Validate().IsValid);
        Assert.Equal(note, added.Sketch.Find(id));
        Solved removed = Assert.IsType<Solved>(Updater.Apply(added.Sketch, new RemoveEntity(id)));
        Assert.Empty(removed.Sketch.Entities);
        Assert.Equal(note with { Layer = LayerId.Default }, note.OnLayer(LayerId.Default));
        Assert.Equal("(2) 2x6", new TypedHeader(2, "2x6").ToString());
    }

    [Fact]
    public void A_rooms_inputs_are_set_exactly_and_refused_when_a_coverage_or_a_size_is_not_positive()
    {
        SketchBuilder builder = new();
        EntityId room = builder.AddBox(0, 0, 168, 144);
        RoomInputs finished = RoomInputs.None with { Drywall = RoomSurfaces.WallsAndCeiling, Sheet = new SheetSize(Length.Inches(48), Length.Inches(96)) };

        Solved set = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetRoomInputs(room, finished)));
        Assert.Equal(finished, set.Sketch.Find<Box>(room)!.Room);
        Assert.NotEqual(builder.BoxOf(room), set.Sketch.Find<Box>(room));
        Assert.Equal(10, RoomInputs.None.FlooringWaste);
        Assert.Null(RoomRules.Refusal(finished));

        foreach (RoomInputs bad in new[]
                 {
                     RoomInputs.None with { Sheet = new SheetSize(Length.Zero, Length.Inches(96)) },
                     RoomInputs.None with { Sheet = new SheetSize(Length.Inches(48), Length.Zero) },
                     RoomInputs.None with { InsulationCoverage = 0 },
                     RoomInputs.None with { PaintCoverage = -1 },
                     RoomInputs.None with { FlooringBox = 0 },
                     RoomInputs.None with { PaintCoats = 0 },
                     RoomInputs.None with { FlooringWaste = -1 },
                     RoomInputs.None with { BaseboardStick = Length.Zero },
                     RoomInputs.None with { Measured = MeasuredRoom.None with { Diagonal2 = Length.Zero } },
                 })
        {
            Assert.NotNull(RoomRules.Refusal(bad));
            Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetRoomInputs(room, bad)));
        }

        Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetRoomInputs(EntityId.New(), finished)));
        Sketch withNode = builder.Sketch;
        EntityId node = builder.AddNode(0, 0);
        Assert.Equal(RejectionReason.DanglingReference, Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetRoomInputs(node, finished))).Reason);
        Assert.NotNull(withNode);
    }

    [Fact]
    public void A_walls_side_bearing_and_header_count_in_its_value_and_in_saying_nothing()
    {
        WallInputs nothing = new(null, null);
        Assert.Null((nothing with { Side = null }).OrNull());
        Assert.NotNull((nothing with { Side = WallSide.Interior }).OrNull());
        Assert.NotNull((nothing with { Bearing = false }).OrNull());
        Assert.NotNull((nothing with { Header = new TypedHeader(2, "2x6") }).OrNull());
        Assert.NotEqual(nothing with { Side = WallSide.Exterior }, nothing with { Side = WallSide.Interior });
        Assert.NotEqual(nothing with { Bearing = true }, nothing with { Bearing = false });
        Assert.NotEqual(nothing with { Header = new TypedHeader(2, "2x6") }, nothing with { Header = new TypedHeader(1, "2x6") });
        Assert.Equal((nothing with { Bearing = true }).GetHashCode(), (nothing with { Bearing = true }).GetHashCode());
    }

    /// <summary>
    /// Every source file that switches over the entity kinds names <see cref="Note"/> too, or is
    /// listed here with why it may ignore notes (renovation §12 risk 1). A new switch fails this
    /// until someone decides what a note does there.
    /// </summary>
    [Fact]
    public void Every_switch_over_entity_kinds_has_decided_what_a_note_does()
    {
        Dictionary<string, string> ignoresNotes = new()
        {
            ["src/Napkin.Core.Geometry/Propagator.cs"] = "reads scalars of boxes and nodes; a note has no relationships, so never reaches it",
            ["src/Napkin.App/Viewing/Bounds3.cs"] = "the 3D bounds of solids; a note has no size",
        };

        string root = RepositoryRoot();
        Regex kind = new(@"\bcase (Node|Segment|Dimension)\b|\b(Node|Segment|Dimension) [a-z]\w* =>|\bis (Node|Segment)\b|\bSceneNames\.(Node|Segment)\b");
        List<string> missing = [];
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            if (kind.IsMatch(text) && !Regex.IsMatch(text, @"\bNote\b|\bNoteType\b") && !ignoresNotes.ContainsKey(relative))
            {
                missing.Add(relative);
            }
        }

        Assert.True(missing.Count == 0, "These files switch over entity kinds and never say what a note does: " + string.Join(", ", missing));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "napkin.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("napkin.sln not found.");
    }
}

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The structural golden cases 9 to 12 of docs/design/geometry-model.md &#xA7;7.1, plus the rest
/// of &#xA7;8 step 7.
/// </summary>
public class DirectUpdaterStructureTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    [Fact]
    public void Case9_RemovingABoxTakesItsRelationshipsAndItsDimensionWithIt()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(30, 0, 10, 4);
        RelationshipId flush = builder.Flush(a, BoxEdge.East, b, BoxEdge.West);
        RelationshipId width = builder.WidthIs(a, Length.Inches(30));
        EntityId dimension = builder.AddDimension(new ParamMeasurand(new BoxWidthRef(a)), width);

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new RemoveEntity(a)));

        Assert.Null(result.Sketch.Find(a));
        Assert.Null(result.Sketch.Find(dimension));
        Assert.Null(result.Sketch.Find(flush));
        Assert.Null(result.Sketch.Find(width));
        Assert.NotNull(result.Sketch.Find(b));

        // The change set lists all three, so the canvas can say what else went.
        Assert.Equal(new[] { a, dimension }, result.Changes.Removed.OrderBy(id => id));
        Assert.Equal(new[] { flush, width }, result.Changes.RelationshipsRemoved.OrderBy(id => id));
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void Case10_RemovingADrivingRelationshipLeavesAReferenceDimensionAndTheGeometryAlone()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4);
        RelationshipId width = builder.WidthIs(box, Length.Inches(30));
        EntityId dimension = builder.AddDimension(new ParamMeasurand(new BoxWidthRef(box)), width);

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new RemoveRelationship(width)));

        Dimension demoted = Assert.IsType<Dimension>(result.Sketch.Find(dimension));
        Assert.Null(demoted.Drives);
        Assert.Equal(new ParamMeasurand(new BoxWidthRef(box)), demoted.Measures);

        // The geometry stays where it is; it is simply no longer held there.
        SketchAssert.BoxIs(result.Sketch, box, 0, 0, 30, 4);
        Assert.Empty(result.Changes.Moved);
        Assert.Equal(new[] { width }, result.Changes.RelationshipsRemoved);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void Case11_ARelationshipKindTheDirectUpdaterDoesNotImplementIsRejected()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(0, 10, 30, 4);

        UpdateResult result = Updater.Apply(builder.Sketch, new AddRelationship(new Parallel(
            SketchBuilder.RelationshipIdAt(99),
            new BoxEdgeRef(a, BoxEdge.South),
            new BoxEdgeRef(b, BoxEdge.South))));

        Assert.Equal(new Rejected(RejectionReason.UnsupportedRelationship), result);
        Assert.DoesNotContain(typeof(Parallel), Updater.SupportedRelationships);
        Assert.Contains(typeof(Flush), Updater.SupportedRelationships);
    }

    [Fact]
    public void Case12_EditingANumberThatNoRelationshipOwnsIsRejected()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4);

        // A reference dimension has no driving relationship, so there is no number to set. The
        // canvas resolves a dimension to its driving relationship before issuing SetParameter,
        // and a reference dimension has none; see docs/design/geometry-model.md §10.
        EntityId dimension = builder.AddDimension(new ParamMeasurand(new BoxWidthRef(box)));
        Assert.Null(builder.Sketch.Find<Dimension>(dimension)!.Drives);

        Assert.Equal(
            new Rejected(RejectionReason.UnknownRelationship),
            Updater.Apply(builder.Sketch, new SetParameter(SketchBuilder.RelationshipIdAt(99), Length.Inches(40))));
    }

    [Fact]
    public void AddingAnEntityValidatesItBeforeItGoesIn()
    {
        SketchBuilder builder = new();
        EntityId existing = builder.AddBox(0, 0, 10, 10);

        Assert.Equal(
            new Rejected(RejectionReason.NonPositiveSize),
            Updater.Apply(builder.Sketch, new AddEntity(
                new Box(EntityId.New(), LayerId.Default, Point2.Origin, Length.Zero, Length.Inches(4), Angle.Zero))));

        Assert.Equal(
            new Rejected(RejectionReason.RotationNotSupported),
            Updater.Apply(builder.Sketch, new AddEntity(new Box(
                EntityId.New(), LayerId.Default, Point2.Origin, Length.Inches(4), Length.Inches(4), Angle.Degrees(45)))));

        Assert.Equal(
            new Rejected(RejectionReason.DanglingReference),
            Updater.Apply(builder.Sketch, new AddEntity(
                new Node(EntityId.New(), LayerId.New(), Point2.Origin))));

        Assert.Equal(
            new Rejected(RejectionReason.DanglingReference),
            Updater.Apply(builder.Sketch, new AddEntity(new Segment(
                EntityId.New(), LayerId.Default, existing, SketchBuilder.EntityIdAt(99)))));

        Assert.Equal(
            new Rejected(RejectionReason.DuplicateEntity),
            Updater.Apply(builder.Sketch, new AddEntity(builder.BoxOf(existing))));

        Solved added = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new AddEntity(
            new Node(EntityId.New(), LayerId.Default, Point2.Inches(3, 3)))));
        Assert.Single(added.Changes.Added);
        SketchAssert.IsConsistent(added.Sketch);
    }

    [Fact]
    public void RemovingANodeTakesTheSegmentsAndDimensionsThatNeededIt()
    {
        SketchBuilder builder = new();
        EntityId start = builder.AddNode(0, 0);
        EntityId end = builder.AddNode(10, 0);
        EntityId segment = builder.AddSegment(start, end);
        EntityId dimension = builder.AddDimension(new ParamMeasurand(new SegmentLengthRef(segment)));
        RelationshipId horizontal = builder.Add(id => new Horizontal(id, new SegmentRef(segment)));

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new RemoveEntity(start)));

        Assert.Null(result.Sketch.Find(segment));
        Assert.Null(result.Sketch.Find(dimension));
        Assert.Null(result.Sketch.Find(horizontal));
        Assert.NotNull(result.Sketch.Find(end));
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void UnknownIdsAreRejectedRatherThanThrown()
    {
        Sketch sketch = Sketch.Empty;

        Assert.Equal(
            new Rejected(RejectionReason.UnknownEntity),
            Updater.Apply(sketch, new RemoveEntity(SketchBuilder.EntityIdAt(99))));
        Assert.Equal(
            new Rejected(RejectionReason.UnknownRelationship),
            Updater.Apply(sketch, new RemoveRelationship(SketchBuilder.RelationshipIdAt(99))));
        Assert.Equal(
            new Rejected(RejectionReason.UnknownEntity),
            Updater.Apply(sketch, new SetLayer(SketchBuilder.EntityIdAt(99), LayerId.Default)));
        Assert.Equal(
            new Rejected(RejectionReason.UnknownEntity),
            Updater.Apply(sketch, new SetPosition(SketchBuilder.EntityIdAt(99), Point2.Origin)));
        Assert.Equal(
            new Rejected(RejectionReason.UnknownEntity),
            Updater.Apply(sketch, new SetRotation(SketchBuilder.EntityIdAt(99), Angle.Right)));
    }

    [Fact]
    public void ADuplicateRelationshipIsRefusedBecauseInvariant4ForbidsIt()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(30, 0, 10, 4);
        builder.Flush(a, BoxEdge.East, b, BoxEdge.West);

        Assert.Equal(
            new Rejected(RejectionReason.DuplicateRelationship),
            Updater.Apply(builder.Sketch, new AddRelationship(new Flush(
                SketchBuilder.RelationshipIdAt(99),
                new BoxEdgeRef(a, BoxEdge.East),
                new BoxEdgeRef(b, BoxEdge.West)))));
    }

    [Fact]
    public void SetLayerMovesAnEntityWithoutMovingIt()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4);
        Layer other = new(LayerId.New(), "Shelves");
        Sketch withLayer = builder.Sketch.WithLayer(other);

        Solved result = Assert.IsType<Solved>(Updater.Apply(withLayer, new SetLayer(box, other.Id)));

        Assert.Equal(other.Id, result.Sketch.Find(box)!.Layer);
        SketchAssert.BoxIs(result.Sketch, box, 0, 0, 30, 4);
        Assert.Equal(
            new Rejected(RejectionReason.DanglingReference),
            Updater.Apply(withLayer, new SetLayer(box, LayerId.New())));
    }

    [Fact]
    public void AnEmptyBatchIsANoOp()
    {
        Solved result = Assert.IsType<Solved>(Updater.Apply(Sketch.Empty, Batch.Of()));

        Assert.Equal(Sketch.Empty, result.Sketch);
        Assert.True(result.Changes.IsEmpty);
    }
}

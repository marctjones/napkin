using System.Collections.Immutable;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// <see cref="Sketch.Validate"/> against invariants 1, 2 and 4 of design &#xA7;2.5:
/// docs/design/geometry-model.md &#xA7;8 step 5.
/// </summary>
public class SketchTests
{
    [Fact]
    public void AHandBuiltSketchIsValidAndItsRelationshipsHold()
    {
        SketchBuilder builder = new();
        EntityId carcass = builder.AddBox(0, 0, 30, 24);
        EntityId shelf = builder.AddBox(0, 10, 30, 1);
        builder.Anchor(carcass);
        builder.Flush(carcass, BoxEdge.West, shelf, BoxEdge.West);
        builder.WidthIs(carcass, Length.Inches(30));
        RelationshipId shelfWidth = builder.WidthIs(shelf, Length.Inches(30));
        builder.AddDimension(new ParamMeasurand(new BoxWidthRef(shelf)), shelfWidth);

        SketchAssert.IsConsistent(builder.Sketch);
        Assert.Equal(3, builder.Sketch.Entities.Count);
        Assert.Equal(4, builder.Sketch.Relationships.Count);
    }

    [Fact]
    public void EmptyHasOneLayerAndNothingElse()
    {
        Assert.True(Sketch.Empty.Validate().IsValid);
        Assert.Empty(Sketch.Empty.Entities);
        Assert.Empty(Sketch.Empty.Relationships);
        Assert.Equal(Layer.Default, Assert.Single(Sketch.Empty.Layers));
    }

    [Fact]
    public void RelationshipsAreIteratedInIdOrderNotDictionaryOrder()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 10);
        RelationshipId first = builder.Anchor(box);
        RelationshipId second = builder.WidthIs(box, Length.Inches(10));
        RelationshipId third = builder.HeightIs(box, Length.Inches(10));

        // Rebuild the dictionary in a different insertion order; the iteration order must not move.
        ImmutableDictionary<RelationshipId, Relationship> shuffled = ImmutableDictionary<RelationshipId, Relationship>.Empty
            .Add(third, builder.Sketch.Relationships[third])
            .Add(first, builder.Sketch.Relationships[first])
            .Add(second, builder.Sketch.Relationships[second]);

        Sketch reordered = builder.Sketch with { Relationships = shuffled };

        Assert.Equal(
            new[] { first, second, third },
            reordered.RelationshipsInOrder.Select(relationship => relationship.Id).ToArray());
    }

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant1_ADanglingReferenceIsReported()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 10);
        EntityId ghost = SketchBuilder.EntityIdAt(99);
        builder.Add(id => new Coincident(
            id,
            new CornerRef(box, BoxCorner.NorthEast),
            new NodeRef(ghost)));

        ValidationResult result = builder.Sketch.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Kind == ValidationErrorKind.DanglingReference);
    }

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant1_ASegmentWithoutItsNodesIsReported()
    {
        SketchBuilder builder = new();
        EntityId start = builder.AddNode(0, 0);
        builder.AddSegment(start, SketchBuilder.EntityIdAt(99));

        Assert.Contains(
            builder.Sketch.Validate().Errors,
            error => error.Kind == ValidationErrorKind.DanglingReference);
    }

    [Fact]
    public void Invariant1_AReferenceToTheWrongKindOfEntityIsReported()
    {
        SketchBuilder builder = new();
        EntityId node = builder.AddNode(0, 0);
        EntityId box = builder.AddBox(0, 0, 10, 10);
        builder.AddSegment(node, box);

        Assert.Contains(
            builder.Sketch.Validate().Errors,
            error => error.Kind == ValidationErrorKind.WrongEntityKind);
    }

    [Fact]
    public void Invariant1_ADimensionDrivenByAMissingRelationshipIsReported()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 10);
        builder.AddDimension(new ParamMeasurand(new BoxWidthRef(box)), new RelationshipId(Guid.NewGuid()));

        Assert.Contains(
            builder.Sketch.Validate().Errors,
            error => error.Kind == ValidationErrorKind.DanglingReference);
    }

    [Trait("Feature", "GEO-008")]
    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    public void Invariant2_ABoxMustHaveAPositiveWidthAndHeight(long width, long height)
    {
        SketchBuilder builder = new();
        builder.AddBox(Point2.Origin, Length.Inches(width), Length.Inches(height), Angle.Zero);

        Assert.Contains(
            builder.Sketch.Validate().Errors,
            error => error.Kind == ValidationErrorKind.NonPositiveSize);
    }

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant4_TwoRelationshipsSayingTheSameThingAreReported()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 10, 10);
        EntityId b = builder.AddBox(10, 0, 10, 10);
        builder.Flush(a, BoxEdge.South, b, BoxEdge.South);
        builder.Flush(a, BoxEdge.South, b, BoxEdge.South);

        Assert.Contains(
            builder.Sketch.Validate().Errors,
            error => error.Kind == ValidationErrorKind.DuplicateRelationship);
    }

    [Fact]
    public void AnEntityOnAnUnknownLayerIsReported()
    {
        Sketch sketch = Sketch.Empty.WithEntity(
            new Node(EntityId.New(), LayerId.New(), Point2.Origin));

        Assert.Contains(
            sketch.Validate().Errors,
            error => error.Kind == ValidationErrorKind.UnknownLayer);
    }

    [Fact]
    public void SketchesAreEqualByValueSoThatUndoAndRoundTripsCanCompareThem()
    {
        SketchBuilder one = new();
        EntityId box = one.AddBox(0, 0, 30, 4);
        one.Anchor(box);

        SketchBuilder two = new();
        two.AddBox(0, 0, 30, 4);
        two.Anchor(box);

        Assert.Equal(one.Sketch, two.Sketch);
        Assert.Equal(one.Sketch.GetHashCode(), two.Sketch.GetHashCode());
        Assert.NotEqual(one.Sketch, one.Sketch.WithoutRelationship(one.Sketch.Relationships.Keys.First()));
    }

    [Fact]
    public void UnchangedEntitiesStayReferenceEqualThroughAnUpdate()
    {
        // What makes P10's undo property (§7.2) true: structural sharing, not copying.
        SketchBuilder builder = new();
        EntityId untouched = builder.AddBox(0, 0, 10, 10);
        EntityId moved = builder.AddBox(20, 0, 10, 10);

        Sketch after = builder.Sketch.WithEntity(builder.BoxOf(moved) with { Anchor = Point2.Inches(21, 0) });

        Assert.Same(builder.Sketch.Entities[untouched], after.Entities[untouched]);
        Assert.NotSame(builder.Sketch.Entities[moved], after.Entities[moved]);
    }

    [Fact]
    public void GeometryIsReadThroughReferences()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(10, 20, 30, 8);
        EntityId node = builder.AddNode(1, 2);
        EntityId other = builder.AddNode(1, 6);
        EntityId segment = builder.AddSegment(node, other);

        Assert.Equal(Point2.Inches(40, 28), builder.Sketch.PointOf(new CornerRef(box, BoxCorner.NorthEast)));
        Assert.Equal(Point2.Inches(25, 24), builder.Sketch.PointOf(new CenterRef(box)));
        Assert.Equal(Point2.Inches(1, 2), builder.Sketch.PointOf(new NodeRef(node)));
        Assert.Equal(Length.Inches(30), builder.Sketch.ValueOf(new BoxWidthRef(box)));
        Assert.Equal(Length.Inches(8), builder.Sketch.ValueOf(new BoxHeightRef(box)));
        Assert.Equal(Length.Inches(4), builder.Sketch.ValueOf(new SegmentLengthRef(segment)));
        Assert.Equal(
            (Point2.Inches(40, 20), Point2.Inches(40, 28)),
            builder.Sketch.EdgeOf(new BoxEdgeRef(box, BoxEdge.East)));
    }
}

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// One test per confirmed finding of Fable's review of PR #35, so that none of them can come
/// back. The design bullets they pin are recorded in docs/design/geometry-model.md &#xA7;10.
/// </summary>
public class ReviewRegressionTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    [Trait("Feature", "GEO-010")]
    [Fact]
    public void Finding1_AnUnrelatedDimensionOnANeighbourDoesNotDecideWhichPartMoves()
    {
        // The same sketch as §7.1 case 2 with the Flush written the other way round and one
        // unrelated dimension on B. A ParamValue that restates a size nobody edited pins that size
        // but drives nothing, so it must not win the tie-break.
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(30, 0, 10, 4);
        RelationshipId widthOfA = builder.WidthIs(a, Length.Inches(30));
        builder.HeightIs(b, Length.Inches(4));
        builder.Add(id => new Flush(id, TestRefs.Edge(b, BoxEdge.West), TestRefs.Edge(a, BoxEdge.East)));

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(widthOfA, Length.Inches(40))));

        // A keeps its anchor and grows east; B translates. Writing the Flush (A, B) must give the
        // same answer, and did before this was fixed.
        SketchAssert.BoxIs(result.Sketch, a, 0, 0, 40, 4);
        SketchAssert.BoxIs(result.Sketch, b, 40, 0, 10, 4);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void Finding1_AddingACoincidentMovesTheSecondPointEvenWhenItsBoxIsDimensioned()
    {
        // §4.4 step 2: Coincident(p, q) assigns q := p. An unchanged ParamValue on q's box must
        // not reverse that.
        SketchBuilder builder = new();
        EntityId node = builder.AddNode(5, 5);
        EntityId box = builder.AddBox(20, 20, 10, 4);
        builder.WidthIs(box, Length.Inches(10));

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new AddRelationship(
            new Coincident(
                SketchBuilder.RelationshipIdAt(99),
                new NodeRef(node),
                TestRefs.Corner(box, BoxCorner.SouthWest)))));

        Assert.Equal(Point2.Inches(5, 5), result.Sketch.Find<Node>(node)!.Position);
        SketchAssert.BoxIs(result.Sketch, box, 5, 5, 10, 4);
    }

    [Trait("Feature", "GEO-011")]
    [Fact]
    public void Finding1_AConflictReportDoesNotBlameAnUnchangedDimension()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(30, 0, 10, 4);
        RelationshipId flush = builder.Flush(a, BoxEdge.East, b, BoxEdge.West);
        RelationshipId widthOfA = builder.WidthIs(a, Length.Inches(30));
        RelationshipId heightOfB = builder.HeightIs(b, Length.Inches(4));
        RelationshipId anchorA = builder.Anchor(a);
        RelationshipId anchorB = builder.Anchor(b);

        OverConstrained result = Assert.IsType<OverConstrained>(
            Updater.Apply(builder.Sketch, new SetParameter(widthOfA, Length.Inches(40))));

        Assert.Contains(anchorA, result.Conflict.Relationships);
        Assert.Contains(anchorB, result.Conflict.Relationships);
        Assert.Contains(flush, result.Conflict.Relationships);
        Assert.Contains(widthOfA, result.Conflict.Relationships);

        // B's height has nothing to do with A growing to the east.
        Assert.DoesNotContain(heightOfB, result.Conflict.Relationships);
    }

    [Fact]
    public void Finding2_DraggingACentredSpanByAnOddUnitIsNotAnError()
    {
        // §4.4: a drag is never an error. Before the half-unit rule was restored this threw,
        // because an odd shift lands on the other side of the half-to-even tie.
        SketchBuilder builder = new();
        EntityId left = builder.AddNode(0, 0);
        EntityId right = builder.AddNode(Point2.Origin with { X = new Length(3) });
        EntityId centre = builder.AddNode(Point2.Origin with { X = new Length(2) });
        builder.Add(id => new Centered(id, new NodeRef(centre), new NodeRef(left), new NodeRef(right), Axis.X));

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, Drag.InPlan(left, new Vector2(new Length(1), Length.Zero))));

        Assert.Equal(new Vector3(new Length(1), Length.Zero, Length.Zero), result.Changes.AppliedDelta);
        Assert.Equal(1, result.Sketch.Find<Node>(left)!.Position.X.Units);
        Assert.Equal(3, result.Sketch.Find<Node>(centre)!.Position.X.Units);
        Assert.Equal(4, result.Sketch.Find<Node>(right)!.Position.X.Units);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void Finding2_MovingTheMiddleOfAnOddSpanByOneUnitMovesBothEnds()
    {
        SketchBuilder builder = new();
        EntityId left = builder.AddNode(0, 0);
        EntityId right = builder.AddNode(Point2.Origin with { X = new Length(3) });
        EntityId centre = builder.AddNode(Point2.Origin with { X = new Length(2) });
        builder.Add(id => new Centered(id, new NodeRef(centre), new NodeRef(left), new NodeRef(right), Axis.X));

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            SetPosition.InPlan(centre, Point2.Origin with { X = new Length(3) })));

        Assert.Equal(3, result.Sketch.Find<Node>(centre)!.Position.X.Units);
        Assert.Equal(1, result.Sketch.Find<Node>(left)!.Position.X.Units);
        Assert.Equal(4, result.Sketch.Find<Node>(right)!.Position.X.Units);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void Finding3_AnchoringSomethingWithNoPositionOfItsOwnIsRefused()
    {
        SketchBuilder builder = new();
        EntityId start = builder.AddNode(0, 0);
        EntityId end = builder.AddNode(10, 0);
        EntityId segment = builder.AddSegment(start, end);
        EntityId dimension = builder.AddDimension(new ParamMeasurand(new SegmentLengthRef(segment)));

        // An anchor that silently does nothing is worse than a refusal.
        Assert.Equal(
            new Rejected(RejectionReason.DanglingReference),
            Updater.Apply(builder.Sketch, new AddRelationship(new Anchored(SketchBuilder.RelationshipIdAt(99), segment))));
        Assert.Equal(
            new Rejected(RejectionReason.DanglingReference),
            Updater.Apply(builder.Sketch, new AddRelationship(new Anchored(SketchBuilder.RelationshipIdAt(98), dimension))));

        // A node still anchors, and the anchor still holds it.
        Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new AddRelationship(new Anchored(SketchBuilder.RelationshipIdAt(97), start))));

        Assert.Contains(
            builder.Sketch.WithRelationship(new Anchored(SketchBuilder.RelationshipIdAt(99), segment)).Validate().Errors,
            error => error.Kind == ValidationErrorKind.WrongEntityKind);
    }

    [Fact]
    public void Finding4_ValidateCatchesAReferenceToTheWrongKindOfEntityBeforeTheCheckerThrows()
    {
        SketchBuilder builder = new();
        EntityId node = builder.AddNode(0, 0);
        EntityId other = builder.AddNode(5, 5);

        // A corner of a node is not a point. §6's loader validates and then checks, so this has to
        // be a load error and not an exception out of the checker.
        Sketch sketch = builder.Sketch.WithRelationship(new Coincident(
            SketchBuilder.RelationshipIdAt(99),
            TestRefs.Corner(node, BoxCorner.SouthWest),
            new NodeRef(other)));

        Assert.Contains(
            sketch.Validate().Errors,
            error => error.Kind == ValidationErrorKind.WrongEntityKind);
    }

    [Theory]
    [InlineData("boxWidthOnANode")]
    [InlineData("segmentLengthOnABox")]
    [InlineData("boxEdgeOnASegment")]
    [InlineData("dimensionMeasuringANode")]
    public void Finding4_EveryReferenceKindIsValidated(string shape)
    {
        SketchBuilder builder = new();
        EntityId node = builder.AddNode(0, 0);
        EntityId end = builder.AddNode(10, 0);
        EntityId segment = builder.AddSegment(node, end);
        EntityId box = builder.AddBox(0, 20, 10, 4);

        Sketch sketch = shape switch
        {
            "boxWidthOnANode" => builder.Sketch.WithRelationship(
                new ParamValue(SketchBuilder.RelationshipIdAt(99), new BoxWidthRef(node), Length.Inches(4))),
            "segmentLengthOnABox" => builder.Sketch.WithRelationship(
                new ParamValue(SketchBuilder.RelationshipIdAt(99), new SegmentLengthRef(box), Length.Inches(4))),
            "boxEdgeOnASegment" => builder.Sketch.WithRelationship(
                new Horizontal(SketchBuilder.RelationshipIdAt(99), TestRefs.Edge(segment, BoxEdge.South))),
            _ => builder.Sketch.WithEntity(new Dimension(
                SketchBuilder.EntityIdAt(50),
                LayerId.Default,
                new ParamMeasurand(new BoxHeightRef(node)),
                null,
                new DimensionPlacement(Length.Inches(8), DimensionSide.North))),
        };

        Assert.Contains(sketch.Validate().Errors, error => error.Kind == ValidationErrorKind.WrongEntityKind);
    }

    [Fact]
    public void Finding5_ANumeralTooLongToHoldIsRefusedRatherThanWrappedAround()
    {
        // 2^118 + 3 fits Int128; multiplying it by 1024 does not. Unchecked it wrapped to 3072
        // units and parsed as a confident, wrong 3".
        Assert.False(Length.TryParse("332306998946228968225951765070086147", out Length value, out bool wasRounded));
        Assert.Equal(Length.Zero, value);
        Assert.False(wasRounded);

        Assert.False(Length.TryParse(new string('9', 40), out _, out _));
        Assert.False(Length.TryParse("0." + new string('9', 40), out _, out _));
        Assert.Throws<FormatException>(() => Length.Parse(new string('9', 40)));
    }

    [Fact]
    public void Finding7_ACentredConflictNamesTheEndThatIsActuallyBlocked()
    {
        SketchBuilder builder = new();
        EntityId left = builder.AddNode(0, 0);
        EntityId right = builder.AddNode(10, 0);
        EntityId centre = builder.AddNode(5, 0);
        builder.Add(id => new Centered(id, new NodeRef(centre), new NodeRef(left), new NodeRef(right), Axis.X));
        RelationshipId anchorOnTheRight = builder.Anchor(right);

        OverConstrained result = Assert.IsType<OverConstrained>(
            Updater.Apply(builder.Sketch, SetPosition.InPlan(centre, Point2.Inches(9, 0))));

        // The left end is free; the right one is what stops this, so it is what the report names.
        Assert.Contains(anchorOnTheRight, result.Conflict.Relationships);
        Assert.Contains(right, result.Conflict.Entities);
    }

    [Fact]
    public void Finding8_BothResizeHandlesBehaveTheSameWayOnAnAnchoredBox()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(10, 10, 30, 4);
        builder.Anchor(box);

        // Anchored holds an entity still; it does not stop the user resizing it with a handle.
        // Before the revert the east handle worked and the west one silently refused.
        Solved east = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragFace(box, BoxFace.East, Length.Inches(5))));
        Solved west = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragFace(box, BoxFace.West, Length.Inches(5))));
        Solved north = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragFace(box, BoxFace.North, Length.Inches(2))));
        Solved south = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragFace(box, BoxFace.South, Length.Inches(2))));

        SketchAssert.BoxIs(east.Sketch, box, 10, 10, 35, 4);
        SketchAssert.BoxIs(west.Sketch, box, 5, 10, 35, 4);
        SketchAssert.BoxIs(north.Sketch, box, 10, 10, 30, 6);
        SketchAssert.BoxIs(south.Sketch, box, 10, 8, 30, 6);
    }

    [Fact]
    public void Finding9_ARequestAgainstSomethingWithNoCoordinatesNamesTheRightReason()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 4);
        EntityId dimension = builder.AddDimension(new ParamMeasurand(new BoxWidthRef(box)));

        // A dimension's placement is canvas data; it has no anchor to set and nothing to drag.
        Assert.Equal(
            new Rejected(RejectionReason.DanglingReference),
            Updater.Apply(builder.Sketch, SetPosition.InPlan(dimension, Point2.Inches(1, 1))));
        Assert.Equal(
            new Rejected(RejectionReason.DanglingReference),
            Updater.Apply(builder.Sketch, Drag.InPlan(dimension, new Vector2(Length.Inches(1), Length.Zero))));
    }

    [Theory]
    [InlineData(".75", 768)]
    [InlineData(".5", 512)]
    [InlineData("-.25", -256)]
    [InlineData("6'-.5\"", 73728 + 512)]
    public void Finding11_ANumberMayStartAtThePoint(string text, long expectedUnits)
    {
        Assert.True(Length.TryParse(text, out Length value, out _), text);
        Assert.Equal(expectedUnits, value.Units);
    }

    [Theory]
    [InlineData("5.")]
    [InlineData("5.\"")]
    [InlineData(".")]
    [InlineData("-.")]
    public void Finding11_AHalfTypedNumberIsStillMalformed(string text)
    {
        Assert.False(Length.TryParse(text, out _, out _), text);
    }

    [Fact]
    public void Finding11_TryDivideExactRefusesTheDivisionThatHasNoAnswer()
    {
        // long.MinValue / -1 is not representable, so it is not an exact division either.
        Assert.False(new Length(long.MinValue).TryDivideExact(-1, out Length result));
        Assert.Equal(Length.Zero, result);

        // The ordinary case still works.
        Assert.True(new Length(-1024).TryDivideExact(-1, out Length negated));
        Assert.Equal(new Length(1024), negated);
    }

    [Fact]
    public void DecisionA_AnchoredRefusesAResizeThatComesFromAnotherEntity()
    {
        // §3.2: an anchored entity "does not move or resize in response to other entities".
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 20, 4);
        EntityId b = builder.AddBox(0, 10, 20, 4);
        RelationshipId widthOfA = builder.WidthIs(a, Length.Inches(20));
        RelationshipId equal = builder.EqualWidths(a, b);
        RelationshipId anchorB = builder.Anchor(b);

        OverConstrained result = Assert.IsType<OverConstrained>(
            Updater.Apply(builder.Sketch, new SetParameter(widthOfA, Length.Inches(30))));

        Assert.Contains(anchorB, result.Conflict.Relationships);
        Assert.Contains(equal, result.Conflict.Relationships);
        Assert.Contains(widthOfA, result.Conflict.Relationships);
    }

    [Fact]
    public void DecisionA_AnchoredStandsAsideForTheSizeTheUserIsEditing()
    {
        // ... but editing the anchored part's own dimension is not a response to another entity.
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(10, 10, 20, 4);
        RelationshipId width = builder.WidthIs(box, Length.Inches(20));
        builder.Anchor(box);

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(width, Length.Inches(30))));

        SketchAssert.BoxIs(result.Sketch, box, 10, 10, 30, 4);
        SketchAssert.IsConsistent(result.Sketch);

        // Its position is still held.
        Assert.IsType<OverConstrained>(Updater.Apply(builder.Sketch, SetPosition.InPlan(box, Point2.Inches(0, 0))));
    }

    [Fact]
    public void DecisionB_ChangesThatAreNeitherMoveNorResizeAreReportedAsModified()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4);
        RelationshipId width = builder.WidthIs(box, Length.Inches(30));
        EntityId dimension = builder.AddDimension(new ParamMeasurand(new BoxWidthRef(box)), width);
        Layer shelves = new(LayerId.New(), "Shelves");
        Sketch sketch = builder.Sketch.WithLayer(shelves);

        Solved demoted = Assert.IsType<Solved>(Updater.Apply(sketch, new RemoveRelationship(width)));
        Assert.Equal(new[] { dimension }, demoted.Changes.Modified);
        Assert.Null(demoted.Sketch.Find<Dimension>(dimension)!.Drives);

        Solved moved = Assert.IsType<Solved>(Updater.Apply(sketch, new SetLayer(box, shelves.Id)));
        Assert.Equal(new[] { box }, moved.Changes.Modified);
        Assert.False(moved.Changes.IsEmpty);
    }
}

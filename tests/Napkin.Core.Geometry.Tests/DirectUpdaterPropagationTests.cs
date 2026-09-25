namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The propagation golden cases 1 to 6, 13 and 14 of docs/design/geometry-model.md &#xA7;7.1.
/// </summary>
public class DirectUpdaterPropagationTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    [Trait("Feature", "GEO-010")]
    [Fact]
    public void Case1_ResizingALoneBoxKeepsItsAnchorAndMovesItsFarCorner()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4);
        RelationshipId width = builder.WidthIs(box, Length.Inches(30));

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(width, Length.Inches(40))));

        SketchAssert.BoxIs(result.Sketch, box, 0, 0, 40, 4);
        Assert.Equal(Point2.Inches(40, 0), result.Sketch.Find<Box>(box)!.Corner(BoxCorner.SouthEast));
        Assert.Contains(box, result.Changes.Resized);
        Assert.DoesNotContain(box, result.Changes.Moved);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Trait("Feature", "GEO-010")]
    [Fact]
    public void Case2_ABoxFlushAgainstAResizedOneTranslatesAndKeepsItsSize()
    {
        (SketchBuilder builder, EntityId a, EntityId b, RelationshipId width, _) = FlushPair();

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(width, Length.Inches(40))));

        SketchAssert.BoxIs(result.Sketch, a, 0, 0, 40, 4);
        SketchAssert.BoxIs(result.Sketch, b, 40, 0, 10, 4);
        Assert.Contains(b, result.Changes.Moved);
        Assert.DoesNotContain(b, result.Changes.Resized);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Trait("Feature", "GEO-011")]
    [Fact]
    public void Case3_WhenTheFarSideIsPinnedTheAnchorMovesInstead()
    {
        (SketchBuilder builder, EntityId a, EntityId b, RelationshipId width, _) = FlushPair();
        builder.Anchor(b);

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(width, Length.Inches(40))));

        // A's anchor moves west and its east edge stays where B's west edge is.
        SketchAssert.BoxIs(result.Sketch, a, -10, 0, 40, 4);
        SketchAssert.BoxIs(result.Sketch, b, 30, 0, 10, 4);
        Assert.Equal(Point2.Inches(30, 0), result.Sketch.Find<Box>(a)!.Corner(BoxCorner.SouthEast));
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Trait("Feature", "GEO-011")]
    [Fact]
    public void Case4_WhenBothSidesArePinnedTheResizeIsAContradictionThatNamesThePins()
    {
        (SketchBuilder builder, EntityId a, EntityId b, RelationshipId width, RelationshipId flush) = FlushPair();
        RelationshipId anchorB = builder.Anchor(b);
        RelationshipId anchorA = builder.Anchor(a);
        Sketch before = builder.Sketch;

        OverConstrained result = Assert.IsType<OverConstrained>(
            Updater.Apply(before, new SetParameter(width, Length.Inches(40))));

        Assert.Equal(ConflictKind.Contradictory, result.Conflict.Kind);
        Assert.Contains(anchorA, result.Conflict.Relationships);
        Assert.Contains(anchorB, result.Conflict.Relationships);
        Assert.Contains(flush, result.Conflict.Relationships);
        Assert.Contains(a, result.Conflict.Entities);
        Assert.Contains(b, result.Conflict.Entities);
        Assert.Equal(2, result.Conflict.Derivations.Count);
        Assert.NotEmpty(result.Conflict.Summary);

        // The sketch is untouched by construction: the working table was never written back.
        Assert.Equal(before, builder.Sketch);
    }

    [Fact]
    public void Case5_EqualParamFollowsAResizeAndThenRefusesASecondOwnerForTheNumber()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 20, 4);
        EntityId b = builder.AddBox(0, 10, 20, 4);
        RelationshipId widthOfA = builder.WidthIs(a, Length.Inches(20));
        RelationshipId equal = builder.EqualWidths(a, b);

        Solved resized = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(widthOfA, Length.Inches(30))));

        SketchAssert.BoxIs(resized.Sketch, a, 0, 0, 30, 4);
        SketchAssert.BoxIs(resized.Sketch, b, 0, 10, 30, 4);
        SketchAssert.IsConsistent(resized.Sketch);

        // A second owner for B's width, disagreeing, is a conflict naming both ParamValues and
        // the EqualParam that ties them together.
        RelationshipId widthOfB = SketchBuilder.RelationshipIdAt(99);
        OverConstrained conflict = Assert.IsType<OverConstrained>(Updater.Apply(
            resized.Sketch,
            new AddRelationship(new ParamValue(widthOfB, new BoxWidthRef(b), Length.Inches(28)))));

        Assert.Contains(widthOfA, conflict.Conflict.Relationships);
        Assert.Contains(widthOfB, conflict.Conflict.Relationships);
        Assert.Contains(equal, conflict.Conflict.Relationships);
    }

    [Fact]
    public void Case6_CenteredRoundsAnOddSpanHalfToEvenAndHoldsExactlyOnAnEvenOne()
    {
        foreach ((long span, long expected) in new[] { (3L, 2L), (4L, 2L), (5L, 2L), (1L, 0L) })
        {
            SketchBuilder builder = new();
            EntityId left = builder.AddNode(0, 0);
            EntityId right = builder.AddNode(Point2.Origin with { X = new Length(span) });
            EntityId middle = builder.AddNode(0, 0);

            RelationshipId centred = SketchBuilder.RelationshipIdAt(99);
            Solved result = Assert.IsType<Solved>(Updater.Apply(
                builder.Sketch,
                new AddRelationship(new Centered(
                    centred,
                    new NodeRef(middle),
                    new NodeRef(left),
                    new NodeRef(right),
                    Axis.X))));

            Assert.Equal(expected, result.Sketch.Find<Node>(middle)!.Position.X.Units);

            // The checker measures against the same rounded midpoint, so it holds, and on an even
            // span it holds with nothing rounded at all.
            SketchAssert.IsConsistent(result.Sketch, $"span of {span} units");
        }
    }

    [Fact]
    public void Case13_AWallWithAnOpeningResizesSlidesAndStaysInTheWall()
    {
        (SketchBuilder builder, EntityId wall, EntityId opening, RelationshipId along, RelationshipId openingWidth)
            = WallWithOpening();

        // Resize the opening: its anchor stays, and the wall does not move.
        Solved resized = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(openingWidth, Length.Inches(40))));
        SketchAssert.BoxIs(resized.Sketch, opening, 36, 0, 40, 6);
        SketchAssert.BoxIs(resized.Sketch, wall, 0, 0, 120, 6);
        SketchAssert.IsConsistent(resized.Sketch);

        // Change the distance along the wall: the opening slides, the wall does not.
        Solved slid = Assert.IsType<Solved>(
            Updater.Apply(resized.Sketch, new SetParameter(along, Length.Inches(50))));
        SketchAssert.BoxIs(slid.Sketch, opening, 50, 0, 40, 6);
        SketchAssert.BoxIs(slid.Sketch, wall, 0, 0, 120, 6);
        SketchAssert.IsConsistent(slid.Sketch);
    }

    [Trait("Feature", "GEO-014")]
    [Fact]
    public void Case14_ABatchWhoseSecondRequestConflictsAppliesNothing()
    {
        (SketchBuilder builder, EntityId a, EntityId b, RelationshipId width, _) = FlushPair();
        builder.Anchor(b);
        builder.Anchor(a);
        Sketch before = builder.Sketch;

        UpdateResult result = Updater.Apply(before, Batch.Of(
            new SetLayer(a, LayerId.Default),
            new SetParameter(width, Length.Inches(40))));

        OverConstrained conflict = Assert.IsType<OverConstrained>(result);
        Assert.Equal(ConflictKind.Contradictory, conflict.Conflict.Kind);
        Assert.Equal(before, builder.Sketch);
    }

    [Fact]
    public void ABatchThatSucceedsAppliesEveryRequestAsOneChange()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 20, 4);
        RelationshipId width = builder.WidthIs(a, Length.Inches(20));

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, Batch.Of(
            new SetParameter(width, Length.Inches(30)),
            SetPosition.InPlan(a, Point2.Inches(5, 5)))));

        SketchAssert.BoxIs(result.Sketch, a, 5, 5, 30, 4);
        Assert.Contains(a, result.Changes.Moved);
        Assert.Contains(a, result.Changes.Resized);
    }

    [Fact]
    public void SetPositionOnAnAnchoredBoxIsAConflictNamingTheAnchor()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 20, 4);
        RelationshipId anchor = builder.Anchor(box);

        OverConstrained result = Assert.IsType<OverConstrained>(
            Updater.Apply(builder.Sketch, SetPosition.InPlan(box, Point2.Inches(5, 0))));

        Assert.Contains(anchor, result.Conflict.Relationships);
    }

    [Fact]
    public void AddingACoincidentMovesTheSecondPointOntoTheFirst()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 20, 4);
        EntityId node = builder.AddNode(5, 5);

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new AddRelationship(
            new Coincident(
                SketchBuilder.RelationshipIdAt(99),
                TestRefs.Corner(box, BoxCorner.NorthEast),
                new NodeRef(node)))));

        // The box's north-east corner is at (20, 4); the node follows it.
        Assert.Equal(Point2.Inches(20, 4), result.Sketch.Find<Node>(node)!.Position);
        SketchAssert.BoxIs(result.Sketch, box, 0, 0, 20, 4);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void AddingACoincidentToAnAnchoredPointMovesTheOtherSideInstead()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 20, 4);
        EntityId node = builder.AddNode(5, 5);
        builder.Anchor(node);

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new AddRelationship(
            new Coincident(
                SketchBuilder.RelationshipIdAt(99),
                TestRefs.Corner(box, BoxCorner.NorthEast),
                new NodeRef(node)))));

        // The node stays; the box moves so that its north-east corner lands on it.
        Assert.Equal(Point2.Inches(5, 5), result.Sketch.Find<Node>(node)!.Position);
        SketchAssert.BoxIs(result.Sketch, box, -15, 1, 20, 4);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Trait("Feature", "GEO-013")]
    [Fact]
    public void SetOrientationIsRefusedWhenTheBoxHasRelationshipsRotatingWouldReinterpret()
    {
        (SketchBuilder builder, EntityId a, _, _, _) = FlushPair();

        Rejected refused = Assert.IsType<Rejected>(
            Updater.Apply(builder.Sketch, new SetOrientation(a, BoxFace.Top, Angle.Right)));
        Assert.Equal(RejectionReason.OrientationWithRelationships, refused.Reason);
        Assert.Equal(ValidationErrorKind.TurnWouldReinterpret, refused.Detail!.Kind);
        Assert.Contains("Flush", refused.Detail.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetOrientationTurnsABoxWithOnlySizeAndAnchorRelationshipsAboutItsAnchor()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(10, 20, 30, 8);
        builder.Anchor(box);
        builder.WidthIs(box, Length.Inches(30));

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetOrientation(box, BoxFace.Top, Angle.Right)));

        Box rotated = result.Sketch.Find<Box>(box)!;
        Assert.Equal(Angle.Right, rotated.Rotation);
        Assert.Equal(Point3.Inches(10, 20, 0), rotated.Anchor);
        Assert.Equal(Point2.Inches(10, 50), rotated.Corner(BoxCorner.SouthEast));

        // A rotation leaves the anchor where it is, so it is neither a move nor a resize.
        Assert.Contains(box, result.Changes.Modified);
        Assert.Empty(result.Changes.Moved);

        Assert.Equal(
            new Rejected(RejectionReason.RotationNotSupported),
            Updater.Apply(builder.Sketch, new SetOrientation(box, BoxFace.Top, Angle.Degrees(45))));
    }

    [Fact]
    public void ASketchOutsideTheRectilinearDomainIsRefusedRatherThanGuessedAt()
    {
        SketchBuilder builder = new();
        EntityId tilted = builder.AddBox(Point2.Origin, Length.Inches(20), Length.Inches(4), Angle.Degrees(45));
        RelationshipId width = builder.WidthIs(tilted, Length.Inches(20));

        Assert.Equal(
            new Rejected(RejectionReason.RotationNotSupported),
            Updater.Apply(builder.Sketch, new SetParameter(width, Length.Inches(30))));

        // Structural requests still work, so the sketch is not a dead end.
        Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new RemoveRelationship(width)));
    }

    private static (SketchBuilder Builder, EntityId A, EntityId B, RelationshipId Width, RelationshipId Flush) FlushPair()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(30, 0, 10, 4);
        RelationshipId flush = builder.Flush(a, BoxEdge.East, b, BoxEdge.West);
        RelationshipId width = builder.WidthIs(a, Length.Inches(30));
        return (builder, a, b, width, flush);
    }

    private static (SketchBuilder Builder, EntityId Wall, EntityId Opening, RelationshipId Along, RelationshipId Width)
        WallWithOpening()
    {
        // A wall in plan view is a box whose width is its length and whose height is its
        // thickness; an opening is a box flush with both its long edges and set a distance along
        // it from a wall corner (design §2.3).
        SketchBuilder builder = new();
        EntityId wall = builder.AddBox(0, 0, 120, 6);
        EntityId opening = builder.AddBox(36, 0, 36, 6);

        builder.Anchor(wall);
        builder.Flush(wall, BoxEdge.South, opening, BoxEdge.South);
        builder.Flush(wall, BoxEdge.North, opening, BoxEdge.North);
        RelationshipId along = builder.Add(id => new AxisDistance(
            id,
            TestRefs.Corner(wall, BoxCorner.SouthWest),
            TestRefs.Corner(opening, BoxCorner.SouthWest),
            Axis.X,
            Length.Inches(36)));
        RelationshipId width = builder.WidthIs(opening, Length.Inches(36));

        return (builder, wall, opening, along, width);
    }
}

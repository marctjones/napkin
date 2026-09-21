namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The exact/tolerance class decision and the per-kind residuals of design &#xA7;3.4 and &#xA7;5.3:
/// docs/design/geometry-model.md &#xA7;8 step 5.
/// </summary>
public class RelationshipCheckerTests
{
    [Fact]
    public void ThePositionalToleranceIsDerivedFromTheGrid()
    {
        // §5.3: 4 units = 1/256". Recomputed here so that a change to the grid cannot leave the
        // tolerance stale.
        Assert.Equal(Length.UnitsPerInch / 256, Tolerances.Default.Position.Units);
        Assert.Equal(4L, Tolerances.Default.Position.Units);
        Assert.Equal(2L, Tolerances.Default.Angle.Arcseconds);

        // Far below display precision: a quarter of the finest tape mark.
        Assert.True(Tolerances.Default.Position < Length.Inches(0, 1, 64));
    }

    [Fact]
    public void AnchoredIsNeverAViolationBecauseItConstrainsUpdatesNotState()
    {
        SketchBuilder builder = new();
        builder.Anchor(builder.AddBox(0, 0, 10, 10));

        Assert.True(RelationshipChecker.Check(builder.Sketch).AllHold);
    }

    [Fact]
    public void CoincidentHoldsExactlyOrNotAtAll()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 10);
        EntityId node = builder.AddNode(10, 10);
        builder.Add(id => new Coincident(id, new CornerRef(box, BoxCorner.NorthEast), new NodeRef(node)));

        Assert.True(RelationshipChecker.Check(builder.Sketch).AllHold);

        // One unit out is one unit too far for an exact-class relationship.
        Sketch moved = builder.Sketch.WithEntity(
            builder.NodeOf(node) with { Position = new Point2(Length.Inches(10) + new Length(1), Length.Inches(10)) });

        Violation violation = Assert.Single(RelationshipChecker.Check(moved).Violations);
        Assert.True(violation.Exact);
        Assert.Equal(1, violation.Residual.Units);
    }

    [Fact]
    public void CoincidentOnARotatedCornerIsToleranceClass()
    {
        // §5.3: Coincident is tolerance class when either corner is on a non-right-angle rotation.
        SketchBuilder builder = new();
        EntityId tilted = builder.AddBox(Point2.Origin, Length.Inches(10), Length.Inches(10), Angle.Degrees(37));
        EntityId node = builder.AddNode(0, 0);
        Coincident coincident = new(
            new RelationshipId(Guid.NewGuid()),
            new CornerRef(tilted, BoxCorner.NorthEast),
            new NodeRef(node));

        Assert.False(RelationshipChecker.IsExactClass(builder.Sketch, coincident));

        // And the same relationship on a right-angle box is exact class.
        EntityId square = builder.AddBox(0, 0, 10, 10);
        Assert.True(RelationshipChecker.IsExactClass(
            builder.Sketch,
            coincident with { A = new CornerRef(square, BoxCorner.SouthWest) }));
    }

    [Fact]
    public void ToleranceClassViolationsAreJudgedAgainstTheTolerance()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddNode(0, 0);
        EntityId b = builder.AddNode(3, 4);
        builder.Add(id => new Distance(id, new NodeRef(a), new NodeRef(b), Length.Inches(5)));

        // Exactly right.
        Assert.True(RelationshipChecker.Check(builder.Sketch).AllHold);

        // Four units along X moves the distance by two units: inside 1/256", so it still holds.
        Sketch nudged = builder.Sketch.WithEntity(
            builder.NodeOf(b) with { Position = new Point2(Length.Inches(3) + new Length(4), Length.Inches(4)) });
        Assert.True(RelationshipChecker.Check(nudged).AllHold);

        // A sixteenth of an inch out is well past it.
        Sketch shoved = builder.Sketch.WithEntity(
            builder.NodeOf(b) with { Position = new Point2(Length.Inches(3) + Length.Inches(0, 1, 16), Length.Inches(4)) });
        Violation violation = Assert.Single(RelationshipChecker.Check(shoved).Violations);
        Assert.False(violation.Exact);
    }

    [Fact]
    public void FlushComparesTheLinesOfTwoParallelAxisAlignedEdges()
    {
        SketchBuilder builder = new();
        EntityId carcass = builder.AddBox(0, 0, 30, 24);
        EntityId shelf = builder.AddBox(0, 10, 30, 1);
        builder.Flush(carcass, BoxEdge.West, shelf, BoxEdge.West);

        Assert.True(RelationshipChecker.Check(builder.Sketch).AllHold);

        Sketch nudged = builder.Sketch.WithEntity(builder.BoxOf(shelf) with { Anchor = Point2.Inches(1, 10) });
        Violation violation = Assert.Single(RelationshipChecker.Check(nudged).Violations);
        Assert.Equal(Length.Inches(1), violation.Residual);
        Assert.True(violation.Exact);
    }

    [Fact]
    public void HorizontalAndVerticalMeasureHowFarFromAxisAlignedAnEdgeIs()
    {
        SketchBuilder builder = new();
        EntityId start = builder.AddNode(0, 0);
        EntityId end = builder.AddNode(10, 0);
        EntityId segment = builder.AddSegment(start, end);
        builder.Add(id => new Horizontal(id, new SegmentRef(segment)));

        Assert.True(RelationshipChecker.Check(builder.Sketch).AllHold);

        Sketch tilted = builder.Sketch.WithEntity(builder.NodeOf(end) with { Position = Point2.Inches(10, 2) });
        Violation violation = Assert.Single(RelationshipChecker.Check(tilted).Violations);
        Assert.Equal(Length.Inches(2), violation.Residual);
    }

    [Fact]
    public void ParamValueOnAnAxisAlignedSegmentIsExactAndOnADiagonalIsNot()
    {
        SketchBuilder builder = new();
        EntityId start = builder.AddNode(0, 0);
        EntityId alongX = builder.AddNode(10, 0);
        EntityId diagonal = builder.AddNode(3, 4);
        EntityId straight = builder.AddSegment(start, alongX);
        EntityId slanted = builder.AddSegment(start, diagonal);

        ParamValue exact = new(new RelationshipId(Guid.NewGuid()), new SegmentLengthRef(straight), Length.Inches(10));
        ParamValue inexact = new(new RelationshipId(Guid.NewGuid()), new SegmentLengthRef(slanted), Length.Inches(5));

        Assert.True(RelationshipChecker.IsExactClass(builder.Sketch, exact));
        Assert.False(RelationshipChecker.IsExactClass(builder.Sketch, inexact));
    }

    [Theory]
    // The true midpoint of 0 and 3 units is 1.5u, so both 1u and 2u centre the span to within the
    // half unit design §3.2 allows; 0u and 3u are a whole unit out and do not.
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void CenteredOnAnOddSpanHoldsOnEitherSideOfTheTie(long middle, bool holds)
    {
        SketchBuilder builder = new();
        EntityId left = builder.AddNode(0, 0);
        EntityId right = builder.AddNode(Point2.Origin with { X = new Length(3) });
        EntityId centre = builder.AddNode(Point2.Origin with { X = new Length(middle) });
        builder.Add(id => new Centered(id, new NodeRef(centre), new NodeRef(left), new NodeRef(right), Axis.X));

        Assert.Equal(holds, RelationshipChecker.Check(builder.Sketch).AllHold);
    }

    [Fact]
    public void CenteredSurvivesBeingTranslatedByAnOddNumberOfUnits()
    {
        // Measuring against the half-to-even midpoint rather than the nearer of the two would make
        // this a violation, because an odd shift lands on the other side of the tie.
        for (long shift = 0; shift < 6; shift++)
        {
            SketchBuilder builder = new();
            EntityId left = builder.AddNode(Point2.Origin with { X = new Length(shift) });
            EntityId right = builder.AddNode(Point2.Origin with { X = new Length(3 + shift) });
            EntityId centre = builder.AddNode(Point2.Origin with { X = new Length(2 + shift) });
            builder.Add(id => new Centered(id, new NodeRef(centre), new NodeRef(left), new NodeRef(right), Axis.X));

            Assert.True(RelationshipChecker.Check(builder.Sketch).AllHold, $"shifted by {shift} units");
        }
    }

    [Fact]
    public void CenteredIsExactWhenTheSpanIsEven()
    {
        SketchBuilder builder = new();
        EntityId left = builder.AddNode(0, 0);
        EntityId right = builder.AddNode(10, 0);
        EntityId middle = builder.AddNode(5, 0);
        builder.Add(id => new Centered(id, new NodeRef(middle), new NodeRef(left), new NodeRef(right), Axis.X));

        Assert.True(RelationshipChecker.Check(builder.Sketch).AllHold);
    }

    [Fact]
    public void PerpendicularBetweenTwoBoxEdgesIsExactClass()
    {
        // §5.3: Parallel/Perpendicular/AngleBetween are exact class when both edges are box edges,
        // because their directions are stored Angles the repair pass can copy.
        SketchBuilder builder = new();
        EntityId upright = builder.AddBox(0, 0, 10, 4);
        EntityId turned = builder.AddBox(20, 0, 10, 4, quarterTurns: 1);

        Perpendicular perpendicular = new(
            new RelationshipId(Guid.NewGuid()),
            new BoxEdgeRef(upright, BoxEdge.South),
            new BoxEdgeRef(turned, BoxEdge.South));

        Assert.True(RelationshipChecker.IsExactClass(builder.Sketch, perpendicular));
        Assert.True(RelationshipChecker.Check(builder.Sketch.WithRelationship(perpendicular)).AllHold);

        // Parallel between the same two edges does not hold at all.
        Parallel parallel = new(
            new RelationshipId(Guid.NewGuid()),
            new BoxEdgeRef(upright, BoxEdge.South),
            new BoxEdgeRef(turned, BoxEdge.South));
        Assert.Single(RelationshipChecker.Check(builder.Sketch.WithRelationship(parallel)).Violations);
    }

    [Fact]
    public void ParallelInvolvingASegmentIsToleranceClass()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 4);
        EntityId start = builder.AddNode(0, 20);
        EntityId end = builder.AddNode(10, 20);
        EntityId segment = builder.AddSegment(start, end);

        Parallel parallel = new(
            new RelationshipId(Guid.NewGuid()),
            new BoxEdgeRef(box, BoxEdge.South),
            new SegmentRef(segment));

        Assert.False(RelationshipChecker.IsExactClass(builder.Sketch, parallel));
        Assert.True(RelationshipChecker.Check(builder.Sketch.WithRelationship(parallel)).AllHold);
    }

    [Fact]
    public void TangentAndRadiusCannotHoldUntilArcsExist()
    {
        // They exist as records so that the file format and the UI have names for them, but there
        // is nothing to evaluate them against in #5 (§3.2, §10).
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 4);
        builder.Add(id => new Radius(id, box, Length.Inches(5)));

        Violation violation = Assert.Single(RelationshipChecker.Check(builder.Sketch).Violations);
        Assert.False(violation.Exact);
    }

    [Fact]
    public void PointOnEdgeAndSymmetricMeasureDistanceFromTheEdgeLine()
    {
        SketchBuilder builder = new();
        EntityId start = builder.AddNode(0, 0);
        EntityId end = builder.AddNode(10, 0);
        EntityId segment = builder.AddSegment(start, end);
        EntityId on = builder.AddNode(4, 0);
        EntityId above = builder.AddNode(4, 3);
        EntityId below = builder.AddNode(4, -3);

        Assert.True(RelationshipChecker.Check(builder.Sketch.WithRelationship(
            new PointOnEdge(new RelationshipId(Guid.NewGuid()), new NodeRef(on), new SegmentRef(segment)))).AllHold);

        Assert.Single(RelationshipChecker.Check(builder.Sketch.WithRelationship(
            new PointOnEdge(new RelationshipId(Guid.NewGuid()), new NodeRef(above), new SegmentRef(segment)))).Violations);

        Assert.True(RelationshipChecker.Check(builder.Sketch.WithRelationship(
            new Symmetric(
                new RelationshipId(Guid.NewGuid()),
                new NodeRef(above),
                new NodeRef(below),
                new SegmentRef(segment)))).AllHold);
    }
}

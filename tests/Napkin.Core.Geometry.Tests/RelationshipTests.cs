namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Structural equality of relationships, which is what invariant 4 (&#xA7;2.5) is stated in terms
/// of: docs/design/geometry-model.md &#xA7;8 step 4.
/// </summary>
public class RelationshipTests
{
    private static readonly EntityId BoxA = EntityId.New();
    private static readonly EntityId BoxB = EntityId.New();

    [Fact]
    public void TwoRelationshipsSayingTheSameThingAreStructurallyIdentical()
    {
        Flush first = new(RelationshipId.New(), new BoxEdgeRef(BoxA, BoxEdge.East), new BoxEdgeRef(BoxB, BoxEdge.West));
        Flush second = new(RelationshipId.New(), new BoxEdgeRef(BoxA, BoxEdge.East), new BoxEdgeRef(BoxB, BoxEdge.West));

        Assert.NotEqual(first, second);                                     // different ids
        Assert.True(Relationship.AreStructurallyIdentical(first, second));  // same statement
    }

    [Fact]
    public void DifferentReferencesAreNotStructurallyIdentical()
    {
        Flush east = new(RelationshipId.New(), new BoxEdgeRef(BoxA, BoxEdge.East), new BoxEdgeRef(BoxB, BoxEdge.West));
        Flush north = new(RelationshipId.New(), new BoxEdgeRef(BoxA, BoxEdge.North), new BoxEdgeRef(BoxB, BoxEdge.West));
        Flush swapped = new(RelationshipId.New(), new BoxEdgeRef(BoxB, BoxEdge.West), new BoxEdgeRef(BoxA, BoxEdge.East));

        Assert.False(Relationship.AreStructurallyIdentical(east, north));

        // Symmetric kinds written the other way round are not detected as duplicates (§10).
        Assert.False(Relationship.AreStructurallyIdentical(east, swapped));
    }

    [Fact]
    public void DifferentKindsAreNotStructurallyIdentical()
    {
        Parallel parallel = new(RelationshipId.New(), new BoxEdgeRef(BoxA, BoxEdge.East), new BoxEdgeRef(BoxB, BoxEdge.West));
        Flush flush = new(RelationshipId.New(), new BoxEdgeRef(BoxA, BoxEdge.East), new BoxEdgeRef(BoxB, BoxEdge.West));

        Assert.False(Relationship.AreStructurallyIdentical(parallel, flush));
    }

    [Fact]
    public void TheStatedValueCounts()
    {
        ParamValue thirty = new(RelationshipId.New(), new BoxWidthRef(BoxA), Length.Inches(30));
        ParamValue alsoThirty = new(RelationshipId.New(), new BoxWidthRef(BoxA), Length.Inches(30));
        ParamValue twentyEight = new(RelationshipId.New(), new BoxWidthRef(BoxA), Length.Inches(28));

        // Two owners for the same number is a duplicate; two owners disagreeing is a conflict the
        // updater should report, not a duplicate it should refuse.
        Assert.True(Relationship.AreStructurallyIdentical(thirty, alsoThirty));
        Assert.False(Relationship.AreStructurallyIdentical(thirty, twentyEight));
    }

    [Fact]
    public void EveryKindReportsTheEntitiesItReferences()
    {
        EntityId c = EntityId.New();
        RelationshipId id = RelationshipId.New();

        AssertReferences(new Anchored(id, BoxA), BoxA);
        AssertReferences(new Coincident(id, new CornerRef(BoxA, BoxCorner.NorthEast), new NodeRef(BoxB)), BoxA, BoxB);
        AssertReferences(new Horizontal(id, new SegmentRef(BoxA)), BoxA);
        AssertReferences(new Vertical(id, new SegmentRef(BoxA)), BoxA);
        AssertReferences(new Flush(id, new BoxEdgeRef(BoxA, BoxEdge.East), new BoxEdgeRef(BoxB, BoxEdge.West)), BoxA, BoxB);
        AssertReferences(
            new AxisDistance(id, new CenterRef(BoxA), new CenterRef(BoxB), Axis.X, Length.Inches(4)),
            BoxA, BoxB);
        AssertReferences(new ParamValue(id, new BoxWidthRef(BoxA), Length.Inches(4)), BoxA);
        AssertReferences(new EqualParam(id, new BoxWidthRef(BoxA), new BoxHeightRef(BoxB)), BoxA, BoxB);
        AssertReferences(
            new Centered(id, new CenterRef(c), new CornerRef(BoxA, BoxCorner.SouthWest), new CornerRef(BoxB, BoxCorner.SouthEast), Axis.X),
            c, BoxA, BoxB);

        // The kinds reserved for the solver (#28) exist from #5 so that the file format and the
        // UI have names for them.
        AssertReferences(new Parallel(id, new SegmentRef(BoxA), new SegmentRef(BoxB)), BoxA, BoxB);
        AssertReferences(new Perpendicular(id, new SegmentRef(BoxA), new SegmentRef(BoxB)), BoxA, BoxB);
        AssertReferences(new AngleBetween(id, new SegmentRef(BoxA), new SegmentRef(BoxB), Angle.Degrees(45)), BoxA, BoxB);
        AssertReferences(new Distance(id, new NodeRef(BoxA), new NodeRef(BoxB), Length.Inches(4)), BoxA, BoxB);
        AssertReferences(new PointOnEdge(id, new NodeRef(BoxA), new SegmentRef(BoxB)), BoxA, BoxB);
        AssertReferences(new Symmetric(id, new NodeRef(BoxA), new NodeRef(BoxB), new SegmentRef(c)), BoxA, BoxB, c);
        AssertReferences(new Tangent(id, new SegmentRef(BoxA), new SegmentRef(BoxB)), BoxA, BoxB);
        AssertReferences(new Radius(id, BoxA, Length.Inches(4)), BoxA);
    }

    private static void AssertReferences(Relationship relationship, params EntityId[] expected)
        => Assert.Equal(expected, relationship.References.ToArray());
}

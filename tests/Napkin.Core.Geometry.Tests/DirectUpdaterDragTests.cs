namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The best-effort golden cases 7 and 8 of docs/design/geometry-model.md &#xA7;7.1, the drag
/// clause of case 13, and &#xA7;8 step 9's <see cref="DragEdge"/> cases.
/// </summary>
public class DirectUpdaterDragTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    [Trait("Feature", "GEO-012")]
    [Fact]
    public void Case7_DraggingABoxFlushAgainstAnAnchoredOneSlidesAlongTheFreeAxis()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(30, 0, 10, 4);
        builder.Flush(a, BoxEdge.East, b, BoxEdge.West);
        builder.Anchor(b);

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            Drag.InPlan(a, new Vector2(Length.Inches(5), Length.Inches(7)))));

        // The flush is on vertical edges, so it blocks X and not Y.
        Assert.Equal(new Vector3(Length.Zero, Length.Inches(7), Length.Zero), result.Changes.AppliedDelta);
        SketchAssert.BoxIs(result.Sketch, a, 0, 7, 30, 4);
        SketchAssert.BoxIs(result.Sketch, b, 30, 0, 10, 4);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Trait("Feature", "GEO-012")]
    [Fact]
    public void Case8_ADragThatGoesNowhereIsStillSolved()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 10);
        EntityId node = builder.AddNode(0, 0);
        builder.Add(id => new Coincident(id, new CornerRef(box, BoxCorner.SouthWest), new NodeRef(node)));
        builder.Anchor(node);

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            Drag.InPlan(box, new Vector2(Length.Inches(5), Length.Inches(5)))));

        // A drag is a question, not a demand: the answer "no" is not a conflict.
        Assert.Equal(Vector3.Zero, result.Changes.AppliedDelta);
        Assert.Empty(result.Changes.Moved);
        SketchAssert.BoxIs(result.Sketch, box, 0, 0, 10, 10);
    }

    [Fact]
    public void Case13_AnOpeningPinnedAlongItsWallDoesNotSlideUntilThatNumberIsRemoved()
    {
        SketchBuilder builder = new();
        EntityId wall = builder.AddBox(0, 0, 120, 6);
        EntityId opening = builder.AddBox(36, 0, 36, 6);
        builder.Anchor(wall);
        builder.Flush(wall, BoxEdge.South, opening, BoxEdge.South);
        builder.Flush(wall, BoxEdge.North, opening, BoxEdge.North);
        RelationshipId along = builder.Add(id => new AxisDistance(
            id,
            new CornerRef(wall, BoxCorner.SouthWest),
            new CornerRef(opening, BoxCorner.SouthWest),
            Axis.X,
            Length.Inches(36)));

        Vector2 wanted = new(Length.Inches(10), Length.Inches(3));

        // With the AxisDistance in place the opening's X is a number the user typed, and a drag
        // never overrides one: the flushes block Y, the AxisDistance blocks X. Design §7.1 case 13
        // expects the Y component zeroed and X free, which contradicts §4.4's own rule that an
        // AxisDistance along X blocks X; §4.4 wins. See docs/design/geometry-model.md §10.
        Solved pinned = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, Drag.InPlan(opening, wanted)));
        Assert.Equal(Vector3.Zero, pinned.Changes.AppliedDelta);

        // Remove the number and the opening slides along the wall, but not out of it.
        Solved freed = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new RemoveRelationship(along)));
        Solved dragged = Assert.IsType<Solved>(Updater.Apply(freed.Sketch, Drag.InPlan(opening, wanted)));

        Assert.Equal(new Vector3(Length.Inches(10), Length.Zero, Length.Zero), dragged.Changes.AppliedDelta);
        SketchAssert.BoxIs(dragged.Sketch, opening, 46, 0, 36, 6);
        SketchAssert.BoxIs(dragged.Sketch, wall, 0, 0, 120, 6);
        SketchAssert.IsConsistent(dragged.Sketch);
    }

    [Fact]
    public void ADragMovesEverythingCoupledToWhatWasGrabbed()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(30, 0, 10, 4);
        builder.Flush(a, BoxEdge.East, b, BoxEdge.West);

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            Drag.InPlan(a, new Vector2(Length.Inches(5), Length.Inches(7)))));

        // The flush couples X, so B comes along on X; it is free on Y, so it stays.
        Assert.Equal(new Vector3(Length.Inches(5), Length.Inches(7), Length.Zero), result.Changes.AppliedDelta);
        SketchAssert.BoxIs(result.Sketch, a, 5, 7, 30, 4);
        SketchAssert.BoxIs(result.Sketch, b, 35, 0, 10, 4);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void DraggingANodeOfAHorizontalSegmentTakesTheOtherEndWithItOnY()
    {
        SketchBuilder builder = new();
        EntityId start = builder.AddNode(0, 0);
        EntityId end = builder.AddNode(10, 0);
        EntityId segment = builder.AddSegment(start, end);
        builder.Add(id => new Horizontal(id, new SegmentRef(segment)));

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            Drag.InPlan(start, new Vector2(Length.Inches(3), Length.Inches(4)))));

        Assert.Equal(Point2.Inches(3, 4), result.Sketch.Find<Node>(start)!.Position);
        Assert.Equal(Point2.Inches(10, 4), result.Sketch.Find<Node>(end)!.Position);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void DragEdgeGrowsABoxFromTheEdgeThatWasGrabbed()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(10, 0, 30, 4);

        // Grabbing the far edge leaves the anchor alone.
        Solved east = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(box, BoxEdge.East, Length.Inches(5))));
        SketchAssert.BoxIs(east.Sketch, box, 10, 0, 35, 4);
        Assert.Equal(new Vector3(Length.Inches(5), Length.Zero, Length.Zero), east.Changes.AppliedDelta);
        Assert.Contains(box, east.Changes.Resized);

        // Grabbing the anchor's own edge moves the anchor and leaves the far edge.
        Solved west = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(box, BoxEdge.West, Length.Inches(5))));
        SketchAssert.BoxIs(west.Sketch, box, 5, 0, 35, 4);
        Assert.Equal(new Vector3(-Length.Inches(5), Length.Zero, Length.Zero), west.Changes.AppliedDelta);
        Assert.Equal(Point2.Inches(40, 0), west.Sketch.Find<Box>(box)!.Corner(BoxCorner.SouthEast));

        // And north grows the height.
        Solved north = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(box, BoxEdge.North, Length.Inches(2))));
        SketchAssert.BoxIs(north.Sketch, box, 10, 0, 30, 6);
        Assert.Equal(new Vector3(Length.Zero, Length.Inches(2), Length.Zero), north.Changes.AppliedDelta);
    }

    [Fact]
    public void DragEdgeAgainstAFlushToAnAnchoredBoxDoesNotMoveTheEdgeAtAll()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(30, 0, 10, 4);
        builder.Flush(a, BoxEdge.East, b, BoxEdge.West);
        builder.Anchor(b);

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(a, BoxEdge.East, Length.Inches(5))));

        Assert.Equal(Vector3.Zero, result.Changes.AppliedDelta);
        SketchAssert.BoxIs(result.Sketch, a, 0, 0, 30, 4);
        Assert.True(result.Changes.IsEmpty);

        // The opposite edge is free, though: the anchor moves and the flush still holds.
        Solved west = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(a, BoxEdge.West, Length.Inches(5))));
        SketchAssert.BoxIs(west.Sketch, a, -5, 0, 35, 4);
        SketchAssert.IsConsistent(west.Sketch);
    }

    [Trait("Feature", "GEO-013")]
    [Fact]
    public void DragEdgeNeverOverridesANumberTheUserTyped()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4);
        builder.WidthIs(box, Length.Inches(30));

        Assert.Equal(
            new Rejected(RejectionReason.DrivenSize),
            Updater.Apply(builder.Sketch, new DragEdge(box, BoxEdge.East, Length.Inches(5))));

        // The height is not driven, so that handle still works.
        Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new DragEdge(box, BoxEdge.North, Length.Inches(2))));
    }

    [Fact]
    public void DragEdgeWillNotShrinkABoxToNothing()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4);

        Assert.Equal(
            new Rejected(RejectionReason.NonPositiveSize),
            Updater.Apply(builder.Sketch, new DragEdge(box, BoxEdge.East, -Length.Inches(30))));
    }

    [Fact]
    public void DragEdgeOnARotatedBoxMovesTheEdgeInTheDirectionItFaces()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4, quarterTurns: 1);

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(box, BoxEdge.East, Length.Inches(5))));

        // The box's local +X points along global +Y at a quarter turn.
        SketchAssert.BoxIs(result.Sketch, box, 0, 0, 35, 4);
        Assert.Equal(new Vector3(Length.Zero, Length.Inches(5), Length.Zero), result.Changes.AppliedDelta);
    }
}

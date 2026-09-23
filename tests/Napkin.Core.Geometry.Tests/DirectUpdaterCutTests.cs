using System.Collections.Immutable;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The updater golden cases 7 to 11d of docs/design/shaped-parts-model.md &#xA7;9.1: the two cut
/// requests of &#xA7;2.2, the post-write fit check and the <see cref="DragEdge"/> clamp of
/// &#xA7;2.3, and the relationship readings of &#xA7;2.1 and &#xA7;2.5.
/// </summary>
public class DirectUpdaterCutTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    // ---------------------------------------------------------------------------------------
    // Test 7: SetCut and RemoveCut
    // ---------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-014")]
    [Fact]
    public void Case7_SettingACutOnAFreeCornerMovesNothingAndDisturbsNoRelationship()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBox(0, 0, 48, 24);
        EntityId leg = builder.AddBox(48, 0, 4, 24);
        RelationshipId flush = builder.Flush(top, BoxEdge.East, leg, BoxEdge.West);

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            new SetCut(top, new RoundedCorner(BoxCorner.NorthEast, Length.Inches(1)))));

        // §2.2: neither moved nor resized, so the box is in Modified and in nothing else.
        Assert.Equal(top, Assert.Single(result.Changes.Modified));
        Assert.Empty(result.Changes.Moved);
        Assert.Empty(result.Changes.Resized);
        Assert.Null(result.Changes.AppliedDelta);

        Assert.Equal(
            new RoundedCorner(BoxCorner.NorthEast, Length.Inches(1)),
            Assert.Single(result.Sketch.Find<Box>(top)!.Cuts));

        // Nothing moved and the flush is still there, untouched.
        SketchAssert.BoxIs(result.Sketch, top, 0, 0, 48, 24);
        SketchAssert.BoxIs(result.Sketch, leg, 48, 0, 4, 24);
        Assert.Equal(builder.Sketch.Relationships[flush], result.Sketch.Relationships[flush]);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Trait("Feature", "GEO-014")]
    [Fact]
    public void Case7_TheSameSiteAgainReplacesTheCutRatherThanAddingASecond()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBlank(0, 0, 48, 24, new RoundedCorner(BoxCorner.NorthEast, Length.Inches(1)));

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            new SetCut(top, new RoundedCorner(BoxCorner.NorthEast, Length.Inches(3)))));

        Assert.Equal(
            new RoundedCorner(BoxCorner.NorthEast, Length.Inches(3)),
            Assert.Single(result.Sketch.Find<Box>(top)!.Cuts));

        // A different kind at the same site replaces it too: one operation per site (§6).
        Solved kindChanged = Assert.IsType<Solved>(Updater.Apply(
            result.Sketch,
            new SetCut(top, new CornerCut(BoxCorner.NorthEast, Length.Inches(4), Length.Inches(2)))));

        Assert.Equal(
            new CornerCut(BoxCorner.NorthEast, Length.Inches(4), Length.Inches(2)),
            Assert.Single(kindChanged.Sketch.Find<Box>(top)!.Cuts));

        // Setting the very same cut again is not a change at all.
        Solved again = Assert.IsType<Solved>(Updater.Apply(
            kindChanged.Sketch,
            new SetCut(top, new CornerCut(BoxCorner.NorthEast, Length.Inches(4), Length.Inches(2)))));

        Assert.True(again.Changes.IsEmpty);
        Assert.Equal(kindChanged.Sketch, again.Sketch);
    }

    [Trait("Feature", "GEO-013")]
    [Fact]
    public void Case7_ACornerACurvedEdgeClaimsIsRefusedAndSaysWhichSite()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBlank(0, 0, 48, 24, new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(2)));

        // Invariant 6: a curve is an operation on three sites, so neither of its corners is free.
        Rejected refused = Assert.IsType<Rejected>(Updater.Apply(
            builder.Sketch,
            new SetCut(top, new CornerCut(BoxCorner.NorthEast, Length.Inches(3), Length.Inches(3)))));

        Assert.Equal(RejectionReason.CutSiteTaken, refused.Reason);
        Assert.Contains(top.ToString(), refused.Detail!.Message, StringComparison.Ordinal);
        Assert.Contains("NorthEast corner", refused.Detail.Message, StringComparison.Ordinal);

        // The other way round is the same invariant: a curve whose corner is already cut.
        SketchBuilder clipped = new();
        EntityId other = clipped.AddBlank(
            0, 0, 48, 24, new CornerCut(BoxCorner.NorthEast, Length.Inches(3), Length.Inches(3)));

        Assert.Equal(
            RejectionReason.CutSiteTaken,
            Assert.IsType<Rejected>(Updater.Apply(
                clipped.Sketch,
                new SetCut(other, new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(2))))).Reason);
    }

    [Trait("Feature", "GEO-013")]
    [Fact]
    public void Case7_RemovingACutFromAnEmptySiteIsRefused()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBlank(0, 0, 48, 24, new RoundedCorner(BoxCorner.NorthEast, Length.Inches(1)));

        Assert.Equal(
            RejectionReason.NoSuchCut,
            Assert.IsType<Rejected>(Updater.Apply(
                builder.Sketch, new RemoveCut(top, CutSite.Corner(BoxCorner.SouthWest)))).Reason);

        // Nor does a curve claiming a corner put a cut at that corner's own site.
        Assert.Equal(
            RejectionReason.NoSuchCut,
            Assert.IsType<Rejected>(Updater.Apply(
                builder.Sketch, new RemoveCut(top, CutSite.Edge(BoxEdge.North)))).Reason);

        Solved removed = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch, new RemoveCut(top, CutSite.Corner(BoxCorner.NorthEast))));

        Assert.Empty(removed.Sketch.Find<Box>(top)!.Cuts);
        Assert.Equal(top, Assert.Single(removed.Changes.Modified));
    }

    [Trait("Feature", "GEO-013")]
    [Fact]
    public void Case7_CuttingSomethingThatIsNotABlankIsRejectedRatherThanThrown()
    {
        SketchBuilder builder = new();
        EntityId node = builder.AddNode(0, 0);
        EntityId missing = SketchBuilder.EntityIdAt(9);
        Cut cut = new RoundedCorner(BoxCorner.NorthEast, Length.Inches(1));

        Assert.Equal(
            RejectionReason.UnknownEntity,
            Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetCut(missing, cut))).Reason);
        Assert.Equal(
            RejectionReason.UnknownEntity,
            Assert.IsType<Rejected>(Updater.Apply(
                builder.Sketch, new RemoveCut(missing, cut.Site))).Reason);

        // A node has no edges to cut, and a dimension is an annotation: the wrong kind of
        // reference, not a cut this updater cannot make.
        Assert.Equal(
            RejectionReason.DanglingReference,
            Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetCut(node, cut))).Reason);
        Assert.Equal(
            RejectionReason.DanglingReference,
            Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new RemoveCut(node, cut.Site))).Reason);
    }

    // ---------------------------------------------------------------------------------------
    // Test 8: the post-write fit check
    // ---------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-013")]
    [Fact]
    public void Case8_ShrinkingABlankBelowItsCutIsRefusedNamingTheBoxAndTheSite()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBlank(0, 0, 48, 24, new RoundedCorner(BoxCorner.NorthEast, Length.Inches(6)));
        RelationshipId width = builder.WidthIs(top, Length.Inches(48));

        Rejected refused = Assert.IsType<Rejected>(
            Updater.Apply(builder.Sketch, new SetParameter(width, Length.Inches(4))));

        Assert.Equal(RejectionReason.CutDoesNotFit, refused.Reason);
        Assert.Contains(top.ToString(), refused.Detail!.Message, StringComparison.Ordinal);
        Assert.Contains("NorthEast corner", refused.Detail.Message, StringComparison.Ordinal);

        // §2.3: the original sketch is returned untouched by construction — a rejection carries no
        // sketch at all, so the caller still holds the one it put the request to.
        SketchAssert.BoxIs(builder.Sketch, top, 0, 0, 48, 24);

        // Growing is fine: a stated radius is a stated fact, and 6" still fits a wider blank.
        Solved grown = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(width, Length.Inches(60))));

        SketchAssert.BoxIs(grown.Sketch, top, 0, 0, 60, 24);
        Assert.Equal(top, Assert.Single(grown.Changes.Resized));
        SketchAssert.IsConsistent(grown.Sketch);
    }

    [Trait("Feature", "GEO-014")]
    [Fact]
    public void Case8_AnEqualParamFromAnotherBoxIsRefusedTheSameWayAndTheWholeBatchIsUntouched()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBlank(0, 0, 48, 24, new RoundedCorner(BoxCorner.NorthEast, Length.Inches(6)));
        EntityId narrow = builder.AddBox(0, 40, 4, 24);
        builder.WidthIs(narrow, Length.Inches(4));

        // The propagator knows nothing about cuts, so without §2.3's seam this would quietly copy
        // 4" onto a blank that needs at least 6".
        Batch batch = Batch.Of(
            new SetName(top, "Top"),
            new AddRelationship(new EqualParam(
                SketchBuilder.RelationshipIdAt(9), new BoxWidthRef(narrow), new BoxWidthRef(top))));

        Rejected refused = Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, batch));

        Assert.Equal(RejectionReason.CutDoesNotFit, refused.Reason);
        Assert.Contains(top.ToString(), refused.Detail!.Message, StringComparison.Ordinal);

        // Atomic: the rename that came first did not happen either, and no relationship was added.
        Assert.Equal(string.Empty, builder.Sketch.Find<Box>(top)!.Name);
        Assert.Empty(builder.Sketch.RelationshipsInOrder.OfType<EqualParam>());
        SketchAssert.BoxIs(builder.Sketch, top, 0, 0, 48, 24);
    }

    // ---------------------------------------------------------------------------------------
    // Test 9: the DragEdge clamp
    // ---------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-012")]
    [Fact]
    public void Case9_DraggingAnEdgeTowardsACutClampsAtWhatTheCutLeaves()
    {
        // The corner cut claims 12" of the south edge, so the blank cannot be narrower than 12"
        // and still carry it (invariants 7 and 8).
        SketchBuilder builder = new();
        EntityId blank = builder.AddBlank(
            0, 0, 48, 24, new CornerCut(BoxCorner.SouthEast, Length.Inches(12), Length.Inches(6)));

        Solved clamped = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(blank, BoxEdge.East, Length.Inches(-40))));

        // Best effort, and the applied delta is what actually happened: 36" of the 40" asked for.
        Assert.Equal(new Vector3(Length.Inches(-36), Length.Zero, Length.Zero), clamped.Changes.AppliedDelta);
        SketchAssert.BoxIs(clamped.Sketch, blank, 0, 0, 12, 24);
        Assert.Equal(blank, Assert.Single(clamped.Changes.Resized));
        SketchAssert.IsConsistent(clamped.Sketch);

        // Dragging the west edge east is the same clamp with the anchor moving instead.
        Solved fromTheWest = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(blank, BoxEdge.West, Length.Inches(-40))));

        Assert.Equal(new Vector3(Length.Inches(36), Length.Zero, Length.Zero), fromTheWest.Changes.AppliedDelta);
        SketchAssert.BoxIs(fromTheWest.Sketch, blank, 36, 0, 12, 24);
    }

    [Trait("Feature", "GEO-012")]
    [Fact]
    public void Case9_DraggingAnEdgeAwayFromACutAppliesTheWholeDelta()
    {
        SketchBuilder builder = new();
        EntityId blank = builder.AddBlank(
            0, 0, 48, 24, new CornerCut(BoxCorner.SouthEast, Length.Inches(12), Length.Inches(6)));

        Solved grown = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(blank, BoxEdge.East, Length.Inches(10))));

        Assert.Equal(new Vector3(Length.Inches(10), Length.Zero, Length.Zero), grown.Changes.AppliedDelta);
        SketchAssert.BoxIs(grown.Sketch, blank, 0, 0, 58, 24);

        // A blank with no cuts has no floor, so a drag past nothing is still refused rather than
        // clamped: the clamp is about cuts, not a new minimum size.
        EntityId plain = builder.AddBox(0, 60, 10, 10);
        Assert.Equal(
            RejectionReason.NonPositiveSize,
            Assert.IsType<Rejected>(Updater.Apply(
                builder.Sketch, new DragEdge(plain, BoxEdge.East, Length.Inches(-20)))).Reason);
    }

    [Trait("Feature", "GEO-012")]
    [Fact]
    public void Case9_AnEdgeDragThatWouldBreakAnotherBoxsCutsDoesNotMoveAtAll()
    {
        // The clamp covers the blank the user grabbed. A box this resizes through an EqualParam
        // has cuts of its own, and a drag is a question rather than a demand, so the answer here
        // is the one a drag blocked by an anchored neighbour already gets: nothing moves. Nothing
        // partial is ever handed back.
        SketchBuilder builder = new();
        EntityId shaped = builder.AddBlank(
            0, 0, 20, 20, new RoundedCorner(BoxCorner.NorthEast, Length.Inches(6)));
        EntityId plain = builder.AddBox(0, 40, 20, 20);
        builder.EqualWidths(plain, shaped);

        Solved blocked = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(plain, BoxEdge.East, Length.Inches(-16))));

        Assert.Equal(Vector3.Zero, blocked.Changes.AppliedDelta);
        Assert.True(blocked.Changes.IsEmpty);
        SketchAssert.BoxIs(blocked.Sketch, plain, 0, 40, 20, 20);
        SketchAssert.BoxIs(blocked.Sketch, shaped, 0, 0, 20, 20);

        // Growing through the same EqualParam is fine, and both boxes follow.
        Solved grown = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new DragEdge(plain, BoxEdge.East, Length.Inches(10))));

        SketchAssert.BoxIs(grown.Sketch, plain, 0, 40, 30, 20);
        SketchAssert.BoxIs(grown.Sketch, shaped, 0, 0, 30, 20);
        SketchAssert.IsConsistent(grown.Sketch);
    }

    [Trait("Feature", "GEO-012")]
    [Fact]
    public void Case9_ABlankThatAlreadyDoesNotFitItsCutsCannotBeDraggedSmaller()
    {
        // Such a blank cannot be reached through the updater — Validate and AddEntity both refuse
        // it — but DragEdge does not re-validate what it was handed, and clamping to a floor that
        // does not exist would be worse than clamping to where it already is.
        Box broken = Box.AsDrawn(
            SketchBuilder.EntityIdAt(1),
            LayerId.Default,
            Point2.Origin,
            Length.Inches(10),
            Length.Inches(10),
            Box.DefaultDepth,
            Angle.Zero) with
        {
            Cuts = [new CornerCut(BoxCorner.SouthWest, Length.Inches(20), Length.Inches(1))],
        };

        Sketch sketch = Sketch.Empty.WithEntity(broken);
        Assert.False(sketch.Validate().IsValid);

        Solved held = Assert.IsType<Solved>(
            Updater.Apply(sketch, new DragEdge(broken.Id, BoxEdge.East, Length.Inches(-4))));

        Assert.Equal(Vector3.Zero, held.Changes.AppliedDelta);
        Assert.Equal(Length.Inches(10), held.Sketch.Find<Box>(broken.Id)!.Width);
    }

    // ---------------------------------------------------------------------------------------
    // Test 10: rotation through the real updater
    // ---------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Case10_RotatingABlankWithCutsRotatesItsOutlineExactly()
    {
        SketchBuilder builder = new();
        EntityId blank = builder.AddBlank(
            10,
            20,
            48,
            24,
            new CornerCut(BoxCorner.SouthEast, Length.Inches(12), Length.Inches(6)),
            new RoundedCorner(BoxCorner.SouthWest, Length.Inches(4)),
            new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(3)));

        Box before = builder.BoxOf(blank);
        Angle quarterTurn = Angle.Zero.Rotate90(1);

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetRotation(blank, quarterTurn)));

        Assert.Equal(blank, Assert.Single(result.Changes.Modified));
        Assert.Empty(result.Changes.Moved);
        Assert.Empty(result.Changes.Resized);

        // The outline is in the blank's own frame (assembly-model §7.2), so turning the box does
        // not change it; placed in the plan, every point of it turns exactly about the anchor.
        Box after = result.Sketch.Find<Box>(blank)!;
        ImmutableArray<OutlineSegment> was = before.Outline().Segments;
        ImmutableArray<OutlineSegment> now = after.Outline().Segments;

        Assert.Equal(was, now);
        for (int i = 0; i < was.Length; i++)
        {
            Assert.Equal(Turned(before.Anchor.XY, Placed(before, was[i]), quarterTurn), Placed(after, now[i]));
        }
    }

    /// <summary>One segment of a blank's local outline, placed in the plan by the box it is on.</summary>
    private static OutlineSegment Placed(Box box, OutlineSegment segment)
    {
        Point2 At(Point2 local) => box.World(new Vector3(local.X, local.Y, Length.Zero)).XY;

        return segment switch
        {
            StraightSegment straight => new StraightSegment(At(straight.From), At(straight.To)),
            ArcByCenter arc => new ArcByCenter(At(arc.From), At(arc.To), At(arc.Center)),
            ArcThrough arc => new ArcThrough(At(arc.From), At(arc.Through), At(arc.To)),
            _ => throw new InvalidOperationException($"Unknown segment {segment.GetType().Name}."),
        };
    }

    /// <summary>One segment of an outline, rotated about the anchor by a right-angle multiple.</summary>
    private static OutlineSegment Turned(Point2 anchor, OutlineSegment segment, Angle by)
    {
        Point2 At(Point2 point) => anchor + (point - anchor).Rotate(by);

        return segment switch
        {
            StraightSegment straight => new StraightSegment(At(straight.From), At(straight.To)),
            ArcByCenter arc => new ArcByCenter(At(arc.From), At(arc.To), At(arc.Center)),
            ArcThrough arc => new ArcThrough(At(arc.From), At(arc.Through), At(arc.To)),
            _ => throw new InvalidOperationException($"Unknown segment {segment.GetType().Name}."),
        };
    }

    // ---------------------------------------------------------------------------------------
    // Test 11: relationships read the blank, not the shape
    // ---------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-010")]
    [Fact]
    public void Case11_ResizingAPlainBoardTranslatesTheCutBoardCutsAndAll()
    {
        SketchBuilder builder = new();
        EntityId board = builder.AddBox(0, 0, 30, 4);
        EntityId shaped = builder.AddBlank(
            30, 0, 12, 4, new CornerCut(BoxCorner.NorthEast, Length.Inches(3), Length.Inches(2)));
        builder.Flush(board, BoxEdge.East, shaped, BoxEdge.West);
        builder.Anchor(board);
        RelationshipId width = builder.WidthIs(board, Length.Inches(30));

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(width, Length.Inches(40))));

        // The cut board moved by the whole 10" and kept its cut exactly: a cut is in the blank's
        // local frame, so it travels with it (§2.1).
        SketchAssert.BoxIs(result.Sketch, shaped, 40, 0, 12, 4);
        Assert.Equal(
            new CornerCut(BoxCorner.NorthEast, Length.Inches(3), Length.Inches(2)),
            Assert.Single(result.Sketch.Find<Box>(shaped)!.Cuts));

        // The two edge lines still coincide, which is what Flush means even where a cut has eaten
        // part of the run.
        Assert.Equal(
            result.Sketch.Find<Box>(board)!.Corner(BoxCorner.SouthEast).X,
            result.Sketch.Find<Box>(shaped)!.Corner(BoxCorner.SouthWest).X);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Trait("Feature", "GEO-010")]
    [Fact]
    public void Case11_CoincidentOnARoundedCornerHoldsAtTheVirtualCornerBeforeAndAfterRemovingIt()
    {
        SketchBuilder builder = new();
        EntityId shaped = builder.AddBlank(
            0, 0, 24, 12, new RoundedCorner(BoxCorner.NorthEast, Length.Inches(2)));
        EntityId pin = builder.AddNode(100, 100);
        builder.Anchor(pin);

        // §2.1: the point is the blank's virtual corner — where the two edge lines would meet —
        // not a point on the arc, so the blank lands with its rounded-off corner exactly on the
        // pin and the arc nowhere near it.
        Solved placed = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            new AddRelationship(new Coincident(
                SketchBuilder.RelationshipIdAt(2), TestRefs.Corner(shaped, BoxCorner.NorthEast), new NodeRef(pin)))));

        Assert.Equal(Point2.Inches(100, 100), placed.Sketch.Find<Box>(shaped)!.Corner(BoxCorner.NorthEast));
        SketchAssert.BoxIs(placed.Sketch, shaped, 76, 88, 24, 12);
        SketchAssert.IsConsistent(placed.Sketch);

        // A drag cannot pull it off the pin, either.
        Solved held = Assert.IsType<Solved>(
            Updater.Apply(placed.Sketch, Drag.InPlan(shaped, new Vector2(Length.Inches(5), Length.Inches(5)))));

        Assert.Equal(Vector3.Zero, held.Changes.AppliedDelta);
        placed = held;

        // Nothing moves when the cut goes: the virtual corner was the blank's corner all along.
        Solved unrounded = Assert.IsType<Solved>(
            Updater.Apply(placed.Sketch, new RemoveCut(shaped, CutSite.Corner(BoxCorner.NorthEast))));

        Assert.Equal(Point2.Inches(100, 100), unrounded.Sketch.Find<Box>(shaped)!.Corner(BoxCorner.NorthEast));
        Assert.Equal(
            placed.Sketch.Find<Box>(shaped)!.Anchor,
            unrounded.Sketch.Find<Box>(shaped)!.Anchor);
        SketchAssert.IsConsistent(unrounded.Sketch);
    }

    // ---------------------------------------------------------------------------------------
    // Test 11a: the gussets of §2.5
    // ---------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-010")]
    [Fact]
    public void Case11a_TwoGussetsSnapToEachOtherAndToAPlainBoardOnBlankEdgeLines()
    {
        // A is cut corner to corner at the north-east, B at the north-west: mirror-image
        // triangles. A's east edge line is one the diagonal removed entirely, and Flush holds it
        // anyway, because it ties lines and not extents.
        SketchBuilder builder = new();
        EntityId board = builder.AddBox(0, 0, 48, 4);
        EntityId a = builder.AddBlank(
            0, 4, 6, 6, new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(6)));
        EntityId b = builder.AddBlank(
            6, 4, 6, 6, new CornerCut(BoxCorner.NorthWest, Length.Inches(6), Length.Inches(6)));

        Sketch seated = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, Batch.Of(
            new AddRelationship(new Flush(
                SketchBuilder.RelationshipIdAt(1), TestRefs.Edge(a, BoxEdge.South), TestRefs.Edge(board, BoxEdge.North))),
            new AddRelationship(new Flush(
                SketchBuilder.RelationshipIdAt(2), TestRefs.Edge(b, BoxEdge.West), TestRefs.Edge(a, BoxEdge.East))),
            new AddRelationship(new Flush(
                SketchBuilder.RelationshipIdAt(3), TestRefs.Edge(b, BoxEdge.South), TestRefs.Edge(board, BoxEdge.North)))))).Sketch;

        SketchAssert.IsConsistent(seated);
        SketchAssert.BoxIs(seated, a, 0, 4, 6, 6);
        SketchAssert.BoxIs(seated, b, 6, 4, 6, 6);

        // Dragging A along the shared vertical line drags B with it; the board is only tied on the
        // horizontal lines, so it stays where it is.
        Solved dragged = Assert.IsType<Solved>(
            Updater.Apply(seated, Drag.InPlan(a, new Vector2(Length.Inches(10), Length.Zero))));

        Assert.Equal(new Vector3(Length.Inches(10), Length.Zero, Length.Zero), dragged.Changes.AppliedDelta);
        SketchAssert.BoxIs(dragged.Sketch, a, 10, 4, 6, 6);
        SketchAssert.BoxIs(dragged.Sketch, b, 16, 4, 6, 6);
        SketchAssert.BoxIs(dragged.Sketch, board, 0, 0, 48, 4);
        SketchAssert.IsConsistent(dragged.Sketch);

        // The two triangles' points meet at the board's edge line with a V between them: the cut
        // segments both end on x = A's east edge line at the board's north line.
        Assert.Equal(
            dragged.Sketch.Find<Box>(a)!.Corner(BoxCorner.SouthEast),
            dragged.Sketch.Find<Box>(b)!.Corner(BoxCorner.SouthWest));
    }

    // ---------------------------------------------------------------------------------------
    // Test 11b: the picture-frame corner, and the gap it documents
    // ---------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-010")]
    [Fact]
    public void Case11b_AMitredFrameCornerIsExactUnderLengtheningAndOpensWhenTheWidthChanges()
    {
        // §2.5. Top rail A runs along X, 2" wide, mitred from its inner south-east corner up to
        // its outer north-east corner. Right rail B runs along Y with the mirror cut at its
        // north-west corner. The two blanks share the outer corner, and their widths are equal.
        SketchBuilder builder = new();
        EntityId a = builder.AddBlank(
            0, 0, 24, 2, new CornerCut(BoxCorner.SouthEast, Length.Inches(2), Length.Inches(2)));
        EntityId b = builder.AddBlank(
            22, -16, 2, 18, new CornerCut(BoxCorner.NorthWest, Length.Inches(2), Length.Inches(2)));

        RelationshipId length = builder.Add(id => new ParamValue(id, new BoxWidthRef(a), Length.Inches(24)));
        RelationshipId rail = builder.Add(id => new ParamValue(id, new BoxWidthRef(b), Length.Inches(2)));
        builder.Add(id => new EqualParam(id, new BoxWidthRef(b), new BoxHeightRef(a)));
        builder.Add(id => new Coincident(
            id, TestRefs.Corner(a, BoxCorner.NorthEast), TestRefs.Corner(b, BoxCorner.NorthEast)));

        // Nothing is anchored: anchoring a rail would stop its width following the EqualParam,
        // which is an ordinary conflict and not what this case is about.
        SketchAssert.IsConsistent(builder.Sketch);
        AssertTheMitreCloses(builder.Sketch, a, b);

        // Lengthen either rail and the joint holds: the coincident corner and both cuts are at the
        // same end, so everything that matters moves together.
        Sketch longer = Assert.IsType<Solved>(
            Updater.Apply(builder.Sketch, new SetParameter(length, Length.Inches(36)))).Sketch;

        SketchAssert.IsConsistent(longer);
        Assert.Equal(Length.Inches(36), longer.Find<Box>(a)!.Width);
        AssertTheMitreCloses(longer, a, b);

        // Now the gap §2.5 names, and this test documents rather than asserts away: change the
        // width through the EqualParam and the setbacks stay at the old 2". The cut still fits, so
        // §2.3 does not refuse it — it is simply no longer a full mitre, and the joint opens.
        // Keeping a cut equal to a size is a relationship on cuts, which is issue #67 and not this
        // design. When #67 lands, this test is the one that changes.
        Sketch wider = Assert.IsType<Solved>(
            Updater.Apply(longer, new SetParameter(rail, Length.Inches(3)))).Sketch;

        SketchAssert.IsConsistent(wider);
        Assert.Equal(Length.Inches(3), wider.Find<Box>(b)!.Width);
        Assert.Equal(Length.Inches(3), wider.Find<Box>(a)!.Height);

        // The setbacks did not follow the width, and so the two cut segments no longer meet.
        Assert.Equal(
            new CornerCut(BoxCorner.SouthEast, Length.Inches(2), Length.Inches(2)),
            Assert.Single(wider.Find<Box>(a)!.Cuts));
        Assert.NotEqual(CutSegment(wider, a).To, CutSegment(wider, b).From);
        Assert.NotEqual(CutSegment(wider, a).From, CutSegment(wider, b).To);
    }

    /// <summary>The two mitres are the same line with the same two endpoints, exactly (§2.5).</summary>
    private static void AssertTheMitreCloses(Sketch sketch, EntityId a, EntityId b)
    {
        StraightSegment mitreOfA = CutSegment(sketch, a);
        StraightSegment mitreOfB = CutSegment(sketch, b);

        Assert.Equal(mitreOfA.From, mitreOfB.To);
        Assert.Equal(mitreOfA.To, mitreOfB.From);
    }

    /// <summary>
    /// The one segment of a blank's outline that its single corner cut produced, placed in the
    /// plan. Every other run of a blank's boundary is along an axis of its own frame, so the mitre
    /// is the only diagonal.
    /// </summary>
    private static StraightSegment CutSegment(Sketch sketch, EntityId blank)
    {
        Box box = sketch.Find<Box>(blank)!;
        List<StraightSegment> diagonals =
        [
            .. box.Outline().Segments
                .OfType<StraightSegment>()
                .Where(segment => segment.From.X != segment.To.X && segment.From.Y != segment.To.Y),
        ];

        return (StraightSegment)Placed(box, Assert.Single(diagonals));
    }

    // ---------------------------------------------------------------------------------------
    // Test 11d: a duplicate is a value copy
    // ---------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-014")]
    [Fact]
    public void Case11d_ADuplicateIsAValueCopyWithANewIdAnOffsetAnchorAndNoRelationships()
    {
        // §2.6: no new request kind — the canvas builds this AddEntity and the updater sees an
        // ordinary addition. This is the model-level half; the canvas Duplicate command is step 6.
        SketchBuilder builder = new();
        EntityId source = builder.AddBlank(
            0,
            0,
            6,
            6,
            new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(6)));
        EntityId neighbour = builder.AddBox(0, 20, 10, 10);
        builder.Flush(source, BoxEdge.West, neighbour, BoxEdge.West);
        builder.Anchor(neighbour);

        Box original = builder.BoxOf(source) with
        {
            Name = "Gusset",
            Depth = Length.Inches(1),
            Part = new Part("1x6", Species: null, Quantity: 1, new PlanAxes(PartDimension.Length, PartDimension.Width)),
        };
        Sketch withPart = builder.Sketch.WithEntity(original);

        Vector3 offset = new(Length.Inches(8), Length.Zero, Length.Zero);
        Box copy = original with { Id = SketchBuilder.EntityIdAt(9), Anchor = original.Anchor + offset };

        Solved result = Assert.IsType<Solved>(Updater.Apply(withPart, new AddEntity(copy)));

        Box added = result.Sketch.Find<Box>(copy.Id)!;
        Assert.Equal(copy.Id, Assert.Single(result.Changes.Added));
        Assert.Equal(original.Anchor + offset, added.Anchor);
        Assert.NotEqual(original.Id, added.Id);

        // Equal in every field but the id and the anchor: putting those back gives the source.
        Assert.Equal(original, added with { Id = original.Id, Anchor = original.Anchor });

        // Unrelated until it is snapped, like any part just drawn.
        Assert.DoesNotContain(
            result.Sketch.RelationshipsInOrder,
            relationship => relationship.References.Contains(added.Id));
        SketchAssert.IsConsistent(result.Sketch);
    }
}

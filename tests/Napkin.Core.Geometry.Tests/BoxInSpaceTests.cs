namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// A box in space, docs/design/assembly-model.md &#xA7;10 step 2: golden cases 5 and 6 of &#xA7;9.1,
/// the plan/local upright distinction of case 7, property P15, and what the kernel does with a
/// depth and a tipped box before &#xA7;10 steps 3 and 4.
/// </summary>
/// <remarks>
/// Expectations are written independently of <see cref="Orientation"/>: the tip is &#xA7;1.3's table
/// typed out as coordinate formulas, and the spin is the swap-and-negate written by hand.
/// </remarks>
public class BoxInSpaceTests
{
    private static readonly BoxFace[] Faces = Enum.GetValues<BoxFace>();

    // 48" x 24" x 3/4", anchored at (1536, 2048, 4096): case 5's box.
    private const long W = 49152;
    private const long H = 24576;
    private const long D = 768;
    private static readonly Point3 CaseAnchor = new(new Length(1536), new Length(2048), new Length(4096));

    public static TheoryData<BoxFace, int> AllOrientations
    {
        get
        {
            TheoryData<BoxFace, int> data = [];
            foreach (BoxFace face in Faces)
            {
                for (int q = 0; q < 4; q++)
                {
                    data.Add(face, q);
                }
            }

            return data;
        }
    }

    private static Box CaseBox(BoxFace faceUp, int quarterTurns, Point3? anchor = null) => new(
        SketchBuilder.EntityIdAt(1),
        LayerId.Default,
        anchor ?? CaseAnchor,
        new Length(W),
        new Length(H),
        new Length(D),
        faceUp,
        Angle.Right * quarterTurns);

    /// <summary>§1.3's Tip(FaceUp), typed out: where a local point lands before the spin.</summary>
    private static (long X, long Y, long Z) Tip(BoxFace faceUp, long x, long y, long z) => faceUp switch
    {
        BoxFace.Top => (x, y, z),        // +X, +Y, +Z
        BoxFace.Bottom => (x, -y, -z),   // +X, -Y, -Z
        BoxFace.North => (x, -z, y),     // +X, +Z, -Y
        BoxFace.South => (x, z, -y),     // +X, -Z, +Y
        BoxFace.East => (-z, y, x),      // +Z, +Y, -X
        BoxFace.West => (z, y, -x),      // -Z, +Y, +X
        _ => throw new ArgumentOutOfRangeException(nameof(faceUp)),
    };

    /// <summary>A right-hand quarter-turn spin about Z, by hand.</summary>
    private static (long X, long Y) Spin(int quarterTurns, long x, long y) => quarterTurns switch
    {
        0 => (x, y),
        1 => (-y, x),
        2 => (-x, -y),
        _ => (y, -x),
    };

    // ---- Case 5 ------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Case5_EveryVertexIsTheAnchorPlusTheTurnedLocalCorner(BoxFace faceUp, int quarterTurns)
    {
        Box box = CaseBox(faceUp, quarterTurns);

        foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
        {
            foreach (BoxLevel level in Enum.GetValues<BoxLevel>())
            {
                long x = corner is BoxCorner.SouthEast or BoxCorner.NorthEast ? W : 0;
                long y = corner is BoxCorner.NorthEast or BoxCorner.NorthWest ? H : 0;
                long z = level == BoxLevel.Top ? D : 0;

                (long tx, long ty, long tz) = Tip(faceUp, x, y, z);
                (long sx, long sy) = Spin(quarterTurns, tx, ty);
                Point3 expected = new(
                    new Length(CaseAnchor.X.Units + sx),
                    new Length(CaseAnchor.Y.Units + sy),
                    new Length(CaseAnchor.Z.Units + tz));

                Point3 vertex = box.Vertex(corner, level);
                Assert.Equal(expected, vertex);

                // Nothing rounds: every component is a multiple of 256 units.
                Assert.Equal(0, vertex.X.Units % 256);
                Assert.Equal(0, vertex.Y.Units % 256);
                Assert.Equal(0, vertex.Z.Units % 256);
            }
        }

        // The anchor is the local origin vertex, whatever the orientation.
        Assert.Equal(CaseAnchor, box.Vertex(BoxCorner.SouthWest, BoxLevel.Bottom));
    }

    [Fact]
    public void Case5_ABoxAsDrawnHasTodaysCornersAndCentre()
    {
        // "A box whose Anchor.Z is zero, whose FaceUp is Top ... is today's box in every respect."
        for (int q = 0; q < 4; q++)
        {
            Box box = Box.AsDrawn(
                EntityId.New(), LayerId.Default, Point2.Inches(10, 20), Length.Inches(30), Length.Inches(8), Length.Inches(2), Angle.Right * q);

            foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
            {
                Point2 planCorner = Point2.Inches(10, 20) + box.LocalOffset(corner).Rotate(box.Rotation);
                Assert.Equal(planCorner, box.Corner(corner));
                Assert.Equal(new Point3(planCorner.X, planCorner.Y, Length.Zero), box.Vertex(corner, BoxLevel.Bottom));
                Assert.Equal(new Point3(planCorner.X, planCorner.Y, Length.Inches(2)), box.Vertex(corner, BoxLevel.Top));
            }

            Assert.Equal(Point2.Inches(10, 20) + new Vector2(Length.Inches(15), Length.Inches(4)).Rotate(box.Rotation), box.Center.XY);
            Assert.Equal(Length.Inches(1), box.Center.Z);
        }
    }

    [Fact]
    public void TheCentreRoundsHalfAUnitPerAxisThenPlacesExactly()
    {
        Box odd = new(
            EntityId.New(), LayerId.Default, Point3.Origin, new Length(3), new Length(5), new Length(7), BoxFace.East, Angle.Zero);

        // Halves 2, 2, 4 (half to even), then tipped East: (x, y, z) -> (-z, y, x).
        Assert.Equal(new Point3(new Length(-4), new Length(2), new Length(2)), odd.Center);
    }

    [Fact]
    public void SizeReadsAllThreeLocalAxes()
    {
        Box box = CaseBox(BoxFace.Top, 0);

        Assert.Equal(new Length(W), box.Size(Axis.X));
        Assert.Equal(new Length(H), box.Size(Axis.Y));
        Assert.Equal(new Length(D), box.Size(Axis.Z));
        Assert.Throws<ArgumentOutOfRangeException>(() => box.Size((Axis)3));
        Assert.Throws<ArgumentOutOfRangeException>(() => box.Vertex(BoxCorner.SouthWest, (BoxLevel)2));
        Assert.Equal(new Orientation(BoxFace.Top, Angle.Zero), box.Orientation);
    }

    [Fact]
    public void DepthAndFaceUpArePartOfABoxsValue()
    {
        Box box = CaseBox(BoxFace.Top, 0);

        Assert.NotEqual(box, box with { Depth = new Length(D + 1) });
        Assert.NotEqual(box, box with { FaceUp = BoxFace.Bottom });
        Assert.NotEqual(box, box with { Anchor = box.Anchor with { Z = Length.Zero } });
        Assert.Equal(box, box with { });
        Assert.Equal(box.GetHashCode(), (box with { }).GetHashCode());
    }

    // ---- Case 6 ------------------------------------------------------------------------------

    // §7.1's table: plan X extent, plan Y extent, before the spin, relative to the anchor.
    public static TheoryData<BoxFace, long, long, long, long> FootprintTable => new()
    {
        { BoxFace.Top, 0, W, 0, H },
        { BoxFace.Bottom, 0, W, -H, 0 },
        { BoxFace.North, 0, W, -D, 0 },
        { BoxFace.South, 0, W, 0, D },
        { BoxFace.East, -D, 0, 0, H },
        { BoxFace.West, 0, D, 0, H },
    };

    [Theory]
    [MemberData(nameof(FootprintTable))]
    public void Case6_TheFootprintIsTheTable(BoxFace faceUp, long lowX, long highX, long lowY, long highY)
    {
        foreach (int q in new[] { 0, 1, 2, 3 })
        {
            Footprint footprint = CaseBox(faceUp, q).Footprint();

            Assert.Equal(CaseAnchor.XY, footprint.Anchor);
            Assert.Equal(new Vector2(new Length(lowX), new Length(lowY)), footprint.LowCorner);
            Assert.Equal(new Length(highX - lowX), footprint.PlanWidth);
            Assert.Equal(new Length(highY - lowY), footprint.PlanHeight);
            Assert.Equal(Angle.Right * q, footprint.Rotation);
            Assert.Equal(faceUp, footprint.FaceUp);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Case6_ATopBoxsFootprintIsTodaysBoxFieldForField(int quarterTurns)
    {
        Box box = CaseBox(BoxFace.Top, quarterTurns);
        Footprint footprint = box.Footprint();

        Assert.Equal(box.Anchor.XY, footprint.Anchor);
        Assert.Equal(Vector2.Zero, footprint.LowCorner);
        Assert.Equal(box.Width, footprint.PlanWidth);
        Assert.Equal(box.Height, footprint.PlanHeight);
        Assert.Equal(box.Rotation, footprint.Rotation);
        Assert.Equal(new Footprint(box.Anchor.XY, Vector2.Zero, box.Width, box.Height, box.Rotation), footprint);

        // And every plan corner is the box's own corner, so the canvas sees what it always saw.
        foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
        {
            Assert.Equal(box.Corner(corner), footprint.Corner(corner));
        }

        Assert.Equal(box.Center.XY, footprint.Center);
    }

    // Which local face the plan sees on each side, by hand from §1.3's table (south, east, north, west).
    public static TheoryData<BoxFace, BoxFace, BoxFace, BoxFace, BoxFace> FaceAtTable => new()
    {
        { BoxFace.Top, BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West },
        { BoxFace.Bottom, BoxFace.North, BoxFace.East, BoxFace.South, BoxFace.West },
        { BoxFace.North, BoxFace.Top, BoxFace.East, BoxFace.Bottom, BoxFace.West },
        { BoxFace.South, BoxFace.Bottom, BoxFace.East, BoxFace.Top, BoxFace.West },
        { BoxFace.East, BoxFace.South, BoxFace.Bottom, BoxFace.North, BoxFace.Top },
        { BoxFace.West, BoxFace.South, BoxFace.Top, BoxFace.North, BoxFace.Bottom },
    };

    [Theory]
    [MemberData(nameof(FaceAtTable))]
    public void Case6_FaceAtNamesTheFaceTheTableImplies(BoxFace faceUp, BoxFace south, BoxFace east, BoxFace north, BoxFace west)
    {
        // The spin does not change which face is which side: plan sides are named before it.
        foreach (int q in new[] { 0, 1, 2, 3 })
        {
            Footprint footprint = CaseBox(faceUp, q).Footprint();

            Assert.Equal(south, footprint.FaceAt(BoxEdge.South));
            Assert.Equal(east, footprint.FaceAt(BoxEdge.East));
            Assert.Equal(north, footprint.FaceAt(BoxEdge.North));
            Assert.Equal(west, footprint.FaceAt(BoxEdge.West));

            // The up face and its opposite are never a plan side.
            Assert.Null(footprint.SideOf(faceUp));
            Assert.Null(footprint.SideOf(BoxFeature.Opposite(faceUp)));
            Assert.Equal(BoxEdge.South, footprint.SideOf(south));
            Assert.Equal(BoxEdge.West, footprint.SideOf(west));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => CaseBox(faceUp, 0).Footprint().FaceAt((BoxEdge)4));
    }

    [Fact]
    public void Case6_ForEastUpThePlansWestSideIsTheBlanksTop()
        => Assert.Equal(BoxFace.Top, CaseBox(BoxFace.East, 0).Footprint().FaceAt(BoxEdge.West));

    [Fact]
    public void Case7_APlanUprightIsNotALocalUprightOnceTheBoxIsTipped()
    {
        Footprint east = CaseBox(BoxFace.East, 0).Footprint();

        BoxFeature upright = east.UprightAt(BoxCorner.SouthWest);
        Assert.NotEqual(BoxFeature.LocalUpright(BoxCorner.SouthWest), upright);
        Assert.Equal(BoxFeature.Edge(BoxFace.South, BoxFace.Top), upright);

        // It stands vertical: its two faces fix world X and Y, and nothing fixes Z.
        Orientation o = new(BoxFace.East, Angle.Zero);
        Assert.Equal(
            [Axis.X, Axis.Y],
            upright.Faces.Select(face => o.Normal(face).Axis).OrderBy(axis => axis));

        // For a box as drawn the two words agree at every corner.
        Footprint top = CaseBox(BoxFace.Top, 0).Footprint();
        foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
        {
            Assert.Equal(BoxFeature.LocalUpright(corner), top.UprightAt(corner));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => top.UprightAt((BoxCorner)4));
        Assert.Throws<ArgumentOutOfRangeException>(() => top.Corner((BoxCorner)4));
    }

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void P15_TheFootprintIsTheProjectionOfTheVertices(BoxFace faceUp, int quarterTurns)
    {
        Box box = CaseBox(faceUp, quarterTurns);
        Footprint footprint = box.Footprint();

        // Un-spin the eight vertices' plan projections about the anchor: their bounding rectangle
        // is the footprint's.
        List<Vector2> unspun = [];
        foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
        {
            foreach (BoxLevel level in Enum.GetValues<BoxLevel>())
            {
                unspun.Add(footprint.ToLocal(box.Vertex(corner, level).XY) + footprint.LowCorner);
            }
        }

        Assert.Equal(footprint.LowCorner.Dx, unspun.Min(v => v.Dx));
        Assert.Equal(footprint.LowCorner.Dy, unspun.Min(v => v.Dy));
        Assert.Equal(footprint.LowCorner.Dx + footprint.PlanWidth, unspun.Max(v => v.Dx));
        Assert.Equal(footprint.LowCorner.Dy + footprint.PlanHeight, unspun.Max(v => v.Dy));

        // Round trip between the plan and the footprint's own frame.
        Vector2 inside = new(footprint.PlanWidth.Divide(3, Rounding.HalfToEven), footprint.PlanHeight.Divide(5, Rounding.HalfToEven));
        Assert.Equal(inside, footprint.ToLocal(footprint.FromLocal(inside)));
    }

    // ---- The depth through the updater ------------------------------------------------------

    [Fact]
    public void ATypedDepthIsAParamValueTheUpdaterHolds()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 4);
        DirectUpdater updater = new();

        RelationshipId depth = RelationshipId.New();
        Solved typed = Assert.IsType<Solved>(updater.Apply(
            builder.Sketch,
            new AddRelationship(new ParamValue(depth, new BoxDepthRef(box), Length.Inches(2)))));

        Box resized = typed.Sketch.Find<Box>(box)!;
        Assert.Equal(Length.Inches(2), resized.Depth);
        Assert.Equal([box], typed.Changes.Resized);
        Assert.Empty(typed.Changes.Moved);
        Assert.Equal(Length.Inches(2), typed.Sketch.ValueOf(new BoxDepthRef(box)));
        SketchAssert.IsConsistent(typed.Sketch);

        // Then SetParameter edits it, and a non-positive one is refused like any size.
        Solved edited = Assert.IsType<Solved>(updater.Apply(typed.Sketch, new SetParameter(depth, Length.Inches(3))));
        Assert.Equal(Length.Inches(3), edited.Sketch.Find<Box>(box)!.Depth);
        Assert.Equal(
            RejectionReason.NonPositiveSize,
            Assert.IsType<Rejected>(updater.Apply(typed.Sketch, new SetParameter(depth, Length.Zero))).Reason);

        // The plan never moved: no plan corner of a box lying as drawn depends on its depth.
        Assert.Equal(builder.BoxOf(box).Anchor, edited.Sketch.Find<Box>(box)!.Anchor);
        Assert.Equal(builder.BoxOf(box).Width, edited.Sketch.Find<Box>(box)!.Width);
    }

    [Fact]
    public void EqualDepthsPropagateAndAnAnchoredDepthConflictsByName()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 10, 4);
        EntityId b = builder.AddBox(20, 0, 10, 4);
        builder.Add(id => new EqualParam(id, new BoxDepthRef(a), new BoxDepthRef(b)));
        DirectUpdater updater = new();

        Solved both = Assert.IsType<Solved>(updater.Apply(
            builder.Sketch,
            new AddRelationship(new ParamValue(RelationshipId.New(), new BoxDepthRef(a), Length.Inches(2)))));
        Assert.Equal(Length.Inches(2), both.Sketch.Find<Box>(b)!.Depth);
        Assert.Equal(new[] { a, b }.OrderBy(id => id), both.Changes.Resized.OrderBy(id => id));

        // Anchored pins all of a box's sizes, the depth among them.
        builder.Anchor(b);
        OverConstrained refused = Assert.IsType<OverConstrained>(updater.Apply(
            builder.Sketch,
            new AddRelationship(new ParamValue(RelationshipId.New(), new BoxDepthRef(a), Length.Inches(2)))));
        Assert.Contains("depth", refused.Conflict.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ADepthOfNothingIsNotABox()
    {
        Box flat = CaseBox(BoxFace.Top, 0) with { Depth = Length.Zero };

        Assert.Equal(
            RejectionReason.NonPositiveSize,
            Assert.IsType<Rejected>(new DirectUpdater().Apply(Sketch.Empty, new AddEntity(flat))).Reason);
        ValidationError error = Assert.Single(Sketch.Empty.WithEntity(flat).Validate().Errors);
        Assert.Equal(ValidationErrorKind.NonPositiveSize, error.Kind);
        Assert.Contains("deep", error.Message, StringComparison.Ordinal);
    }

    // ---- Before steps 3 and 4 ---------------------------------------------------------------

    [Fact]
    public void ADragAlongZIsRefusedOutLoudUntilThePropagatorHasAZ()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 4);

        Rejected refused = Assert.IsType<Rejected>(new DirectUpdater().Apply(
            builder.Sketch,
            new Drag(box, new Vector3(Length.Zero, Length.Zero, Length.Inches(1)))));
        Assert.Equal(RejectionReason.UnsupportedRequest, refused.Reason);

        // A plan drag reports a zero Z.
        Solved moved = Assert.IsType<Solved>(new DirectUpdater().Apply(
            builder.Sketch, Drag.InPlan(box, new Vector2(Length.Inches(1), Length.Inches(2)))));
        Assert.Equal(new Vector3(Length.Inches(1), Length.Inches(2), Length.Zero), moved.Changes.AppliedDelta);
    }

    [Fact]
    public void APlanMoveOfABoxInSpaceKeepsItsHeightAndItsTurn()
    {
        Box raised = CaseBox(BoxFace.North, 1);
        Sketch sketch = Sketch.Empty.WithEntity(raised);
        DirectUpdater updater = new();

        Box dragged = Assert.IsType<Solved>(updater.Apply(
            sketch, Drag.InPlan(raised.Id, new Vector2(Length.Inches(1), Length.Zero)))).Sketch.Find<Box>(raised.Id)!;
        Assert.Equal(raised.Anchor + new Vector3(Length.Inches(1), Length.Zero, Length.Zero), dragged.Anchor);
        Assert.Equal(BoxFace.North, dragged.FaceUp);

        Box placed = Assert.IsType<Solved>(updater.Apply(
            sketch, new SetPosition(raised.Id, Point2.Inches(3, 4)))).Sketch.Find<Box>(raised.Id)!;
        Assert.Equal(new Point3(Length.Inches(3), Length.Inches(4), raised.Anchor.Z), placed.Anchor);
    }

    [Fact]
    public void ResizingAnEdgeOfATurnedOverBoxMovesTheAnchorTheWayTheEdgeFaces()
    {
        // Bottom up: local Y runs south in the world, so the blank's south edge is the plan's
        // north side, and dragging it outward moves the anchor north.
        Box over = CaseBox(BoxFace.Bottom, 0, Point3.Origin);
        Sketch sketch = Sketch.Empty.WithEntity(over);

        Solved grown = Assert.IsType<Solved>(new DirectUpdater().Apply(
            sketch, new DragEdge(over.Id, BoxEdge.South, Length.Inches(1))));
        Box after = grown.Sketch.Find<Box>(over.Id)!;

        Assert.Equal(new Length(H) + Length.Inches(1), after.Height);
        Assert.Equal(new Point3(Length.Zero, Length.Inches(1), Length.Zero), after.Anchor);
        Assert.Equal(new Vector3(Length.Zero, Length.Inches(1), Length.Zero), grown.Changes.AppliedDelta);

        // The far edge — the plan's south side — did not move.
        Assert.Equal(over.Footprint().Corner(BoxCorner.SouthWest), after.Footprint().Corner(BoxCorner.SouthWest));
    }

    [Fact]
    public void ResizingAnEdgeThatStandsVerticalWaitsForDragFace()
    {
        // North up: local Y points up, so moving the south edge would move the anchor along Z.
        Box tipped = CaseBox(BoxFace.North, 0);

        Assert.Equal(
            RejectionReason.UnsupportedRequest,
            Assert.IsType<Rejected>(new DirectUpdater().Apply(
                Sketch.Empty.WithEntity(tipped), new DragEdge(tipped.Id, BoxEdge.South, Length.Inches(1)))).Reason);
    }

    [Fact]
    public void APositionalRelationshipOnATippedBoxWaitsForFeatures()
    {
        // A corner reference names the blank's corner, which the plan propagator would hold at
        // the wrong place on a box that is not Top up; step 3's features fix that. Until then it
        // is refused rather than silently wrong, and sizes still work.
        SketchBuilder builder = new();
        EntityId drawn = builder.AddBox(0, 0, 10, 4);
        EntityId node = builder.AddNode(0, 0);
        Box tipped = CaseBox(BoxFace.East, 0, Point3.Origin) with { Id = SketchBuilder.EntityIdAt(50) };
        Sketch sketch = builder.Sketch.WithEntity(tipped);
        DirectUpdater updater = new();

        foreach (Relationship relationship in new Relationship[]
        {
            new Coincident(RelationshipId.New(), new CornerRef(drawn, BoxCorner.SouthWest), new CornerRef(tipped.Id, BoxCorner.SouthWest)),
            new Flush(RelationshipId.New(), new BoxEdgeRef(drawn, BoxEdge.West), new BoxEdgeRef(tipped.Id, BoxEdge.North)),
            new AxisDistance(RelationshipId.New(), new CenterRef(tipped.Id), new NodeRef(node), Axis.X, Length.Zero),
            new Centered(RelationshipId.New(), new CenterRef(tipped.Id), new CornerRef(drawn, BoxCorner.SouthWest), new CornerRef(drawn, BoxCorner.SouthEast), Axis.X),
        })
        {
            Assert.Equal(
                RejectionReason.UnsupportedRelationship,
                Assert.IsType<Rejected>(updater.Apply(sketch, new AddRelationship(relationship))).Reason);
        }

        Assert.IsType<Solved>(updater.Apply(
            sketch, new AddRelationship(new ParamValue(RelationshipId.New(), new BoxWidthRef(tipped.Id), Length.Inches(40)))));
    }
}

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The propagator and the updater in three dimensions, docs/design/assembly-model.md &#xA7;10 step
/// 4: golden cases 9, 10, 12, 13, 13a, 15 and 16 of &#xA7;9.1. Case 11's propagation half is in
/// <see cref="ReferenceTests"/>, beside its refusals; case 17 is in <see cref="PropagatorTests"/>.
/// </summary>
/// <remarks>
/// The fixture is a table's worth of parts, in whole inches: a top 40&#x2033; by 20&#x2033; by
/// 1&#x2033; with its underside at 16&#x2033;, and 2&#x2033; legs 16&#x2033; long standing under it.
/// </remarks>
public class AssemblyPropagationTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    // ---- Case 9: a flush between caps, and the anchor rule along Z ------------------------------

    [Fact]
    public void Case9_AFlushUnderTheTopPinsZAndLeavesXAndYAlone()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBox(Point3.Inches(0, 0, 16), 40, 20, 1, name: "Top");
        EntityId leg = builder.AddBox(Point3.Inches(3, 4, 0), 2, 2, 15, name: "Leg");

        Solved held = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            new AddRelationship(new Flush(
                SketchBuilder.RelationshipIdAt(1),
                new FeatureRef(top, BoxFeature.Face(BoxFace.Bottom)),
                new FeatureRef(leg, BoxFeature.Face(BoxFace.Top))))));

        // The second place follows the first: the leg rises an inch to meet the underside, along
        // Z only, and the top is where it was.
        Assert.Equal(Point3.Inches(3, 4, 1), held.Sketch.Find<Box>(leg)!.Anchor);
        Assert.Equal(builder.BoxOf(top), held.Sketch.Find<Box>(top));
        Assert.Equal([leg], held.Changes.Moved);
        Assert.Empty(held.Changes.Resized);
        SketchAssert.IsConsistent(held.Sketch);
    }

    [Fact]
    public void Case9_TypingTheDepthOfAnAnchoredLegLiftsTheTop()
    {
        (SketchBuilder builder, EntityId top, EntityId leg, RelationshipId depth) = TopOnALeg();
        builder.Anchor(leg);

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetParameter(depth, Length.Inches(18))));

        // The leg keeps its anchor and grows up; the top translates up by the two inches.
        Box after = result.Sketch.Find<Box>(leg)!;
        Assert.Equal(Point3.Inches(0, 0, 0), after.Anchor);
        Assert.Equal(Length.Inches(18), after.Depth);
        Assert.Equal(Point3.Inches(0, 0, 18), result.Sketch.Find<Box>(top)!.Anchor);

        Assert.Equal([top], result.Changes.Moved);
        Assert.Equal([leg], result.Changes.Resized);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void Case9_TypingTheDepthOfALegUnderAnAnchoredTopMovesTheLegsAnchorDown()
    {
        (SketchBuilder builder, EntityId top, EntityId leg, RelationshipId depth) = TopOnALeg();
        builder.Anchor(top);

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetParameter(depth, Length.Inches(18))));

        // The anchor rule along Z: the leg's top face is held by the anchored top, so it is the
        // anchor that gives, two inches down; the top face stays at 16".
        Box after = result.Sketch.Find<Box>(leg)!;
        Assert.Equal(Point3.Inches(0, 0, -2), after.Anchor);
        Assert.Equal(Length.Inches(16), after.Vertex(BoxCorner.SouthWest, BoxLevel.Top).Z);
        Assert.Equal(builder.BoxOf(top), result.Sketch.Find<Box>(top));

        Assert.Equal([leg], result.Changes.Moved);
        Assert.Equal([leg], result.Changes.Resized);
        SketchAssert.IsConsistent(result.Sketch);
    }

    // ---- Case 10: four legs, one number ---------------------------------------------------------

    [Fact]
    public void Case10_TypingOneLegsDepthResizesAllFourAndTheTopRises()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBox(Point3.Inches(0, 0, 16), 40, 20, 1, name: "Top");
        EntityId[] legs =
        [
            builder.AddBox(Point3.Inches(0, 0, 0), 2, 2, 16),
            builder.AddBox(Point3.Inches(38, 0, 0), 2, 2, 16),
            builder.AddBox(Point3.Inches(38, 18, 0), 2, 2, 16),
            builder.AddBox(Point3.Inches(0, 18, 0), 2, 2, 16),
        ];

        foreach (EntityId leg in legs)
        {
            builder.FlushFaces(top, BoxFace.Bottom, leg, BoxFace.Top);
        }

        // The typed number is on the third leg, and the others follow it in a chain, so neither
        // the first leg nor the first relationship is the one that decides.
        RelationshipId typed = builder.DepthIs(legs[2], Length.Inches(16));
        builder.Add(id => new EqualParam(id, new BoxDepthRef(legs[2]), new BoxDepthRef(legs[1])));
        builder.Add(id => new EqualParam(id, new BoxDepthRef(legs[1]), new BoxDepthRef(legs[0])));
        builder.Add(id => new EqualParam(id, new BoxDepthRef(legs[3]), new BoxDepthRef(legs[0])));
        SketchAssert.IsConsistent(builder.Sketch);

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetParameter(typed, Length.Inches(18))));

        foreach (EntityId leg in legs)
        {
            Box after = result.Sketch.Find<Box>(leg)!;
            Assert.Equal(Length.Inches(18), after.Depth);
            Assert.Equal(Length.Zero, after.Anchor.Z);
        }

        Assert.Equal(Length.Inches(18), result.Sketch.Find<Box>(top)!.Anchor.Z);
        Assert.Equal([top], result.Changes.Moved);
        Assert.Equal(legs.ToHashSet(), result.Changes.Resized.ToHashSet());
        Assert.Equal(5, result.Changes.Moved.Union(result.Changes.Resized).Count);
        SketchAssert.IsConsistent(result.Sketch);
    }

    // ---- Case 12: distances and centring along Z --------------------------------------------------

    [Fact]
    public void Case12_ADistanceAlongZFromAFaceToAVertexMovesTheFreeBoxAlongZOnly()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBox(Point3.Inches(0, 0, 16), 40, 20, 1, name: "Top");
        EntityId leg = builder.AddBox(Point3.Inches(3, 4, 0), 2, 2, 16, name: "Leg");
        builder.Anchor(top);

        RelationshipId distance = SketchBuilder.RelationshipIdAt(2);
        Solved added = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch,
            new AddRelationship(new AxisDistance(
                distance,
                new FeatureRef(top, BoxFeature.Face(BoxFace.Bottom)),
                new FeatureRef(leg, BoxFeature.Vertex(BoxCorner.NorthEast, BoxLevel.Top)),
                Axis.Z,
                Length.Zero))));
        Assert.Empty(added.Changes.Moved);

        // The leg's top an inch below the underside: the leg moves down an inch, and only along Z.
        Solved lowered = Assert.IsType<Solved>(Updater.Apply(added.Sketch, new SetParameter(distance, -Length.Inches(1))));
        Assert.Equal(Point3.Inches(3, 4, -1), lowered.Sketch.Find<Box>(leg)!.Anchor);
        Assert.Equal(builder.BoxOf(top), lowered.Sketch.Find<Box>(top));
        Assert.Equal([leg], lowered.Changes.Moved);
        SketchAssert.IsConsistent(lowered.Sketch);
    }

    [Fact]
    public void Case12_CentringAlongZOnAnOddSpanRoundsHalfAUnitOnZAlone()
    {
        // A shelf centred between a bottom's top face at 768 and a lid's underside at 2049: the
        // span is odd, so the midpoint 1408.5 rounds half to even, to 1408, on Z and nowhere else.
        Sketch sketch = Sketch.Empty
            .WithEntity(new Box(SketchBuilder.EntityIdAt(1), LayerId.Default, Point3.Origin, Length.Inches(10), Length.Inches(10), new Length(768), BoxFace.Top, Angle.Zero))
            .WithEntity(new Box(SketchBuilder.EntityIdAt(2), LayerId.Default, new Point3(Length.Zero, Length.Zero, new Length(2049)), Length.Inches(10), Length.Inches(10), new Length(768), BoxFace.Top, Angle.Zero))
            .WithEntity(new Box(SketchBuilder.EntityIdAt(3), LayerId.Default, new Point3(new Length(7), new Length(-3), Length.Zero), Length.Inches(4), Length.Inches(4), new Length(256), BoxFace.Top, Angle.Zero));

        Centered centred = new(
            SketchBuilder.RelationshipIdAt(1),
            new CenterRef(SketchBuilder.EntityIdAt(3)),
            new FeatureRef(SketchBuilder.EntityIdAt(1), BoxFeature.Face(BoxFace.Top)),
            new FeatureRef(SketchBuilder.EntityIdAt(2), BoxFeature.Face(BoxFace.Bottom)),
            Axis.Z);

        Solved result = Assert.IsType<Solved>(Updater.Apply(sketch, new AddRelationship(centred)));
        Box shelf = result.Sketch.Find<Box>(SketchBuilder.EntityIdAt(3))!;

        Assert.Equal(new Length(1408), shelf.Center.Z);
        Assert.Equal(new Point3(new Length(7), new Length(-3), new Length(1280)), shelf.Anchor);
        Assert.Equal([SketchBuilder.EntityIdAt(3)], result.Changes.Moved);
        SketchAssert.IsConsistent(result.Sketch);
    }

    // ---- Case 13: turning a box -----------------------------------------------------------------

    public static TheoryData<BoxFace, int> AllOrientations
    {
        get
        {
            TheoryData<BoxFace, int> data = [];
            foreach (BoxFace face in Enum.GetValues<BoxFace>())
            {
                for (int q = 0; q < 4; q++)
                {
                    data.Add(face, q);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Case13_TurningAFreeBoxLeavesItsAnchorAndPutsEveryVertexWhereTheOrientationSays(BoxFace faceUp, int quarterTurns)
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(Point3.Inches(1, 2, 4), 48, 24, 1);
        Box before = builder.BoxOf(box);
        Angle spin = Angle.Right * quarterTurns;

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetOrientation(box, faceUp, spin)));
        Box after = result.Sketch.Find<Box>(box)!;

        Assert.Equal(new Orientation(faceUp, spin), after.Orientation);
        Assert.Equal(before.Anchor, after.Anchor);
        Assert.Equal((before.Width, before.Height, before.Depth), (after.Width, after.Height, after.Depth));

        // Case 5 pins Box.Vertex against the hand-derived tip table for every orientation.
        Box predicted = before with { FaceUp = faceUp, Rotation = spin };
        foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
        {
            foreach (BoxLevel level in Enum.GetValues<BoxLevel>())
            {
                Assert.Equal(predicted.Vertex(corner, level), after.Vertex(corner, level));
            }
        }

        if (faceUp != BoxFace.Top || quarterTurns != 0)
        {
            Assert.Equal([box], result.Changes.Modified);
        }
        else
        {
            Assert.True(result.Changes.IsEmpty);
        }

        Assert.Empty(result.Changes.Moved);
        Assert.Empty(result.Changes.Resized);
    }

    [Fact]
    public void Case13_ATurnAboutAWorldAxisIsOneRequest()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(Point3.Origin, 40, 4, 1);
        Orientation tipped = Orientation.AsDrawn.TurnedAbout(Axis.X, 1);

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, SetOrientation.To(box, tipped)));

        // A quarter turn about X stands the apron on edge: north face up, both fields in one step.
        Assert.Equal(new Orientation(BoxFace.North, Angle.Zero), result.Sketch.Find<Box>(box)!.Orientation);
    }

    [Fact]
    public void Case13_ABoxHeldByAPlaceIsNotTurnedAndTheRefusalNamesWhatHoldsIt()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBox(Point3.Inches(0, 0, 16), 40, 20, 1, name: "Top");
        EntityId leg = builder.AddBox(Point3.Inches(0, 0, 0), 2, 2, 16, name: "Leg");
        RelationshipId flush = builder.FlushFaces(top, BoxFace.Bottom, leg, BoxFace.Top);

        Rejected refused = Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetOrientation(leg, BoxFace.North, Angle.Zero)));

        Assert.Equal(RejectionReason.OrientationWithRelationships, refused.Reason);
        Assert.Equal(ValidationErrorKind.TurnWouldReinterpret, refused.Detail!.Kind);
        Assert.Contains(flush.ToString(), refused.Detail.Message, StringComparison.Ordinal);
        Assert.Contains("Leg", refused.Detail.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Case13_AnchorsSizesAndEqualSizesTurnWithTheBox()
    {
        SketchBuilder builder = new();
        EntityId leg = builder.AddBox(Point3.Origin, 2, 2, 16);
        EntityId other = builder.AddBox(Point3.Inches(10, 0, 0), 2, 2, 16);
        builder.Anchor(leg);
        builder.DepthIs(leg, Length.Inches(16));
        builder.Add(id => new EqualParam(id, new BoxDepthRef(leg), new BoxDepthRef(other)));

        Solved result = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetOrientation(leg, BoxFace.East, Angle.Right)));
        Assert.Equal(new Orientation(BoxFace.East, Angle.Right), result.Sketch.Find<Box>(leg)!.Orientation);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void Case13_ATurnOffTheQuarterTurnsIsRefused()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(Point3.Origin, 40, 4, 1);

        Assert.Equal(
            new Rejected(RejectionReason.RotationNotSupported),
            Updater.Apply(builder.Sketch, new SetOrientation(box, BoxFace.Top, Angle.Degrees(45))));
        Assert.Equal(
            new Rejected(RejectionReason.UnknownEntity),
            Updater.Apply(builder.Sketch, new SetOrientation(SketchBuilder.EntityIdAt(99), BoxFace.Top, Angle.Zero)));
        Assert.Equal(
            new Rejected(RejectionReason.UnsupportedRequest),
            Updater.Apply(builder.Sketch, new SetOrientation(box, (BoxFace)17, Angle.Zero)));
    }

    // ---- Case 13a: invariant 13 survives a turn --------------------------------------------------

    [Fact]
    public void Case13a_ADrivingWidthDimensionRefusesATurnThatStandsTheWidthUp()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(Point3.Origin, 16, 2, 3, name: "Rail");
        RelationshipId width = builder.WidthIs(box, Length.Inches(16));
        EntityId dimension = builder.AddDimension(new ParamMeasurand(new BoxWidthRef(box)), width);
        SketchAssert.IsConsistent(builder.Sketch);

        // East up stands local X along world Z: the dimension would measure along Z.
        Rejected refused = Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetOrientation(box, BoxFace.East, Angle.Zero)));
        Assert.Equal(RejectionReason.OrientationWithRelationships, refused.Reason);
        Assert.Equal(ValidationErrorKind.MeasurandLeavesThePlan, refused.Detail!.Kind);
        Assert.Contains(dimension.ToString(), refused.Detail.Message, StringComparison.Ordinal);

        // Turned over, or spun a quarter turn, the width still lies in the plan.
        foreach (SetOrientation turn in new[] { new SetOrientation(box, BoxFace.Bottom, Angle.Zero), new SetOrientation(box, BoxFace.Top, Angle.Right) })
        {
            Solved turned = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, turn));
            SketchAssert.IsConsistent(turned.Sketch, turn.ToString());
        }
    }

    [Fact]
    public void Case13a_AReferenceDimensionAloneIsEnoughToRefuseTheTurn()
    {
        // A reference dimension along Y between the blank's two west uprights: in the plan while the
        // box lies as drawn. Tipped north, those uprights fix X and Z, not Y — and there is no
        // relationship for the refusal to find, only the dimension.
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(Point3.Origin, 16, 2, 3, name: "Rail");
        EntityId dimension = builder.AddDimension(new AxisMeasurand(
            new FeatureRef(box, BoxFeature.LocalUpright(BoxCorner.SouthWest)),
            new FeatureRef(box, BoxFeature.LocalUpright(BoxCorner.NorthWest)),
            Axis.Y));
        SketchAssert.IsConsistent(builder.Sketch);
        Assert.Empty(builder.Sketch.Relationships);

        Rejected refused = Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetOrientation(box, BoxFace.North, Angle.Zero)));
        Assert.Equal(RejectionReason.OrientationWithRelationships, refused.Reason);
        Assert.Equal(ValidationErrorKind.MeasurandLeavesThePlan, refused.Detail!.Kind);
        Assert.Contains(dimension.ToString(), refused.Detail.Message, StringComparison.Ordinal);

        // A spin keeps both uprights standing, and the span still runs along a plan axis.
        Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetOrientation(box, BoxFace.Top, Angle.Right)));
    }

    // ---- Case 15: dragging a face ----------------------------------------------------------------

    [Fact]
    public void Case15_DraggingTheBottomMovesTheAnchorDownAndDraggingTheTopDoesNot()
    {
        SketchBuilder builder = new();
        EntityId leg = builder.AddBox(Point3.Inches(3, 4, 5), 2, 2, 16);

        Solved down = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new DragFace(leg, BoxFace.Bottom, Length.Inches(2))));
        Box lowered = down.Sketch.Find<Box>(leg)!;
        Assert.Equal(Point3.Inches(3, 4, 3), lowered.Anchor);
        Assert.Equal(Length.Inches(18), lowered.Depth);
        Assert.Equal(new Vector3(Length.Zero, Length.Zero, -Length.Inches(2)), down.Changes.AppliedDelta);
        Assert.Equal([leg], down.Changes.Moved);
        Assert.Equal([leg], down.Changes.Resized);

        Solved up = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new DragFace(leg, BoxFace.Top, Length.Inches(2))));
        Box raised = up.Sketch.Find<Box>(leg)!;
        Assert.Equal(Point3.Inches(3, 4, 5), raised.Anchor);
        Assert.Equal(Length.Inches(18), raised.Depth);
        Assert.Equal(new Vector3(Length.Zero, Length.Zero, Length.Inches(2)), up.Changes.AppliedDelta);
        Assert.Empty(up.Changes.Moved);
    }

    [Fact]
    public void Case15_ADrivenDepthIsNotDragged()
    {
        SketchBuilder builder = new();
        EntityId leg = builder.AddBox(Point3.Origin, 2, 2, 16);
        builder.DepthIs(leg, Length.Inches(16));

        foreach (BoxFace face in new[] { BoxFace.Bottom, BoxFace.Top })
        {
            Assert.Equal(
                new Rejected(RejectionReason.DrivenSize),
                Updater.Apply(builder.Sketch, new DragFace(leg, face, Length.Inches(1))));
        }

        // The width is not driven, so a side face still drags.
        Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new DragFace(leg, BoxFace.East, Length.Inches(1))));
    }

    [Fact]
    public void Case15_ASideFaceIsClampedByACutAndTheTopByNothingButNothingness()
    {
        // The south-east corner cut claims 12" of the south edge (shaped parts §2.3).
        SketchBuilder builder = new();
        EntityId blank = builder.AddBlank(
            0, 0, 48, 24, new CornerCut(BoxCorner.SouthEast, Length.Inches(12), Length.Inches(6)));

        Solved clamped = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new DragFace(blank, BoxFace.East, Length.Inches(-40))));
        Assert.Equal(Length.Inches(12), clamped.Sketch.Find<Box>(blank)!.Width);
        Assert.Equal(new Vector3(Length.Inches(-36), Length.Zero, Length.Zero), clamped.Changes.AppliedDelta);

        // A cut never reaches the top: the depth goes down to one unit if asked, whatever the cut.
        Length depth = builder.BoxOf(blank).Depth;
        Solved thin = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch, new DragFace(blank, BoxFace.Top, -(depth - new Length(1)))));
        Assert.Equal(new Length(1), thin.Sketch.Find<Box>(blank)!.Depth);
        SketchAssert.IsConsistent(thin.Sketch);

        // And all the way through it is no box at all.
        Assert.Equal(
            new Rejected(RejectionReason.NonPositiveSize),
            Updater.Apply(builder.Sketch, new DragFace(blank, BoxFace.Top, -depth)));
    }

    [Fact]
    public void Case15_DraggingTheTopOfALegUnderAnAnchoredTopIsBlocked()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBox(Point3.Inches(0, 0, 16), 40, 20, 1);
        EntityId leg = builder.AddBox(Point3.Origin, 2, 2, 16);
        builder.FlushFaces(top, BoxFace.Bottom, leg, BoxFace.Top);
        builder.Anchor(top);

        // Best effort: the top face is held, so it does not move at all.
        Solved blocked = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new DragFace(leg, BoxFace.Top, Length.Inches(1))));
        Assert.Equal(Vector3.Zero, blocked.Changes.AppliedDelta);
        Assert.True(blocked.Changes.IsEmpty);

        // The bottom is free: the leg grows down.
        Solved grown = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new DragFace(leg, BoxFace.Bottom, Length.Inches(1))));
        Assert.Equal(Point3.Inches(0, 0, -1), grown.Sketch.Find<Box>(leg)!.Anchor);
        SketchAssert.IsConsistent(grown.Sketch);
    }

    // ---- Case 16: dragging along Z ---------------------------------------------------------------

    [Fact]
    public void Case16_ALegFlushUnderAnAnchoredTopCannotBeDraggedUp()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBox(Point3.Inches(0, 0, 16), 40, 20, 1);
        EntityId leg = builder.AddBox(Point3.Origin, 2, 2, 16);
        builder.FlushFaces(top, BoxFace.Bottom, leg, BoxFace.Top);
        builder.Anchor(top);

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch, new Drag(leg, new Vector3(Length.Zero, Length.Zero, Length.Inches(2)))));

        Assert.Equal(Vector3.Zero, result.Changes.AppliedDelta);
        Assert.Empty(result.Changes.Moved);

        // Along X the flush on Z couples nothing, so the leg slides along under the top.
        Solved slid = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch, new Drag(leg, new Vector3(Length.Inches(3), Length.Zero, Length.Inches(2)))));
        Assert.Equal(new Vector3(Length.Inches(3), Length.Zero, Length.Zero), slid.Changes.AppliedDelta);
        Assert.Equal(Point3.Inches(3, 0, 0), slid.Sketch.Find<Box>(leg)!.Anchor);
    }

    [Fact]
    public void Case16_ABoxFlushBesideAnAnchoredOneOnXStillLifts()
    {
        SketchBuilder builder = new();
        EntityId fixedBox = builder.AddBox(Point3.Origin, 10, 4, 1);
        EntityId beside = builder.AddBox(Point3.Inches(10, 0, 0), 10, 4, 1);
        builder.Flush(fixedBox, BoxEdge.East, beside, BoxEdge.West);
        builder.Anchor(fixedBox);

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch, new Drag(beside, new Vector3(Length.Zero, Length.Zero, Length.Inches(2)))));

        Assert.Equal(new Vector3(Length.Zero, Length.Zero, Length.Inches(2)), result.Changes.AppliedDelta);
        Assert.Equal(Point3.Inches(10, 0, 2), result.Sketch.Find<Box>(beside)!.Anchor);
        SketchAssert.IsConsistent(result.Sketch);
    }

    [Fact]
    public void Case16_ANodeOnAPlanUprightDoesNotHoldTheBoxDownButDoesHoldItInThePlan()
    {
        // A node coincides with a plan upright on X and Y only, so an anchored node blocks a drag
        // along X and Y and lets the box rise.
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 4);
        EntityId node = builder.AddNode(0, 0);
        builder.Add(id => new Coincident(id, TestRefs.Corner(box, BoxCorner.SouthWest), new NodeRef(node)));
        builder.Anchor(node);

        Solved result = Assert.IsType<Solved>(Updater.Apply(
            builder.Sketch, new Drag(box, new Vector3(Length.Inches(1), Length.Inches(1), Length.Inches(2)))));

        Assert.Equal(new Vector3(Length.Zero, Length.Zero, Length.Inches(2)), result.Changes.AppliedDelta);
        Assert.Equal(Point3.Inches(0, 0, 2), result.Sketch.Find<Box>(box)!.Anchor);
        Assert.Equal(builder.NodeOf(node), result.Sketch.Find<Node>(node));
        SketchAssert.IsConsistent(result.Sketch);
    }

    private static (SketchBuilder Builder, EntityId Top, EntityId Leg, RelationshipId Depth) TopOnALeg()
    {
        SketchBuilder builder = new();
        EntityId top = builder.AddBox(Point3.Inches(0, 0, 16), 40, 20, 1, name: "Top");
        EntityId leg = builder.AddBox(Point3.Origin, 2, 2, 16, name: "Leg");
        builder.FlushFaces(top, BoxFace.Bottom, leg, BoxFace.Top);
        RelationshipId depth = builder.DepthIs(leg, Length.Inches(16));
        SketchAssert.IsConsistent(builder.Sketch);
        return (builder, top, leg, depth);
    }
}

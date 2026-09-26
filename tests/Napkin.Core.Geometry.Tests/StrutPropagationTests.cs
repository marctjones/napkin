namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The strut in the relationship system (#190): the per-end rule of assembly-model §3a.5, the
/// requests on a strut, and a flush to a one-way leg's face with its two-end coupling
/// (angled-parts §3.2). assembly-model §9 cases 29, 32 and 33; angled-parts §9.3 cases 8 and 9.
/// Every expected coordinate is worked out by hand in the comment beside it.
/// </summary>
public class StrutPropagationTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    private static RelationshipId NewId() => new(Guid.NewGuid());

    private static Sketch Apply(Sketch sketch, Request request)
        => Assert.IsAssignableFrom<Succeeded>(Updater.Apply(sketch, request)).Sketch;

    private static Sketch Build(Sketch sketch, params Request[] requests)
        => requests.Aggregate(sketch, Apply);

    private static Point3 At(long x, long y, long z) => new(new Length(x), new Length(y), new Length(z));

    private static Box Standing(EntityId id, Point3 anchor, long width, long height, long depth)
        => new(id, Layer.Default.Id, anchor, new Length(width), new Length(height), new Length(depth), BoxFace.Top, Angle.Zero);

    private static Strut Member(EntityId id, Point3 from, Point3 to, EndCut fromCut, EndCut toCut, long height = 3584, long depth = 1536)
        => new(id, Layer.Default.Id, from, to, fromCut, toCut, Axis.Z, new Length(height), new Length(depth));

    private static FeatureRef Face(EntityId box, BoxFace face) => new(box, BoxFeature.Face(face));

    // ---- The knee brace: assembly-model §9 case 29 ----
    //
    // A post 3½″ square and 36″ tall, anchored at the origin; a beam flush on its top. A brace from
    // the post's east face 24″ up to the beam's underside 12″ east of the post: From = (3584, 1792,
    // 24576), To = (15872, 1792, 36864), d = (12288, 0, 12288), 45°. Its foot is held to the post
    // (east face, south face, bottom face); its top to the beam's underside in Z and the post in X, Y.

    private static readonly EntityId Post = EntityId.New();
    private static readonly EntityId Beam = EntityId.New();
    private static readonly EntityId Brace = EntityId.New();
    private static readonly RelationshipId PostHeight = NewId();
    private static readonly RelationshipId PostHeld = NewId();

    private static Sketch KneeBrace()
        => Build(
            Sketch.Empty,
            new AddEntity(Standing(Post, Point3.Origin, 3584, 3584, 36864)),
            new AddEntity(Standing(Beam, At(0, 0, 36864), 36864, 3584, 5632)),
            new AddEntity(Member(Brace, At(3584, 1792, 24576), At(15872, 1792, 36864), EndCut.X, EndCut.Z)),
            new AddRelationship(new Anchored(PostHeld, Post)),
            new AddRelationship(new ParamValue(PostHeight, new BoxDepthRef(Post), new Length(36864))),
            new AddRelationship(new Flush(NewId(), Face(Beam, BoxFace.Bottom), Face(Post, BoxFace.Top))),
            new AddRelationship(new AxisDistance(NewId(), Face(Post, BoxFace.East), new StrutEndRef(Brace, StrutEnd.From), Axis.X, Length.Zero)),
            new AddRelationship(new AxisDistance(NewId(), Face(Post, BoxFace.South), new StrutEndRef(Brace, StrutEnd.From), Axis.Y, new Length(1792))),
            new AddRelationship(new AxisDistance(NewId(), Face(Post, BoxFace.Bottom), new StrutEndRef(Brace, StrutEnd.From), Axis.Z, new Length(24576))),
            new AddRelationship(new AxisDistance(NewId(), Face(Beam, BoxFace.Bottom), new StrutEndRef(Brace, StrutEnd.To), Axis.Z, Length.Zero)),
            new AddRelationship(new AxisDistance(NewId(), Face(Post, BoxFace.East), new StrutEndRef(Brace, StrutEnd.To), Axis.X, new Length(12288))),
            new AddRelationship(new AxisDistance(NewId(), Face(Post, BoxFace.South), new StrutEndRef(Brace, StrutEnd.To), Axis.Y, new Length(1792))));

    [Fact]
    public void AnEndHeldToAPartFollowsItAndTheOtherEndStays()
    {
        Sketch sketch = KneeBrace();
        Strut before = sketch.Find<Strut>(Brace)!;

        // The post grows 6″: its top, the beam and the brace's top rise 6144; its foot, held 24″
        // above the post's bottom, does not move. The brace lengthens: d = (12288, 0, 18432).
        Solved solved = Assert.IsType<Solved>(Updater.Apply(sketch, new SetParameter(PostHeight, new Length(43008))));
        Strut after = solved.Sketch.Find<Strut>(Brace)!;

        Assert.Equal(before.From, after.From);
        Assert.Equal(At(15872, 1792, 43008), after.To);
        Assert.Contains(Brace, solved.Changes.Moved);
        Assert.True(after.Blank().Length.Value > before.Blank().Length.Value);
        Assert.True(RelationshipChecker.Check(solved.Sketch).AllHold);
    }

    [Fact]
    public void ADragReachingAnAnchoredPartThroughAnEndGoesNowhere()
    {
        Solved solved = Assert.IsType<Solved>(Updater.Apply(KneeBrace(), new Drag(Brace, new Vector3(Length.Zero, Length.Zero, new Length(1024)))));

        Assert.Equal(Vector3.Zero, solved.Changes.AppliedDelta);
    }

    [Fact]
    public void AFreeStrutDragsAsAUnit()
    {
        Strut free = Member(Brace, At(0, 0, 0), At(3072, 0, 4096), EndCut.Z, EndCut.Z);
        Sketch sketch = Apply(Sketch.Empty, new AddEntity(free));
        Vector3 delta = new(new Length(1024), new Length(2048), new Length(512));

        Solved solved = Assert.IsType<Solved>(Updater.Apply(sketch, new Drag(Brace, delta)));
        Strut moved = solved.Sketch.Find<Strut>(Brace)!;

        Assert.Equal(delta, solved.Changes.AppliedDelta);
        Assert.Equal((free.From + delta, free.To + delta), (moved.From, moved.To));

        // Case 32: a translation changes no d, so the board is the same board.
        Assert.Equal(free.Blank(), moved.Blank());
    }

    [Fact]
    public void SettingAnEndHeldToAnAnchoredPartIsAContradictionNamingIt()
    {
        Sketch sketch = KneeBrace();

        OverConstrained conflict = Assert.IsType<OverConstrained>(Updater.Apply(sketch, new SetStrutEnd(Brace, StrutEnd.From, At(4096, 1792, 24576))));

        Assert.Contains(PostHeld, conflict.Conflict.Relationships);
        Assert.Contains(Brace, conflict.Conflict.Entities);
    }

    [Fact]
    public void AnAnchoredStrutRefusesToHaveAnEndSet()
    {
        Strut free = Member(Brace, At(0, 0, 0), At(3072, 0, 4096), EndCut.Z, EndCut.Z);
        RelationshipId held = NewId();
        Sketch sketch = Build(Sketch.Empty, new AddEntity(free), new AddRelationship(new Anchored(held, Brace)));

        OverConstrained conflict = Assert.IsType<OverConstrained>(Updater.Apply(sketch, new SetStrutEnd(Brace, StrutEnd.To, At(3072, 0, 5120))));

        Assert.Contains(held, conflict.Conflict.Relationships);
    }

    [Fact]
    public void SettingAFreeEndMovesThatEndOnly()
    {
        Strut free = Member(Brace, At(0, 0, 0), At(3072, 0, 4096), EndCut.Z, EndCut.Z);
        Sketch sketch = Apply(Sketch.Empty, new AddEntity(free));

        Solved solved = Assert.IsType<Solved>(Updater.Apply(sketch, new SetStrutEnd(Brace, StrutEnd.To, At(3072, 1024, 4096))));

        Assert.Equal(free with { To = At(3072, 1024, 4096) }, solved.Sketch.Find<Strut>(Brace));
        Assert.Equal(new[] { Brace }, solved.Changes.Moved);
    }

    [Fact]
    public void SettingAnEndWhereTheStrutWouldStopLeaningIsRefusedByName()
    {
        Strut free = Member(Brace, At(0, 0, 0), At(3072, 0, 4096), EndCut.Z, EndCut.Z);
        Sketch sketch = Apply(Sketch.Empty, new AddEntity(free));

        Rejected upright = Assert.IsType<Rejected>(Updater.Apply(sketch, new SetStrutEnd(Brace, StrutEnd.To, At(0, 0, 4096))));
        Assert.Equal(RejectionReason.StrutIsAxisAligned, upright.Reason);

        // A 2x4 between a floor and a wall, 45° over 2″ each way: its mitres cross (case 28).
        Strut wall = Member(Brace, At(0, 0, 0), At(0, 8192, 8192), EndCut.Z, EndCut.Y);
        Sketch walled = Apply(Sketch.Empty, new AddEntity(wall));
        Rejected stub = Assert.IsType<Rejected>(Updater.Apply(walled, new SetStrutEnd(Brace, StrutEnd.To, At(0, 2048, 2048))));
        Assert.Equal(RejectionReason.StrutTooShortForItsCuts, stub.Reason);
    }

    [Fact]
    public void ATypedCrossSectionResizesTheStrutAndMovesNeitherEnd()
    {
        Strut free = Member(Brace, At(0, 0, 0), At(3072, 0, 4096), EndCut.Z, EndCut.Z);
        RelationshipId height = NewId();
        Sketch sketch = Build(
            Sketch.Empty,
            new AddEntity(free),
            new AddRelationship(new Anchored(NewId(), Brace)),
            new AddRelationship(new ParamValue(height, new StrutHeightRef(Brace), new Length(3584))));

        Solved solved = Assert.IsType<Solved>(Updater.Apply(sketch, new SetParameter(height, new Length(5632))));
        Strut resized = solved.Sketch.Find<Strut>(Brace)!;

        Assert.Equal(free with { Height = new Length(5632) }, resized);
        Assert.Equal(new[] { Brace }, solved.Changes.Resized);
        Assert.Empty(solved.Changes.Moved);
    }

    [Fact]
    public void ACrossSectionFollowsAnEqualSize()
    {
        // A leg's depth equal to a rail's height: both the 1½″ of a 2x.
        Strut free = Member(Brace, At(0, 0, 0), At(3072, 0, 4096), EndCut.Z, EndCut.Z);
        Box rail = Standing(Beam, At(10240, 0, 0), 36864, 1536, 3584);
        RelationshipId railHeight = NewId();
        Sketch sketch = Build(
            Sketch.Empty,
            new AddEntity(free),
            new AddEntity(rail),
            new AddRelationship(new ParamValue(railHeight, new BoxHeightRef(Beam), new Length(1536))),
            new AddRelationship(new EqualParam(NewId(), new BoxHeightRef(Beam), new StrutDepthRef(Brace))));

        Sketch after = Apply(sketch, new SetParameter(railHeight, new Length(2560)));

        Assert.Equal(new Length(2560), after.Find<Strut>(Brace)!.Depth);
    }

    [Fact]
    public void BoxOnlyRequestsOnAStrutAreNotSupported()
    {
        Sketch sketch = Apply(Sketch.Empty, new AddEntity(Member(Brace, At(0, 0, 0), At(3072, 0, 4096), EndCut.Z, EndCut.Z)));

        foreach (Request request in new Request[]
                 {
                     new SetPosition(Brace, Point3.Origin),
                     new SetOrientation(Brace, BoxFace.North, Angle.Zero),
                     new DragFace(Brace, BoxFace.East, new Length(1024)),
                     new SetCut(Brace, new CornerCut(BoxCorner.SouthWest, new Length(10), new Length(10))),
                     new RemoveCut(Brace, CutSite.Corner(BoxCorner.SouthWest)),
                 })
        {
            Assert.Equal(RejectionReason.UnsupportedRequest, Assert.IsType<Rejected>(Updater.Apply(sketch, request)).Reason);
        }
    }

    [Fact]
    public void AStrutEndRequestNamesWhatIsNotAStrut()
    {
        Sketch sketch = Apply(Sketch.Empty, new AddEntity(Standing(Post, Point3.Origin, 3584, 3584, 36864)));

        Assert.Equal(RejectionReason.DanglingReference, Assert.IsType<Rejected>(Updater.Apply(sketch, new SetStrutEnd(Post, StrutEnd.From, Point3.Origin))).Reason);
        Assert.Equal(RejectionReason.UnknownEntity, Assert.IsType<Rejected>(Updater.Apply(sketch, new SetStrutEnd(Brace, StrutEnd.From, Point3.Origin))).Reason);
        Assert.Equal(RejectionReason.DanglingReference, Assert.IsType<Rejected>(Updater.Apply(sketch, new DragStrutEnd(Post, StrutEnd.From, Vector3.Zero))).Reason);
        Assert.Equal(RejectionReason.UnknownEntity, Assert.IsType<Rejected>(Updater.Apply(sketch, new DragStrutEnd(Brace, StrutEnd.From, Vector3.Zero))).Reason);
    }

    [Fact]
    public void DraggingAnEndMovesItAsFarAsItsRelationshipsAllowPerAxis()
    {
        // The brace's top is held in X and Z; in Y it is held only to the post's south face, which
        // is anchored, so Y is refused too. Its foot's X, held to the anchored post, goes nowhere;
        // a free strut's end goes all the way.
        Solved held = Assert.IsType<Solved>(Updater.Apply(KneeBrace(), new DragStrutEnd(Brace, StrutEnd.To, new Vector3(new Length(1024), new Length(1024), new Length(1024)))));
        Assert.Equal(Vector3.Zero, held.Changes.AppliedDelta);

        Strut free = Member(Brace, At(0, 0, 0), At(3072, 0, 4096), EndCut.Z, EndCut.Z);
        Sketch sketch = Apply(Sketch.Empty, new AddEntity(free));
        Vector3 delta = new(new Length(512), new Length(256), Length.Zero);
        Solved moved = Assert.IsType<Solved>(Updater.Apply(sketch, new DragStrutEnd(Brace, StrutEnd.To, delta)));
        Assert.Equal(delta, moved.Changes.AppliedDelta);
        Assert.Equal(free.To + delta, moved.Sketch.Find<Strut>(Brace)!.To);

        // Straight over the foot the strut would stand on its end: refused, so that axis goes nowhere.
        Solved blocked = Assert.IsType<Solved>(Updater.Apply(sketch, new DragStrutEnd(Brace, StrutEnd.To, new Vector3(new Length(-3072), Length.Zero, Length.Zero))));
        Assert.Equal(Vector3.Zero, blocked.Changes.AppliedDelta);
    }

    // ---- A flush to a one-way leg's face: angled-parts §9.3 cases 8 and 9 ----
    //
    // The splayed bench's leg (§9.1): From = (4096, −4096, 0), To = (4096, 3072, 24576), d = (0, 7168,
    // 24576), a 2x2, reference Z. Its local Z is d × Z = (7168, 0, 0), world +X, so its Top face is
    // x = 4096 + 768 = 4864 and its Bottom face x = 3328. A rail 12″ long, anchored, lies west of the
    // leg with its east face flush to the leg's bottom face: anchor x = 3328 − 12288 = −8960.

    private static readonly EntityId Leg = EntityId.New();
    private static readonly EntityId Rail = EntityId.New();
    private static readonly RelationshipId RailWidth = NewId();
    private static readonly RelationshipId RailHeld = NewId();
    private static readonly RelationshipId LegFlush = NewId();

    private static Strut BenchLeg(long depth = 1536)
        => Member(Leg, At(4096, -4096, 0), At(4096, 3072, 24576), EndCut.Z, EndCut.Z, height: 1536, depth: depth);

    private static Sketch Bench()
        => Build(
            Sketch.Empty,
            new AddEntity(BenchLeg()),
            new AddEntity(Standing(Rail, At(-8960, 0, 12288), 12288, 768, 3584)),
            new AddRelationship(new Anchored(RailHeld, Rail)),
            new AddRelationship(new ParamValue(RailWidth, new BoxWidthRef(Rail), new Length(12288))),
            new AddRelationship(new Flush(LegFlush, Face(Rail, BoxFace.East), new StrutFaceRef(Leg, StrutFace.Bottom))));

    [Fact]
    public void AOneWayLegsFaceIsAPlaneOnTheAxisItDoesNotLeanAlong()
    {
        Sketch sketch = Sketch.Empty.WithEntity(BenchLeg());

        Assert.Equal(Place.On(Axis.X, new Length(4864)), sketch.PlaceOf(new StrutFaceRef(Leg, StrutFace.Top)));
        Assert.Equal(Place.On(Axis.X, new Length(3328)), sketch.PlaceOf(new StrutFaceRef(Leg, StrutFace.Bottom)));
        Assert.Equal(default, sketch.PlaceOf(new StrutFaceRef(Leg, StrutFace.South)));
    }

    [Fact]
    public void WideningTheRailCarriesBothEndsOfTheLegAlongX()
    {
        // Case 8: the rail's east face moves 1024 east; the leg's bottom face follows, so both ends
        // go from x = 4096 to 5120. Y and Z are untouched and the board is the same board.
        Sketch sketch = Bench();

        Solved solved = Assert.IsType<Solved>(Updater.Apply(sketch, new SetParameter(RailWidth, new Length(13312))));
        Strut leg = solved.Sketch.Find<Strut>(Leg)!;

        Assert.Equal((At(5120, -4096, 0), At(5120, 3072, 24576)), (leg.From, leg.To));
        Assert.Equal(BenchLeg().Blank(), leg.Blank());
        Assert.True(RelationshipChecker.Check(solved.Sketch).AllHold);
    }

    [Fact]
    public void MovingOneEndOffTheFaceAxisIsAContradictionNamingTheFlush()
    {
        // Case 9: the lean would stop being one-way.
        OverConstrained conflict = Assert.IsType<OverConstrained>(Updater.Apply(Bench(), new SetStrutEnd(Leg, StrutEnd.From, At(5120, -4096, 0))));

        Assert.Contains(LegFlush, conflict.Conflict.Relationships);
    }

    [Fact]
    public void WithTheRailFreeMovingOneEndCarriesTheOtherAndTheRail()
    {
        // The flush holds both ends equal in X, and a free rail follows the face.
        Sketch sketch = Apply(Bench(), new RemoveRelationship(RailHeld));

        Solved solved = Assert.IsType<Solved>(Updater.Apply(sketch, new SetStrutEnd(Leg, StrutEnd.From, At(5120, -4096, 0))));

        Assert.Equal(At(5120, 3072, 24576), solved.Sketch.Find<Strut>(Leg)!.To);
        Assert.Equal(new Length(-7936), solved.Sketch.Find<Box>(Rail)!.Anchor.X);
    }

    [Fact]
    public void WithTheRailFreeMovingTheOtherEndDoesTheSame()
    {
        // The mirror: the face's value is read from one end, so the coupling must settle the other
        // end first or the flush would see the rail and the face disagree on a second pass.
        Sketch sketch = Apply(Bench(), new RemoveRelationship(RailHeld));

        Solved solved = Assert.IsType<Solved>(Updater.Apply(sketch, new SetStrutEnd(Leg, StrutEnd.To, At(5120, 3072, 24576))));

        Assert.Equal(At(5120, -4096, 0), solved.Sketch.Find<Strut>(Leg)!.From);
        Assert.Equal(new Length(-7936), solved.Sketch.Find<Box>(Rail)!.Anchor.X);
    }

    [Fact]
    public void AFaceThatIsSquareToNothingIsRefusedByName()
    {
        Sketch sketch = Apply(Sketch.Empty, new AddEntity(BenchLeg()))
            .WithEntity(Standing(Rail, At(-8960, 0, 12288), 12288, 768, 3584));

        Rejected tilted = Assert.IsType<Rejected>(Updater.Apply(sketch, new AddRelationship(new Flush(NewId(), Face(Rail, BoxFace.North), new StrutFaceRef(Leg, StrutFace.South)))));
        Assert.Equal(RejectionReason.PlacesNotComparable, tilted.Reason);
        Assert.Contains("tilts with the lean", tilted.Detail!.Message, StringComparison.Ordinal);

        // The footstool's leg leans two ways: none of its faces is square to anything.
        Strut stool = Member(Leg, At(0, -1024, 0), At(3072, 3072, 12288), EndCut.Z, EndCut.Z, height: 1536);
        Sketch stooled = Sketch.Empty.WithEntity(stool).WithEntity(Standing(Rail, At(-8960, 0, 12288), 12288, 768, 3584));
        foreach (StrutFace face in Enum.GetValues<StrutFace>())
        {
            Rejected refused = Assert.IsType<Rejected>(Updater.Apply(stooled, new AddRelationship(new Flush(NewId(), Face(Rail, BoxFace.East), new StrutFaceRef(Leg, face)))));
            Assert.Contains("leans two ways", refused.Detail!.Message, StringComparison.Ordinal);
        }

        // An odd size puts the face half a unit off the grid.
        Sketch odd = Sketch.Empty.WithEntity(BenchLeg(depth: 1535)).WithEntity(Standing(Rail, At(-8960, 0, 12288), 12288, 768, 3584));
        Rejected half = Assert.IsType<Rejected>(Updater.Apply(odd, new AddRelationship(new Flush(NewId(), Face(Rail, BoxFace.East), new StrutFaceRef(Leg, StrutFace.Bottom)))));
        Assert.Contains("half a unit off the grid", half.Detail!.Message, StringComparison.Ordinal);

        // Only a flush holds a face.
        Rejected distance = Assert.IsType<Rejected>(Updater.Apply(sketch, new AddRelationship(new AxisDistance(NewId(), Face(Rail, BoxFace.East), new StrutFaceRef(Leg, StrutFace.Bottom), Axis.X, Length.Zero))));
        Assert.Contains("only a flush", distance.Detail!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACrossSectionMadeOddUnderAFlushIsRefused()
    {
        RelationshipId depth = NewId();
        Sketch sketch = Apply(Bench(), new AddRelationship(new ParamValue(depth, new StrutDepthRef(Leg), new Length(1536))));

        Rejected refused = Assert.IsType<Rejected>(Updater.Apply(sketch, new SetParameter(depth, new Length(1535))));

        Assert.Equal(RejectionReason.PlacesNotComparable, refused.Reason);
    }

    // ---- Dimensions between struts: assembly-model §9 case 33 ----

    [Fact]
    public void ADimensionBetweenTwoFeetLiesInThePlanAndOneUpALegDoesNot()
    {
        Strut one = BenchLeg();
        Strut two = Member(EntityId.New(), At(20480, -4096, 0), At(20480, 3072, 24576), EndCut.Z, EndCut.Z, height: 1536);
        Sketch sketch = Sketch.Empty.WithEntity(one).WithEntity(two);
        DimensionPlacement placement = new(Length.Inches(2), DimensionSide.North);

        Dimension across = new(EntityId.New(), Layer.Default.Id, new AxisMeasurand(new StrutEndRef(one.Id, StrutEnd.From), new StrutEndRef(two.Id, StrutEnd.From), Axis.X), null, placement);
        Dimension up = new(EntityId.New(), Layer.Default.Id, new AxisMeasurand(new StrutEndRef(one.Id, StrutEnd.From), new StrutEndRef(one.Id, StrutEnd.To), Axis.Z), null, placement);
        Dimension section = new(EntityId.New(), Layer.Default.Id, new ParamMeasurand(new StrutHeightRef(one.Id)), null, placement);

        Assert.IsType<Solved>(Updater.Apply(sketch, new AddEntity(across)));
        Assert.Equal(RejectionReason.UnsupportedRequest, Assert.IsType<Rejected>(Updater.Apply(sketch, new AddEntity(up))).Reason);
        Assert.Equal(RejectionReason.UnsupportedRequest, Assert.IsType<Rejected>(Updater.Apply(sketch, new AddEntity(section))).Reason);
    }

    // ---- Part and stock: invariant 16 through SetPart ----

    [Fact]
    public void AStrutTakesAPartWhoseDerivedDimensionIsItsLengthOrWidth()
    {
        Sketch sketch = Apply(Sketch.Empty, new AddEntity(BenchLeg()));

        Part legPart = new("2x2", null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width));
        Assert.Equal(legPart, Apply(sketch, new SetPart(Leg, legPart)).Find<Strut>(Leg)!.Part);

        Rejected thick = Assert.IsType<Rejected>(Updater.Apply(sketch, new SetPart(Leg, legPart with { PlanAxes = new PlanAxes(PartDimension.Thickness, PartDimension.Width) })));
        Assert.Equal(RejectionReason.InvalidStrut, thick.Reason);
    }
}

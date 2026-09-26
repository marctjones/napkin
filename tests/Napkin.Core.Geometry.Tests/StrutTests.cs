namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The strut, its integer frame and its derived blank (#189): assembly-model §9 cases 23–28 as
/// amended, and angled-parts §9.1–§9.3. Every expected number is the design's hand derivation.
/// </summary>
public class StrutTests
{
    private static Strut Leg(
        (long X, long Y, long Z) d,
        EndCut fromCut = EndCut.Z,
        EndCut toCut = EndCut.Z,
        Axis reference = Axis.Z,
        long height = 3584,
        long depth = 1536,
        (long X, long Y, long Z)? from = null)
    {
        (long X, long Y, long Z) f = from ?? (0, 0, 0);
        return new Strut(
            EntityId.New(),
            Layer.Default.Id,
            new Point3(new Length(f.X), new Length(f.Y), new Length(f.Z)),
            new Point3(new Length(f.X + d.X), new Length(f.Y + d.Y), new Length(f.Z + d.Z)),
            fromCut,
            toCut,
            reference,
            new Length(height),
            new Length(depth));
    }

    private static Strut Swapped(Strut strut) => strut with { From = strut.To, To = strut.From, FromCut = strut.ToCut, ToCut = strut.FromCut };

    private static ValidationResult Validated(Strut strut) => Sketch.Empty.WithEntity(strut).Validate();

    private static DerivedCut Plain(BoxCorner corner, long setback, bool exact, long height = 3584)
        => new(new CornerCut(corner, new Length(setback), new Length(height)), exact);

    // ---- The frame (assembly-model §9 case 23) ----

    [Fact]
    public void TheFrameIsIntegerAndRightHanded()
    {
        StrutFrame frame = Leg((6144, 9216, 27648)).Frame();

        Assert.Equal(new IntegerVector3(6144, 9216, 27648), frame.D);
        Assert.Equal(new IntegerVector3(9216, -6144, 0), frame.Z);
        Assert.True(frame.Y.Z > 0);
        Assert.Equal(0, frame.Y.Dot(frame.D));
        Assert.Equal(0, frame.Z.Dot(frame.D));
        Assert.True(frame.D.Cross(frame.Y).Dot(frame.Z) > 0);
        Assert.False(frame.Reversed);
    }

    [Fact]
    public void SwappingTheEndsGivesTheSameFrame()
    {
        Strut leg = Leg((6144, 9216, 27648), from: (100, 200, 0));
        StrutFrame swapped = Swapped(leg).Frame();

        Assert.True(swapped.Reversed);
        Assert.Equal(leg.Frame() with { Reversed = true }, swapped);
    }

    [Fact]
    public void AStrutSquareToItsReferencePutsItsLowerEndFirst()
    {
        // d · X = 0 says nothing; the lower end comes first either way round.
        Strut leg = Leg((0, 7168, 24576), reference: Axis.X);

        Assert.False(leg.Frame().Reversed);
        Assert.True(Swapped(leg).Frame().Reversed);
        Assert.Equal(new IntegerVector3(0, 24576, -7168), leg.Frame().Z);
    }

    [Fact]
    public void AnAxisAlignedDirectionHasNoFrame()
        => Assert.Throws<ArgumentException>(() => Leg((0, 0, 27648)).Frame());

    // ---- Invariants 14–17 ----

    [Theory]
    [InlineData(0, 0, 27648)]
    [InlineData(6144, 0, 0)]
    [InlineData(0, 0, 0)]
    public void AnAxisAlignedStrutIsRefused(long x, long y, long z)
        => Assert.Contains(Validated(Leg((x, y, z), EndCut.Square, EndCut.Square)).Errors, e => e.Kind == ValidationErrorKind.StrutIsAxisAligned);

    [Fact]
    public void ASizeOfZeroIsRefused()
    {
        Assert.Contains(Validated(Leg((6144, 9216, 27648), height: 0)).Errors, e => e.Kind == ValidationErrorKind.NonPositiveSize);
        Assert.Contains(Validated(Leg((6144, 9216, 27648), depth: -1)).Errors, e => e.Kind == ValidationErrorKind.NonPositiveSize);
    }

    [Fact]
    public void APartWhoseDerivedDimensionIsItsThicknessIsRefused()
    {
        Strut leg = Leg((6144, 9216, 27648)) with { Part = new Part("2x4", null, 1, new PlanAxes(PartDimension.Thickness, PartDimension.Width)) };

        Assert.Contains(Validated(leg).Errors, e => e.Kind == ValidationErrorKind.StrutLengthIsThickness);
    }

    [Theory]
    [InlineData(PartDimension.Length, PartDimension.Width)]
    [InlineData(PartDimension.Width, PartDimension.Thickness)]
    public void APartMayCallItsDerivedDimensionLengthOrWidth(PartDimension x, PartDimension y)
    {
        Strut leg = Leg((6144, 9216, 27648)) with { Part = new Part("2x4", null, 1, new PlanAxes(x, y)) };

        Assert.True(Validated(leg).IsValid, Validated(leg).ToString());
    }

    [Fact]
    public void AnEndCutAlongTheStrutIsRefused()
    {
        ValidationResult result = Validated(Leg((0, 9216, 27648), EndCut.Z, EndCut.X));

        Assert.Contains(result.Errors, e => e.Kind == ValidationErrorKind.StrutCutAlongItself);
        Assert.Null(Strut.CutAlongItself(Leg((0, 9216, 27648), EndCut.Z, EndCut.Y)));
    }

    [Theory]
    [InlineData(EndCut.Square, EndCut.Square)]
    [InlineData(EndCut.Z, EndCut.Z)]
    [InlineData(EndCut.Z, EndCut.Square)]
    [InlineData(EndCut.X, EndCut.Y)]
    public void AnyPairOfCutsIsAcceptedOnALeaningStrut(EndCut fromCut, EndCut toCut)
        => Assert.True(Validated(Leg((6144, 9216, 27648), fromCut, toCut)).IsValid);

    [Fact]
    public void CoplanarityIsRetiredAndOneBlankMayHoldBothKindsOfEnd()
    {
        // angled-parts §9.3 case 3: assembly-model case 24's StrutNeedsBevel, now accepted.
        Strut leg = Leg((1024, 9216, 27648), EndCut.Z, EndCut.Y);
        StrutBlank blank = leg.Blank();

        Assert.True(Validated(leg).IsValid);
        DerivedCut plain = Assert.Single(blank.Cuts);
        Assert.Equal(BoxCorner.SouthWest, ((CornerCut)plain.Cut).Corner);
        Assert.Equal(BlankEnd.East, Assert.Single(blank.CompoundEnds).End);
        Assert.False(blank.Length.Exact);
    }

    [Fact]
    public void ATrapezoidTooShortForItsMitresIsRefused()
    {
        // Assembly-model case 28: a 2x4 at 45° spanning 2″ each way, floor to wall. L = 6480, and
        // the two mitres claim 7168 of its south edge.
        Strut leg = Leg((0, 2048, 2048), EndCut.Z, EndCut.Y);

        Assert.Equal(new DerivedLength(new Length(6480), false), leg.Blank().Length);
        Assert.Contains(Validated(leg).Errors, e => e.Kind == ValidationErrorKind.StrutTooShortForItsCuts);
    }

    [Fact]
    public void ACompoundBlankTooShortForItsEndsIsRefused()
    {
        // A floor-to-wall 2x4 leaning 1″ east as well: its wall end is compound, and at 2″ each way
        // the two ends cross on one long edge. Ten times the span leaves room.
        Strut stub = Leg((1024, 2048, 2048), EndCut.Z, EndCut.Y);

        Assert.NotEmpty(stub.Blank().CompoundEnds);
        Assert.Contains(Validated(stub).Errors, e => e.Kind == ValidationErrorKind.StrutTooShortForItsCuts);
        Assert.True(Validated(Leg((1024, 20480, 20480), EndCut.Z, EndCut.Y)).IsValid);
    }

    // ---- The blank: plain mitres (assembly-model §9 cases 25–28) ----

    [Fact]
    public void TheSawhorseLegIsAParallelogramRoundedOnce()
    {
        // Case 25: |d|² = 887 095 296 is not a square; s ≈ 1435.81; L ≈ 31 219.956.
        StrutBlank blank = Leg((6144, 9216, 27648)).Blank();

        Assert.Equal(new DerivedLength(new Length(31220), false), blank.Length);
        Assert.Equal([Plain(BoxCorner.SouthWest, 1436, false), Plain(BoxCorner.NorthEast, 1436, false)], blank.Cuts);
        Assert.Empty(blank.CompoundEnds);
    }

    [Fact]
    public void MirroredAndSwappedLegsDeriveTheSameBlank()
    {
        Strut leg = Leg((6144, 9216, 27648));
        StrutBlank blank = leg.Blank();

        Assert.Equal(blank, Leg((6144, -9216, 27648)).Blank());
        Assert.Equal(blank, Leg((-6144, 9216, 27648)).Blank());
        Assert.Equal(blank, Swapped(leg).Blank());
        Assert.Equal(blank.GetHashCode(), Swapped(leg).Blank().GetHashCode());
    }

    [Fact]
    public void TheLengthIsRoundedOnceNotSummedFromRoundedParts()
    {
        // Case 26: round(c) + round(s) would be 12 331 + 299 = 12 630.
        Assert.Equal(new Length(12629), Leg((1024, 0, 12288)).Blank().Length.Value);
    }

    [Fact]
    public void AThreeFourFiveLegIsProvenExact()
    {
        StrutBlank blank = Leg((3072, 0, 4096)).Blank();

        Assert.Equal(new DerivedLength(new Length(7808), true), blank.Length);
        Assert.Equal([Plain(BoxCorner.SouthWest, 2688, true), Plain(BoxCorner.NorthEast, 2688, true)], blank.Cuts);
    }

    [Fact]
    public void ExactnessOfAPartDoesNotLeakToTheWhole()
    {
        // c = 14 336 is exact; s ≈ 2153.72 is not.
        StrutBlank blank = Leg((4096, 6144, 12288)).Blank();

        Assert.Equal(new DerivedLength(new Length(16490), false), blank.Length);
        Assert.All(blank.Cuts, cut => Assert.False(cut.Exact));
    }

    [Fact]
    public void PerpendicularCutsMakeATrapezoid()
    {
        StrutBlank blank = Leg((0, 9216, 27648), EndCut.Z, EndCut.Y).Blank();

        Assert.Equal([Plain(BoxCorner.SouthWest, 1195, false), Plain(BoxCorner.SouthEast, 10752, true)], blank.Cuts);
        Assert.Equal(blank, Leg((0, -9216, 27648), EndCut.Z, EndCut.Y).Blank());
    }

    [Fact]
    public void OneCutAndOneSquareEndReachesHalfASetback()
    {
        StrutBlank blank = Leg((3072, 0, 4096), EndCut.Z, EndCut.Square).Blank();

        Assert.Equal(new DerivedLength(new Length(5120 + 1344), true), blank.Length);
        Assert.Equal([Plain(BoxCorner.SouthWest, 2688, true)], blank.Cuts);
    }

    [Fact]
    public void AnOddSetbackSumLeavesTheLengthOffTheGrid()
    {
        // s = 1536 · 3 / 4 = 1152 exact at the Z end; c = 5120 exact; one Z end, s/2 = 576: exact.
        // Height 1537 makes s = 1152.75: not exact, and so neither is L.
        Assert.True(Leg((3072, 0, 4096), EndCut.Z, EndCut.Square, height: 1536).Blank().Length.Exact);
        Assert.False(Leg((3072, 0, 4096), EndCut.Z, EndCut.Square, height: 1537).Blank().Length.Exact);

        // s = 3 · 3 / 4 exact only when the height is a multiple of four; 4 gives s = 3, odd, so L
        // is c + 3/2 — on no grid.
        Assert.False(Leg((3072, 0, 4096), EndCut.Z, EndCut.Square, height: 4).Blank().Length.Exact);
    }

    [Fact]
    public void ASquareEndedBraceIsItsCentreline()
    {
        StrutBlank blank = Leg((3072, 0, 4096), EndCut.Square, EndCut.Square).Blank();

        Assert.Equal(new DerivedLength(new Length(5120), true), blank.Length);
        Assert.Empty(blank.Cuts);
        Assert.Empty(blank.CompoundEnds);
    }

    [Fact]
    public void ASetbackThatRoundsToNothingIsNoCut()
    {
        StrutBlank blank = Leg((1, 0, 27648), height: 1536).Blank();

        Assert.Empty(blank.Cuts);
        Assert.False(blank.Length.Exact);
    }

    // ---- The worked examples (angled-parts §9.1, §9.2, §9.3 case 1) ----

    [Fact]
    public void TheSplayedBenchLegIsExactThroughout()
    {
        StrutBlank blank = Leg((0, 7168, 24576), height: 1536).Blank();

        Assert.Equal(new DerivedLength(new Length(26048), true), blank.Length);
        Assert.Equal([Plain(BoxCorner.SouthWest, 448, true, 1536), Plain(BoxCorner.NorthEast, 448, true, 1536)], blank.Cuts);
    }

    [Fact]
    public void TheBenchLegWithItsWideFaceTurnedIsTheSameBoard()
    {
        // Reference X: the Z cut is a bevel on the narrow face, mitre 0; the same long-point length.
        StrutBlank blank = Leg((0, 7168, 24576), reference: Axis.X, height: 1536).Blank();

        Assert.Equal(new Length(26048), blank.Length.Value);
        Assert.Empty(blank.Cuts);
        DerivedCompoundEnd west = blank.CompoundEnds[0];
        Assert.Equal(new DerivedAngle(0, true), west.Mitre);
        Assert.Equal(16.5, west.Bevel.Shown);
        Assert.False(west.Bevel.Exact);
        Assert.Equal(new StrutCorner(0, -1), west.LongPoint);
    }

    [Fact]
    public void TheFootstoolLegWithAVerticalWideFaceHasPlainMitres()
    {
        StrutBlank blank = Leg((3072, 4096, 12288), height: 1536).Blank();

        Assert.Equal(new DerivedLength(new Length(13952), true), blank.Length);
        Assert.Equal([Plain(BoxCorner.SouthWest, 640, true, 1536), Plain(BoxCorner.NorthEast, 640, true, 1536)], blank.Cuts);
    }

    [Fact]
    public void TheFootstoolLegParallelToTheLongSideIsCompound()
    {
        // §9.2 board 2, and the operation-order test: 14 202.497 must round to 14 202.
        StrutBlank blank = Leg((3072, 4096, 12288), reference: Axis.X, height: 1536).Blank();

        Assert.Equal(new DerivedLength(new Length(14202), false), blank.Length);
        Assert.Empty(blank.Cuts);
        Assert.Equal(2, blank.CompoundEnds.Count);
        DerivedCompoundEnd foot = blank.CompoundEnds[0], seat = blank.CompoundEnds[1];
        Assert.Equal((BlankEnd.West, BlankEnd.East), (foot.End, seat.End));
        Assert.Equal(13.342363797, foot.Mitre.Degrees, 6);
        Assert.Equal(18.434948823, foot.Bevel.Degrees, 6);
        Assert.Equal((13.5, 18.5), (foot.Mitre.Shown, foot.Bevel.Shown));
        Assert.False(foot.Mitre.Exact || foot.Bevel.Exact);
        Assert.Equal(new StrutCorner(-1, -1), foot.LongPoint);
        Assert.Equal(new StrutCorner(1, 1), seat.LongPoint);
    }

    [Fact]
    public void TheFootstoolLegParallelToTheShortSideIsAThirdBoard()
    {
        StrutBlank blank = Leg((3072, 4096, 12288), reference: Axis.Y, height: 1536).Blank();

        Assert.Equal(new Length(14212), blank.Length.Value);
        Assert.Equal((18.0, 14.0), (blank.CompoundEnds[0].Mitre.Shown, blank.CompoundEnds[0].Bevel.Shown));
    }

    [Fact]
    public void MirroredCompoundLegsHaveTheirLongPointsTurned()
    {
        // §9.2's four legs, reference X. Mirroring across X reverses the frame (d · X < 0), so the
        // west end of that blank is its seat and it derives the same board as its twin; mirroring
        // across Y turns the long points over. Grouping the two boards as one is the cut list's (§2.5, slice C).
        (StrutCorner West, StrutCorner East, bool Reversed) Ends((long X, long Y, long Z) d)
        {
            Strut leg = Leg(d, reference: Axis.X, height: 1536);
            StrutBlank blank = leg.Blank();
            return (blank.CompoundEnds[0].LongPoint, blank.CompoundEnds[1].LongPoint, leg.Frame().Reversed);
        }

        (StrutCorner, StrutCorner, bool) southBottom = (new(-1, -1), new(1, 1), false);
        (StrutCorner, StrutCorner, bool) southTop = (new(-1, 1), new(1, -1), false);
        Assert.Equal(southBottom, Ends((3072, 4096, 12288)));
        Assert.Equal(southTop, Ends((3072, -4096, 12288)));
        Assert.Equal(southBottom with { Item3 = true }, Ends((-3072, 4096, 12288)));
        Assert.Equal(southTop with { Item3 = true }, Ends((-3072, -4096, 12288)));
        Assert.Equal(Leg((3072, 4096, 12288), reference: Axis.X, height: 1536).Blank(), Leg((-3072, 4096, 12288), reference: Axis.X, height: 1536).Blank());
    }

    [Fact]
    public void AMitreIsProvenExactAtFortyFiveDegreesAndABevelNeverIs()
    {
        // d = (5, 3, 4), reference X, a Z cut: (n·y)² = (n·d)²·|z|², a 45° mitre, proven in integers.
        DerivedCompoundEnd exact = Assert.Single(Leg((5120, 3072, 4096), EndCut.Z, EndCut.Square, Axis.X, height: 1536).Blank().CompoundEnds);
        Assert.True(exact.Mitre.Exact);
        Assert.Equal(45, exact.Mitre.Degrees, 9);

        // d = (0, 1, 1), reference X, a Y cut: mitre 0, exact; bevel 45°, never marked exact.
        DerivedCompoundEnd bevel = Assert.Single(Leg((0, 1024, 1024), EndCut.Y, EndCut.Square, Axis.X, height: 1536).Blank().CompoundEnds);
        Assert.Equal(new DerivedAngle(0, true), bevel.Mitre);
        Assert.Equal(45.0, bevel.Bevel.Shown);
        Assert.False(bevel.Bevel.Exact);

        // Any other mitre is marked, whatever it shows.
        Assert.False(Leg((3072, 4096, 12288), reference: Axis.X).Blank().CompoundEnds[0].Mitre.Exact);
    }

    [Theory]
    [InlineData(16.2602, 16.5)]
    [InlineData(16.25, 16.5)]
    [InlineData(16.24, 16.0)]
    [InlineData(22.6199, 22.5)]
    [InlineData(0, 0)]
    public void AnAngleIsShownToTheNearestHalfDegreeAwayFromZero(double degrees, double shown)
        => Assert.Equal(shown, new DerivedAngle(degrees, false).Shown);

    // ---- The updater and the place rules (slice A's share) ----

    private static UpdateResult Add(Sketch sketch, Entity entity) => DirectUpdater.Instance.Apply(sketch, new AddEntity(entity));

    [Fact]
    public void AStrutIsAddedAndRemovedStructurally()
    {
        Strut leg = Leg((0, 7168, 24576), height: 1536);
        Solved added = Assert.IsType<Solved>(Add(Sketch.Empty, leg));

        Assert.Equal(leg, added.Sketch.Find<Strut>(leg.Id));
        Solved removed = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(added.Sketch, new RemoveEntity(leg.Id)));
        Assert.Null(removed.Sketch.Find(leg.Id));
    }

    [Fact]
    public void AStrutThatBreaksAnInvariantIsRefusedByName()
    {
        Assert.Equal(RejectionReason.StrutIsAxisAligned, Assert.IsType<Rejected>(Add(Sketch.Empty, Leg((0, 0, 27648)))).Reason);
        Assert.Equal(RejectionReason.NonPositiveSize, Assert.IsType<Rejected>(Add(Sketch.Empty, Leg((0, 7168, 24576), height: 0))).Reason);
        Assert.Equal(RejectionReason.StrutTooShortForItsCuts, Assert.IsType<Rejected>(Add(Sketch.Empty, Leg((0, 2048, 2048), EndCut.Z, EndCut.Y))).Reason);

        Rejected along = Assert.IsType<Rejected>(Add(Sketch.Empty, Leg((0, 7168, 24576), EndCut.Z, EndCut.X)));
        Assert.Equal(RejectionReason.InvalidStrut, along.Reason);
        Assert.Equal(ValidationErrorKind.StrutCutAlongItself, along.Detail!.Kind);
    }

    [Fact]
    public void AStrutsEndIsAPointAndItsBodyIsNotAPlace()
    {
        Strut leg = Leg((0, 7168, 24576), height: 1536, from: (4096, -4096, 0)) with { Name = "Leg" };
        Sketch sketch = Sketch.Empty.WithEntity(leg);

        Assert.Equal(new Place(new Length(4096), new Length(3072), new Length(24576)), sketch.PlaceOf(new StrutEndRef(leg.Id, StrutEnd.To)));
        Assert.Equal(default, sketch.PlaceOf(new StrutFaceRef(leg.Id, StrutFace.North)));
        Assert.Equal("Leg's from end", PlaceRules.Describe(sketch, new StrutEndRef(leg.Id, StrutEnd.From)));

        Coincident onFace = new(new RelationshipId(Guid.NewGuid()), new StrutFaceRef(leg.Id, StrutFace.North), new StrutEndRef(leg.Id, StrutEnd.To));
        ValidationError refusal = PlaceRules.Refusal(sketch, onFace)!;
        Assert.Equal(ValidationErrorKind.PlacesNotComparable, refusal.Kind);
        Assert.Contains("north face is part of a strut's body", refusal.Message, StringComparison.Ordinal);

        Coincident onEndFace = onFace with { A = new StrutEndFaceRef(leg.Id, StrutEnd.From) };
        Assert.Contains("from end face", PlaceRules.Refusal(sketch, onEndFace)!.Message, StringComparison.Ordinal);

        // A reference to a strut that names another kind of entity is the wrong kind.
        Box box = Box.AsDrawn(EntityId.New(), Layer.Default.Id, Point2.Inches(0, 0), Length.Inches(1), Length.Inches(1), Length.Inches(1), Angle.Zero);
        Sketch wrong = sketch.WithEntity(box).WithRelationship(onFace with { A = new StrutEndRef(box.Id, StrutEnd.From) });
        Assert.Contains(wrong.Validate().Errors, e => e.Kind == ValidationErrorKind.WrongEntityKind);
    }

    [Fact]
    public void AStrutEndHeldToAPartHoldsAndChecks()
    {
        // The bench's seat underside at 24″, and a leg's top on it (§9.1): exact, satisfied.
        Box seat = new(EntityId.New(), Layer.Default.Id, new Point3(Length.Zero, Length.Zero, new Length(24576)), Length.Inches(36), Length.Inches(12), new Length(768), BoxFace.Top, Angle.Zero);
        Strut leg = Leg((0, 7168, 24576), height: 1536, from: (4096, -4096, 0));
        AxisDistance under = new(new RelationshipId(Guid.NewGuid()), new FeatureRef(seat.Id, BoxFeature.Face(BoxFace.Bottom)), new StrutEndRef(leg.Id, StrutEnd.To), Axis.Z, Length.Zero);
        Sketch sketch = Sketch.Empty.WithEntity(seat).WithEntity(leg).WithRelationship(under);

        Assert.True(sketch.Validate().IsValid, sketch.Validate().ToString());
        Assert.True(RelationshipChecker.Check(sketch).AllHold);
    }

    [Fact]
    public void AGeometryRequestThatCouldReachAStrutIsNotGuessedAt()
    {
        Box seat = new(EntityId.New(), Layer.Default.Id, new Point3(Length.Zero, Length.Zero, new Length(24576)), Length.Inches(36), Length.Inches(12), new Length(768), BoxFace.Top, Angle.Zero);
        Strut leg = Leg((0, 7168, 24576), height: 1536, from: (4096, -4096, 0));
        Sketch sketch = Sketch.Empty.WithEntity(seat).WithEntity(leg);
        AxisDistance under = new(new RelationshipId(Guid.NewGuid()), new FeatureRef(seat.Id, BoxFeature.Face(BoxFace.Bottom)), new StrutEndRef(leg.Id, StrutEnd.To), Axis.Z, Length.Zero);

        Rejected Refused(Sketch on, Request request) => Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(on, request));

        Assert.Equal(RejectionReason.UnsupportedRequest, Refused(sketch, new AddRelationship(under)).Reason);
        Assert.Equal(RejectionReason.UnsupportedRequest, Refused(sketch, new SetPosition(leg.Id, Point3.Origin)).Reason);
        Assert.Equal(RejectionReason.UnsupportedRequest, Refused(sketch, new Drag(leg.Id, Vector3.Zero)).Reason);

        // Once a file holds one, nothing that propagates runs on that sketch until #190.
        Sketch held = sketch.WithRelationship(under);
        Assert.Equal(RejectionReason.UnsupportedRequest, Refused(held, new SetPosition(seat.Id, Point3.Origin)).Reason);

        // Without one, a box still moves.
        Assert.IsType<Solved>(DirectUpdater.Instance.Apply(sketch, new SetPosition(seat.Id, Point3.Origin)));
    }

    [Fact]
    public void AnAxisIsReadAsAnIntegerVector()
    {
        IntegerVector3 v = new(1, 2, 3);

        Assert.Equal((Int128)2, v.Component(Axis.Y));
        Assert.Equal(new IntegerVector3(0, 1, 0), IntegerVector3.Unit(Axis.Y));
        Assert.Throws<ArgumentOutOfRangeException>(() => v.Component((Axis)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegerVector3.Unit((Axis)7));
        Assert.Throws<OverflowException>(() => new IntegerVector3(Int128.MaxValue, 0, 0).Dot(new IntegerVector3(2, 0, 0)));
    }

    // ---- Properties (angled-parts §9.4) ----

    [Fact]
    public void TheBlankIsCanonicalAndNeverShorterThanItsCentreline()
    {
        Random random = new(189);
        for (int i = 0; i < 400; i++)
        {
            (long X, long Y, long Z) d = (random.Next(-40, 41) * 256, random.Next(-40, 41) * 256, random.Next(-40, 41) * 256);
            Vector3 direction = new(new Length(d.X), new Length(d.Y), new Length(d.Z));
            Axis reference = (Axis)random.Next(3);
            EndCut fromCut = (EndCut)random.Next(4), toCut = (EndCut)random.Next(4);
            Strut leg = Leg(d, fromCut, toCut, reference, height: random.Next(1, 8) * 512, depth: random.Next(1, 8) * 512);
            if (!Strut.LeansIn(direction) || Strut.CutAlongItself(leg) is not null)
            {
                continue;
            }

            StrutFrame frame = leg.Frame();
            Assert.True(frame.D.Cross(frame.Y).Dot(frame.Z) > 0);
            Assert.Equal(0, frame.Y.Dot(frame.D));

            StrutBlank blank = leg.Blank();
            double c = Math.Sqrt((double)frame.D.SquaredLength);
            Assert.True(blank.Length.Value.Units >= Math.Floor(c));
            Assert.Equal(blank, Swapped(leg).Blank());
            Assert.Equal(blank, (leg with { From = leg.From + new Vector3(new Length(7), new Length(-3), new Length(11)), To = leg.To + new Vector3(new Length(7), new Length(-3), new Length(11)) }).Blank());

            // Exact iff the integer proof says so, and then the double agrees with it.
            if (blank.Length.Exact)
            {
                long s = blank.Cuts.Sum(cut => ((CornerCut)cut.Cut).AlongX.Units);
                long root = (long)ExactRoots.SquareRoot(frame.D.SquaredLength)!.Value;
                Assert.Equal(root + (s / 2), blank.Length.Value.Units);
            }
        }
    }
}

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// A butt joint on a strut's end (#193, angled-parts §5, §9.3 case 11): where it sits, how long the
/// contact is, and what it refuses. Every length is worked by hand beside it.
/// </summary>
public class StrutJointTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    private static RelationshipId NewId() => new(Guid.NewGuid());

    private static Point3 At(long x, long y, long z) => new(new Length(x), new Length(y), new Length(z));

    private static Strut Leg(Point3 from, Point3 to, EndCut fromCut = EndCut.Z, EndCut toCut = EndCut.Z)
        => new(EntityId.New(), Layer.Default.Id, from, to, fromCut, toCut, Axis.Z, new Length(1536), new Length(1536));

    private static Box Seat(long underside) => new(EntityId.New(), Layer.Default.Id, At(0, 0, underside), Length.Inches(36), Length.Inches(12), new Length(768), BoxFace.Top, Angle.Zero);

    private static FeatureRef Bottom(Box box) => new(box.Id, BoxFeature.Face(BoxFace.Bottom));

    private static StrutJoint Pocketed(PlaceRef receiving, Strut strut, StrutEnd end = StrutEnd.To)
        => new(NewId(), receiving, new StrutEndFaceRef(strut.Id, end), new Fastening(FasteningKind.PocketScrews, null, null), Glue: true, StrutFace.Bottom);

    [Fact]
    public void TheBenchLegsTopSitsUnderTheSeat()
    {
        // §9.1: the leg's end, cut to Z, is its 1½″ square section stretched along the lean by 25/24:
        // 1½ across X and 1½ × 25/24 = 1.5625″ along Y. The long side is the joint length: 1600 units.
        Box seat = Seat(24576);
        Strut leg = Leg(At(4096, -4096, 0), At(4096, 3072, 24576));
        StrutJoint joint = Pocketed(Bottom(seat), leg);
        Sketch sketch = Sketch.Empty.WithEntity(seat).WithEntity(leg).WithRelationship(joint);

        Assert.True(sketch.Validate().IsValid, sketch.Validate().ToString());
        Assert.Equal(new StrutContact(Axis.Z, new Length(1600)), StrutJointGeometry.Contact(sketch, joint));
        Assert.True(RelationshipChecker.Check(sketch).AllHold);
    }

    [Fact]
    public void TheFootstoolLegsTopIsLongerAlongItsRun()
    {
        // §9.2 board 1: the section stretched along a 12-in-13 rise: 1½ × 13/12 = 1.625″ = 1664 units.
        Box seat = Seat(12288);
        Strut leg = Leg(At(0, -1024, 0), At(3072, 3072, 12288));
        StrutJoint joint = Pocketed(Bottom(seat), leg);

        Assert.Equal(new Length(1664), StrutJointGeometry.Contact(Sketch.Empty.WithEntity(seat).WithEntity(leg), joint)!.JointLength);
    }

    [Fact]
    public void TwoStrutsMeetAtAnApex()
    {
        // An A-frame: two 45° rafters, their Y ends cut to the plane y = 12″ and meeting there. Each end
        // is 1½″ across X and 1½√2 ≈ 2.1213″ up Z; they coincide, so the overlap is the same: 2172 units.
        Strut left = Leg(At(0, 0, 0), At(0, 12288, 12288), EndCut.Z, EndCut.Y);
        Strut right = Leg(At(0, 24576, 0), At(0, 12288, 12288), EndCut.Z, EndCut.Y);
        StrutJoint apex = new(NewId(), new StrutEndFaceRef(right.Id, StrutEnd.To), new StrutEndFaceRef(left.Id, StrutEnd.To), Fastening.None, Glue: true, null);
        Sketch sketch = Sketch.Empty.WithEntity(left).WithEntity(right).WithRelationship(apex);

        Assert.True(sketch.Validate().IsValid, sketch.Validate().ToString());
        Assert.Equal(new StrutContact(Axis.Y, new Length(2172)), StrutJointGeometry.Contact(sketch, apex));
    }

    [Fact]
    public void AJointWhoseLegHasComeAwayOpensAndIsNotAViolation()
    {
        Box seat = Seat(24576);
        Strut leg = Leg(At(4096, -4096, 0), At(4096, 3072, 24576));
        Strut shorter = leg with { To = At(4096, 3072, 23552) };
        StrutJoint joint = Pocketed(Bottom(seat), leg);
        Sketch sketch = Sketch.Empty.WithEntity(seat).WithEntity(shorter).WithRelationship(joint);

        Assert.False(StrutJointGeometry.IsSatisfied(sketch, joint));
        Assert.True(RelationshipChecker.Check(sketch).AllHold);

        // Off the seat to the side, on its plane: on one plane, not overlapping, still open.
        Strut beside = Leg(At(40960, -4096, 0), At(40960, 3072, 24576));
        Assert.Null(StrutJointGeometry.Contact(Sketch.Empty.WithEntity(seat).WithEntity(beside), Pocketed(Bottom(seat), beside)));
    }

    [Fact]
    public void TheUpdaterHoldsItAndRefusesWhatCannotBeOne()
    {
        Box seat = Seat(24576);
        Strut leg = Leg(At(4096, -4096, 0), At(4096, 3072, 24576));
        Sketch sketch = Sketch.Empty.WithEntity(seat).WithEntity(leg);

        Assert.IsType<Solved>(Updater.Apply(sketch, new AddRelationship(Pocketed(Bottom(seat), leg))));

        // A square end meets nothing flat.
        Strut square = leg with { ToCut = EndCut.Square };
        Rejected squared = Assert.IsType<Rejected>(Updater.Apply(Sketch.Empty.WithEntity(seat).WithEntity(square), new AddRelationship(Pocketed(Bottom(seat), square))));
        Assert.Contains("meets nothing flat", squared.Detail!.Message, StringComparison.Ordinal);

        // Pocket screws need a face to drill from; a strut's own face, never a box's.
        StrutJoint noFace = Pocketed(Bottom(seat), leg) with { PocketFrom = null };
        Assert.Equal(RejectionReason.InvalidJoint, Assert.IsType<Rejected>(Updater.Apply(sketch, new AddRelationship(noFace))).Reason);
        StrutJoint boxFace = Pocketed(Bottom(seat), leg) with { Fastening = new Fastening(FasteningKind.PocketScrews, 3, BoxFace.South) };
        Assert.Contains("box's", Assert.IsType<Rejected>(Updater.Apply(sketch, new AddRelationship(boxFace))).Detail!.Message, StringComparison.Ordinal);
        StrutJoint faceWithoutPockets = Pocketed(Bottom(seat), leg) with { Fastening = new Fastening(FasteningKind.Screws, null, null) };
        Assert.Contains("not held with pocket screws", StrutJoint.Errors(faceWithoutPockets).Single(), StringComparison.Ordinal);

        // A clip or a count of nothing is not a butt's.
        Assert.NotEmpty(StrutJoint.Errors(Pocketed(Bottom(seat), leg) with { Fastening = new Fastening(FasteningKind.Clips, null, null), PocketFrom = null }));
        Assert.NotEmpty(StrutJoint.Errors(Pocketed(Bottom(seat), leg) with { Fastening = new Fastening(FasteningKind.PocketScrews, 0, null) }));

        // A strut's end cannot sit on itself, nor on a long face or a point.
        Assert.NotEmpty(StrutJoint.Errors(Pocketed(new StrutEndFaceRef(leg.Id, StrutEnd.From), leg)));
        Assert.NotEmpty(StrutJoint.Errors(Pocketed(new NodeRef(EntityId.New()), leg)));

        // Its end face in any other relationship is refused.
        Rejected elsewhere = Assert.IsType<Rejected>(Updater.Apply(sketch, new AddRelationship(new Flush(NewId(), Bottom(seat), new StrutEndFaceRef(leg.Id, StrutEnd.To)))));
        Assert.Contains("only a joint can hold", elsewhere.Detail!.Message, StringComparison.Ordinal);

        // A face on another axis than the end is cut to.
        FeatureRef side = new(seat.Id, BoxFeature.Face(BoxFace.South));
        Assert.Equal(RejectionReason.PlacesNotComparable, Assert.IsType<Rejected>(Updater.Apply(sketch, new AddRelationship(Pocketed(side, leg)))).Reason);
    }

    [Fact]
    public void AnEndFaceIsThePlaneTheEndIsCutTo()
    {
        Strut leg = Leg(At(4096, -4096, 0), At(4096, 3072, 24576), EndCut.Z, EndCut.Y);
        Sketch sketch = Sketch.Empty.WithEntity(leg);

        Assert.Equal(Place.On(Axis.Z, Length.Zero), sketch.PlaceOf(new StrutEndFaceRef(leg.Id, StrutEnd.From)));
        Assert.Equal(Place.On(Axis.Y, new Length(3072)), sketch.PlaceOf(new StrutEndFaceRef(leg.Id, StrutEnd.To)));
        Assert.Equal(default, Sketch.Empty.WithEntity(leg with { FromCut = EndCut.Square }).PlaceOf(new StrutEndFaceRef(leg.Id, StrutEnd.From)));
        Assert.Equal(Place.On(Axis.X, new Length(4096)), Sketch.Empty.WithEntity(leg with { FromCut = EndCut.X, From = At(4096, -4096, 1024) }).PlaceOf(new StrutEndFaceRef(leg.Id, StrutEnd.From)));
    }
}

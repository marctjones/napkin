using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>J on an angled part (#193, angled-parts §5): the butt its end already makes, found by where it sits.</summary>
public class StrutJoiningTests
{
    private static Point3 At(long x, long y, long z) => new(new Length(x), new Length(y), new Length(z));

    private static Strut Leg(Point3 from, Point3 to, EndCut toCut = EndCut.Z)
        => new(EntityId.New(), LayerId.Default, from, to, EndCut.Z, toCut, Axis.Z, new Length(1536), new Length(1536));

    private static readonly Box Seat = new(EntityId.New(), LayerId.Default, At(0, 0, 24576), Length.Inches(36), Length.Inches(12), new Length(768), BoxFace.Top, Angle.Zero);

    [Fact]
    public void ALegsTopUnderTheSeatIsPocketScrewedToItsUnderside()
    {
        Strut leg = Leg(At(4096, -4096, 0), At(4096, 3072, 24576));
        Sketch sketch = Sketch.Empty.WithEntity(Seat).WithEntity(leg);

        StrutJoint joint = StrutJoining.Propose(sketch, Seat.Id, leg.Id)!;

        Assert.Equal(new FeatureRef(Seat.Id, BoxFeature.Face(BoxFace.Bottom)), joint.Receiving);
        Assert.Equal(new StrutEndFaceRef(leg.Id, StrutEnd.To), joint.Inserted);
        Assert.Equal((FasteningKind.PocketScrews, StrutFace.Bottom, true), (joint.Fastening.Kind, joint.PocketFrom!.Value, joint.Glue));
        Assert.Equal(joint with { Id = default }, StrutJoining.Propose(sketch, leg.Id, Seat.Id)! with { Id = default });
    }

    [Fact]
    public void TwoRaftersMeetAtTheirApex()
    {
        Strut left = Leg(At(0, 0, 0), At(0, 12288, 12288), EndCut.Y);
        Strut right = Leg(At(0, 24576, 0), At(0, 12288, 12288), EndCut.Y);

        StrutJoint apex = StrutJoining.Propose(Sketch.Empty.WithEntity(left).WithEntity(right), left.Id, right.Id)!;

        Assert.IsType<StrutEndFaceRef>(apex.Receiving);
    }

    [Fact]
    public void NothingIsProposedWhereNoEndSits()
    {
        Strut away = Leg(At(4096, -4096, 0), At(4096, 3072, 23552));
        Sketch sketch = Sketch.Empty.WithEntity(Seat).WithEntity(away);
        Box other = Seat with { Id = EntityId.New() };

        Assert.Null(StrutJoining.Propose(sketch, Seat.Id, away.Id));
        Assert.Null(StrutJoining.Propose(sketch.WithEntity(other), Seat.Id, other.Id));
        Note note = new(EntityId.New(), LayerId.Default, Point2.Origin, "x", NoteSymbol.None);
        Assert.Null(StrutJoining.Propose(sketch.WithEntity(note), away.Id, note.Id));
    }
}

using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>A box's side snapped flush to a one-way leg's face in the plan (#208, angled-parts §3.3).</summary>
public class StrutSnapTests
{
    private static Point3 At(long x, long y, long z) => new(new Length(x), new Length(y), new Length(z));

    // The splayed bench's leg (§9.1): its local Z is world X, so its bottom face is x = 3328 and its
    // top face x = 4864, both running along Y over its plan extent.
    private static readonly Strut BenchLeg = new(EntityId.New(), LayerId.Default, At(4096, -4096, 0), At(4096, 3072, 24576), EndCut.Z, EndCut.Z, Axis.Z, new Length(1536), new Length(1536));

    [Fact]
    public void AOneWayLegsTwoSideFacesAreSnapLines()
    {
        (StrutFace Face, EdgeLine Line)[] lines = [.. SnapResolver.StrutFaceLines(BenchLeg)];

        Assert.Equal([StrutFace.Bottom, StrutFace.Top], lines.Select(line => line.Face));
        Assert.All(lines, line => Assert.Equal(Axis.X, line.Line.NormalAxis));
        Assert.Equal([new Length(3328), new Length(4864)], lines.Select(line => line.Line.Coordinate));

        // A two-way leg keeps none square: nothing to snap to.
        Strut stool = BenchLeg with { From = At(0, -1024, 0), To = At(3072, 3072, 12288) };
        Assert.Empty(SnapResolver.StrutFaceLines(stool));
    }

    [Fact]
    public void ARailDraggedNearTheLegsBottomFaceLandsFlushToItAndTheUpdaterHoldsIt()
    {
        // A 12″ rail, 3/4″ wide, dragged so its east side is 1/16″ short of the leg's bottom face.
        Box rail = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Origin, Length.Inches(12), Length.Inches(1, 1, 2), Length.Inches(3), Angle.Zero)
            with { Anchor = At(0, 0, 12288) };
        Sketch sketch = Sketch.Empty.WithEntity(BenchLeg).WithEntity(rail);
        Point2 wanted = new(new Length(3328 - 12288 - 64), Length.Zero);

        SnapPlan plan = SnapResolver.Resolve(sketch, rail, wanted, 1, radius: new Length(256));

        Assert.Equal(new Length(3328 - 12288), plan.Anchor.X);
        Flush flush = Assert.IsType<Flush>(Assert.Single(plan.Relationships));
        Assert.Equal(new StrutFaceRef(BenchLeg.Id, StrutFace.Bottom), flush.A);
        Assert.Equal(new FeatureRef(rail.Id, BoxFeature.Face(BoxFace.East)), flush.B);

        Sketch moved = sketch.WithEntity(rail with { Anchor = rail.Anchor with { X = plan.Anchor.X } });
        Assert.IsType<Solved>(DirectUpdater.Instance.Apply(moved, new AddRelationship(flush)));

        // Ignoring the leg, the rail lands on the grid instead.
        Assert.Empty(SnapResolver.Resolve(sketch, rail, wanted, 1, new Length(256), [BenchLeg.Id]).Relationships);
    }
}

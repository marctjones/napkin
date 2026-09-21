namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The engine on its own, which is the shape #28's exactness-repair pass relies on:
/// docs/design/geometry-model.md &#xA7;4.4 "two components" and &#xA7;8 step 8.
/// </summary>
public class PropagatorTests
{
    private static readonly Dictionary<ScalarKey, Length> NoSeeds = [];

    [Fact]
    public void TheEngineHonoursAnEqualParamOnASketchTheWrapperRefuses()
    {
        SketchBuilder builder = new();
        EntityId tilted = builder.AddBox(Point2.Origin, Length.Inches(20), Length.Inches(4), Angle.Degrees(45));
        EntityId upright = builder.AddBox(50, 0, 10, 4);
        RelationshipId width = builder.WidthIs(tilted, Length.Inches(30));
        builder.EqualWidths(tilted, upright);

        // The wrapper refuses: a box at 45° is outside the direct updater's domain.
        Assert.Equal(
            new Rejected(RejectionReason.RotationNotSupported),
            DirectUpdater.Instance.Apply(builder.Sketch, new SetParameter(width, Length.Inches(30))));

        // The engine does not check rotations, so the solver can call it on a rotated sketch.
        Propagated propagated = Assert.IsType<Propagated>(
            Propagator.Run(builder.Sketch, NoSeeds, builder.Sketch.Relationships.Values));

        Assert.Equal(Length.Inches(30), propagated.Assignments[new ScalarKey(tilted, ScalarKind.Width)].Value);
        Assert.Equal(Length.Inches(30), propagated.Assignments[new ScalarKey(upright, ScalarKind.Width)].Value);
    }

    [Fact]
    public void AnEmptySeedStillRepairsTheExactClassRelationships()
    {
        // Design §5.2 step 4: the solver hands the engine a rounded sketch with the exact-class
        // relationships only, and it re-derives every integer-copy relationship.
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 30, 4);
        EntityId b = builder.AddBox(
            Point2.Origin with { X = Length.Inches(30) + new Length(1) },
            Length.Inches(10),
            Length.Inches(4),
            Angle.Zero);
        builder.Flush(a, BoxEdge.East, b, BoxEdge.West);

        // One unit apart after rounding; the repair pass snaps the dependent side onto the other.
        Assert.False(RelationshipChecker.Check(builder.Sketch).AllHold);

        Propagated propagated = Assert.IsType<Propagated>(
            Propagator.Run(builder.Sketch, NoSeeds, builder.Sketch.Relationships.Values));

        Assert.Equal(Length.Inches(30), propagated.Assignments[new ScalarKey(b, ScalarKind.X)].Value);
    }

    [Fact]
    public void TheEngineReportsAConflictRatherThanPickingASide()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4);
        RelationshipId thirty = builder.WidthIs(box, Length.Inches(30));
        RelationshipId forty = builder.Add(id => new ParamValue(id, new BoxWidthRef(box), Length.Inches(40)));

        Conflicted conflicted = Assert.IsType<Conflicted>(
            Propagator.Run(builder.Sketch, NoSeeds, builder.Sketch.Relationships.Values));

        Assert.Equal(ConflictKind.Contradictory, conflicted.Report.Kind);
        Assert.Contains(thirty, conflicted.Report.Relationships);
        Assert.Contains(forty, conflicted.Report.Relationships);
        Assert.Equal(2, conflicted.Report.Derivations.Count);
        Assert.Equal(
            new ParamTarget(new BoxWidthRef(box)),
            conflicted.Report.Derivations[0].Target);
    }

    [Fact]
    public void ASeedIsTheRequestItselfAndCarriesNoRelationship()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 30, 4);

        Propagated propagated = Assert.IsType<Propagated>(Propagator.Run(
            builder.Sketch,
            new Dictionary<ScalarKey, Length> { [new ScalarKey(box, ScalarKind.X)] = Length.Inches(7) },
            builder.Sketch.Relationships.Values));

        Assignment assignment = propagated.Assignments[new ScalarKey(box, ScalarKind.X)];
        Assert.Equal(Length.Inches(7), assignment.Value);
        Assert.Empty(assignment.Via);
    }

    [Fact]
    public void SizesSettleBeforePositionsSoNoCornerOffsetIsEverStale()
    {
        // A corner offset depends on the box's size, so a positional relationship computed before
        // a size relationship had finished would derive a stale value and look like a conflict.
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 20, 4);
        EntityId b = builder.AddBox(20, 0, 10, 4);
        EntityId c = builder.AddBox(0, 20, 20, 4);

        builder.Flush(a, BoxEdge.East, b, BoxEdge.West);
        builder.EqualWidths(c, a);
        builder.WidthIs(c, Length.Inches(35));

        Propagated propagated = Assert.IsType<Propagated>(
            Propagator.Run(builder.Sketch, NoSeeds, builder.Sketch.Relationships.Values));

        Assert.Equal(Length.Inches(35), propagated.Assignments[new ScalarKey(a, ScalarKind.Width)].Value);
        Assert.Equal(Length.Inches(35), propagated.Assignments[new ScalarKey(b, ScalarKind.X)].Value);
    }
}

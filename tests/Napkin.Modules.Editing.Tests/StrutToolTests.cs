using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>The angled-part tool's two clicks, its defaults and the panel's readouts (#192, assembly-model §3a.7).</summary>
public class StrutToolTests
{
    private static Point3 At(long x, long y, long z) => new(new Length(x), new Length(y), new Length(z));

    private static Strut Make(Point3 from, EndCut fromCut, Point3 to, EndCut toCut)
        => StrutTool.Make(EntityId.New(), LayerId.Default, from, fromCut, to, toCut, new Length(1536), new Length(1536));

    [Fact]
    public void TheFirstClickIsHeldAndTheSecondMakesTheStrut()
    {
        StrutTool tool = new();

        Assert.Null(tool.Click(At(4096, -4096, 0), EndCut.Z, Make));
        Assert.Equal((At(4096, -4096, 0), EndCut.Z), tool.First);

        Strut leg = tool.Click(At(4096, 3072, 24576), EndCut.Z, Make)!;
        Assert.Equal((At(4096, -4096, 0), At(4096, 3072, 24576)), (leg.From, leg.To));
        Assert.Equal((EndCut.Z, EndCut.Z, Axis.Z), (leg.FromCut, leg.ToCut, leg.Reference));
        Assert.Null(tool.First);
    }

    [Fact]
    public void TwoClicksOnOnePointMakeNothingAndEscapeForgetsTheFirst()
    {
        StrutTool tool = new();
        tool.Click(At(0, 0, 0), EndCut.Z, Make);
        Assert.Null(tool.Click(At(0, 0, 0), EndCut.Z, Make));

        tool.Click(At(0, 0, 0), EndCut.Z, Make);
        tool.Cancel();
        Assert.Null(tool.First);
    }

    [Theory]
    [InlineData(EndCut.Z, EndCut.Y, Axis.Z)]
    [InlineData(EndCut.Y, EndCut.X, Axis.Y)]
    [InlineData(EndCut.X, EndCut.Square, Axis.X)]
    [InlineData(EndCut.Square, EndCut.Square, Axis.Z)]
    public void TheReferenceIsTheHighestPriorityCutAxis(EndCut fromCut, EndCut toCut, Axis reference)
        => Assert.Equal(reference, StrutTool.DefaultReference(fromCut, toCut));

    [Fact]
    public void ClicksThatCannotBeAStrutAreRefusedInWords()
    {
        Assert.Contains("rectangle tool", StrutTool.Refusal(Make(At(0, 0, 0), EndCut.Square, At(0, 0, 4096), EndCut.Square)), StringComparison.Ordinal);
        Assert.Contains("different heights", StrutTool.Refusal(Make(At(0, 0, 0), EndCut.Z, At(3072, 4096, 0), EndCut.Z)), StringComparison.Ordinal);
        Assert.Null(StrutTool.Refusal(Make(At(0, 0, 0), EndCut.Square, At(3072, 4096, 0), EndCut.Square)));
    }

    [Fact]
    public void TheReadoutsAreTheBenchLegsAndTheFootstoolsWorkedNumbers()
    {
        // Bench (§9.1): 25 7/16″ exact; tilt atan(7/24) = 16.26° → ≈16.5°; it runs due north, 90° exactly.
        (string length, string tilt, string azimuth) = StrutTool.Readouts(Make(At(4096, -4096, 0), EndCut.Z, At(4096, 3072, 24576), EndCut.Z), LengthFormat.Default);
        Assert.Equal(("2'-1 7/16\"", "≈16.5°", "90°"), (length, tilt, azimuth));

        // Footstool board 1 (§9.2): 13 5/8″; tilt atan(5/12) = 22.62° → ≈22.5°; azimuth atan(4/3) = 53.13° → ≈53°.
        (length, tilt, azimuth) = StrutTool.Readouts(Make(At(0, -1024, 0), EndCut.Z, At(3072, 3072, 12288), EndCut.Z), LengthFormat.Default);
        Assert.Equal(("1'-1 5/8\"", "≈22.5°", "≈53°"), (length, tilt, azimuth));

        // Board 2 is not proven: its length reads ≈ however it rounds.
        Strut compound = Make(At(0, -1024, 0), EndCut.Z, At(3072, 3072, 12288), EndCut.Z) with { Reference = Axis.X };
        Assert.StartsWith("≈", StrutTool.Readouts(compound, LengthFormat.Default).Length, StringComparison.Ordinal);
    }

    [Fact]
    public void APlanClickOnAPartIsATopAtItsUndersideAndOnPaperAFootOnTheFloor()
    {
        // The splayed bench's seat, 3/4″ thick with its underside at 24″ (§9.1).
        Box seat = new(EntityId.New(), LayerId.Default, At(0, 0, 24576), Length.Inches(36), Length.Inches(12), new Length(768), BoxFace.Top, Angle.Zero);

        Assert.Equal((At(4096, 3072, 24576), EndCut.Z), StrutTool.PlanEnd(new Point2(new Length(4096), new Length(3072)), seat));
        Assert.Equal((At(4096, -4096, 0), EndCut.Z), StrutTool.PlanEnd(new Point2(new Length(4096), new Length(-4096)), null));
    }

    [Fact]
    public void TwoClicksOnThePaperAreAFlatBraceWithSquareEnds()
    {
        Strut flat = StrutTool.Flattened(Make(At(0, 0, 0), EndCut.Z, At(3072, 4096, 0), EndCut.Z));
        Assert.Equal((EndCut.Square, EndCut.Square), (flat.FromCut, flat.ToCut));
        Assert.Null(StrutTool.Refusal(flat));

        Strut kept = StrutTool.Flattened(Make(At(0, 0, 0), EndCut.X, At(3072, 4096, 0), EndCut.Y));
        Assert.Equal((EndCut.X, EndCut.Y), (kept.FromCut, kept.ToCut));

        Strut leg = Make(At(4096, -4096, 0), EndCut.Z, At(4096, 3072, 24576), EndCut.Z);
        Assert.Same(leg, StrutTool.Flattened(leg));
    }

    [Theory]
    [InlineData(Axis.Z, "keep the wide face vertical")]
    [InlineData(Axis.X, "keep the wide face parallel to the long side")]
    [InlineData(Axis.Y, "keep the wide face parallel to the short side")]
    public void TheReferenceIsOfferedInPlainWords(Axis axis, string words) => Assert.Equal(words, StrutTool.ReferenceWords(axis));
}

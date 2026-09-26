using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// A strut on the cut list (#191): its derived length on the dimension its part names, marked ≈
/// unless proven exact; its plain mitres in the shaped-parts sentence and its compound ends in
/// angled-parts §2.2's; the half-degree rule; half-turn grouping (angled-parts §9.3 cases 4–7,
/// assembly-model §9 cases 25–27, 30 and 32). Every expected string is worked by hand beside it.
/// </summary>
public class StrutCutListTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    private static readonly PlanAxes LengthWidth = new(PartDimension.Length, PartDimension.Width);

    private static Point3 At(long x, long y, long z) => new(new Length(x), new Length(y), new Length(z));

    private static Strut Leg(
        string name,
        Point3 from,
        Point3 to,
        Axis reference = Axis.Z,
        long height = 1536,
        long depth = 1536,
        string stock = "2x2",
        EndCut toCut = EndCut.Z,
        PlanAxes? axes = null)
        => new Strut(EntityId.New(), LayerId.Default, from, to, EndCut.Z, toCut, reference, new Length(height), new Length(depth))
        {
            Name = name,
            Part = new Part(stock, null, 1, axes ?? LengthWidth),
        };

    private static ImmutableArray<CutListRow> Rows(params Entity[] entities)
        => CutList.Of(entities.Aggregate(Sketch.Empty, (sketch, entity) => sketch.WithEntity(entity)), Library);

    // ---- The splayed bench (§9.1): one-way lean, plain mitres, every length exact ----

    [Fact]
    public void TheSplayedBenchsFourLegsAreOneExactRow()
    {
        // Each leg runs 7″ out and 24″ up (7-24-25), mirrored four ways; a 2x2 is 1½″ square.
        ImmutableArray<CutListRow> rows = Rows(
            Leg("Leg, south-west", At(4096, -4096, 0), At(4096, 3072, 24576)),
            Leg("Leg, north-west", At(4096, 16384, 0), At(4096, 9216, 24576)),
            Leg("Leg, south-east", At(32768, -4096, 0), At(32768, 3072, 24576)),
            Leg("Leg, north-east", At(32768, 16384, 0), At(32768, 9216, 24576)));

        CutListRow row = Assert.Single(rows);
        Assert.Equal(("Leg", 4), (row.Label, row.Quantity));

        // L = 25 + 7/16 = 25 7/16″ = 26048, exact: 7² + 24² = 25², and 1½ × 7 / 24 = 7/16.
        Assert.Equal((26048, 1536, 1536), (row.Length.Units, row.Width.Units, row.Thickness.Units));
        Assert.Equal(("2'-1 7/16\"", "1 1/2\"", "1 1/2\""), (row.LengthText, row.WidthText, row.ThicknessText));
        Assert.True(row.DerivedExact);
        Assert.Equal(PartDimension.Length, row.Derived);

        // The south-west cut takes 7/16″ off the south edge and the whole west end: a mitre of the
        // west end to the north-west corner. atan(7/24) = 16.26° → ≈16.5°. The north-east cut is
        // its twin at the other end, a parallelogram.
        Assert.Equal(
            [
                "Mitre the west end: from 7/16\" in along the south edge to the north-west corner (≈16.5° off square).",
                "Mitre the east end: from 7/16\" in along the north edge to the south-east corner (≈16.5° off square).",
            ],
            row.CutText);
    }

    // ---- The splayed footstool (§9.2): two-way lean, both boards ----

    private static Strut StoolLeg(string name, long dx, long dy, Axis reference)
        => Leg(name, At(0, 0, 0), At(dx, dy, 12288), reference);

    [Fact]
    public void TheFootstoolLegWithItsWideFaceVerticalIsAPlainMitreAndExact()
    {
        // d = (3, 4, 12): 13″ centreline, run 5, setback 1½ × 5 / 12 = 5/8″, L = 13 5/8″ = 13952.
        // atan(5/12) = 22.62° → ≈22.5°.
        CutListRow row = Assert.Single(Rows(StoolLeg("Leg", 3072, 4096, Axis.Z)));

        Assert.Equal(13952, row.Length.Units);
        Assert.Equal("1'-1 5/8\"", row.LengthText);
        Assert.Equal(
            [
                "Mitre the west end: from 5/8\" in along the south edge to the north-west corner (≈22.5° off square).",
                "Mitre the east end: from 5/8\" in along the north edge to the south-east corner (≈22.5° off square).",
            ],
            row.CutText);
    }

    [Fact]
    public void TheFootstoolLegParallelToTheLongSideIsCompoundAndMarked()
    {
        // Board 2: L = 14 202 (13.869″), not proven, so marked however it rounds: 13 7/8″ at 1/16.
        // Mitre atan(0.21893 / 0.92308) = 13.34° → ≈13.5°; bevel asin(0.31623) = 18.43° → ≈18.5°.
        // Long points south-bottom at the foot (west) and north-top at the seat (east).
        CutListRow row = Assert.Single(Rows(StoolLeg("Leg", 3072, 4096, Axis.X)));

        Assert.Equal(14202, row.Length.Units);
        Assert.False(row.DerivedExact);
        Assert.Equal("≈1'-1 7/8\"", row.LengthText);
        Assert.Equal(
            [
                "Cut both ends at a compound angle: mitre ≈13.5°, bevel ≈18.5°, with the top face on the saw table; "
                + "long point at the south-bottom corner of the west end and the north-top corner of the east end.",
            ],
            row.CutText);
    }

    [Fact]
    public void FourCompoundLegsTurnedOverAreOneRowAndAnotherBoardIsNot()
    {
        // §9.3 case 6. The four legs of the magazine stool put their long points at different
        // corners; each is another turned end for end or over, so they group.
        ImmutableArray<CutListRow> rows = Rows(
            StoolLeg("Leg, a", 3072, 4096, Axis.X),
            StoolLeg("Leg, b", 3072, -4096, Axis.X),
            StoolLeg("Leg, c", -3072, 4096, Axis.X),
            StoolLeg("Leg, d", -3072, -4096, Axis.X),
            StoolLeg("Odd, reference Y", 3072, 4096, Axis.Y),
            Leg("Odd, thin", At(0, 0, 0), At(3072, 4096, 12288), Axis.X, depth: 1024));

        Assert.Equal(3, rows.Length);
        CutListRow legs = Assert.Single(rows, row => row.Label == "Leg");
        Assert.Equal(4, legs.Quantity);
    }

    [Fact]
    public void AMixedBlankSaysItsMitreThenItsCompoundEnd()
    {
        // Assembly-model case 24's strut, now accepted (§9.3 case 3): a Z end that is a plain mitre,
        // a Y end that is compound. The mitre's setback, 3584 × |y·Z| / (|z| × 27648), is not proven.
        CutListRow row = Assert.Single(Rows(Leg("Brace", At(0, 0, 0), At(1024, 9216, 27648), height: 3584, stock: "2x4", toCut: EndCut.Y)));

        Assert.False(row.SetbacksExact);
        Assert.Equal(2, row.CutText.Length);
        Assert.StartsWith("Mitre the west end: from ≈", row.CutText[0], StringComparison.Ordinal);
        Assert.StartsWith("Cut the east end at a compound angle: mitre ≈", row.CutText[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ABevelOnlyEndIsSaidAsABevelAlongAnEdge()
    {
        // The bench leg with its wide face turned (reference X): the floor cut is a bevel through
        // the narrow face, mitre 0°, bevel atan(7/24) = 16.26° → ≈16.5°, long point the bottom edge
        // at the foot and the top edge at the seat.
        CutListRow row = Assert.Single(Rows(Leg("Leg", At(0, 0, 0), At(0, 7168, 24576), Axis.X)));

        Assert.Equal(26048, row.Length.Units);
        Assert.Equal(
            [
                "Cut both ends at a compound angle: bevel ≈16.5°, with the top face on the saw table; "
                + "long point at the bottom edge of the west end and the top edge of the east end.",
            ],
            row.CutText);
    }

    // ---- The sawhorse (assembly-model §9 cases 25, 30, 32) ----

    [Fact]
    public void TheSawhorsesFourLegsAreOneRowOfFourRoundedOnce()
    {
        // Tops at (3072, 0) and (33792, 0) under a 27″ rail; feet 6″ out in X and 9″ out in Y.
        // d = (6, 9, 27)″, |d|² = 887 095 296 not a square: L ≈ 31 219.956 → 31 220 (30.488″, 30 1/2″
        // at 1/16) and the setback ≈ 1435.81 → 1436 (1.402″, 1 3/8″ at 1/16), atan(1436 / 3584) =
        // 21.83° → ≈22°. None proven, so all marked.
        Strut Sawhorse(string name, long footX, long footY, long topX)
            => Leg(name, At(footX, footY, 0), At(topX, 0, 27648), height: 3584, stock: "2x4");

        ImmutableArray<CutListRow> rows = Rows(
            Sawhorse("Leg, 1", -3072, -9216, 3072),
            Sawhorse("Leg, 2", -3072, 9216, 3072),
            Sawhorse("Leg, 3", 39936, -9216, 33792),
            Sawhorse("Leg, 4", 39936, 9216, 33792));

        CutListRow row = Assert.Single(rows);
        Assert.Equal(4, row.Quantity);
        Assert.Equal(31220, row.Length.Units);
        Assert.Equal("≈2'-6 1/2\"", row.LengthText);
        Assert.Equal(
            [
                "Mitre the west end: from ≈1 3/8\" in along the south edge to the north-west corner (≈22° off square).",
                "Mitre the east end: from ≈1 3/8\" in along the north edge to the south-east corner (≈22° off square).",
            ],
            row.CutText);
        Assert.Contains("\"≈2'-6 1/2\"\"", CutListCsv.ToCsv(rows), StringComparison.Ordinal);
    }

    [Fact]
    public void AFootMovedOneUnitLeavesTheRow()
    {
        // Case 32: a translation changes no direction; one unit on one foot changes that leg's.
        Strut a = Leg("Leg", At(0, 0, 0), At(0, 7168, 24576));
        Strut b = Leg("Leg", At(10240, 0, 0), At(10240, 7168, 24576));
        Strut c = Leg("Leg", At(20480, 1, 0), At(20480, 7168, 24576));

        ImmutableArray<CutListRow> rows = Rows(a, b, c);

        Assert.Equal([2, 1], rows.Select(row => row.Quantity).OrderDescending());
    }

    // ---- What the list leaves out, and how a strut's part is read ----

    [Fact]
    public void AStrutThatIsNotAPartOrNotNewIsNotCut()
    {
        Strut bare = Leg("Leg", At(0, 0, 0), At(0, 7168, 24576)) with { Part = null };
        Strut there = Leg("Leg", At(0, 0, 0), At(0, 7168, 24576)) with { Phase = Phase.Existing };

        Assert.Empty(Rows(bare, there));
    }

    [Fact]
    public void AnAngledShelfListsItsDerivedDimensionAsItsWidthForTheFullLength()
    {
        // §1.5: a shelf 30″ long tilted across its depth, 10″ back and 3″ up, ¾″ thick, cut plumb
        // at the front and square to the back panel (both Y cuts, reference Y). Its derived
        // dimension is its width: √(10² + 3²) = 10.44″, not a square, so marked. The out-of-plane
        // dimension is its 30″ length, which the sentence carries (shaped-parts §4.4).
        Strut shelf = Leg(
            "Shelf",
            At(0, 0, 0),
            At(0, 10240, 3072),
            Axis.Y,
            height: 768,
            depth: 30720,
            stock: "3/4 plywood",
            toCut: EndCut.Y,
            axes: new PlanAxes(PartDimension.Width, PartDimension.Thickness)) with { FromCut = EndCut.Y };

        CutListRow row = Assert.Single(Rows(shelf));

        Assert.Equal(PartDimension.Width, row.Derived);
        Assert.Equal((30720, 768), (row.Length.Units, row.Thickness.Units));
        Assert.StartsWith("≈", row.WidthText, StringComparison.Ordinal);
        Assert.All(row.CutText, sentence => Assert.EndsWith(", for the full 2'-6\" length.", sentence, StringComparison.Ordinal));
    }

    // ---- A rounded value that happens to land on a sixteenth is still marked ----

    [Fact]
    public void ARoundedLengthOnASixteenthIsStillMarked()
    {
        // d = (0, 1/4, 3 1/4)″: |d|² = 256² + 3328² = 11 141 120, not a square. L ≈ 3455.9 → 3456 =
        // 3 3/8″ exactly on the tape, but not the leg's length, so it reads ≈ (assembly-model §3a.4).
        CutListRow row = Assert.Single(Rows(Leg("Peg", At(0, 0, 0), At(0, 256, 3328))));

        Assert.Equal(3456, row.Length.Units);
        Assert.Equal("≈3 3/8\"", row.LengthText);
        Assert.Equal("1 1/2\"", row.WidthText);
    }

    [Fact]
    public void ARoundedSetbackOnASixteenthIsStillMarked()
    {
        // d = (1, 4, 9)″, a 2x2, reference Z: s = 1536 × √17 / 9 = 703.68 → 704 = 11/16″ on the tape,
        // irrational in truth, so marked; and so is its angle.
        CutListRow row = Assert.Single(Rows(Leg("Leg", At(0, 0, 0), At(1024, 4096, 9216))));

        Assert.False(row.SetbacksExact);
        Assert.StartsWith("Mitre the west end: from ≈11/16\" in", row.CutText[0], StringComparison.Ordinal);
    }

    // ---- Stock, species and the square-ended brace ----

    [Fact]
    public void AStrutsStockIsResolvedAsABoxsIs()
    {
        Strut unnamed = Leg("Brace, bare", At(0, 0, 0), At(3072, 0, 4096)) with { Part = new Part(null, null, 1, LengthWidth) };
        Strut unknown = Leg("Brace, odd", At(0, 0, 0), At(3072, 0, 5120), stock: "2x2 unobtanium");
        Strut oak = Leg("Brace, oak", At(0, 0, 0), At(3072, 0, 6144)) with { Part = new Part("2x2", "white oak", 1, LengthWidth) };

        ImmutableArray<CutListRow> rows = Rows(unnamed, unknown, oak);

        CutListRow bare = Assert.Single(rows, row => row.Label == "Brace, bare");
        Assert.Equal((string.Empty, false), (bare.Material, bare.Unresolved));
        CutListRow odd = Assert.Single(rows, row => row.Label == "Brace, odd");
        Assert.Equal(("2x2 unobtanium", true), (odd.Material, odd.Unresolved));
        Assert.Equal("white oak", Assert.Single(rows, row => row.Label == "Brace, oak").Species);
    }

    [Fact]
    public void ASquareEndedBraceIsItsCentrelineWithNothingToSay()
    {
        // A 3-4-5 brace fixed by hardware at both ends: 5″ long, exact, no cuts at all.
        Strut brace = Leg("Brace", At(0, 0, 0), At(3072, 0, 4096)) with { FromCut = EndCut.Square, ToCut = EndCut.Square };

        CutListRow row = Assert.Single(Rows(brace, brace with { Id = EntityId.New() }));

        Assert.Equal((2, 5120), (row.Quantity, row.Length.Units));
        Assert.Empty(row.CutText);
    }

    [Fact]
    public void RowsWithCompoundEndsCompareAndHashByValue()
    {
        CutListRow one = Assert.Single(Rows(StoolLeg("Leg", 3072, 4096, Axis.X)));
        CutListRow two = one with { };

        Assert.Equal(one, two);
        Assert.Equal(one.GetHashCode(), two.GetHashCode());
        Assert.NotEqual(one, one with { CompoundEnds = [] });
    }

    // ---- Pocket-screwed legs (angled-parts §5, §9.3 case 11) ----

    [Fact]
    public void BenchLegsPocketScrewedUnderTheSeatSayWhereToDrillAndCountTheScrews()
    {
        // Each leg's top meets the seat's underside over a 1½″ × 1.5625″ patch; pocket screws are at
        // least 2, one per 2″ (§7.2), so 2 each, drilled in the top (east) end from the bottom face.
        Box seat = new(EntityId.New(), LayerId.Default, At(0, 0, 24576), Length.Inches(36), Length.Inches(12), new Length(768), BoxFace.Top, Angle.Zero)
        {
            Name = "Seat",
            Part = new Part("3/4 plywood", null, 1, LengthWidth),
        };
        Strut[] legs =
        [
            Leg("Leg, south-west", At(4096, -4096, 0), At(4096, 3072, 24576)),
            Leg("Leg, north-west", At(4096, 16384, 0), At(4096, 9216, 24576)),
            Leg("Leg, south-east", At(32768, -4096, 0), At(32768, 3072, 24576)),
            Leg("Leg, north-east", At(32768, 16384, 0), At(32768, 9216, 24576)),
        ];
        Sketch sketch = legs.Aggregate(Sketch.Empty.WithEntity(seat), (s, leg) => s.WithEntity(leg).WithRelationship(new StrutJoint(
            new RelationshipId(Guid.NewGuid()),
            new FeatureRef(seat.Id, BoxFeature.Face(BoxFace.Bottom)),
            new StrutEndFaceRef(leg.Id, StrutEnd.To),
            new Fastening(FasteningKind.PocketScrews, null, null),
            Glue: true,
            StrutFace.Bottom)));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library), candidate => candidate.Label == "Leg");
        Assert.Equal(4, row.Quantity);
        Assert.Equal(["Drill 2 pocket holes in the east end from the bottom face."], row.JointText);
        Assert.False(row.JointsUnsatisfied);

        FastenerRow screws = Assert.Single(FastenerList.Of(sketch));
        Assert.Equal((FastenerKind.PocketScrew, 8), (screws.Kind, screws.Count));
        Assert.Equal(new Length(1536), screws.Thickness);
        Assert.All(screws.Sources, source => Assert.Equal(new Length(1600), source.JointLength));
        Assert.Contains(SuppliesList.GlueLine(4, 4), SuppliesList.Of(sketch).Select(extra => extra.Item));

        // A leg whose top has come off the seat opens: flagged on its row, nothing drilled.
        Sketch lowered = sketch.WithEntity(legs[0] with { To = At(4096, 3072, 23552) });
        Assert.Contains(CutList.Of(lowered, Library), candidate => candidate.JointsUnsatisfied);
    }

    [Fact]
    public void TheFootstoolSampleBuysEightPocketScrewsAndGluesFourJoints()
    {
        // samples/splayed-footstool: four legs, each 2 pocket screws into the seat (1 5/8″ contact).
        Sketch sketch = Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath("splayed-footstool"))).Sketch;

        FastenerRow screws = Assert.Single(FastenerList.Of(sketch));
        Assert.Equal((FastenerKind.PocketScrew, 8, new Length(1536)), (screws.Kind, screws.Count, screws.Thickness));
        Assert.All(screws.Sources, source => Assert.Equal(new Length(1664), source.JointLength));
        Assert.Contains(SuppliesList.GlueLine(4, 4), SuppliesList.Of(sketch).Select(extra => extra.Item));
    }

    // ---- The half-degree rule (§2.4), in its one function ----

    [Theory]
    [InlineData(16.2602, false, "≈16.5°")]
    [InlineData(16.25, false, "≈16.5°")]
    [InlineData(16.24, false, "≈16°")]
    [InlineData(22.6199, false, "≈22.5°")]
    [InlineData(45, true, "45°")]
    [InlineData(45, false, "≈45°")]
    public void AnAngleReadsToTheNearestHalfDegreeMarkedUnlessProven(double degrees, bool exact, string text)
        => Assert.Equal(text, CutDescription.AngleText(degrees, exact));
}

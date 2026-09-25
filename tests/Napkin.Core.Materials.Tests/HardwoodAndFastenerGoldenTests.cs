using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Core.Materials.Tests;

/// <summary>
/// The hardwood-thickness and common-nail tables against the sources they were read from.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Hardwood.</strong> NHLA, <em>Rules for the Measurement &amp; Inspection of Hardwood
/// &amp; Cypress</em>, effective 1 January 2023, page 7, retrieved 2026-09-21. ¶13 gives the
/// standard rough thicknesses and says those of 1 in and over may be written in quarter inches
/// (4/4, 5/4, 6/4, 7/4, 8/4, 10/4, 12/4, 14/4, 16/4, ...). ¶14 says surfaced thickness is the
/// rough thickness less 3/16 in for lumber 1-1/2 in thick or less and less 1/4 in for lumber
/// between 1-3/4 in and 4 in thick, and tabulates: 4/4 → 13/16, 5/4 → 1-1/16, 6/4 → 1-5/16,
/// 7/4 → 1-1/2, 8/4 → 1-3/4, 10/4 → 2-1/4, 12/4 → 2-3/4, 14/4 → 3-1/4, 16/4 → 3-3/4. ¶12 gives
/// standard lengths of 4' through 16' in every foot.
/// </para>
/// <para>
/// <strong>Nails.</strong> Federal Specification FF-N-105B (GSA, 17 March 1971), §3.6.11.2,
/// Type II style 10 steel wire common nails, page 11, retrieved 2026-09-21. Penny size, length,
/// diameter: 2d 1 .072 · 3d 1-1/4 .080 · 4d 1-1/2 .099 · 5d 1-3/4 .099 · 6d 2 .113 · 7d 2-1/4
/// .113 · 8d 2-1/2 .131 · 9d 2-3/4 .131 · 10d 3 .148 · 12d 3-1/4 .148 · 16d 3-1/2 .162 · 20d 4
/// .192 · 30d 4-1/2 .207 · 40d 5 .226 · 50d 5-1/2 .244 · 60d 6 .262.
/// </para>
/// </remarks>
public sealed class HardwoodAndFastenerGoldenTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    /// <summary>NHLA ¶13 and ¶14, row by row.</summary>
    public static TheoryData<string, Length, Length> HardwoodRows => new()
    {
        { "4/4", Length.Inches(1), Length.Inches(0, 13, 16) },
        { "5/4", Length.Inches(1, 1, 4), Length.Inches(1, 1, 16) },
        { "6/4", Length.Inches(1, 1, 2), Length.Inches(1, 5, 16) },
        { "7/4", Length.Inches(1, 3, 4), Length.Inches(1, 1, 2) },
        { "8/4", Length.Inches(2), Length.Inches(1, 3, 4) },
        { "10/4", Length.Inches(2, 1, 2), Length.Inches(2, 1, 4) },
        { "12/4", Length.Inches(3), Length.Inches(2, 3, 4) },
        { "14/4", Length.Inches(3, 1, 2), Length.Inches(3, 1, 4) },
        { "16/4", Length.Inches(4), Length.Inches(3, 3, 4) },
    };

    [Theory]
    [MemberData(nameof(HardwoodRows))]
    [Trait("Feature", "MAT-003")]
    public void EachHardwoodRowMatchesTheNhlaRule(string name, Length rough, Length surfaced)
    {
        Assert.True(Library.TryFind(name, out StockItem item), $"{name} is not in the shipped library.");
        HardwoodStock hardwood = Assert.IsType<HardwoodStock>(item);

        Assert.Equal(rough, hardwood.RoughThickness);
        Assert.Equal(surfaced, hardwood.SurfacedTwoSides);
    }

    /// <summary>
    /// ¶14's two deductions, checked as arithmetic rather than as transcription: 3/16 in off up to
    /// 1-1/2 in thick, 1/4 in off from 1-3/4 in up.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-003")]
    public void TheSurfacingDeductionIsTheOneParagraphFourteenStates()
    {
        foreach (StockItem item in Library.InCategory(StockCategory.HardwoodBoard))
        {
            HardwoodStock hardwood = Assert.IsType<HardwoodStock>(item);
            Length expected = hardwood.RoughThickness <= Length.Inches(1, 1, 2)
                ? Length.Inches(0, 3, 16)
                : Length.Inches(0, 1, 4);

            Assert.Equal(expected, hardwood.RoughThickness - hardwood.SurfacedTwoSides);
        }
    }

    /// <summary>
    /// Hardwood is racked in every foot from 4' to 16' (¶12), unlike softwood framing, which a
    /// yard carries in 2 ft steps. Two different sources, two different lists.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-003")]
    public void HardwoodIsStockedInEveryFootAndSoftwoodIsNot()
    {
        Assert.True(Library.TryFind("8/4", out StockItem hardwood));
        Assert.Equal(13, hardwood.StandardLengths.Length);
        Assert.Equal(Length.Feet(4), hardwood.StandardLengths[0]);
        Assert.Equal(Length.Feet(16), hardwood.StandardLengths[^1]);
        Assert.NotNull(hardwood.StandardLengthSource);

        Assert.True(Library.TryFindLumber("2x4", out LumberStock twoByFour));
        Assert.Equal(
            [Length.Feet(6), Length.Feet(8), Length.Feet(10), Length.Feet(12), Length.Feet(14), Length.Feet(16)],
            twoByFour.StandardLengths);

        Assert.NotNull(twoByFour.StandardLengthSource);
        Assert.NotEqual(hardwood.StandardLengthSource!.Standard, twoByFour.StandardLengthSource!.Standard);
        Assert.NotEqual(twoByFour.Source.Standard, twoByFour.StandardLengthSource.Standard);
    }

    /// <summary>
    /// A 4x4 and a 6x6 are stocked from 8 ft, not 6 ft — Standard No. 17 ¶260-a(h) and (i) start
    /// them there.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-001")]
    public void ThickerFramingStartsAtEightFeet()
    {
        Assert.True(Library.TryFindLumber("4x4", out LumberStock post));
        Assert.Equal(Length.Feet(8), post.StandardLengths[0]);

        Assert.True(Library.TryFindLumber("6x6", out LumberStock timber));
        Assert.Equal(Length.Feet(8), timber.StandardLengths[0]);

        Assert.True(Library.TryFindLumber("1x6", out LumberStock board));
        Assert.Equal(Length.Feet(6), board.StandardLengths[0]);
    }

    /// <summary>FF-N-105B §3.6.11.2, row by row.</summary>
    public static TheoryData<string, Length, double> NailRows => new()
    {
        { "2d", Length.Inches(1), 0.072 },
        { "3d", Length.Inches(1, 1, 4), 0.080 },
        { "4d", Length.Inches(1, 1, 2), 0.099 },
        { "5d", Length.Inches(1, 3, 4), 0.099 },
        { "6d", Length.Inches(2), 0.113 },
        { "7d", Length.Inches(2, 1, 4), 0.113 },
        { "8d", Length.Inches(2, 1, 2), 0.131 },
        { "9d", Length.Inches(2, 3, 4), 0.131 },
        { "10d", Length.Inches(3), 0.148 },
        { "12d", Length.Inches(3, 1, 4), 0.148 },
        { "16d", Length.Inches(3, 1, 2), 0.162 },
        { "20d", Length.Inches(4), 0.192 },
        { "30d", Length.Inches(4, 1, 2), 0.207 },
        { "40d", Length.Inches(5), 0.226 },
        { "50d", Length.Inches(5, 1, 2), 0.244 },
        { "60d", Length.Inches(6), 0.262 },
    };

    [Theory]
    [MemberData(nameof(NailRows))]
    [Trait("Feature", "MAT-003")]
    public void EachNailRowMatchesTheFederalSpecification(string name, Length length, double diameter)
    {
        Assert.True(Library.TryFind(name, out StockItem item), $"{name} is not in the shipped library.");
        FastenerStock nail = Assert.IsType<FastenerStock>(item);

        Assert.Equal(name, nail.PennySize);
        Assert.Equal(length, nail.FastenerLength);
        Assert.Equal(diameter, nail.ShankDiameterInches);
    }

    /// <summary>
    /// The penny size is a length convention and nothing else: 16d is 3 1/2 in, which is the
    /// figure a fastener schedule calls for. Nothing in the library reaches this number by
    /// arithmetic on the penny number.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-003")]
    public void SixteenPennyIsThreeAndAHalfInches()
    {
        Assert.True(Library.TryFind("16d", out StockItem item));
        FastenerStock nail = Assert.IsType<FastenerStock>(item);

        Assert.Equal(Length.Inches(3, 1, 2), nail.FastenerLength);
        Assert.Equal("16d — actual 3 1/2\" long, 0.162\" shank, FF-N-105B", nail.HoverText);
    }

    /// <summary>
    /// The nail table's citation says out loud that FF-N-105B was cancelled and that ASTM F1667
    /// succeeded it, because a reader has to know which document to check a row against.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-004")]
    public void TheNailCitationDeclaresTheDocumentsStatus()
    {
        Assert.True(Library.TryFind("16d", out StockItem nail));

        Assert.Contains("cancelled", nail.Source.Where, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("F1667", nail.Source.Where, StringComparison.Ordinal);
    }

    /// <summary>
    /// FF-N-105B §3.6.15, Type II style 14 finish nails, page 13, read as text from the PDF on 2026-09-24:
    /// the twelve rows of the table (S, L, D), listed here from the printed table.
    /// </summary>
    public static TheoryData<string, Length, double> FinishNailRows => new()
    {
        { "2d finish nail", Length.Inches(1), 0.058 },
        { "3d finish nail", Length.Inches(1, 1, 4), 0.067 },
        { "4d finish nail", Length.Inches(1, 1, 2), 0.072 },
        { "5d finish nail", Length.Inches(1, 3, 4), 0.072 },
        { "6d finish nail", Length.Inches(2), 0.092 },
        { "7d finish nail", Length.Inches(2, 1, 4), 0.092 },
        { "8d finish nail", Length.Inches(2, 1, 2), 0.099 },
        { "9d finish nail", Length.Inches(2, 3, 4), 0.099 },
        { "10d finish nail", Length.Inches(3), 0.113 },
        { "12d finish nail", Length.Inches(3, 1, 4), 0.113 },
        { "16d finish nail", Length.Inches(3, 1, 2), 0.120 },
        { "20d finish nail", Length.Inches(4), 0.135 },
    };

    [Theory]
    [MemberData(nameof(FinishNailRows))]
    [Trait("Feature", "MAT-003")]
    public void EachFinishNailRowMatchesTheFederalSpecification(string name, Length length, double diameter)
    {
        Assert.True(Library.TryFind(name, out StockItem item), $"{name} is not in the shipped library.");
        FastenerStock nail = Assert.IsType<FastenerStock>(item);

        Assert.Equal(("nail", length, diameter), (nail.Family, nail.FastenerLength, nail.ShankDiameterInches));
        Assert.Contains("§3.6.15", nail.Source.Where, StringComparison.Ordinal);
    }

    /// <summary>
    /// FF-N-105B §3.6.1, brads, page 8, read as text from the PDF on 2026-09-24: every row of the table
    /// (L, D, and S where one is printed beside it: 3d is 1-1/4 in x .080, 4d 1-1/2 x .099, ...).
    /// </summary>
    public static TheoryData<string, string, Length, double> BradRows => new()
    {
        { "Brad 3/8 in x .035", "", Length.Inches(0, 3, 8), 0.035 },
        { "Brad 1/2 in x .035", "", Length.Inches(0, 1, 2), 0.035 },
        { "Brad 1/2 in x .048", "", Length.Inches(0, 1, 2), 0.048 },
        { "Brad 5/8 in x .035", "", Length.Inches(0, 5, 8), 0.035 },
        { "Brad 5/8 in x .048", "", Length.Inches(0, 5, 8), 0.048 },
        { "Brad 3/4 in x .035", "", Length.Inches(0, 3, 4), 0.035 },
        { "Brad 3/4 in x .048", "", Length.Inches(0, 3, 4), 0.048 },
        { "Brad 3/4 in x .062", "", Length.Inches(0, 3, 4), 0.062 },
        { "Brad 7/8 in x .035", "", Length.Inches(0, 7, 8), 0.035 },
        { "Brad 7/8 in x .048", "", Length.Inches(0, 7, 8), 0.048 },
        { "Brad 7/8 in x .062", "", Length.Inches(0, 7, 8), 0.062 },
        { "Brad 1 in x .054", "", Length.Inches(1), 0.054 },
        { "Brad 1 in x .062", "", Length.Inches(1), 0.062 },
        { "Brad 1 in x .072", "", Length.Inches(1), 0.072 },
        { "Brad 1-1/4 in x .054", "", Length.Inches(1, 1, 4), 0.054 },
        { "Brad 1-1/4 in x .062", "", Length.Inches(1, 1, 4), 0.062 },
        { "Brad 1-1/4 in x .080", "3d", Length.Inches(1, 1, 4), 0.080 },
        { "Brad 1-1/2 in x .054", "", Length.Inches(1, 1, 2), 0.054 },
        { "Brad 1-1/2 in x .080", "", Length.Inches(1, 1, 2), 0.080 },
        { "Brad 1-1/2 in x .099", "4d", Length.Inches(1, 1, 2), 0.099 },
        { "Brad 1-3/4 in x .062", "", Length.Inches(1, 3, 4), 0.062 },
        { "Brad 1-3/4 in x .080", "", Length.Inches(1, 3, 4), 0.080 },
        { "Brad 1-3/4 in x .099", "5d", Length.Inches(1, 3, 4), 0.099 },
        { "Brad 2 in x .062", "", Length.Inches(2), 0.062 },
        { "Brad 2 in x .080", "", Length.Inches(2), 0.080 },
        { "Brad 2 in x .113", "6d", Length.Inches(2), 0.113 },
        { "Brad 2-1/4 in x .080", "", Length.Inches(2, 1, 4), 0.080 },
        { "Brad 2-1/4 in x .113", "7d", Length.Inches(2, 1, 4), 0.113 },
        { "Brad 2-1/2 in x .080", "", Length.Inches(2, 1, 2), 0.080 },
        { "Brad 2-1/2 in x .131", "8d", Length.Inches(2, 1, 2), 0.131 },
        { "Brad 2-3/4 in x .131", "9d", Length.Inches(2, 3, 4), 0.131 },
        { "Brad 3 in x .148", "10d", Length.Inches(3), 0.148 },
        { "Brad 3-1/4 in x .148", "12d", Length.Inches(3, 1, 4), 0.148 },
        { "Brad 3-1/2 in x .162", "16d", Length.Inches(3, 1, 2), 0.162 },
        { "Brad 4 in x .192", "20d", Length.Inches(4), 0.192 },
        { "Brad 4-1/2 in x .207", "30d", Length.Inches(4, 1, 2), 0.207 },
        { "Brad 5 in x .225", "40d", Length.Inches(5), 0.225 },
        { "Brad 5-1/2 in x .244", "50d", Length.Inches(5, 1, 2), 0.244 },
        { "Brad 6 in x .262", "60d", Length.Inches(6), 0.262 },
    };

    [Theory]
    [MemberData(nameof(BradRows))]
    [Trait("Feature", "MAT-003")]
    public void SampledBradRowsMatchTheFederalSpecification(string name, string designation, Length length, double diameter)
    {
        Assert.True(Library.TryFind(name, out StockItem item), $"{name} is not in the shipped library.");
        FastenerStock brad = Assert.IsType<FastenerStock>(item);

        Assert.Equal(("brad", designation, length, diameter), (brad.Family, brad.PennySize, brad.FastenerLength, brad.ShankDiameterInches));
    }

    /// <summary>The brad table has 39 rows (14 + 13 + 12 in its three column blocks), and each row is cited to its section.</summary>
    [Fact]
    [Trait("Feature", "MAT-004")]
    public void TheBradTableIsEveryRowOfTheSourceAndCitesIt()
    {
        FastenerStock[] brads = [.. Library.Items.OfType<FastenerStock>().Where(item => item.Family == "brad")];

        Assert.Equal(39, brads.Length);
        Assert.All(brads, brad => Assert.StartsWith("§3.6.1 table", brad.Derivation, StringComparison.Ordinal));
        Assert.Equal(12, Library.Items.OfType<FastenerStock>().Count(item => item.Name.EndsWith("finish nail", StringComparison.Ordinal)));
    }

    /// <summary>A table with no citation, or an empty one, is never loaded: every fastener row names what it was read from.</summary>
    [Fact]
    [Trait("Feature", "MAT-004")]
    public void EveryFastenerRowHasACompleteCitationAndADerivation()
    {
        FastenerStock[] all = [.. Library.Items.OfType<FastenerStock>()];

        Assert.Equal(16 + 12 + 39, all.Length);
        Assert.All(all, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Derivation), item.Name);
            Assert.False(string.IsNullOrWhiteSpace(item.Source.Designation), item.Name);
            Assert.False(string.IsNullOrWhiteSpace(item.Source.Where), item.Name);
            Assert.NotEqual(default, item.Source.Retrieved);
        });
    }
}

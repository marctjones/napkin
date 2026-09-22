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
}

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Core.Materials.Tests;

/// <summary>
/// The shipped panel tables against PS 1-19 Table 10 and PS 2-18 Table 1.
/// </summary>
/// <remarks>
/// <para>
/// Sources read in the task that wrote these rows: Voluntary Product Standard PS 1-19,
/// <em>Structural Plywood</em> (U.S. Department of Commerce / NIST, December 2019), §5.4, §5.10.1,
/// §5.10.2 and Table 10 "Plywood thickness requirements" p. 39, retrieved 2026-09-21 from
/// <c>https://www.wbdg.org/FFC/NIST/nist_ps1_2019.pdf</c>; and Voluntary Product Standard PS 2-18,
/// <em>Performance Standard for Wood Structural Panels</em> (effective 30 March 2019, published by
/// APA as Form No. S350H), §5.2.1.1, §5.2.1.2, Table 1 p. 7 and Appendix D Table D1 p. 54,
/// retrieved 2026-09-21 from
/// <c>https://tolko.com/wp-content/uploads/2019/11/APA-PS-2-18-Performance-Standard-for-Wood-Structural-Panels.pdf</c>.
/// </para>
/// <para>
/// <strong>What those standards actually say, which is not the folklore.</strong> Neither states
/// a nominal-to-actual mapping. Both designate a panel by a <em>Performance Category</em> and give
/// only a minimum and a maximum thickness it may measure, and both list 23/32 and 3/4 as separate
/// Performance Categories with separate ranges. PS 1-19 Table 10, sanded grades: 23/32 CAT is
/// 0.703 to 0.734 in, 3/4 CAT is 0.734 to 0.766 in. PS 2-18 Table 1: 23/32 CAT is 0.688 to
/// 0.750 in, 3/4 CAT is 0.719 to 0.781 in. So "3/4 plywood is really 23/32" is not a statement
/// either standard makes, and this library does not make it either.
/// </para>
/// </remarks>
public sealed class SheetGoodsGoldenTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    /// <summary>
    /// Every panel row: its Performance Category read back as the exact length the category names,
    /// and the 48 in x 96 in sheet.
    /// </summary>
    public static TheoryData<string, string, Length> Rows => new()
    {
        { "1/4 plywood", "1/4", Length.Inches(0, 1, 4) },
        { "3/8 plywood", "3/8", Length.Inches(0, 3, 8) },
        { "7/16 plywood", "7/16", Length.Inches(0, 7, 16) },
        { "15/32 plywood", "15/32", Length.Inches(0, 15, 32) },
        { "1/2 plywood", "1/2", Length.Inches(0, 1, 2) },
        { "19/32 plywood", "19/32", Length.Inches(0, 19, 32) },
        { "5/8 plywood", "5/8", Length.Inches(0, 5, 8) },
        { "23/32 plywood", "23/32", Length.Inches(0, 23, 32) },
        { "3/4 plywood", "3/4", Length.Inches(0, 3, 4) },
        { "1-1/8 plywood", "1-1/8", Length.Inches(1, 1, 8) },

        { "3/8 osb", "3/8", Length.Inches(0, 3, 8) },
        { "7/16 osb", "7/16", Length.Inches(0, 7, 16) },
        { "15/32 osb", "15/32", Length.Inches(0, 15, 32) },
        { "19/32 osb", "19/32", Length.Inches(0, 19, 32) },
        { "23/32 osb", "23/32", Length.Inches(0, 23, 32) },
        { "3/4 osb", "3/4", Length.Inches(0, 3, 4) },
        { "1-1/8 osb", "1-1/8", Length.Inches(1, 1, 8) },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    [Trait("Feature", "MAT-002")]
    public void EachPanelCarriesItsPerformanceCategoryAndSheetSize(string name, string category, Length thickness)
    {
        Assert.True(Library.TryFind(name, out StockItem item), $"{name} is not in the shipped library.");
        PanelStock panel = Assert.IsType<PanelStock>(item);

        Assert.Equal(category, panel.PerformanceCategory);
        Assert.Equal(thickness, panel.Thickness);
        Assert.Equal(Length.Feet(4), panel.SheetWidth);
        Assert.Equal(Length.Feet(8), panel.SheetLength);
    }

    /// <summary>
    /// The 23/32 value DESIGN.md §5.6 quotes in prose, re-derived from the standard: it is a
    /// Performance Category of its own — 736 units, which is 23/32 in exactly on the 1/1024 in
    /// grid — and not the measured thickness of a 3/4 panel.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-002")]
    public void TwentyThreeThirtySecondsIsItsOwnCategoryAndIsExactOnTheGrid()
    {
        Assert.True(Library.TryFind("23/32 plywood", out StockItem item));
        PanelStock panel = Assert.IsType<PanelStock>(item);

        Assert.Equal(736, panel.Thickness.Units);

        Assert.True(Library.TryFind("3/4 plywood", out StockItem threeQuarterItem));
        PanelStock threeQuarter = Assert.IsType<PanelStock>(threeQuarterItem);

        Assert.NotEqual(panel.Thickness, threeQuarter.Thickness);
        Assert.Equal(768, threeQuarter.Thickness.Units);
    }

    /// <summary>
    /// The standard states a range, not a number, so the row says so in its derivation rather than
    /// carrying a false exact thickness. PS 1-19's 23/32 sanded limits are 0.703 to 0.734 in.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-004")]
    public void APanelRowRecordsTheRangeTheStandardAllows()
    {
        Assert.True(Library.TryFind("23/32 plywood", out StockItem panel));

        Assert.Contains("0.703", panel.Derivation, StringComparison.Ordinal);
        Assert.Contains("0.734", panel.Derivation, StringComparison.Ordinal);
        Assert.Contains("Table 10", panel.Source.Where, StringComparison.Ordinal);
    }

    /// <summary>A sheet is not cut to length, so it carries no stock-length list.</summary>
    [Fact]
    [Trait("Feature", "MAT-002")]
    public void ASheetHasNoStockLengthList()
    {
        Assert.True(Library.TryFind("23/32 osb", out StockItem panel));

        Assert.Empty(panel.StandardLengths);
        Assert.Null(panel.StandardLengthSource);
    }

    /// <summary>The hover a person reads before placing a sheet.</summary>
    [Fact]
    [Trait("Feature", "MAT-002")]
    public void TheHoverTextShowsTheThicknessAndTheSheet()
    {
        Assert.True(Library.TryFind("23/32 plywood", out StockItem panel));

        Assert.Equal("23/32\" thick, 4'-0\" x 8'-0\" sheet", panel.ActualSizeText);
        Assert.Equal("PS 1-19", panel.Source.ShortForm);
    }
}

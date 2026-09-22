using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Core.Materials.Tests;

/// <summary>
/// The shipped softwood table, row by row, against the cells of PS 20-20 Table 3.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Every expected value in this file was read out of the standard, not out of napkin.</strong>
/// The source is Voluntary Product Standard PS 20-20, <em>American Softwood Lumber Standard</em>
/// (U.S. Department of Commerce / NIST for the American Lumber Standard Committee, January 2020),
/// Table 3, "Nominal and minimum-dressed sizes of boards, dimension, and timbers", page 16,
/// retrieved 2026-09-21 from <c>https://www.alsc.org/greenbook%20collection/ps20.pdf</c>. The Dry
/// columns are the ones carried; the same table's Green columns are not
/// (<see cref="TheGreenColumnIsNotWhatIsCarried"/> pins that choice).
/// </para>
/// <para>
/// These are the cells, transcribed once here so the test is checkable line by line:
/// </para>
/// <para>
/// <em>Boards</em> — thicknesses, nominal to minimum dressed dry: 3/8→5/16, 1/2→7/16, 5/8→9/16,
/// 3/4→5/8, 1→3/4, 1-1/4→1, 1-1/2→1-1/4. Widths: 2→1-1/2, 3→2-1/2, 4→3-1/2, 5→4-1/2, 6→5-1/2,
/// 7→6-1/2, 8→7-1/4, 9→8-1/4, 10→9-1/4, 11→10-1/4, 12→11-1/4, 14→13-1/4, 16→15-1/4.
/// </para>
/// <para>
/// <em>Dimension</em> — thicknesses: 2→1-1/2, 2-1/2→2, 3→2-1/2, 3-1/2→3, 4→3-1/2, 4-1/2→4.
/// Widths: 2→1-1/2, 2-1/2→2, 3→2-1/2, 3-1/2→3, 4→3-1/2, 4-1/2→4, 5→4-1/2, 6→5-1/2, 8→7-1/4,
/// 10→9-1/4, 12→11-1/4, 14→13-1/4, 16→15-1/4.
/// </para>
/// <para>
/// <em>Timbers</em> — stated as an amount off the nominal size, for thicknesses and widths alike:
/// 5 &amp; 6 → 13 mm (1/2 in) off dry; 7-15 → 19 mm (3/4 in) off dry; ≥16 → 25 mm (1 in) off dry.
/// </para>
/// </remarks>
public sealed class SoftwoodLumberGoldenTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    /// <summary>
    /// Every row of the shipped table against the standard's cell for it. The expected values are
    /// written as <see cref="Length.Inches"/> calls so that nothing in the test can be satisfied
    /// by reading the data file back.
    /// </summary>
    public static TheoryData<string, Length, Length, SizeClass> Rows => new()
    {
        // Boards: nominal 1 in thick -> 3/4 in dry, for every width.
        { "1x2", Length.Inches(0, 3, 4), Length.Inches(1, 1, 2), SizeClass.Board },
        { "1x3", Length.Inches(0, 3, 4), Length.Inches(2, 1, 2), SizeClass.Board },
        { "1x4", Length.Inches(0, 3, 4), Length.Inches(3, 1, 2), SizeClass.Board },
        { "1x6", Length.Inches(0, 3, 4), Length.Inches(5, 1, 2), SizeClass.Board },
        { "1x8", Length.Inches(0, 3, 4), Length.Inches(7, 1, 4), SizeClass.Board },
        { "1x10", Length.Inches(0, 3, 4), Length.Inches(9, 1, 4), SizeClass.Board },
        { "1x12", Length.Inches(0, 3, 4), Length.Inches(11, 1, 4), SizeClass.Board },

        // Dimension: nominal 2 in thick -> 1-1/2 in dry; 3 in -> 2-1/2 in; 4 in -> 3-1/2 in.
        { "2x2", Length.Inches(1, 1, 2), Length.Inches(1, 1, 2), SizeClass.Dimension },
        { "2x3", Length.Inches(1, 1, 2), Length.Inches(2, 1, 2), SizeClass.Dimension },
        { "2x4", Length.Inches(1, 1, 2), Length.Inches(3, 1, 2), SizeClass.Dimension },
        { "2x6", Length.Inches(1, 1, 2), Length.Inches(5, 1, 2), SizeClass.Dimension },
        { "2x8", Length.Inches(1, 1, 2), Length.Inches(7, 1, 4), SizeClass.Dimension },
        { "2x10", Length.Inches(1, 1, 2), Length.Inches(9, 1, 4), SizeClass.Dimension },
        { "2x12", Length.Inches(1, 1, 2), Length.Inches(11, 1, 4), SizeClass.Dimension },
        { "3x3", Length.Inches(2, 1, 2), Length.Inches(2, 1, 2), SizeClass.Dimension },
        { "4x4", Length.Inches(3, 1, 2), Length.Inches(3, 1, 2), SizeClass.Dimension },
        { "4x6", Length.Inches(3, 1, 2), Length.Inches(5, 1, 2), SizeClass.Dimension },

        // Timbers: 6 - 1/2 = 5-1/2; 8 - 3/4 = 7-1/4.
        { "6x6", Length.Inches(5, 1, 2), Length.Inches(5, 1, 2), SizeClass.Timber },
        { "6x8", Length.Inches(5, 1, 2), Length.Inches(7, 1, 4), SizeClass.Timber },
        { "8x8", Length.Inches(7, 1, 4), Length.Inches(7, 1, 4), SizeClass.Timber },

        // Decking, which PS 20-20 §3.4.1 grades as boards: nominal 1-1/4 in thick -> 1 in dry.
        { "5/4x4", Length.Inches(1), Length.Inches(3, 1, 2), SizeClass.Board },
        { "5/4x6", Length.Inches(1), Length.Inches(5, 1, 2), SizeClass.Board },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    [Trait("Feature", "MAT-001")]
    public void EachRowMatchesTheCellOfPs20Table3(string name, Length thickness, Length width, SizeClass sizeClass)
    {
        Assert.True(Library.TryFindLumber(name, out LumberStock lumber), $"{name} is not in the shipped library.");

        Assert.Equal(thickness, lumber.Thickness);
        Assert.Equal(width, lumber.Width);
        Assert.Equal(sizeClass, lumber.SizeClass);
    }

    /// <summary>
    /// The two sizes DESIGN.md §5.6 names in prose, checked against the standard rather than
    /// against the prose: a 2x4 is 1-1/2 in x 3-1/2 in and a 4x4 is 3-1/2 in square.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-001")]
    public void ThePrimarySourceSettlesTheSizesTheDesignDocumentQuotes()
    {
        Assert.True(Library.TryFindLumber("2x4", out LumberStock twoByFour));
        Assert.Equal(1536, twoByFour.Thickness.Units);
        Assert.Equal(3584, twoByFour.Width.Units);

        Assert.True(Library.TryFindLumber("4x4", out LumberStock fourByFour));
        Assert.Equal(Length.Inches(3, 1, 2), fourByFour.Thickness);
        Assert.Equal(fourByFour.Thickness, fourByFour.Width);
    }

    /// <summary>
    /// The nominal name's own numbers are stored as exact lengths too, because a board-foot
    /// takeoff is computed from the nominal size and not the dressed one (PS 20-20 §2.2, "Board
    /// measure": the number of board feet is obtained "by multiplying the nominal thickness in
    /// inches or fraction of an inch by the nominal width in feet by the length in feet"). A 5/4
    /// deck board's nominal thickness is 1-1/4 in, not 1 in.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-001")]
    public void NominalSizesAreCarriedExactlyForTheTakeoff()
    {
        Assert.True(Library.TryFindLumber("5/4x6", out LumberStock deckBoard));
        Assert.Equal(Length.Inches(1, 1, 4), deckBoard.NominalThickness);
        Assert.Equal(Length.Inches(6), deckBoard.NominalWidth);
        Assert.Equal(Length.Inches(1), deckBoard.Thickness);
    }

    /// <summary>
    /// Table 3's Green columns are a different set of numbers — a green 2x4 is 1-9/16 in x
    /// 3-9/16 in, not 1-1/2 in x 3-1/2 in. The library carries the Dry columns and says so; this
    /// test fails the day a green value is transcribed into a dry row by accident.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-004")]
    public void TheGreenColumnIsNotWhatIsCarried()
    {
        Assert.True(Library.TryFindLumber("2x4", out LumberStock twoByFour));

        Assert.NotEqual(Length.Inches(1, 9, 16), twoByFour.Thickness);
        Assert.NotEqual(Length.Inches(3, 9, 16), twoByFour.Width);
        Assert.Contains("Dry", twoByFour.Source.Where, StringComparison.Ordinal);
    }

    /// <summary>
    /// A 4x4 is dimension lumber, not a timber: PS 20-20 §3.4.2 puts everything from nominal 2 in
    /// up to but not including nominal 5 in thick in the dimension class, and §3.4.3 starts
    /// timbers at nominal 5 in. Reading a 4x4 out of the timbers block would give 4 - 3/4 = 3-1/4
    /// in, which is wrong.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-001")]
    public void AFourByFourIsDimensionLumberAndNotATimber()
    {
        Assert.True(Library.TryFindLumber("4x4", out LumberStock fourByFour));

        Assert.Equal(SizeClass.Dimension, fourByFour.SizeClass);
        Assert.NotEqual(Length.Inches(3, 1, 4), fourByFour.Thickness);
    }

    /// <summary>
    /// The hover the picker shows before an item is placed (issue #7's decided design,
    /// <c>GUI-CUT-02</c>): the name, the actual size, and the standard it came from.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-001")]
    public void TheHoverTextShowsTheActualSizeAndTheStandard()
    {
        Assert.True(Library.TryFindLumber("2x4", out LumberStock twoByFour));

        Assert.Equal("1 1/2\" x 3 1/2\"", twoByFour.ActualSizeText);
        Assert.Equal("2x4 — actual 1 1/2\" x 3 1/2\", PS 20-20", twoByFour.HoverText);
    }
}

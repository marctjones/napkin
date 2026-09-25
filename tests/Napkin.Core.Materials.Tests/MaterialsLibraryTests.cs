using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Core.Materials.Tests;

/// <summary>
/// The lookup API and the invariants that hold across every shipped table at once.
/// </summary>
public sealed class MaterialsLibraryTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    /// <summary>
    /// How a person actually types a nominal size. Every one of these has to reach the same 2x4.
    /// </summary>
    [Theory]
    [InlineData("2x4")]
    [InlineData("2X4")]
    [InlineData("2 x 4")]
    [InlineData("2 X 4")]
    [InlineData(" 2x4 ")]
    [InlineData("2×4")]
    [InlineData("2 × 4")]
    [InlineData("2*4")]
    [InlineData("2 by 4")]
    [InlineData("2by4")]
    [Trait("Feature", "MAT-001")]
    public void EveryWayAPersonWritesATwoByFourFindsTheSameItem(string typed)
    {
        Assert.True(Library.TryFind(typed, out StockItem item), $"\"{typed}\" found nothing.");

        Assert.Equal("2x4", item.Name);
        Assert.Equal("2x4", NominalName.Normalize(typed));
    }

    /// <summary>
    /// What normalisation must leave alone: the slash and the hyphen that carry meaning inside a
    /// name. "5/4x6" is not "54x6", and a 1-1/8 panel is not an 11/8 one.
    /// </summary>
    [Theory]
    [InlineData("5/4x6", "5/4x6")]
    [InlineData("5/4 X 6", "5/4x6")]
    [InlineData("1-1/8 Plywood", "1-1/8plywood")]
    [InlineData("23/32 OSB", "23/32osb")]
    [InlineData("16D", "16d")]
    [InlineData("8/4", "8/4")]
    [Trait("Feature", "MAT-001")]
    public void NormalisationKeepsTheCharactersThatCarryMeaning(string typed, string expected)
    {
        Assert.Equal(expected, NominalName.Normalize(typed));
        Assert.True(Library.TryFind(typed, out _), $"\"{typed}\" found nothing.");
    }

    /// <summary>
    /// Text that is not a name finds nothing rather than landing on something by accident. A bare
    /// separator normalises to "x", which is a legal key shape and simply names no item.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("×")]
    [InlineData("2x")]
    [InlineData("plywood")]
    [Trait("Feature", "MAT-001")]
    public void TextThatIsNotAStockNameFindsNothing(string? typed)
    {
        Assert.False(Library.TryFind(typed, out _));
        Assert.False(Library.TryFind(StockCategory.DimensionalLumber, typed, out _));
    }

    /// <summary>
    /// Nothing at all is not a name, so two of them are not the same name — which keeps an empty
    /// cell in a future data file from matching another empty cell.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "MAT-001")]
    public void NothingIsNotAName(string? typed)
    {
        Assert.Empty(NominalName.Normalize(typed));
        Assert.False(NominalName.AreSame(typed, typed));
    }

    [Fact]
    [Trait("Feature", "MAT-001")]
    public void TwoSpellingsOfOneNameAreTheSameNameAndTwoNamesAreNot()
    {
        Assert.True(NominalName.AreSame("2 X 4", "2x4"));
        Assert.False(NominalName.AreSame("2x4", "2x6"));
    }

    [Fact]
    [Trait("Feature", "MAT-001")]
    public void ANameThatIsNotStockedFindsNothingRatherThanGuessing()
    {
        Assert.False(Library.TryFind("2x5", out _));
        Assert.False(Library.TryFindLumber("23/32 plywood", out _));
        Assert.False(Library.TryFind(StockCategory.SheetGood, "2x4", out _));
        Assert.True(Library.TryFind(StockCategory.DimensionalLumber, "2x4", out _));
    }

    /// <summary>
    /// The picker's two levels: a handful of categories at the top, a text list inside the one
    /// that is opened (issue #7). Every category the picker offers holds something.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-001")]
    public void EveryCategoryTheLibraryOffersHoldsSomething()
    {
        Assert.Equal(
            [
                StockCategory.DimensionalLumber,
                StockCategory.SheetGood,
                StockCategory.HardwoodBoard,
                StockCategory.Decking,
                StockCategory.Fastener,
            ],
            Library.Categories);

        foreach (StockCategory category in Library.Categories)
        {
            Assert.NotEmpty(Library.InCategory(category));
        }
    }

    /// <summary>
    /// <c>MAT-004</c>: a table whose citation is missing or empty fails the load, so by the time
    /// anything is in the library every row names its source — precisely enough to check.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-004")]
    public void EveryTableAndEveryRowCarriesACheckableCitation()
    {
        Assert.NotEmpty(Library.Tables);

        foreach (StockTable table in Library.Tables)
        {
            AssertCitation(table.Source, table.Id);
            Assert.NotEmpty(table.Items);
        }

        foreach (StockItem item in Library.Items)
        {
            AssertCitation(item.Source, item.Name);

            Assert.False(
                string.IsNullOrWhiteSpace(item.Derivation),
                $"{item.Name} does not say how its numbers come from the cited cell.");

            if (!item.StandardLengths.IsEmpty)
            {
                Assert.NotNull(item.StandardLengthSource);
                AssertCitation(item.StandardLengthSource!, $"{item.Name} lengths");
                Assert.False(string.IsNullOrWhiteSpace(item.StandardLengthDerivation));
            }
        }
    }

    /// <summary>
    /// Every dimension the library carries is exact at 1/64" — the finest mark on a tape — so
    /// nothing the picker, the cut list or the shopping list prints is a rounded value shown
    /// without saying so.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-004")]
    public void EveryCarriedDimensionIsExactAtATapeMeasurePrecision()
    {
        foreach (StockItem item in Library.Items)
        {
            foreach (Length length in Dimensions(item))
            {
                Assert.True(length > Length.Zero, $"{item.Name} carries a non-positive dimension.");
                Assert.True(
                    length.Format(StockItem.SizeFormat).IsExact,
                    $"{item.Name} carries {length}, which 1/64\" cannot show exactly.");
            }
        }
    }

    /// <summary>The hover the picker shows names the item, its size and its standard.</summary>
    [Fact]
    [Trait("Feature", "MAT-004")]
    public void EveryItemCanSayWhatItIsInOneLine()
    {
        foreach (StockItem item in Library.Items)
        {
            Assert.StartsWith($"{item.Name} — actual ", item.HoverText, StringComparison.Ordinal);
            Assert.EndsWith($", {item.Source.Designation}", item.HoverText, StringComparison.Ordinal);
            Assert.Matches("retrieved 2026-09-(21|24)", item.Source.ToString());
        }
    }

    /// <summary>Adding a size is a data change: the shipped tables come through the public reader.</summary>
    [Fact]
    [Trait("Feature", "MAT-005")]
    public void TheShippedTablesLoadThroughThePublicReader()
    {
        MaterialsLoaded loaded = Assert.IsType<MaterialsLoaded>(MaterialsLibrary.LoadShipped());

        Assert.Equal(8, loaded.Library.Tables.Length);
        Assert.Equal(Library.Items.Length, loaded.Library.Items.Length);
        Assert.All(loaded.Library.Tables, table => Assert.EndsWith(".json", table.File, StringComparison.Ordinal));
    }

    private static IEnumerable<Length> Dimensions(StockItem item)
    {
        IEnumerable<Length> own = item switch
        {
            LumberStock lumber => [lumber.NominalThickness, lumber.NominalWidth, lumber.Thickness, lumber.Width],
            PanelStock panel => [panel.Thickness, panel.SheetWidth, panel.SheetLength],
            HardwoodStock hardwood => [hardwood.RoughThickness, hardwood.SurfacedTwoSides],
            FastenerStock fastener => [fastener.FastenerLength],
            _ => throw new InvalidOperationException($"Unknown stock kind {item.GetType().Name}."),
        };

        return own.Concat(item.StandardLengths);
    }

    private static void AssertCitation(Citation citation, string what)
    {
        Assert.False(string.IsNullOrWhiteSpace(citation.Designation), $"{what} has no designation.");
        Assert.False(string.IsNullOrWhiteSpace(citation.Standard), $"{what} names no standard.");
        Assert.False(string.IsNullOrWhiteSpace(citation.Publisher), $"{what} names no publisher.");
        Assert.False(string.IsNullOrWhiteSpace(citation.Where), $"{what} does not say where in the source.");
        Assert.StartsWith("https://", citation.Url, StringComparison.Ordinal);
        Assert.True(citation.Retrieved > new DateOnly(2020, 1, 1), $"{what} has no retrieval date.");
    }
}

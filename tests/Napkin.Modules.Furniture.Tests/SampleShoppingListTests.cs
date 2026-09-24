using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The stocked bench's shopping list against the one worked out by hand in
/// <c>samples/stocked-bench.expected.json</c> — stock counts, board lengths and takeoff totals, each
/// with its derivation. Never regenerated from napkin's output (<c>samples/README.md</c>).
/// </summary>
public sealed class SampleShoppingListTests
{
    private const string Fixture = "stocked-bench";

    private static ImmutableArray<CutListRow> CutRows()
    {
        LoadResult result = SceneReader.ReadFile(ExpectedFixture.ScenePath(Fixture));
        return CutList.Of(Assert.IsType<Loaded>(result).Sketch, MaterialsLibrary.Shipped);
    }

    private static JsonElement Expected()
    {
        using FileStream stream = File.OpenRead(Path.Combine(ExpectedFixture.Directory, $"{Fixture}.expected.json"));
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    [Fact]
    [Trait("Feature", "CUT-005")]
    [Trait("Feature", "CUT-006")]
    public void The_stocked_benchs_shopping_list_is_the_one_worked_out_by_hand()
    {
        ImmutableArray<ShoppingListRow> rows = ShoppingList.Of(CutRows());
        JsonElement[] want = [.. Expected().GetProperty("shoppingList").EnumerateArray()];

        Assert.Equal(want.Length, rows.Length);

        for (int i = 0; i < rows.Length; i++)
        {
            ShoppingListRow row = rows[i];
            JsonElement expected = want[i];

            Assert.Equal(expected.GetProperty("material").GetString(), row.Material);
            Assert.Equal(expected.GetProperty("species").GetString(), row.Species);
            Assert.Equal(expected.GetProperty("for").GetString(), row.For);
            Assert.Equal(expected.GetProperty("count").GetInt32(), row.Count);
            Assert.Equal(expected.GetProperty("sheets").GetInt32(), row.Sheets);
            Assert.Equal(
                expected.GetProperty("boards").EnumerateArray().Select(board => new BoardsOfLength(
                    new Length(board.GetProperty("lengthUnits").GetInt64()),
                    board.GetProperty("count").GetInt32())),
                row.Boards);

            // The exact numerators, compared exactly, before any rounding.
            Assert.Equal(BigInteger.Parse(expected.GetProperty("boughtCubicUnits").GetRawText()), (BigInteger)row.BoughtCubicUnits);
            Assert.Equal(BigInteger.Parse(expected.GetProperty("usedCubicUnits").GetRawText()), (BigInteger)row.UsedCubicUnits);

            Assert.Equal(expected.GetProperty("buy").GetString(), row.BuyText);
            Assert.Equal(expected.GetProperty("boardFeetBought").GetString(), row.BoughtText);
            Assert.Equal(expected.GetProperty("boardFeetUsed").GetString(), row.UsedText);
            Assert.Equal(expected.GetProperty("waste").GetString(), row.WasteText);
            Assert.Equal(expected.GetProperty("note").GetString(), row.Note);
            Assert.False(string.IsNullOrWhiteSpace(expected.GetProperty("derivation").GetString()));
        }
    }

    [Fact]
    [Trait("Feature", "CUT-005")]
    public void Several_parts_from_one_board_make_the_two_lists_visibly_differ()
    {
        // The cut list has four 1x4 pieces on two rows; the shopping list has one 1x4 line of two
        // boards. That is the difference issue #9 asks a fixture to show.
        ImmutableArray<CutListRow> cut = CutRows();
        ImmutableArray<ShoppingListRow> shop = ShoppingList.Of(cut);

        Assert.Equal(2, cut.Count(row => row.Material == "1x4"));
        Assert.Equal(4, cut.Where(row => row.Material == "1x4").Sum(row => row.Quantity));

        ShoppingListRow oneByFour = Assert.Single(shop, row => row.Material == "1x4");
        Assert.Equal(2, oneByFour.Count);
        Assert.Equal(5, cut.Length);
        Assert.Equal(3, shop.Length);
    }

    [Fact]
    [Trait("Feature", "CUT-005")]
    public void The_stocked_benchs_csv_is_the_one_worked_out_by_hand()
    {
        string[] want = [.. Expected().GetProperty("shoppingListCsv").EnumerateArray().Select(line => line.GetString()!)];

        Assert.Equal(string.Join("\n", want) + "\n", ShoppingListCsv.ToCsv(ShoppingList.Of(CutRows())));
    }
}

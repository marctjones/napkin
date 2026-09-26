using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>An estimate from prices the person entered (#141). The prices here are typed-in test values, not facts.</summary>
public class ShoppingCostTests
{
    private static ImmutableArray<ShoppingListRow> StockedBench()
    {
        Sketch sketch = Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath("stocked-bench"))).Sketch;
        return ShoppingList.Of(CutList.Of(sketch, MaterialsLibrary.Shipped));
    }

    [Fact]
    public void EveryBoughtLineIsPricedByBoardOfItsLengthOrBySheet()
    {
        // The stocked bench buys one 12' 1x4, one 14' 2x4 and one sheet of 3/4 plywood.
        Dictionary<PriceKey, decimal> prices = new()
        {
            [new PriceKey("1x4", Length.Feet(12))] = 9.98m,
            [new PriceKey("2x4", Length.Feet(14))] = 11.47m,
            [new PriceKey("3/4 plywood", null)] = 54m,
        };

        ImmutableArray<CostLine> lines = ShoppingCost.Lines(StockedBench(), prices);

        Assert.Equal(["1x4, 12'-0\"", "2x4, 14'-0\"", "3/4 plywood, sheet"], lines.Select(line => line.What));
        Assert.Equal([9.98m, 11.47m, 54.00m], lines.Select(line => line.Cost!.Value));
        Assert.Equal("Estimate: 75.45, from the prices you entered.", ShoppingCost.Summary(lines));
    }

    [Fact]
    public void AnUnpricedLineIsLeftOutOfTheTotalAndSaid()
    {
        Dictionary<PriceKey, decimal> prices = new() { [new PriceKey("3/4 plywood", null)] = 54m };

        ImmutableArray<CostLine> lines = ShoppingCost.Lines(StockedBench(), prices);

        Assert.Equal("Estimate: 54.00 for 1 of 3 lines — 2 have no price.", ShoppingCost.Summary(lines));
        Assert.Equal("No estimate yet: enter a price for each line to buy.", ShoppingCost.Summary(ShoppingCost.Lines(StockedBench(), new Dictionary<PriceKey, decimal>())));
        Assert.Null(ShoppingCost.Summary([]));
        Assert.Equal(
            "Estimate: 21.45 for 2 of 3 lines — 1 has no price.",
            ShoppingCost.Summary(ShoppingCost.Lines(StockedBench(), new Dictionary<PriceKey, decimal> { [new PriceKey("2x4", Length.Feet(14))] = 11.47m, [new PriceKey("1x4", Length.Feet(12))] = 9.98m })));
    }

    [Fact]
    public void TheCsvCarriesTheEstimateOnlyWhenAPriceWasEntered()
    {
        ImmutableArray<ShoppingListRow> rows = StockedBench();
        string plain = ShoppingListCsv.ToCsv(rows, null);

        Assert.Equal(plain, ShoppingListCsv.ToCsv(rows, null, ShoppingCost.Lines(rows, new Dictionary<PriceKey, decimal>())));

        string priced = ShoppingListCsv.ToCsv(rows, null, ShoppingCost.Lines(rows, new Dictionary<PriceKey, decimal> { [new PriceKey("3/4 plywood", null)] = 54m }));
        Assert.StartsWith(plain, priced, StringComparison.Ordinal);
        Assert.EndsWith(
            "\nWhat,Quantity,Price each,Cost\n\"1x4, 12'-0\"\"\",1,,\n\"2x4, 14'-0\"\"\",1,,\n\"3/4 plywood, sheet\",1,54.00,54.00\nEstimate: 54.00 for 1 of 3 lines — 2 have no price.\n",
            priced,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HardwoodIsPricedByTheBoardFootItBuys()
    {
        // 2.5 bd ft of walnut at an entered 12.40 a board foot: 31.00.
        Int128 twoAndAHalf = BoardFeet.CubicUnitsPerBoardFoot * 5 / 2;
        ShoppingListRow walnut = new("Walnut", string.Empty, ShoppingListKind.BoardFeet, "Top", [], 0, twoAndAHalf, twoAndAHalf, string.Empty, null);
        ShoppingListRow nothing = walnut with { Kind = ShoppingListKind.NothingToBuy };

        CostLine line = Assert.Single(ShoppingCost.Lines([walnut, nothing], new Dictionary<PriceKey, decimal> { [new PriceKey("Walnut", null)] = 12.40m }));

        Assert.Equal(("Walnut, bd ft", 2.5m, 31.00m), (line.What, line.Quantity, line.Cost!.Value));
        Assert.Equal("1.50", ShoppingCost.Money(1.5m));
    }
}

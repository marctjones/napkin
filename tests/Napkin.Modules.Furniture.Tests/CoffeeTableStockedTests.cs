using System.Collections.Immutable;
using System.Text.Json.Nodes;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// M3's literal promise (PLAN.md): the coffee table, top and four legs and aprons, with materials
/// assigned, gives a cut list with a 2x4 at its true size and a shopping list with board feet and
/// sheet counts. <c>samples/coffee-table.scene.json</c> names no stock (its dimensions are its own,
/// and it is not edited), so this works on a COPY that assigns stock: 3/4 plywood to the top, 2x4
/// to the legs (re-cut to the 2x4's 1 1/2" x 3 1/2" section) and 1x4 to the aprons. The expected
/// numbers are worked out by hand below, independently of napkin's output.
/// </summary>
public sealed class CoffeeTableStockedTests
{
    private static ImmutableArray<CutListRow> StockedRows()
    {
        JsonObject scene = JsonNode.Parse(File.ReadAllText(ExpectedFixture.ScenePath("coffee-table")))!.AsObject();

        // A copy: the constraints that pinned the sample's own 2 1/2" legs go, since a 2x4 leg has
        // a different section; the boxes are re-placed by hand instead.
        scene["relationships"] = new JsonArray();
        JsonArray entities = scene["entities"]!.AsArray();
        foreach (JsonNode? dimension in entities.Where(e => e!["type"]!.GetValue<string>() == "dimension").ToList())
        {
            entities.Remove(dimension);
        }

        foreach (JsonNode? node in entities)
        {
            string name = node!["name"]!.GetValue<string>();
            JsonObject part = node["part"]!.AsObject();

            if (name == "Top")
            {
                part["stock"] = "3/4 plywood";
            }
            else if (name.StartsWith("Leg", StringComparison.Ordinal))
            {
                // 2x4 on end: 3 1/2" (3584) across X, 1 1/2" (1536) up Y, still 16 1/4" tall.
                node["width"] = 3584;
                node["height"] = 1536;
                part["stock"] = "2x4";
            }
            else if (name.StartsWith("Apron, long", StringComparison.Ordinal))
            {
                // Between the legs' inner faces: x 1 1/2+3 1/2 = 5" .. 44" (the SE leg starts at 44").
                node["anchor"]!["x"] = 5120;
                node["width"] = 39936;
                part["stock"] = "1x4";
            }
            else if (name.StartsWith("Apron, short", StringComparison.Ordinal))
            {
                // y 1 1/2+1 1/2 = 3" .. 20" (the north legs start at 20").
                node["anchor"]!["y"] = 3072;
                node["height"] = 17408;
                part["stock"] = "1x4";
            }
        }

        string text = scene.ToJsonString();
        string path = Path.Combine(Path.GetTempPath(), $"coffee-table-stocked-{Guid.NewGuid():N}.scene.json");
        File.WriteAllText(path, text);
        try
        {
            Loaded loaded = Assert.IsType<Loaded>(SceneReader.ReadFile(path));
            return CutList.Of(loaded.Sketch, MaterialsLibrary.Shipped);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void A_coffee_table_with_stock_assigned_cuts_a_2x4_at_its_true_size()
    {
        ImmutableArray<CutListRow> rows = StockedRows();

        // Hand arithmetic (1 in = 1024 units): top 48" = 49152 x 24" = 24576 x 3/4" = 768;
        // long apron 44"-5" = 39" = 39936; short apron 20"-3" = 17" = 17408 (both 3/4" x 3 1/2" = 768 x 3584, a 1x4);
        // leg 16 1/4" = 16640 long, 3 1/2" = 3584 wide, 1 1/2" = 1536 thick (a 2x4's dry section).
        CutListRow top = Assert.Single(rows, r => r.Material == "3/4 plywood");
        Assert.Equal((49152, 24576, 768), (top.Length.Units, top.Width.Units, top.Thickness.Units));

        CutListRow legs = Assert.Single(rows, r => r.Material == "2x4");
        Assert.Equal(4, legs.Quantity);
        Assert.Equal((16640, 3584, 1536), (legs.Length.Units, legs.Width.Units, legs.Thickness.Units));
        Assert.Equal("3 1/2\"", CutListCsv.Text(legs.Width));
        Assert.Equal("1 1/2\"", CutListCsv.Text(legs.Thickness));

        Assert.Equal(new[] { 39936, 17408 }, rows.Where(r => r.Material == "1x4").Select(r => (int)r.Length.Units).ToArray());
        Assert.Equal(2, rows.Count(r => r.Material == "1x4"));
        Assert.Equal(4, rows.Where(r => r.Material == "1x4").Sum(r => r.Quantity));
        Assert.DoesNotContain(rows, r => r.Unresolved);
    }

    [Fact]
    [Trait("Feature", "CUT-005")]
    public void A_coffee_table_with_stock_assigned_buys_board_feet_and_a_sheet()
    {
        ImmutableArray<ShoppingListRow> shop = ShoppingList.Of(StockedRows());

        // Plywood: the 48" x 24" top is one sheet by area (1152 sq in of 4608).
        ShoppingListRow ply = Assert.Single(shop, r => r.Material == "3/4 plywood");
        Assert.Equal(1, ply.Sheets);

        // Board feet use the NOMINAL section (the lumber trade's convention): four 2x4 legs of
        // 16 1/4" -> 4 x 2 x 4 x 16.25 = 520 cubic inches = 3.61 board feet used; bought in whole
        // boards, never fewer than used. One cubic inch is 1024^3 cubic units.
        ShoppingListRow twoByFour = Assert.Single(shop, r => r.Material == "2x4");
        Assert.Equal((System.Numerics.BigInteger)(520L * 1024 * 1024 * 1024), (System.Numerics.BigInteger)twoByFour.UsedCubicUnits);
        Assert.True(twoByFour.BoughtCubicUnits >= twoByFour.UsedCubicUnits);
        Assert.True(twoByFour.Boards.Sum(b => b.Count) >= 1);

        // 1x4: 2 x 39" + 2 x 17" = 112 inches of nominal 1 x 4 = 448 cubic inches (3.11 board feet).
        ShoppingListRow oneByFour = Assert.Single(shop, r => r.Material == "1x4");
        Assert.Equal((System.Numerics.BigInteger)(448L * 1024 * 1024 * 1024), (System.Numerics.BigInteger)oneByFour.UsedCubicUnits);
    }
}

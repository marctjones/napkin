using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>What one price is for: a board of one length of one stock, one sheet of one panel, or a board foot of one hardwood.</summary>
/// <param name="Material">The shopping row's material, as it reads.</param>
/// <param name="StockLength">For boards, the length they are bought in; null for sheets and board feet.</param>
public readonly record struct PriceKey(string Material, Length? StockLength);

/// <summary>How a price is counted.</summary>
public enum PriceUnit
{
    /// <summary>Per board of the key's length.</summary>
    Board,

    /// <summary>Per sheet.</summary>
    Sheet,

    /// <summary>Per board foot, for hardwood bought in random widths.</summary>
    BoardFoot,
}

/// <summary>One line of the estimate: what is bought, how many of the unit, and — when a price was entered — what it costs.</summary>
/// <param name="Key">What the price is for.</param>
/// <param name="Unit">How it is counted.</param>
/// <param name="Quantity">How many of the unit are bought.</param>
/// <param name="Price">The price entered for one unit, or null when none was.</param>
public sealed record CostLine(PriceKey Key, PriceUnit Unit, decimal Quantity, decimal? Price)
{
    /// <summary>What this line costs, or null when it has no price.</summary>
    public decimal? Cost => Price is { } each ? decimal.Round(each * Quantity, 2, MidpointRounding.AwayFromZero) : null;

    /// <summary>What the price is for, in words: "2x4, 8'", "3/4 plywood, sheet", "Walnut, bd ft".</summary>
    public string What => Unit switch
    {
        PriceUnit.Board => $"{Key.Material}, {Key.StockLength!.Value.Format(Core.Materials.StockItem.LengthFormatting).Text}",
        PriceUnit.Sheet => $"{Key.Material}, sheet",
        _ => $"{Key.Material}, bd ft",
    };
}

/// <summary>
/// An estimate from prices the person entered (#141): never built in, because prices change and a
/// built-in one would be a fact from memory. Each line the shopping list buys is priced by board of a
/// length, by sheet, or by board foot; a line with no price is left out of the total and counted, so
/// the total is never read as the whole cost when it is not.
/// </summary>
public static class ShoppingCost
{
    /// <summary>Every line the shopping list buys that a price can apply to, with the price entered for it.</summary>
    /// <param name="rows">The shopping list.</param>
    /// <param name="prices">The prices entered, by what each is for.</param>
    public static ImmutableArray<CostLine> Lines(IEnumerable<ShoppingListRow> rows, IReadOnlyDictionary<PriceKey, decimal> prices)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(prices);

        List<CostLine> lines = [];
        foreach (ShoppingListRow row in rows)
        {
            switch (row.Kind)
            {
                case ShoppingListKind.Boards:
                    foreach (BoardsOfLength boards in row.Boards)
                    {
                        PriceKey key = new(row.Material, boards.StockLength);
                        lines.Add(new CostLine(key, PriceUnit.Board, boards.Count, Found(prices, key)));
                    }

                    break;

                case ShoppingListKind.Sheets:
                {
                    PriceKey key = new(row.Material, null);
                    lines.Add(new CostLine(key, PriceUnit.Sheet, row.Sheets, Found(prices, key)));
                    break;
                }

                case ShoppingListKind.BoardFeet:
                {
                    PriceKey key = new(row.Material, null);
                    decimal boardFeet = decimal.Parse(BoardFeet.Text(row.UsedCubicUnits), CultureInfo.InvariantCulture);
                    lines.Add(new CostLine(key, PriceUnit.BoardFoot, boardFeet, Found(prices, key)));
                    break;
                }
            }
        }

        return [.. lines];
    }

    /// <summary>
    /// The estimate in a sentence: "Estimate: 84.12, from the prices you entered", or, with lines
    /// unpriced, "Estimate: 60.00 for 3 of 4 lines — 1 has no price". Null with nothing to price.
    /// </summary>
    public static string? Summary(ImmutableArray<CostLine> lines)
    {
        if (lines.IsEmpty)
        {
            return null;
        }

        CostLine[] priced = [.. lines.Where(line => line.Cost is not null)];
        if (priced.Length == 0)
        {
            return "No estimate yet: enter a price for each line to buy.";
        }

        string total = Money(priced.Sum(line => line.Cost!.Value));
        int missing = lines.Length - priced.Length;
        return missing == 0
            ? $"Estimate: {total}, from the prices you entered."
            : $"Estimate: {total} for {priced.Length} of {lines.Length} lines — {missing} {(missing == 1 ? "has" : "have")} no price.";
    }

    /// <summary>An amount as the estimate writes it: two places, no currency, since napkin knows none.</summary>
    public static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    static decimal? Found(IReadOnlyDictionary<PriceKey, decimal> prices, PriceKey key)
        => prices.TryGetValue(key, out decimal price) ? price : null;
}

using System.Collections.Immutable;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// The shopping list as a file a spreadsheet opens: exactly the rows on screen, in the same order,
/// with the same text (issue #9, "Output format").
/// </summary>
/// <remarks>
/// The same shape as <see cref="CutListCsv"/>: a header line saying what the list is before — saw
/// kerf, defect and joinery allowance — then the column names, then one line per row, each field
/// the very string the table shows. Lines end in <c>\n</c> on every platform. Read it back with
/// <see cref="CutListCsv.Parse"/>, which parses any file either export writes.
/// </remarks>
public static class ShoppingListCsv
{
    /// <summary>The column names, in the order they are written and shown.</summary>
    public const string Header = "Material,Species,Buy,For,Board feet bought,Board feet used,Waste,Note";

    /// <summary>The text of one row, field by field, as the table shows it and the file carries it.</summary>
    /// <param name="row">The row.</param>
    public static ImmutableArray<string> Fields(ShoppingListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return [row.Material, row.Species, row.BuyText, row.For, row.BoughtText, row.UsedText, row.WasteText, row.Note];
    }

    /// <summary>The shopping list as CSV text.</summary>
    /// <param name="rows">The rows, in the order they are on screen. Written in that order.</param>
    /// <param name="kerf">The kerf the list was planned with; the napkin default when omitted.</param>
    public static string ToCsv(IEnumerable<ShoppingListRow> rows, Length? kerf = null)
    {
        ArgumentNullException.ThrowIfNull(rows);

        StringBuilder csv = new();
        csv.Append(CutListCsv.Field(ShoppingList.Statement(kerf ?? CutLayout.DefaultKerf))).Append('\n');
        csv.Append(Header).Append('\n');

        foreach (ShoppingListRow row in rows)
        {
            csv.AppendJoin(',', Fields(row).Select(CutListCsv.Field)).Append('\n');
        }

        return csv.ToString();
    }

    /// <summary>
    /// The same, with the estimate from the prices entered (#141) after it — a blank line, then one line
    /// per thing priced and the estimate's sentence — when any price was entered; unchanged otherwise.
    /// </summary>
    /// <param name="rows">The shopping list.</param>
    /// <param name="kerf">The saw kerf the list was worked out with.</param>
    /// <param name="costs">The lines the prices apply to, with the prices entered.</param>
    public static string ToCsv(IEnumerable<ShoppingListRow> rows, Length? kerf, ImmutableArray<CostLine> costs)
    {
        string list = ToCsv(rows, kerf);
        if (!costs.Any(line => line.Price is not null))
        {
            return list;
        }

        StringBuilder csv = new(list);
        csv.Append('\n').Append("What,Quantity,Price each,Cost").Append('\n');
        foreach (CostLine line in costs)
        {
            csv.Append(CutListCsv.Field(line.What)).Append(',')
               .Append(line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
               .Append(line.Price is { } each ? ShoppingCost.Money(each) : string.Empty).Append(',')
               .Append(line.Cost is { } cost ? ShoppingCost.Money(cost) : string.Empty).Append('\n');
        }

        csv.Append(CutListCsv.Field(ShoppingCost.Summary(costs)!)).Append('\n');
        return csv.ToString();
    }
}

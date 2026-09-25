using System.Collections.Immutable;
using System.Globalization;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// One thing that comes out (docs/design/renovation-sketches.md §6.2): a demolished part by name
/// and count, a demolished wall, opening or room by name, or framing pieces a new opening takes out.
/// A count of what comes out, never a dumpster size (§1.8).
/// </summary>
/// <param name="Item">What comes out: "Shelf", "Wall 1", "Wall 1: stud 7'-7 1/2\"".</param>
/// <param name="Count">How many.</param>
/// <param name="Note">What the count assumes, or empty.</param>
public sealed record DemolitionLine(string Item, int Count, string Note)
{
    /// <summary>"Shelf × 2", or "Wall 1" for one, with the note in brackets after it.</summary>
    public string Text => (Count == 1 ? Item : $"{Item} × {Count.ToString(CultureInfo.InvariantCulture)}") + (Note.Length > 0 ? $" ({Note})" : string.Empty);
}

/// <summary>The shopping list's Demolition section: what comes out, derived, never stored.</summary>
public static class Demolition
{
    /// <summary>
    /// Every demolished box: a part by its name and how many (its quantity, summed over parts of
    /// the same name), anything else — a wall, an opening, a room — by its name, in id order.
    /// </summary>
    public static ImmutableArray<DemolitionLine> Boxes(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        List<(string Name, int Count)> lines = [];
        foreach (Box box in sketch.Entities.Values.OfType<Box>().Where(box => box.Phase == Phase.Demolish).OrderBy(box => box.Id))
        {
            string name = box.Name.Length > 0 ? box.Name : box.Part is null ? "Box" : "Part";
            int count = box.Part?.Quantity ?? 1;
            int at = box.Part is null ? -1 : lines.FindIndex(line => line.Name == name);
            if (at < 0)
            {
                lines.Add((name, count));
            }
            else
            {
                lines[at] = (name, lines[at].Count + count);
            }
        }

        return [.. lines.Select(line => new DemolitionLine(line.Name, line.Count, string.Empty))];
    }

    /// <summary>
    /// The line over the shopping list when the design says what is already there or coming out:
    /// "Only what is New is listed; 3 items to remove are under Demolition." Null for a design with
    /// nothing existing or demolished, which reads exactly as before renovation.
    /// </summary>
    public static string? Header(Sketch sketch, IEnumerable<DemolitionLine> lines)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(lines);
        if (sketch.Entities.Values.All(entity => entity.Phase == Phase.New))
        {
            return null;
        }

        int count = lines.Sum(line => line.Count);
        return count switch
        {
            0 => "Only what is New is listed; nothing comes out.",
            1 => "Only what is New is listed; 1 item to remove is under Demolition.",
            _ => $"Only what is New is listed; {count.ToString(CultureInfo.InvariantCulture)} items to remove are under Demolition.",
        };
    }

    /// <summary>The section as a CSV file carries it: its own header, then Item, Count, Note.</summary>
    public static string ToCsv(IEnumerable<DemolitionLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        StringBuilder text = new();
        text.Append("Demolition\n");
        text.Append("Item,Count,Note\n");
        foreach (DemolitionLine line in lines)
        {
            text.Append(CutListCsv.Field(line.Item)).Append(',')
                .Append(line.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(CutListCsv.Field(line.Note)).Append('\n');
        }

        return text.ToString();
    }
}

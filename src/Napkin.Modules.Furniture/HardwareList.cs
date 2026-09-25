using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>One line of the hardware list: a typed name and how many the parts carry between them (&#xA7;7.5).</summary>
/// <param name="Name">The typed name; two items are the same only if their text is exactly equal.</param>
/// <param name="Count">The total: each part's quantity of it times the part's copies.</param>
/// <param name="Parts">The parts carrying it, in id order, each with what it adds.</param>
public sealed record HardwareRow(string Name, int Count, ImmutableArray<(string Part, int Adds)> Parts)
{
    /// <summary>The part names, first seen first.</summary>
    public string For => string.Join(", ", Parts.Select(part => part.Part));

    /// <summary>The sum written out: "1 + 1 = 2".</summary>
    public string Derivation => string.Join(" + ", Parts.Select(part => part.Adds)) + $" = {Count}";
}

/// <summary>
/// The hardware typed onto parts — slides, pulls, hinges — summed by exact name (&#xA7;7.5). Nothing is
/// derived from a name and no hardware table ships.
/// </summary>
public static class HardwareList
{
    /// <summary>The list, in the order each name is first seen over the parts in id order.</summary>
    /// <param name="sketch">The design.</param>
    public static ImmutableArray<HardwareRow> Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        List<(string Name, List<(string Part, int Adds)> Parts)> lines = [];
        foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            if (box.Part is not { } part)
            {
                continue;
            }

            foreach (HardwareItem item in part.Hardware)
            {
                int index = lines.FindIndex(line => line.Name == item.Name);
                if (index < 0)
                {
                    lines.Add((item.Name, []));
                    index = lines.Count - 1;
                }

                lines[index].Parts.Add((box.Name, item.Quantity * part.Quantity));
            }
        }

        return [.. lines.Select(line => new HardwareRow(line.Name, line.Parts.Sum(part => part.Adds), [.. line.Parts]))];
    }
}

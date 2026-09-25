using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// Reads the hardware a joint lists, one item per line, as a person types it: <c>pocket screw
/// x 4</c>, <c>dowel × 2</c>, or just a name for one of it.
/// </summary>
public static partial class HardwareEntry
{
    /// <summary>
    /// The items the text lists, or the first line whose count is not at least one. Blank lines
    /// are skipped; a line with no count is one of it.
    /// </summary>
    public static bool TryRead(string? typed, out ImmutableList<HardwareItem> items, out string problem)
    {
        List<HardwareItem> list = [];
        problem = string.Empty;
        foreach (string raw in (typed ?? string.Empty).Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            Match m = Line().Match(line);
            string name = m.Success ? m.Groups[1].Value : line;
            int quantity = 1;
            if (m.Success && !int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out quantity))
            {
                quantity = 0;
            }

            if (quantity < 1)
            {
                problem = $"Hardware \"{name}\" needs a count of at least 1.";
                items = [];
                return false;
            }

            list.Add(new HardwareItem(name, quantity));
        }

        items = [.. list];
        return true;
    }

    [GeneratedRegex(@"^(.*\S)\s+[x×]\s*(\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Line();
}

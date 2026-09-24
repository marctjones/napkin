using System.Text.Json;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// A second, deliberately naive cut list for plain rectangular parts, worked out straight from the
/// scene JSON (#100).
/// </summary>
/// <remarks>
/// Independence is the whole point. The expectations files were derived by hand once; this class is
/// a second implementation that a regression in the shared arithmetic cannot also break. So it must
/// not use anything from <c>Napkin.Core.*</c> — no <c>Length</c>, no <c>Box</c>, no
/// <c>SceneReader</c>, no <c>PartDimension</c>: it parses the file with System.Text.Json and does
/// integer arithmetic on the stored 1/1024-inch units. If it ever imports a napkin type, two paths
/// that share code will agree on a shared mistake and the check is worthless.
/// The rule it encodes: a box's <c>planAxes</c> names what its stored width (x) and height (y) are
/// (length, width or thickness); the third dimension is the box's depth. Rotation and which face is
/// up do not change a piece's sizes. Boxes with cuts or no part are outside this oracle.
/// </remarks>
internal static class IndependentCutOracle
{
    /// <summary>Pieces per (length, width, thickness) in stored units, summed over plain parts.</summary>
    internal static SortedDictionary<(long Length, long Width, long Thickness), long> Pieces(string sceneJson)
    {
        SortedDictionary<(long, long, long), long> result = [];
        using JsonDocument document = JsonDocument.Parse(sceneJson);

        foreach (JsonElement entity in document.RootElement.GetProperty("entities").EnumerateArray())
        {
            if (entity.GetProperty("type").GetString() != "box"
                || entity.GetProperty("part").ValueKind == JsonValueKind.Null
                || entity.GetProperty("cuts").GetArrayLength() > 0)
            {
                continue;
            }

            JsonElement part = entity.GetProperty("part");
            JsonElement axes = part.GetProperty("planAxes");
            Dictionary<string, long> size = new()
            {
                [axes.GetProperty("x").GetString()!] = entity.GetProperty("width").GetInt64(),
                [axes.GetProperty("y").GetString()!] = entity.GetProperty("height").GetInt64(),
            };

            foreach (string name in new[] { "length", "width", "thickness" })
            {
                if (!size.ContainsKey(name))
                {
                    size[name] = entity.GetProperty("depth").GetInt64();
                }
            }

            (long, long, long) key = (size["length"], size["width"], size["thickness"]);
            result[key] = result.GetValueOrDefault(key) + part.GetProperty("quantity").GetInt64();
        }

        return result;
    }
}

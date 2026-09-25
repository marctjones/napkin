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
/// up do not change a piece's sizes. A part inserted into a groove or a rabbet is longer than drawn by
/// that joint's depth (joinery note &#xA7;6.1), along the dimension its inserted face's local axis is:
/// west or east the stored width, north or south the stored height, top or bottom the depth. Boxes
/// with cuts or no part are outside this oracle.
/// </remarks>
internal static class IndependentCutOracle
{
    /// <summary>Pieces per (length, width, thickness) in stored units, summed over plain parts.</summary>
    internal static SortedDictionary<(long Length, long Width, long Thickness), long> Pieces(string sceneJson)
    {
        SortedDictionary<(long, long, long), long> result = [];
        using JsonDocument document = JsonDocument.Parse(sceneJson);

        // Joinery allowances, in stored units by the inserted box's id and the local axis (x, y or z) they lengthen.
        Dictionary<(string Box, string Axis), long> grown = [];
        foreach (JsonElement relationship in document.RootElement.GetProperty("relationships").EnumerateArray())
        {
            if (relationship.GetProperty("kind").GetString() != "joint"
                || relationship.GetProperty("type").GetString() is not ("groove" or "rabbet"))
            {
                continue;
            }

            JsonElement inserted = relationship.GetProperty("inserted");
            string face = inserted.GetProperty("faces")[0].GetString()!;
            string axis = face is "west" or "east" ? "x" : face is "north" or "south" ? "y" : "z";
            (string, string) key = (inserted.GetProperty("box").GetString()!, axis);
            grown[key] = grown.GetValueOrDefault(key) + relationship.GetProperty("depth").GetInt64();
        }

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
            string id = entity.GetProperty("id").GetString()!;
            Dictionary<string, long> size = new()
            {
                [axes.GetProperty("x").GetString()!] = entity.GetProperty("width").GetInt64() + grown.GetValueOrDefault((id, "x")),
                [axes.GetProperty("y").GetString()!] = entity.GetProperty("height").GetInt64() + grown.GetValueOrDefault((id, "y")),
            };

            foreach (string name in new[] { "length", "width", "thickness" })
            {
                if (!size.ContainsKey(name))
                {
                    size[name] = entity.GetProperty("depth").GetInt64() + grown.GetValueOrDefault((id, "z"));
                }
            }

            (long, long, long) key = (size["length"], size["width"], size["thickness"]);
            result[key] = result.GetValueOrDefault(key) + part.GetProperty("quantity").GetInt64();
        }

        return result;
    }
}

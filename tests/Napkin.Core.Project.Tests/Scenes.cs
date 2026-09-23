using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Small scenes written out in full, for tests about the format itself rather than about a
/// design. Each test starts from one of these and introduces exactly one fault.
/// </summary>
internal static class Scenes
{
    internal const string LayerId = "00000000-0000-0000-0000-000000000001";
    internal const string BoxId = "0192f1a0-0000-4000-8000-00000000000a";
    internal const string NodeId = "0192f1a0-0000-4000-8000-00000000000b";
    internal const string SecondNodeId = "0192f1a0-0000-4000-8000-00000000000e";
    internal const string SegmentId = "0192f1a0-0000-4000-8000-00000000000c";
    internal const string DimensionId = "0192f1a0-0000-4000-8000-00000000000d";
    internal const string RelationshipId = "0192f1a0-0000-4000-8000-00000000001a";
    internal const string SecondRelationshipId = "0192f1a0-0000-4000-8000-00000000001b";
    internal const string MissingId = "0192f1a0-0000-4000-8000-0000000000ff";

    /// <summary>A 30 inch by 4 inch box at the origin, with its width driven.</summary>
    internal const string OneBox = """
        {
          "formatVersion": 4,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Shelf",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 30720, "height": 4096, "depth": 768, "faceUp": "top", "rotation": 0,
              "part": { "stock": "1x6", "species": null, "quantity": 1,
                        "planAxes": { "x": "length", "y": "width" } }, "cuts": [] }
          ],
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "paramValue",
              "param": { "kind": "boxWidth", "box": "0192f1a0-0000-4000-8000-00000000000a" }, "value": 30720 }
          ]
        }
        """;

    /// <summary>
    /// The same box, quarter-turned about its anchor, with a node on its south-east corner. That
    /// corner is the local offset (width, 0) = (30720, 0) turned a quarter turn, which is
    /// (-0, 30720) — so the node sits 30 inches straight up from the anchor at (0, 30720).
    /// </summary>
    internal const string TurnedBoxAndNode = """
        {
          "formatVersion": 4,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Shelf",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 30720, "height": 4096, "depth": 768, "faceUp": "top", "rotation": 324000,
              "part": { "stock": "1x6", "species": null, "quantity": 1,
                        "planAxes": { "x": "length", "y": "width" } }, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000b", "type": "node", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "position": { "x": 0, "y": 30720 } }
          ],
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "coincident",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south", "east"] },
              "b": { "kind": "node", "node": "0192f1a0-0000-4000-8000-00000000000b" } }
          ]
        }
        """;

    /// <summary>
    /// Two nodes 30 inches apart on the X axis, the segment between them, a driving size on the
    /// segment's length and a reference dimension measuring the same span.
    /// </summary>
    internal const string SegmentAndDimension = """
        {
          "formatVersion": 4,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000b", "type": "node", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "West end", "position": { "x": 0, "y": 0 } },
            { "id": "0192f1a0-0000-4000-8000-00000000000e", "type": "node", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "East end", "position": { "x": 30720, "y": 0 } },
            { "id": "0192f1a0-0000-4000-8000-00000000000c", "type": "segment", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Centreline",
              "start": "0192f1a0-0000-4000-8000-00000000000b", "end": "0192f1a0-0000-4000-8000-00000000000e" },
            { "id": "0192f1a0-0000-4000-8000-00000000000d", "type": "dimension", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Centreline length",
              "measures": { "kind": "axis",
                "from": { "kind": "node", "node": "0192f1a0-0000-4000-8000-00000000000b" },
                "to": { "kind": "node", "node": "0192f1a0-0000-4000-8000-00000000000e" },
                "axis": "x" },
              "drives": null,
              "placement": { "offset": 2048, "side": "south" } }
          ],
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "paramValue",
              "param": { "kind": "segmentLength", "segment": "0192f1a0-0000-4000-8000-00000000000c" }, "value": 30720 },
            { "id": "0192f1a0-0000-4000-8000-00000000001b", "kind": "horizontal",
              "edge": { "kind": "segment", "segment": "0192f1a0-0000-4000-8000-00000000000c" } }
          ]
        }
        """;

    /// <summary>The box, with a relationship kind that is reserved for the solver.</summary>
    internal const string TangentBetweenBoxEdges = """
        {
          "formatVersion": 4,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Shelf",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 30720, "height": 4096, "depth": 768, "faceUp": "top", "rotation": 0,
              "part": null, "cuts": [] }
          ],
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "tangent",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["north"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south"] } }
          ]
        }
        """;

    /// <summary>The same box written twice, under one id.</summary>
    internal const string TwoEntitiesUnderOneId = """
        {
          "formatVersion": 4,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Shelf",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 30720, "height": 4096, "depth": 768, "faceUp": "top", "rotation": 0, "part": null, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Shelf",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 30720, "height": 4096, "depth": 768, "faceUp": "top", "rotation": 0, "part": null, "cuts": [] }
          ],
          "relationships": []
        }
        """;

    /// <summary>Two layers under one id.</summary>
    internal const string TwoLayersUnderOneId = """
        {
          "formatVersion": 4,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [
            { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" },
            { "id": "00000000-0000-0000-0000-000000000001", "name": "Also default" }
          ],
          "entities": [],
          "relationships": []
        }
        """;

    /// <summary>Two relationships, two ids, one statement — invariant 4 of the geometry design.</summary>
    internal const string TwoRelationshipsSayingTheSameThing = """
        {
          "formatVersion": 4,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Shelf",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 30720, "height": 4096, "depth": 768, "faceUp": "top", "rotation": 0, "part": null, "cuts": [] }
          ],
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "paramValue",
              "param": { "kind": "boxWidth", "box": "0192f1a0-0000-4000-8000-00000000000a" }, "value": 30720 },
            { "id": "0192f1a0-0000-4000-8000-00000000001b", "kind": "paramValue",
              "param": { "kind": "boxWidth", "box": "0192f1a0-0000-4000-8000-00000000000a" }, "value": 30720 }
          ]
        }
        """;

    /// <summary>An empty design: no entities, no relationships, one layer.</summary>
    internal const string Empty = """
        {
          "formatVersion": 4,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [],
          "relationships": []
        }
        """;

    internal static LoadResult Read(string json)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        return SceneReader.Read(stream);
    }

    internal static Sketch Accept(string json) => Assert.IsType<Loaded>(Read(json)).Sketch;

    internal static Refused Refuse(string json) => Assert.IsType<Refused>(Read(json));

    /// <summary>
    /// Refuses <paramref name="json"/> and asserts that one problem of <paramref name="kind"/> is
    /// reported, naming every one of <paramref name="mustName"/> — because M1's viewer shows the
    /// message and "something is wrong with this file" is not a message.
    /// </summary>
    internal static LoadProblem RefuseWith(string json, LoadProblemKind kind, params string[] mustName)
    {
        Refused refused = Refuse(json);
        LoadProblem problem = Assert.Single(refused.Problems.Where(candidate => candidate.Kind == kind));

        foreach (string fragment in mustName)
        {
            Assert.Contains(fragment, $"{problem.Location} {problem.Message}", StringComparison.Ordinal);
        }

        Assert.Contains(problem.Message, refused.Summary, StringComparison.Ordinal);
        return problem;
    }

    /// <summary>The one replacement a test makes, asserted to have actually changed something.</summary>
    internal static string With(this string json, string original, string replacement)
    {
        Assert.Contains(original, json, StringComparison.Ordinal);
        return json.Replace(original, replacement, StringComparison.Ordinal);
    }
}

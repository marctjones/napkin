using System.Collections.Immutable;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// The relationship kinds reserved for the constraint solver (#28). They are part of the format
/// so that files and the UI have names for them: a build with the solver reads them, and a build
/// without one refuses the file naming the kind rather than crashing (geometry design §6).
/// </summary>
public sealed class ReservedRelationshipTests
{
    [Fact]
    [Trait("Feature", "PRJ-001")]
    public void A_build_with_the_solver_reads_the_kinds_reserved_for_it()
    {
        Sketch sketch = Read(SolverKinds);

        Assert.Equal(6, sketch.Relationships.Count);
        Assert.Collection(
            sketch.RelationshipsInOrder,
            relationship => Assert.IsType<Geometry.Parallel>(relationship),
            relationship => Assert.IsType<Perpendicular>(relationship),
            relationship => Assert.IsType<AngleBetween>(relationship),
            relationship => Assert.IsType<Distance>(relationship),
            relationship => Assert.IsType<PointOnEdge>(relationship),
            relationship => Assert.IsType<Symmetric>(relationship));

        // The file's geometry was worked out to satisfy every one of them.
        Assert.True(RelationshipChecker.Check(sketch).AllHold, RelationshipChecker.Check(sketch).ToString());
    }

    [Theory]
    [InlineData("tangent")]
    [InlineData("radius")]
    [Trait("Feature", "PRJ-003")]
    public void A_kind_about_arcs_cannot_hold_while_there_are_no_arcs(string kind)
    {
        // Tangent and Radius are meaningful only once arcs exist, so the checker reports them as
        // violations rather than passing over them — and the reader refuses the file rather than
        // opening a sketch that claims to satisfy a relationship nothing evaluated.
        string json = kind == "tangent" ? Scenes.TangentBetweenBoxEdges : RadiusOfABox;

        Refused refused = Assert.IsType<Refused>(Load(json, new SolverCapableUpdater()));

        LoadProblem problem = Assert.Single(refused.Problems);
        Assert.Equal(LoadProblemKind.RelationshipViolated, problem.Kind);
        Assert.Contains(kind, problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void The_build_without_the_solver_refuses_each_reserved_kind_by_name()
    {
        Refused refused = Scenes.Refuse(SolverKinds);

        Assert.Equal(6, refused.Problems.Count);
        Assert.All(refused.Problems, problem => Assert.Equal(LoadProblemKind.UnsupportedRelationship, problem.Kind));

        foreach (string kind in new[] { "parallel", "perpendicular", "angleBetween", "distance", "pointOnEdge", "symmetric" })
        {
            Assert.Contains(kind, refused.Summary, StringComparison.Ordinal);
        }
    }

    private static Sketch Read(string json) => Assert.IsType<Loaded>(Load(json, new SolverCapableUpdater())).Sketch;

    private static LoadResult Load(string json, IGeometryUpdater updater)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        return SceneReader.Read(stream, updater);
    }

    /// <summary>
    /// Two 8 inch squares, the first at the origin and the second starting at 16384 units, a node
    /// on the first's south edge, and a vertical segment at x = 12288 — midway between the two
    /// squares' facing corners, 8192 and 16384, because (8192 + 16384) / 2 = 12288. Every reserved
    /// kind that can be evaluated without an arc holds on that geometry:
    /// the two south edges run the same way (parallel); a south edge and an east edge meet at a
    /// right angle (perpendicular, and angleBetween at 90 degrees = 324000 arcseconds); the two
    /// south-west corners are 16384 units apart (distance); the node at (4096, 0) is on the first
    /// square's south edge; and the facing corners are mirror images across the segment.
    /// </summary>
    private const string SolverKinds = """
        {
          "formatVersion": 6,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 8192, "height": 8192, "depth": 768, "faceUp": "top", "rotation": 0, "part": null, "wall": null, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000b", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "anchor": { "x": 16384, "y": 0, "z": 0 }, "width": 8192, "height": 8192, "depth": 768, "faceUp": "top", "rotation": 0, "part": null, "wall": null, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000c", "type": "node", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "position": { "x": 4096, "y": 0 } },
            { "id": "0192f1a0-0000-4000-8000-00000000000d", "type": "node", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "position": { "x": 12288, "y": 0 } },
            { "id": "0192f1a0-0000-4000-8000-00000000000e", "type": "node", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "position": { "x": 12288, "y": 8192 } },
            { "id": "0192f1a0-0000-4000-8000-00000000000f", "type": "segment", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "",
              "start": "0192f1a0-0000-4000-8000-00000000000d", "end": "0192f1a0-0000-4000-8000-00000000000e" }
          ],
          "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "source": null },
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-000000000001", "kind": "parallel",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["south"] } },
            { "id": "0192f1a0-0000-4000-8000-000000000002", "kind": "perpendicular",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["east"] } },
            { "id": "0192f1a0-0000-4000-8000-000000000003", "kind": "angleBetween",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["east"] },
              "angle": 324000 },
            { "id": "0192f1a0-0000-4000-8000-000000000004", "kind": "distance",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south", "west"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["south", "west"] },
              "value": 16384 },
            { "id": "0192f1a0-0000-4000-8000-000000000005", "kind": "pointOnEdge",
              "point": { "kind": "node", "node": "0192f1a0-0000-4000-8000-00000000000c" },
              "edge": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south"] } },
            { "id": "0192f1a0-0000-4000-8000-000000000006", "kind": "symmetric",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south", "east"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["south", "west"] },
              "mirror": { "kind": "segment", "segment": "0192f1a0-0000-4000-8000-00000000000f" } }
          ]
        }
        """;

    /// <summary>A radius stated about a box, because there is no arc entity to state it about.</summary>
    private const string RadiusOfABox = """
        {
          "formatVersion": 6,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 8192, "height": 8192, "depth": 768, "faceUp": "top", "rotation": 0, "part": null, "wall": null, "cuts": [] }
          ],
          "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "source": null },
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-000000000001", "kind": "radius",
              "arc": "0192f1a0-0000-4000-8000-00000000000a", "value": 4096 }
          ]
        }
        """;

    /// <summary>
    /// An updater that holds every kind, standing in for a build with the constraint solver. It
    /// exists for its list of kinds; nothing here asks it to apply anything.
    /// </summary>
    private sealed class SolverCapableUpdater : IGeometryUpdater
    {
        public ImmutableHashSet<Type> SupportedRelationships { get; } =
        [
            typeof(Anchored), typeof(Coincident), typeof(Horizontal), typeof(Vertical), typeof(Flush),
            typeof(AxisDistance), typeof(ParamValue), typeof(EqualParam), typeof(Centered),
            typeof(Geometry.Parallel), typeof(Perpendicular), typeof(AngleBetween), typeof(Distance),
            typeof(PointOnEdge), typeof(Symmetric), typeof(Tangent), typeof(Radius),
        ];

        public UpdateResult Apply(Sketch sketch, Request request)
            => throw new NotSupportedException("This updater is only here for its list of kinds.");
    }
}

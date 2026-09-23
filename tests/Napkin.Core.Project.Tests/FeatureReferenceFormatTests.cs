using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Format version 3 against the reference vocabulary of docs/design/assembly-model.md &#xA7;10
/// step 3: the file keeps its point and edge slots until step 5, so a <c>corner</c> is read as the
/// blank's local upright and a <c>boxEdge</c> as the side face of the same name; the loader judges
/// each pairing by what its places fix (&#xA7;2.3), and the writer refuses a feature version 3
/// cannot spell rather than saving it as another.
/// </summary>
public sealed class FeatureReferenceFormatTests
{
    private const string Left = "0192f1a0-0000-4000-8000-00000000000a";
    private const string Right = "0192f1a0-0000-4000-8000-00000000000b";
    private const string Node = "0192f1a0-0000-4000-8000-00000000000c";

    private static readonly string Scene = $$"""
        {
          "formatVersion": 3,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "{{Left}}", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Left",
              "anchor": { "x": 0, "y": 0 }, "width": 10240, "height": 4096, "rotation": 0,
              "part": null, "cuts": [] },
            { "id": "{{Right}}", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Right",
              "anchor": { "x": 10240, "y": 0 }, "width": 10240, "height": 4096, "rotation": 0,
              "part": null, "cuts": [] },
            { "id": "{{Node}}", "type": "node", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "position": { "x": 20480, "y": 4096 } }
          ],
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "flush",
              "a": { "kind": "boxEdge", "box": "{{Left}}", "edge": "east" },
              "b": { "kind": "boxEdge", "box": "{{Right}}", "edge": "west" } },
            { "id": "0192f1a0-0000-4000-8000-00000000001b", "kind": "coincident",
              "a": { "kind": "corner", "box": "{{Right}}", "corner": "northEast" },
              "b": { "kind": "node", "node": "{{Node}}" } }
          ]
        }
        """;

    private static EntityId IdOf(string id) => new(Guid.Parse(id));

    [Fact]
    public void A_corner_is_read_as_the_blanks_local_upright_and_a_box_edge_as_its_side_face()
    {
        Sketch sketch = Scenes.Accept(Scene);

        Flush flush = Assert.Single(sketch.Relationships.Values.OfType<Flush>());
        Assert.Equal(new FeatureRef(IdOf(Left), BoxFeature.Face(BoxFace.East)), flush.A);
        Assert.Equal(new FeatureRef(IdOf(Right), BoxFeature.Face(BoxFace.West)), flush.B);

        Coincident coincident = Assert.Single(sketch.Relationships.Values.OfType<Coincident>());
        Assert.Equal(new FeatureRef(IdOf(Right), BoxFeature.LocalUpright(BoxCorner.NorthEast)), coincident.A);
    }

    [Fact]
    public void Features_the_version_can_spell_survive_a_round_trip_as_they_were_written()
    {
        Sketch sketch = Scenes.Accept(Scene);

        string text = Encoding.UTF8.GetString(SceneWriter.WriteToBytes(sketch));
        Assert.Contains("\"kind\": \"boxEdge\"", text, StringComparison.Ordinal);
        Assert.Contains("\"corner\": \"northEast\"", text, StringComparison.Ordinal);
        Assert.Equal(sketch, Scenes.Accept(text));
    }

    [Fact]
    public void A_flush_between_edges_on_different_axes_is_refused_naming_both_places()
        => Scenes.RefuseWith(
            Scene.With("\"edge\": \"west\"", "\"edge\": \"north\""),
            LoadProblemKind.InvalidValue,
            "Left's east face fixes X",
            "Right's north face fixes Y");

    [Fact]
    public void A_feature_version_3_cannot_spell_is_refused_rather_than_saved_as_another()
    {
        Sketch sketch = Scenes.Accept(Scene);

        foreach (Relationship relationship in new Relationship[]
        {
            new Coincident(
                RelationshipId.New(),
                new FeatureRef(IdOf(Right), BoxFeature.Vertex(BoxCorner.NorthEast, BoxLevel.Top)),
                new FeatureRef(IdOf(Left), BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Top))),
            new Flush(
                RelationshipId.New(),
                new FeatureRef(IdOf(Left), BoxFeature.Face(BoxFace.Top)),
                new FeatureRef(IdOf(Right), BoxFeature.Face(BoxFace.Top))),
        })
        {
            NotSupportedException thrown = Assert.Throws<NotSupportedException>(
                () => SceneWriter.WriteToBytes(sketch.WithRelationship(relationship)));
            Assert.Contains(nameof(FeatureRef), thrown.Message, StringComparison.Ordinal);
        }
    }
}

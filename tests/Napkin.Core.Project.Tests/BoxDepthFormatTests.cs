using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Format version 3 against a box in space, docs/design/assembly-model.md &#xA7;10 step 2: the
/// file keeps its shape until step 5, so a part's out-of-plane dimension is read as the box's
/// depth, a plain box is read at the default depth, the depth's <c>boxDepth</c> param round-trips,
/// and a box the format cannot spell is refused rather than flattened.
/// </summary>
public sealed class BoxDepthFormatTests
{
    private const string PartBox = "0192f1a0-0000-4000-8000-00000000000a";
    private const string PlainBox = "0192f1a0-0000-4000-8000-00000000000b";

    private static readonly string Scene = $$"""
        {
          "formatVersion": 3,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "{{PartBox}}", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Leg",
              "anchor": { "x": 1024, "y": 2048 }, "width": 2560, "height": 2560, "rotation": 0,
              "part": { "stock": null, "species": null, "quantity": 4, "outOfPlane": 16640,
                        "planAxes": { "x": "width", "y": "thickness" } }, "cuts": [] },
            { "id": "{{PlainBox}}", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Wall",
              "anchor": { "x": 0, "y": 0 }, "width": 147456, "height": 5632, "rotation": 0,
              "part": null, "cuts": [] }
          ],
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "paramValue",
              "param": { "kind": "boxDepth", "box": "{{PartBox}}" }, "value": 16640 }
          ]
        }
        """;

    [Fact]
    public void A_parts_out_of_plane_dimension_is_read_as_its_depth_and_a_plain_box_gets_the_default()
    {
        Sketch sketch = Scenes.Accept(Scene);

        Box leg = sketch.Find<Box>(new EntityId(Guid.Parse(PartBox)))!;
        Assert.Equal(new Length(16640), leg.Depth);
        Assert.Equal(new Point3(new Length(1024), new Length(2048), Length.Zero), leg.Anchor);
        Assert.Equal(BoxFace.Top, leg.FaceUp);
        Assert.Equal(new Length(16640), leg.Part!.SizeOn(leg).Length);

        Box wall = sketch.Find<Box>(new EntityId(Guid.Parse(PlainBox)))!;
        Assert.Equal(Box.DefaultDepth, wall.Depth);
        Assert.Equal(768, Box.DefaultDepth.Units);

        ParamValue depth = Assert.IsType<ParamValue>(Assert.Single(sketch.Relationships.Values));
        Assert.Equal(new BoxDepthRef(leg.Id), depth.Param);
    }

    [Fact]
    public void A_depth_and_its_param_survive_a_round_trip()
    {
        Sketch sketch = Scenes.Accept(Scene);

        byte[] written = SceneWriter.WriteToBytes(sketch);
        string text = Encoding.UTF8.GetString(written);
        Assert.Contains("\"boxDepth\"", text, StringComparison.Ordinal);
        Assert.Contains("\"outOfPlane\": 16640", text, StringComparison.Ordinal);

        Assert.Equal(sketch, Scenes.Accept(text));
    }

    [Fact]
    public void A_box_the_format_cannot_spell_is_refused_rather_than_flattened()
    {
        Sketch sketch = Scenes.Accept(Scene);
        Box leg = sketch.Find<Box>(new EntityId(Guid.Parse(PartBox)))!;
        Box wall = sketch.Find<Box>(new EntityId(Guid.Parse(PlainBox)))!;

        foreach ((Box box, string says) in new (Box, string)[]
        {
            (leg with { FaceUp = BoxFace.East }, "East up"),
            (leg with { Anchor = leg.Anchor with { Z = Length.Inches(1) } }, "above the plan"),
            (wall with { Depth = Length.Inches(96) }, "is not a part"),
        })
        {
            Sketch unspellable = sketch.WithEntity(box);

            NotSupportedException thrown = Assert.Throws<NotSupportedException>(() => SceneWriter.WriteToBytes(unspellable));
            Assert.Contains(says, thrown.Message, StringComparison.Ordinal);

            NotSaved refused = Assert.IsType<NotSaved>(ProjectFile.Save(new MemoryStream(), unspellable));
            SaveProblem problem = Assert.Single(refused.Problems);
            Assert.Equal(SaveProblemKind.Unwritable, problem.Kind);
            Assert.Contains(box.Id.ToString(), problem.Message, StringComparison.Ordinal);
        }

        // A plain box at the default depth, and a part at any depth, are spelled as they are.
        Assert.Null(SceneWriter.Unspellable(wall));
        Assert.Null(SceneWriter.Unspellable(leg with { Depth = Length.Inches(30) }));
        Assert.IsType<Saved>(ProjectFile.Save(new MemoryStream(), sketch));
    }
}

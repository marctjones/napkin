using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// What the reader accepts: every entity kind, every reference shape, and a sketch that equals
/// the file's contents by value.
/// </summary>
public sealed class SceneReaderTests
{
    [Fact]
    [Trait("Feature", "PRJ-001")]
    public void A_scene_loads_to_the_sketch_it_describes_by_value()
    {
        Sketch expected = Sketch.Empty
            .WithEntity(Box.AsDrawn(
                new EntityId(Guid.Parse(Scenes.BoxId)),
                LayerId.Default,
                new Point2(Length.Zero, Length.Zero),
                new Length(30720),
                new Length(4096),
                new Length(768),
                Angle.Zero) with
            {
                Name = "Shelf",
                Part = new Part(
                    "1x6",
                    Species: null,
                    Quantity: 1,
                    new PlanAxes(PartDimension.Length, PartDimension.Width)),
            })
            .WithRelationship(new ParamValue(
                new RelationshipId(Guid.Parse(Scenes.RelationshipId)),
                new BoxWidthRef(new EntityId(Guid.Parse(Scenes.BoxId))),
                new Length(30720)));

        Assert.Equal(expected, Scenes.Accept(Scenes.OneBox));
    }

    [Fact]
    [Trait("Feature", "PRJ-001")]
    public void An_empty_design_loads_as_an_empty_sketch()
        => Assert.Equal(Sketch.Empty, Scenes.Accept(Scenes.Empty));

    [Fact]
    [Trait("Feature", "PRJ-001")]
    public void Nodes_segments_and_dimensions_load_with_their_parts()
    {
        Sketch sketch = Scenes.Accept(Scenes.SegmentAndDimension);

        Node start = Assert.IsType<Node>(sketch.Find(new EntityId(Guid.Parse(Scenes.NodeId))));
        Node end = Assert.IsType<Node>(sketch.Find(new EntityId(Guid.Parse(Scenes.SecondNodeId))));
        Segment segment = Assert.IsType<Segment>(sketch.Find(new EntityId(Guid.Parse(Scenes.SegmentId))));
        Dimension dimension = Assert.IsType<Dimension>(sketch.Find(new EntityId(Guid.Parse(Scenes.DimensionId))));

        Assert.Equal(0, start.Position.X.Units);
        Assert.Equal(30720, end.Position.X.Units);
        Assert.Equal(start.Id, segment.Start);
        Assert.Equal(end.Id, segment.End);

        // A reference dimension: it measures, and no relationship owns its number (design §3.3).
        Assert.Null(dimension.Drives);
        AxisMeasurand measurand = Assert.IsType<AxisMeasurand>(dimension.Measures);
        Assert.Equal(Axis.X, measurand.Axis);
        Assert.Equal(new NodeRef(start.Id), measurand.From);
        Assert.Equal(new NodeRef(end.Id), measurand.To);
        Assert.Equal(2048, dimension.Placement.Offset.Units);
        Assert.Equal(DimensionSide.South, dimension.Placement.Side);

        // The segment's length is driven, and the segment is held horizontal.
        Assert.Contains(sketch.RelationshipsInOrder, relationship => relationship is Horizontal);
        ParamValue driven = Assert.IsType<ParamValue>(sketch.Find(new RelationshipId(Guid.Parse(Scenes.RelationshipId))));
        Assert.Equal(new SegmentLengthRef(segment.Id), driven.Param);
        Assert.Equal(30720, driven.Value.Units);
    }

    [Fact]
    [Trait("Feature", "PRJ-001")]
    public void A_rotation_loads_as_stored_and_its_corners_come_out_where_the_file_says()
    {
        Sketch sketch = Scenes.Accept(Scenes.TurnedBoxAndNode);

        Box box = Assert.IsType<Box>(sketch.Find(new EntityId(Guid.Parse(Scenes.BoxId))));
        Assert.Equal(324000, box.Rotation.Arcseconds);
        Assert.True(box.Rotation.IsRightAngleMultiple);

        // A quarter turn takes the local offset (30720, 0) to (0, 30720), exactly.
        Point2 corner = box.Corner(BoxCorner.SouthEast);
        Assert.Equal(0, corner.X.Units);
        Assert.Equal(30720, corner.Y.Units);

        // Which is where the node is, so the file's own coincident holds.
        Assert.True(RelationshipChecker.Check(sketch).AllHold);
    }

    [Fact]
    [Trait("Feature", "PRJ-001")]
    public void Every_reference_shape_the_format_defines_loads()
    {
        Sketch sketch = Scenes.Accept(AllReferenceShapes);

        Assert.Equal(5, sketch.Entities.Count);
        Assert.Equal(6, sketch.Relationships.Count);
        Assert.True(sketch.Validate().IsValid, sketch.Validate().ToString());
        Assert.True(RelationshipChecker.Check(sketch).AllHold, RelationshipChecker.Check(sketch).ToString());

        Assert.Collection(
            sketch.RelationshipsInOrder,
            relationship => Assert.IsType<Anchored>(relationship),
            relationship => Assert.IsType<Coincident>(relationship),
            relationship => Assert.IsType<Vertical>(relationship),
            relationship => Assert.IsType<Flush>(relationship),
            relationship => Assert.IsType<AxisDistance>(relationship),
            relationship => Assert.IsType<Centered>(relationship));
    }

    /// <summary>
    /// Two 8 inch by 8 inch boxes side by side from x = 0 and x = 8192, a node at their shared
    /// corner (8192, 0), a vertical segment from it, and one relationship of every shape that
    /// refers to a point, an edge or a size:
    /// <list type="bullet">
    ///   <item>the first box anchored,</item>
    ///   <item>its south-east corner coincident with the node,</item>
    ///   <item>the segment vertical,</item>
    ///   <item>the two boxes' south edges flush,</item>
    ///   <item>8192 units along X from the first box's south-west corner to the second's,</item>
    ///   <item>the node midway between the two boxes' centres along X: the centres are at
    ///     (4096, 4096) and (12288, 4096), and (4096 + 12288) / 2 = 8192, which is the node.</item>
    /// </list>
    /// </summary>
    private const string AllReferenceShapes = """
        {
          "formatVersion": 11,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "West square", "phase": "new",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 8192, "height": 8192, "depth": 768, "faceUp": "top", "rotation": 0,
              "part": { "stock": null, "species": "white oak", "quantity": 2,
                        "planAxes": { "x": "length", "y": "width" }, "hardware": [], "rough": false }, "wall": null, "room": null, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000b", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "East square", "phase": "new",
              "anchor": { "x": 8192, "y": 0, "z": 0 }, "width": 8192, "height": 8192, "depth": 768, "faceUp": "top", "rotation": 0,
              "part": null, "wall": null, "room": null, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000c", "type": "node", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Meeting point", "phase": "new", "position": { "x": 8192, "y": 0 } },
            { "id": "0192f1a0-0000-4000-8000-00000000000d", "type": "node", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "phase": "new", "position": { "x": 8192, "y": 16384 } },
            { "id": "0192f1a0-0000-4000-8000-00000000000e", "type": "segment", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Joint line", "phase": "new",
              "start": "0192f1a0-0000-4000-8000-00000000000c", "end": "0192f1a0-0000-4000-8000-00000000000d" }
          ],
          "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null, "source": null },
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-000000000001", "kind": "anchored",
              "entity": "0192f1a0-0000-4000-8000-00000000000a" },
            { "id": "0192f1a0-0000-4000-8000-000000000002", "kind": "coincident",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south", "east"] },
              "b": { "kind": "node", "node": "0192f1a0-0000-4000-8000-00000000000c" } },
            { "id": "0192f1a0-0000-4000-8000-000000000003", "kind": "vertical",
              "edge": { "kind": "segment", "segment": "0192f1a0-0000-4000-8000-00000000000e" } },
            { "id": "0192f1a0-0000-4000-8000-000000000004", "kind": "flush",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["south"] } },
            { "id": "0192f1a0-0000-4000-8000-000000000005", "kind": "axisDistance",
              "from": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south", "west"] },
              "to": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["south", "west"] },
              "axis": "x", "distance": 8192 },
            { "id": "0192f1a0-0000-4000-8000-000000000006", "kind": "centered",
              "middle": { "kind": "node", "node": "0192f1a0-0000-4000-8000-00000000000c" },
              "a": { "kind": "center", "box": "0192f1a0-0000-4000-8000-00000000000a" },
              "b": { "kind": "center", "box": "0192f1a0-0000-4000-8000-00000000000b" },
              "axis": "x" }
          ]
        }
        """;
}

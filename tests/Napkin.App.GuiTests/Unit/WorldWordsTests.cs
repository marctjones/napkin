using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// One set of direction words, each naming the way a face faces now (#81).
/// </summary>
public class WorldWordsTests
{
    static Box Turned(BoxFace faceUp, int quarterTurns = 0) => new(
        EntityId.New(), LayerId.Default, Point3.Inches(0, 0, 0), Length.Inches(4), Length.Inches(3), Length.Inches(2), faceUp, Angle.Right * quarterTurns);

    [Theory]
    [InlineData(BoxFace.South, "south face")]
    [InlineData(BoxFace.East, "east face")]
    [InlineData(BoxFace.Top, "top face")]
    [InlineData(BoxFace.Bottom, "bottom face")]
    public void A_part_as_drawn_is_named_by_its_own_faces(BoxFace face, string expected) =>
        Assert.Equal(expected, WorldWords.Feature(Turned(BoxFace.Top), BoxFeature.Face(face)));

    [Fact]
    public void An_upright_edge_is_a_corner_a_level_edge_an_edge_and_three_faces_a_corner()
    {
        Box box = Turned(BoxFace.Top);
        Assert.Equal("south-west corner", WorldWords.Feature(box, BoxFeature.LocalUpright(BoxCorner.SouthWest)));
        Assert.Equal("bottom east edge", WorldWords.Feature(box, BoxFeature.Edge(BoxFace.East, BoxFace.Bottom)));
        Assert.Equal("top north-east corner", WorldWords.Feature(box, BoxFeature.Vertex(BoxCorner.NorthEast, BoxLevel.Top)));
    }

    [Fact]
    public void A_turned_part_is_named_by_the_way_its_faces_face_now()
    {
        // Tipped over with its east face up: its drawn top faces west (assembly-model §1.3).
        Assert.Equal("west face", WorldWords.Feature(Turned(BoxFace.East), BoxFeature.Face(BoxFace.Top)));
        Assert.Equal("top face", WorldWords.Feature(Turned(BoxFace.East), BoxFeature.Face(BoxFace.East)));

        // Tipped back: the drawn south-west upright lies level along the bottom west.
        Assert.Equal("bottom west edge", WorldWords.Feature(Turned(BoxFace.North), BoxFeature.LocalUpright(BoxCorner.SouthWest)));

        // Spun a quarter turn in the plan: the drawn south face faces east.
        Assert.Equal("east face", WorldWords.Feature(Turned(BoxFace.Top, 1), BoxFeature.Face(BoxFace.South)));
    }

    [Theory]
    [InlineData(Axis.X, 1, "east of")]
    [InlineData(Axis.X, -1, "west of")]
    [InlineData(Axis.Y, 1, "north of")]
    [InlineData(Axis.Y, -1, "south of")]
    [InlineData(Axis.Z, 1, "above")]
    [InlineData(Axis.Z, -1, "below")]
    public void A_distance_is_east_west_north_south_above_or_below(Axis axis, int sign, string expected) =>
        Assert.Equal(expected, WorldWords.Direction(axis, Length.Inches(sign)));

    [Fact]
    public void A_distance_reads_in_compass_words()
    {
        Box top = Box.AsDrawn(EditingBuilder.Id(0), LayerId.Default, Point2.Inches(0, 0), Length.Inches(48), Length.Inches(24), Length.Inches(0, 3, 4), Angle.Zero);
        Box leg = Box.AsDrawn(EditingBuilder.Id(1), LayerId.Default, Point2.Inches(1, 20), Length.Inches(2), Length.Inches(2), Length.Inches(16), Angle.Zero);
        Sketch sketch = Sketch.Empty.WithEntity(top).WithEntity(leg);
        AxisDistance inset = new(
            RelationshipId.New(),
            new FeatureRef(top.Id, BoxFeature.LocalUpright(BoxCorner.NorthWest)),
            new FeatureRef(leg.Id, BoxFeature.LocalUpright(BoxCorner.NorthWest)),
            Axis.Y,
            -Length.Inches(2));

        Assert.Equal(
            "Leg's north-west corner is 2\" south of Top's north-west corner.",
            RelationshipText.Describe(sketch, inset, id => id == top.Id ? "Top" : "Leg", LengthFormat.Default));
    }
}

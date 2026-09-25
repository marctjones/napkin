using Napkin.Modules.Editing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

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

/// <summary>
/// The rest of the direction vocabulary — the six facings, the three axes as runs and as
/// centring phrases — and the plan's stance mark for a turned part (#82).
/// </summary>
public class WorldWordsVocabularyTests
{
    static Box Standing(BoxFace faceUp, Part? part = null) => new(
        EntityId.New(), LayerId.Default, Point3.Inches(0, 0, 0), Length.Inches(4), Length.Inches(3), Length.Inches(2), faceUp, Angle.Zero)
    { Part = part };

    [Theory]
    [InlineData(Axis.X, true, "east")]
    [InlineData(Axis.X, false, "west")]
    [InlineData(Axis.Y, true, "north")]
    [InlineData(Axis.Y, false, "south")]
    [InlineData(Axis.Z, true, "top")]
    [InlineData(Axis.Z, false, "bottom")]
    public void Each_world_direction_has_one_word(Axis axis, bool positive, string expected) =>
        Assert.Equal(expected, WorldWords.Facing(axis, positive));

    [Fact]
    public void The_three_axes_read_as_runs_and_as_centring_phrases_each_different()
    {
        Axis[] axes = [Axis.X, Axis.Y, Axis.Z];

        Assert.Equal(3, axes.Select(WorldWords.Along).Distinct().Count());
        Assert.Equal(3, axes.Select(WorldWords.Across).Distinct().Count());

        // A centring phrase names both ends of its axis, in the facing words.
        foreach (Axis axis in axes)
        {
            string across = WorldWords.Across(axis);
            Assert.Contains(WorldWords.Facing(axis, true), across, StringComparison.Ordinal);
            Assert.Contains(WorldWords.Facing(axis, false), across, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_value_that_is_not_an_axis_has_no_direction_or_centring_phrase()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldWords.Direction((Axis)7, Length.Inches(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldWords.Across((Axis)7));
    }

    [Fact]
    public void Zero_is_on_the_positive_side()
    {
        Assert.Equal(WorldWords.Direction(Axis.X, Length.Inches(1)), WorldWords.Direction(Axis.X, Length.Zero));
    }

    [Fact]
    public void Without_a_box_or_off_the_quarter_turns_a_feature_is_named_by_its_own_faces()
    {
        BoxFeature edge = BoxFeature.Edge(BoxFace.East, BoxFace.Bottom);
        Box skewed = Standing(BoxFace.East) with { Rotation = Angle.Degrees(30) };

        Assert.Equal(PlaceRules.InWords(edge), WorldWords.Feature(null, edge));
        Assert.Equal(PlaceRules.InWords(edge), WorldWords.Feature(skewed, edge));
    }

    [Fact]
    public void A_box_as_drawn_or_off_the_quarter_turns_has_no_stance_mark()
    {
        Assert.Null(WorldWords.Stance(Standing(BoxFace.Top)));
        Assert.Null(WorldWords.Stance(Standing(BoxFace.East) with { Rotation = Angle.Degrees(30) }));
        Assert.Throws<ArgumentNullException>(() => WorldWords.Stance(null!));
    }

    [Fact]
    public void A_box_upside_down_is_turned_over_and_one_on_its_side_names_the_size_that_stands_up()
    {
        Assert.Equal("turned over", WorldWords.Stance(Standing(BoxFace.Bottom)));

        // East or west face up: the box's own X (its width) is now vertical; north or south: its Y.
        Assert.Equal("↑ width", WorldWords.Stance(Standing(BoxFace.East)));
        Assert.Equal("↑ width", WorldWords.Stance(Standing(BoxFace.West)));
        Assert.Equal("↑ height", WorldWords.Stance(Standing(BoxFace.North)));
        Assert.Equal("↑ height", WorldWords.Stance(Standing(BoxFace.South)));
    }

    [Fact]
    public void A_part_on_its_side_names_the_size_by_the_part_s_own_dimension_names()
    {
        // A board drawn with its length along X and its width along Y.
        Part board = new(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width));

        Assert.Equal("↑ length", WorldWords.Stance(Standing(BoxFace.East, board)));
        Assert.Equal("↑ width", WorldWords.Stance(Standing(BoxFace.North, board)));
    }
}

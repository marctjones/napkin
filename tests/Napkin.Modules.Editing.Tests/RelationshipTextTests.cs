using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The sentence the relationship list shows for each kind the editor can hold, the places and
/// sizes those sentences are built from, and the words for the kinds reserved for the solver.
/// </summary>
/// <remarks>
/// A sentence is checked for what it must carry — the names of the parts it is about, the words
/// for the places, the direction — rather than copied whole, so rewording one is not a test
/// failure but dropping a part's name from it is.
/// </remarks>
public class RelationshipTextTests
{
    static readonly EntityId Top = EditingBuilder.Id(0);
    static readonly EntityId Leg = EditingBuilder.Id(1);
    static readonly EntityId Rail = EditingBuilder.Id(2);

    static readonly Sketch Scene = Sketch.Empty
        .WithEntity(Box.AsDrawn(Top, LayerId.Default, Point2.Inches(0, 0), Length.Inches(48), Length.Inches(24), Length.Inches(1), Angle.Zero))
        .WithEntity(Box.AsDrawn(Leg, LayerId.Default, Point2.Inches(0, 0), Length.Inches(2), Length.Inches(2), Length.Inches(16), Angle.Zero))
        .WithEntity(Box.AsDrawn(Rail, LayerId.Default, Point2.Inches(2, 0), Length.Inches(20), Length.Inches(1), Length.Inches(3), Angle.Zero));

    static string Name(EntityId id) => id == Top ? "Top" : id == Leg ? "Leg" : id == Rail ? "Rail" : "?";

    static string Describe(Relationship relationship) =>
        RelationshipText.Describe(Scene, relationship, Name, LengthFormat.Default);

    static FeatureRef Face(EntityId box, BoxFace face) => new(box, BoxFeature.Face(face));

    [Fact]
    public void A_strut_place_names_the_strut_and_which_end_or_face()
    {
        Assert.Equal("Leg's first end", RelationshipText.Place(Scene, new StrutEndRef(Leg, StrutEnd.From), Name));
        Assert.Equal("Leg's second end", RelationshipText.Place(Scene, new StrutEndRef(Leg, StrutEnd.To), Name));
        Assert.Equal("Leg's bottom face", RelationshipText.Place(Scene, new StrutFaceRef(Leg, StrutFace.Bottom), Name));
        Assert.Equal("Leg's second end face", RelationshipText.Place(Scene, new StrutEndFaceRef(Leg, StrutEnd.To), Name));
        Assert.Equal("Leg's first end face", RelationshipText.Place(Scene, new StrutEndFaceRef(Leg, StrutEnd.From), Name));
    }

    [Fact]
    public void Centred_names_the_middle_both_sides_and_the_run_it_is_centred_along()
    {
        string text = Describe(new Centered(RelationshipId.New(), new CenterRef(Rail), Face(Leg, BoxFace.East), Face(Top, BoxFace.East), Axis.X));

        Assert.StartsWith(RelationshipText.Place(Scene, new CenterRef(Rail), Name), text, StringComparison.Ordinal);
        Assert.Contains(RelationshipText.Place(Scene, Face(Leg, BoxFace.East), Name), text, StringComparison.Ordinal);
        Assert.Contains(RelationshipText.Place(Scene, Face(Top, BoxFace.East), Name), text, StringComparison.Ordinal);
        Assert.Contains(WorldWords.Across(Axis.X), text, StringComparison.Ordinal);
    }

    [Fact]
    public void Level_and_plumb_are_different_sentences_about_the_same_edge()
    {
        FeatureRef edge = new(Rail, BoxFeature.Edge(BoxFace.South, BoxFace.Top));

        string level = Describe(new Horizontal(RelationshipId.New(), edge));
        string plumb = Describe(new Vertical(RelationshipId.New(), edge));

        Assert.NotEqual(level, plumb);
        Assert.StartsWith(RelationshipText.Place(Scene, edge, Name), level, StringComparison.Ordinal);
        Assert.StartsWith(RelationshipText.Place(Scene, edge, Name), plumb, StringComparison.Ordinal);
    }

    [Fact]
    public void A_joint_reads_as_its_own_tooltip()
    {
        Joint joint = new(RelationshipId.New(), Face(Leg, BoxFace.East), Face(Rail, BoxFace.West), JointType.Butt, null, Fastening.None, false);

        Assert.Equal(JointTooltip.Of(Scene, joint, Name), Describe(joint));
    }

    [Fact]
    public void A_distance_along_an_axis_says_how_far_which_way_and_from_what()
    {
        AxisDistance west = new(RelationshipId.New(), Face(Top, BoxFace.West), Face(Leg, BoxFace.West), Axis.X, Length.Inches(-3));

        string text = Describe(west);

        // The distance is stated as a magnitude, the sign turned into the direction word.
        Assert.Contains(Length.Inches(3).Format(LengthFormat.Default).Text, text, StringComparison.Ordinal);
        Assert.DoesNotContain("-", text, StringComparison.Ordinal);
        Assert.Contains(WorldWords.Direction(Axis.X, Length.Inches(-3)), text, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_reserved_kind_is_said_in_its_own_words_and_the_list_says_this_build_cannot_hold_it()
    {
        NodeRef a = new(EntityId.New());
        NodeRef b = new(EntityId.New());
        SegmentRef edge = new(EntityId.New());
        Relationship[] reserved =
        [
            new Napkin.Core.Geometry.Parallel(RelationshipId.New(), edge, edge),
            new Perpendicular(RelationshipId.New(), edge, edge),
            new AngleBetween(RelationshipId.New(), edge, edge, Angle.Degrees(30)),
            new Distance(RelationshipId.New(), a, b, Length.Inches(5)),
            new PointOnEdge(RelationshipId.New(), a, edge),
            new Symmetric(RelationshipId.New(), a, b, edge),
            new Tangent(RelationshipId.New(), edge, edge),
            new Radius(RelationshipId.New(), EntityId.New(), Length.Inches(2)),
        ];

        string[] kinds = [.. reserved.Select(RelationshipText.Kind)];

        Assert.Equal(reserved.Length, kinds.Distinct().Count());
        Assert.All(kinds, kind => Assert.False(string.IsNullOrWhiteSpace(kind)));
        for (int i = 0; i < reserved.Length; i++)
        {
            string text = Describe(reserved[i]);
            Assert.StartsWith(kinds[i], text, StringComparison.Ordinal);
            Assert.NotEqual(kinds[i], text);
        }
    }

    [Fact]
    public void A_kind_napkin_has_no_words_for_is_named_by_its_type()
    {
        Assert.Equal(nameof(Unheard), RelationshipText.Kind(new Unheard(RelationshipId.New())));
    }

    [Fact]
    public void Places_are_named_after_their_owner()
    {
        EntityId node = EntityId.New();
        EntityId segment = EntityId.New();
        string NameAll(EntityId id) => id == node ? "Point A" : id == segment ? "Line 1" : Name(id);

        Assert.Equal("Point A", RelationshipText.Place(Scene, new NodeRef(node), NameAll));
        Assert.Equal("Line 1", RelationshipText.Place(Scene, new SegmentRef(segment), NameAll));
        Assert.StartsWith("Rail", RelationshipText.Place(Scene, new CenterRef(Rail), NameAll), StringComparison.Ordinal);
        Assert.NotEqual(
            RelationshipText.Place(Scene, new CenterRef(Rail), NameAll),
            RelationshipText.Place(Scene, Face(Rail, BoxFace.Top), NameAll));

        // A face is named by the way it faces now, the same words the rest of the canvas uses.
        Assert.Contains(
            WorldWords.Feature(Scene.Find<Box>(Rail), BoxFeature.Face(BoxFace.Top)),
            RelationshipText.Place(Scene, Face(Rail, BoxFace.Top), NameAll),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Every_size_names_its_owner_and_the_four_sizes_differ()
    {
        EntityId segment = EntityId.New();
        string NameAll(EntityId id) => id == segment ? "Line 1" : Name(id);
        ParamRef[] sizes = [new BoxWidthRef(Rail), new BoxHeightRef(Rail), new BoxDepthRef(Rail), new SegmentLengthRef(segment)];

        string[] said = [.. sizes.Select(size => RelationshipText.Size(size, NameAll))];

        Assert.Equal(4, said.Distinct().Count());
        Assert.All(said.Take(3), text => Assert.StartsWith("Rail", text, StringComparison.Ordinal));
        Assert.StartsWith("Line 1", said[3], StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_helpers_need_their_arguments()
    {
        Relationship pin = new Anchored(RelationshipId.New(), Rail);
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Describe(null!, pin, Name, LengthFormat.Default));
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Describe(Scene, null!, Name, LengthFormat.Default));
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Describe(Scene, pin, null!, LengthFormat.Default));
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Describe(Scene, pin, Name, null!));
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Place(null!, new CenterRef(Rail), Name));
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Place(Scene, null!, Name));
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Place(Scene, new CenterRef(Rail), null!));
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Size(null!, Name));
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Size(new BoxWidthRef(Rail), null!));
        Assert.Throws<ArgumentNullException>(() => RelationshipText.Kind(null!));
    }

    /// <summary>A relationship kind nothing in napkin knows, as a newer file might carry.</summary>
    sealed record Unheard(RelationshipId Id) : Relationship(Id)
    {
        public override IEnumerable<EntityId> References => [];
    }
}

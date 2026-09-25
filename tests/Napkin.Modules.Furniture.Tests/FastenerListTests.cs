using System.Collections.Immutable;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Project;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Fastener recipes and the fastener list (docs/design/joinery-and-fasteners.md &#xA7;7). Every
/// expectation is arithmetic done by hand in a comment, in inches, never napkin's own output.
/// </summary>
public sealed class FastenerListTests
{
    private static Length In(double inches) => new((long)(inches * 1024));

    // ------------------------------------------------------------------------------------------------
    // Recipes: max(least, ceil(L / spacing)) at the note's five lengths, 1 1/2, 3 1/2, 5 1/2, 16 and 36.
    // ------------------------------------------------------------------------------------------------

    [Theory]
    // pocket screws, one per 2 in, least 2:  ceil(.75)=1->2, ceil(1.75)=2, ceil(2.75)=3, 8, 18
    [InlineData(FasteningKind.PocketScrews, 2, 2, 3, 8, 18)]
    // screws, one per 6 in, least 2:  1, 1, 1 -> 2; ceil(2.67)=3; 6
    [InlineData(FasteningKind.Screws, 2, 2, 2, 3, 6)]
    // brads, one per 1.5 in, least 2:  1->2; ceil(2.33)=3; ceil(3.67)=4; ceil(10.67)=11; 24
    [InlineData(FasteningKind.Brads, 2, 3, 4, 11, 24)]
    // nails, one per 4 in, least 2:  1->2; 1->2; ceil(1.375)=2; 4; 9
    [InlineData(FasteningKind.Nails, 2, 2, 2, 4, 9)]
    // dowels: the same spacing as nails
    [InlineData(FasteningKind.Dowels, 2, 2, 2, 4, 9)]
    // biscuits, one per 8 in, least 1:  1, 1, 1, 2, ceil(4.5)=5
    [InlineData(FasteningKind.Biscuits, 1, 1, 1, 2, 5)]
    // tabletop clips, one per 12 in, least 2:  2, 2, 2, ceil(1.33)=2, 3
    [InlineData(FasteningKind.Clips, 2, 2, 2, 2, 3)]
    [Trait("Feature", "CUT-011")]
    public void A_recipes_count_follows_its_spacing_and_its_least(FasteningKind kind, int at1p5, int at3p5, int at5p5, int at16, int at36)
    {
        Assert.Equal(at1p5, Recipes.Recipe(kind, In(1.5)));
        Assert.Equal(at3p5, Recipes.Recipe(kind, In(3.5)));
        Assert.Equal(at5p5, Recipes.Recipe(kind, In(5.5)));
        Assert.Equal(at16, Recipes.Recipe(kind, In(16)));
        Assert.Equal(at36, Recipes.Recipe(kind, In(36)));
    }

    [Theory]
    [InlineData(FasteningKind.PocketScrews, FastenerKind.PocketScrew)]
    [InlineData(FasteningKind.Screws, FastenerKind.WoodScrew)]
    [InlineData(FasteningKind.Brads, FastenerKind.Brad)]
    [InlineData(FasteningKind.Nails, FastenerKind.Nail)]
    [InlineData(FasteningKind.Dowels, FastenerKind.Dowel)]
    [InlineData(FasteningKind.Biscuits, FastenerKind.Biscuit)]
    [InlineData(FasteningKind.Clips, FastenerKind.TabletopClip)]
    [Trait("Feature", "CUT-011")]
    public void Each_fastening_is_made_of_its_own_kind_of_fastener(FasteningKind fastening, FastenerKind fastener)
        => Assert.Equal(fastener, Recipes.FastenerOf(fastening));

    [Fact]
    [Trait("Feature", "CUT-011")]
    public void No_fastening_takes_none_and_names_no_fastener()
    {
        Assert.Equal(0, Recipes.Recipe(FasteningKind.None, In(36)));
        Assert.Null(Recipes.FastenerOf(FasteningKind.None));
    }

    // ------------------------------------------------------------------------------------------------
    // A post with rails butted to its east face, each a joint of a chosen length (the contact's long side).
    //   R1 3/4 thick, 5 1/2 long, pocket screws: 3
    //   R2 3/4 thick, 1 1/2 long, pocket screws: 2
    //   R3 3/4 thick, 5 1/2 long, pocket screws, typed count 5: 5
    //   R4 1/2 thick, 3 1/2 long, brads: 3
    //   R5 3/4 thick, 16 long, screws: 3
    //   R6 1/2 thick, 1 1/2 long, screws: 2, two copies of the part: 4
    // ------------------------------------------------------------------------------------------------

    private static readonly PlanAxes LengthThickness = new(PartDimension.Length, PartDimension.Thickness);

    private static Sketch Rails(ImmutableList<FastenerChoice> choices)
    {
        int next = 0;
        Sketch sketch = Sketch.Empty with { FastenerChoices = choices };

        Box Add(string name, double x0, double x1, double y1, double z0, double z1, int quantity)
        {
            Box box = new(
                new EntityId(new Guid(++next, 0, 0, new byte[8])),
                LayerId.Default,
                new Point3(In(x0), Length.Zero, In(z0)),
                In(x1 - x0),
                In(y1),
                In(z1 - z0),
                BoxFace.Top,
                Angle.Zero)
            {
                Name = name,
                Part = new Part(null, null, quantity, LengthThickness),
            };
            sketch = sketch.WithEntity(box);
            return box;
        }

        Box post = Add("Post", 0, 1.5, 1.5, 0, 40, 1);
        void Join(Box rail, Fastening fastening) => sketch = sketch.WithRelationship(new Joint(
            new RelationshipId(new Guid(++next, 1, 0, new byte[8])),
            new FeatureRef(post.Id, BoxFeature.Face(BoxFace.East)),
            new FeatureRef(rail.Id, BoxFeature.Face(BoxFace.West)),
            JointType.Butt,
            null,
            fastening,
            false));

        Fastening pocket = new(FasteningKind.PocketScrews, null, BoxFace.Top);
        Join(Add("Rail 1", 1.5, 7.5, 0.75, 0, 5.5, 1), pocket);
        Join(Add("Rail 2", 1.5, 7.5, 0.75, 6, 7.5, 1), pocket);
        Join(Add("Rail 3", 1.5, 7.5, 0.75, 8, 13.5, 1), pocket with { Count = 5 });
        Join(Add("Rail 4", 1.5, 7.5, 0.5, 14, 17.5, 1), new Fastening(FasteningKind.Brads, null, null));
        Join(Add("Rail 5", 1.5, 7.5, 0.75, 18, 34, 1), new Fastening(FasteningKind.Screws, null, null));
        Join(Add("Rail 6", 1.5, 7.5, 0.5, 34, 35.5, 2), new Fastening(FasteningKind.Screws, null, null));
        Join(Add("Rail 7", 1.5, 7.5, 0.75, 36, 39, 1), Fastening.None);
        return sketch;
    }

    [Fact]
    [Trait("Feature", "CUT-012")]
    public void Counts_are_summed_by_kind_and_thickness_with_the_typed_size_and_packs()
    {
        Sketch sketch = Rails([
            new FastenerChoice(FastenerKind.PocketScrew, new Length(768), "S1", 4),
            new FastenerChoice(FastenerKind.WoodScrew, new Length(512), "S2", null),
        ]);

        ImmutableArray<FastenerRow> rows = FastenerList.Of(sketch);

        // Sorted by kind (pocket screw, wood screw, brad), thickness descending (3/4 before 1/2); rail 7 has no fastening.
        Assert.Equal(
            [
                (FastenerKind.PocketScrew, 768L, "S1", 10, 3),   // 3 + 2 + typed 5 = 10; ceil(10 / 4) = 3 packs
                (FastenerKind.WoodScrew, 768L, string.Empty, 3, 0),   // 3 (16 in / 6 = 2.67, up), no choice
                (FastenerKind.WoodScrew, 512L, "S2", 4, 0),   // 2 per copy x 2 copies = 4; no pack size
                (FastenerKind.Brad, 512L, string.Empty, 3, 0),   // ceil(3.5 / 1.5) = 3, no choice
            ],
            rows.Select(row => (row.Kind, row.Thickness!.Value.Units, row.SizeText, row.Count, row.Packs ?? 0)));

        Assert.Equal(["size not chosen"], rows.Where(row => row.SizeText.Length == 0).Select(row => row.Note).Distinct());
        Assert.Equal(string.Empty, rows[0].Note);
        Assert.Null(rows[2].PackSize);
        Assert.Null(rows[2].Packs);
    }

    [Fact]
    [Trait("Feature", "CUT-012")]
    public void A_line_is_traceable_to_its_joints()
    {
        ImmutableArray<FastenerRow> rows = FastenerList.Of(Rails([]));
        FastenerRow pocket = rows[0];

        Assert.Equal(3, pocket.Sources.Length);
        Assert.Equal(["Rail 1", "Rail 2", "Rail 3"], pocket.Sources.Select(source => source.Inserted));
        Assert.Equal([false, false, true], pocket.Sources.Select(source => source.Typed));
        Assert.Equal("3 + 2 + 5 = 10", pocket.Derivation);
        Assert.Equal("Rail 1 → Post, Rail 2 → Post, Rail 3 → Post", pocket.For);
        Assert.Equal("2×2 = 4", rows[2].Derivation);
    }

    [Fact]
    [Trait("Feature", "CUT-012")]
    public void Two_joints_between_the_same_parts_are_written_once_with_a_multiplier()
    {
        Sketch sketch = Rails([]);
        Joint again = sketch.RelationshipsInOrder.OfType<Joint>().First() with { Id = new RelationshipId(Guid.NewGuid()) };
        sketch = sketch.WithRelationship(again);

        FastenerRow pocket = FastenerList.Of(sketch)[0];

        Assert.Equal(13, pocket.Count);
        Assert.StartsWith("Rail 1 → Post × 2, ", pocket.For);
    }

    [Fact]
    [Trait("Feature", "CUT-012")]
    public void A_joint_whose_parts_have_moved_apart_adds_nothing_unless_it_has_a_typed_count()
    {
        Sketch sketch = Rails([]);
        Box rail = sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Rail 1");
        sketch = sketch.WithEntity(rail with { Anchor = rail.Anchor with { X = In(10) } });

        FastenerRow pocket = FastenerList.Of(sketch)[0];

        Assert.Equal(7, pocket.Count);   // 2 + 5; rail 1's recipe needs a contact
        Assert.Contains("1 joint apart, not counted", pocket.Note);

        Box second = sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Rail 2");
        pocket = FastenerList.Of(sketch.WithEntity(second with { Anchor = second.Anchor with { X = In(10) } }))[0];

        Assert.Equal(5, pocket.Count);   // only the typed 5
        Assert.Contains("2 joints apart, not counted", pocket.Note);
    }

    [Fact]
    [Trait("Feature", "CUT-012")]
    public void A_tabletop_clip_ignores_thickness_and_reads_a_choice_without_one()
    {
        Sketch sketch = Rails([new FastenerChoice(FastenerKind.TabletopClip, null, "figure-8", 8)]);
        Joint clips = sketch.RelationshipsInOrder.OfType<Joint>().First() with
        {
            Type = JointType.Tabletop,
            Fastening = new Fastening(FasteningKind.Clips, null, null),
        };
        sketch = sketch.WithRelationship(clips);   // a joint with this id replaces the old one

        FastenerRow clip = FastenerList.Of(sketch).Single(row => row.Kind == FastenerKind.TabletopClip);

        Assert.Null(clip.Thickness);
        Assert.Equal("figure-8", clip.SizeText);
        Assert.Equal(2, clip.Count);   // 5 1/2 in / 12 = 1, least 2
        Assert.Equal(1, clip.Packs);
    }

    // ------------------------------------------------------------------------------------------------
    // The sample: the five rows of note 7.4 against samples/diy-coffee-table-drawers.expected.json.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "CUT-012")]
    public void The_coffee_table_sample_needs_the_hand_worked_fasteners()
    {
        Sketch sketch = Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath("diy-coffee-table-drawers"))).Sketch;
        using FileStream stream = File.OpenRead(Path.Combine(ExpectedFixture.Directory, "diy-coffee-table-drawers.expected.json"));
        JsonElement[] want = [.. JsonDocument.Parse(stream).RootElement.GetProperty("fasteners").EnumerateArray()];

        ImmutableArray<FastenerRow> rows = FastenerList.Of(sketch);

        Assert.Equal(want.Length, rows.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            Assert.Equal(want[i].GetProperty("kind").GetString(), rows[i].Kind.ToString());
            Assert.Equal(want[i].GetProperty("thicknessUnits").ValueKind == JsonValueKind.Null ? null : want[i].GetProperty("thicknessUnits").GetInt64(), rows[i].Thickness?.Units);
            Assert.Equal(want[i].GetProperty("size").GetString(), rows[i].SizeText);
            Assert.Equal(want[i].GetProperty("count").GetInt32(), rows[i].Count);
            Assert.Equal(want[i].GetProperty("packs").GetInt32(), rows[i].Packs);
            Assert.Equal(want[i].GetProperty("packSize").GetInt32(), rows[i].PackSize);
            Assert.Equal(string.Empty, rows[i].Note);
            Assert.False(string.IsNullOrWhiteSpace(want[i].GetProperty("derivation").GetString()));
        }
    }
}

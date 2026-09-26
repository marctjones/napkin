using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// Screens and the 40 % line (deck-and-porch §5.2, §5.5, §9.4, §9.6; §11.2 tests 11–12), worked by
/// hand on §9's porch: three 8'-0" walls on the deck, the front with three glass windows 36 × 60, the
/// sides with screens, the east with a screen door, under a 5-in-12 roof whose sloped area is
/// 144 × 141 3/8 = 20358 sq in (§9.5).
/// </summary>
public class GlazingTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId OpeningLayer = LayerId.New();
    static readonly LayerId DeckLayer = LayerId.New();

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static ExactFraction SquareInches(long whole) => ExactFraction.Whole(whole * 1024 * 1024);

    /// <summary>A wall whose plan bounds start at (<paramref name="west"/>, <paramref name="south"/>), however the turn places its anchor.</summary>
    static Box WallBox(string name, Point3 at, Length length, Angle turn)
    {
        Box probe = new(EntityId.New(), WallLayer, Point3.Origin, length, In(3, 1, 2), In(96), BoxFace.Top, turn) { Name = name };
        Point2[] corners = [.. new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(probe.Corner)];
        return probe with { Anchor = new Point3(at.X - corners.Min(c => c.X), at.Y - corners.Min(c => c.Y), at.Z) };
    }

    /// <summary>An opening placed as the tool places one, with its fill.</summary>
    static Box OpeningIn(Box wall, long at, long height, long sill, OpeningFill? fill)
    {
        Batch batch = (Batch)OpeningPlacement.Request(new Wall(wall), OpeningLayer, EntityId.New(), "Opening", In(at), In(36), In(sill), In(height), fill);
        return (Box)((AddEntity)batch.Requests[0]).Entity;
    }

    static (Sketch Sketch, Deck Deck) Porch(OpeningFill sides = OpeningFill.Screen)
    {
        Box house = new(EntityId.New(), WallLayer, new Point3(Length.Zero, Length.Zero, In(36)), In(240), In(5, 1, 2), In(96), BoxFace.Top, Angle.Zero) { Phase = Phase.Existing };
        Box deck = new(EntityId.New(), DeckLayer, new Point3(In(48), In(-120), Length.Zero), In(144), In(120), In(36), BoxFace.Top, Angle.Zero)
        {
            Deck = new DeckInputs(JoistDirection.Out, In(16), "2x8", new BeamSpec(2, "2x10"), "4x4", 3, Length.Zero, "5/4x6", In(0, 1, 8), true, null, null, null, null, null),
        };
        Box front = WallBox("Front", new Point3(In(48), In(-120), In(36)), In(144), Angle.Zero);
        Box west = WallBox("West", new Point3(In(48), -In(116, 1, 2), In(36)), In(116, 1, 2), Angle.Right);
        Box east = WallBox("East", new Point3(In(188, 1, 2), -In(116, 1, 2), In(36)), In(116, 1, 2), Angle.Right);
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening)).WithLayer(new Layer(DeckLayer, BuildingLayers.Deck))
            .WithEntity(house).WithEntity(deck).WithEntity(front).WithEntity(west).WithEntity(east);
        Box[] openings =
        [
            OpeningIn(front, 9, 60, 24, OpeningFill.Glass), OpeningIn(front, 54, 60, 24, OpeningFill.Glass), OpeningIn(front, 99, 60, 24, null),
            OpeningIn(west, 14, 60, 24, sides), OpeningIn(west, 66, 60, 24, sides) with { },
            OpeningIn(east, 14, 60, 24, sides), OpeningIn(east, 66, 80, 0, sides),
        ];
        sketch = openings.Aggregate(sketch, (with, box) => with.WithEntity(box));
        return (sketch, new Deck(deck));
    }

    [Fact]
    [Trait("Feature", "ROOF-004")]
    public void The_porch_of_section_9_6_is_ten_point_four_percent_glazed_under_the_line()
    {
        (Sketch sketch, Deck deck) = Porch();
        GlazingRatio ratio = Glazing.Of(sketch, deck, In(120), In(50), SquareInches(20358))!;

        // Walls gross: 144 × 96 + 2 × 116 1/2 × 96 = 13824 + 22368 = 36192 sq in.
        Assert.Equal(3, ratio.Walls.Length);
        Assert.Equal((Int128)36192 * 1024 * 1024, ratio.WallsGross);

        // Rake: 2 × ½ × 116 1/2 × (116 1/2 × 50 ÷ 120) = 2 × 2827 53/96 = 5655 5/48 sq in.
        Assert.Equal(new ExactFraction(((Int128)5655 * 48 + 5) * 1024 * 1024, 48), ratio.RakeFill);

        // Glass: the three front windows, 3 × 36 × 60 = 6480 sq in; screens and the screen door count nothing.
        Assert.Equal((Int128)6480 * 1024 * 1024, ratio.Glass);

        // 6480 ÷ 62205 5/48 = 10.4 %.
        Assert.Equal(104, ratio.TenthsOfPercent);
        Assert.False(ratio.OverTheLine);
        Assert.Equal(
            "Glazing 10.4 % of walls and roof (45.0 sq ft of 432.0 sq ft): under the 40 % line, so not a 'sunroom' by IRC 2021 §R202's definition (read via UpCodes 2026-09-25, docs/research/porch-rules.md); an ordinary unconditioned roofed addition.",
            ratio.Text);
    }

    [Fact]
    [Trait("Feature", "ROOF-004")]
    public void Every_screen_glass_is_twenty_five_point_five_and_a_glass_room_crosses_the_line()
    {
        (Sketch sketch, Deck deck) = Porch(OpeningFill.Glass);

        // + 3 × 2160 + 2880 = 9360: 15840 ÷ 62205 5/48 = 25.5 %.
        Assert.Equal(255, Glazing.Of(sketch, deck, In(120), In(50), SquareInches(20358))!.TenthsOfPercent);

        // Even with no roof at all this porch stays under (15840 ÷ 41847 5/48 = 37.9 %); 41 of 100 is over.
        Assert.Equal(379, Glazing.Of(sketch, deck, In(120), In(50), ExactFraction.Whole(0))!.TenthsOfPercent);
        GlazingRatio small = new([], 100, ExactFraction.Whole(0), ExactFraction.Whole(0), 41);
        Assert.False((small with { Glass = 40 }).OverTheLine);
        Assert.True(small.OverTheLine);
        Assert.Contains("over the 40 % line, a 'sunroom' by IRC 2021 §R202's definition", small.Text, StringComparison.Ordinal);
        Assert.Contains("a three-season porch is I, II or III", small.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Fill_reads_as_stored_or_glass_for_a_window_and_solid_for_a_door()
    {
        (Sketch sketch, _) = Porch();
        Wall front = Assert.Single(Wall.All(sketch), wall => wall.Name == "Front");
        Wall east = Assert.Single(Wall.All(sketch), wall => wall.Name == "East");

        Assert.Equal([OpeningFill.Glass, OpeningFill.Glass, OpeningFill.Glass], Opening.In(sketch, front).Select(opening => opening.Fill));
        Assert.Equal([OpeningFill.Screen, OpeningFill.Screen], Opening.In(sketch, east).Select(opening => opening.Fill));

        Box door = OpeningIn(front.Box, 54, 80, 0, null);
        Assert.Equal(OpeningFill.Solid, new Opening(door, front, In(54), Length.Zero).Fill);
    }

    [Fact]
    public void Without_walls_on_the_deck_or_against_the_house_there_is_no_ratio_and_the_roof_line_waits()
    {
        (Sketch sketch, Deck deck) = Porch();
        Sketch bare = Wall.All(sketch).Where(wall => wall.Box.Phase == Phase.New).Aggregate(sketch, (with, wall) => with.WithoutEntity(wall.Id));
        Assert.Null(Glazing.Of(bare, deck, In(120), In(50), SquareInches(20358)));

        Sketch alone = sketch.WithoutEntity(Wall.All(sketch).Single(wall => wall.Box.Phase == Phase.Existing).Id);
        Assert.Null(Glazing.Of(alone, deck, In(120), In(50), SquareInches(20358)));

        Assert.StartsWith("Glazing: draw the porch roof first", Glazing.NoRoof, StringComparison.Ordinal);
    }
}

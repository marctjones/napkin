using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Draw → Porch roof (deck-and-porch §5.3, §8): a click on a deck makes a shed roof over its outline,
/// high at the ledger, low on the wall standing on its far edge — or on a beam and posts at a wall's
/// starting height when no wall is there — at napkin's starting 4 in 12.
/// </summary>
public class RoofToolTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId DeckLayer = LayerId.New();
    static readonly LayerId RoofLayer = LayerId.New();

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    /// <summary>A wall whose plan bounds start at (x, y), however the turn places its anchor.</summary>
    static Box WallBox(string name, Point3 at, Length length, Angle turn, Phase phase = Phase.New)
    {
        Box probe = new(EntityId.New(), WallLayer, Point3.Origin, length, In(3, 1, 2), In(96), BoxFace.Top, turn) { Name = name, Phase = phase };
        Point2[] corners = [.. new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(probe.Corner)];
        return probe with { Anchor = new Point3(at.X - corners.Min(c => c.X), at.Y - corners.Min(c => c.Y), at.Z) };
    }

    static Sketch Layers() => Sketch.Empty
        .WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(DeckLayer, BuildingLayers.Deck)).WithLayer(new Layer(RoofLayer, BuildingLayers.Roof));

    /// <summary>§9's porch: the house's south face on y = 0, the deck 144 × 120 south of it, 3'-0" up.</summary>
    static (Sketch Sketch, Deck Deck) Porch()
    {
        Box house = new(EntityId.New(), WallLayer, new Point3(Length.Zero, Length.Zero, In(36)), In(240), In(5, 1, 2), In(96), BoxFace.Top, Angle.Zero) { Phase = Phase.Existing };
        Box deck = new(EntityId.New(), DeckLayer, new Point3(In(48), In(-120), Length.Zero), In(144), In(120), In(36), BoxFace.Top, Angle.Zero) { Deck = DeckTool.StartingInputs };
        return (Layers().WithEntity(house).WithEntity(deck), new Deck(deck));
    }

    static Box Made(Sketch sketch, Deck deck, EntityId id)
    {
        (Request? request, string? problem) = RoofTool.Request(sketch, deck, RoofLayer, null, id, "Roof 1");
        Assert.Null(problem);
        return Assert.IsType<Box>(Assert.IsType<AddEntity>(request).Entity);
    }

    [Fact]
    [Trait("Feature", "ROOF-001")]
    public void A_click_on_a_porch_deck_roofs_it_from_the_ledger_down_to_the_front_wall()
    {
        (Sketch sketch, Deck deck) = Porch();
        Box front = WallBox("Front", new Point3(In(48), In(-120), In(36)), In(144), Angle.Zero);
        sketch = sketch.WithEntity(front);
        EntityId id = EntityId.New();
        Box box = Made(sketch, deck, id);

        // Over the deck's outline, 144 along the house and 120 of run; its z the front wall's top, 36 + 96; 4 in 12 is a 40″ rise.
        Assert.Equal((new Point3(In(48), In(-120), In(132)), In(144), In(120), In(40), Angle.Zero), (box.Anchor, box.Width, box.Height, box.Depth, box.Rotation));
        Assert.Equal((RoofLayer, "Roof 1"), (box.Layer, box.Name));
        Assert.Equal(RoofTool.StartingInputs(new WallLowEnd(front.Id)), box.Roof);
        Assert.Null(RoofRules.Refusal(box.Roof!));

        // What it made reads as a roof over that deck, and frames.
        Sketch roofed = sketch.WithEntity(box);
        Roof roof = Assert.Single(Roof.All(roofed));
        Assert.Equal(deck, roof.Over(roofed));
        Assert.Equal("4 in 12", roof.Pitch);
        Assert.Null(RoofFrame.Of(roofed, roof, MaterialsLibrary.Shipped).Problem);

        // A second click on the same deck adds nothing.
        Assert.Equal($"{deck.Name} already has a roof: select it to change it.", RoofTool.Request(roofed, deck, RoofLayer, null, EntityId.New(), "Roof 2").Problem);

        Assert.IsType<Batch>(RoofTool.Request(sketch, deck, RoofLayer, new AddLayer(new Layer(RoofLayer, BuildingLayers.Roof)), id, "Roof 1").Request);
    }

    [Fact]
    public void A_house_running_north_south_turns_the_roof_and_with_no_front_wall_a_beam_carries_it()
    {
        // The house's east face on x = 0 from y 0 to 240; the deck east of it, 120 out and 144 along.
        Box house = WallBox("House", new Point3(-In(3, 1, 2), Length.Zero, In(36)), In(240), Angle.Right, Phase.Existing);
        Box platform = new(EntityId.New(), DeckLayer, new Point3(Length.Zero, In(48), Length.Zero), In(120), In(144), In(36), BoxFace.Top, Angle.Zero) { Deck = DeckTool.StartingInputs };
        Sketch sketch = Layers().WithEntity(house).WithEntity(platform);
        Deck deck = new(platform);
        Box box = Made(sketch, deck, EntityId.New());

        // 144 along the house (y), 120 of run (x): turned a right angle, its plan outline the deck's.
        Assert.Equal((In(144), In(120), Angle.Right), (box.Width, box.Height, box.Rotation));
        Assert.Equal(deck.Outline, Deck.Bounds(box));

        // No wall on the east edge: a (2) 2x10 beam on 2 4x4 posts, its top a wall's starting height above the deck.
        Assert.Equal(RoofTool.StartingBeam, box.Roof!.LowEnd);
        Assert.Equal(In(36 + 96), box.Anchor.Z);
        Assert.Equal(deck, new Roof(box).Over(sketch.WithEntity(box)));
    }

    [Fact]
    public void The_front_wall_is_the_one_standing_on_the_far_edge()
    {
        (Sketch sketch, Deck deck) = Porch();
        Box south = WallBox("South", new Point3(In(48), In(-120), In(36)), In(144), Angle.Zero);
        Box north = WallBox("North", new Point3(In(48), -In(3, 1, 2), In(36)), In(144), Angle.Zero);
        Box west = WallBox("West", new Point3(In(48), -In(116, 1, 2), In(36)), In(113), Angle.Right);
        Box east = WallBox("East", new Point3(In(188, 1, 2), -In(116, 1, 2), In(36)), In(113), Angle.Right);
        Box demolished = WallBox("Gone", new Point3(In(48), In(-120), In(36)), In(144), Angle.Zero) with { Phase = Phase.Demolish };
        Box below = WallBox("Below", new Point3(In(48), In(-120), Length.Zero), In(144), Angle.Zero);
        sketch = sketch.WithEntity(demolished).WithEntity(below).WithEntity(south).WithEntity(north).WithEntity(west).WithEntity(east);

        Assert.Equal(
            ["South", "North", "West", "East"],
            new[] { DeckEdge.South, DeckEdge.North, DeckEdge.West, DeckEdge.East }.Select(edge => RoofTool.FrontWall(sketch, deck, edge)!.Name));
        Assert.Equal(
            [DeckEdge.South, DeckEdge.North, DeckEdge.West, DeckEdge.East],
            new[] { DeckEdge.North, DeckEdge.South, DeckEdge.East, DeckEdge.West }.Select(RoofTool.Far));

        // A wall off the deck is not its front wall.
        Box off = WallBox("Off", new Point3(In(300), In(-120), In(36)), In(144), Angle.Zero);
        Assert.Null(RoofTool.FrontWall(Porch().Sketch.WithEntity(off), deck, DeckEdge.South));
    }

    [Fact]
    public void A_deck_with_no_ledger_has_no_roof_and_a_click_finds_the_deck_under_it()
    {
        (Sketch sketch, Deck deck) = Porch();
        Sketch alone = Layers().WithEntity(deck.Box);
        Assert.StartsWith($"{deck.Name} has no ledger on the house", RoofTool.Request(alone, deck, RoofLayer, null, EntityId.New(), "Roof 1").Problem, StringComparison.Ordinal);
        Deck tipped = new(deck.Box with { FaceUp = BoxFace.South });
        Assert.NotNull(RoofTool.Request(sketch, tipped, RoofLayer, null, EntityId.New(), "Roof 1").Problem);

        Assert.Equal(deck, RoofTool.DeckAt(sketch, Point2.Inches(100, -60)));
        Assert.Null(RoofTool.DeckAt(sketch, Point2.Inches(100, 60)));
        Assert.Null(RoofTool.DeckAt(sketch, Point2.Inches(20, -60)));
        Assert.Null(RoofTool.DeckAt(sketch, Point2.Inches(200, -60)));
        Assert.Null(RoofTool.DeckAt(sketch, Point2.Inches(100, -130)));
    }

    [Theory]
    [InlineData("5 in 12", 120, 50, 0, 1)]
    [InlineData("5", 120, 50, 0, 1)]
    [InlineData(" 4.5 IN 12 ", 120, 45, 0, 1)]
    [InlineData("12 in 12", 120, 120, 0, 1)]
    public void A_typed_pitch_is_a_rise_over_the_run_rounded_to_the_grid(string typed, long run, long whole, long numerator, long denominator)
        => Assert.Equal(In(whole, numerator, denominator), RoofTool.RiseFor(typed, In(run)));

    [Fact]
    public void A_pitch_off_the_grid_rounds_half_up_once()
    {
        // 121 × 5 ÷ 12 = 50 5/12″ = 51626 2/3 units → 51627.
        Assert.Equal(51627, RoofTool.RiseFor("5", In(121))!.Value.Units);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("steep")]
    [InlineData("0 in 12")]
    [InlineData("-3")]
    [InlineData("25 in 12")]
    public void A_pitch_that_does_not_read_is_null(string? typed) => Assert.Null(RoofTool.RiseFor(typed, In(120)));
}

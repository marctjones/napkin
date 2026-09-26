using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The shed roof of deck-and-porch §9.5 (§11.2 tests 13–15), worked by hand: a 144 × 120 × 50 roof box
/// over §9's deck, its low end on the front wall's plates at 132″, 2x8 rafters at 16″ and a 2x8 ledger,
/// a 12″ overhang, 7/16 OSB and asphalt shingles covering 33 sq ft a bundle. 120 : 50 : 130 is a whole
/// triangle (12 : 5 : 13), so nothing here is ≈ but what §9.5 says is.
/// </summary>
public class RoofTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId DeckLayer = LayerId.New();
    static readonly LayerId RoofLayer = LayerId.New();
    static readonly LoadedPack ZzDeck = Assert.Single(CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "deck")]).Loaded);

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static ExactFraction Inches(long whole, long numerator, long denominator) => new(((Int128)whole * denominator + numerator) * 1024, denominator);

    static RoofInputs Inputs(RoofLowEnd low) => new(In(16), "2x8", "2x8", In(12), true, "7/16 osb", new Roofing("asphalt shingles", 33, 0), low);

    static (Sketch Sketch, Roof Roof, Box Front) Porch(Func<Box, RoofLowEnd>? low = null, long rise = 50, bool bearing = true, long roofZ = 132)
    {
        Box house = new(EntityId.New(), WallLayer, new Point3(Length.Zero, Length.Zero, In(36)), In(240), In(5, 1, 2), In(96), BoxFace.Top, Angle.Zero) { Phase = Phase.Existing };
        Box deck = new(EntityId.New(), DeckLayer, new Point3(In(48), In(-120), Length.Zero), In(144), In(120), In(36), BoxFace.Top, Angle.Zero)
        {
            Deck = new DeckInputs(JoistDirection.Out, In(16), "2x8", new BeamSpec(2, "2x10"), "4x4", 3, Length.Zero, "5/4x6", In(0, 1, 8), true, "zz-deck-and-roof", "zz-fir", In(42), null, null),
        };
        Box front = new(EntityId.New(), WallLayer, new Point3(In(48), In(-120), In(36)), In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Front",
            WallInputs = new WallInputs("zz-roof", null) { Side = WallSide.Exterior, Bearing = bearing },
        };
        Box roof = new(EntityId.New(), RoofLayer, new Point3(In(48), In(-120), In(roofZ)), In(144), In(120), In(rise), BoxFace.Top, Angle.Zero)
        {
            Name = "Roof",
            Roof = Inputs((low ?? (wall => new WallLowEnd(wall.Id)))(front)),
        };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(DeckLayer, BuildingLayers.Deck)).WithLayer(new Layer(RoofLayer, BuildingLayers.Roof))
            .WithEntity(house).WithEntity(deck).WithEntity(front).WithEntity(roof) with
        {
            Site = SiteValues.NotEntered with { GroundSnowLoadPsf = 30 },
        };
        return (sketch, new Roof(roof), front);
    }

    static RoofFraming Frame(Sketch sketch, Roof roof)
    {
        (RoofFraming? framing, string? problem) = RoofFrame.Of(sketch, roof, MaterialsLibrary.Shipped);
        Assert.Null(problem);
        return framing!;
    }

    [Fact]
    [Trait("Feature", "ROOF-001")]
    public void The_roof_of_section_9_5_is_ten_rafters_of_141_3_8()
    {
        (Sketch sketch, Roof roof, _) = Porch();
        RoofFraming frame = Frame(sketch, roof);

        Assert.Equal("5 in 12", roof.Pitch);
        Assert.Equal((In(130), true), (frame.Hypotenuse, frame.HypotenuseExact));

        // Rafter run 120 − 1 1/2 + 12 = 130 1/2; length 130 1/2 × 13 ÷ 12 = 141 3/8, exactly.
        Assert.Equal(In(130, 1, 2), frame.RafterRun);
        Assert.Equal(ExactFraction.Whole(In(141, 3, 8).Units), frame.RafterLength);
        Assert.Equal(10, frame.Rafters.Length);
        FramingPiece rafters = Assert.Single(frame.Pieces, piece => piece.Role == FramingRole.Rafter);
        Assert.Equal((10, In(141, 3, 8)), (rafters.Quantity, rafters.Length));
        Assert.Equal(In(144), Assert.Single(frame.Pieces, piece => piece.Role == FramingRole.Ledger).Length);

        // Blocking at the plate: 8 × 14 1/2 + 1 × 13.
        Assert.Equal(8, Assert.Single(frame.Pieces, piece => piece.Role == FramingRole.Blocking && piece.Length == In(14, 1, 2)).Quantity);
        Assert.Equal(1, Assert.Single(frame.Pieces, piece => piece.Role == FramingRole.Blocking && piece.Length == In(13)).Quantity);
        Assert.Equal(
            [("Roof rafter", 10, In(141, 3, 8), "2x8"), ("Roof ledger", 1, In(144), "2x8")],
            RoofFrame.CutRows(frame).Take(2).Select(row => (row.Label, row.Quantity, row.Length, row.Material)));
        Assert.All(RoofFrame.CutRows(frame), row => Assert.Equal([roof.Id], row.Members));
        Assert.StartsWith("10 rafters 2x8 × 11'-9 3/8\" at 16\"; ledger 2x8 × 12'-0\", its top ≈4'-7 3/4\" above the low support's top", frame.Line, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "ROOF-002")]
    public void The_height_above_plate_and_the_ledger_are_exact_fractions()
    {
        (Sketch sketch, Roof roof, _) = Porch();
        RoofFraming frame = Frame(sketch, roof);

        // HAP = 7 1/4 × 13 ÷ 12 − 3 1/2 × 5 ÷ 12 = 7 41/48 − 1 11/24 = 6 19/48.
        Assert.Equal(Inches(6, 19, 48), frame.Hap);

        // Ledger top above the plates: 6 19/48 + 118 1/2 × 5 ÷ 12 = 6 19/48 + 49 3/8 = 55 37/48 (≈ 55 3/4).
        Assert.Equal(Inches(55, 37, 48), frame.LedgerAbovePlate);
        Assert.Equal(In(118, 1, 2), frame.HorizontalSpan);

        // The cuts: birdsmouth 118 1/2 × 13 ÷ 12 = 128 3/8 along the top edge; notch 3 1/2 × 5 ÷ 12 = 1 11/24 (≈ 1 7/16); tail 12 × 13 ÷ 12 = 13.
        Assert.Equal(
            "Plumb cut at the top: set the square at 5 and 12 and mark plumb. Birdsmouth: from the top plumb cut measure 10'-8 3/8\" along the top edge, mark a plumb line, "
            + "then a level seat 3 1/2\" long back toward the top; the notch is ≈1 7/16\" deep. Tail: 1'-1\" further along the top edge, cut plumb.",
            frame.Cuts);
    }

    [Fact]
    [Trait("Feature", "ROOF-003")]
    public void The_sheathing_and_roofing_are_counted_from_the_sloped_area()
    {
        (Sketch sketch, Roof roof, _) = Porch();
        RoofFraming frame = Frame(sketch, roof);

        // 144 × 141 3/8 = 20358 sq in = 141.4 sq ft: ⌈20358 ÷ 4608⌉ = 5 sheets; 141.375 ÷ 33 → 5 bundles.
        Assert.Equal(ExactFraction.Whole(20358L * 1024 * 1024), frame.SlopedArea);
        Assert.Equal(
            ["Sheathing 7/16 osb: 141.4 sq ft → 5 sheets (sheets by area — a layout may need more).", "Roofing asphalt shingles: 141.4 sq ft with 0 % waste → 5 units of 33 sq ft."],
            frame.Coverings);

        // The 40 % line under this roof is §9.6's 10.4 % once the porch has its walls; with only the front wall it has no side triangles.
        GlazingRatio ratio = Glazing.Of(sketch, frame.Deck, roof.Box.Height, roof.Box.Depth, frame.SlopedArea)!;
        Assert.Equal(ExactFraction.Whole(0), ratio.RakeFill);
    }

    [Fact]
    [Trait("Feature", "ROOF-001")]
    public void The_rafters_are_checked_against_the_packs_rafter_table()
    {
        (Sketch sketch, Roof roof, _) = Porch();
        DeckCheckLine line = RoofCheck.Rafters(sketch, Frame(sketch, roof), ZzDeck);

        // 2x8 at 16", zz-fir, 30 psf: allowed 13'-2"; the span is 9'-10 1/2".
        Assert.True(line.Passing);
        Assert.Equal("Rafters 2x8 at 16\" o.c., horizontal span 9'-10 1/2\": allowed up to 13'-2\" (ZZ-RAFTER row r.2x8.30, synthetic p. 4).", line.Text);

        Assert.Contains("over by", RoofCheck.Rafters(sketch with { Site = sketch.Site with { GroundSnowLoadPsf = 50 } }, Frame(sketch, roof) with { HorizontalSpan = In(140) }, ZzDeck).Text, StringComparison.Ordinal);
        Assert.Contains("Enter the ground snow load", RoofCheck.Rafters(sketch with { Site = SiteValues.NotEntered }, Frame(sketch, roof), ZzDeck).Text, StringComparison.Ordinal);
        Assert.Contains("past the last band", RoofCheck.Rafters(sketch with { Site = sketch.Site with { GroundSnowLoadPsf = 60 } }, Frame(sketch, roof), ZzDeck).Text, StringComparison.Ordinal);
        // A deck read by its layer alone has no species: the rafter table asks for one.
        Sketch bare = sketch.WithEntity(roof.Over(sketch)!.Box with { Deck = null });
        Assert.Equal(DeckCheckKind.Joists, RoofCheck.Rafters(bare, Frame(bare, roof), ZzDeck).Kind);
        Assert.False(RoofCheck.Rafters(bare, Frame(bare, roof), ZzDeck).Passing);
        Assert.Contains("No adopted code is chosen", RoofCheck.Rafters(sketch, Frame(sketch, roof), null).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pitch_that_is_not_whole_says_so_and_the_slope_is_rounded_up_once()
    {
        // Rise 50 over a 121 run: 50 × 12 ÷ 121 = 4.958… → ≈ 4.96 in 12.
        Box box = new(EntityId.New(), RoofLayer, Point3.Origin, In(144), In(121), In(50), BoxFace.Top, Angle.Zero);
        Assert.Equal("≈ 4.96 in 12 (rise 4'-2\" over 10'-1\")", new Roof(box).Pitch);

        (Sketch sketch, Roof roof, _) = Porch(rise: 49);
        RoofFraming frame = Frame(sketch, roof);
        Assert.False(frame.HypotenuseExact);
        Assert.StartsWith("Plumb cut at the top: set the square at ≈ 4.90 in 12", frame.Cuts, StringComparison.Ordinal);
    }

    [Fact]
    public void An_open_porch_roof_on_a_beam_and_posts()
    {
        // A (2) 2x10 beam: its plies are the seat, 3″; the posts stand on the deck (36″) to the beam's underside (132 − 9 1/4): 86 3/4″.
        (Sketch sketch, Roof roof, _) = Porch(_ => new BeamLowEnd(new BeamSpec(2, "2x10"), "4x4", 2));
        RoofFraming frame = Frame(sketch, roof);

        Assert.Equal((2, In(86, 3, 4)), (Assert.Single(frame.Pieces, piece => piece.Role == FramingRole.Post).Quantity, Assert.Single(frame.Pieces, piece => piece.Role == FramingRole.Post).Length));
        Assert.Equal(2, Assert.Single(frame.Pieces, piece => piece.Role == FramingRole.Beam).Quantity);

        // HAP with a 3″ seat: 7 1/4 × 13 ÷ 12 − 3 × 5 ÷ 12 = 7 41/48 − 1 1/4 = 6 29/48.
        Assert.Equal(Inches(6, 29, 48), frame.Hap);
    }

    [Fact]
    public void A_roof_says_why_it_cannot_be_framed()
    {
        (Sketch sketch, Roof roof, Box front) = Porch();
        string? Why(Sketch s, Roof r) => RoofFrame.Of(s, r, MaterialsLibrary.Shipped).Problem;

        Assert.StartsWith("No roof inputs yet", Why(sketch, new Roof(roof.Box with { Roof = null })), StringComparison.Ordinal);
        Assert.StartsWith("The roof does not stand over a deck", Why(sketch, new Roof(roof.Box with { Width = In(100) })), StringComparison.Ordinal);
        Assert.StartsWith("2x7 is not in the materials library", Why(sketch, new Roof(roof.Box with { Roof = roof.Box.Roof! with { Rafter = "2x7" } })), StringComparison.Ordinal);
        Assert.StartsWith("2x7 is not in the materials library", Why(sketch, new Roof(roof.Box with { Roof = roof.Box.Roof! with { Ledger = "2x7" } })), StringComparison.Ordinal);
        Assert.Equal("The roof's low end names something that is not a wall.", Why(sketch, new Roof(roof.Box with { Roof = roof.Box.Roof! with { LowEnd = new WallLowEnd(EntityId.New()) } })));
        Assert.StartsWith("The low end's beam or post is not in the materials library", Why(sketch, new Roof(roof.Box with { Roof = roof.Box.Roof! with { LowEnd = new BeamLowEnd(new BeamSpec(2, "2x7"), "4x4", 2) } })), StringComparison.Ordinal);
        (Sketch low, Roof lowRoof, _) = Porch(_ => new BeamLowEnd(new BeamSpec(2, "2x10"), "4x4", 2), roofZ: 40);
        Assert.StartsWith("The roof's low end is too low for its beam", Why(low, lowRoof), StringComparison.Ordinal);

        Assert.Equal("The roof is narrower than one 2x8: widen the deck under it.", Why(sketch.WithEntity(roof.Over(sketch)!.Box with { Width = In(1) }), new Roof(roof.Box with { Width = In(1) })));

        // A front wall not marked bearing is asked to be — and one read only by its layer, with no inputs at all.
        (Sketch notBearing, Roof loose, _) = Porch(bearing: false);
        Assert.Contains("Front carries the roof: mark it bearing, and choose what it supports.", Frame(notBearing, loose).Notes);
        Assert.Contains("Front carries the roof: mark it bearing, and choose what it supports.", Frame(sketch.WithEntity(front with { WallInputs = null }), roof).Notes);
        Assert.Contains("napkin does not know the house: check the ledger clears its eave and openings.", Frame(sketch, roof).Notes);
        Assert.NotNull(front);
    }

    [Fact]
    public void Coverings_without_a_known_sheathing_or_a_coverage_say_what_is_missing()
    {
        (Sketch sketch, Roof roof, _) = Porch();
        Roof bare = new(roof.Box with { Roof = roof.Box.Roof! with { Sheathing = "3/8 cardboard", Roofing = new Roofing("metal", null, 10) } });
        Assert.Equal(
            ["Sheathing 3/8 cardboard: not in the materials library, so no sheets are counted.", "Roofing metal: 155.5 sq ft with 10 % waste; type the coverage from the bundle."],
            Frame(sketch, bare).Coverings);

        Roof none = new(roof.Box with { Roof = roof.Box.Roof! with { Sheathing = null, Blocking = false } });
        Assert.Single(Frame(sketch, none).Coverings);
        Assert.DoesNotContain(Frame(sketch, none).Pieces, piece => piece.Role == FramingRole.Blocking);
    }

    [Fact]
    public void A_roof_is_read_by_its_layer_its_name_or_its_inputs()
    {
        (Sketch sketch, Roof roof, _) = Porch();
        Assert.Equal(roof.Box.Id, Assert.Single(Roof.All(sketch)).Id);
        Assert.Equal("Roof", new Roof(roof.Box with { Name = string.Empty }).Name);
        Assert.True(Roof.Is(sketch, roof.Box with { Layer = LayerId.Default, Name = "Canopy" }));
        Assert.False(Roof.Is(sketch, roof.Box with { Layer = LayerId.Default, Name = "Canopy", Roof = null }));
        Assert.Null(new Roof(roof.Box with { FaceUp = BoxFace.South }).Over(sketch));
    }
}

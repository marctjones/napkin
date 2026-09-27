using System.Collections.Immutable;
using System.Text.RegularExpressions;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

using static Napkin.Interop.Pdf.Tests.ReadBack;

namespace Napkin.Interop.Pdf.Tests;

/// <summary>
/// The deck set (#226, docs/design/permit-set.md §2) on the deck tests' §9.1 deck under the SYNTHETIC
/// us-zz-deck (made-up numbers): 144 × 120, 36″ up, its north edge on the house; 2x8 joists at 16″
/// (1 1/2″ thick, the library's 2x8), a (2) 2x10 beam at the far edge (3″), three 6x6 posts (5 1/2″).
/// Every page coordinate below is worked out by hand from the frame's arithmetic:
/// <code>
/// Letter: drawing area 720 wide from x 36; a drawn sheet's drawing side is its left 64 %, less 12 all
/// round: 460.8 − 24 = 436.8 wide from x 48, centred at 266.4, on the area's own vertical centre.
/// S2 insets that by 24 more (room for its dimensions): 388.8 wide, still centred at 266.4.
/// </code>
/// </summary>
public class DeckSetPdfTests
{
    static readonly CodePacks Packs = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "deck")]);
    static readonly LayerId WallLayer = LayerId.New(), DeckLayer = LayerId.New(), SiteLayer = LayerId.New();
    static readonly DateOnly Day = new(2026, 9, 27);

    static Length In(long whole) => Length.Inches(whole);

    static DeckInputs Inputs(string? supports = "zz-deck", string? species = "zz-fir") => new(
        JoistDirection.Out, In(16), "2x8", new BeamSpec(2, "2x10"), "6x6", 3, Length.Zero, "5/4x6", Length.Inches(0, 1, 8), true, supports, species, In(42), null, null);

    static Sketch Design(DeckInputs? inputs = null, ImmutableArray<Course>? lot = null)
    {
        Box house = new(EntityId.New(), WallLayer, new Point3(Length.Zero, Length.Zero, In(36)), In(240), Length.Inches(5, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "House",
            Phase = Phase.Existing,
        };
        Box deck = new(EntityId.New(), DeckLayer, new Point3(In(48), In(-120), Length.Zero), In(144), In(120), In(36), BoxFace.Top, Angle.Zero)
        {
            Name = "Deck 1",
            Deck = inputs ?? Inputs(),
        };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(DeckLayer, BuildingLayers.Deck))
            .WithLayer(new Layer(SiteLayer, BuildingLayers.Site))
            .WithEntity(house)
            .WithEntity(deck) with
        {
            Code = new CodeChoice("us-zz-deck", 1, CodeMode.Locked, new DateOnly(2026, 9, 26)),
            Site = SiteValues.NotEntered with { SoilBearingPsf = 2000, FrostDepth = In(42), GroundSnowLoadPsf = 30 },
        };
        return lot is { } courses ? sketch.WithEntity(new Boundary(EntityId.New(), SiteLayer, new Point2(In(-360), In(-480)), courses) { Name = "Lot", Phase = Phase.Existing }) : sketch;
    }

    static DeckSet Set(Sketch sketch, DrawingView? elevation = null, SheetPaper? paper = null)
    {
        PermitSet permit = new(
            new TitleBlock("Deck", Day, SheetNotes.CodeLine(sketch, Packs), $"{ScopeDisclaimer.Text} {DeckSetPdf.NotASurvey}"),
            new FeetInchesFormat(16),
            paper ?? SheetPaper.Letter,
            PermitItems.Of(sketch, Packs, MaterialsLibrary.Shipped));
        return new DeckSet(permit, sketch, Packs, MaterialsLibrary.Shipped, elevation);
    }

    static ReadBack Read(DeckSet set)
    {
        using MemoryStream stream = new();
        DeckSetPdf.Write(set, stream);
        return ReadBack.Open(stream.ToArray());
    }

    /// <summary>The drawing area's vertical centre for a set's title block on Letter.</summary>
    static double MiddleY(DeckSet set) =>
        SheetFrame.For(set.Permit.Title with { Banner = PermitItems.Banner(set.Permit.Items) }).DrawingArea.Centre.Y;

    [Fact]
    [Trait("Feature", "PERMIT-002")]
    public void A_deck_set_is_its_site_plan_elevation_framing_plan_details_and_code_page_in_order()
    {
        DrawingView front = new(StandardView.Front, [new DrawingLine(LineKind.Visible, new(48, 0), new(192, 0)), new DrawingLine(LineKind.Visible, new(48, 36), new(192, 36))], []);
        ReadBack read = Read(Set(Design(), front));
        string[] titles = ["S1 Site plan", "A2 Elevation: Front", "S2 Framing plan: Deck 1", "S3 Details: Deck 1", PermitSetPdf.CodePage];
        for (int page = 0; page < titles.Length; page++)
        {
            Assert.Contains(titles[page], read.Text[page], StringComparison.Ordinal);
        }

        Assert.All(read.Text, text => Assert.Contains(DeckSetPdf.NotASurvey, text, StringComparison.Ordinal));
        Assert.Contains("the decking is 3'-0\" above grade", read.Text[1], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PERMIT-002")]
    public void The_framing_plan_draws_the_frame_at_3_8_to_the_foot_where_the_hand_says()
    {
        // 144 × 120 in 388.8 wide and some 360 tall: 2.7 pt/in at most across, so 1:32 (3/8" = 1'-0",
        // 2.25 pt/in; 1:24 would be 3). The frame's centre, (72, 60) from the ledger's start, lands on
        // (266.4, the area's middle); the house is at the top.
        DeckSet set = Set(Design());
        ReadBack read = Read(set);
        int s2 = read.Text.ToList().FindIndex(text => text.Contains("S2 Framing plan", StringComparison.Ordinal));
        string page = read.Operators[s2];
        Assert.Contains("Scale 1:32", read.Text[s2], StringComparison.Ordinal);
        Assert.Contains("3/8\" = 1'-0\"", read.Text[s2], StringComparison.Ordinal);

        double y0 = MiddleY(set) + (60 * 2.25);
        // The ledger's house face, u 0…144 at v 0: x 266.4 ∓ 72 × 2.25 = 104.4 to 428.4.
        Assert.Contains("0 G\n1.4 w\n" + Stroke(104.4, y0, 428.4, y0), page, StringComparison.Ordinal);
        // The last joist, faces 142.5…144 (W − t), from the ledger's back (v 1.5) to the rim's (118.5):
        // x 266.4 + 70.5 × 2.25 = 425.025.
        Assert.Contains("0 G\n1.4 w\n" + Stroke(425.025, y0 - (1.5 * 2.25), 428.4, y0 - (1.5 * 2.25)), page, StringComparison.Ordinal);
        // The beam under them, hidden: its inner face at v 120 − 3 = 117, dashed grey from x 104.4 (3 pt dashes).
        Assert.Contains("0.65 G\n1 w\n" + Stroke(104.4, y0 - 263.25, 107.4, y0 - 263.25), page, StringComparison.Ordinal);
        // Ten joists at 16" and the end one, each a box of four strokes; their labels as the panel says them.
        Assert.Contains(Squash("Joists: 2x8 at 1'-4\" o.c., 10 of them, span 9'-7 1/2\""), read.Letters[s2], StringComparison.Ordinal);
        Assert.Contains(Squash("Beam: (2) 2x10, span"), read.Letters[s2], StringComparison.Ordinal);
        // Width and depth dimensioned.
        Assert.Contains("12'-0\"", read.Text[s2], StringComparison.Ordinal);
        Assert.Contains("10'-0\"", read.Text[s2], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PERMIT-002")]
    public void A_member_whose_check_has_no_answer_prints_a_blank_where_its_size_would_be_and_the_set_says_it_is_incomplete()
    {
        // No species typed: the joists wait for it, so the size is a blank rule 54 pt long and the note follows.
        DeckSet set = Set(Design(Inputs(species: null)));
        Assert.NotNull(PermitItems.Banner(set.Permit.Items));
        ReadBack read = Read(set);
        int s2 = read.Text.ToList().FindIndex(text => text.Contains("S2 Framing plan", StringComparison.Ordinal));
        Assert.DoesNotContain(Squash("Joists: 2x8 at"), read.Letters[s2], StringComparison.Ordinal);
        Assert.Contains("Joists:", read.Text[s2], StringComparison.Ordinal);
        MatchCollection rules = Regex.Matches(read.Operators[s2], @"0 G\n0\.8 w\n(\S+) (\S+) m\n(\S+) \2 l\nS");
        Assert.Contains(rules, rule => Math.Abs(double.Parse(rule.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) - double.Parse(rule.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) - DeckSetPdf.SizeBlank) < 1e-6);
        Assert.All(read.Text, text => Assert.Contains("NOT A COMPLETE PERMIT SET", text, StringComparison.Ordinal));
        Assert.Contains(read.Text, text => text.Contains(PermitSetPdf.Worksheet, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Feature", "PERMIT-002")]
    public void The_site_plan_draws_the_lot_its_setbacks_inward_and_says_each_lines_distance()
    {
        // The lot of the site-plan tests, from (−360, −480): 100' east–west, 50' north–south, clockwise;
        // with the house and deck in it the plan spans 1200 × 600", which fits 436.8 wide at 0.364 pt/in —
        // under every architect scale, so the largest engineer scale that fits: 1:240, 1" = 20' (0.3 pt/in).
        ImmutableArray<Course> lot =
        [
            new(new Bearing(NorthSouth.North, Angle.Degrees(0), EastWest.East), Length.Feet(50), new Setback(Length.Feet(10), SetbackKind.Side)),
            new(new Bearing(NorthSouth.North, Angle.Degrees(90), EastWest.East), Length.Feet(100), null),
            new(new Bearing(NorthSouth.South, Angle.Degrees(0), EastWest.East), Length.Feet(50), null),
            new(new Bearing(NorthSouth.North, Angle.Degrees(90), EastWest.West), Length.Feet(100), null),
        ];
        DeckSet set = Set(Design(lot: lot));
        ReadBack read = Read(set);
        Assert.Contains("Scale 1:240", read.Text[0], StringComparison.Ordinal);
        Assert.Contains("1\" = 20'", read.Text[0], StringComparison.Ordinal);

        // The west line, x −360, y −480…120: the plan's centre (240, −180) lands on (266.4, middle), so
        // x 266.4 − 600 × 0.3 = 86.4, from middle − 90 to middle + 90; its 10' side setback 36 pt inside, at 122.4.
        double middle = MiddleY(set);
        string page = read.Operators[0];
        Assert.Contains("0 G\n1.4 w\n" + Stroke(86.4, middle - 90, 86.4, middle + 90), page, StringComparison.Ordinal);
        Assert.Contains("0 G\n0.8 w\n" + Stroke(122.4, middle - 90, 122.4, middle - 78), page, StringComparison.Ordinal);

        SitePlanMeasure measure = Napkin.Modules.Building.SitePlan.Of(set.Sketch)!;
        Assert.All(measure.Lines, line => Assert.Contains(Squash(line.Text), read.Letters[0], StringComparison.Ordinal));
        Assert.Contains(Squash(Napkin.Modules.Building.SitePlan.ZoningNote), read.Letters[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_lot_the_site_plan_says_so_and_draws_the_footprints()
    {
        ReadBack read = Read(Set(Design()));
        Assert.Contains(Squash(DeckSetPdf.NoBoundary), read.Letters[0], StringComparison.Ordinal);
        Assert.Contains("1.4 w", read.Operators[0], StringComparison.Ordinal);
    }

    [Fact]
    public void A_deck_napkin_cannot_frame_says_why_on_its_framing_and_detail_sheets()
    {
        DeckSet set = Set(Design(Inputs() with { Joist = "zz-nothing" }));
        ReadBack read = Read(set);
        string refusal = Assert.Single(DeckCheck.Of(set.Sketch, Packs, MaterialsLibrary.Shipped)).Refusal!.Text;
        Assert.Equal(2, read.Letters.Count(letters => letters.Contains(Squash(refusal), StringComparison.Ordinal) && letters.Contains(Squash("Deck 1: not framed"), StringComparison.Ordinal)));
    }

    [Fact]
    public void The_details_sheet_holds_the_ledger_footing_guard_and_stair_blocks()
    {
        DeckSet set = Set(Design());
        ReadBack read = Read(set);
        int s3 = read.Text.ToList().FindIndex(text => text.Contains("S3 Details", StringComparison.Ordinal));
        foreach (string block in (string[])["Ledger attachment", "Footings", "Guard", "Stair"])
        {
            Assert.Contains(block, read.Text[s3], StringComparison.Ordinal);
        }

        Assert.Contains(Squash("Footing: 3'-6\" below grade"), read.Letters[s3], StringComparison.Ordinal);
        Assert.Contains(Squash("Guard height: none typed"), read.Letters[s3], StringComparison.Ordinal);
    }

    [Fact]
    public void A_north_turned_site_turns_its_arrow_and_the_inward_offset_follows_the_lot_s_turn()
    {
        // Anticlockwise, the inside of a line is on its left: the offset of (0,0)→(10,0) by 2 is (0,2)→(10,2).
        Assert.Equal((new PagePoint(0, 2), new PagePoint(10, 2)), DeckSetPdf.Inward(new PagePoint(0, 0), new PagePoint(10, 0), 2, clockwise: false));
        Assert.Equal((new PagePoint(0, -2), new PagePoint(10, -2)), DeckSetPdf.Inward(new PagePoint(0, 0), new PagePoint(10, 0), 2, clockwise: true));
        Assert.True(DeckSetPdf.SignedArea([Point2.Origin, new Point2(In(10), Length.Zero), new Point2(In(10), In(10))]) > 0);

        Sketch turned = Design() with { Site = Design().Site with { North = Angle.Degrees(90) } };
        Assert.Contains("N", Read(Set(turned)).Text[0], StringComparison.Ordinal);
    }
}

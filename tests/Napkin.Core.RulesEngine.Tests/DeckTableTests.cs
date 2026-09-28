using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// The deck table kinds (#198, deck-and-porch §3) on the SYNTHETIC pack us-zz-deck (NOT CODE VALUES):
/// every lookup answers exactly one honest result, and every malformed file is refused naming what is
/// wrong. The expected answers are the fixture's made-up rows, worked by hand in each comment.
/// </summary>
public class DeckTableTests
{
    static string Root => Path.Combine(AppContext.BaseDirectory, "DeckPacks");

    static LoadedPack Deck() => Fx.Loaded(PackLoader.Load(Root, "us-zz-deck"));

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    /// <summary>A joist request under the synthetic guide: its scope asks what the deck supports, the snow load and the deck's shape.</summary>
    static SpanRequest Joists(Length span, string? species = "zz-fir", string? supports = "zz-deck", string member = "2x8", long spacing = 16)
        => new(member, span, supports, species, In(spacing), null, GroundSnowLoad: 30, DeckLength: In(120), DeckWidth: In(144));

    [Fact]
    public void The_synthetic_deck_pack_loads_every_kind()
    {
        LoadedPack pack = Deck();

        Assert.Equal([SpanUse.DeckJoist, SpanUse.DeckBeam, SpanUse.Rafter], pack.Deck.Spans.Keys.Order());
        Assert.Equal("ZZ-DECK-LEDGER", pack.Deck.Ledger!.Designation);
        Assert.Equal("ZZ-DECK-FOOTING", pack.Deck.Footing!.Designation);
        Assert.Equal((In(28), In(34), In(5)), (pack.Deck.GuardStair!.Guard!.TriggerHeight, pack.Deck.GuardStair.Guard.MinimumHeight, pack.Deck.GuardStair.Guard.MaximumOpening));
        Assert.Equal((In(8, 1, 4), In(9), In(0, 1, 2), 3, In(32)), (pack.Deck.GuardStair.Stair!.MaximumRiser, pack.Deck.GuardStair.Stair.MinimumTread, pack.Deck.GuardStair.Stair.MaximumRiserDifference, pack.Deck.GuardStair.Stair.HandrailWhenRisersAtLeast, pack.Deck.GuardStair.Stair.MinimumWidth));
        Assert.Equal(In(42), pack.Frost!.FrostLineDepth);
        Assert.Equal("synthetic frost p. 1", pack.Frost.Source.Location);
        Assert.True(pack.HasHeaderTables);
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void Joists_within_the_row_pass_and_joists_past_it_are_short_by_the_difference()
    {
        // 2x8 at 16", zz-fir, zz-deck: allowed 11'-1" (133"). The §9 deck's joists span 117": passes.
        DeckResult.Passes passes = Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(117))));
        Assert.Equal((In(133), "r.fir.2x8.16"), (passes.Allowed, passes.Row.Id));

        // A 12'-6" deck: joists 150 − 3 = 147", over by 14" = 1'-2".
        DeckResult.Short over = Assert.IsType<DeckResult.Short>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(147))));
        Assert.Equal(In(14), over.Over);

        // Exactly the allowed span passes.
        Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(133))));
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void A_supports_or_spacing_the_table_has_no_row_for_is_out_of_scope_and_a_missing_species_is_asked_for()
    {
        // The guide's table has no supports column: its scope limit s.loads answers for a porch deck.
        DeckResult.OutOfScope roof = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(117), supports: "zz-deck-and-roof")));
        Assert.Equal("Beyond the scope of ZZ GUIDE: \"SYNTHETIC: covers a deck carrying only its own loads.\" (ZZ GUIDE synthetic guide p. 2, item 8). Get it engineered.", roof.Explanation);
        Assert.Equal(("s.loads", null), (roof.Limit!.Id, roof.Column));

        DeckResult.OutOfScope wide = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(117), spacing: 24)));
        Assert.Equal((null, "spacing"), (wide.Limit, wide.Column));

        DeckResult.InputMissing species = Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(117), species: null)));
        Assert.Equal("species", species.Input);
        DeckResult.InputMissing supports = Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(117), supports: null)));
        Assert.Equal("Enter what the deck supports: ZZ GUIDE scope limit s.loads (ZZ GUIDE synthetic guide p. 2, item 8) depends on it.", supports.Explanation);
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void A_beam_is_banded_on_the_joist_span_it_carries()
    {
        // (2) 2x10 carrying 9'-9" of joists: the ≤ 10'-0" row, 6'-10"; its 66 3/4" span passes.
        SpanRequest beam = new("(2) 2x10", In(66, 3, 4), null, "zz-fir", null, In(117));
        Assert.Equal(In(82), Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckBeam, beam)).Allowed);

        // 10'-0" and 1/1024" of joists: the ≤ 16'-0" row, 5'-2" (62"); 66 3/4" is over by 4 3/4".
        Assert.Equal(In(4, 3, 4), Assert.IsType<DeckResult.Short>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckBeam, beam with { JoistSpan = In(120) + new Length(1) })).Over);

        // Past 16'-0": out of scope, citing the last band.
        DeckResult.OutOfScope past = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckBeam, beam with { JoistSpan = In(193) }));
        Assert.Contains("past the last band of table ZZ-DECK-BEAM (16'-0\")", past.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Rafters_band_on_the_ground_snow_load_and_ask_for_it()
    {
        SpanRequest rafters = new("2x8", In(141), null, "zz-fir", In(16), null, GroundSnowLoad: 30);
        Assert.Equal(In(158), Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckSpan(Deck(), SpanUse.Rafter, rafters)).Allowed);
        Assert.Equal(In(136), Assert.IsType<DeckResult.Short>(DeckEvaluator.CheckSpan(Deck(), SpanUse.Rafter, rafters with { GroundSnowLoad = 35 })).Allowed);
        Assert.Equal("groundSnowLoad", Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.CheckSpan(Deck(), SpanUse.Rafter, rafters with { GroundSnowLoad = null })).Input);
    }

    [Fact]
    [Trait("Feature", "DECK-001")]
    public void The_ledger_is_sized_with_napkins_count_and_its_footnote()
    {
        // 2x8, joists 9'-9" (≤ 12'-0"): zz-bolts, staggered, 17"; ⌈144 ÷ 17⌉ + 1 = 10.
        DeckResult.Sized sized = Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeLedger(Deck(), "2x8", In(117), In(144)));
        Assert.Equal(("zz-bolts, staggered", In(17), 10), (sized.Row.Text, sized.Row.Spacing, sized.Count));
        Assert.Equal(["a"], sized.Row.Footnotes);

        // At the band exactly (12'-0") it is the same row; 1/1024" over is the next, 11".
        Assert.Equal(In(17), Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeLedger(Deck(), "2x8", In(144), In(144))).Row.Spacing);
        Assert.Equal(In(11), Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeLedger(Deck(), "2x8", In(144) + new Length(1), In(144))).Row.Spacing);
        Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.SizeLedger(Deck(), "2x8", In(200), In(144)));
    }

    [Fact]
    [Trait("Feature", "DECK-003")]
    public void The_footing_takes_the_largest_bearing_column_at_most_the_sites()
    {
        // The worked example's middle post, DCA 6 Appendix B Eq. B-1: 72" × 59 1/4" = 4266 sq in = 29.6 sq ft (≤ 40) on
        // 2000 psf: 17" round or 15" square, 7" thick. The synthetic table declares no centre-post factor, so a
        // continuous beam does not multiply it.
        PostArea area = new(new ExactFraction((Int128)In(72).Units * In(59, 1, 4).Units, 1), PostPosition.Center, ContinuousBeam: true);
        DeckResult.Sized sized = Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(Deck(), area, 2000));
        Assert.Equal(new FootingSize(In(17), In(15), In(7)), sized.Row.Footing);
        Assert.Equal(new AreaAsked(area.Area, null, area.Area), sized.Area);

        // 1999 psf is never rounded up to the 2000 column: the 1500 one, 18" square.
        Assert.Equal(In(18), Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(Deck(), area, 1999)).Row.Footing!.Square);
        Assert.Equal(In(13), Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(Deck(), area, 3500)).Row.Footing!.Square);

        DeckResult.OutOfScope soft = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.SizeFooting(Deck(), area, 1400));
        Assert.Contains("below the lowest band of table ZZ-DECK-FOOTING (1500 psf)", soft.Explanation, StringComparison.Ordinal);
        Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.SizeFooting(Deck(), area with { Area = new ExactFraction((Int128)81 * 144 * 1024 * 1024, 1) }, 2000));
        DeckResult.InputMissing soil = Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.SizeFooting(Deck(), area, null));
        Assert.Equal("soilBearing", soil.Input);
        Assert.Equal(
            "Enter the site's soil bearing value, from the building department or a soils report, in Project → Adopted code and site: table ZZ-DECK-FOOTING bands on it.",
            soil.Explanation);
    }

    [Fact]
    [Trait("Feature", "DECK-003")]
    public void A_post_is_checked_against_its_positions_table_and_a_centre_post_under_a_continuous_beam_is_multiplied()
    {
        // The worked example's end post, Eq. B-2: 36" × 59 1/4" = 2133 sq in = 14.8 sq ft, a zz-fir 4x4 1'-6 1/2" high:
        // the corner table's ≤ 20 sq ft row, 6'-0".
        PostArea end = new(new ExactFraction((Int128)In(36).Units * In(59, 1, 4).Units, 1), PostPosition.Corner, ContinuousBeam: true);
        DeckResult.Passes corner = Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckPost(Deck(), new PostRequest("4x4", In(18, 1, 2), "zz-fir", end)));
        Assert.Equal(("ZZ-DECK-POST-CORNER", "r.fir.4x4.20", In(72)), (corner.Table.Designation, corner.Row.Id, corner.Allowed));
        Assert.Null(corner.Area!.Factor);

        // The middle post, Eq. B-1: 29.6 sq ft × the synthetic factor 3/2 = 44.4 sq ft: the centre table's ≤ 80 row, 7'-0".
        PostArea middle = new(new ExactFraction((Int128)In(72).Units * In(59, 1, 4).Units, 1), PostPosition.Center, ContinuousBeam: true);
        DeckResult.Passes center = Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckPost(Deck(), new PostRequest("4x4", In(18, 1, 2), "zz-fir", middle)));
        Assert.Equal(("ZZ-DECK-POST-CENTER", "r.fir.4x4.80", In(84)), (center.Table.Designation, center.Row.Id, center.Allowed));
        Assert.Equal(("b", new ExactFraction((Int128)In(72).Units * In(59, 1, 4).Units * 3, 2)), (center.Area!.Factor!.Note, center.Area.Looked));

        // A spliced beam is not multiplied: 29.6 sq ft, the ≤ 40 row, 8'-0"; 8'-1" is 1" over.
        DeckResult.Short spliced = Assert.IsType<DeckResult.Short>(DeckEvaluator.CheckPost(Deck(), new PostRequest("4x4", In(97), "zz-fir", middle with { ContinuousBeam = false })));
        Assert.Equal(("r.fir.4x4.40", In(1)), (spliced.Row.Id, spliced.Over));

        // A cell printed NP is out of scope, citing the row; the sentence quotes NP as printed.
        DeckResult.OutOfScope np = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckPost(Deck(), new PostRequest("4x4", In(12), "zz-cedar", middle)));
        Assert.Equal(("r.cedar.4x4.80", true, null, null), (np.Row!.Id, np.Row.NotPermitted, np.Limit, np.Column));
        Assert.Equal("Table ZZ-DECK-POST-CENTER prints NP, no height, for a 4x4 post of zz-cedar carrying up to 80 sq ft (row r.cedar.4x4.80, synthetic p. 9 row zz-cedar 4x4 80): get it engineered.", np.Explanation);

        // No such post, no species, and a pack without a post table.
        Assert.Equal("post", Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckPost(Deck(), new PostRequest("8x8", In(12), "zz-fir", end))).Column);
        Assert.Equal("species", Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.CheckPost(Deck(), new PostRequest("4x4", In(12), null, end))).Input);
        LoadedPack ct = Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "BracePacks"), "us-zz-brace-a"));
        Assert.StartsWith("The loaded pack", Assert.IsType<DeckResult.NoData>(DeckEvaluator.CheckPost(ct, new PostRequest("4x4", In(12), "zz-fir", end))).Explanation, StringComparison.Ordinal);
        Assert.Equal(
            ["1.25", "2", "5/3", "0.5", "-0.75"],
            new[] { new ExactFraction(5, 4), ExactFraction.Whole(2), new ExactFraction(5, 3), new ExactFraction(1, 2), new ExactFraction(-3, 4) }.Select(factor => new CenterPostFactor("4", factor, "t", "l").Words));
    }

    [Fact]
    public void Without_a_code_or_a_table_the_answer_is_no_data()
    {
        Assert.StartsWith("No adopted code is chosen", Assert.IsType<DeckResult.NoData>(DeckEvaluator.CheckSpan(null, SpanUse.DeckJoist, Joists(In(117)))).Explanation, StringComparison.Ordinal);
        // The shipped Connecticut pack has DCA 6's joist and beam tables (#41) and no rafter, ledger or footing table yet.
        LoadedPack ct = Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "RealPacks"), "us-ct-2022"));
        Assert.Equal(
            "The loaded pack CT 2022 has no rafter span, so napkin cannot check this. Nothing is guessed: add it from your copy of the code (docs/rules-engine.md).",
            Assert.IsType<DeckResult.NoData>(DeckEvaluator.CheckSpan(ct, SpanUse.Rafter, new SpanRequest("2x8", In(117), null, "Southern Pine", In(16), null, 30))).Explanation);
        Assert.IsType<DeckResult.NoData>(DeckEvaluator.SizeLedger(ct, "2x8", In(117), In(144)));
        Assert.IsType<DeckResult.NoData>(DeckEvaluator.SizeFooting(Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "BracePacks"), "us-zz-brace-a")), new PostArea(ExactFraction.Whole(1), PostPosition.Corner, false), 2000));
        Assert.Equal(["deck joist span", "deck beam span", "rafter span"], new[] { SpanUse.DeckJoist, SpanUse.DeckBeam, SpanUse.Rafter }.Select(DeckEvaluator.Words));
        Assert.Equal("what the deck supports", DeckEvaluator.Spoken("supports"));
        Assert.Equal("member", DeckEvaluator.Spoken("member"));
    }
}

/// <summary>The deck files are refused as strictly as every pack file (#198): each malformed file below names its fault.</summary>
public class DeckLoaderTests
{
    const string Layer = "layers/zz-deck-2099/deck";

    static InMemoryPackSource Deck() => InMemoryPackSource.FromDirectory(Path.Combine(AppContext.BaseDirectory, "DeckPacks"));

    static string Refused(InMemoryPackSource source)
        => string.Join("\n", Fx.Invalid(PackLoader.Load(source, "us-zz-deck")).Problems.Select(problem => problem.Message));

    static InMemoryPackSource Edit(string file, string original, string replacement)
    {
        InMemoryPackSource source = Deck();
        string text = source.Text(file);
        Assert.Contains(original, text, StringComparison.Ordinal);
        return source.With(file, text.Replace(original, replacement, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"use\": \"deck-joist\"", "\"use\": \"deck-stair\"", "deck-stair")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"kind\": \"member-span\"", "\"kind\": \"deck-railing\"", "'deck-railing' is not a deck file kind")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"name\": \"spacing\"", "\"name\": \"height\"", "'height' is not an input this table can be asked")]
    [InlineData("zz-deck-footing.json", "\"band\": \"lower-bound\"", "\"band\": \"upper-bound\"", "'soilBearing' uses 'lower-bound' bands, not 'upper-bound'")]
    [InlineData("zz-deck-footing.json", "\"type\": \"sqft\"", "\"type\": \"psf\"", "'tributaryArea' is 'sqft', not 'psf'")]
    [InlineData("zz-deck-footing.json", "\"min\": 1500", "\"min\": 1000", "the 'soilBearing' bands start at 1500 psf but the column's domain declares min 1000 psf")]
    [InlineData("zz-deck-ledger.json", "\"encodedAs\": \"not-encoded\"", "\"encodedAs\": \"as-rows\"", "'not-encoded'")]
    [InlineData("zz-deck-ledger.json", "\"footnotes\": [\n        \"a\"", "\"footnotes\": [\n        \"q\"", "'q' is not one of the table's footnotes")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"member\": \"2x10\",\n      \"spacing\": \"12in\"", "\"member\": \"2x12\",\n      \"spacing\": \"12in\"", "'2x12' is not one of the column's values")]
    [InlineData("zz-deck-beam.json", "\"joistSpan\": \"16ft 0in\",\n      \"span\": \"4ft 3in\"", "\"joistSpan\": \"17ft 0in\",\n      \"span\": \"4ft 3in\"", "outside the column's domain")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"span\": \"11ft 1in\"", "\"span\": \"0in\"", "must be longer than zero")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"id\": \"r.fir.2x8.12\"", "\"id\": \"r.fir.2x8.16\"", "row id 'r.fir.2x8.16' is used 2 times")]
    [InlineData("zz-deck-beam.json", "\"source\": \"zz-synth-base\"", "\"source\": \"nowhere\"", "nowhere")]
    [InlineData("zz-guard-stair.json", "\"triggerHeight\": \"28in\"", "\"triggerHeight\": \"0in\"", "must be longer than zero, or null")]
    [InlineData("zz-guard-stair.json", "\"handrailWhenRisersAtLeast\": 3", "\"handrailWhenRisersAtLeast\": 0", "handrailWhenRisersAtLeast")]
    [InlineData("zz-guard-stair.json", "\"section\": \"ZZ-GUARD.1\"", "\"section\": \"ZZ-GUARD.1\", \"extra\": 1", "extra: unknown field")]
    [InlineData("zz-deck-post-center.json", "\"multiply\": \"3/2\",", "\"multiply\": \"1.5\",", "'1.5' is not a positive exact fraction")]
    [InlineData("zz-deck-post-center.json", "\"note\": \"b\",", "\"note\": \"a\",", "note 'a' is the factor, so it is not also one of the table's footnotes")]
    [InlineData("zz-deck-post-center.json", "\"text\": \"SYNTHETIC note b: a centre post's area under a continuous beam is multiplied by 3/2.\",", "\"text\": \" \",", "centerPostFactor.text: blank")]
    [InlineData("zz-deck-post-center.json", "\"location\": \"synthetic p. 9 note b\"", "\"place\": \"synthetic p. 9 note b\"", "centerPostFactor.location: missing required field")]
    [InlineData("zz-deck-post-center.json", "\"location\": \"synthetic p. 9 note b\"", "\"location\": \" \"", "centerPostFactor.location: blank")]
    [InlineData("zz-deck-post-center.json", "\"centerPostFactor\": {", "\"centerPostFactor\": 5, \"x\": {", "centerPostFactor: must be a JSON object")]
    [InlineData("zz-deck-post-corner.json", "\"position\": \"corner\",", "\"position\": \"corner\", \"centerPostFactor\": { \"note\": \"b\", \"text\": \"t\", \"multiply\": \"3/2\", \"location\": \"l\" },", "only a deck-footing table or a deck-post table for 'center' posts multiplies a centre post's tributary area; this deck-post table may not declare one")]
    [InlineData("zz-deck-ledger.json", "\"kind\": \"deck-ledger\",", "\"kind\": \"deck-ledger\", \"centerPostFactor\": { \"note\": \"b\", \"text\": \"t\", \"multiply\": \"3/2\", \"location\": \"l\" },", "this deck-ledger table may not declare one")]
    [InlineData("zz-deck-footing.json", "\"kind\": \"deck-footing\",", "\"kind\": \"deck-footing\", \"position\": \"corner\",", "position: unknown field")]
    public void A_malformed_deck_file_is_refused_naming_its_fault(string file, string original, string replacement, string message)
        => Assert.Contains(message, Refused(Edit(file.StartsWith("layers/", StringComparison.Ordinal) ? file : $"{Layer}/{file}", original, replacement)), StringComparison.Ordinal);

    [Fact]
    public void A_second_table_for_one_use_or_a_second_ledger_footing_or_guard_file_is_refused()
    {
        InMemoryPackSource twice = Deck();
        twice.With($"{Layer}/zz-deck-beam-2.json", twice.Text($"{Layer}/zz-deck-beam.json").Replace("ZZ-DECK-BEAM", "ZZ-DECK-BEAM-2", StringComparison.Ordinal));
        Assert.Contains("a second member-span table for 'deck-beam'", Refused(twice), StringComparison.Ordinal);

        foreach ((string file, string message) in new[]
                 {
                     ("zz-deck-ledger.json", "a second deck-ledger table"),
                     ("zz-deck-footing.json", "a second deck-footing table"),
                     ("zz-deck-post-corner.json", "a second deck-post table for 'corner' posts"),
                     ("zz-guard-stair.json", "a second deck-guard-stair file"),
                 })
        {
            InMemoryPackSource again = Deck();
            again.With($"{Layer}/copy-{file}", again.Text($"{Layer}/{file}"));
            Assert.Contains(message, Refused(again), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_guard_or_stair_may_be_null_and_any_item_not_covered()
    {
        InMemoryPackSource source = Edit($"{Layer}/zz-guard-stair.json", "\"maximumOpening\": \"5in\"", "\"maximumOpening\": null");
        string text = source.Text($"{Layer}/zz-guard-stair.json");
        int stair = text.IndexOf("\"stair\":", StringComparison.Ordinal);
        int end = text.IndexOf('}', stair);
        source.With($"{Layer}/zz-guard-stair.json", text[..stair] + "\"stair\": null" + text[(end + 1)..]);

        GuardStairProvisions provisions = Fx.Loaded(PackLoader.Load(source, "us-zz-deck")).Deck.GuardStair!;
        Assert.Null(provisions.Guard!.MaximumOpening);
        Assert.Null(provisions.Stair);
    }

    [Theory]
    [InlineData("\"kind\": \"frost\"", "\"kind\": \"frozen\"", "frost.json is 'frost', not 'frozen'")]
    [InlineData("\"frostLineDepth\": \"3ft 6in\"", "\"frostLineDepth\": \"0in\"", "frostLineDepth: must be deeper than zero")]
    [InlineData("\"encodedAs\": \"not-encoded\"", "\"encodedAs\": \"as-rows\"", "'not-encoded'")]
    public void A_malformed_frost_file_is_refused(string original, string replacement, string message)
        => Assert.Contains(message, Refused(Edit("packs/us-zz-deck/frost.json", original, replacement)), StringComparison.Ordinal);

    [Fact]
    public void A_header_table_may_not_use_the_lower_bound_band()
    {
        InMemoryPackSource source = Edit("layers/zz-deck-2099/tables/zz-deck-header.json", "\"band\": \"upper-bound\"", "\"band\": \"lower-bound\"");
        Assert.Contains("header-sizing columns are upper-bound or capacity, not lower-bound", Refused(source), StringComparison.Ordinal);
    }

    [Fact]
    public void A_pack_whose_base_layer_has_no_deck_files_and_no_guide_has_no_deck_tables()
    {
        // Connecticut's base layer irc-2021 has no deck/ directory, and revision 6 removed the DCA 6 guide
        // (docs/research/safe-default-header-sources.md §1: AWC's EULA, read for a different question, found it
        // prohibits inputting its Product into an AI program). No deck tables load from any source.
        LoadedPack ct = Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "RealPacks"), "us-ct-2022"));
        Assert.Empty(ct.Deck.Spans);
        Assert.Empty(ct.Deck.Tables);
        Assert.Equal((null, null), (ct.Deck.Ledger, ct.Deck.GuardStair));
        Assert.Empty(DeckProvisions.None.Spans);
        Assert.NotNull(ct.Frost);
        Assert.Equal("1500 psf", CellValue.Whole(ColumnType.Psf, 1500).ToString());
        Assert.Equal("27 sq ft", CellValue.Whole(ColumnType.SquareFeet, 27).ToString());
    }
}

/// <summary>More of the deck reader's refusals, one malformed file each, and the evaluator given every input.</summary>
public class DeckReaderEdgeTests
{
    const string Layer = "layers/zz-deck-2099/deck";
    const string GuideJoist = "layers/zz-guide-2099/deck/zz-guide-joist.json";

    static InMemoryPackSource Deck() => InMemoryPackSource.FromDirectory(Path.Combine(AppContext.BaseDirectory, "DeckPacks"));

    static string Refused(InMemoryPackSource source)
        => string.Join("\n", Fx.Invalid(PackLoader.Load(source, "us-zz-deck")).Problems.Select(problem => problem.Message));

    static string Edited(string file, string original, string replacement)
    {
        InMemoryPackSource source = Deck();
        string text = source.Text(file);
        Assert.Contains(original, text, StringComparison.Ordinal);
        return Refused(source.With(file, text.Replace(original, replacement, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"kind\": \"member-span\",", "", "kind: missing required field")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"inputs\": [\n    {", "\"inputs\": [\n    42, {", "must be a JSON object")]
    [InlineData("zz-deck-beam.json", "\"zz-fir\"\n      ]", "\"zz-fir\", \"zz-fir\"\n      ]", "'zz-fir' is listed twice")]
    [InlineData("zz-deck-beam.json", "\"name\": \"member\",", "\"name\": \"species\",", "'species' is declared twice")]
    [InlineData("zz-deck-ledger.json", "\"min\": \"1in\",\n        \"max\": \"16ft 0in\"", "\"min\": \"17ft 0in\",\n        \"max\": \"16ft 0in\"", "is above max")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"footnotes\": [\n    {", "\"footnotes\": [\n    42, {", "must be a JSON object")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"rows\": [\n    {", "\"rows\": [\n    42, {", "must be a JSON object")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"spacing\": \"12in\",", "", "spacing: missing required field")]
    [InlineData("layers/zz-guide-2099/deck/zz-guide-joist.json", "\"span\": \"11ft 1in\"", "\"span\": 5", "span")]
    [InlineData("zz-deck-ledger.json", "\"fastener\": \"zz-bolts, staggered\",", "", "fastener: missing required field")]
    [InlineData("zz-deck-footing.json", "\"round\": \"16in\",", "", "round: missing required field")]
    [InlineData("zz-deck-footing.json", "\"thickness\": \"6in\",", "\"thickness\": \"0in\",", "thickness: must be longer than zero")]
    [InlineData("zz-deck-post-corner.json", "\"height\": \"6ft 0in\",", "\"height\": \"6ft 0in\", \"notPermitted\": true,", "a deck-post row has exactly one of 'height' (the printed post height) or 'notPermitted': true")]
    [InlineData("zz-deck-post-corner.json", "\"height\": \"6ft 0in\",", "", "a deck-post row has exactly one of 'height'")]
    [InlineData("zz-deck-post-corner.json", "\"notPermitted\": true,", "\"notPermitted\": false,", "is true when the cell prints NP; a cell with a height writes 'height' instead")]
    [InlineData("zz-deck-post-corner.json", "\"height\": \"6ft 0in\",", "\"height\": \"0in\",", "height: must be longer than zero")]
    [InlineData("zz-deck-post-corner.json", "\"position\": \"corner\",", "\"position\": \"edge\",", "'edge'")]
    [InlineData("zz-deck-post-corner.json", "\"position\": \"corner\",", "", "position: missing required field")]
    [InlineData("zz-deck-post-corner.json", "\"name\": \"post\",", "\"name\": \"member\",", "'member' is not an input this table can be asked")]
    [InlineData("zz-guard-stair.json", "\"location\": \"synthetic p. 7 guard\"", "\"place\": \"synthetic p. 7 guard\"", "location: missing required field")]
    [InlineData("zz-guard-stair.json", "\"minimumTread\": \"9in\"", "\"minimumTread\": 9", "minimumTread")]
    public void Each_is_refused_naming_its_fault(string file, string original, string replacement, string message)
        => Assert.Contains(message, Edited(file.StartsWith("layers/", StringComparison.Ordinal) ? file : $"{Layer}/{file}", original, replacement), StringComparison.Ordinal);

    [Fact]
    public void A_footnote_declared_twice_a_file_that_is_not_json_and_a_bad_frost_file_are_refused()
    {
        InMemoryPackSource twice = Deck();
        string joist = twice.Text(GuideJoist);
        int start = joist.IndexOf("\"footnotes\": [", StringComparison.Ordinal);
        int open = joist.IndexOf('{', start), close = joist.IndexOf('}', open);
        string note = joist[open..(close + 1)];
        twice.With(GuideJoist, joist.Insert(close + 1, ", " + note));
        Assert.Contains("footnote 'a' is declared twice", Refused(twice), StringComparison.Ordinal);

        Assert.NotEmpty(Refused(Deck().With(GuideJoist, "{ not json")));
        Assert.NotEmpty(Refused(Deck().With("packs/us-zz-deck/frost.json", "{ not json")));
        Assert.Contains("kind: missing required field", Edited("packs/us-zz-deck/frost.json", "\"kind\": \"frost\",", ""), StringComparison.Ordinal);
        Assert.Contains("frostLineDepth: missing required field", Edited("packs/us-zz-deck/frost.json", "\"frostLineDepth\": \"3ft 6in\",", ""), StringComparison.Ordinal);
    }

    [Fact]
    public void A_guard_with_no_trigger_and_a_stair_with_no_handrail_rule_are_not_covered_items()
    {
        InMemoryPackSource source = Deck();
        string text = source.Text($"{Layer}/zz-guard-stair.json")
            .Replace("\"triggerHeight\": \"28in\"", "\"triggerHeight\": null", StringComparison.Ordinal)
            .Replace("\"handrailWhenRisersAtLeast\": 3", "\"handrailWhenRisersAtLeast\": null", StringComparison.Ordinal);
        GuardStairProvisions provisions = Fx.Loaded(PackLoader.Load(source.With($"{Layer}/zz-guard-stair.json", text), "us-zz-deck")).Deck.GuardStair!;

        Assert.Null(provisions.Guard!.TriggerHeight);
        Assert.Null(provisions.Stair!.HandrailWhenRisersAtLeast);

        string noGuard = source.Text($"{Layer}/zz-guard-stair.json");
        int guard = noGuard.IndexOf("\"guard\":", StringComparison.Ordinal);
        int end = noGuard.IndexOf('}', guard);
        GuardStairProvisions stairOnly = Fx.Loaded(PackLoader.Load(Deck().With($"{Layer}/zz-guard-stair.json", noGuard[..guard] + "\"guard\": null" + noGuard[(end + 1)..]), "us-zz-deck")).Deck.GuardStair!;
        Assert.Null(stairOnly.Guard);
    }

    [Fact]
    public void Every_input_given_the_joist_table_reads_only_its_own()
    {
        LoadedPack pack = Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "DeckPacks"), "us-zz-deck"));
        SpanRequest everything = new("2x8", Length.Inches(117), "zz-deck", "zz-fir", Length.Inches(16), Length.Inches(117), GroundSnowLoad: 30, RoofLiveLoad: 20, DeckLength: Length.Inches(120), DeckWidth: Length.Inches(144));
        Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckSpan(pack, SpanUse.DeckJoist, everything));

        SpanRequest nothing = new("2x8", Length.Inches(117), null, null, null, null);
        Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.CheckSpan(pack, SpanUse.DeckJoist, nothing));
        Assert.Equal("roof live load", DeckEvaluator.Spoken("roofLiveLoad"));
        Assert.Equal("ground snow load", DeckEvaluator.Spoken("groundSnowLoad"));
        Assert.Equal("tributary area", DeckEvaluator.Spoken("tributaryArea"));
        Assert.Equal("joist span", DeckEvaluator.Spoken("joistSpan"));
        Assert.Equal("soil bearing value", DeckEvaluator.Spoken("soilBearing"));
    }
}

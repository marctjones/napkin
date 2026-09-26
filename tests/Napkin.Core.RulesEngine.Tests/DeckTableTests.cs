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

    static SpanRequest Joists(Length span, string? species = "zz-fir", string? supports = "zz-deck", string member = "2x8", long spacing = 16)
        => new(member, span, supports, species, In(spacing), null);

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
        DeckResult.OutOfScope roof = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(117), supports: "zz-deck-and-roof")));
        Assert.Equal("Table ZZ-DECK-JOIST has no row for what the deck supports zz-deck-and-roof: get it engineered.", roof.Explanation);

        Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(117), spacing: 24)));

        DeckResult.InputMissing species = Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(117), species: null)));
        Assert.Equal("species", species.Input);
        Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, Joists(In(117), supports: null)));
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
        // 66 3/4" × 58 1/2" = 3904.875 sq in = 27.1 sq ft (≤ 40) on 2000 psf: "zz 15 in square".
        ExactFraction area = new((Int128)In(66, 3, 4).Units * In(58, 1, 2).Units, 1);
        Assert.Equal("zz 15 in square", Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(Deck(), area, 2000)).Row.Text);

        // 1999 psf is never rounded up to the 2000 column: the 1500 one, "zz 18 in square".
        Assert.Equal("zz 18 in square", Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(Deck(), area, 1999)).Row.Text);
        Assert.Equal("zz 13 in square", Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(Deck(), area, 3500)).Row.Text);

        DeckResult.OutOfScope soft = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.SizeFooting(Deck(), area, 1400));
        Assert.Contains("below the lowest band of table ZZ-DECK-FOOTING (1500 psf)", soft.Explanation, StringComparison.Ordinal);
        Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.SizeFooting(Deck(), new ExactFraction((Int128)81 * 144 * 1024 * 1024, 1), 2000));
        Assert.Equal("soilBearing", Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.SizeFooting(Deck(), area, null)).Input);
    }

    [Fact]
    public void Without_a_code_or_a_table_the_answer_is_no_data()
    {
        Assert.StartsWith("No adopted code is chosen", Assert.IsType<DeckResult.NoData>(DeckEvaluator.CheckSpan(null, SpanUse.DeckJoist, Joists(In(117)))).Explanation, StringComparison.Ordinal);
        LoadedPack ct = Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "RealPacks"), "us-ct-2022"));
        Assert.Equal(
            "The loaded pack CT 2022 has no deck joist span, so napkin cannot check this. Nothing is guessed: add it from your copy of the code (docs/rules-engine.md).",
            Assert.IsType<DeckResult.NoData>(DeckEvaluator.CheckSpan(ct, SpanUse.DeckJoist, Joists(In(117)))).Explanation);
        Assert.IsType<DeckResult.NoData>(DeckEvaluator.SizeLedger(ct, "2x8", In(117), In(144)));
        Assert.IsType<DeckResult.NoData>(DeckEvaluator.SizeFooting(ct, ExactFraction.Whole(1), 2000));
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
    [InlineData("zz-deck-joist.json", "\"use\": \"deck-joist\"", "\"use\": \"deck-stair\"", "deck-stair")]
    [InlineData("zz-deck-joist.json", "\"kind\": \"member-span\"", "\"kind\": \"deck-railing\"", "'deck-railing' is not a deck file kind")]
    [InlineData("zz-deck-joist.json", "\"name\": \"spacing\"", "\"name\": \"height\"", "'height' is not an input this table can be asked")]
    [InlineData("zz-deck-footing.json", "\"band\": \"lower-bound\"", "\"band\": \"upper-bound\"", "'soilBearing' uses 'lower-bound' bands, not 'upper-bound'")]
    [InlineData("zz-deck-footing.json", "\"type\": \"sqft\"", "\"type\": \"psf\"", "'tributaryArea' is 'sqft', not 'psf'")]
    [InlineData("zz-deck-footing.json", "\"min\": 1500", "\"min\": 1000", "the 'soilBearing' bands start at 1500 psf but the column's domain declares min 1000 psf")]
    [InlineData("zz-deck-ledger.json", "\"encodedAs\": \"not-encoded\"", "\"encodedAs\": \"as-rows\"", "'not-encoded'")]
    [InlineData("zz-deck-ledger.json", "\"footnotes\": [\n        \"a\"", "\"footnotes\": [\n        \"q\"", "'q' is not one of the table's footnotes")]
    [InlineData("zz-deck-joist.json", "\"member\": \"2x10\",\n      \"spacing\": \"12in\"", "\"member\": \"2x12\",\n      \"spacing\": \"12in\"", "'2x12' is not one of the column's values")]
    [InlineData("zz-deck-beam.json", "\"joistSpan\": \"16ft 0in\",\n      \"span\": \"4ft 3in\"", "\"joistSpan\": \"17ft 0in\",\n      \"span\": \"4ft 3in\"", "outside the column's domain")]
    [InlineData("zz-deck-joist.json", "\"span\": \"11ft 1in\"", "\"span\": \"0in\"", "must be longer than zero")]
    [InlineData("zz-deck-joist.json", "\"id\": \"r.fir.2x8.12\"", "\"id\": \"r.fir.2x8.16\"", "row id 'r.fir.2x8.16' is used 2 times")]
    [InlineData("zz-deck-joist.json", "\"source\": \"zz-synth-base\"", "\"source\": \"nowhere\"", "nowhere")]
    [InlineData("zz-guard-stair.json", "\"triggerHeight\": \"28in\"", "\"triggerHeight\": \"0in\"", "must be longer than zero, or null")]
    [InlineData("zz-guard-stair.json", "\"handrailWhenRisersAtLeast\": 3", "\"handrailWhenRisersAtLeast\": 0", "handrailWhenRisersAtLeast")]
    [InlineData("zz-guard-stair.json", "\"section\": \"ZZ-GUARD.1\"", "\"section\": \"ZZ-GUARD.1\", \"extra\": 1", "extra: unknown field")]
    public void A_malformed_deck_file_is_refused_naming_its_fault(string file, string original, string replacement, string message)
        => Assert.Contains(message, Refused(Edit($"{Layer}/{file}", original, replacement)), StringComparison.Ordinal);

    [Fact]
    public void A_second_table_for_one_use_or_a_second_ledger_footing_or_guard_file_is_refused()
    {
        InMemoryPackSource twice = Deck();
        twice.With($"{Layer}/zz-deck-joist-2.json", twice.Text($"{Layer}/zz-deck-joist.json").Replace("ZZ-DECK-JOIST", "ZZ-DECK-JOIST-2", StringComparison.Ordinal));
        Assert.Contains("a second member-span table for 'deck-joist'", Refused(twice), StringComparison.Ordinal);

        foreach ((string file, string message) in new[]
                 {
                     ("zz-deck-ledger.json", "a second deck-ledger table"),
                     ("zz-deck-footing.json", "a second deck-footing table"),
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
    public void A_pack_with_no_deck_files_has_no_deck_provisions()
    {
        LoadedPack ct = Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "RealPacks"), "us-ct-2022"));
        Assert.Empty(ct.Deck.Spans);
        Assert.Equal((null, null, null), (ct.Deck.Ledger, ct.Deck.Footing, ct.Deck.GuardStair));
        Assert.Empty(DeckProvisions.None.Spans);
        Assert.Null(ct.Frost);
        Assert.Equal("1500 psf", CellValue.Whole(ColumnType.Psf, 1500).ToString());
        Assert.Equal("27 sq ft", CellValue.Whole(ColumnType.SquareFeet, 27).ToString());
    }
}

/// <summary>More of the deck reader's refusals, one malformed file each, and the evaluator given every input.</summary>
public class DeckReaderEdgeTests
{
    const string Layer = "layers/zz-deck-2099/deck";

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
    [InlineData("zz-deck-joist.json", "\"kind\": \"member-span\",", "", "kind: missing required field")]
    [InlineData("zz-deck-joist.json", "\"inputs\": [\n    {", "\"inputs\": [\n    42, {", "must be a JSON object")]
    [InlineData("zz-deck-joist.json", "\"zz-fir\"\n      ]", "\"zz-fir\", \"zz-fir\"\n      ]", "'zz-fir' is listed twice")]
    [InlineData("zz-deck-joist.json", "\"name\": \"supports\",", "\"name\": \"species\",", "'species' is declared twice")]
    [InlineData("zz-deck-ledger.json", "\"min\": \"1in\",\n        \"max\": \"16ft 0in\"", "\"min\": \"17ft 0in\",\n        \"max\": \"16ft 0in\"", "is above max")]
    [InlineData("zz-deck-joist.json", "\"footnotes\": [\n    {", "\"footnotes\": [\n    42, {", "must be a JSON object")]
    [InlineData("zz-deck-joist.json", "\"rows\": [\n    {", "\"rows\": [\n    42, {", "must be a JSON object")]
    [InlineData("zz-deck-joist.json", "\"spacing\": \"12in\",", "", "spacing: missing required field")]
    [InlineData("zz-deck-joist.json", "\"span\": \"11ft 1in\"", "\"span\": 5", "span")]
    [InlineData("zz-deck-ledger.json", "\"fastener\": \"zz-bolts, staggered\",", "", "fastener: missing required field")]
    [InlineData("zz-deck-footing.json", "\"footing\": \"zz 14 in square\",", "", "footing: missing required field")]
    [InlineData("zz-guard-stair.json", "\"location\": \"synthetic p. 7 guard\"", "\"place\": \"synthetic p. 7 guard\"", "location: missing required field")]
    [InlineData("zz-guard-stair.json", "\"minimumTread\": \"9in\"", "\"minimumTread\": 9", "minimumTread")]
    public void Each_is_refused_naming_its_fault(string file, string original, string replacement, string message)
        => Assert.Contains(message, Edited($"{Layer}/{file}", original, replacement), StringComparison.Ordinal);

    [Fact]
    public void A_footnote_declared_twice_a_file_that_is_not_json_and_a_bad_frost_file_are_refused()
    {
        InMemoryPackSource twice = Deck();
        string joist = twice.Text($"{Layer}/zz-deck-joist.json");
        int start = joist.IndexOf("\"footnotes\": [", StringComparison.Ordinal);
        int open = joist.IndexOf('{', start), close = joist.IndexOf('}', open);
        string note = joist[open..(close + 1)];
        twice.With($"{Layer}/zz-deck-joist.json", joist.Insert(close + 1, ", " + note));
        Assert.Contains("footnote 'a' is declared twice", Refused(twice), StringComparison.Ordinal);

        Assert.NotEmpty(Refused(Deck().With($"{Layer}/zz-deck-joist.json", "{ not json")));
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
        SpanRequest everything = new("2x8", Length.Inches(117), "zz-deck", "zz-fir", Length.Inches(16), Length.Inches(117), GroundSnowLoad: 30, RoofLiveLoad: 20);
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

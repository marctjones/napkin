using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// Guide layers, scope limits and species groups (#238, docs/design/deck-guide-pack.md §1.2, §2, §3.6) on
/// the SYNTHETIC pack us-zz-deck (NOT CODE VALUES), whose joist table comes from the synthetic guide
/// zz-guide-2099: a guide on the "2098 IRC" under a pack adopting the "IRC 2099", with limits on what the
/// deck supports (only zz-deck), the snow load (not above 77 psf) and the deck's shape (no longer than
/// wide), and species groups "zz-fir, zz-hem" and "zz-cedar".
/// </summary>
public class GuideLayerTests
{
    const string Joist = "layers/zz-guide-2099/deck/zz-guide-joist.json";
    const string Manifest = "layers/zz-guide-2099/layer.json";

    static string Root => Path.Combine(AppContext.BaseDirectory, "DeckPacks");

    static LoadedPack Deck() => Fx.Loaded(PackLoader.Load(Root, "us-zz-deck"));

    static Length In(long whole) => Length.Inches(whole);

    static SpanRequest Joists(Length span, string? species = "zz-fir", string? supports = "zz-deck", string member = "2x8", int? snow = 30, Length? length = null, Length? width = null)
        => new(member, span, supports, species, In(16), null, GroundSnowLoad: snow, DeckLength: length ?? In(120), DeckWidth: width ?? In(144));

    static DeckResult Check(SpanRequest request) => DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckJoist, request);

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void The_pack_declares_the_guide_and_its_table_carries_it()
    {
        LoadedPack pack = Deck();
        DeckGuide guide = Assert.Single(pack.Guides);
        DeckTable joists = pack.Deck.Spans[SpanUse.DeckJoist];

        Assert.Equal(("zz-guide-2099", "ZZ GUIDE", "2098 IRC"), (guide.Id, guide.ShortName, guide.BasisText));
        Assert.Equal(["s.loads", "s.snow", "s.shape"], guide.Limits.Select(limit => limit.Id));
        Assert.Equal(["n.single"], guide.Notes.Select(note => note.Id));
        Assert.Equal(["zz-fir", "zz-hem", "zz-cedar"], guide.Species);
        Assert.Equal("zz-synth-guide", Assert.Single(guide.Sources).Id);
        Assert.Equal(new GuideEntry("zz-guide-2099", "SYNTHETIC: the deck joist table comes from the synthetic guide, not the base layer."), Assert.Single(pack.Manifest.Guides));

        Assert.Same(guide, joists.Guide);
        Assert.Equal(CitationLayer.Guide, joists.Layer);
        Assert.Equal(Joist, joists.File);
        Assert.Equal("zz-synth-guide", joists.Source.SourceId);
        Assert.Equal(["zz-fir, zz-hem", "zz-cedar"], joists.SpeciesGroups.Select(group => group.Group));
        Assert.Equal("t.member", Assert.Single(joists.Limits).Id);

        // The base layer's own tables carry no guide and cite the model code.
        Assert.Null(pack.Deck.Spans[SpanUse.DeckBeam].Guide);
        Assert.Equal(CitationLayer.ModelCode, pack.Deck.Ledger!.Layer);
        Assert.Null(pack.Deck.GuardStair!.Guide);
        Assert.Equal("layers/zz-deck-2099/deck/zz-guard-stair.json", pack.Deck.GuardStair.File);
        Assert.Equal(["ZZ-GUIDE-JOIST", "ZZ-DECK-BEAM", "ZZ-RAFTER", "ZZ-DECK-LEDGER", "ZZ-DECK-FOOTING", "ZZ-DECK-POST-CORNER", "ZZ-DECK-POST-CENTER"], pack.Deck.Tables.Select(table => table.Designation));
    }

    [Fact]
    public void The_picker_says_where_the_deck_tables_come_from_and_a_pack_without_a_guide_is_unchanged()
    {
        Assert.Equal("deck tables from ZZ GUIDE, a guide", Deck().StatusLabel);

        // The shipped Connecticut pack declares no guide (revision 6 removed DCA 6: no clause, just the base label).
        LoadedPack ct = Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "RealPacks"), "us-ct-2022"));
        Assert.Equal("base tables not loaded", ct.StatusLabel);
        Assert.Empty(ct.Guides);
        Assert.Empty(ct.Manifest.Guides);

        // A pack without a guide is unchanged.
        Assert.Empty(Fx.Load("us-zz-state").Guides);
        Assert.Equal(string.Empty, Fx.Load("us-zz-state").StatusLabel);

        // No header table and a guide: both clauses.
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Root).Without("layers/zz-deck-2099/tables/zz-deck-header.json");
        Assert.Equal("base tables not loaded; deck tables from ZZ GUIDE, a guide", Fx.Loaded(PackLoader.Load(source, "us-zz-deck")).StatusLabel);
    }

    [Fact]
    public void The_clause_and_the_paragraph_are_composed_from_the_manifests()
    {
        LoadedPack pack = Deck();
        DeckGuide guide = pack.Guides[0];

        Assert.Equal("a guide on the 2098 IRC, not ZZ DECK's adopted IRC 2099; the IRC governs where they differ (synthetic guide p. 1)", guide.Clause(pack.Code));
        Assert.Equal(
            "Deck checks under ZZ DECK use ZZ GUIDE (SYNTHETIC DECK GUIDE - NOT A CODE, nobody (synthetic)), a guide based on the 2098 IRC, not ZZ DECK's adopted code (IRC 2099). "
            + "It says: \"SYNTHETIC: based on the 2098 model code.\" (synthetic guide cover); \"SYNTHETIC: where this guide and the model code differ, the model code applies.\" (synthetic guide p. 1). "
            + "Its scope: \"SYNTHETIC: covers a deck carrying only its own loads.\" (synthetic guide p. 2, item 8); \"SYNTHETIC: not for ground snow loads above 77 psf.\" (synthetic guide p. 2, item 9); "
            + "\"SYNTHETIC: a deck no longer out from the house than it is wide.\" (synthetic guide p. 2, item 2); \"SYNTHETIC: one level, attached to the house.\" (synthetic guide p. 2, item 1).",
            guide.Paragraph(pack.Code));

        // Without its own statement that the model code governs, the clause says only what the guide is based on.
        DeckGuide silent = guide with { Caveats = ValueList.Of(guide.Caveats[0]) };
        Assert.Equal("a guide on the 2098 IRC, not ZZ DECK's adopted IRC 2099", silent.Clause(pack.Code));
    }

    [Fact]
    public void A_citation_from_a_guide_names_it_as_a_guide()
    {
        LoadedPack pack = Deck();
        SourceRef source = pack.Deck.Spans[SpanUse.DeckJoist].Source;
        Citation cited = new(pack.Code, "ZZ-GUIDE-JOIST", "r.fir.2x8.16", "zz-fir", CitationLayer.Guide, source, ValueList<FootnoteRef>.Empty, ValueList<BandMatch>.Empty);
        Assert.StartsWith("Table ZZ-GUIDE-JOIST of a guide, not ZZ DECK's adopted IRC 2099 row r.fir.2x8.16", cited.ToString(), StringComparison.Ordinal);
        Assert.Equal(" UNREVIEWED: values not yet checked against the source.", pack.Code.UnreviewedSentence);
        Assert.Empty((pack.Code with { Review = ReviewStatus.SignedOff }).UnreviewedSentence);
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void Every_scope_limit_is_checked_before_the_lookup_and_cited()
    {
        // At the limits: in scope, the row answers.
        Assert.IsType<DeckResult.Passes>(Check(Joists(In(117), snow: 77, length: In(144))));

        DeckResult.OutOfScope snow = Assert.IsType<DeckResult.OutOfScope>(Check(Joists(In(117), snow: 78)));
        Assert.Equal("Beyond the scope of ZZ GUIDE: \"SYNTHETIC: not for ground snow loads above 77 psf.\" (ZZ GUIDE synthetic guide p. 2, item 9). Get it engineered.", snow.Explanation);
        Assert.Equal("s.snow", snow.Limit!.Id);

        DeckResult.OutOfScope shape = Assert.IsType<DeckResult.OutOfScope>(Check(Joists(In(117), length: In(144) + new Length(1))));
        Assert.Equal(("s.shape", ScopeForm.AboveInput, "deckWidth"), (shape.Limit!.Id, shape.Limit.When.Form, shape.Limit.When.OtherInput));

        // The first limit that holds decides: a porch deck in deep snow is out of scope for what it supports.
        Assert.Equal("s.loads", Assert.IsType<DeckResult.OutOfScope>(Check(Joists(In(117), supports: "zz-deck-and-roof", snow: 99))).Limit!.Id);

        // The table's own limit comes after the guide's, and is cited to the table.
        DeckResult.OutOfScope shallow = Assert.IsType<DeckResult.OutOfScope>(Check(Joists(In(117), member: "2x6")));
        Assert.Equal("Beyond table ZZ-GUIDE-JOIST: \"SYNTHETIC note 6: joists are 2x8 or deeper.\" (ZZ GUIDE synthetic guide p. 3, note 6). Get it engineered.", shallow.Explanation);
    }

    [Fact]
    public void An_input_a_limit_tests_and_nobody_entered_is_asked_for_by_name()
    {
        DeckResult.InputMissing snow = Assert.IsType<DeckResult.InputMissing>(Check(Joists(In(117), snow: null)));
        Assert.Equal(("groundSnowLoad", "Enter the ground snow load: ZZ GUIDE scope limit s.snow (ZZ GUIDE synthetic guide p. 2, item 9) depends on it."), (snow.Input, snow.Explanation));

        Assert.Equal("deckLength", Assert.IsType<DeckResult.InputMissing>(Check(Joists(In(117)) with { DeckLength = null })).Input);
        DeckResult.InputMissing width = Assert.IsType<DeckResult.InputMissing>(Check(Joists(In(117)) with { DeckWidth = null }));
        Assert.Equal(("deckWidth", "Enter the deck's width along the house: ZZ GUIDE scope limit s.shape (ZZ GUIDE synthetic guide p. 2, item 2) depends on it."), (width.Input, width.Explanation));
        Assert.Equal("deck's length out from the house", DeckEvaluator.Spoken("deckLength"));
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void A_typed_species_is_read_as_the_group_its_table_prints()
    {
        DeckResult.Passes hem = Assert.IsType<DeckResult.Passes>(Check(Joists(In(117), species: "zz-hem")));
        Assert.Equal(("r.fir.2x8.16", "zz-fir, zz-hem"), (hem.Row.Id, hem.Group!.Group));

        DeckResult.Short cedar = Assert.IsType<DeckResult.Short>(Check(Joists(In(117), species: "zz-cedar")));
        Assert.Equal(("r.cedar.2x8.16", Length.FeetInches(9, 7)), (cedar.Row.Id, cedar.Allowed));

        DeckResult.OutOfScope oak = Assert.IsType<DeckResult.OutOfScope>(Check(Joists(In(117), species: "zz-oak")));
        Assert.Equal("Table ZZ-GUIDE-JOIST places no species zz-oak in its groups (zz-fir, zz-hem; zz-cedar): get it engineered.", oak.Explanation);
        Assert.Equal((null, "species"), (oak.Limit, oak.Column));

        // A table without groups reads the species as typed.
        Assert.Null(Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckSpan(Deck(), SpanUse.DeckBeam, new SpanRequest("(2) 2x10", In(60), null, "zz-fir", null, In(117)))).Group);
    }

    [Fact]
    public void A_guides_limits_reach_its_ledger_and_footing_through_the_deck_they_are_given()
    {
        // The ledger and footing moved under the guide: the scope needs the deck's facts, and a footing
        // has no member for a member limit.
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Root);
        foreach (string file in new[] { "zz-deck-ledger.json", "zz-deck-footing.json" })
        {
            source.With($"layers/zz-guide-2099/deck/{file}", source.Text($"layers/zz-deck-2099/deck/{file}").Replace("zz-synth-base", "zz-synth-guide", StringComparison.Ordinal))
                .Without($"layers/zz-deck-2099/deck/{file}");
        }

        LoadedPack pack = Fx.Loaded(PackLoader.Load(source, "us-zz-deck"));
        DeckScopeInputs deck = new("zz-deck", "zz-fir", 30, In(120), In(144));
        PostArea area = new(new ExactFraction((Int128)In(60).Units * In(60).Units, 1), PostPosition.Corner, ContinuousBeam: false);

        Assert.Equal("supports", Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.SizeLedger(pack, "2x8", In(117), In(144))).Input);
        Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeLedger(pack, "2x8", In(117), In(144), deck));
        Assert.Equal("s.snow", Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.SizeFooting(pack, area, 2000, deck with { GroundSnowLoad = 78 })).Limit!.Id);
        Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(pack, area, 2000, deck));
        Assert.Equal("supports", Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.SizeFooting(pack, area, 2000)).Input);
        Assert.Equal(DeckScopeInputs.NotEntered, new DeckScopeInputs(null, null, null, null, null));
    }

    [Fact]
    public void A_table_limit_on_a_base_layer_table_is_cited_to_the_table()
    {
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Root);
        string ledger = "layers/zz-deck-2099/deck/zz-deck-ledger.json";
        source.With(ledger, source.Text(ledger).Replace(
            "\"footnotes\": [\n    {",
            "\"limits\": [ { \"id\": \"t.ledger\", \"when\": { \"input\": \"member\", \"equals\": \"2x10\" }, \"text\": \"SYNTHETIC: no 2x10 ledgers.\", \"location\": \"synthetic p. 5 note 9\" } ],\n  \"footnotes\": [\n    {",
            StringComparison.Ordinal));
        LoadedPack pack = Fx.Loaded(PackLoader.Load(source, "us-zz-deck"));

        DeckResult.OutOfScope stopped = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.SizeLedger(pack, "2x10", In(117), In(144)));
        Assert.Equal("Beyond table ZZ-DECK-LEDGER: \"SYNTHETIC: no 2x10 ledgers.\" (synthetic p. 5 note 9). Get it engineered.", stopped.Explanation);
        Assert.Equal(ScopeForm.EqualTo, stopped.Limit!.When.Form);
        Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeLedger(pack, "2x8", In(117), In(144)));
    }
}

/// <summary>A guide layer is refused as strictly as every pack file (#238): each malformed file below names its fault.</summary>
public class GuideLoaderTests
{
    const string Joist = "layers/zz-guide-2099/deck/zz-guide-joist.json";
    const string Manifest = "layers/zz-guide-2099/layer.json";
    const string Pack = "packs/us-zz-deck/pack.json";

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
    [InlineData(Pack, "\"id\": \"zz-guide-2099\",", "\"id\": \"ZZ\",", "guides[0].id: 'ZZ' is not a layer id")]
    [InlineData(Pack, "\"id\": \"zz-guide-2099\",", "\"id\": \"zz-nowhere\",", "guide 'zz-nowhere' does not resolve")]
    [InlineData(Pack, "\"id\": \"zz-guide-2099\",", "\"id\": \"zz-deck-2099\",", "'zz-deck-2099' is the pack's base layer")]
    [InlineData(Pack, "\"guides\": [\n    {", "\"guides\": [\n    { \"id\": \"zz-guide-2099\" },\n    {", "guide 'zz-guide-2099' is listed twice")]
    [InlineData(Pack, "\"guides\": [\n    {", "\"guides\": [\n    42, {", "guides[0]: must be a JSON object")]
    [InlineData(Manifest, "\"kind\": \"guide\",", "\"kind\": \"base\",", "kind: 'base': a pack's guides name guide layers")]
    [InlineData(Manifest, "\"kind\": \"guide\",", "", "kind: missing required field")]
    [InlineData(Manifest, "\"id\": \"zz-guide-2099\",", "\"id\": \"zz-guide-2098\",", "id 'zz-guide-2098' does not match its directory")]
    [InlineData(Manifest, "\"publisher\": \"ICC\",", "\"publisher\": \"ZZC\",", "guide.basis.publisher: 'ZZC' is not supported")]
    [InlineData(Manifest, "\"code\": \"IRC\",", "\"code\": \"ZRC\",", "guide.basis.code: 'ZRC' is not supported")]
    [InlineData(Manifest, "\"shortName\": \"ZZ GUIDE\",", "", "guide.shortName: missing required field")]
    [InlineData(Manifest, "\"location\": \"synthetic guide cover\"", "\"place\": \"synthetic guide cover\"", "caveats[0].location: missing required field")]
    [InlineData(Manifest, "\"id\": \"irc-governs\",", "\"id\": \"basis\",", "caveats[1].id: 'basis' is used twice")]
    [InlineData(Manifest, "\"caveats\": [\n    {", "\"caveats\": [\n    42, {", "caveats[0]: must be a JSON object")]
    [InlineData(Manifest, "\"input\": \"supports\",", "\"input\": \"species\",", "a guide's scope has a limit on 'supports'")]
    [InlineData(Manifest, "\"input\": \"supports\",", "\"input\": \"porch\",", "'porch' is not an input a scope limit can test")]
    [InlineData(Manifest, "\"above\": 77", "\"above\": 77, \"equals\": \"x\"", "scope.limits[1].when: exactly one of above, equals, in, notIn, aboveInput")]
    [InlineData(Manifest, "\"input\": \"groundSnowLoad\",", "\"input\": \"species\",", "'species' is a category, not a number or a length")]
    [InlineData(Manifest, "\"above\": 77", "\"above\": 77.0", "must be a whole number")]
    [InlineData(Manifest, "\"input\": \"deckLength\",", "\"input\": \"groundSnowLoad\",", "compares two lengths, and 'groundSnowLoad' is not a length")]
    [InlineData(Manifest, "\"aboveInput\": \"deckWidth\"", "\"aboveInput\": \"deckLength\"", "'deckLength' is not another length input")]
    [InlineData(Manifest, "\"aboveInput\": \"deckWidth\"", "\"aboveInput\": 5", "scope.limits[2].when.aboveInput: must be a string")]
    [InlineData(Manifest, "\"input\": \"supports\",", "\"input\": \"groundSnowLoad\",", "'groundSnowLoad' is a psf, not a category; test it with above")]
    [InlineData(Manifest, "\"when\": {\n          \"input\": \"groundSnowLoad\",\n          \"above\": 77\n        },", "", "scope.limits[1].when: missing required field")]
    [InlineData(Manifest, "\"aboveInput\": \"deckWidth\"", "\"aboveInput\": \"groundSnowLoad\"", "'groundSnowLoad' is not another length input")]
    [InlineData(Manifest, "\"notIn\": [\n            \"zz-deck\"\n          ]", "\"notIn\": [ \"zz-deck\", \"zz-deck\" ]", "'zz-deck' is listed twice")]
    [InlineData(Manifest, "\"notIn\": [\n            \"zz-deck\"\n          ]", "\"notIn\": []", "must have at least 1 item")]
    [InlineData(Manifest, "\"above\": 77", "\"above\": 77, \"below\": 1", "scope.limits[1].when.below: unknown field")]
    [InlineData(Manifest, "\"text\": \"SYNTHETIC: covers a deck carrying only its own loads.\",", "", "scope.limits[0].text: missing required field")]
    [InlineData(Manifest, "\"id\": \"s.snow\",", "\"id\": \"s.loads\",", "'s.loads' is used twice; every limit and note has its own id")]
    [InlineData(Manifest, "\"id\": \"n.single\",", "\"id\": \"s.snow\",", "'s.snow' is used twice; every limit and note has its own id")]
    [InlineData(Manifest, "\"limits\": [\n      {", "\"limits\": [\n      42, {", "scope.limits[0]: must be a JSON object")]
    [InlineData(Manifest, "\"notes\": [\n      {", "\"notes\": [\n      42, {", "scope.notes[0]: must be a JSON object")]
    [InlineData(Manifest, "\"location\": \"synthetic guide p. 2, item 1\"", "\"place\": \"synthetic guide p. 2, item 1\"", "scope.notes[0].location: missing required field")]
    [InlineData(Manifest, "\"species\": [\n    \"zz-fir\",", "\"species\": [\n    \"zz-hem\",", "species[1]: 'zz-hem' is listed twice")]
    [InlineData(Joist, "\"source\": \"zz-synth-guide\"", "\"source\": \"zz-synth-base\"", "zz-synth-base")]
    [InlineData(Joist, "\"group\": \"zz-cedar\",", "\"group\": \"zz-fir, zz-hem\",", "speciesGroups[1].group: 'zz-fir, zz-hem' is declared twice")]
    [InlineData(Joist, "\"group\": \"zz-cedar\",", "\"group\": \"zz-larch\",", "group 'zz-larch' is not one of the species column's values")]
    [InlineData(Joist, "\"group\": \"zz-cedar\",", "\"group\": \"zz-larch\",", "the species column's value 'zz-cedar' is not one of the table's groups")]
    [InlineData(Joist, "\"species\": [\n        \"zz-cedar\"\n      ]", "\"species\": [\n        \"zz-cedar\", \"zz-hem\"\n      ]", "'zz-hem' is placed in 'zz-fir, zz-hem' and 'zz-cedar'")]
    [InlineData(Joist, "\"species\": [\n        \"zz-cedar\"\n      ]", "\"species\": [\n        \"zz-cedar\", \"zz-oak\"\n      ]", "'zz-oak' is not one of guide 'zz-guide-2099''s species")]
    [InlineData(Joist, "\"species\": [\n        \"zz-cedar\"\n      ]", "\"species\": [\n        \"zz-pine\"\n      ]", "guide 'zz-guide-2099' covers 'zz-cedar' but no group of this table places it")]
    [InlineData(Joist, "\"speciesGroups\": [\n    {", "\"speciesGroups\": [\n    42, {", "speciesGroups[0]: must be a JSON object")]
    [InlineData(Joist, "\"location\": \"synthetic guide p. 3, second row heading\"", "\"place\": \"x\"", "speciesGroups[1].location: missing required field")]
    [InlineData(Joist, "\"id\": \"t.member\",", "\"id\": \"s.snow\",", "'s.snow' is used twice")]
    public void A_malformed_guide_is_refused_naming_its_fault(string file, string original, string replacement, string message)
        => Assert.Contains(message, Edited(file, original, replacement), StringComparison.Ordinal);

    [Fact]
    public void A_guide_table_with_a_species_column_must_print_its_groups()
    {
        InMemoryPackSource source = Deck();
        string text = source.Text(Joist);
        int start = text.IndexOf("\"speciesGroups\"", StringComparison.Ordinal);
        int end = text.IndexOf("\"limits\"", StringComparison.Ordinal);
        Assert.Contains("declares the species groups it prints", Refused(source.With(Joist, text[..start] + text[end..])), StringComparison.Ordinal);
    }

    [Fact]
    public void Species_groups_on_a_table_without_a_species_column_are_refused_and_a_base_table_may_group_species()
    {
        InMemoryPackSource ledger = Deck();
        string file = "layers/zz-deck-2099/deck/zz-deck-ledger.json";
        ledger.With(file, ledger.Text(file).Replace("\"footnotes\": [\n    {", "\"speciesGroups\": [ { \"group\": \"g\", \"species\": [\"zz-fir\"], \"location\": \"x\" } ],\n  \"footnotes\": [\n    {", StringComparison.Ordinal));
        Assert.Contains("speciesGroups: the table has no 'species' column to group.", Refused(ledger), StringComparison.Ordinal);

        // A base layer's beam table may group its species; without a guide, no guide list applies.
        InMemoryPackSource beam = Deck();
        string beamFile = "layers/zz-deck-2099/deck/zz-deck-beam.json";
        beam.With(beamFile, beam.Text(beamFile).Replace("\"footnotes\": [", "\"speciesGroups\": [ { \"group\": \"zz-fir\", \"species\": [\"zz-fir\", \"zz-spruce\"], \"location\": \"x\" } ],\n  \"footnotes\": [", StringComparison.Ordinal));
        DeckTable grouped = Fx.Loaded(PackLoader.Load(beam, "us-zz-deck")).Deck.Spans[SpanUse.DeckBeam];
        Assert.Equal(["zz-fir", "zz-spruce"], Assert.Single(grouped.SpeciesGroups).Species);
    }

    [Fact]
    public void A_guide_without_deck_files_or_a_manifest_or_named_as_the_base_layer_is_refused()
    {
        Assert.Contains("guide 'zz-guide-2099' has no files under deck/", Refused(Deck().Without(Joist)), StringComparison.Ordinal);
        Assert.Contains("file not found", Refused(Deck().Without(Manifest)), StringComparison.Ordinal);
        Assert.NotEmpty(Refused(Deck().With(Manifest, "{ not json")));

        // A guide named in layers: its layer.json says it is a guide.
        InMemoryPackSource asBase = Deck();
        asBase.With(Pack, asBase.Text(Pack).Replace("\"zz-deck-2099\"\n  ],", "\"zz-guide-2099\"\n  ],", StringComparison.Ordinal));
        Assert.Contains("a guide layer is never a pack's base layer; list it under the pack's 'guides'", Refused(asBase), StringComparison.Ordinal);

        // Any other kind on a base layer is refused too, and a base layer carries no guide fields.
        string layer = "layers/zz-deck-2099/layer.json";
        Assert.Contains("kind: 'base' is not a layer kind", Edited(layer, "\"id\": \"zz-deck-2099\",", "\"kind\": \"base\", \"id\": \"zz-deck-2099\","), StringComparison.Ordinal);
        Assert.Contains("scope: unknown field", Edited(layer, "\"id\": \"zz-deck-2099\",", "\"scope\": {}, \"id\": \"zz-deck-2099\","), StringComparison.Ordinal);
    }

    [Fact]
    public void A_kind_both_the_base_layer_and_a_guide_declare_is_refused_naming_both_files()
    {
        InMemoryPackSource joists = Deck();
        joists.With("layers/zz-deck-2099/deck/zz-deck-joist.json", joists.Text(Joist).Replace("zz-synth-guide", "zz-synth-base", StringComparison.Ordinal).Replace("ZZ-GUIDE", "ZZ-DECK", StringComparison.Ordinal));
        string said = Refused(joists);
        Assert.Contains(
            $"layers/zz-deck-2099/deck/zz-deck-joist.json and {Joist} both declare a member-span table for 'deck-joist': a pack's layers may not both carry it, and neither wins",
            said,
            StringComparison.Ordinal);

        foreach ((string file, string what) in new[] { ("zz-deck-ledger.json", "a deck-ledger table"), ("zz-deck-footing.json", "a deck-footing table"), ("zz-guard-stair.json", "a deck-guard-stair file") })
        {
            InMemoryPackSource twice = Deck();
            twice.With($"layers/zz-guide-2099/deck/{file}", twice.Text($"layers/zz-deck-2099/deck/{file}").Replace("zz-synth-base", "zz-synth-guide", StringComparison.Ordinal));
            Assert.Contains($"layers/zz-deck-2099/deck/{file} and layers/zz-guide-2099/deck/{file} both declare {what}", Refused(twice), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_guard_and_stair_file_under_a_guide_carries_it()
    {
        InMemoryPackSource source = Deck();
        string file = "zz-guard-stair.json";
        source.With($"layers/zz-guide-2099/deck/{file}", source.Text($"layers/zz-deck-2099/deck/{file}").Replace("zz-synth-base", "zz-synth-guide", StringComparison.Ordinal))
            .Without($"layers/zz-deck-2099/deck/{file}");
        GuardStairProvisions provisions = Fx.Loaded(PackLoader.Load(source, "us-zz-deck")).Deck.GuardStair!;
        Assert.Equal("zz-guide-2099", provisions.Guide!.Id);
        Assert.Equal("layers/zz-guide-2099/deck/zz-guard-stair.json", provisions.File);
    }
}

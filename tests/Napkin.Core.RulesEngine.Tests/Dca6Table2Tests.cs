using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// DCA 6-2015 under the shipped Connecticut pack (#41 slice B1, docs/design/deck-guide-pack.md §3.1): the
/// guide's identity, Table 2 (p. 4) and the cantilever check against the lesser of a row's overhang and a
/// quarter of the span. Expected values were typed from the DCA 6 PDF (sha256 205d57b5…3009e, read
/// 2026-09-27), never copied out of the pack; the full row-by-row check is the golden file
/// packs/golden/us-ct-2022/dca6-table-2.golden.json. The overhang schema's refusals are proven on in-memory
/// edits of the real pack and of the SYNTHETIC pack us-zz-deck (NOT CODE VALUES).
/// </summary>
public class Dca6Table2Tests
{
    const string Table = "layers/dca6-2015/deck/table-2.json";

    static LoadedPack Ct() => Fx.Loaded(PackLoader.Load(Fx.RealRoot, "us-ct-2022"));

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static Length Ft(long feet, long inches = 0) => Length.FeetInches(feet, inches);

    /// <summary>Joists under DCA 6's scope: a deck carrying only itself, 30 psf of snow, 12'-0" out from the house and 16'-0" along it.</summary>
    static SpanRequest Joists(Length span, string species = "Southern Pine", string member = "2x8", long spacing = 16, string supports = "deck")
        => new(member, span, supports, species, In(spacing), null, GroundSnowLoad: 30, DeckLength: Ft(12), DeckWidth: Ft(16));

    static string Refused(InMemoryPackSource source, string pack = "us-ct-2022")
        => string.Join("\n", Fx.Invalid(PackLoader.Load(source, pack)).Problems.Select(problem => problem.Message));

    static InMemoryPackSource Edit(string file, string original, string replacement, string root = "")
    {
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(root.Length == 0 ? Fx.RealRoot : root);
        string text = source.Text(file);
        Assert.Contains(original, text, StringComparison.Ordinal);
        return source.With(file, text.Replace(original, replacement, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void The_guide_is_DCA_6_on_the_2015_IRC_with_its_caveats_scope_and_eight_species()
    {
        DeckGuide guide = Assert.Single(Ct().Guides);

        Assert.Equal(("dca6-2015", "DCA 6-2015", "2015 IRC", "American Wood Council"), (guide.Id, guide.ShortName, guide.BasisText, guide.Publisher));

        // p. 1: the cover's subtitle and the sentence that the IRC governs.
        Assert.Equal(
            [("basis", "Based on the 2015 International Residential Code"), ("irc-governs", "Where differences exist between provisions of this document and the IRC, provisions of the IRC shall apply.")],
            guide.Caveats.Select(caveat => (caveat.Id, caveat.Text)));

        // p. 2, items 8, 9 and 2: only the deck's own loads, snow not over 40 psf, length no more than width.
        Assert.Equal(["s.loads", "s.snow", "s.shape"], guide.Limits.Select(limit => limit.Id));
        Assert.Equal((ScopeForm.NotIn, "deck"), (guide.Limits[0].When.Form, Assert.Single(guide.Limits[0].When.Values)));
        Assert.Equal((ScopeForm.Above, CellValue.Whole(ColumnType.Psf, 40)), (guide.Limits[1].When.Form, guide.Limits[1].When.Value!.Value));
        Assert.Equal((ScopeForm.AboveInput, "deckLength", "deckWidth"), (guide.Limits[2].When.Form, guide.Limits[2].When.Input, guide.Limits[2].When.OtherInput));
        Assert.Equal(["n.single", "n.stairs", "n.post-size", "n.materials", "n.beam-span", "n.beam-sides"], guide.Notes.Select(note => note.Id));

        // Item 3, p. 2, shown once at the top of the block; Tables B1 and B2 enforce it, citing p. 10 (#42).
        Assert.Equal("Minimum post size is 6x6 nominal and maximum post height shall be in accordance with Table 4.", guide.Notes[2].Text);
        Assert.Contains("\"Minimum post size is 6x6 nominal and maximum post height shall be in accordance with Table 4.\" (MINIMUM REQUIREMENTS & LIMITATIONS item 3, p. 2)", guide.Paragraph(Ct().Code), StringComparison.Ordinal);

        // Table 1, p. 3 and Table 2, p. 4 (Table 1 abbreviates Spruce-Pine-Fir as SPF).
        Assert.Equal(["Southern Pine", "Douglas Fir-Larch", "Hem-Fir", "Spruce-Pine-Fir", "Redwood", "Western Cedars", "Ponderosa Pine", "Red Pine"], guide.Species);
        SourceDocument source = Assert.Single(guide.Sources);
        Assert.Equal("205d57b515e22aeed6b2c5c5eecd02967dbb98aa40b6f841d25fcec119b3009e", source.Sha256);

        Assert.Equal("a guide on the 2015 IRC, not CT 2022's adopted IRC 2021; the IRC governs where they differ (p. 1)", guide.Clause(Ct().Code));
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void Table_2_prints_36_rows_in_three_species_groups_with_a_quarter_span_cap_on_the_overhang()
    {
        DeckTable table = Ct().Deck.Spans[SpanUse.DeckJoist];

        Assert.Equal(("2", "Maximum Joist Spans and Overhangs.", "p. 4"), (table.Designation, table.Title, table.Source.Location));
        Assert.Equal(36, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.NotNull(row.Overhang));
        Assert.Equal(new ExactFraction(1, 4), table.OverhangLimit!.Fraction);
        Assert.Equal(["Southern Pine", "Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir", "Redwood, Western Cedars, Ponderosa Pine, Red Pine"], table.SpeciesGroups.Select(group => group.Group));
        Assert.Equal(["1", "2", "3", "4", "5", "6", "7"], table.Footnotes.Select(note => note.Id));
        Assert.All(table.Footnotes, note => Assert.Equal(FootnoteEncoding.NotEncoded, note.EncodedAs));
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void The_worked_example_passes_and_a_typed_species_is_read_as_its_printed_group()
    {
        // The note's example: 2x8 at 16" o.c., Southern Pine, 9'-9": Table 2 allows 11'-10" (p. 4).
        DeckResult.Passes passes = Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckSpan(Ct(), SpanUse.DeckJoist, Joists(Ft(9, 9))));
        Assert.Equal(("r.sp.2x8.16", Ft(11, 10), "Southern Pine"), (passes.Row.Id, passes.Allowed, passes.Group!.Group));

        // Hem-Fir 2x8 at 16": the Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir row, 11'-1".
        DeckResult.Short hem = Assert.IsType<DeckResult.Short>(DeckEvaluator.CheckSpan(Ct(), SpanUse.DeckJoist, Joists(Ft(11, 4), "Hem-Fir")));
        Assert.Equal((Ft(11, 1), In(3), "Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir"), (hem.Allowed, hem.Over, hem.Group!.Group));

        // A porch roof on the deck is out of the guide's scope before any lookup (item 8, p. 2).
        DeckResult.OutOfScope porch = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckSpan(Ct(), SpanUse.DeckJoist, Joists(Ft(9, 9), supports: "porch-roof")));
        Assert.Equal("s.loads", porch.Limit!.Id);
        Assert.StartsWith("Beyond the scope of DCA 6-2015: \"Assumes 40 psf live load", porch.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void A_cantilever_is_allowed_the_lesser_of_the_rows_overhang_and_a_quarter_of_the_span()
    {
        // Southern Pine 2x8 at 16": L_O 2'-0" (p. 4). On 9'-9" of span L/4 is 2'-5 1/4", so the row's 2'-0" governs.
        DeckResult.Passes row = Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckCantilever(Ct(), Joists(Ft(9, 9)), Ft(2)));
        Assert.Equal((Ft(2), Ft(2), "r.sp.2x8.16"), (row.Allowed, row.Actual, row.Row.Id));

        // On 7'-0" of span L/4 is 1'-9", less than the row's 2'-0": a 1'-10" cantilever is 1" over.
        DeckResult.Short quarter = Assert.IsType<DeckResult.Short>(DeckEvaluator.CheckCantilever(Ct(), Joists(Ft(7)), Ft(1, 10)));
        Assert.Equal((Ft(1, 9), In(1)), (quarter.Allowed, quarter.Over));

        // Exact, never rounded up: on 5'-0 1/1024" of span L/4 is 15 1/4096", so 15" passes and 15 1/1024" does not.
        Length span = Ft(5) + new Length(1);
        Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckCantilever(Ct(), Joists(span), In(15)));
        Assert.Equal(In(15), Assert.IsType<DeckResult.Short>(DeckEvaluator.CheckCantilever(Ct(), Joists(span), In(15) + new Length(1))).Allowed);
        Assert.Equal(In(15), DeckEvaluator.AllowedOverhang(Ct().Deck.Spans[SpanUse.DeckJoist], row.Row, span));

        // The scope and the lookup are the span check's.
        Assert.Equal("s.loads", Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckCantilever(Ct(), Joists(Ft(9, 9), supports: "porch-roof"), Ft(1))).Limit!.Id);
        Assert.Equal("member", Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckCantilever(Ct(), Joists(Ft(9, 9), member: "2x14"), Ft(1))).Column);
        Assert.StartsWith("No adopted code is chosen", Assert.IsType<DeckResult.NoData>(DeckEvaluator.CheckCantilever(null, Joists(Ft(9, 9)), Ft(1))).Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void A_joist_table_that_prints_no_overhang_does_not_cover_a_cantilever()
    {
        LoadedPack synthetic = Fx.Loaded(PackLoader.Load(Fx.DeckRoot, "us-zz-deck"));
        SpanRequest joists = new("2x8", In(117), "zz-deck", "zz-fir", In(16), null, GroundSnowLoad: 30, DeckLength: In(120), DeckWidth: In(144));

        DeckResult.OutOfScope stopped = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckCantilever(synthetic, joists, In(18)));
        Assert.Equal("cantilever", stopped.Column);
        Assert.Null(stopped.Limit);
        Assert.Equal("Table ZZ-GUIDE-JOIST does not cover an overhang, so a cantilever of 1'-6\" past the beam is not checked: get it engineered.", stopped.Explanation);

        DeckTable table = synthetic.Deck.Spans[SpanUse.DeckJoist];
        Assert.Throws<ArgumentException>(() => DeckEvaluator.AllowedOverhang(table, table.Rows[0], In(117)));
    }

    [Theory]
    [InlineData("\"overhangLimit\": {\n    \"fraction\": \"1/4\",", "\"overhangLimit\": {\n    \"fraction\": \"5/4\",", "'5/4' is not a positive exact fraction of the span")]
    [InlineData("\"overhangLimit\": {\n    \"fraction\": \"1/4\",", "\"overhangLimit\": {\n    \"fraction\": \"0.25\",", "'0.25' is not a positive exact fraction of the span")]
    [InlineData("\"of\": \"span\"", "\"of\": \"length\"", "'length': an overhang is capped as a fraction of the joist 'span'")]
    [InlineData("\"span\": \"9ft 11in\", \"overhang\": \"1ft 0in\", ", "\"span\": \"9ft 11in\", ", "rows r.sp.2x6.12: a table with an overhangLimit gives every row its overhang")]
    [InlineData("\"span\": \"9ft 11in\", \"overhang\": \"1ft 0in\", ", "\"span\": \"9ft 11in\", \"overhang\": \"0in\", ", "overhang: must be longer than zero")]
    [InlineData("\"overhangLimit\": {", "\"overhangCap\": {", "the rows carry an overhang, so the table declares the cap on it")]
    public void A_malformed_overhang_is_refused_naming_its_fault(string original, string replacement, string message)
        => Assert.Contains(message, Refused(Edit(Table, original, replacement)), StringComparison.Ordinal);

    [Fact]
    public void Only_a_deck_joist_table_carries_an_overhang()
    {
        const string beam = "layers/zz-deck-2099/deck/zz-deck-beam.json";
        Assert.Contains(
            "overhangLimit: only a deck-joist table's rows carry an overhang",
            Refused(Edit(beam, "\"location\": \"synthetic p. 2\",", "\"location\": \"synthetic p. 2\", \"overhangLimit\": { \"fraction\": \"1/4\", \"of\": \"span\", \"location\": \"x\" },", Fx.DeckRoot), "us-zz-deck"),
            StringComparison.Ordinal);
        Assert.Contains(
            "overhang: only a deck-joist row carries an overhang",
            Refused(Edit(beam, "\"span\": \"4ft 3in\"", "\"span\": \"4ft 3in\", \"overhang\": \"1ft 0in\"", Fx.DeckRoot), "us-zz-deck"),
            StringComparison.Ordinal);
        Assert.Contains(
            "overhang: unknown field",
            Refused(Edit("layers/zz-deck-2099/deck/zz-deck-ledger.json", "\"spacing\": \"17in\"", "\"spacing\": \"17in\", \"overhang\": \"1ft 0in\"", Fx.DeckRoot), "us-zz-deck"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_golden_file_must_prove_each_rows_overhang_where_it_governs_and_asks_a_cantilever_only_of_joists()
    {
        string golden = File.ReadAllText(Path.Combine(Fx.RealRoot, "golden", "us-ct-2022", "dca6-table-2.golden.json"));

        // Southern Pine 2x8 at 12": asked on 13'-1" the row's 1'-10" governs; asked on 7'-0", L/4 = 1'-9" does, and L_O is unproven.
        const string proven = "\"member\": \"2x8\", \"spacing\": \"12in\", \"span\": \"13ft 1in\", \"cantilever\": \"1ft 10in\" },\n      \"expect\": { \"passes\": { \"allowed\": \"1ft 10in\" } }";
        const string unproven = "\"member\": \"2x8\", \"spacing\": \"12in\", \"span\": \"7ft 0in\", \"cantilever\": \"1ft 9in\" },\n      \"expect\": { \"passes\": { \"allowed\": \"1ft 9in\" } }";
        Assert.Contains(proven, golden, StringComparison.Ordinal);
        GoldenFileResult result = GoldenRunner.Run(PackLoader.Load(Fx.RealRoot, "us-ct-2022"), golden.Replace(proven, unproven, StringComparison.Ordinal), "edited.golden.json");
        Assert.Contains(result.Problems, problem => problem.Contains("row 'r.sp.2x8.12' of table 2 has no hand-authored cantilever case expecting its own overhang (1'-10\")", StringComparison.Ordinal));

        string beam = File.ReadAllText(Path.Combine(Fx.DeckRoot, "golden", "us-zz-deck", "zz-deck-beam.golden.json"));
        int at = beam.IndexOf("\"joistSpan\"", StringComparison.Ordinal);
        GoldenFileResult asked = GoldenRunner.Run(PackLoader.Load(Fx.DeckRoot, "us-zz-deck"), beam[..at] + "\"cantilever\": \"1ft 0in\", " + beam[at..], "edited.golden.json");
        Assert.Contains("only a deck-joist table's case asks about a cantilever", asked.ToString(), StringComparison.Ordinal);
    }
}

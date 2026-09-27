namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// The deck golden runner (#238, deck-guide-pack §4 item 4) on the SYNTHETIC pack us-zz-deck (NOT CODE
/// VALUES): a wrong answer, an uncovered row or limit, an orphan, a stale boundary pair and a malformed case
/// each fail the file and say why.
/// </summary>
public class DeckGoldenTests
{
    static string Golden(string name) => File.ReadAllText(Path.Combine(Fx.DeckRoot, "golden", "us-zz-deck", name));

    static PackLoadResult Pack => PackLoader.Load(Fx.DeckRoot, "us-zz-deck");

    static GoldenFileResult Run(string json) => GoldenRunner.Run(Pack, json, "edited.golden.json");

    static string Edited(string name, string original, string replacement)
    {
        string json = Golden(name);
        Assert.Contains(original, json, StringComparison.Ordinal);
        return json.Replace(original, replacement, StringComparison.Ordinal);
    }

    /// <summary>The file with only its first hand-authored case and the given replacement for it.</summary>
    static string OneCase(string name, string caseJson)
    {
        string json = Golden(name);
        int start = json.IndexOf("\"cases\": [", StringComparison.Ordinal);
        return json[..start] + $"\"cases\": [ {caseJson} ] }}";
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void Every_synthetic_deck_golden_file_passes_and_its_boundaries_are_the_generators()
    {
        foreach (string name in new[] { "zz-guide-joist.golden.json", "zz-deck-beam.golden.json", "zz-rafter.golden.json", "zz-deck-ledger.golden.json", "zz-deck-footing.golden.json", "zz-deck-post-corner.golden.json", "zz-deck-post-center.golden.json" })
        {
            GoldenFileResult result = Run(Golden(name));
            Assert.True(result.Passed, result.ToString());
            int generated = Golden(name).Split('\n').Count(line => line.Contains("\"generated\":\"boundary\"", StringComparison.Ordinal));
            Assert.Equal(generated, GoldenRunner.DeckBoundaries(((PackLoadResult.Loaded)Pack).Pack, Golden(name)).Count);
        }
    }

    [Theory]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"passes\": { \"allowed\": \"12ft 7in\" } }", "\"expect\": { \"passes\": { \"allowed\": \"12ft 8in\" } }", "expected allowed 12'-8\"; got 12'-7\"")]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"short\": { \"allowed\": \"15ft 2in\", \"over\": \"4in\" } }", "\"expect\": { \"short\": { \"allowed\": \"15ft 2in\", \"over\": \"5in\" } }", "over by 5\"; got 15'-2\", over by 4\"")]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"short\": { \"allowed\": \"15ft 2in\", \"over\": \"4in\" } }", "\"expect\": { \"passes\": { \"allowed\": \"15ft 2in\" } }", "expected passes from row 'r.fir.2x10.12'; got Short")]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"passes\": { \"allowed\": \"12ft 7in\" } }", "\"expect\": { \"short\": { \"allowed\": \"12ft 7in\", \"over\": \"1in\" } }", "expected short from row 'r.fir.2x8.12'; got Passes")]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"outOfScope\": { \"limit\": \"s.snow\" } }", "\"expect\": { \"outOfScope\": { \"limit\": \"s.shape\" } }", "out of scope for s.snow, expected s.shape")]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"outOfScope\": { \"column\": \"spacing\" } }", "\"expect\": { \"inputMissing\": { \"input\": \"spacing\" } }", "expected spacing missing; got OutOfScope")]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"inputMissing\": { \"input\": \"species\" } }", "\"expect\": { \"inputMissing\": { \"input\": \"supports\" } }", "expected supports missing; got species missing")]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"inputMissing\": { \"input\": \"species\" } }", "\"expect\": { \"noData\": {} }", "expected no data; got InputMissing")]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"outOfScope\": { \"column\": \"spacing\" } }", "\"expect\": { \"outOfScope\": { \"limit\": \"s.loads\" } }", "out of scope for spacing, expected s.loads")]
    [InlineData("zz-deck-ledger.golden.json", "\"spacing\": \"17in\", \"count\": 10", "\"spacing\": \"17in\", \"count\": 11", "expected zz-bolts, staggered, 1'-5\", 11; got zz-bolts, staggered, 1'-5\", 10")]
    [InlineData("zz-deck-ledger.golden.json", "\"expect\": { \"outOfScope\": { \"column\": \"member\" } }", "\"expect\": { \"sized\": { \"text\": \"zz-bolts\" } }", "a sized case names the row it expects")]
    [InlineData("zz-deck-footing.golden.json", "\"row\": \"r.20.2000\", \"location\"", "\"row\": \"r.20.1500\", \"location\"", "sized, but from row 'r.20.2000', expected 'r.20.1500'")]
    [InlineData("zz-deck-footing.golden.json", "\"expect\": { \"sized\": { \"round\": \"16in\", \"square\": \"14in\", \"thickness\": \"6in\" } }", "\"expect\": { \"sized\": { \"round\": \"16in\", \"square\": \"99in\", \"thickness\": \"6in\" } }", "expected 1ft 4in round, 8ft 3in square, 6in thick; got 1ft 4in round, 1ft 2in square, 6in thick")]
    [InlineData("zz-deck-post-corner.golden.json", "\"expect\": { \"outOfScope\": { \"notPermitted\": true } }", "\"expect\": { \"passes\": { \"allowed\": \"1ft 0in\" } }", "expected passes from row 'r.cedar.4x4.80'; got OutOfScope")]
    [InlineData("zz-deck-post-corner.golden.json", "{ \"row\": \"r.cedar.4x4.80\", \"location\"", "{ \"row\": \"r.cedar.4x4.40\", \"location\"", "NP, but from row 'r.cedar.4x4.80', expected 'r.cedar.4x4.40'")]
    [InlineData("zz-deck-post-corner.golden.json", "\"expect\": { \"outOfScope\": { \"column\": \"post\" } }", "\"expect\": { \"outOfScope\": { \"column\": \"species\" } }", "out of scope for post, expected species")]
    [InlineData("zz-deck-post-corner.golden.json", "\"expect\": { \"short\": { \"allowed\": \"4ft 0in\", \"over\": \"1in\" } }", "\"expect\": { \"outOfScope\": { \"notPermitted\": true } }", "expected out of scope, row 'r.fir.4x4.40' printing NP; got Short")]
    [InlineData("zz-deck-footing.golden.json", "\"expect\": { \"inputMissing\": { \"input\": \"soilBearing\" } }", "\"expect\": { \"sized\": { \"text\": \"zz 14 in square\" } }", "a sized case names the row it expects")]
    public void A_wrong_expectation_fails_and_says_why(string name, string original, string replacement, string detail)
        => Assert.Contains(detail, Run(Edited(name, original, replacement)).ToString(), StringComparison.Ordinal);

    [Fact]
    public void A_right_answer_from_the_wrong_row_fails()
    {
        string json = Edited("zz-guide-joist.golden.json", "{ \"row\": \"r.fir.2x10.16\", \"location\"", "{ \"row\": \"r.fir.2x10.12\", \"location\"");
        Assert.Contains("passes, but from row 'r.fir.2x10.16', expected 'r.fir.2x10.12'", Run(json).ToString(), StringComparison.Ordinal);
        string beam = Edited("zz-deck-beam.golden.json", "{ \"row\": \"r.2-2x8.16\", \"location\"", "{ \"row\": \"r.2-2x8.10\", \"location\"");
        Assert.Contains("short, but from row 'r.2-2x8.16', expected 'r.2-2x8.10'", Run(beam).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_uncovered_row_or_limit_an_orphan_and_a_missing_table_fail_the_file()
    {
        GoldenFileResult renamed = Run(Edited("zz-guide-joist.golden.json", "{ \"row\": \"r.cedar.2x8.12\", \"location\"", "{ \"row\": \"r.cedar.renamed\", \"location\""));
        Assert.Contains(renamed.Problems, p => p.Contains("row 'r.cedar.2x8.12' of table ZZ-GUIDE-JOIST has no hand-authored golden case", StringComparison.Ordinal));
        Assert.Contains(renamed.Problems, p => p.Contains("row 'r.cedar.renamed' is not in table ZZ-GUIDE-JOIST", StringComparison.Ordinal));

        GoldenFileResult limit = Run(Edited("zz-guide-joist.golden.json", "\"expect\": { \"outOfScope\": { \"limit\": \"t.member\" } }", "\"expect\": { \"outOfScope\": { \"limit\": \"t.other\" } }"));
        Assert.Contains(limit.Problems, p => p.Contains("scope limit 't.member' of table ZZ-GUIDE-JOIST is expected by no hand-authored golden case", StringComparison.Ordinal));
        Assert.Contains(limit.Problems, p => p.Contains("'t.other' is not a scope limit of table ZZ-GUIDE-JOIST or its guide", StringComparison.Ordinal));

        GoldenFileResult gone = Run(Edited("zz-deck-beam.golden.json", "\"deck\": \"ZZ-DECK-BEAM\"", "\"deck\": \"ZZ-DECK-GIRDER\""));
        Assert.Contains("deck table 'ZZ-DECK-GIRDER' is not in pack 'us-zz-deck'", Assert.Single(gone.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void A_centre_post_factor_that_no_hand_case_exercises_fails_the_file()
    {
        // Every hand case on a spliced beam: the table's factor is never applied, so the file does not prove it.
        string json = Golden("zz-deck-post-center.golden.json").Replace("\"continuousBeam\": true", "\"continuousBeam\": false", StringComparison.Ordinal);
        Assert.Contains(
            Run(json).Problems,
            problem => problem.Contains("the centre-post factor of table ZZ-DECK-POST-CENTER (note b) is exercised by no hand-authored golden case naming its row", StringComparison.Ordinal));
    }

    [Fact]
    public void A_stale_or_missing_boundary_pair_fails_the_file_and_the_generators_pairs_are_printed()
    {
        string json = Golden("zz-deck-ledger.golden.json");
        string stale = json.Replace("\"joistSpan\":\"12ft 0-1/1024in\",\"ledgerLength\":\"12ft 0in\"", "\"joistSpan\":\"12ft 0-1/1024in\",\"ledgerLength\":\"12ft 1in\"", StringComparison.Ordinal);
        GoldenFileResult result = Run(stale);
        string problem = Assert.Single(result.Problems);
        Assert.StartsWith("the committed boundary cases (8) are not the generator's (8); replace every \"generated\" case with these, in this order:", problem, StringComparison.Ordinal);
        Assert.Contains("{\"row\":\"r.2x8.16\",\"generated\":\"boundary\",\"inputs\":{\"member\":\"2x8\",\"joistSpan\":\"12ft 0-1/1024in\",\"ledgerLength\":\"12ft 0in\"}", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"passes\": { \"allowed\": \"12ft 7in\" } }", "\"expect\": { \"passes\": { \"allowed\": \"12ft 7in\" }, \"noData\": {} }", "expect: exactly one of passes, short, sized, outOfScope, inputMissing, noData")]
    [InlineData("zz-guide-joist.golden.json", "\"expect\": { \"outOfScope\": { \"limit\": \"s.loads\" } }", "\"expect\": { \"outOfScope\": { \"limit\": \"s.loads\", \"column\": \"supports\" } }", "exactly one of limit (a scope limit's id), column (the input no row covers) or notPermitted: true")]
    [InlineData("zz-guide-joist.golden.json", "\"span\": \"11ft 0in\" }", "\"span\": \"11ft 0in\", \"ledgerLength\": \"1in\" }", "ledgerLength: unknown field")]
    [InlineData("zz-guide-joist.golden.json", "\"span\": \"11ft 0in\" }", "\"spam\": \"11ft 0in\" }", "span: missing required field")]
    [InlineData("zz-deck-footing.golden.json", "\"tributaryArea\": 15,", "\"tributaryArea\": [\"1ft 0in\"],", "whole square feet, or two lengths whose product is the area")]
    [InlineData("zz-deck-footing.golden.json", "\"tributaryArea\": 15,", "\"tributaryArea\": \"15\",", "whole square feet, or two lengths whose product is the area")]
    [InlineData("zz-deck-footing.golden.json", "\"tributaryArea\": 15,", "\"tributaryArea\": 15.5,", "must be a whole number")]
    [InlineData("zz-deck-footing.golden.json", "\"tributaryArea\": 15,", "\"tributaryArea\": [\"x\", \"2ft 0in\"],", "is not a length")]
    [InlineData("zz-deck-ledger.golden.json", "\"joistSpan\": \"9ft 9in\", \"ledgerLength\": \"12ft 0in\" },\n      \"expect\": { \"sized\"", "\"joistSpan\": \"9ft 9in\" },\n      \"expect\": { \"sized\"", "ledgerLength: missing required field")]
    [InlineData("zz-deck-ledger.golden.json", "\"spacing\": \"17in\", \"count\": 10", "\"spacing\": \"17in\", \"count\": 0", "must be at least 1")]
    [InlineData("zz-deck-ledger.golden.json", "\"cases\": [\n    {", "\"cases\": [\n    42, {", "cases[0]: must be a JSON object")]
    [InlineData("zz-deck-ledger.golden.json", "\"inputs\": { \"member\": \"2x12\", \"joistSpan\": \"9ft 9in\", \"ledgerLength\": \"12ft 0in\" },", "", "inputs: missing required field")]
    [InlineData("zz-deck-ledger.golden.json", "\"expect\": { \"outOfScope\": { \"column\": \"member\" } }", "\"hope\": {}", "expect: missing required field")]
    [InlineData("zz-deck-ledger.golden.json", "\"expect\": { \"outOfScope\": { \"column\": \"member\" } }", "\"expect\": { \"outOfScope\": 5 }", "must be a JSON object")]
    [InlineData("zz-deck-footing.golden.json", "\"inputs\": { \"tributaryArea\": 20, \"position\": \"corner\" },", "\"inputs\": { \"soilBearing\": 20, \"position\": \"corner\" },", "tributaryArea: missing required field")]
    [InlineData("zz-deck-footing.golden.json", "\"inputs\": { \"tributaryArea\": 20, \"position\": \"corner\" },", "\"inputs\": { \"tributaryArea\": 20 },", "position: missing required field")]
    [InlineData("zz-deck-footing.golden.json", "\"inputs\": { \"tributaryArea\": 20, \"position\": \"corner\" },", "\"inputs\": { \"tributaryArea\": 20, \"position\": \"edge\" },", "'edge'")]
    [InlineData("zz-deck-post-center.golden.json", "\"tributaryArea\": 12, \"continuousBeam\": true,", "\"tributaryArea\": 12, \"continuousBeam\": \"yes\",", "continuousBeam: true or false")]
    [InlineData("zz-deck-post-corner.golden.json", "\"tributaryArea\": 40, \"height\": \"4ft 1in\" },", "\"tributaryArea\": 40 },", "height: missing required field")]
    [InlineData("zz-deck-post-corner.golden.json", "\"expect\": { \"outOfScope\": { \"notPermitted\": true } }", "\"expect\": { \"outOfScope\": { \"notPermitted\": false } }", "notPermitted: true, when the row's cell prints NP")]
    [InlineData("zz-deck-post-corner.golden.json", "\"expect\": { \"outOfScope\": { \"notPermitted\": true } }", "\"expect\": { \"outOfScope\": { \"notPermitted\": true, \"column\": \"post\" } }", "exactly one of limit")]
    [InlineData("zz-deck-post-corner.golden.json", "\"post\": \"8x8\", \"tributaryArea\": 20, \"height\": \"1ft 0in\" },\n      \"expect\": { \"outOfScope\": { \"column\": \"post\" } }", "\"post\": \"8x8\", \"tributaryArea\": 20, \"height\": \"1ft 0in\" },\n      \"expect\": { \"outOfScope\": { \"notPermitted\": true } }", "an outOfScope notPermitted case names the row it expects")]
    [InlineData("zz-deck-beam.golden.json", "\"expect\": { \"passes\": { \"allowed\": \"5ft 7in\" } }", "\"expect\": { \"passes\": {} }", "allowed: missing required field")]
    [InlineData("zz-deck-beam.golden.json", "\"expect\": { \"short\": { \"allowed\": \"4ft 3in\", \"over\": \"3in\" } }", "\"expect\": { \"short\": { \"allowed\": \"4ft 3in\" } }", "over: missing required field")]
    [InlineData("zz-deck-footing.golden.json", "\"expect\": { \"sized\": { \"round\": \"16in\", \"square\": \"14in\", \"thickness\": \"6in\" } }", "\"expect\": { \"sized\": {} }", "text: missing required field")]
    [InlineData("zz-deck-footing.golden.json", "\"expect\": { \"sized\": { \"round\": \"16in\", \"square\": \"14in\", \"thickness\": \"6in\" } }", "\"expect\": { \"sized\": { \"round\": \"16in\", \"square\": \"14in\" } }", "thickness: a footing case expects all three of round, square and thickness")]
    [InlineData("zz-deck-footing.golden.json", "\"expect\": { \"inputMissing\": { \"input\": \"soilBearing\" } }", "\"expect\": { \"inputMissing\": {} }", "input: missing required field")]
    public void A_malformed_case_is_a_problem_of_the_file(string name, string original, string replacement, string message)
        => Assert.Contains(message, Run(Edited(name, original, replacement)).ToString(), StringComparison.Ordinal);

    [Fact]
    public void A_case_with_no_limit_or_answer_generates_nothing_and_a_file_with_none_generates_no_limit_pairs()
    {
        // Only an out-of-scope case: no row is answered, so there is nothing to generate from.
        string json = OneCase(
            "zz-guide-joist.golden.json",
            "{ \"location\": \"x\", \"inputs\": { \"supports\": \"zz-porch\", \"species\": \"zz-fir\", \"groundSnowLoad\": 30, \"deckLength\": \"10ft 0in\", \"deckWidth\": \"12ft 0in\", \"member\": \"2x8\", \"spacing\": \"16in\", \"span\": \"1in\" }, \"expect\": { \"outOfScope\": { \"limit\": \"s.loads\" } } }");
        Assert.Empty(GoldenRunner.DeckBoundaries(((PackLoadResult.Loaded)Pack).Pack, json));
        Assert.Contains(Run(json).Problems, p => p.Contains("has no hand-authored golden case", StringComparison.Ordinal));
    }
}

using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>
/// The answer guard, one case per rule (docs/design/llm-assistant.md §4.2, §11.1 item 3): a number
/// in the answer stands only if the pack has it, in any form that folds to the same token.
/// </summary>
public class AnswerGuardTests
{
    private static ContextPack Pack(params string[] items) => ContextPack.Of(items.Select(text => (ContextKind.Check, text)));

    private static void Kept(string answer, ContextPack pack)
    {
        GuardedAnswer guarded = AnswerGuard.Check(answer, pack);
        Assert.True(guarded.Refused == 0, $"refused: {guarded.Text}");
        Assert.Equal(answer, guarded.Text);
    }

    private static void Refused(string answer, ContextPack pack, params string[] tokens)
    {
        GuardedSentence sentence = Assert.Single(AnswerGuard.Check(answer, pack).Sentences);
        Assert.False(sentence.Kept);
        Assert.Equal(tokens, sentence.Unsupported);
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void An_integer_stands_only_when_the_pack_has_it()
    {
        ContextPack pack = Pack("Header (2) 2x10, 1 jack, 2 king.");
        Kept("It has 2 king studs.", pack);
        Refused("It has 3 king studs.", pack, "3");
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void A_decimal_stands_only_as_the_same_value()
    {
        ContextPack pack = Pack("The wall is 3.5 in thick; the factor is 0.2 on this line.");
        Kept("It is 3.5 inches thick.", pack);
        Kept("That is 3 1/2\".", pack);
        Kept("The factor is 0.2.", pack);
        Refused("It is 3.25 inches thick.", pack, "3.25 inches");
        Refused("The factor is 0.2001.", pack, "0.2001");
        Refused("The factor is 0.20.", pack, "0.20");
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void A_fraction_stands_only_as_the_same_value()
    {
        ContextPack pack = Pack("Top — part, 3/4\" thick.");
        Kept("The top is 3/4 in thick.", pack);
        Kept("The top is .75\" thick.", pack);
        Kept("The top is ¾″ thick.", pack);
        Refused("The top is 5/8 in thick.", pack, "5/8 in");
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void Every_way_of_writing_one_length_is_one_token()
    {
        ContextPack pack = Pack("Wall 1 — wall, 4'-0\" long.");
        foreach (string length in new[] { "4'-0\"", "48\"", "48″", "48 in", "48 inches", "4 ft", "4 feet", "4-foot", "4′", "48" })
        {
            Kept($"It is {length} long.", pack);
        }

        Refused("It is 4'-6\" long.", pack, "4'-6\"");
        Refused("It is 5 ft long.", pack, "5 ft");
        Refused("It is 4 inches long.", pack, "4 inches");
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void A_lumber_name_stands_only_as_that_lumber_name()
    {
        ContextPack pack = Pack("Wall 1 — 2x4 studs, 2 plies, 6 in thick.");
        foreach (string lumber in new[] { "2x4", "2 x 4", "2×4", "2X4", "2x4s", "2x4's" })
        {
            Kept($"The studs are {lumber}.", pack);
        }

        // 2 and 6 are both in the pack; a 2x6 is not.
        Refused("A 2x6 would do.", pack, "2x6");
        Refused("A 5/4x6 board.", pack, "5/4x6");
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void A_table_or_section_stands_only_as_that_designation_in_any_case()
    {
        ContextPack pack = Pack("fill it from your own copy of the IRC (Tables R602.7(1)-(3), R602.3, R602.10.3 ...)");
        foreach (string designation in new[] { "R602.7(1)", "r602.7(1)", "Table R602.7(1)", "§R602.7(1)", "§ R602.10.3", "R602.3" })
        {
            Kept($"The header table is {designation}.", pack);
        }

        // The pack says "(1)-(3)"; it never names R602.7(3) itself, and a designation is not guessed.
        Refused("The header table is R602.7(3).", pack, "R602.7(3)");
        Refused("See §R507.2 for decks.", pack, "§R507.2");
        Refused("See Table R301.2.", pack, "R301.2");
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void A_percentage_stands_only_as_a_percentage()
    {
        ContextPack pack = Pack("Waste 15%, 15 boards.");
        Kept("About 15 percent is waste.", pack);
        Kept("About 15% is waste.", pack);
        Refused("About 20% is waste.", pack, "20%");
        Refused("About 2 percent is waste.", pack, "2 percent");
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void A_number_word_is_the_number_it_says()
    {
        ContextPack pack = Pack("Wall 1: 2 jack studs, 12 boards, 24 in, 40 rows.");
        Kept("One wall has two jack studs.", pack);
        Kept("A dozen boards.", pack);
        Kept("Twelve boards, twenty-four inches, forty rows.", pack);
        Refused("It needs three jack studs.", pack, "three");
        Refused("Buy eleven boards.", pack, "eleven");
        Refused("Twenty five rows.", pack, "Twenty five");

        // A word that only contains a number word is not one.
        Kept("Someone often weighs it.", pack);
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void A_reference_past_the_end_of_the_pack_is_refused()
    {
        ContextPack eight = Pack("a", "b", "c", "d", "e", "f", "g", "h");
        Kept("It is in the list [8].", eight);
        Kept("It is in the list [3, 8].", eight);

        GuardedSentence nine = Assert.Single(AnswerGuard.Check("It is in the list [9].", eight).Sentences);
        Assert.False(nine.Kept);
        Assert.Equal([9], nine.MissingReferences);
        Assert.Empty(nine.Unsupported);
        Assert.Equal("[one sentence refused: it referred to [9], which napkin did not give it]", nine.Shown);

        Assert.Equal([0], Assert.Single(AnswerGuard.Check("See [0].", eight).Sentences).MissingReferences);
        Assert.Equal([3, 9], Assert.Single(AnswerGuard.Check("See [3, 9].", eight).Sentences).References);
        Assert.Equal([int.MaxValue], Assert.Single(AnswerGuard.Check("See [99999999999].", eight).Sentences).MissingReferences);

        // A reference is not a number: "[8]" does not put an 8 in the pack or in the answer.
        Refused("There are 8 of them.", eight, "8");
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void An_answer_with_no_numbers_is_kept_whole()
    {
        const string answer = "No data means napkin has no table to look in.\nIt is not a fault in the window!\n\nAsk the building department?";
        GuardedAnswer guarded = AnswerGuard.Check(answer, Pack("nothing numeric"));
        Assert.Equal(0, guarded.Refused);
        Assert.Equal(3, guarded.Sentences.Length);
        Assert.Equal(answer, guarded.Text);
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void The_refusal_says_what_was_said_in_napkins_words()
    {
        Assert.Equal(
            "[one sentence refused: it said 5'-6″, which napkin did not give it]",
            AnswerGuard.Refusal(["5'-6″"], []));
        Assert.Equal(
            "[one sentence refused: it said 2x6 and 3, which napkin did not give it]",
            AnswerGuard.Refusal(["2x6", "3"], []));
        Assert.Equal(
            "[one sentence refused: it said 2x6, 3 and 4'-0\", which napkin did not give it]",
            AnswerGuard.Refusal(["2x6", "3", "4'-0\""], []));
        Assert.Equal(
            "[one sentence refused: it said 2x6 and referred to [9] and [10], which napkin did not give it]",
            AnswerGuard.Refusal(["2x6"], [9, 10]));

        GuardedAnswer guarded = AnswerGuard.Check("A 5'-6″ header. The window [1] is fine.", Pack("Window 1 — window."));
        Assert.Equal("[one sentence refused: it said 5'-6″, which napkin did not give it] The window [1] is fine.", guarded.Text);
        Assert.Equal(1, guarded.Refused);
        Assert.Equal([1], guarded.References);
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void One_refused_sentence_costs_only_itself()
    {
        ContextPack pack = Pack("Window 1 — window in Wall 1, 3'-0\" wide.", "Header check, Window 1: no table.");
        GuardedAnswer guarded = AnswerGuard.Check(
            "The window is 3'-0\" wide [1]. A 3-foot opening usually takes a (2) 2x6 header. There is no table [2].",
            pack);

        Assert.Equal([true, false, true], guarded.Sentences.Select(sentence => sentence.Kept));
        // The ply count is a number too: the pack has no 2 at all.
        Assert.Equal(["2", "2x6"], guarded.Sentences[1].Unsupported);
        Assert.Equal(
            "The window is 3'-0\" wide [1]. [one sentence refused: it said 2 and 2x6, which napkin did not give it] There is no table [2].",
            guarded.Text);

        // The items rendered under the answer are the kept sentences' references, in order.
        Assert.Equal([1, 2], guarded.References);
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void Every_digit_lands_in_a_token_so_nothing_numeric_slips_through()
    {
        ContextPack pack = Pack("Wall 1, 10 nails, dated 2026-09-25.");
        Refused("Use 10d nails.", pack, "10d");
        Refused("Legs1 is the name.", pack, "Legs1");
        Refused("That is 30psf.", pack, "30psf");
        Refused("The area is 12² feet.", pack, "12", "²");
        Refused("It is ２ inches.", pack, "２ inches");
        Kept("It was dated 2026-09-25.", pack);
        Kept("Wall 1's header.", pack);
        Kept("It is labelled \"Wall 1\".", pack);
    }

    [Fact]
    [Trait("Feature", "AST-003")]
    public void A_list_row_as_napkin_writes_it_to_csv_supports_its_sizes()
    {
        // The coffee table's cut list as its CSV export writes it: a length's own quote is doubled.
        CutListRow row = CutList.Of(Fixtures.Sample("coffee-table").Sketch, Napkin.Core.Materials.MaterialsLibrary.Shipped)
            .First(candidate => candidate.Length >= new Length(12 * Length.UnitsPerInch));
        string csv = CutListCsv.ToCsv([row]);
        Assert.Contains("\"\"\"", csv, StringComparison.Ordinal);

        ContextPack pack = ContextPack.Of(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => (ContextKind.ListRow, "Cut list: " + line)));
        string feetInches = row.Length.Format(LengthFormat.Default).Text;
        string inchesOnly = row.Length.Format(new InchesOnlyFormat(16)).Text;
        Assert.NotEqual(feetInches, inchesOnly);
        Kept($"Each is {feetInches} long [3].", pack);
        Kept($"Each is {inchesOnly} long.", pack);
        Refused($"Each is {(row.Length + new Length(512)).Format(LengthFormat.Default).Text} long.", pack, (row.Length + new Length(512)).Format(LengthFormat.Default).Text);
    }

    [Fact]
    public void Sentences_end_at_a_stop_before_white_space_or_a_line_break_and_references_after_a_stop_stay_with_it()
    {
        List<string> sentences = [.. AnswerGuard.Sentences("Table R602.7(1) is 3.5 in. It is empty. [4] [6]\nNext line? Yes!\"Quoted.\" Done").Select(s => s.Text)];
        Assert.Equal(["Table R602.7(1) is 3.5 in.", "It is empty. [4] [6]", "Next line?", "Yes!\"Quoted.\"", "Done"], sentences);

        Assert.Equal([4, 6], AnswerGuard.Check("It is empty. [4] [6]", Pack("1", "2", "3", "4", "5", "6")).References);
        Assert.Empty(AnswerGuard.Check(string.Empty, Pack("x")).Sentences);
        Assert.Equal("One.\r\nTwo.".Replace("\r\n", "\n", StringComparison.Ordinal), AnswerGuard.Check("One.\r\nTwo.", Pack("1", "2")).Text);
    }

    [Fact]
    public void The_guard_refuses_no_answer_or_pack()
    {
        Assert.Throws<ArgumentNullException>(() => AnswerGuard.Check(null!, Pack("x")));
        Assert.Throws<ArgumentNullException>(() => AnswerGuard.Check("x", null!));
        Assert.Throws<ArgumentNullException>(() => AnswerGuard.Refusal(null!, []));
        Assert.Throws<ArgumentNullException>(() => AnswerGuard.Refusal([], null!));
        Assert.Throws<ArgumentNullException>(() => AnswerGuard.Sentences(null!).ToList());
        Assert.Throws<ArgumentNullException>(() => NumberTokens.In(null!));
        Assert.Throws<ArgumentNullException>(() => NumberTokens.ReferencesIn(null!));
    }
}

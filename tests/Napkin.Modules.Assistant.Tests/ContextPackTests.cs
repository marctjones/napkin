using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

using Citation = Napkin.Core.RulesEngine.Citation;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>
/// The context pack (docs/design/llm-assistant.md §3, §9.1, §11.1 item 1): what napkin already says,
/// numbered, in one stated order, under a word budget.
/// </summary>
public class ContextPackTests
{
    private const string Question = "why is this header No data?";

    /// <summary>§9.1's pack: the window selected, the shipped Connecticut pack loaded from the repository's packs/.</summary>
    private static (ContextPack Pack, HeaderResult.NoData Result) WindowPack()
    {
        Design design = Fixtures.Sample("window-in-existing-wall");
        CodePacks packs = Fixtures.ShippedPacks();

        // Preconditions, named, so a change to the shipped pack fails here rather than as a diff of help text.
        LoadedPack ct = Assert.Single(packs.Loaded);
        Assert.Equal(("us-ct-2022", 1), (ct.Code.PackId, ct.Code.Revision));
        Assert.Equal(LoadedPack.BaseTablesNotLoaded, ct.StatusLabel);

        CodeResolution code = packs.Resolve(design.Sketch.Code);
        ImmutableArray<OpeningCheck> headers = CodeCheck.Of(design.Sketch, packs);
        HeaderResult.NoData result = Assert.IsType<HeaderResult.NoData>(Assert.Single(headers).Result);
        ContextPack pack = ContextPack.For(design, [Fixtures.Named(design, "Window 1")], ContextChecks.Of(code, headers), [], Question);
        return (pack, result);
    }

    [Fact]
    [Trait("Feature", "AST-001")]
    public void The_window_in_an_existing_wall_pack_is_the_hand_written_golden_item_by_item()
    {
        (ContextPack pack, HeaderResult.NoData result) = WindowPack();

        List<string> expected = [.. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Goldens", "window-in-existing-wall.pack.txt")).Select(Expand)];
        Assert.Equal(expected.Count, pack.Items.Length);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i], pack.Items[i].ToString());
        }

        Assert.Equal(string.Join("\n", expected), pack.Text);
        Assert.Equal(
            [
                ContextKind.Selection, ContextKind.Entity, ContextKind.Site, ContextKind.Code, ContextKind.Check,
                ContextKind.Help, ContextKind.Help, ContextKind.Disclaimer,
            ],
            pack.Items.Select(item => item.Kind));

        // Item [5] is the engine's own explanation, character for character, after its label.
        Assert.Equal(HeaderResult.NoData.NoTableExplanation("CT 2022", WallKind.ExteriorBearing), result.Explanation);
        Assert.Equal("Header check, Window 1: " + result.Explanation, pack.Item(5)!.Text);

        // Site values not entered read "not entered"; the disclaimer is last.
        Assert.Equal(7, pack.Item(3)!.Text.Split("not entered").Length - 1);
        Assert.Equal(ContextPack.Disclaimer, pack.Items[^1].Text);
        Assert.Null(pack.Item(0));
        Assert.Null(pack.Item(9));

        // The selection has a result, so the help is the result-kind map's and not the question's:
        // the question's own best section would have brought building.md's example "(2) 2x6" in.
        Assert.Equal(
            [HelpSections.Find("docs/rules-engine.md", "Data status: no real tables ship").ItemText, HelpSections.Find("docs/rules-engine.md", "In the app").ItemText],
            pack.Items.Where(item => item.Kind == ContextKind.Help).Select(item => item.Text));
        Assert.Contains(HelpSections.Find("docs/building.md", "Wall bracing"), HelpSections.ForQuestion(Question));
        Assert.Equal(pack.Items.Sum(item => ContextPack.CountWords(item.Text)), pack.Words);
        Assert.True(pack.Words <= ContextPack.WordBudget);
    }

    [Fact]
    [Trait("Feature", "AST-001")]
    public void The_good_answer_to_the_no_data_question_stands_whole_and_a_made_up_header_is_refused()
    {
        (ContextPack pack, _) = WindowPack();

        // §9.1's good answer: its one designation is in the pack and every item it refers to exists.
        const string good =
            "No data means napkin has no table to look in, not that the window is wrong [5]. The Connecticut pack you chose carries "
            + "the state's amendments but its base IRC tables are empty — \"base tables not loaded\" [4] [6]. The header table for an "
            + "exterior bearing wall is IRC Table R602.7(1) [6]; napkin ships no code values, so someone has to transcribe it from a "
            + "copy of the code into the pack directory [6]. Until then every header on a bearing wall says No data [5]. The site "
            + "values are also not entered yet [3], and the table will ask for the ground snow load once it exists.";
        GuardedAnswer kept = AnswerGuard.Check(good, pack);
        Assert.Equal(0, kept.Refused);
        Assert.Equal(good, kept.Text);
        Assert.Equal([5, 4, 6, 3], kept.References);

        // §9.1's bad sentence is refused, in napkin's words, and the others stand.
        const string bad = "No data means napkin has no table to look in [5]. A 3-foot opening in a 2x4 wall usually takes a (2) 2x6 header. "
                           + "The table is Table R602.7(1) [6].";
        GuardedAnswer guarded = AnswerGuard.Check(bad, pack);
        Assert.Equal([true, false, true], guarded.Sentences.Select(sentence => sentence.Kept));
        GuardedSentence refused = guarded.Sentences[1];
        // The 3-foot opening is the window's 3'-0" [1]; the pack has no 2x4, no 2 and no 2x6.
        Assert.Equal(["2x4", "2", "2x6"], refused.Unsupported);
        Assert.Equal("[one sentence refused: it said 2x4, 2 and 2x6, which napkin did not give it]", refused.Shown);
        Assert.Equal(
            $"No data means napkin has no table to look in [5]. {refused.Shown} The table is Table R602.7(1) [6].",
            guarded.Text);
    }

    [Fact]
    [Trait("Feature", "AST-001")]
    public void An_open_list_goes_in_as_the_lines_napkin_writes_to_csv()
    {
        Design design = Fixtures.Sample("coffee-table");
        ImmutableArray<CutListRow> rows = CutList.Of(design.Sketch, MaterialsLibrary.Shipped);
        ImmutableArray<ShoppingListRow> shopping = ShoppingList.Of(rows);
        string csv = ShoppingListCsv.ToCsv(shopping);

        ContextPack pack = ContextPack.For(design, [], ContextChecks.None, [OpenList.ShoppingList(shopping)], "how many boards?");

        List<string> lines = [.. csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
        Assert.Equal(shopping.Length + 2, lines.Count);
        List<ContextItem> listItems = [.. pack.Items.Where(item => item.Kind == ContextKind.ListRow)];
        Assert.Equal(lines.Select(line => "Shopping list: " + line), listItems.Select(item => item.Text));

        // In §3's order: after the design, the site, the code and the checks; before the help and the disclaimer.
        int first = pack.Items.IndexOf(listItems[0]);
        Assert.All(pack.Items.Take(first), item => Assert.Contains(item.Kind, new[] { ContextKind.Entity, ContextKind.Relationship, ContextKind.Site, ContextKind.Code }));
        Assert.All(pack.Items.Skip(first + listItems.Count), item => Assert.Contains(item.Kind, new[] { ContextKind.Help, ContextKind.Disclaimer }));
        Assert.Equal("Adopted code: No code selected: choose one under Project → Adopted code and site.", pack.Items.Single(item => item.Kind == ContextKind.Code).Text);
        Assert.DoesNotContain(pack.Items, item => item.Kind == ContextKind.ListCut);

        // Every part is a line, and every relationship is the relationship list's sentence.
        Assert.Equal(design.Sketch.Entities.Values.Count(entity => entity is not Node), pack.Items.Count(item => item.Kind == ContextKind.Entity));
        Assert.Equal(design.Sketch.Relationships.Count, pack.Items.Count(item => item.Kind == ContextKind.Relationship));
        Relationship firstRelationship = design.Sketch.RelationshipsInOrder.First();
        Assert.Equal(
            RelationshipText.Describe(design.Sketch, firstRelationship, id => DesignWords.NameOf(design, id), LengthFormat.Default),
            pack.Items.First(item => item.Kind == ContextKind.Relationship).Text);
    }

    [Fact]
    [Trait("Feature", "AST-001")]
    public void Over_budget_the_lists_are_cut_first_then_the_help_and_never_the_design()
    {
        Design design = Fixtures.Sample("coffee-table");
        ImmutableArray<CutListRow> rows = CutList.Of(design.Sketch, MaterialsLibrary.Shipped);
        OpenList cutList = OpenList.CutList(rows);
        OpenList shopping = OpenList.ShoppingList(ShoppingList.Of(rows));
        EntityId top = design.Sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id).First().Id;
        const string question = "how many boards are on the cut list and what is ground snow load";

        ContextPack whole = ContextPack.For(design, [top], ContextChecks.None, [cutList, shopping], question);
        Assert.DoesNotContain(whole.Items, item => item.Kind == ContextKind.ListCut);
        List<ContextItem> design_ = [.. whole.Items.Where(IsDesign)];
        List<ContextItem> help = [.. whole.Items.Where(item => item.Kind == ContextKind.Help)];
        Assert.Equal(3, help.Count);
        int listWords = whole.Items.Where(item => item.Kind == ContextKind.ListRow).Sum(item => ContextPack.CountWords(item.Text));

        // A budget a few rows short: the cut list keeps its first lines, says what it dropped, and the
        // shopping list follows the same rule; the help and the design are untouched.
        int tight = whole.Words - (listWords / 2);
        ContextPack cut = ContextPack.For(design, [top], ContextChecks.None, [cutList, shopping], question, tight);
        Assert.True(cut.Words <= tight, $"{cut.Words} words against a budget of {tight}");
        Assert.Equal(Texts(design_), Texts(cut.Items.Where(IsDesign)));
        Assert.Equal(Texts(help), Texts(cut.Items.Where(item => item.Kind == ContextKind.Help)));
        Assert.Contains(cut.Items, item => item.Kind == ContextKind.ListCut);
        foreach (OpenList list in new[] { cutList, shopping })
        {
            // Each list keeps its first lines in order; one that lost any says how many rows it lost.
            List<ContextItem> kept = [.. cut.Items.Where(item => item.Kind == ContextKind.ListRow && item.Text.StartsWith(list.Title + ": ", StringComparison.Ordinal))];
            Assert.Equal(list.Lines.Take(kept.Count).Select(line => $"{list.Title}: {line}"), kept.Select(item => item.Text));
            List<string> saidCut = Texts(cut.Items.Where(item => item.Kind == ContextKind.ListCut && item.Text.StartsWith(list.Title + ": ", StringComparison.Ordinal)));
            int shownRows = Math.Max(0, kept.Count - list.HeaderLines);
            Assert.Equal(
                kept.Count < list.Lines.Length ? [$"{list.Title}: … {list.Rows - shownRows} more rows not shown; napkin's list has {list.Rows}."] : [],
                saidCut);
        }

        // A budget with room for no list line at all: every list is cut to its one line, then the help goes, last first.
        int designOnly = design_.Sum(item => ContextPack.CountWords(item.Text)) + ContextPack.CountWords(ContextPack.Disclaimer);
        int cuts = ContextPack.CountWords($"Cut list: … {cutList.Rows} more rows not shown; napkin's list has {cutList.Rows}.")
                   + ContextPack.CountWords($"Shopping list: … {shopping.Rows} more rows not shown; napkin's list has {shopping.Rows}.");
        int firstHelp = ContextPack.CountWords(help[0].Text);
        ContextPack noLists = ContextPack.For(design, [top], ContextChecks.None, [cutList, shopping], question, designOnly + cuts + firstHelp);
        Assert.DoesNotContain(noLists.Items, item => item.Kind == ContextKind.ListRow);
        Assert.Equal(
            [
                $"Cut list: … {cutList.Rows} more rows not shown; napkin's list has {cutList.Rows}.",
                $"Shopping list: … {shopping.Rows} more rows not shown; napkin's list has {shopping.Rows}.",
            ],
            Texts(noLists.Items.Where(item => item.Kind == ContextKind.ListCut)));
        Assert.Equal([help[0].Text], Texts(noLists.Items.Where(item => item.Kind == ContextKind.Help)));
        Assert.True(noLists.Words <= designOnly + cuts + firstHelp);

        // A budget smaller than the design itself: no list, no help, and every design line still there.
        ContextPack tiny = ContextPack.For(design, [top], ContextChecks.None, [cutList, shopping], question, 10);
        Assert.Equal(Texts(design_), Texts(tiny.Items.Where(IsDesign)));
        Assert.DoesNotContain(tiny.Items, item => item.Kind is ContextKind.ListRow or ContextKind.Help);
        Assert.Equal(ContextKind.Disclaimer, tiny.Items[^1].Kind);
        Assert.True(tiny.Words > 10);

        static bool IsDesign(ContextItem item) => item.Kind is ContextKind.Selection or ContextKind.Entity or ContextKind.Relationship
            or ContextKind.Site or ContextKind.Code or ContextKind.Check or ContextKind.Disclaimer;

        static List<string> Texts(IEnumerable<ContextItem> items) => [.. items.Select(item => item.Text)];
    }

    [Fact]
    [Trait("Feature", "AST-001")]
    public void No_budget_with_room_for_the_design_and_the_cut_lines_is_ever_exceeded()
    {
        Design design = Fixtures.Sample("coffee-table");
        ImmutableArray<CutListRow> rows = CutList.Of(design.Sketch, MaterialsLibrary.Shipped);
        OpenList[] lists = [OpenList.CutList(rows), new OpenList("Fasteners", string.Empty), OpenList.ShoppingList(ShoppingList.Of(rows))];
        const string question = "how many boards are on the cut list and what is ground snow load";

        ContextPack whole = ContextPack.For(design, [], ContextChecks.None, lists, question);
        int design_ = whole.Items.Where(item => item.Kind is not (ContextKind.ListRow or ContextKind.Help)).Sum(item => ContextPack.CountWords(item.Text));
        int cuts = lists.Sum(list => ContextPack.CountWords($"{list.Title}: … {list.Rows} more rows not shown; napkin's list has {list.Rows}."));

        for (int budget = design_ + cuts; budget <= whole.Words; budget += 5)
        {
            ContextPack pack = ContextPack.For(design, [], ContextChecks.None, lists, question, budget);
            Assert.True(pack.Words <= budget, $"{pack.Words} words against a budget of {budget}");
            Assert.DoesNotContain(pack.Items, item => item.Text.StartsWith("Fasteners", StringComparison.Ordinal));
            Assert.Equal(
                whole.Items.Where(item => item.Kind is not (ContextKind.ListRow or ContextKind.Help)).Select(item => item.Text),
                pack.Items.Where(item => item.Kind is not (ContextKind.ListRow or ContextKind.Help or ContextKind.ListCut)).Select(item => item.Text));

            // Help goes only once no list line is left.
            if (pack.Items.Count(item => item.Kind == ContextKind.Help) < whole.Items.Count(item => item.Kind == ContextKind.Help))
            {
                Assert.DoesNotContain(pack.Items, item => item.Kind == ContextKind.ListRow);
            }
        }
    }

    [Fact]
    [Trait("Feature", "AST-001")]
    public void Every_check_is_its_results_own_text_the_selections_first_and_picks_its_help()
    {
        Box deckBox = Box.AsDrawn(new EntityId(Guid.NewGuid()), LayerId.Default, new Point2(Length.Zero, new Length(-24576)), new Length(12288), new Length(12288), new Length(1024), Angle.Zero)
            with { Name = "Deck" };
        Design sample = Fixtures.Sample("window-in-existing-wall");
        Design design = sample with { Sketch = sample.Sketch.WithEntity(deckBox) };
        Sketch sketch = design.Sketch;
        Wall wall = Wall.All(sketch)[0];
        Opening window = Opening.In(sketch, wall)[0];
        AdoptedCodeRef code = Synthetic.Code;

        // SYNTHETIC TEST DATA - NOT CODE VALUES.
        FootnoteRef footnote = new("zz-a", "Synthetic footnote text.", FootnoteEncoding.NotEncoded, Synthetic.Source);
        Citation traced = Synthetic.Citation(
            trace: ValueList.Of(new BandMatch("groundSnowLoad", "30 psf", "≤ 30 psf")),
            footnotes: ValueList.Of(footnote));
        HeaderResult.Sized sized = new(new MemberSpec(2, "2x8"), 1, 2, traced);
        HeaderResult.OutOfScope beyond = new(OutOfScopeReason.SpanExceedsTable, traced, "The span is longer than row r.zz.a.");
        HeaderResult.InputMissing missing = new(new ValueList<string>(["groundSnowLoad"]), "ZZ-HEADER", code, "Table ZZ-HEADER needs the ground snow load.");
        WallLine line = WallLine.All(sketch)[0];
        BracingResult.NoData unbraced = new(BracingNoDataReason.NoBracingProvisions, code, "ZZ FRAME has no bracing provisions.");
        Deck deck = new(deckBox);
        DeckChecks deckChecks = new(
            deck,
            null,
            new DeckRefusal(DeckProblem.NoInputs, "none"),
            [new DeckCheckLine(DeckCheckKind.Joists, null, "Joists: synthetic line.", false)],
            "Say what the deck supports.",
            new FrostSuggestion(new Length(1024), "ZZ FRAME says 1\" — use it?"),
            []);

        ContextChecks checks = new(
            new CodeResolution(null, "Code pack us-zz-frame, which this project chose, is not installed (docs/rules-engine.md says where packs go)."),
            [
                new OpeningCheck(window, sized),
                new OpeningCheck(window, beyond),
                new OpeningCheck(window, missing),
                new OpeningCheck(window, null) { NotChecked = CodeCheck.NotBearingText(wall) },
            ],
            [new WallBracingCheck(line, unbraced)],
            [deckChecks]);

        // The wall is selected: its bracing and its openings' checks come first, the deck's after.
        ContextPack pack = ContextPack.For(design, [wall.Id, new EntityId(Guid.NewGuid())], checks, [], "zzzz");
        List<string> said = [.. pack.Items.Where(item => item.Kind == ContextKind.Check).Select(item => item.Text)];
        Assert.Equal(
            [
                "Header check, Window 1: " + sized + "\nHow it was found: groundSnowLoad 30 psf → ≤ 30 psf\nFootnote zz-a: Synthetic footnote text.",
                "Header check, Window 1: " + beyond + "\nHow it was found: groundSnowLoad 30 psf → ≤ 30 psf\nFootnote zz-a: Synthetic footnote text.",
                "Header check, Window 1: " + missing.Explanation,
                "Header check, Window 1: Not checked: " + CodeCheck.NotBearingText(wall),
                "Bracing check, Wall 1: " + unbraced.Explanation,
                "Deck check, Deck: " + deckChecks.Refusal!.Text,
                "Deck check, Deck: Joists: synthetic line.",
                "Deck check, Deck: Say what the deck supports.",
                "Deck check, Deck: ZZ FRAME says 1\" — use it?",
            ],
            said);
        Assert.Equal("Adopted code: " + checks.Code.Problem, pack.Items.Single(item => item.Kind == ContextKind.Code).Text);
        Assert.StartsWith("Selected: Wall 1 — wall", pack.Items[0].Text, StringComparison.Ordinal);

        // Help by the kinds of the selection's results, in order, each section once: code check,
        // refuses, In the app, bracing. The deck is not selected, so its section is not picked.
        Assert.Equal(
            [
                "docs/building.md The code check on an opening",
                "docs/rules-engine.md What the engine refuses to do",
                "docs/rules-engine.md In the app",
                "docs/building.md Wall bracing",
            ],
            pack.Items.Where(item => item.Kind == ContextKind.Help).Select(item => HelpSections.All.Single(section => section.ItemText == item.Text)).Select(s => $"{s.Document} {s.Heading}"));
    }

    [Fact]
    public void A_bracing_result_with_working_carries_its_working_lines()
    {
        Design design = Fixtures.Sample("window-in-existing-wall");
        WallLine line = WallLine.All(design.Sketch)[0];

        // SYNTHETIC TEST DATA - NOT CODE VALUES.
        Length foot = new(Length.UnitsPerFoot);
        BracingWorking working = new(
            "b.zz.1", foot, foot, foot * 12, ValueList<FactorApplied>.Empty, ExactFraction.Whole(1024), foot, foot, ValueList<SegmentContribution>.Empty);
        BracingResult.Passes passes = new(foot, foot * 2, Synthetic.Citation(), working);
        BracingResult.Fails fails = new(foot * 3, foot, foot * 2, Synthetic.Citation(), working);
        BracingResult.OutOfScope beyond = new(OutOfScopeReason.InputAboveTableBands, Synthetic.Citation(), "Beyond the synthetic bands.");

        ContextChecks checks = new(ContextChecks.None.Code, [], [new WallBracingCheck(line, passes), new WallBracingCheck(line, fails), new WallBracingCheck(line, beyond)], []);
        List<string> said = [.. ContextPack.For(design, [], checks, [], string.Empty).Items.Where(item => item.Kind == ContextKind.Check).Select(item => item.Text)];

        Assert.Equal($"Bracing check, Wall 1: {passes}\n{string.Join("\n", working.Lines)}", said[0]);
        Assert.Equal($"Bracing check, Wall 1: {fails}\n{string.Join("\n", working.Lines)}", said[1]);
        Assert.Equal($"Bracing check, Wall 1: {beyond}", said[2]);
    }

    [Fact]
    public void The_site_and_code_lines_say_what_was_entered_and_chosen()
    {
        SiteValues site = new(30, 120, "B", new Length(42 * Length.UnitsPerInch), new Length(24 * Length.UnitsPerFoot), 20, new SiteSource("Town building department", new DateOnly(2026, 9, 1)))
        {
            SoilBearingPsf = 1500,
        };
        Assert.Equal(
            "Site: ground snow load 30 psf; ultimate wind speed 120 mph; seismic design category B; frost depth 3'-6\"; building width 24'-0\"; "
            + "roof live load 20 psf; soil bearing 1500 psf; source typed: \"Town building department\", 2026-09-01.",
            ContextPack.SiteLine(site));
        Assert.Equal(
            "Site: ground snow load not entered; ultimate wind speed not entered; seismic design category not entered; frost depth not entered; "
            + "building width not entered; roof live load not entered; soil bearing not entered; source typed: \"phone\".",
            ContextPack.SiteLine(SiteValues.NotEntered with { Source = new SiteSource("phone", null) }));

        LoadedPack ct = Assert.Single(Fixtures.ShippedPacks().Loaded);
        CodeResolution resolved = new(ct, null);
        Assert.Equal(
            "Adopted code: " + CodeCheck.CheckingStatus(ct) + " " + CodeCheck.FollowingNote("us-ct-2022"),
            ContextPack.CodeLine(new CodeChoice("us-ct-2022", 1, CodeMode.Following, null), resolved));
        Assert.Equal("Adopted code: " + CodeCheck.CheckingStatus(ct), ContextPack.CodeLine(new CodeChoice("us-ct-2022", 1, CodeMode.Locked, null), resolved));
        Assert.Equal("Adopted code: " + CodeCheck.CheckingStatus(ct), ContextPack.CodeLine(null, resolved));
        Assert.Equal("Adopted code: " + CodeCheck.NoCodeSelectedText, ContextPack.CodeLine(null, new CodeResolution(null, null)));
    }

    [Fact]
    public void A_pack_of_given_items_is_numbered_in_order_and_holds_their_numbers()
    {
        ContextPack pack = ContextPack.Of([(ContextKind.Help, "one 2x4"), (ContextKind.Disclaimer, null!)]);
        Assert.Equal([new ContextItem(1, ContextKind.Help, "one 2x4"), new ContextItem(2, ContextKind.Disclaimer, string.Empty)], pack.Items);
        Assert.Equal("[1] one 2x4\n[2] ", pack.Text);
        Assert.Equal(2, pack.Words);
        Assert.Contains("L:2x4", pack.Keys);
        Assert.Contains("N:1\"", pack.Keys);

        OpenList list = new("Cut list", "statement\r\nheader\r\n\r\nrow one\n");
        Assert.Equal(["statement", "header", "row one"], list.Lines);
        Assert.Equal(1, list.Rows);
        Assert.Equal(0, new OpenList("Empty", string.Empty).Rows);
    }

    [Fact]
    public void The_pack_refuses_missing_inputs()
    {
        Design design = Design.Unlabelled("empty", Sketch.Empty);
        Assert.Throws<ArgumentNullException>(() => ContextPack.For(null!, [], ContextChecks.None, [], string.Empty));
        Assert.Throws<ArgumentNullException>(() => ContextPack.For(design, null!, ContextChecks.None, [], string.Empty));
        Assert.Throws<ArgumentNullException>(() => ContextPack.For(design, [], null!, [], string.Empty));
        Assert.Throws<ArgumentNullException>(() => ContextPack.For(design, [], ContextChecks.None, null!, string.Empty));
        Assert.Throws<ArgumentNullException>(() => ContextPack.For(design, [], ContextChecks.None, [], null!));
        Assert.Throws<ArgumentNullException>(() => ContextPack.Of(null!));
        Assert.Throws<ArgumentNullException>(() => ContextPack.CountWords(null!));
        Assert.Throws<ArgumentNullException>(() => ContextPack.SiteLine(null!));
        Assert.Throws<ArgumentNullException>(() => ContextPack.CodeLine(null, null!));
        Assert.Throws<ArgumentNullException>(() => ContextChecks.Of(null!, []));
        Assert.Throws<ArgumentNullException>(() => ContextChecks.Of(ContextChecks.None.Code, null!));

        // An empty design still says its site, its code and the disclaimer.
        Assert.Equal(
            [ContextKind.Site, ContextKind.Code, ContextKind.Disclaimer],
            ContextPack.For(design, [], ContextChecks.None, [], string.Empty).Items.Select(item => item.Kind));
    }

    /// <summary>A golden line with its help placeholder filled from the document on disk, split independently of the module.</summary>
    private static string Expand(string line)
    {
        int open = line.IndexOf("{help:", StringComparison.Ordinal);
        if (open < 0)
        {
            return line;
        }

        string reference = line[(open + "{help:".Length)..line.LastIndexOf('}')];
        string document = reference[..reference.IndexOf('#', StringComparison.Ordinal)];
        string heading = reference[(reference.IndexOf('#', StringComparison.Ordinal) + 1)..];

        List<string> body = [];
        bool inside = false;
        bool fenced = false;
        foreach (string text in Fixtures.RepositoryText(document).Split('\n'))
        {
            fenced ^= text.StartsWith("```", StringComparison.Ordinal);
            if (!fenced && text.StartsWith("## ", StringComparison.Ordinal))
            {
                if (inside)
                {
                    break;
                }

                inside = text[3..].Trim() == heading;
                continue;
            }

            if (inside)
            {
                body.Add(text);
            }
        }

        Assert.True(inside, $"{document} has no section headed {heading}");
        return line[..open] + $"Help, {document} \"{heading}\": " + string.Join("\n", body).Trim('\n', ' ');
    }
}

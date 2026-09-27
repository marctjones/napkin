using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>napkin's help as the assistant reads it (docs/design/llm-assistant.md §3.3, §11.1 item 2).</summary>
public class HelpSectionsTests
{
    public static TheoryData<string> Documents
    {
        get
        {
            TheoryData<string> documents = [];
            foreach (string document in HelpSections.Documents)
            {
                documents.Add(document);
            }

            return documents;
        }
    }

    [Theory]
    [Trait("Feature", "AST-002")]
    [MemberData(nameof(Documents))]
    public void The_embedded_help_is_the_help_on_disk(string document)
    {
        // Against the repository's file, not a copy in the build output: a doc edited without a
        // rebuild fails loudly here.
        Assert.Equal(Fixtures.RepositoryText(document), HelpSections.Embedded(document));
    }

    [Theory]
    [Trait("Feature", "AST-002")]
    [MemberData(nameof(Documents))]
    public void Each_document_is_split_at_its_level_two_headings(string document)
    {
        string text = Fixtures.RepositoryText(document);
        List<string> headings = [];
        bool fenced = false;
        foreach (string line in text.Split('\n'))
        {
            fenced ^= line.StartsWith("```", StringComparison.Ordinal);
            if (!fenced && line.StartsWith("## ", StringComparison.Ordinal))
            {
                headings.Add(line[3..].Trim());
            }
        }

        string title = text.Split('\n')[0][2..].Trim();
        List<HelpSection> sections = [.. HelpSections.All.Where(section => section.Document == document)];
        Assert.Equal([title, .. headings], sections.Select(section => section.Heading));

        // Every word of the document is in exactly one section, and a ### stays inside its ## section.
        Assert.All(sections, section => Assert.DoesNotContain("\n## ", "\n" + section.Text, StringComparison.Ordinal));
        Assert.DoesNotContain(sections, section => section.Text.StartsWith("# ", StringComparison.Ordinal));

        // Nothing is lost between sections: the document's words are the sections' words plus the
        // title line and each heading line ("##" and its words).
        int headingWords = ContextPack.CountWords(text.Split('\n')[0]) + headings.Sum(heading => 1 + ContextPack.CountWords(heading));
        Assert.Equal(ContextPack.CountWords(text), sections.Sum(section => ContextPack.CountWords(section.Text)) + headingWords);
    }

    [Fact]
    [Trait("Feature", "AST-002")]
    public void The_result_kind_map_names_its_sections()
    {
        Assert.Equal(
            ["docs/rules-engine.md Data status: no real tables ship", "docs/rules-engine.md In the app"],
            Named(HelpSections.For(ResultKind.NoData)));
        Assert.Equal(["docs/rules-engine.md In the app"], Named(HelpSections.For(ResultKind.InputMissing)));
        Assert.Equal(
            ["docs/building.md The code check on an opening", "docs/rules-engine.md What the engine refuses to do"],
            Named(HelpSections.For(ResultKind.OutOfScope)));
        Assert.Equal(["docs/building.md The code check on an opening"], Named(HelpSections.For(ResultKind.Sized)));
        Assert.Equal(["docs/building.md The code check on an opening"], Named(HelpSections.For(ResultKind.NotChecked)));
        Assert.Equal(["docs/building.md Wall bracing"], Named(HelpSections.For(ResultKind.Bracing)));
        Assert.Equal(["docs/building.md A deck"], Named(HelpSections.For(ResultKind.Deck)));

        // Every kind has an entry: a new kind with no help is a gap, not a silent empty list.
        Assert.All(Enum.GetValues<ResultKind>(), kind => Assert.NotEmpty(HelpSections.For(kind)));
    }

    [Fact]
    [Trait("Feature", "AST-002")]
    public void A_heading_that_is_not_there_fails_loudly()
    {
        KeyNotFoundException missing = Assert.Throws<KeyNotFoundException>(() => HelpSections.Find("docs/rules-engine.md", "In the ap"));
        Assert.Equal("docs/rules-engine.md has no section headed \"In the ap\".", missing.Message);
        Assert.Throws<ArgumentException>(() => HelpSections.Embedded("docs/file-format.md"));
    }

    [Fact]
    [Trait("Feature", "AST-002")]
    public void A_free_question_picks_the_three_sections_sharing_most_of_its_words()
    {
        Assert.Equal(["ground", "snow", "load"], HelpSections.Terms("What is ground snow load?"));

        List<HelpSection> picked = [.. HelpSections.ForQuestion("what is ground snow load")];
        Assert.Equal(3, picked.Count);
        Assert.Contains(HelpSections.Find("docs/rules-engine.md", "In the app"), picked);

        // Each shares all three words; ties go in document order.
        Assert.Equal(
            ["docs/building.md The code check on an opening", "docs/building.md A deck", "docs/rules-engine.md In the app"],
            Named([.. picked]));
        Assert.True(picked[0].Order < picked[1].Order && picked[1].Order < picked[2].Order);
    }

    [Fact]
    [Trait("Feature", "AST-002")]
    public void Stop_words_never_match_and_a_question_of_only_stop_words_picks_nothing()
    {
        Assert.Empty(HelpSections.Terms("What is the, and how do I?"));
        Assert.Empty(HelpSections.ForQuestion("what is the"));
        Assert.Empty(HelpSections.ForQuestion("xyzzy plugh"));
        Assert.Single(HelpSections.ForQuestion("what is ground snow load", count: 1));
        Assert.Empty(HelpSections.ForQuestion("ground snow load", count: 0));
    }

    [Fact]
    public void A_header_check_has_the_kind_of_its_result()
    {
        Sketch sketch = Fixtures.Sample("window-in-existing-wall").Sketch;
        Opening opening = Opening.In(sketch, Wall.All(sketch)[0])[0];
        AdoptedCodeRef code = new("us-zz", 1, "ZZ", "IRC 2099", ReviewStatus.Unreviewed);
        Citation citation = Synthetic.Citation(code);

        Assert.Equal(ResultKind.Sized, HelpSections.KindOf(new OpeningCheck(opening, new HeaderResult.Sized(new MemberSpec(2, "2x8"), 1, 1, citation))));
        Assert.Equal(ResultKind.OutOfScope, HelpSections.KindOf(new OpeningCheck(opening, new HeaderResult.OutOfScope(OutOfScopeReason.SpanExceedsTable, citation, "e"))));
        Assert.Equal(ResultKind.InputMissing, HelpSections.KindOf(new OpeningCheck(opening, new HeaderResult.InputMissing(new ValueList<string>(["supports"]), "ZZ-1", code, "e"))));
        Assert.Equal(ResultKind.NoData, HelpSections.KindOf(new OpeningCheck(opening, new HeaderResult.NoData(NoDataReason.NoPackSelected, null, WallKind.ExteriorBearing, "e"))));
        Assert.Equal(ResultKind.NotChecked, HelpSections.KindOf(new OpeningCheck(opening, null) { NotChecked = "not bearing" }));
    }

    private static List<string> Named(IEnumerable<HelpSection> sections) => [.. sections.Select(section => $"{section.Document} {section.Heading}")];
}

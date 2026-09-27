using System.Collections.Immutable;
using System.Text.RegularExpressions;

using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;

namespace Napkin.Modules.Assistant;

/// <summary>One section of napkin's help: a document's text from one <c>##</c> heading to the next.</summary>
/// <param name="Document">The document, as the repository names it: <c>docs/rules-engine.md</c>.</param>
/// <param name="Heading">The section's heading, without the <c>##</c>; the document's title for the text before its first one.</param>
/// <param name="Text">The section's text under its heading, as written, <c>###</c> subsections included.</param>
/// <param name="Order">Where it is among all the sections, in document order: how ties are broken.</param>
public sealed record HelpSection(string Document, string Heading, string Text, int Order)
{
    /// <summary>The pack item's text: <c>Help, docs/rules-engine.md "In the app": …</c>.</summary>
    public string ItemText => $"Help, {Document} \"{Heading}\": {Text}";
}

/// <summary>The kind of check result a help section is picked for (docs/design/llm-assistant.md §3.3).</summary>
public enum ResultKind
{
    /// <summary>A header sized from a table row.</summary>
    Sized,

    /// <summary>A header beyond what its table covers.</summary>
    OutOfScope,

    /// <summary>A header whose table needs an input not entered.</summary>
    InputMissing,

    /// <summary>A header with no table to answer from.</summary>
    NoData,

    /// <summary>An opening in a wall marked not bearing, which napkin does not size.</summary>
    NotChecked,

    /// <summary>Any wall-bracing result.</summary>
    Bracing,

    /// <summary>Any deck check.</summary>
    Deck,
}

/// <summary>
/// napkin's help, embedded at build time from <c>docs/</c> (§3.3): the only general knowledge the
/// assistant is given. Each document is split at its <c>##</c> headings; a section goes into a pack
/// by the kind of a check result on screen (a static map) or by plain whole-word overlap with the
/// question — no embeddings, no second model, deterministic and testable by hand.
/// </summary>
public static partial class HelpSections
{
    /// <summary>The help documents, in the order they are searched and ties are broken.</summary>
    public static readonly ImmutableArray<string> Documents =
    [
        "docs/building.md",
        "docs/rules-engine.md",
        "docs/shortcuts.md",
        "docs/viewer.md",
        "docs/first-run.md",
    ];

    /// <summary>
    /// Words that say nothing about a topic and so never match: a question's "what", "is", "the".
    /// </summary>
    public static readonly ImmutableHashSet<string> StopWords = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "a", "about", "after", "again", "all", "also", "am", "an", "and", "any", "are", "as", "at", "be", "because", "been",
        "before", "being", "but", "by", "can", "could", "did", "do", "does", "doing", "done", "don", "each", "else", "for",
        "from", "get", "got", "had", "has", "have", "he", "her", "here", "him", "his", "how", "i", "if", "in", "into", "is",
        "isn", "it", "its", "just", "let", "ll", "may", "me", "might", "more", "most", "much", "must", "my", "no", "not",
        "now", "of", "off", "on", "one", "only", "or", "our", "out", "over", "please", "s", "she", "should", "so", "some",
        "such", "t", "tell", "than", "that", "the", "their", "them", "then", "there", "these", "they", "this", "those", "to",
        "too", "under", "up", "us", "ve", "very", "was", "we", "were", "what", "when", "where", "which", "while", "who",
        "whom", "why", "will", "with", "would", "yes", "you", "your");

    private static readonly Lazy<ImmutableArray<HelpSection>> Sections = new(Split);

    private static readonly ImmutableDictionary<ResultKind, ImmutableArray<(string Document, string Heading)>> Map =
        new Dictionary<ResultKind, ImmutableArray<(string Document, string Heading)>>
        {
            [ResultKind.NoData] = [("docs/rules-engine.md", "Data status: no real tables ship"), ("docs/rules-engine.md", "In the app")],
            [ResultKind.InputMissing] = [("docs/rules-engine.md", "In the app")],
            [ResultKind.OutOfScope] = [("docs/building.md", "The code check on an opening"), ("docs/rules-engine.md", "What the engine refuses to do")],
            [ResultKind.Sized] = [("docs/building.md", "The code check on an opening")],
            [ResultKind.NotChecked] = [("docs/building.md", "The code check on an opening")],
            [ResultKind.Bracing] = [("docs/building.md", "Wall bracing")],
            [ResultKind.Deck] = [("docs/building.md", "A deck")],
        }.ToImmutableDictionary();

    /// <summary>Every section of every document, in document order.</summary>
    public static ImmutableArray<HelpSection> All => Sections.Value;

    /// <summary>A document exactly as embedded, line breaks as <c>\n</c>.</summary>
    /// <param name="document">One of <see cref="Documents"/>.</param>
    /// <exception cref="ArgumentException">Not one of napkin's help documents.</exception>
    public static string Embedded(string document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Documents.Contains(document))
        {
            throw new ArgumentException($"{document} is not one of napkin's help documents.", nameof(document));
        }

        string resource = "Napkin.Modules.Assistant.Help." + document["docs/".Length..];
        using Stream stream = typeof(HelpSections).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"{resource} is not embedded in the assistant module.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>The section a document has under a heading.</summary>
    /// <param name="document">The document.</param>
    /// <param name="heading">The heading, without the <c>##</c>.</param>
    /// <exception cref="KeyNotFoundException">
    /// There is no such section — a renamed heading must fail loudly here, never drop a section from
    /// the result-kind map without anyone noticing.
    /// </exception>
    public static HelpSection Find(string document, string heading)
        => All.FirstOrDefault(section => section.Document == document && section.Heading == heading)
           ?? throw new KeyNotFoundException($"{document} has no section headed \"{heading}\".");

    /// <summary>The sections the static map gives for a kind of result.</summary>
    public static ImmutableArray<HelpSection> For(ResultKind kind)
        => [.. Map[kind].Select(entry => Find(entry.Document, entry.Heading))];

    /// <summary>The kind of a header check: the result's kind, or <see cref="ResultKind.NotChecked"/> for a not-bearing wall's opening.</summary>
    public static ResultKind KindOf(OpeningCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return check.Result switch
        {
            HeaderResult.Sized => ResultKind.Sized,
            HeaderResult.OutOfScope => ResultKind.OutOfScope,
            HeaderResult.InputMissing => ResultKind.InputMissing,
            HeaderResult.NoData => ResultKind.NoData,
            _ => ResultKind.NotChecked,
        };
    }

    /// <summary>
    /// The sections that best match a free question: the top <paramref name="count"/> by how many of
    /// the question's words (stop words removed, whole words, any case) each contains, ties in
    /// document order; a section that shares no word is never picked.
    /// </summary>
    /// <param name="question">The question as typed.</param>
    /// <param name="count">How many at most.</param>
    public static ImmutableArray<HelpSection> ForQuestion(string question, int count = 3)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        ImmutableArray<string> terms = Terms(question);
        if (terms.IsEmpty)
        {
            return [];
        }

        return
        [
            .. All
                .Select(section => (Section: section, Score: Overlap(terms, section)))
                .Where(scored => scored.Score > 0)
                .OrderByDescending(scored => scored.Score)
                .ThenBy(scored => scored.Section.Order)
                .Take(count)
                .Select(scored => scored.Section),
        ];
    }

    /// <summary>A question's words that can match: lower case, stop words removed, each once, in order.</summary>
    /// <param name="text">The question.</param>
    public static ImmutableArray<string> Terms(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. Words(text).Where(word => !StopWords.Contains(word)).Distinct()];
    }

    private static int Overlap(ImmutableArray<string> terms, HelpSection section)
    {
        HashSet<string> words = [.. Words(section.Heading), .. Words(section.Text)];
        return terms.Count(words.Contains);
    }

    private static IEnumerable<string> Words(string text)
        => WordPattern().Matches(text.ToLowerInvariant()).Select(match => match.Value);

    private static ImmutableArray<HelpSection> Split()
    {
        List<HelpSection> sections = [];
        foreach (string document in Documents)
        {
            string[] lines = Embedded(document).Split('\n');
            string heading = lines.FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim() ?? document;
            List<string> body = [];
            bool fenced = false;
            foreach (string line in lines)
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    fenced = !fenced;
                }

                if (!fenced && line.StartsWith("## ", StringComparison.Ordinal))
                {
                    Add(document, heading, body);
                    heading = line[3..].Trim();
                    body = [];
                }
                else if (fenced || !line.StartsWith("# ", StringComparison.Ordinal))
                {
                    // The document's title is the first section's heading, not its text.
                    body.Add(line);
                }
            }

            Add(document, heading, body);
        }

        return [.. sections];

        void Add(string document, string heading, List<string> body)
        {
            string text = string.Join("\n", body).Trim('\n', ' ');
            if (text.Length > 0)
            {
                sections.Add(new HelpSection(document, heading, text, sections.Count));
            }
        }
    }

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex WordPattern();
}

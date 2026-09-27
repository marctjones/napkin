using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.Modules.Assistant;

/// <summary>
/// Everything the assistant is told, and all it may use (docs/design/llm-assistant.md §3): a numbered
/// list of what napkin already says — the design in words, the selection first, the site values as
/// entered, the adopted code, every check result in the engine's own words, the open lists' CSV
/// lines, the help sections that fit, and the disclaimer last.
/// </summary>
/// <remarks>
/// <para>
/// Order: the selected entities, the other entities, the relationships, the site, the code, the
/// checks (the selection's first), the open lists, the help, the disclaimer. Nothing is
/// reformatted: a check item is a label and the result's own text, character for character.
/// </para>
/// <para>
/// Help: the result-kind map's sections for the selection's check results when it has any,
/// otherwise the question's three best sections by shared words — one or the other, not both.
/// </para>
/// <para>
/// Budget: <see cref="WordBudget"/> words. Over it, the lists are cut first — each cut list ends in
/// "… N more rows not shown; napkin's list has M" — the help sections second, from the last; the
/// design's own lines, the site, the code, the checks and the disclaimer never.
/// </para>
/// <para>
/// The pack is built from results the app already computed; nothing here runs the rules engine or
/// reads a pack from disk (§12.9, and a test holds the module to it).
/// </para>
/// </remarks>
public sealed class ContextPack
{
    /// <summary>
    /// The word budget: napkin's own number, chosen to sit inside the 32,768-token context the
    /// recommended models declare with room for the prompt and the answer (§3.4).
    /// </summary>
    public const int WordBudget = 6000;

    /// <summary>napkin's disclaimer (DESIGN.md §7), the pack's last item and the note's line under every answer.</summary>
    public const string Disclaimer = ScopeDisclaimer.Text;

    private readonly Lazy<ImmutableHashSet<string>> _keys;

    private ContextPack(ImmutableArray<ContextItem> items)
    {
        Items = items;
        Text = string.Join("\n", items.Select(item => item.ToString()));
        Words = items.Sum(item => CountWords(item.Text));
        _keys = new Lazy<ImmutableHashSet<string>>(() => [.. Items.SelectMany(item => NumberTokens.KeysIn(item.Text))]);
    }

    /// <summary>The items, numbered from 1 in order.</summary>
    public ImmutableArray<ContextItem> Items { get; }

    /// <summary>The pack as the model reads it: one <c>[n] text</c> per item.</summary>
    public string Text { get; }

    /// <summary>How many words the items hold: what the budget counts, and what a cloud runtime says it sent.</summary>
    public int Words { get; }

    /// <summary>Every number token the pack holds, normalised (<see cref="NumberTokens"/>): what the guard looks an answer's numbers up in.</summary>
    public ImmutableHashSet<string> Keys => _keys.Value;

    /// <summary>Item <paramref name="n"/>, or null when there is none.</summary>
    /// <param name="n">The item's number, from 1.</param>
    public ContextItem? Item(int n) => n >= 1 && n <= Items.Length ? Items[n - 1] : null;

    /// <summary>A pack of exactly these items, numbered in order: for a caller that has its own.</summary>
    /// <param name="items">The items' kinds and texts.</param>
    public static ContextPack Of(IEnumerable<(ContextKind Kind, string Text)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new ContextPack([.. items.Select((item, i) => new ContextItem(i + 1, item.Kind, item.Text ?? string.Empty))]);
    }

    /// <summary>Words as the budget counts them: runs of non-white-space.</summary>
    /// <param name="text">The text.</param>
    public static int CountWords(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>The pack for a question about a design (§3).</summary>
    /// <param name="design">The design on screen.</param>
    /// <param name="selection">What is selected; said first.</param>
    /// <param name="checks">The check results on screen, as the app computed them.</param>
    /// <param name="lists">The list windows that are open, as their CSV.</param>
    /// <param name="question">The question, which picks help sections by the words it shares with them.</param>
    /// <param name="budget">The word budget; <see cref="WordBudget"/> unless a test says otherwise.</param>
    public static ContextPack For(
        Design design,
        IEnumerable<EntityId> selection,
        ContextChecks checks,
        IEnumerable<OpenList> lists,
        string question,
        int budget = WordBudget)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(question);

        Sketch sketch = design.Sketch;
        HashSet<EntityId> selected = [.. selection.Where(id => sketch.Find(id) is not null)];

        // The project on screen (§3.1): never cut.
        List<(ContextKind Kind, string Text)> fixedItems = [];
        foreach (Entity entity in sketch.Entities.Values.Where(e => selected.Contains(e.Id)).OrderBy(e => e.Id))
        {
            if (DesignWords.Line(design, entity) is { } line)
            {
                fixedItems.Add((ContextKind.Selection, "Selected: " + line));
            }
        }

        foreach (Entity entity in sketch.Entities.Values.Where(e => !selected.Contains(e.Id)).OrderBy(e => e.Id))
        {
            if (DesignWords.Line(design, entity) is { } line)
            {
                fixedItems.Add((ContextKind.Entity, line));
            }
        }

        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            fixedItems.Add((ContextKind.Relationship, RelationshipText.Describe(sketch, relationship, id => DesignWords.NameOf(design, id), LengthFormat.Default)));
        }

        fixedItems.Add((ContextKind.Site, SiteLine(sketch.Site)));
        fixedItems.Add((ContextKind.Code, CodeLine(sketch.Code, checks.Code)));

        List<(bool Selected, ResultKind Kind, string Text)> checkLines = [.. CheckLines(design, checks, selected)];
        List<(bool Selected, ResultKind Kind, string Text)> ordered = [.. checkLines.Where(c => c.Selected), .. checkLines.Where(c => !c.Selected)];
        fixedItems.AddRange(ordered.Select(c => (ContextKind.Check, c.Text)));

        // The help (§3.3): by the kind of each of the selection's results when it has any — the
        // question is about them, "Explain this result" always is — and otherwise by the question's
        // words. Never both: a section picked by a word carries its own example numbers into the pack.
        List<(bool Selected, ResultKind Kind, string Text)> selectedChecks = [.. ordered.Where(c => c.Selected)];
        IEnumerable<HelpSection> picked = selectedChecks.Count > 0
            ? selectedChecks.SelectMany(c => HelpSections.For(c.Kind))
            : HelpSections.ForQuestion(question);
        List<HelpSection> help = [];
        foreach (HelpSection section in picked)
        {
            if (!help.Contains(section))
            {
                help.Add(section);
            }
        }

        List<OpenList> open = [.. lists];
        int disclaimerWords = CountWords(Disclaimer);
        int used = fixedItems.Sum(item => CountWords(item.Text)) + disclaimerWords;
        int helpWords = help.Sum(section => CountWords(section.ItemText));
        int listWords = open.Sum(list => list.Lines.Sum(line => CountWords(RowText(list, line))));

        List<(ContextKind Kind, string Text)> listItems = [];
        if (used + helpWords + listWords <= budget)
        {
            listItems.AddRange(open.SelectMany(list => list.Lines.Select(line => (ContextKind.ListRow, RowText(list, line)))));
        }
        else
        {
            // Lists first: keep each list's lines in order while they fit, with room kept for every
            // list's line saying what was cut; a list kept whole gives its room back to the next.
            int allCut = open.Sum(Reserve);
            bool helpFits = used + helpWords + allCut <= budget;
            int room = budget - used - (helpFits ? helpWords : 0) - allCut;
            foreach (OpenList list in open)
            {
                int kept = 0;
                foreach (string line in helpFits ? list.Lines : [])
                {
                    int words = CountWords(RowText(list, line));
                    if (words > room)
                    {
                        break;
                    }

                    listItems.Add((ContextKind.ListRow, RowText(list, line)));
                    room -= words;
                    kept++;
                }

                if (kept < list.Lines.Length)
                {
                    listItems.Add((ContextKind.ListCut, CutText(list, kept)));
                }
                else
                {
                    room += Reserve(list);
                }
            }

            // Help second, from the last, once no list line is left to cut.
            if (!helpFits)
            {
                int total = used + listItems.Sum(item => CountWords(item.Text)) + helpWords;
                while (help.Count > 0 && total > budget)
                {
                    total -= CountWords(help[^1].ItemText);
                    help.RemoveAt(help.Count - 1);
                }
            }
        }

        return Of(
        [
            .. fixedItems,
            .. listItems,
            .. help.Select(section => (ContextKind.Help, section.ItemText)),
            (ContextKind.Disclaimer, Disclaimer),
        ]);
    }

    /// <summary>The site values as entered, each field or "not entered" (§3.1).</summary>
    /// <param name="site">The project's site values.</param>
    public static string SiteLine(SiteValues site)
    {
        ArgumentNullException.ThrowIfNull(site);
        string source = site.Source is { } typed
            ? $"; source typed: \"{typed.Text}\"" + (typed.On is { } on ? ", " + on.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty)
            : string.Empty;
        return "Site: "
               + string.Join(
                   "; ",
                   Whole("ground snow load", site.GroundSnowLoadPsf, "psf"),
                   Whole("ultimate wind speed", site.UltimateWindSpeedMph, "mph"),
                   "seismic design category " + (site.SeismicDesignCategory ?? NotEntered),
                   "frost depth " + (site.FrostDepth is { } frost ? DesignWords.Size(frost) : NotEntered),
                   "building width " + (site.BuildingWidth is { } width ? DesignWords.Size(width) : NotEntered),
                   Whole("roof live load", site.RoofLiveLoadPsf, "psf"),
                   Whole("soil bearing", site.SoilBearingPsf, "psf"))
               + source + ".";

        static string Whole(string what, int? value, string unit)
            => value is { } v ? $"{what} {v.ToString(CultureInfo.InvariantCulture)} {unit}" : $"{what} {NotEntered}";
    }

    /// <summary>
    /// The adopted code: the code window's status for the resolved pack and its lock note, or why no
    /// pack resolves — the choice and its status, never the pack's contents (§3.1).
    /// </summary>
    /// <param name="choice">The project's code choice.</param>
    /// <param name="code">The choice as resolved.</param>
    public static string CodeLine(CodeChoice? choice, CodeResolution code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Pack is not { } pack)
        {
            return "Adopted code: " + (code.Problem ?? CodeCheck.NoCodeSelectedText);
        }

        string lockNote = choice switch
        {
            { Mode: CodeMode.Following } => " " + CodeCheck.FollowingNote(choice.PackId),
            { Mode: CodeMode.Locked, LockedOn: { } on } => " " + CodeCheck.LockedNote(on, choice.PackId, choice.Revision),
            _ => string.Empty,
        };
        return "Adopted code: " + CodeCheck.CheckingStatus(pack) + lockNote;
    }

    private const string NotEntered = "not entered";

    private static string RowText(OpenList list, string line) => $"{list.Title}: {line}";

    /// <summary>The words kept for a list's cut line: the numbers in it are one word each, whatever they are.</summary>
    private static int Reserve(OpenList list) => list.Lines.IsEmpty ? 0 : CountWords(CutText(list, 0));

    private static string CutText(OpenList list, int keptLines)
    {
        int shown = Math.Max(0, keptLines - list.HeaderLines);
        return $"{list.Title}: … {list.Rows - shown} more rows not shown; napkin's list has {list.Rows}.";
    }

    private static IEnumerable<(bool Selected, ResultKind Kind, string Text)> CheckLines(Design design, ContextChecks checks, HashSet<EntityId> selected)
    {
        foreach (OpeningCheck check in checks.Headers)
        {
            string text = $"Header check, {DesignWords.NameOf(design, check.Opening.Id)}: "
                          + (check.Result is { } result ? result.ToString() + Working(result) : $"Not checked: {check.NotChecked}");
            yield return (selected.Contains(check.Opening.Id) || selected.Contains(check.Opening.Wall.Id), HelpSections.KindOf(check), text);
        }

        foreach (WallBracingCheck check in checks.Bracing)
        {
            (Citation? citation, IEnumerable<string> lines) = check.Result switch
            {
                BracingResult.Passes p => (p.Citation, p.Working.Lines),
                BracingResult.Fails f => (f.Citation, f.Working.Lines),
                BracingResult.OutOfScope o => ((Citation?)o.Limit, (IEnumerable<string>)[]),
                _ => (null, []),
            };
            string text = $"Bracing check, {DesignWords.NameOf(design, check.Wall.Id)}: {check.Result}"
                          + string.Concat(lines.Select(line => "\n" + line))
                          + Working(citation);
            yield return (selected.Contains(check.Wall.Id), ResultKind.Bracing, text);
        }

        foreach (DeckChecks deck in checks.Decks)
        {
            string name = DesignWords.NameOf(design, deck.Deck.Id);
            bool isSelected = selected.Contains(deck.Deck.Id);
            IEnumerable<string> said =
            [
                .. deck.Refusal is { } refusal ? [refusal.Text] : Array.Empty<string>(),
                .. deck.Lines.Select(line => line.Text),
                .. deck.SupportsNote is { } note ? [note] : Array.Empty<string>(),
                .. deck.Frost is { } frost ? [frost.Text] : Array.Empty<string>(),
            ];
            foreach (string text in said)
            {
                yield return (isSelected, ResultKind.Deck, $"Deck check, {name}: {text}");
            }
        }
    }

    /// <summary>The working the part panel opens under a sized or out-of-scope result: its band trace, interpolation and footnotes.</summary>
    private static string Working(HeaderResult result) => Working(result switch
    {
        HeaderResult.Sized s => s.Citation,
        HeaderResult.OutOfScope o => o.Limit,
        _ => null,
    });

    private static string Working(Citation? citation)
    {
        if (citation is null)
        {
            return string.Empty;
        }

        IEnumerable<string> lines = citation.Trace.Select(match => $"How it was found: {match}")
            .Concat(citation.Interpolation is { } working ? [$"Interpolation: {working}"] : [])
            .Concat(citation.Footnotes.Select(note => $"Footnote {note.Id}: {note.Text}"));
        return string.Concat(lines.Select(line => "\n" + line));
    }
}

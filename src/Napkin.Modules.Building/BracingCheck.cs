using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building;

/// <summary>One wall's line and its bracing result.</summary>
/// <param name="Line">The wall line: its segments and their methods.</param>
/// <param name="Result">What the adopted code's bracing provisions say about it.</param>
public sealed record WallBracingCheck(WallLine Line, BracingResult Result)
{
    /// <summary>The wall.</summary>
    public Wall Wall => Line.Wall;
}

/// <summary>
/// The wall-bracing check (issue #39, docs/building.md): every wall line checked by the rules
/// engine against the project's adopted code and site. Pure and total like <see cref="CodeCheck"/>:
/// the same sketch and packs give the same results, every wall every time, and nothing is stored.
/// </summary>
public static class BracingCheck
{
    /// <summary>Every wall's bracing result.</summary>
    public static ImmutableArray<WallBracingCheck> Of(Sketch sketch, CodePacks packs)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(packs);
        CodeResolution code = packs.Resolve(sketch.Code);
        return [.. WallLine.All(sketch).Select(line => new WallBracingCheck(line, For(sketch, line, code)))];
    }

    /// <summary>One wall line's result under a resolved code.</summary>
    public static BracingResult For(Sketch sketch, WallLine line, CodeResolution code)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(code);
        return code.Pack is { } pack
            ? RulesEngine.For(pack).CheckBracing(Request(sketch, line))
            : new BracingResult.NoData(BracingNoDataReason.NoPackSelected, null, code.Problem!);
    }

    /// <summary>The rules engine's question for a wall line: its length, the wall's height, its segments and methods, and the site.</summary>
    public static BracingRequest Request(Sketch sketch, WallLine line)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(line);
        return new BracingRequest(
            new BracedWallLine(
                line.Wall.Length,
                line.Wall.Height,
                line.Segments.Select(s => new BracedSegment(s.Label, s.Length, s.Method)).ToValueList()),
            CodeCheck.Site(sketch.Site));
    }

    /// <summary>The methods a pack offers for a segment, in the pack's order; empty with no pack or no provisions.</summary>
    public static ImmutableArray<BracingMethod> Methods(LoadedPack? pack) => pack?.Bracing is { } b ? [.. b.Methods] : [];

    /// <summary>What a segment's picker offers first: no method, which is what napkin assumes until one is chosen.</summary>
    public const string NotBraced = "not braced";

    /// <summary>A segment's method that the adopted code does not have (another pack's), shown as it is: "zz-board (not in this code)".</summary>
    /// <param name="method">The method's id as the design stores it.</param>
    public static string NotInThisCode(string method) => $"{method} (not in this code)";

    /// <summary>The pickers' tooltip when the adopted code has no bracing methods to choose from.</summary>
    /// <param name="shortName">The code's short name ("CT 2022").</param>
    public static string NoProvisionsTip(string shortName)
        => $"{shortName} has no wall-bracing provisions loaded, so there is no method to choose (docs/rules-engine.md).";

    /// <summary>A passing line's headline: "Braced length 10'-0" of 6'-6" required: passes (ZZ-BRACE.1)."</summary>
    /// <param name="provided">The braced length.</param>
    /// <param name="required">The required length.</param>
    /// <param name="table">The section it is cited to.</param>
    public static string PassesText(Length provided, Length required, string table)
        => $"Braced length {Show(provided)} of {Show(required)} required: passes ({table}).";

    /// <summary>A failing line's headline: "Braced length 5'-0" of 6'-6" required: SHORT by 1'-6" (ZZ-BRACE.1)."</summary>
    /// <param name="provided">The braced length.</param>
    /// <param name="required">The required length.</param>
    /// <param name="shortfall">How much is missing.</param>
    /// <param name="table">The section it is cited to.</param>
    public static string FailsText(Length provided, Length required, Length shortfall, string table)
        => $"Braced length {Show(provided)} of {Show(required)} required: SHORT by {Show(shortfall)} ({table}).";

    /// <summary>The message bar's line when a wall's braced line comes to pass: "Wall 1's braced line now passes, braced 8'-0" of 6'-6" required (ZZ-BRACE.1)."</summary>
    /// <param name="wall">The wall's name.</param>
    /// <param name="provided">The braced length.</param>
    /// <param name="required">The required length.</param>
    /// <param name="table">The section it is cited to.</param>
    public static string NowPassesText(string wall, Length provided, Length required, string table)
        => $"{wall}'s braced line now {ShortPasses(provided, required, table)}.";

    /// <summary>The message bar's line when a wall's braced line comes to fail: "Wall 1's braced line is now SHORT by 1'-6", braced 5'-0" of 6'-6" required (ZZ-BRACE.1)."</summary>
    /// <param name="wall">The wall's name.</param>
    /// <param name="provided">The braced length.</param>
    /// <param name="required">The required length.</param>
    /// <param name="shortfall">How much is missing.</param>
    /// <param name="table">The section it is cited to.</param>
    public static string NowFailsText(string wall, Length provided, Length required, Length shortfall, string table)
        => $"{wall}'s braced line is now {ShortFails(provided, required, shortfall, table)}.";

    /// <summary>
    /// The result in plain words for the part panel: "Braced length 5'-0" of 6'-6" required: SHORT by
    /// 1'-6" (ZZ-BRACE.1)", the citation line, and the working behind it.
    /// </summary>
    public static CheckWords Words(BracingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result switch
        {
            BracingResult.Passes p => new CheckWords(
                PassesText(p.Provided, p.Required, p.Citation.Table),
                p.Citation.ToString(),
                Details(p.Citation, p.Working),
                string.Empty),
            BracingResult.Fails f => new CheckWords(
                FailsText(f.Provided, f.Required, f.Shortfall, f.Citation.Table),
                f.Citation.ToString(),
                Details(f.Citation, f.Working),
                string.Empty),
            BracingResult.OutOfScope o => new CheckWords(
                $"This wall line is beyond what Section {o.Limit.Table} covers: {Limit(o.Explanation)} napkin stops here: get the bracing engineered.",
                $"Limit: {o.Limit}",
                Details(o.Limit, null),
                string.Empty),
            BracingResult.InputMissing m => new CheckWords(
                $"Not checked: {Named(m.Inputs)} {(m.Inputs.Count == 1 ? "is" : "are")} not entered, and napkin never assumes a value. "
                + $"Enter the site values under {CodeCheck.WhereToChoose}.",
                $"Section {m.Section}, {m.Code}",
                string.Empty,
                string.Empty),
            _ => NoDataWords((BracingResult.NoData)result),
        };
    }

    /// <summary>A short form for a list or the message bar.</summary>
    public static string Short(BracingResult result) => result switch
    {
        BracingResult.Passes p => ShortPasses(p.Provided, p.Required, p.Citation.Table),
        BracingResult.Fails f => ShortFails(f.Provided, f.Required, f.Shortfall, f.Citation.Table),
        BracingResult.OutOfScope o => $"beyond Section {o.Limit.Table}: get it engineered",
        BracingResult.InputMissing m => $"not checked: {Named(m.Inputs)} not entered",
        _ => "no data to check it against",
    };

    /// <summary>
    /// What a recompute changed, one sentence per wall whose bracing result differs, newly flagged
    /// first (design §7.3). Walls added or removed between the two are not changes of a result.
    /// </summary>
    public static ImmutableArray<string> Changes(IReadOnlyList<WallBracingCheck> before, IReadOnlyList<WallBracingCheck> after)
        => [.. Report(before, after).Changes.Where(Announced).Select(change => Sentence(Name(after, change.Element), change))];

    /// <summary>The engine's diff over the walls present both before and after.</summary>
    public static BracingRecomputeReport Report(IReadOnlyList<WallBracingCheck> before, IReadOnlyList<WallBracingCheck> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        HashSet<EntityId> both = [.. before.Select(c => c.Wall.Id).Intersect(after.Select(c => c.Wall.Id))];
        return Recompute.DiffBracing(
            [.. before.Where(c => both.Contains(c.Wall.Id)).Select(c => KeyValuePair.Create(c.Wall.Id, c.Result))],
            [.. after.Where(c => both.Contains(c.Wall.Id)).Select(c => KeyValuePair.Create(c.Wall.Id, c.Result))]);
    }

    /// <summary>
    /// The segments whose method did not carry over an edit, in plain words: a merge of segments
    /// with different methods, or a segment that is new (an opening moved past another, or split it)
    /// where an assigned one was before.
    /// </summary>
    public static ImmutableArray<string> Unassigned(IReadOnlyList<WallBracingCheck> before, IReadOnlyList<WallBracingCheck> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        List<string> said = [];
        foreach (WallBracingCheck now in after)
        {
            if (before.FirstOrDefault(c => c.Wall.Id == now.Wall.Id) is not { } was)
            {
                continue;
            }

            foreach (WallSegment segment in now.Line.Segments.Where(s => s.Method is null))
            {
                bool wasBraced = was.Line.Segments.Any(old => old.Method is not null
                                                              && old.Start < segment.End && segment.Start < old.End
                                                              && !(old.From == segment.From && old.To == segment.To));
                bool alreadySaid = was.Line.Segments.Any(old => old.From == segment.From && old.To == segment.To && old.Origin == AssignmentOrigin.MergedConflict);
                if (segment.Origin == AssignmentOrigin.MergedConflict && !alreadySaid)
                {
                    said.Add($"{now.Wall.Name}: the segment {segment.Label} merged braced segments with different methods, so it is not braced now; choose its method again.");
                }
                else if (wasBraced && segment.Origin != AssignmentOrigin.MergedConflict)
                {
                    said.Add($"{now.Wall.Name}: the segment {segment.Label} is a new segment where a braced one was, so it is not braced now; choose its method again.");
                }
            }
        }

        return [.. said];
    }

    private static string Name(IReadOnlyList<WallBracingCheck> checks, EntityId wall) => checks.First(c => c.Wall.Id == wall).Wall.Name;

    /// <summary>A citation-only change is announced only when the code itself changed (a pack switch), not for a relabelled segment.</summary>
    private static bool Announced(BracingChange change)
        => change.Kind != BracingChangeKind.CitationOnly || CodeOf(change.Before) != CodeOf(change.After);

    /// <summary>The code a passing or failing result was computed under (the only results a citation-only change is between).</summary>
    private static AdoptedCodeRef CodeOf(BracingResult result)
        => result is BracingResult.Passes p ? p.Citation.Code : ((BracingResult.Fails)result).Citation.Code;

    private static string Sentence(string wall, BracingChange change) => change.Kind switch
    {
        BracingChangeKind.PassToFail or BracingChangeKind.ToFail or BracingChangeKind.FailChanged =>
            NowFails(wall, (BracingResult.Fails)change.After),
        BracingChangeKind.FailToPass or BracingChangeKind.ToPass or BracingChangeKind.PassChanged =>
            NowPasses(wall, (BracingResult.Passes)change.After),
        BracingChangeKind.ToOutOfScope or BracingChangeKind.NoAnswerToOutOfScope or BracingChangeKind.OutOfScopeChanged =>
            $"{wall}'s bracing is now {Short(change.After)}.",
        BracingChangeKind.ToNoAnswer => $"{wall}'s bracing can no longer be checked: {Short(change.After)}.",
        BracingChangeKind.CitationOnly => $"{wall}'s bracing is unchanged, {Short(change.After)}, now under {CodeOf(change.After).ShortName}.",
        _ => $"{wall}'s bracing still cannot be checked: {Short(change.After)}.",
    };

    private static CheckWords NoDataWords(BracingResult.NoData n)
        => n.Code is { } code
            ? new CheckWords($"{n.Explanation} {CodeCheck.WhereToAddTables}", code.ToString(), string.Empty, string.Empty)
            : new CheckWords(n.Explanation, string.Empty, string.Empty, string.Empty);

    private static string Details(Citation citation, BracingWorking? working)
        => string.Join(
            "\n",
            citation.Trace.Select(match => $"How it was found: {match}")
                .Concat(working?.Lines ?? [])
                .Concat(citation.Footnotes.Select(note => $"Footnote {note.Id}: {note.Text}"))
                .Append($"Source: {citation.Source.Title}, {citation.Source.Location} ({citation.Source.Url}, retrieved {citation.Source.RetrievedOn:yyyy-MM-dd})"));

    private static string Limit(string explanation)
    {
        int at = explanation.IndexOf(" This wall line is outside", StringComparison.Ordinal);
        return at > 0 ? explanation[..at] : explanation;
    }

    private static string NowPasses(string wall, BracingResult.Passes p) => NowPassesText(wall, p.Provided, p.Required, p.Citation.Table);

    private static string NowFails(string wall, BracingResult.Fails f) => NowFailsText(wall, f.Provided, f.Required, f.Shortfall, f.Citation.Table);

    private static string ShortPasses(Length provided, Length required, string table)
        => $"passes, braced {Show(provided)} of {Show(required)} required ({table})";

    private static string ShortFails(Length provided, Length required, Length shortfall, string table)
        => $"SHORT by {Show(shortfall)}, braced {Show(provided)} of {Show(required)} required ({table})";

    private static string Show(Length length) => CellValue.Of(length).ToString();

    private static string Named(IEnumerable<string> inputs) => string.Join(", ", inputs.Select(input => CodeCheck.Input(input)));
}

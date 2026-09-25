using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building;

/// <summary>
/// The code packs napkin found: every one that loaded, and every one that did not with its problems
/// (docs/design/rules-engine-model.md §9.3). Read once from the packs roots; the check itself never
/// touches the disk.
/// </summary>
public sealed class CodePacks
{
    /// <summary>Builds the set from load results (a test's, or <see cref="Discover"/>'s).</summary>
    public CodePacks(IEnumerable<PackLoadResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        List<PackLoadResult> all = [.. results];
        Loaded = [.. all.OfType<PackLoadResult.Loaded>().Select(loaded => loaded.Pack)];
        Invalid = [.. all.OfType<PackLoadResult.Invalid>()];
    }

    /// <summary>No packs at all.</summary>
    public static CodePacks None { get; } = new([]);

    /// <summary>Every pack that loaded, in the order found.</summary>
    public ImmutableArray<LoadedPack> Loaded { get; }

    /// <summary>Every pack that did not, with its problems.</summary>
    public ImmutableArray<PackLoadResult.Invalid> Invalid { get; }

    /// <summary>Every pack under each packs root that exists, in root order (<see cref="PackLocations.All()"/> for the app).</summary>
    public static CodePacks Discover(IEnumerable<string> packsRoots)
    {
        ArgumentNullException.ThrowIfNull(packsRoots);
        return new CodePacks(packsRoots
            .Where(root => Directory.Exists(Path.Combine(root, "packs")))
            .SelectMany(root => PackCatalog.Discover(root)));
    }

    /// <summary>
    /// The pack a project's choice names, or why there is none: a locked choice takes exactly its
    /// revision, a following one the newest installed revision of the same pack.
    /// </summary>
    public CodeResolution Resolve(CodeChoice? choice)
    {
        if (choice is null)
        {
            return new CodeResolution(null, $"No code selected: choose one under {CodeCheck.WhereToChoose}.");
        }

        List<LoadedPack> same = [.. Loaded.Where(pack => pack.Manifest.Id == choice.PackId).OrderByDescending(pack => pack.Manifest.Revision)];
        if (choice.Mode == CodeMode.Following && same.Count > 0)
        {
            return new CodeResolution(same[0], null);
        }

        if (same.FirstOrDefault(pack => pack.Manifest.Revision == choice.Revision) is { } locked)
        {
            return new CodeResolution(locked, null);
        }

        if (same.Count > 0)
        {
            return new CodeResolution(
                null,
                $"This project is locked to code pack {choice.PackId} revision {choice.Revision}, and the installed pack is revision "
                + $"{string.Join(", ", same.Select(pack => pack.Manifest.Revision))}. Nothing is computed under a revision the project did not choose: "
                + $"lock to the installed one, or follow it, under {CodeCheck.WhereToChoose}.");
        }

        if (Invalid.FirstOrDefault(pack => pack.PackId == choice.PackId) is { } invalid)
        {
            return new CodeResolution(
                null,
                $"Code pack {choice.PackId} is installed but does not load: {string.Join("; ", invalid.Problems.Take(3))}.");
        }

        return new CodeResolution(null, $"Code pack {choice.PackId}, which this project chose, is not installed (docs/rules-engine.md says where packs go).");
    }
}

/// <summary>The pack a project's code choice resolves to, or why there is none.</summary>
/// <param name="Pack">The pack, or null.</param>
/// <param name="Problem">Why there is no pack, in plain words; null when there is one.</param>
public sealed record CodeResolution(LoadedPack? Pack, string? Problem);

/// <summary>One opening and its header result.</summary>
/// <param name="Opening">The opening.</param>
/// <param name="Result">What the adopted code says about its header.</param>
public sealed record OpeningCheck(Opening Opening, HeaderResult Result);

/// <summary>
/// The code check on walls' openings (issue #18): every opening's header, sized by the rules
/// engine from the project's adopted code, its site values and what the wall supports. Pure and
/// total: the same sketch and packs give the same results, every opening every time, and nothing is
/// stored (docs/design/rules-engine-model.md §7.3; docs/building.md).
/// </summary>
/// <remarks>
/// The request: the wall kind is <see cref="WallKind.ExteriorBearing"/> (the only kind napkin
/// draws yet, design §3.2); the header span is the opening's rough width; <c>supports</c> is the
/// wall's; the site is the project's. A pack table's jack and king stud counts are read as per
/// side of the opening.
/// </remarks>
public static class CodeCheck
{
    /// <summary>Where in the app the code and the site values are chosen.</summary>
    public const string WhereToChoose = "Edit → Adopted code and site";

    /// <summary>Where a person reads how to add tables.</summary>
    public const string WhereToAddTables = "Where to add tables: docs/rules-engine.md";

    /// <summary>The rules engine's site inputs for the project's typed values; null stays null.</summary>
    public static SiteInputs Site(SiteValues site)
    {
        ArgumentNullException.ThrowIfNull(site);
        return new SiteInputs(
            site.GroundSnowLoadPsf,
            site.UltimateWindSpeedMph,
            site.SeismicDesignCategory,
            site.FrostDepth,
            site.BuildingWidth,
            site.Source is { } source ? new InputProvenance(source.Text, source.On) : null);
    }

    /// <summary>The rules engine's code selection for the project's choice, or null when none is chosen.</summary>
    public static CodeSelection? Selection(CodeChoice? choice)
        => choice is null ? null : new CodeSelection(
            choice.PackId,
            choice.Revision,
            choice.Mode == CodeMode.Locked ? CodeLockMode.Locked : CodeLockMode.Following,
            choice.LockedOn);

    /// <summary>The header table a pack uses for the walls napkin draws, or null.</summary>
    public static HeaderSizingTable? Table(LoadedPack? pack)
        => pack?.Tables.FirstOrDefault(table => table.WallKind == WallKind.ExteriorBearing);

    /// <summary>The values a pack's header table declares for what a wall supports, in the table's order; empty with no table.</summary>
    public static ImmutableArray<string> SupportsChoices(LoadedPack? pack)
        => Table(pack)?.Inputs.FirstOrDefault(column => column.Name == "supports") is { } column ? [.. column.Values] : [];

    /// <summary>Every opening in every wall, with its header result.</summary>
    public static ImmutableArray<OpeningCheck> Of(Sketch sketch, CodePacks packs)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(packs);
        CodeResolution code = packs.Resolve(sketch.Code);
        return
        [
            .. Wall.All(sketch)
                .SelectMany(wall => Opening.In(sketch, wall))
                .Select(opening => new OpeningCheck(opening, For(sketch, opening, code))),
        ];
    }

    /// <summary>One opening's header result under a resolved code.</summary>
    public static HeaderResult For(Sketch sketch, Opening opening, CodeResolution code)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(code);

        if (code.Pack is not { } pack)
        {
            return new HeaderResult.NoData(NoDataReason.NoPackSelected, null, WallKind.ExteriorBearing, code.Problem!);
        }

        string? supports = opening.Wall.Box.WallInputs?.Supports;
        if (supports is null && Table(pack) is { } table)
        {
            return new HeaderResult.InputMissing(
                new ValueList<string>(["supports"]),
                table.Designation,
                pack.Code,
                $"Table {table.Designation} needs what {opening.Wall.Name} supports, which has not been chosen. "
                + "Choose it under Supports in the wall's panel; napkin never assumes it.");
        }

        HeaderRequest request = new(supports ?? string.Empty, WallKind.ExteriorBearing, opening.Width, Site(sketch.Site));
        return RulesEngine.For(pack).SizeHeader(request);
    }

    /// <summary>
    /// The framing options with the code check plugged in: a sized opening's jack and king counts
    /// and its header member (the library lumber the result names, as many plies as it says);
    /// anything else stays unsized.
    /// </summary>
    public static FramingOptions Framing(IEnumerable<OpeningCheck> checks, MaterialsLibrary library, FramingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(library);
        Dictionary<EntityId, HeaderResult.Sized> sized = checks
            .Where(check => check.Result is HeaderResult.Sized)
            .ToDictionary(check => check.Opening.Id, check => (HeaderResult.Sized)check.Result);

        return (options ?? new FramingOptions()) with
        {
            JacksPerSide = opening => sized.TryGetValue(opening.Id, out HeaderResult.Sized? s) ? s.JackStuds : null,
            KingsPerSide = opening => sized.TryGetValue(opening.Id, out HeaderResult.Sized? s) ? s.KingStuds : null,
            Header = opening => sized.TryGetValue(opening.Id, out HeaderResult.Sized? s) && library.TryFindLumber(s.Header.Nominal, out LumberStock lumber)
                ? new HeaderMember(s.Header.Plies, lumber)
                : null,
        };
    }

    /// <summary>The result in plain words for the part panel: a headline, the citation line, and the details behind it.</summary>
    public static CheckWords Words(HeaderResult result, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(library);
        return result switch
        {
            HeaderResult.Sized s => new CheckWords(
                $"Header {s.Header}, {Count(s.JackStuds, "jack stud")} and {Count(s.KingStuds, "king stud")} each side."
                + (library.TryFindLumber(s.Header.Nominal, out _) ? string.Empty : $" {s.Header.Nominal} is not in the materials library, so the header is not on the shopping list."),
                s.Citation.ToString(),
                Details(s.Citation)),
            HeaderResult.OutOfScope o => new CheckWords(
                $"This opening is beyond what Table {o.Limit.Table} covers: {Limit(o.Explanation)} napkin stops here: get this header engineered.",
                $"Limit: {o.Limit}",
                Details(o.Limit)),
            HeaderResult.InputMissing m => new CheckWords(
                $"Not checked: {Named(m.Inputs)} {(m.Inputs.Count == 1 ? "is" : "are")} not entered, and napkin never assumes a value. "
                + Where(m.Inputs),
                $"Table {m.Table}, {m.Code}",
                string.Empty),
            _ => NoDataWords((HeaderResult.NoData)result),
        };
    }

    private static CheckWords NoDataWords(HeaderResult.NoData n)
        => n.Code is { } code
            ? new CheckWords($"{n.Explanation} {WhereToAddTables}", code.ToString(), string.Empty)
            : new CheckWords(n.Explanation, string.Empty, string.Empty);

    /// <summary>A short form of a result for a list or the message bar: "(2) 2x10 (Table T row R)".</summary>
    public static string Short(HeaderResult result) => result switch
    {
        HeaderResult.Sized s => $"{s.Header}, {s.JackStuds} jack and {s.KingStuds} king each side (Table {s.Citation.Table} row {s.Citation.RowId})",
        HeaderResult.OutOfScope o => $"beyond Table {o.Limit.Table}: get it engineered",
        HeaderResult.InputMissing m => $"not checked: {Named(m.Inputs)} not entered",
        _ => "no data to check it against",
    };

    /// <summary>
    /// What a recompute changed, one sentence per opening whose result differs, the most serious
    /// first (design §7.3). Openings added or removed between the two are not changes of a result.
    /// </summary>
    public static ImmutableArray<string> Changes(IReadOnlyList<OpeningCheck> before, IReadOnlyList<OpeningCheck> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        HashSet<EntityId> both = [.. before.Select(check => check.Opening.Id).Intersect(after.Select(check => check.Opening.Id))];
        Dictionary<EntityId, string> names = after.ToDictionary(check => check.Opening.Id, check => check.Opening.Name);
        RecomputeReport report = Recompute.Diff(
            [.. before.Where(check => both.Contains(check.Opening.Id)).Select(check => KeyValuePair.Create(check.Opening.Id, check.Result))],
            [.. after.Where(check => both.Contains(check.Opening.Id)).Select(check => KeyValuePair.Create(check.Opening.Id, check.Result))]);

        return
        [
            .. report.Changes
                .Where(change => !SameRow(change))
                .OrderBy(change => Rank(change.Kind))
                .Select(change => Sentence(names[change.Element], change)),
        ];
    }

    /// <summary>
    /// The same member from the same row of the same code, only the inputs traced differently (a
    /// resize inside one band): not a change of the result, so not announced.
    /// </summary>
    private static bool SameRow(ResultChange change)
        => change is { Kind: ChangeKind.CitationOnly, Before: HeaderResult.Sized a, After: HeaderResult.Sized b }
           && a.Citation with { Trace = ValueList<BandMatch>.Empty } == b.Citation with { Trace = ValueList<BandMatch>.Empty };

    private static string Sentence(string name, ResultChange change) => change.Kind switch
    {
        ChangeKind.CitationOnly => $"Header for {name} is unchanged, {Cited((HeaderResult.Sized)change.After)}.",
        ChangeKind.SizedToSized => $"Header for {name} changed: {((HeaderResult.Sized)change.Before).Header} → {Short(change.After)}.",
        ChangeKind.SizedToOutOfScope or ChangeKind.NoAnswerToOutOfScope or ChangeKind.OutOfScopeChanged =>
            $"Header for {name} is now beyond Table {((HeaderResult.OutOfScope)change.After).Limit.Table}: get it engineered.",
        ChangeKind.OutOfScopeToSized or ChangeKind.NoAnswerToSized => $"Header for {name} is now sized: {Short(change.After)}.",
        ChangeKind.SizedToNoAnswer => $"Header for {name} is no longer sized: {Short(change.After)}.",
        ChangeKind.OutOfScopeToNoAnswer => $"Header for {name} can no longer be checked: {Short(change.After)}.",
        _ => $"Header for {name} still cannot be checked: {Short(change.After)}.",
    };

    private static string Cited(HeaderResult.Sized b)
        => $"{b.Header}, now cited from {b.Citation.Code.ShortName} rev {b.Citation.Code.Revision} Table {b.Citation.Table} row {b.Citation.RowId}";

    /// <summary>The order changes are said in: losing a size first (design §7.3), a citation-only change last.</summary>
    private static readonly ChangeKind[] Ranked =
    [
        ChangeKind.SizedToOutOfScope, ChangeKind.SizedToNoAnswer, ChangeKind.NoAnswerToOutOfScope, ChangeKind.OutOfScopeChanged,
        ChangeKind.SizedToSized, ChangeKind.OutOfScopeToSized, ChangeKind.NoAnswerToSized, ChangeKind.OutOfScopeToNoAnswer,
        ChangeKind.NoAnswerChanged, ChangeKind.CitationOnly,
    ];

    private static int Rank(ChangeKind kind) => Array.IndexOf(Ranked, kind);

    private static string Details(Napkin.Core.RulesEngine.Citation citation)
        => string.Join(
            "\n",
            citation.Trace.Select(match => $"How it was found: {match}")
                .Concat(citation.Footnotes.Select(note => $"Footnote {note.Id}: {note.Text}"))
                .Append($"Source: {citation.Source.Title}, {citation.Source.Location} ({citation.Source.Url}, retrieved {citation.Source.RetrievedOn:yyyy-MM-dd})"));

    /// <summary>The explanation's first sentence, the one that names the limit; the "get an engineer" sentence is said by the headline.</summary>
    private static string Limit(string explanation)
    {
        foreach (string tail in new[] { " This opening is outside", " This case is outside" })
        {
            int at = explanation.IndexOf(tail, StringComparison.Ordinal);
            if (at > 0)
            {
                return explanation[..at];
            }
        }

        return explanation;
    }

    private static string Where(IEnumerable<string> inputs)
    {
        List<string> said = [];
        List<string> all = [.. inputs];
        if (all.Contains("supports"))
        {
            said.Add("Choose what the wall supports under Supports in the wall's panel.");
        }

        if (all.Any(input => input != "supports" && input != "headerSpan"))
        {
            said.Add($"Enter the site values under {WhereToChoose}.");
        }

        return string.Join(" ", said);
    }

    private static string Named(IEnumerable<string> inputs) => string.Join(", ", inputs.Select(input => Input(input)));

    /// <summary>A table input's name in plain words.</summary>
    public static string Input(string name) => name switch
    {
        "supports" => "what the wall supports",
        "groundSnowLoad" => "the ground snow load",
        "ultimateWindSpeed" => "the wind speed",
        "seismicDesignCategory" => "the seismic design category",
        "frostDepth" => "the frost depth",
        "buildingWidth" => "the building width",
        _ => name,
    };

    private static string Count(int count, string what) => $"{count} {what}{(count == 1 ? string.Empty : "s")}";
}

/// <summary>A header result in plain words.</summary>
/// <param name="Headline">What it means, in a sentence or two.</param>
/// <param name="Citation">The citation line, as the engine gives it (empty when there is none).</param>
/// <param name="Details">The band trace, footnotes and source, one per line (empty when there is none).</param>
public sealed record CheckWords(string Headline, string Citation, string Details);

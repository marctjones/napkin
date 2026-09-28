using System.Collections.Immutable;
using System.Globalization;

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
            return new CodeResolution(null, CodeCheck.NoCodeSelectedText);
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
/// <param name="Result">
/// What the adopted code says about its header, or null when napkin does not check it: an opening
/// in a wall marked not bearing (renovation-sketches §4.3), whose <see cref="NotChecked"/> says why.
/// </param>
public sealed record OpeningCheck(Opening Opening, HeaderResult? Result)
{
    /// <summary>Why the header is not checked — "Wall 1 is marked not bearing, …" — or null when it is.</summary>
    public string? NotChecked { get; init; }

    /// <summary>
    /// The row a person entered for this opening that no longer applies, and which of its inputs
    /// moved (docs/design/manual-code-values.md §6.4); null when there is none. The result beside it
    /// is the engine's own: a stale row never shows a number.
    /// </summary>
    public StaleRow? Stale { get; init; }

    /// <summary>
    /// The row a person entered for this opening, now that napkin answers it from its own table
    /// (§5.2): the result beside it is napkin's, never the row's. Null when there is none.
    /// </summary>
    public EnteredHeader? Superseded { get; init; }
}

/// <summary>An entered row that no longer applies, and what moved since it was entered (manual-code-values §6.4).</summary>
/// <param name="Row">The row.</param>
/// <param name="Moved">Each input that differs from what the row was entered for, in the order §6.1 lists them.</param>
public sealed record StaleRow(EnteredHeader Row, ValueList<MovedInput> Moved);

/// <summary>One input that moved since a row was entered: "the header span moved from 3'-0" to 3'-6"".</summary>
/// <param name="Input">Which input, as the tables name it: <c>headerSpan</c>, <c>side</c>, <c>groundSnowLoad</c>, … and <c>pack</c> and <c>bearing</c>.</param>
/// <param name="Was">What it was when the row was entered, in words.</param>
/// <param name="Now">What it is now, in words.</param>
public sealed record MovedInput(string Input, string Was, string Now)
{
    /// <summary>"the header span moved from 3'-0" to 3'-6"".</summary>
    public override string ToString() => $"{CodeCheck.Input(Input)} moved from {Was} to {Now}";
}

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
    /// <summary>
    /// What the status line says after the adopted code changes: which code now resolves, and how
    /// many results changed, were newly flagged, or can no longer be computed. A result that had no
    /// answer and still has none (only the pack named in it differs) is not counted as changed.
    /// </summary>
    /// <example>"Now checking against ZZ BRACE B (…): every result recomputed; 2 changed, 1 newly flagged, none can no longer be computed."</example>
    public static string SwitchSummary(AdoptedCodeRef? code, RecomputeReport headers, BracingRecomputeReport bracing, DeckRecomputeReport? decks = null)
    {
        ValueList<DeckChange> deckChanges = decks?.Changes ?? ValueList<DeckChange>.Empty;
        int changed = headers.Changes.Count(change => change.Kind is not (ChangeKind.CitationOnly or ChangeKind.NoAnswerChanged))
                      + bracing.Changes.Count(change => change.Kind is not (BracingChangeKind.CitationOnly or BracingChangeKind.NoAnswerChanged))
                      + deckChanges.Count(change => change.Kind is not (DeckChangeKind.CitationOnly or DeckChangeKind.NoAnswerChanged or DeckChangeKind.SpanMoved));
        int flagged = headers.Changes.Count(change => change.Kind is ChangeKind.SizedToOutOfScope or ChangeKind.NoAnswerToOutOfScope or ChangeKind.EnteredToOutOfScope)
                      + bracing.NewlyFlagged.Count()
                      + (decks?.NewlyFlagged.Count() ?? 0);
        int lost = headers.Changes.Count(change => change.Kind is ChangeKind.SizedToNoAnswer or ChangeKind.OutOfScopeToNoAnswer or ChangeKind.EnteredToNoAnswer)
                   + bracing.NoLongerComputable.Count()
                   + (decks?.NoLongerComputable.Count() ?? 0);
        string under = code is null ? "No code resolves now" : $"Now checking against {PackLabel(code)}";
        return $"{under}: every result recomputed; {Tally(changed, "changed")}, {Tally(flagged, "newly flagged")}, "
               + $"{Tally(lost, "can no longer be computed")}.";

        static string Tally(int n, string words) => n == 0 ? $"none {words}" : $"{n} {words}";
    }

    /// <summary>Where in the app the code and the site values are chosen.</summary>
    public const string WhereToChoose = "Project → Adopted code and site";

    /// <summary>Where a person reads how to add tables.</summary>
    public const string WhereToAddTables = "Where to add tables: docs/rules-engine.md";

    /// <summary>The status line's text when no code is adopted at all.</summary>
    public const string NoCodeSelectedText = "No code selected: choose one under " + WhereToChoose + ".";

    /// <summary>A code named with its pack, in a sentence: "ZZ FRAME (IRC 2099, pack us-zz-frame rev 1)".</summary>
    /// <param name="code">The code.</param>
    public static string PackLabel(AdoptedCodeRef code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return $"{code.ShortName} ({code.BaseCode}, pack {code.PackId} rev {code.Revision})";
    }

    /// <summary>What the shopping list's code-check note starts with: "Code check under ZZ FRAME (…)", or "Code check" with no code.</summary>
    /// <param name="code">The code checked against, or null when none resolves.</param>
    public static string UnderHeading(AdoptedCodeRef? code) => code is null ? "Code check" : $"Code check under {PackLabel(code)}";

    /// <summary>The code window's lock note when nothing is chosen.</summary>
    public const string ChooseToLockNote = "Choose a code to lock it or let it follow.";

    /// <summary>The code window's lock note for a locked code: "Locked on 2026-09-25 to pack us-zz-frame revision 1."</summary>
    /// <param name="on">The day it was locked.</param>
    /// <param name="packId">The pack.</param>
    /// <param name="revision">The revision it is locked to.</param>
    public static string LockedNote(DateOnly on, string packId, int revision)
        => $"Locked on {on.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} to pack {packId} revision {revision}.";

    /// <summary>The code window's lock note for a code that follows its pack's newest revision.</summary>
    /// <param name="packId">The pack.</param>
    public static string FollowingNote(string packId)
        => $"Following pack {packId}: a newer revision is used when one is installed, and napkin says what changed.";

    /// <summary>What the code window adds when a pack has neither header tables nor bracing provisions.</summary>
    public const string NoBaseTablesNote = "Its base tables are not loaded: no header can be sized until they are (docs/rules-engine.md says how to add them).";

    /// <summary>What the code window adds when a pack has bracing provisions but no header table.</summary>
    public const string NoHeaderTablesNote = "It has no header table, so headers are not sized; walls' bracing is checked.";

    /// <summary>What the code window adds when a pack has header tables but no bracing provisions.</summary>
    public const string NoBracingNote = "It has no wall-bracing provisions, so no wall's bracing is checked.";

    /// <summary>The code window's status for a pack that loaded: "Checking against …." and what it cannot check.</summary>
    /// <param name="pack">The resolved pack.</param>
    public static string CheckingStatus(LoadedPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return $"Checking against {pack.Code}."
               + (pack.HasHeaderTables ? string.Empty
                   : pack.Bracing is null ? " " + NoBaseTablesNote
                   : " " + NoHeaderTablesNote)
               + (pack.HasHeaderTables && pack.Bracing is null ? " " + NoBracingNote : string.Empty);
    }

    /// <summary>The Supports picker's tooltip when the adopted code has no header table to choose from.</summary>
    /// <param name="shortName">The code's short name ("CT 2022").</param>
    public static string NoHeaderTableTip(string shortName)
        => $"{shortName} has no header table loaded, so there is nothing to choose from yet (docs/rules-engine.md).";

    /// <summary>A sized header's headline: "Header (1) 2x8, 1 jack stud and 1 king stud each side."</summary>
    /// <param name="header">The header as the table gives it ("(1) 2x8").</param>
    /// <param name="jackStuds">Jack studs each side.</param>
    /// <param name="kingStuds">King studs each side.</param>
    public static string HeaderText(string header, int jackStuds, int kingStuds)
        => $"Header {header}, {Count(jackStuds, "jack stud")} and {Count(kingStuds, "king stud")} each side.";

    /// <summary>The message bar's line when a header comes to be beyond its table: "Header for Window 1 is now beyond Table ZZ-HEADER: get it engineered."</summary>
    /// <param name="opening">The opening's name.</param>
    /// <param name="table">The table it is beyond.</param>
    public static string NowBeyondText(string opening, string table) => $"Header for {opening} is now beyond Table {table}: get it engineered.";

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
            site.RoofLiveLoadPsf,
            site.Source is { } source ? new InputProvenance(source.Text, source.On) : null);
    }

    /// <summary>The rules engine's code selection for the project's choice, or null when none is chosen.</summary>
    public static CodeSelection? Selection(CodeChoice? choice)
        => choice is null ? null : new CodeSelection(
            choice.PackId,
            choice.Revision,
            choice.Mode == CodeMode.Locked ? CodeLockMode.Locked : CodeLockMode.Following,
            choice.LockedOn);

    /// <summary>The header table a pack uses for a kind of wall (exterior-bearing unless said), or null.</summary>
    public static HeaderSizingTable? Table(LoadedPack? pack, WallKind kind = WallKind.ExteriorBearing)
        => pack?.Tables.FirstOrDefault(table => table.WallKind == kind);

    /// <summary>The values a pack's header table declares for what a wall supports, in the table's order; empty with no table.</summary>
    public static ImmutableArray<string> SupportsChoices(LoadedPack? pack, WallKind kind = WallKind.ExteriorBearing)
        => Table(pack, kind)?.Inputs.FirstOrDefault(column => column.Name == "supports") is { } column ? [.. column.Values] : [];

    /// <summary>The header table a wall's side asks for (renovation-sketches §4.3): interior-bearing for an interior wall, exterior otherwise.</summary>
    public static WallKind KindOf(Wall wall)
    {
        ArgumentNullException.ThrowIfNull(wall);
        return wall.Box.WallInputs?.Side == WallSide.Interior ? WallKind.InteriorBearing : WallKind.ExteriorBearing;
    }

    /// <summary>
    /// Why a not-bearing wall's opening is not checked, and the header the person chose for it:
    /// "Wall 1 is marked not bearing, so napkin does not size this header from the code. Header: (2) 2x6, your choice."
    /// </summary>
    public static string NotBearingText(Wall wall)
    {
        ArgumentNullException.ThrowIfNull(wall);
        return $"{wall.Name} is marked not bearing, so napkin does not size this header from the code. "
               + (wall.Box.WallInputs?.Header is { } header
                   ? $"Header: {header}, your choice."
                   : "No header chosen: choose one under Header in the wall's panel; until then the header buys nothing.");
    }

    /// <summary>What the panel says after "Not checked" for a demolished bearing wall (renovation-sketches §1.4).</summary>
    public const string BearingDemolishedText = "Removing a bearing wall needs an engineer; napkin does nothing here.";

    /// <summary>What the typed header's jacks and kings are, said wherever the frame is shown.</summary>
    public const string ChosenHeaderJacks = "1 jack and 1 king stud each side of a header you chose: napkin's placeholder counts, not a code result";

    /// <summary>
    /// Every opening in every wall of the building as it will be (<see cref="Sketch.After"/>,
    /// renovation-sketches §6.1), with its header result: a demolished wall or opening is in no check.
    /// </summary>
    public static ImmutableArray<OpeningCheck> Of(Sketch sketch, CodePacks packs)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(packs);
        return OfView(sketch.After(), packs);
    }

    /// <summary>Every opening in every wall of a view exactly as given — the before view, for the framing diff.</summary>
    public static ImmutableArray<OpeningCheck> OfView(Sketch view, CodePacks packs)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(packs);
        CodeResolution code = packs.Resolve(view.Code);
        return
        [
            .. Wall.All(view)
                .SelectMany(wall => Opening.In(view, wall))
                .Select(opening => Check(view, opening, code)),
        ];
    }

    /// <summary>
    /// One opening's check, routed by its wall's side and bearing (renovation-sketches §4.3): a wall
    /// marked not bearing is not checked; otherwise <see cref="For"/>.
    /// </summary>
    public static OpeningCheck Check(Sketch sketch, Opening opening, CodeResolution code)
    {
        ArgumentNullException.ThrowIfNull(opening);
        EnteredHeader? row = opening.Box.EnteredHeader;
        if (opening.Wall.Box.WallInputs?.Bearing == false)
        {
            return new OpeningCheck(opening, null) { NotChecked = NotBearingText(opening.Wall) + (row is null ? string.Empty : " " + EnteredKeptText) };
        }

        HeaderResult engine = For(sketch, opening, code);
        if (row is null)
        {
            return new OpeningCheck(opening, engine);
        }

        // manual-code-values §5.1: a row is consulted only where napkin has nothing — a pack chosen,
        // the wall's side and bearing said, and no table — and only while every input it was
        // entered for is exactly the live one. Wherever a table exists, napkin's answer stands and
        // the row is superseded (§5.2); it is never an override (§5.3).
        ValueList<MovedInput> moved = Moved(sketch, opening, code, row);
        return engine switch
        {
            HeaderResult.NoData { Reason: NoDataReason.NoTableForWallKind } when moved.Count == 0 => new OpeningCheck(opening, Entered(row)),
            HeaderResult.NoData => new OpeningCheck(opening, engine) { Stale = new StaleRow(row, moved) },
            HeaderResult.InputMissing missing when missing.Inputs.Any(input => input is "side" or "bearing") =>
                new OpeningCheck(opening, engine) { Stale = new StaleRow(row, moved) },

            // Sized, OutOfScope, or InputMissing for a table's own input (what the wall supports, a
            // site value): each means a table exists.
            _ => new OpeningCheck(opening, engine) { Superseded = row },
        };
    }

    /// <summary>What a not-bearing wall's Not checked sentence adds for an opening that carries an entered row (§5.1): it is kept, and not used.</summary>
    public const string EnteredKeptText = "The header row entered by hand for this opening is kept but not used: napkin does not check a not-bearing wall's headers.";

    /// <summary>
    /// The entered row as a result: its member and counts, and where it came from, with the inputs
    /// it was entered for in words — never a citation, never napkin's (§3.1).
    /// </summary>
    private static HeaderResult.Entered Entered(EnteredHeader row)
        => new(
            new MemberSpec(row.Plies, row.Lumber),
            row.JackStuds,
            row.KingStuds,
            new EnteredRow(
                row.Citation.Code,
                row.Citation.Table,
                row.Citation.Location,
                row.Citation.Notes,
                row.EnteredBy,
                row.EnteredOn,
                row.For.Pack,
                new ValueList<string>([.. Recorded(row.For).Select(input => $"{Input(input.Name)}: {input.Words}")])));

    /// <summary>
    /// Every input an entered row records, with what it was, in the order §6.1 lists them: the
    /// pack, the wall's side, what it supports, the header span and the six site values.
    /// </summary>
    private static IEnumerable<(string Name, string Words)> Recorded(EnteredHeaderInputs was)
    {
        yield return ("pack", $"pack {was.Pack}");
        yield return ("side", SideWords(was.Side));
        yield return ("supports", $"\"{was.Supports}\"");
        yield return ("headerSpan", LengthWords(was.Span));
        yield return ("groundSnowLoad", Psf(was.GroundSnowLoadPsf));
        yield return ("ultimateWindSpeed", Mph(was.UltimateWindSpeedMph));
        yield return ("seismicDesignCategory", was.SeismicDesignCategory ?? NotEnteredWords);
        yield return ("frostDepth", LengthWords(was.FrostDepth));
        yield return ("buildingWidth", LengthWords(was.BuildingWidth));
        yield return ("roofLiveLoad", Psf(was.RoofLiveLoadPsf));
    }

    /// <summary>
    /// Which of a row's recorded inputs differ from the live ones, compared exactly, field by field
    /// (§6.1): a site value going from not entered to entered is a move; so is another pack, and a
    /// wall whose side or bearing is no longer said.
    /// </summary>
    private static ValueList<MovedInput> Moved(Sketch sketch, Opening opening, CodeResolution code, EnteredHeader row)
    {
        WallInputs? wall = opening.Wall.Box.WallInputs;
        SiteValues site = sketch.Site;
        EnteredHeaderInputs was = row.For;

        // Compared as values, never as their words: a length 1/1024″ off still moved.
        (bool Same, string Now)[] live =
        [
            (code.Pack?.Manifest.Id == was.Pack, code.Pack is { } pack ? $"pack {pack.Manifest.Id}" : "no code"),
            (wall?.Side == was.Side, wall?.Side is { } side ? SideWords(side) : "not said"),
            (wall?.Supports == was.Supports, wall?.Supports is { } supports ? $"\"{supports}\"" : "not chosen"),
            (opening.Width == was.Span, LengthWords(opening.Width)),
            (site.GroundSnowLoadPsf == was.GroundSnowLoadPsf, Psf(site.GroundSnowLoadPsf)),
            (site.UltimateWindSpeedMph == was.UltimateWindSpeedMph, Mph(site.UltimateWindSpeedMph)),
            (site.SeismicDesignCategory == was.SeismicDesignCategory, site.SeismicDesignCategory ?? NotEnteredWords),
            (site.FrostDepth == was.FrostDepth, LengthWords(site.FrostDepth)),
            (site.BuildingWidth == was.BuildingWidth, LengthWords(site.BuildingWidth)),
            (site.RoofLiveLoadPsf == was.RoofLiveLoadPsf, Psf(site.RoofLiveLoadPsf)),
        ];

        List<MovedInput> moved =
        [
            .. Recorded(was).Zip(live)
                .Where(pair => !pair.Second.Same)
                .Select(pair => new MovedInput(pair.First.Name, pair.First.Words, pair.Second.Now)),
        ];

        if (wall?.Bearing is null)
        {
            moved.Add(new MovedInput("bearing", "bearing", "not said"));
        }

        return new ValueList<MovedInput>([.. moved]);
    }

    private const string NotEnteredWords = "not entered";

    private static string SideWords(WallSide side) => side == WallSide.Interior ? "interior" : "exterior";

    private static string Psf(int? psf) => psf is { } value ? $"{value} psf" : NotEnteredWords;

    private static string Mph(int? mph) => mph is { } value ? $"{value} mph" : NotEnteredWords;

    private static string LengthWords(Length? length) => length is { } value ? value.Format(LengthFormat.Default).Text : NotEnteredWords;

    /// <summary>One opening's header result under a resolved code.</summary>
    public static HeaderResult For(Sketch sketch, Opening opening, CodeResolution code)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(code);

        WallKind kind = KindOf(opening.Wall);
        if (code.Pack is not { } pack)
        {
            return new HeaderResult.NoData(NoDataReason.NoPackSelected, null, kind, code.Problem!);
        }

        // Which table, and whether any: napkin never assumes a wall's side or whether it bears.
        WallInputs? inputs = opening.Wall.Box.WallInputs;
        List<string> unsaid = [];
        List<string> say = [];
        if (inputs?.Side is null)
        {
            unsaid.Add("side");
            say.Add($"Say whether {opening.Wall.Name} is exterior or interior (Part panel).");
        }

        if (inputs?.Bearing is null)
        {
            unsaid.Add("bearing");
            say.Add($"Say whether {opening.Wall.Name} is bearing (Part panel).");
        }

        if (unsaid.Count > 0)
        {
            return new HeaderResult.InputMissing(new ValueList<string>([.. unsaid]), Table(pack, kind)?.Designation ?? string.Empty, pack.Code, string.Join(" ", say));
        }

        string? supports = inputs!.Supports;
        if (supports is null && Table(pack, kind) is { } table)
        {
            return new HeaderResult.InputMissing(
                new ValueList<string>(["supports"]),
                table.Designation,
                pack.Code,
                $"Table {table.Designation} needs what {opening.Wall.Name} supports, which has not been chosen. "
                + "Choose it under Supports in the wall's panel; napkin never assumes it.");
        }

        HeaderRequest request = new(supports ?? string.Empty, kind, opening.Width, Site(sketch.Site));
        return RulesEngine.For(pack).SizeHeader(request);
    }

    /// <summary>
    /// The framing options with the code check plugged in: a sized opening's jack and king counts
    /// and its header member (the library lumber the result names, as many plies as it says) — and
    /// an entered row's the same way (manual-code-values §3.4); anything else stays unsized.
    /// </summary>
    public static FramingOptions Framing(IEnumerable<OpeningCheck> checks, MaterialsLibrary library, FramingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(library);
        List<OpeningCheck> all = [.. checks];
        Dictionary<EntityId, (MemberSpec Header, int JackStuds, int KingStuds)> sized = [];
        foreach (OpeningCheck check in all)
        {
            if (check.Result is HeaderResult.Sized s)
            {
                sized[check.Opening.Id] = (s.Header, s.JackStuds, s.KingStuds);
            }
            else if (check.Result is HeaderResult.Entered e)
            {
                sized[check.Opening.Id] = (e.Header, e.JackStuds, e.KingStuds);
            }
        }

        // A not-bearing wall's openings take the header the person typed, with napkin's placeholder
        // jack and king (one each side), said so (renovation-sketches §4.3).
        Dictionary<EntityId, TypedHeader?> chosen = all
            .Where(check => check.NotChecked is not null)
            .ToDictionary(check => check.Opening.Id, check => check.Opening.Wall.Box.WallInputs?.Header);

        return (options ?? new FramingOptions()) with
        {
            JacksPerSide = opening => sized.TryGetValue(opening.Id, out var s) ? s.JackStuds : chosen.ContainsKey(opening.Id) ? 1 : null,
            KingsPerSide = opening => sized.TryGetValue(opening.Id, out var s) ? s.KingStuds : null,
            Header = opening => sized.TryGetValue(opening.Id, out var s)
                ? library.TryFindLumber(s.Header.Nominal, out LumberStock lumber) ? new HeaderMember(s.Header.Plies, lumber) : null
                : chosen.TryGetValue(opening.Id, out TypedHeader? typed) && typed is { } t && library.TryFindLumber(t.Lumber, out LumberStock picked)
                    ? new HeaderMember(t.Plies, picked)
                    : null,
            Chosen = opening => chosen.ContainsKey(opening.Id),
        };
    }

    /// <summary>A check in plain words: <see cref="Words(HeaderResult, MaterialsLibrary)"/>, or "Not checked: …" for a not-bearing wall's opening.</summary>
    public static CheckWords Words(OpeningCheck check, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(check);
        if (check.Result is not { } result)
        {
            return new CheckWords($"Not checked: {check.NotChecked}", string.Empty, string.Empty, string.Empty);
        }

        CheckWords words = Words(result, library);
        return check.Stale is { } stale ? words with { Headline = $"{words.Headline} {StaleText(stale)}" }
            : check.Superseded is { } row ? words with { Headline = $"{words.Headline} {SupersededText(row)}" }
            : words;
    }

    /// <summary>
    /// "The row ENTERED BY HAND by A. Person on 2026-09-27 no longer applies: the header span moved
    /// from 3'-0" to 3'-6". Enter the row again, or remove it." (manual-code-values §6.4).
    /// </summary>
    public static string StaleText(StaleRow stale)
    {
        ArgumentNullException.ThrowIfNull(stale);
        return $"The row {EnteredRow.Tag} by {By(stale.Row)} no longer applies: {string.Join("; ", stale.Moved)}. Enter the row again, or remove it.";
    }

    /// <summary>
    /// "napkin now answers this from its own table. The row ENTERED BY HAND by A. Person on
    /// 2026-09-27 said (2) 2x10, 1 jack and 2 king each side; remove it." (§5.2).
    /// </summary>
    public static string SupersededText(EnteredHeader row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return $"napkin now answers this from its own table. The row {EnteredRow.Tag} by {By(row)} said "
               + $"({row.Plies}) {row.Lumber}, {row.JackStuds} jack and {row.KingStuds} king each side; remove it.";
    }

    private static string By(EnteredHeader row) => $"{row.EnteredBy} on {row.EnteredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    /// <summary>A check's short form for a list: <see cref="Short(HeaderResult)"/>, or "not checked: not bearing, (2) 2x6 your choice".</summary>
    public static string Short(OpeningCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return check.Result is { } result
            ? Short(result)
            : check.Opening.Wall.Box.WallInputs?.Header is { } header
                ? $"not checked: not bearing, {header} your choice"
                : "not checked: not bearing, no header chosen";
    }

    /// <summary>The result in plain words for the part panel: a headline, the citation line, and the details behind it.</summary>
    public static CheckWords Words(HeaderResult result, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(library);
        return result switch
        {
            HeaderResult.Sized s => new CheckWords(
                HeaderText(s.Header.ToString(), s.JackStuds, s.KingStuds)
                + (library.TryFindLumber(s.Header.Nominal, out _) ? string.Empty : $" {s.Header.Nominal} is not in the materials library, so the header is not on the shopping list."),
                s.Citation.ToString(),
                Details(s.Citation),
                s.Citation.Interpolation?.Summary(s.Citation.Code) ?? string.Empty),
            HeaderResult.OutOfScope o => new CheckWords(
                $"This opening is beyond what Table {o.Limit.Table} covers: {Limit(o.Explanation)} napkin stops here: get this header engineered.",
                $"Limit: {o.Limit}",
                Details(o.Limit),
                o.Limit.Interpolation?.Summary(o.Limit.Code) ?? string.Empty),
            HeaderResult.InputMissing m when m.Inputs.Any(input => input is "side" or "bearing") => new CheckWords(
                $"Not checked: {m.Explanation}",
                m.Table.Length > 0 ? $"Table {m.Table}, {m.Code}" : string.Empty,
                string.Empty,
                string.Empty),
            HeaderResult.InputMissing m => new CheckWords(
                $"Not checked: {Named(m.Inputs)} {(m.Inputs.Count == 1 ? "is" : "are")} not entered, and napkin never assumes a value. "
                + Where(m.Inputs),
                $"Table {m.Table}, {m.Code}",
                string.Empty,
                string.Empty),
            HeaderResult.Entered e => new CheckWords(
                $"{EnteredRow.Tag} — {HeaderText(e.Header.ToString(), e.JackStuds, e.KingStuds)}"
                + (library.TryFindLumber(e.Header.Nominal, out _) ? string.Empty : $" {e.Header.Nominal} is not in the materials library, so the header is not on the shopping list."),
                e.Entry.ToString(),
                string.Join("\n", e.Entry.Inputs.Select(input => $"Entered for {input}").Concat(e.Entry.Notes is { } notes ? [$"Notes: {notes}"] : [])),
                string.Empty),
            HeaderResult.NoData n => NoDataWords(n),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "A header result napkin does not know how to say."),
        };
    }

    private static CheckWords NoDataWords(HeaderResult.NoData n)
        => n.Code is { } code
            ? new CheckWords($"{n.Explanation} {WhereToAddTables}", code.ToString(), string.Empty, string.Empty)
            : new CheckWords(n.Explanation, string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// A short form of a result for a list or the message bar: "(2) 2x10 (Table T row R)", plus
    /// UNREVIEWED (design §13 Decision 5) until the adopted pack is signed off. Uses
    /// <see cref="AdoptedCodeRef.UnreviewedFragment"/>, not <see cref="AdoptedCodeRef.UnreviewedSentence"/>:
    /// callers (the shopping list's Framing note, <see cref="Sentence"/>) supply their own closing
    /// period, so the fragment must not add a second one.
    /// </summary>
    public static string Short(HeaderResult result) => result switch
    {
        HeaderResult.Sized s => $"{s.Header}, {s.JackStuds} jack and {s.KingStuds} king each side (Table {s.Citation.Table} row {s.Citation.RowId}){s.Citation.Code.UnreviewedFragment}",
        HeaderResult.OutOfScope o => $"beyond Table {o.Limit.Table}: get it engineered{o.Limit.Code.UnreviewedFragment}",
        HeaderResult.InputMissing m => $"not checked: {Named(m.Inputs)} not entered",
        HeaderResult.Entered e => $"{e.Header}, {e.JackStuds} jack and {e.KingStuds} king each side "
                                  + $"({EnteredRow.Tag} — {e.Entry.EnteredBy}, {e.Entry.Code} {e.Entry.Table} {e.Entry.Location}; not napkin's data)",
        _ => "no data to check it against",
    };

    /// <summary>
    /// What a recompute changed, one sentence per opening whose result differs, the most serious
    /// first (design §7.3). Openings added or removed between the two are not changes of a result.
    /// </summary>
    public static ImmutableArray<string> Changes(IReadOnlyList<OpeningCheck> before, IReadOnlyList<OpeningCheck> after)
    {
        RecomputeReport report = Report(before, after);
        Dictionary<EntityId, OpeningCheck> now = after.ToDictionary(check => check.Opening.Id);

        return
        [
            .. report.Changes
                .Where(change => !SameRow(change))
                .OrderBy(change => Rank(change.Kind))
                .Select(change => Sentence(now[change.Element], change)),
        ];
    }

    /// <summary>The engine's diff over the openings present both before and after.</summary>
    public static RecomputeReport Report(IReadOnlyList<OpeningCheck> before, IReadOnlyList<OpeningCheck> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        // An opening napkin does not check (a not-bearing wall's) has no result to compare.
        HashSet<EntityId> both =
        [
            .. before.Where(check => check.Result is not null).Select(check => check.Opening.Id)
                .Intersect(after.Where(check => check.Result is not null).Select(check => check.Opening.Id)),
        ];
        return Recompute.Diff(
            [.. before.Where(check => both.Contains(check.Opening.Id)).Select(check => KeyValuePair.Create(check.Opening.Id, check.Result!))],
            [.. after.Where(check => both.Contains(check.Opening.Id)).Select(check => KeyValuePair.Create(check.Opening.Id, check.Result!))]);
    }

    /// <summary>
    /// The same member from the same row of the same code, only the inputs traced differently (a
    /// resize inside one band): not a change of the result, so not announced.
    /// </summary>
    private static bool SameRow(ResultChange change)
        => change is { Kind: ChangeKind.CitationOnly, Before: HeaderResult.Sized a, After: HeaderResult.Sized b }
           && a.Citation with { Trace = ValueList<BandMatch>.Empty } == b.Citation with { Trace = ValueList<BandMatch>.Empty };

    private static string Sentence(OpeningCheck after, ResultChange change)
    {
        string name = after.Opening.Name;
        return change.Kind switch
        {
            ChangeKind.EnteredToOutOfScope when change.After is HeaderResult.OutOfScope o =>
                $"Header for {name} is now beyond Table {o.Limit.Table} under {o.Limit.Code.ShortName}'s own table: get it engineered. "
                + $"The row {EnteredRow.Tag} ({((HeaderResult.Entered)change.Before).Header}) is superseded; remove it.",
            ChangeKind.EnteredToNoAnswer => after.Stale is { } stale
                ? $"Header for {name}: the row {EnteredRow.Tag} no longer applies ({string.Join("; ", stale.Moved.Select(m => $"{m.Input} {m.Was} → {m.Now}"))}); {Short(change.After)}."
                : $"Header for {name}: the row {EnteredRow.Tag} was removed; {Short(change.After)}.",
            ChangeKind.EnteredToSized when change.Before is HeaderResult.Entered e && change.After is HeaderResult.Sized b =>
                b.Header == e.Header && b.JackStuds == e.JackStuds && b.KingStuds == e.KingStuds
                    ? $"Header for {name} is now sized by napkin: {Short(b)} — the same as the row {EnteredRow.Tag}. Remove the entered row."
                    : $"Header for {name} is now sized by napkin: {Short(b)} — instead of the row {EnteredRow.Tag} ({e.Header}). Remove the entered row.",
            ChangeKind.EnteredChanged => $"Header for {name}'s entered row changed: {((HeaderResult.Entered)change.Before).Header} → {Short(change.After)}.",
            ChangeKind.ToEntered => $"Header for {name} is now entered by hand: {Short(change.After)}.",
            _ => Sentence(name, change),
        };
    }

    private static string Sentence(string name, ResultChange change) => change.Kind switch
    {
        ChangeKind.CitationOnly => $"Header for {name} is unchanged, {Cited((HeaderResult.Sized)change.After)}.",
        ChangeKind.SizedToSized => $"Header for {name} changed: {((HeaderResult.Sized)change.Before).Header} → {Short(change.After)}.",
        ChangeKind.SizedToOutOfScope or ChangeKind.NoAnswerToOutOfScope or ChangeKind.OutOfScopeChanged =>
            NowBeyondText(name, ((HeaderResult.OutOfScope)change.After).Limit.Table),
        ChangeKind.OutOfScopeToSized or ChangeKind.NoAnswerToSized => $"Header for {name} is now sized: {Short(change.After)}.",
        ChangeKind.SizedToNoAnswer => $"Header for {name} is no longer sized: {Short(change.After)}.",
        ChangeKind.OutOfScopeToNoAnswer => $"Header for {name} can no longer be checked: {Short(change.After)}.",
        _ => $"Header for {name} still cannot be checked: {Short(change.After)}.",
    };

    private static string Cited(HeaderResult.Sized b)
        => $"{b.Header}, now cited from {b.Citation.Code.ShortName} rev {b.Citation.Code.Revision} Table {b.Citation.Table} row {b.Citation.RowId}";

    /// <summary>The order changes are said in: losing a size first (design §7.3), a citation-only change last.</summary>
    /// <remarks>An entered row's kinds sit beside their sized twins (manual-code-values §6.3).</remarks>
    private static readonly ChangeKind[] Ranked =
    [
        ChangeKind.SizedToOutOfScope, ChangeKind.EnteredToOutOfScope, ChangeKind.SizedToNoAnswer, ChangeKind.EnteredToNoAnswer,
        ChangeKind.NoAnswerToOutOfScope, ChangeKind.OutOfScopeChanged, ChangeKind.SizedToSized, ChangeKind.EnteredToSized,
        ChangeKind.EnteredChanged, ChangeKind.OutOfScopeToSized, ChangeKind.NoAnswerToSized, ChangeKind.ToEntered,
        ChangeKind.OutOfScopeToNoAnswer, ChangeKind.NoAnswerChanged, ChangeKind.CitationOnly,
    ];

    private static int Rank(ChangeKind kind) => Array.IndexOf(Ranked, kind);

    /// <summary>The band trace, interpolation, footnotes and source behind a result, plus UNREVIEWED (design §13 Decision 5) until the pack is signed off.</summary>
    private static string Details(Napkin.Core.RulesEngine.Citation citation)
        => string.Join(
            "\n",
            citation.Trace.Select(match => $"How it was found: {match}")
                .Concat(citation.Interpolation is { } working ? [$"Interpolation: {working}"] : [])
                .Concat(citation.Footnotes.Select(note => $"Footnote {note.Id}: {note.Text}"))
                .Append($"Source: {citation.Source.Title}, {citation.Source.Location} ({citation.Source.Url}, retrieved {citation.Source.RetrievedOn:yyyy-MM-dd})"))
           + citation.Code.UnreviewedSentence;

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
        "roofLiveLoad" => "the roof live load",
        "side" => "which side the wall is on",
        "bearing" => "whether the wall is bearing",
        "headerSpan" => "the header span",
        "pack" => "the adopted code",
        _ => name,
    };

    private static string Count(int count, string what) => $"{count} {what}{(count == 1 ? string.Empty : "s")}";
}

/// <summary>A header result in plain words.</summary>
/// <param name="Headline">What it means, in a sentence or two.</param>
/// <param name="Citation">The citation line, as the engine gives it (empty when there is none).</param>
/// <param name="Details">The band trace, footnotes and source, one per line (empty when there is none).</param>
/// <param name="Interpolation">
/// For a span interpolated by a footnote: "Interpolated between the 30 psf row (…) and the 50 psf row (…)
/// (CT 2022 footnote e, p. 145)"; empty otherwise.
/// </param>
public sealed record CheckWords(string Headline, string Citation, string Details, string Interpolation);

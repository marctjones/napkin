using System.Globalization;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.Interop.Pdf;

/// <summary>Where a rules-engine result leaves a permit set (docs/design/permit-set.md §3).</summary>
public enum PermitStatus
{
    /// <summary>napkin gave an answer from the adopted code's tables — a size, a pass, or a shortfall.</summary>
    Sized,

    /// <summary>No answer: beyond the table, an input not entered, or no table or provision for it.</summary>
    NotSized,

    /// <summary>Not a code question here: an opening in a wall said not to bear, whose header is the person's own.</summary>
    NotChecked,
}

/// <summary>
/// One rules-engine result as a permit set prints it: what it is about, whether it is sized, the
/// panel's own words (so a citation and UNREVIEWED print as on screen), and — when it is not sized —
/// what to look up to size it by hand, for the W1 worksheet (docs/design/permit-set.md §3.1).
/// </summary>
/// <param name="What">"Window 1: header", "Wall 1: bracing", "Deck 1: frost".</param>
/// <param name="Status">Sized, not sized, or not checked.</param>
/// <param name="Lines">The panel's words: a result's headline, citation and interpolation, or a deck line.</param>
/// <param name="Lookup">
/// For an item not sized: the adopted code and table to look in and the inputs already known. Empty
/// otherwise.
/// </param>
public sealed record PermitItem(string What, PermitStatus Status, IReadOnlyList<string> Lines, string Lookup);

/// <summary>
/// Every rules-engine result in a design, as a permit set's C1 code page lists them and its W1
/// worksheet asks after the unsized ones (docs/design/permit-set.md §2, §3, §3.1).
/// </summary>
public static class PermitItems
{
    static readonly LengthFormat Words = LengthFormat.Default;

    /// <summary>Every result: openings' headers, walls' bracing, then each deck's frame and its check lines.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="packs">The code packs napkin found.</param>
    /// <param name="library">The materials library.</param>
    public static IReadOnlyList<PermitItem> Of(Sketch sketch, CodePacks packs, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(library);

        CodeResolution code = packs.Resolve(sketch.Code);
        List<PermitItem> items = [];
        foreach (OpeningCheck check in CodeCheck.Of(sketch, packs))
        {
            CheckWords words = CodeCheck.Words(check, library);
            PermitStatus status = check.Result switch
            {
                null => PermitStatus.NotChecked,
                HeaderResult.Sized => PermitStatus.Sized,
                _ => PermitStatus.NotSized,
            };
            items.Add(new PermitItem(
                $"{check.Opening.Name}: header",
                status,
                Lines(words),
                status == PermitStatus.NotSized ? HeaderLookup(sketch, check.Opening, code) : string.Empty));
        }

        foreach (WallBracingCheck check in BracingCheck.Of(sketch, packs))
        {
            PermitStatus status = check.Result is BracingResult.Passes or BracingResult.Fails ? PermitStatus.Sized : PermitStatus.NotSized;
            items.Add(new PermitItem(
                $"{check.Wall.Name}: bracing",
                status,
                Lines(BracingCheck.Words(check.Result)),
                status == PermitStatus.NotSized ? BracingLookup(sketch, check, code) : string.Empty));
        }

        foreach (DeckChecks deck in DeckCheck.Of(sketch, packs, library))
        {
            string known = DeckKnown(deck.Deck);
            if (deck.Framing is null)
            {
                items.Add(new PermitItem($"{deck.Deck.Name}: frame", PermitStatus.NotSized, [deck.Refusal!.Text], $"Look up: nothing yet — {deck.Refusal.Text} Known: {known}."));
                continue;
            }

            foreach (DeckCheckLine line in deck.Lines)
            {
                PermitStatus status = line.Result switch
                {
                    DeckResult.Passes or DeckResult.Short or DeckResult.Sized => PermitStatus.Sized,
                    null when !line.Unanswered => PermitStatus.Sized,
                    _ => PermitStatus.NotSized,
                };
                items.Add(new PermitItem(
                    $"{deck.Deck.Name}: {Kind(line.Kind)}",
                    status,
                    [line.Text],
                    status == PermitStatus.NotSized ? $"Look up: {DeckWhere(line, code)}. Known: {known}." : string.Empty));
            }
        }

        return items;
    }

    /// <summary>
    /// The banner a set carries when anything is not sized: "NOT A COMPLETE PERMIT SET — 2 items are not
    /// sized; see C1 and W1" (docs/design/permit-set.md §3, §3.1); null when everything is sized.
    /// </summary>
    /// <param name="items">The set's items.</param>
    public static string? Banner(IReadOnlyList<PermitItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        int unsized = items.Count(item => item.Status == PermitStatus.NotSized);
        return unsized switch
        {
            0 => null,
            1 => "NOT A COMPLETE PERMIT SET — 1 item is not sized; see C1 and W1",
            _ => $"NOT A COMPLETE PERMIT SET — {unsized.ToString(CultureInfo.InvariantCulture)} items are not sized; see C1 and W1",
        };
    }

    /// <summary>A deck check line's subject in words: "joists", "beam", "ledger".</summary>
    public static string Kind(DeckCheckKind kind) => kind switch
    {
        DeckCheckKind.Joists => "joists",
        DeckCheckKind.Cantilever => "joist cantilever",
        DeckCheckKind.Beam => "beam",
        DeckCheckKind.Ledger => "ledger",
        DeckCheckKind.EndPosts => "end posts",
        DeckCheckKind.MiddlePosts => "middle posts",
        DeckCheckKind.Footing => "footing",
        DeckCheckKind.Frost => "frost",
        DeckCheckKind.Guard => "guard",
        _ => "stair",
    };

    static List<string> Lines(CheckWords words) =>
        [.. ((string[])[words.Headline, words.Citation, words.Interpolation]).Where(line => line.Length > 0)];

    static string Text(Length length) => length.Format(Words).Text;

    /// <summary>The adopted code a lookup is in, as a result cites it, or why there is none.</summary>
    static string CodeName(CodeResolution code) => code.Pack is { } pack
        ? $"{pack.Code.ShortName} ({pack.Code.BaseCode}), pack {pack.Code.PackId} rev {pack.Code.Revision.ToString(CultureInfo.InvariantCulture)}"
        : code.Problem ?? CodeCheck.NoCodeSelectedText;

    static string Site(Sketch sketch) => string.Join(
        "; ",
        sketch.Site.GroundSnowLoadPsf is { } snow ? $"ground snow load {snow.ToString(CultureInfo.InvariantCulture)} psf" : "ground snow load: not entered",
        sketch.Site.UltimateWindSpeedMph is { } wind ? $"ultimate wind speed {wind.ToString(CultureInfo.InvariantCulture)} mph" : "ultimate wind speed: not entered",
        sketch.Site.SeismicDesignCategory is { } seismic ? $"seismic design category {seismic}" : "seismic design category: not entered");

    /// <summary>A header's lookup: the code and the table its wall's side asks for, the span, what the wall supports, the site.</summary>
    static string HeaderLookup(Sketch sketch, Opening opening, CodeResolution code)
    {
        WallKind kind = CodeCheck.KindOf(opening.Wall);
        WallInputs? inputs = opening.Wall.Box.WallInputs;
        string table = code.Pack is null
            ? string.Empty
            : CodeCheck.Table(code.Pack, kind) is { } found ? $", Table {found.Designation}" : ", which has no header table for this wall";
        string side = inputs?.Side switch
        {
            WallSide.Exterior => "exterior",
            WallSide.Interior => "interior",
            _ => "side not said",
        };
        string supports = inputs?.Supports is { } what ? $"the wall supports {what}" : "what the wall supports: not chosen";
        return $"Look up: {CodeName(code)}{table}. Known: {opening.Wall.Name}, {side}, bearing; header span {Text(opening.Width)}; {supports}; {Site(sketch)}.";
    }

    /// <summary>A wall line's bracing lookup: the code and section, the line's length and height, the site.</summary>
    static string BracingLookup(Sketch sketch, WallBracingCheck check, CodeResolution code)
    {
        string section = code.Pack?.Bracing is { } bracing ? $", Section {bracing.Section}" : code.Pack is null ? string.Empty : ", which has no wall-bracing provisions";
        return $"Look up: {CodeName(code)}{section}. Known: {check.Wall.Name}, wall line {Text(check.Wall.Length)}, wall height {Text(check.Wall.Height)}; {Site(sketch)}.";
    }

    /// <summary>Where a deck line's answer is to be found: the table that could not answer, or what is missing.</summary>
    static string DeckWhere(DeckCheckLine line, CodeResolution code) => line.Result switch
    {
        DeckResult.OutOfScope scope => $"{CodeName(code)}, {scope.Table.Designation} ({scope.Table.Title})",
        DeckResult.InputMissing missing => $"{CodeName(code)}, once {missing.Input} is entered",
        _ => $"{CodeName(code)} — {line.Text}",
    };

    /// <summary>What napkin knows of a deck: its size and height, and the frame the person typed.</summary>
    static string DeckKnown(Deck deck)
    {
        DeckInputs? inputs = deck.Box.Deck;
        string size = $"{deck.Name} {Text(deck.Box.Width)} × {Text(deck.Box.Height)}, {Text(deck.Height)} above grade";
        return inputs is null
            ? size
            : $"{size}; joists {inputs.Joist} at {Text(inputs.JoistSpacing)}; beam {inputs.Beam.Plies.ToString(CultureInfo.InvariantCulture)}-ply {inputs.Beam.Lumber}; "
              + $"{inputs.PostCount.ToString(CultureInfo.InvariantCulture)} {inputs.Post} posts; supports {inputs.Supports ?? "not chosen"}; species {inputs.Species ?? "not chosen"}";
    }
}

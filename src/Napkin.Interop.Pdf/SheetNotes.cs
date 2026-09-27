using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.Interop.Pdf;

/// <summary>One block of a sheet's notes: what it is about, and its lines in order.</summary>
/// <param name="Heading">What the block is about: an opening's header, a wall's bracing, a deck.</param>
/// <param name="Lines">Its lines, each printed whole and wrapped to fit.</param>
public sealed record SheetNote(string Heading, IReadOnlyList<string> Lines);

/// <summary>
/// The rules engine's results a sheet carries (DESIGN.md §7: every prescriptive result shows its
/// citation "in any exported sheet"): each opening's header, each wall's bracing and each deck's
/// checks, in the very words the panels show — the same <see cref="CheckWords"/> and deck lines, so
/// a citation, its UNREVIEWED label and anything else the panel says reach the paper unchanged.
/// </summary>
/// <remarks>
/// What prints is what the panel shows open: a result's headline, its citation line and its
/// interpolation summary. The working behind them (band trace, footnotes), which the panel keeps
/// under a Working expander, is not printed.
/// </remarks>
public static class SheetNotes
{
    /// <summary>Every result of a design, openings first, then walls' bracing, then decks; empty when there is nothing to check.</summary>
    /// <param name="sketch">The design, as the canvas shows it.</param>
    /// <param name="packs">The code packs napkin found.</param>
    /// <param name="library">The materials library the header's lumber is looked up in.</param>
    public static IReadOnlyList<SheetNote> Of(Sketch sketch, CodePacks packs, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(library);

        List<SheetNote> notes = [];
        foreach (OpeningCheck check in CodeCheck.Of(sketch, packs))
        {
            notes.Add(new SheetNote($"{check.Opening.Name}: header", Lines(CodeCheck.Words(check, library))));
        }

        foreach (WallBracingCheck check in BracingCheck.Of(sketch, packs))
        {
            notes.Add(new SheetNote($"{check.Wall.Name}: bracing", Lines(BracingCheck.Words(check.Result))));
        }

        foreach (DeckChecks deck in DeckCheck.Of(sketch, packs, library))
        {
            List<string> lines = [deck.Framing is { } framing ? $"Frame: {DeckTool.FrameLine(framing)}." : deck.Refusal!.Text];
            lines.AddRange(deck.Guides);
            lines.AddRange(deck.Lines.Select(line => line.Text));
            if (deck.SupportsNote is { } supports)
            {
                lines.Add(supports);
            }

            notes.Add(new SheetNote(deck.Deck.Name, lines));
        }

        return notes;
    }

    /// <summary>What the notes are headed with: "Code check under ZZ FRAME (IRC 2099, pack us-zz-frame rev 1)", or "Code check".</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="packs">The code packs napkin found.</param>
    public static string Heading(Sketch sketch, CodePacks packs)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(packs);
        return CodeCheck.UnderHeading(packs.Resolve(sketch.Code).Pack?.Code);
    }

    /// <summary>
    /// The title block's code line: the adopted code as a result cites it, UNREVIEWED included until
    /// its pack is signed off, and whether it is locked or follows; why there is none when the
    /// choice does not resolve; null when no code is chosen.
    /// </summary>
    /// <param name="sketch">The design.</param>
    /// <param name="packs">The code packs napkin found.</param>
    public static string? CodeLine(Sketch sketch, CodePacks packs)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(packs);
        if (sketch.Code is not { } choice)
        {
            return null;
        }

        CodeResolution resolved = packs.Resolve(choice);
        if (resolved.Pack is not { } pack)
        {
            return resolved.Problem;
        }

        string lockNote = choice.Mode == CodeMode.Locked && choice.LockedOn is { } on
            ? CodeCheck.LockedNote(on, choice.PackId, choice.Revision)
            : CodeCheck.FollowingNote(choice.PackId);
        return $"Code: {pack.Code}. {lockNote}";
    }

    static ImmutableArray<string> Lines(CheckWords words) =>
        [.. ((string[])[words.Headline, words.Citation, words.Interpolation]).Where(line => line.Length > 0)];
}

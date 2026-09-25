using System.Collections.Immutable;
using System.Globalization;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>How many New notes carry one symbol (null: no symbol, "other notes").</summary>
/// <param name="Symbol">The symbol, or null for the notes without one.</param>
/// <param name="Count">How many.</param>
public sealed record NoteCount(NoteSymbol? Symbol, int Count);

/// <summary>
/// The shopping list's Notes line (docs/design/renovation-sketches.md §6.2): the New notes counted
/// by symbol — "outlet × 4, switch × 1, light × 1, 2 other notes". Counted, never modelled: no
/// circuits, boxes, wire, pipe or fixtures.
/// </summary>
public static class NotesList
{
    static readonly NoteSymbol[] Order = [NoteSymbol.Outlet, NoteSymbol.Switch, NoteSymbol.Light, NoteSymbol.Supply, NoteSymbol.Drain];

    /// <summary>The counts, symbols in a fixed order, then the notes with none; kinds with no note are left out.</summary>
    public static ImmutableArray<NoteCount> Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        Note[] notes = [.. sketch.Entities.Values.OfType<Note>().Where(note => note.Phase == Phase.New)];
        List<NoteCount> counts = [.. Order.Select(symbol => new NoteCount(symbol, notes.Count(note => note.Symbol == symbol))).Where(count => count.Count > 0)];
        int other = notes.Count(note => note.Symbol == NoteSymbol.None);
        if (other > 0)
        {
            counts.Add(new NoteCount(null, other));
        }

        return [.. counts];
    }

    /// <summary>"outlet × 4, switch × 1, light × 1, 2 other notes"; null when there is no New note.</summary>
    public static string? Line(IEnumerable<NoteCount> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        List<string> parts = [.. counts.Select(Text)];
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>The Notes as a CSV file carries them: their own header, then Note, Count.</summary>
    public static string ToCsv(IEnumerable<NoteCount> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        StringBuilder text = new();
        text.Append("Notes\n");
        text.Append("Note,Count\n");
        foreach (NoteCount count in counts)
        {
            text.Append(count.Symbol is { } symbol ? Word(symbol) : "other").Append(',')
                .Append(count.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return text.ToString();
    }

    static string Text(NoteCount count) => count.Symbol is { } symbol
        ? $"{Word(symbol)} × {count.Count.ToString(CultureInfo.InvariantCulture)}"
        : $"{count.Count.ToString(CultureInfo.InvariantCulture)} other {(count.Count == 1 ? "note" : "notes")}";

    static string Word(NoteSymbol symbol) => symbol.ToString().ToLowerInvariant();
}

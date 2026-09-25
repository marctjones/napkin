using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// Draw → Note (docs/design/renovation-sketches.md §7, §8): a click puts a note at a point; its
/// words are typed in the panel, and the rough-in words napkin knows get their symbol. Notes are
/// counted, never modelled.
/// </summary>
public static class NoteTool
{
    /// <summary>The symbols in the panel's order, none first.</summary>
    public static readonly NoteSymbol[] Symbols = [NoteSymbol.None, NoteSymbol.Outlet, NoteSymbol.Switch, NoteSymbol.Light, NoteSymbol.Supply, NoteSymbol.Drain];

    /// <summary>The word a symbol is said with: "outlet", …; "note" for none.</summary>
    public static string Word(NoteSymbol symbol) => symbol switch
    {
        NoteSymbol.None => "note",
        NoteSymbol.Outlet => "outlet",
        NoteSymbol.Switch => "switch",
        NoteSymbol.Light => "light",
        NoteSymbol.Supply => "supply",
        NoteSymbol.Drain => "drain",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Not a note symbol."),
    };

    /// <summary>
    /// The symbol for the words a person typed, when they are one of the rough-in words napkin knows
    /// ("outlet", "Switch", " light "), or null when they are anything else.
    /// </summary>
    public static NoteSymbol? SymbolFor(string? text)
    {
        string word = (text ?? string.Empty).Trim();
        return Symbols.Skip(1).Select(symbol => (NoteSymbol?)symbol).FirstOrDefault(symbol => string.Equals(Word(symbol!.Value), word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The request that adds a new note — new, on the Notes layer — and the layer, if new.</summary>
    public static Request Request(LayerId layer, Request? addLayer, EntityId id, string name, Point2 at)
    {
        AddEntity add = new(new Note(id, layer, at, string.Empty, NoteSymbol.None) { Name = name });
        return addLayer is null ? add : Batch.Of(addLayer, add);
    }

    /// <summary>
    /// The request that sets what a note says: its words, and the symbol they name when napkin knows
    /// the word — otherwise the symbol it has. Null when nothing would change.
    /// </summary>
    public static Request? Say(Note note, string text)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(text);
        NoteSymbol symbol = SymbolFor(text) ?? note.Symbol;
        return text == note.Text && symbol == note.Symbol ? null : new SetNote(note.Id, text, symbol);
    }
}

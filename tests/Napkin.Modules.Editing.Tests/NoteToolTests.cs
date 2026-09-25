using Napkin.Core.Geometry;
using Napkin.Modules.Building;

using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>Draw → Note (docs/design/renovation-sketches.md §7, §8): words, the symbol they name, one request each.</summary>
public class NoteToolTests
{
    [Theory]
    [InlineData("outlet", NoteSymbol.Outlet)]
    [InlineData("  Switch ", NoteSymbol.Switch)]
    [InlineData("LIGHT", NoteSymbol.Light)]
    [InlineData("supply", NoteSymbol.Supply)]
    [InlineData("drain", NoteSymbol.Drain)]
    public void The_rough_in_words_napkin_knows_name_their_symbol(string text, NoteSymbol symbol)
    {
        Assert.Equal(symbol, NoteTool.SymbolFor(text));
        Assert.Equal(text.Trim().ToLowerInvariant(), NoteTool.Word(symbol));
    }

    [Theory]
    [InlineData("sump pit")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("note")]
    [InlineData("outlets")]
    public void Anything_else_names_no_symbol(string? text) => Assert.Null(NoteTool.SymbolFor(text));

    [Fact]
    public void A_note_is_put_new_on_the_notes_layer_and_saying_a_word_gives_its_symbol()
    {
        LayerId layer = LayerId.New();
        EntityId id = EntityId.New();
        Note note = (Note)Assert.IsType<AddEntity>(NoteTool.Request(layer, null, id, "Note 1", Point2.Inches(3, 4))).Entity;
        Assert.Equal(new Note(id, layer, Point2.Inches(3, 4), string.Empty, NoteSymbol.None) { Name = "Note 1" }, note);
        Assert.Equal(Phase.New, note.Phase);
        Assert.IsType<Batch>(NoteTool.Request(layer, new AddLayer(new Layer(layer, BuildingLayers.Notes)), id, "Note 1", Point2.Origin));

        Assert.Equal(new SetNote(id, "outlet", NoteSymbol.Outlet), NoteTool.Say(note, "outlet"));

        // Other words keep the symbol it has; the same words and symbol change nothing.
        Note light = note with { Text = "light", Symbol = NoteSymbol.Light };
        Assert.Equal(new SetNote(id, "ceiling fan", NoteSymbol.Light), NoteTool.Say(light, "ceiling fan"));
        Assert.Null(NoteTool.Say(light, "light"));
        Assert.Equal("note", NoteTool.Word(NoteSymbol.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoteTool.Word((NoteSymbol)42));
        Assert.Equal(6, NoteTool.Symbols.Length);
    }
}

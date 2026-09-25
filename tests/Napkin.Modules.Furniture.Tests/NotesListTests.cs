using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Project;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The shopping list's Notes line (docs/design/renovation-sketches.md §6.2): New notes counted by
/// symbol, the ones without a symbol as "other notes"; existing and demolished notes not counted.
/// </summary>
public sealed class NotesListTests
{
    static Note At(string text, NoteSymbol symbol, Phase phase = Phase.New)
        => new(EntityId.New(), LayerId.Default, Point2.Origin, text, symbol) { Phase = phase };

    static Sketch Of(params Note[] notes)
    {
        Sketch sketch = Sketch.Empty;
        foreach (Note note in notes)
        {
            sketch = sketch.WithEntity(note);
        }

        return sketch;
    }

    [Fact]
    public void New_notes_are_counted_by_symbol_in_a_fixed_order_then_the_others()
    {
        Sketch sketch = Of(
            At("light", NoteSymbol.Light), At("outlet", NoteSymbol.Outlet), At("outlet", NoteSymbol.Outlet),
            At("switch", NoteSymbol.Switch), At("sump pit", NoteSymbol.None), At("vent", NoteSymbol.None),
            At("drain", NoteSymbol.Drain), At("", NoteSymbol.Supply),
            At("outlet", NoteSymbol.Outlet, Phase.Existing), At("outlet", NoteSymbol.Outlet, Phase.Demolish));

        ImmutableArray<NoteCount> counts = NotesList.Of(sketch);

        Assert.Equal("outlet × 2, switch × 1, light × 1, supply × 1, drain × 1, 2 other notes", NotesList.Line(counts));
        Assert.Equal("Notes\nNote,Count\noutlet,2\nswitch,1\nlight,1\nsupply,1\ndrain,1\nother,2\n", NotesList.ToCsv(counts));
    }

    [Fact]
    public void One_other_note_is_a_note_and_no_new_note_is_no_line()
    {
        Assert.Equal("1 other note", NotesList.Line(NotesList.Of(Of(At("vent", NoteSymbol.None)))));
        Assert.Null(NotesList.Line(NotesList.Of(Of(At("outlet", NoteSymbol.Outlet, Phase.Existing)))));
        Assert.Empty(NotesList.Of(Sketch.Empty));
    }

    [Fact]
    public void The_basement_samples_notes_are_its_expectations()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "samples");
        Sketch sketch = Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(directory, "basement-room.scene.json"))).Sketch;
        using System.Text.Json.JsonDocument expected = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "basement-room.expected.json")));
        System.Text.Json.JsonElement notes = expected.RootElement.GetProperty("notesLine");

        ImmutableArray<NoteCount> counts = NotesList.Of(sketch);

        Assert.Equal(notes.GetProperty("line").GetString(), NotesList.Line(counts));
        Assert.Equal(string.Join("\n", notes.GetProperty("csv").EnumerateArray().Select(line => line.GetString())) + "\n", NotesList.ToCsv(counts));
    }
}

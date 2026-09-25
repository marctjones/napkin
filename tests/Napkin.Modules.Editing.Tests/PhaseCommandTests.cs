using Napkin.Core.Geometry;

using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Edit → Phase (docs/design/renovation-sketches.md §6.1, §8): one undo step over the selection,
/// the openings of a wall left as they are, the words the menu and the status bar say.
/// </summary>
public class PhaseCommandTests
{
    static (DesignEditor Editor, EntityId Wall, EntityId Window) WallAndWindow()
    {
        DesignEditor editor = new();
        EntityId wall = EntityId.New(), window = EntityId.New();
        editor.Apply(new AddEntity(Box.AsDrawn(wall, LayerId.Default, Point2.Origin, Length.Inches(144), Length.Inches(3, 1, 2), Length.Inches(96), Angle.Zero) with { Name = "Wall 1" }), "wall");
        editor.Apply(new AddEntity(Box.AsDrawn(window, LayerId.Default, Point2.Inches(54, 0), Length.Inches(36), Length.Inches(3, 1, 2), Length.Inches(48), Angle.Zero) with { Name = "Window 1" }), "window");
        return (editor, wall, window);
    }

    [Fact]
    public void Marking_the_selection_is_one_undo_step_and_leaves_the_rest_alone()
    {
        (DesignEditor editor, EntityId wall, EntityId window) = WallAndWindow();

        Request one = PhaseCommand.Plan(editor.Sketch, [wall], Phase.Existing)!;
        editor.Apply(one, PhaseCommand.Message(["Wall 1"], Phase.Existing));

        Assert.Equal(Phase.Existing, editor.Sketch.Find(wall)!.Phase);
        Assert.Equal(Phase.New, editor.Sketch.Find(window)!.Phase);
        Assert.Equal("Marked Wall 1 existing.", editor.LastMessage!.Text);

        Request both = PhaseCommand.Plan(editor.Sketch, [window, wall, window], Phase.Demolish)!;
        Assert.Equal(2, Assert.IsType<Batch>(both).Requests.Count);
        editor.Apply(both, PhaseCommand.Message(["Wall 1", "Window 1"], Phase.Demolish));
        Assert.Equal("Marked 2 things demolish.", editor.LastMessage!.Text);
        Assert.Equal(Phase.Demolish, PhaseCommand.Shared(editor.Sketch, [wall, window]));

        Assert.True(editor.Undo());
        Assert.Equal(Phase.Existing, editor.Sketch.Find(wall)!.Phase);
        Assert.Equal(Phase.New, editor.Sketch.Find(window)!.Phase);
        Assert.Null(PhaseCommand.Shared(editor.Sketch, [wall, window]));
    }

    [Fact]
    public void Nothing_to_change_plans_nothing()
    {
        (DesignEditor editor, EntityId wall, _) = WallAndWindow();

        Assert.Null(PhaseCommand.Plan(editor.Sketch, [wall], Phase.New));
        Assert.Null(PhaseCommand.Plan(editor.Sketch, [], Phase.Existing));
        Assert.Null(PhaseCommand.Plan(editor.Sketch, [EntityId.New()], Phase.Existing));
        Assert.Null(PhaseCommand.Shared(editor.Sketch, []));
        Assert.IsType<SetPhase>(PhaseCommand.Plan(editor.Sketch, [wall], Phase.Demolish));
    }

    [Theory]
    [InlineData(Phase.New, "new", "")]
    [InlineData(Phase.Existing, "existing", "Wall 1, existing")]
    [InlineData(Phase.Demolish, "demolish", "Wall 1, demolish")]
    public void The_words(Phase phase, string word, string status)
    {
        Assert.Equal(word, PhaseCommand.Word(phase));
        Assert.Equal(status, PhaseCommand.StatusWords("Wall 1", phase));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseCommand.Word((Phase)9));
    }

    [Fact]
    public void A_note_is_named_by_what_it_says()
    {
        DesignEditor editor = new();
        EntityId said = EntityId.New(), symbol = EntityId.New(), named = EntityId.New();
        editor.Apply(new AddEntity(new Note(said, LayerId.Default, Point2.Origin, "outlet", NoteSymbol.Outlet)), "note");
        editor.Apply(new AddEntity(new Note(symbol, LayerId.Default, Point2.Origin, string.Empty, NoteSymbol.Drain)), "note");
        editor.Apply(new AddEntity(new Note(named, LayerId.Default, Point2.Origin, "x", NoteSymbol.None) { Name = "Note 3" }), "note");

        Assert.Equal("the note \"outlet\"", editor.NameOf(said));
        Assert.Equal("the note", editor.NameOf(symbol));
        Assert.Equal("Note 3", editor.NameOf(named));
    }
}

using Napkin.Modules.Editing;
using Xunit;
using Design = Napkin.Modules.Editing.Design;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The two stacks undo and redo are, on their own: what goes on, what comes off, and in what
/// order.
/// </summary>
public class UndoHistoryTests
{
    [Fact]
    public void Undo_takes_the_most_recent_gesture_first_and_redo_puts_them_back_in_order()
    {
        UndoHistory history = new();
        (GestureCommitted first, GestureCommitted second, GestureCommitted third) = Three();

        history.Record(first);
        history.Record(second);
        history.Record(third);

        Assert.Equal(3, history.UndoCount);
        Assert.Equal("Third", history.UndoWhat);
        Assert.False(history.CanRedo);

        Assert.Same(third, history.Undo());
        Assert.Same(second, history.Undo());
        Assert.Equal("First", history.UndoWhat);
        Assert.Equal("Second", history.RedoWhat);
        Assert.Equal(2, history.RedoCount);

        Assert.Same(second, history.Redo());
        Assert.Same(third, history.Redo());
        Assert.False(history.CanRedo);
        Assert.Null(history.RedoWhat);
        Assert.Equal(3, history.UndoCount);
    }

    [Fact]
    public void A_new_gesture_after_an_undo_throws_the_redo_stack_away()
    {
        UndoHistory history = new();
        (GestureCommitted first, GestureCommitted second, GestureCommitted third) = Three();

        history.Record(first);
        history.Record(second);
        history.Undo();
        Assert.True(history.CanRedo);

        history.Record(third);

        Assert.False(history.CanRedo);
        Assert.Same(third, history.Undo());
        Assert.Same(first, history.Undo());
        Assert.Null(history.Undo());
    }

    [Fact]
    public void Nothing_to_undo_or_redo_is_null_and_changes_nothing()
    {
        UndoHistory history = new();
        int changes = 0;
        history.Changed += (_, _) => changes++;

        Assert.Null(history.Undo());
        Assert.Null(history.Redo());
        Assert.False(history.CanUndo);
        Assert.Null(history.UndoWhat);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Clear_forgets_both_stacks_and_says_so_once()
    {
        UndoHistory history = new();
        (GestureCommitted first, GestureCommitted second, _) = Three();
        history.Record(first);
        history.Record(second);
        history.Undo();

        int changes = 0;
        history.Changed += (_, _) => changes++;
        history.Clear();
        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Every_change_to_a_stack_is_announced()
    {
        UndoHistory history = new();
        (GestureCommitted first, _, _) = Three();
        int changes = 0;
        history.Changed += (_, _) => changes++;

        history.Record(first);
        history.Undo();
        history.Redo();

        Assert.Equal(3, changes);
    }

    static (GestureCommitted, GestureCommitted, GestureCommitted) Three()
    {
        Design a = NewSheet.Empty();
        Design b = a with { Name = "b" };
        Design c = a with { Name = "c" };
        Design d = a with { Name = "d" };
        return (new("First", a, b), new("Second", b, c), new("Third", c, d));
    }
}

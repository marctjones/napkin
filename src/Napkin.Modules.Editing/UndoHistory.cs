namespace Napkin.Modules.Editing;

/// <summary>
/// Undo and redo (#11): two stacks of gestures, each holding the design before it and after it.
/// </summary>
/// <remarks>
/// <para>
/// There are no inverse commands (design &#xA7;2.4). A sketch is an immutable value, so undoing a
/// gesture is putting back the design it started from, and redoing it is putting back the one it
/// ended at — the same two objects, not a recomputation, so what comes back is exactly what was
/// there.
/// </para>
/// <para>
/// This type only keeps the stacks. <see cref="DesignEditor"/> owns one, records every
/// <see cref="GestureCommitted"/> into it, and does the restoring, because it is the only thing in
/// the application that replaces a design.
/// </para>
/// </remarks>
public sealed class UndoHistory
{
    readonly Stack<GestureCommitted> _undo = new();
    readonly Stack<GestureCommitted> _redo = new();

    /// <summary>Raised whenever either stack changes, so a menu can follow what it offers.</summary>
    public event EventHandler? Changed;

    /// <summary>Whether there is a gesture to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether there is an undone gesture to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>What the next undo would take back, in words, or null when there is nothing.</summary>
    public string? UndoWhat => CanUndo ? _undo.Peek().What : null;

    /// <summary>What the next redo would put back, in words, or null when there is nothing.</summary>
    public string? RedoWhat => CanRedo ? _redo.Peek().What : null;

    /// <summary>How many gestures can be undone.</summary>
    public int UndoCount => _undo.Count;

    /// <summary>How many gestures can be redone.</summary>
    public int RedoCount => _redo.Count;

    /// <summary>
    /// The Undo menu item's header for what <paramref name="what"/> describes, or "_Undo" alone
    /// when there is nothing (#179). An underscore in the description is doubled so the menu shows
    /// it rather than taking it as an access key.
    /// </summary>
    public static string UndoMenuHeader(string? what) =>
        what is null ? "_Undo" : $"_Undo {EscapedForMenu(what)}";

    /// <summary>The Redo menu item's header for what <paramref name="what"/> describes (#179).</summary>
    public static string RedoMenuHeader(string? what) =>
        what is null ? "_Redo" : $"_Redo {EscapedForMenu(what)}";

    static string EscapedForMenu(string text) => text.Replace("_", "__", StringComparison.Ordinal);

    /// <summary>
    /// Records a gesture that just ended. Anything that had been undone can no longer be redone:
    /// the drawing has gone a different way from it.
    /// </summary>
    public void Record(GestureCommitted gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);

        _undo.Push(gesture);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Takes the most recent gesture off the undo stack and puts it on the redo stack.
    /// </summary>
    /// <returns>The gesture, whose <see cref="GestureCommitted.Before"/> is what to restore; or null.</returns>
    public GestureCommitted? Undo()
    {
        if (!_undo.TryPop(out GestureCommitted? gesture))
        {
            return null;
        }

        _redo.Push(gesture);
        Changed?.Invoke(this, EventArgs.Empty);
        return gesture;
    }

    /// <summary>
    /// Takes the most recently undone gesture off the redo stack and puts it back on the undo stack.
    /// </summary>
    /// <returns>The gesture, whose <see cref="GestureCommitted.After"/> is what to restore; or null.</returns>
    public GestureCommitted? Redo()
    {
        if (!_redo.TryPop(out GestureCommitted? gesture))
        {
            return null;
        }

        _undo.Push(gesture);
        Changed?.Invoke(this, EventArgs.Empty);
        return gesture;
    }

    /// <summary>Forgets everything: what a different design being opened does.</summary>
    public void Clear()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
        {
            return;
        }

        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

using System.Collections.Immutable;
using System.Globalization;
using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>One relationship the drawing holds, ready to list.</summary>
/// <param name="Id">The relationship.</param>
/// <param name="Text">What it says, in plain words.</param>
/// <param name="Entities">The entities it touches, for highlighting.</param>
public sealed record RelationshipEntry(RelationshipId Id, string Text, ImmutableList<EntityId> Entities);

/// <summary>
/// One gesture's worth of change, from the sketch before it to the sketch after it.
/// </summary>
/// <remarks>
/// A drag is many <see cref="Drag"/> requests and one gesture. Undo/redo (#11) is a stack of
/// sketch values (design &#xA7;2.4), and this is where it hangs: one entry per gesture, pushed
/// when the gesture ends, holding two immutable sketches and no inverse commands at all.
/// </remarks>
/// <param name="What">What the person did, in words.</param>
/// <param name="Before">The design as it was when the gesture started.</param>
/// <param name="After">The design as it is now.</param>
public sealed record GestureCommitted(string What, Design Before, Design After);

/// <summary>
/// The drawing being edited: one immutable <see cref="Sketch"/>, an
/// <see cref="IGeometryUpdater"/>, and what is selected.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is the only thing in the application that replaces a sketch, and it never builds
/// one.</strong> Every edit arrives as a <see cref="Request"/>, goes to
/// <see cref="IGeometryUpdater.Apply"/>, and the design is replaced only when the result is a
/// <see cref="Succeeded"/>. There is no other path: the canvas, the menu and the dimension fields
/// all call <see cref="Apply"/>, and none of them can reach an entity to change it (CVS-005).
/// </para>
/// <para>
/// <strong>Every result lands somewhere.</strong> <see cref="LastMessage"/> holds what to say
/// about the last edit until the next one, including the two results that changed nothing —
/// which is what stops a refused edit from looking like an edit that worked (CVS-008).
/// </para>
/// <para>
/// <strong>Names are the app's, not the model's.</strong> The geometry kernel has no notion of a
/// part name, so new parts are called "Part 1", "Part 2" here and the name is carried in
/// <see cref="Design.Labels"/>. Conflict reports and relationship lists are rewritten through
/// those names so a person never has to read a GUID.
/// </para>
/// </remarks>
public sealed class DesignEditor
{
    /// <summary>What <see cref="Undo"/> says when it puts a gesture back, so the sentence is not
    /// copied wherever it is checked (#179).</summary>
    /// <param name="what">The gesture's own description, e.g. "Moved Part 1".</param>
    public static string UndoneText(string what) => $"Undone: {what}.";

    /// <summary>What <see cref="Redo"/> says when it puts a gesture forward again (#179).</summary>
    /// <param name="what">The gesture's own description, e.g. "Moved Part 1".</param>
    public static string RedoneText(string what) => $"Redone: {what}.";

    readonly IGeometryUpdater _updater;
    Design _design;
    ImmutableHashSet<EntityId> _selection = [];
    Design? _gestureStart;
    string _gestureName = string.Empty;
    int _nextPartNumber = 1;
    Sketch _savedSketch;

    /// <summary>An editor over an empty sheet, using the direct updater.</summary>
    public DesignEditor()
        : this(DirectUpdater.Instance)
    {
    }

    /// <summary>An editor over an empty sheet, using a given updater.</summary>
    /// <remarks>
    /// The updater is injected because the canvas is supposed to work with either implementation
    /// (design &#xA7;4.3), and because a test that wants to see what the canvas does with an
    /// <see cref="UnderConstrained"/> has no other way to produce one: the direct updater never
    /// returns it.
    /// </remarks>
    public DesignEditor(IGeometryUpdater updater)
    {
        _updater = updater ?? throw new ArgumentNullException(nameof(updater));
        _design = NewSheet.Empty();
        _savedSketch = _design.Sketch;
    }

    /// <summary>Raised when the design is replaced — by an edit or by opening another one.</summary>
    public event EventHandler? DesignChanged;

    /// <summary>Raised when a different design is opened, as opposed to edited.</summary>
    public event EventHandler? DesignOpened;

    /// <summary>Raised when the selection changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised when there is something new to say about the last edit.</summary>
    public event EventHandler? MessageChanged;

    /// <summary>Raised when <see cref="EntryMode"/> changes.</summary>
    public event EventHandler? EntryModeChanged;

    EntryMode _entryMode = EntryMode.Precise;

    /// <summary>
    /// How the next gesture enters the design (<c>docs/design/sketch-mode.md</c> &#xA7;1): Precise
    /// every launch, never saved. Switching is not an undo step — like Snap to grid, it changes
    /// what the next gesture does, never the design — and survives opening another design.
    /// </summary>
    public EntryMode EntryMode
    {
        get => _entryMode;
        set
        {
            if (_entryMode != value)
            {
                _entryMode = value;
                EntryModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Raised when a gesture that changed something ends, after it has been recorded in
    /// <see cref="History"/>.
    /// </summary>
    public event EventHandler<GestureCommitted>? GestureCommitted;

    /// <summary>
    /// Undo and redo (#11): every gesture that changed something, in order. Cleared when another
    /// design is opened — there is no undoing across a change of file.
    /// </summary>
    public UndoHistory History { get; } = new();

    /// <summary>
    /// Whether the drawing differs from the one last opened or saved.
    /// </summary>
    /// <remarks>
    /// Compared by value, not by counting edits: a sketch is an immutable value with structural
    /// equality, so undoing back to exactly what was saved — or dragging a part away and back onto
    /// the same spot — is correctly not a change. Only the sketch is compared, because it is all a
    /// save writes.
    /// </remarks>
    public bool HasUnsavedChanges => !_design.Sketch.Equals(_savedSketch);

    /// <summary>The design, with the one sketch in it.</summary>
    public Design Design => _design;

    /// <summary>The sketch. Replaced whole by every accepted edit; never written to.</summary>
    public Sketch Sketch => _design.Sketch;

    /// <summary>How lengths are written on screen. #11 owns the per-project picker.</summary>
    public LengthFormat LabelFormat { get; } = new FeetInchesFormat(16);

    /// <summary>What to say about the last edit, until the next one.</summary>
    public EditMessage? LastMessage { get; private set; }

    /// <summary>What is selected.</summary>
    public IReadOnlySet<EntityId> Selection => _selection;

    /// <summary>The selected entity when exactly one is selected.</summary>
    public EntityId? OnlySelected => _selection.Count == 1 ? _selection.First() : null;

    /// <summary>The selected box when exactly one box is selected.</summary>
    public Box? OnlySelectedBox =>
        OnlySelected is { } id ? _design.Sketch.Find<Box>(id) : null;

    /// <summary>Whether a gesture is running.</summary>
    public bool InGesture => _gestureStart is not null;

    /// <summary>Opens a design, replacing whatever was being edited. Clears the selection.</summary>
    public void Open(Design design)
    {
        ArgumentNullException.ThrowIfNull(design);

        _gestureStart = null;
        _nextPartNumber = NextFreePartNumber(design);
        History.Clear();

        // Every part gets a name on the way in, so that nothing a person reads — a relationship,
        // a conflict, a message — ever has to fall back to a GUID. A file carries no name per
        // entity yet (docs/file-format.md), so for now they are numbered in id order.
        _design = Named(design, ChangeSet.Empty with { Added = [.. design.Sketch.Entities.Keys] });
        _savedSketch = _design.Sketch;

        SetMessage(null);
        ReplaceSelection([]);
        DesignChanged?.Invoke(this, EventArgs.Empty);
        DesignOpened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Puts a request to the updater, and replaces the design when it is accepted.
    /// </summary>
    /// <param name="request">What the person asked for.</param>
    /// <param name="what">
    /// What they did, as a sentence with no full stop — "Moved Shelf". It is the subject of every
    /// message the result produces, whichever result it is.
    /// </param>
    /// <returns>What the updater said, unaltered.</returns>
    /// <remarks>
    /// A request applied outside a gesture is a gesture of its own, so that every accepted edit is
    /// one undo step whether or not the caller thought to say where it began and ended.
    /// </remarks>
    public UpdateResult Apply(Request request, string what)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!InGesture)
        {
            BeginGesture(what);
            try
            {
                return Apply(request, what);
            }
            finally
            {
                EndGesture();
            }
        }

        Sketch before = _design.Sketch;
        UpdateResult result = _updater.Apply(before, request);

        if (result is Succeeded succeeded)
        {
            Replace(succeeded);
        }

        SetMessage(EditMessages.For(result, what, before, NameOf, LabelFormat));
        return result;
    }

    /// <summary>
    /// Applies a request without saying anything about it — what a live drag does between its
    /// first pointer move and its last.
    /// </summary>
    public UpdateResult ApplyQuietly(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        UpdateResult result = _updater.Apply(_design.Sketch, request);
        if (result is Succeeded succeeded)
        {
            Replace(succeeded);
        }

        return result;
    }

    /// <summary>
    /// Takes the sketch an accepted result carries, when it is a different sketch.
    /// </summary>
    /// <remarks>
    /// A drag that goes nowhere — because it was blocked, or because the pointer moved less than a
    /// grid step — reports success over the sketch it was given. Wrapping that in a new
    /// <see cref="Design"/> anyway would make a gesture that changed nothing look like a change,
    /// and #11 would push an undo entry with the same drawing on both sides of it.
    /// </remarks>
    void Replace(Succeeded succeeded)
    {
        if (ReferenceEquals(succeeded.Sketch, _design.Sketch) && succeeded.Changes.IsEmpty)
        {
            return;
        }

        _design = Named(_design with { Sketch = succeeded.Sketch }, succeeded.Changes);
        PruneSelection();
        DesignChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows a message that did not come from a request — a refused keystroke, say.</summary>
    public void Say(EditSeverity severity, string text) =>
        SetMessage(EditMessage.Plain(severity, text));

    /// <summary>
    /// Says what a result was, as <see cref="Apply"/> would have — for a live gesture whose steps
    /// were applied quietly and one of which was refused: at the drop, a drag that went nowhere
    /// because the part is pinned says so, with the way out, instead of "Moved Top."
    /// </summary>
    public void Report(UpdateResult result, string what)
    {
        ArgumentNullException.ThrowIfNull(result);
        SetMessage(EditMessages.For(result, what, _design.Sketch, NameOf, LabelFormat));
    }

    /// <summary>
    /// Puts a message on the screen as it is — for a command that has more to say about a result
    /// than <see cref="EditMessages.For"/> can, such as a way out of a refused turn (#76).
    /// </summary>
    public void Show(EditMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        SetMessage(message);
    }

    /// <summary>Takes the last message off the screen.</summary>
    public void ClearMessage() => SetMessage(null);

    /// <summary>
    /// Whether this updater can hold a relationship over <em>these</em> references.
    /// </summary>
    /// <remarks>
    /// Asked of the updater rather than read off
    /// <see cref="IGeometryUpdater.SupportedRelationships"/>, because that list is by relationship
    /// type and the real support is by (kind, reference kind): the direct updater takes a
    /// <see cref="Horizontal"/> on a segment and refuses one on a box edge, and takes a
    /// <see cref="ParamValue"/> on a box's width and refuses one on a segment's length. A canvas
    /// that trusted the type list would offer relationships and then be told no (#10, Fable's
    /// review of #35, finding 10). Only <see cref="RejectionReason.UnsupportedRelationship"/> and
    /// <see cref="RejectionReason.PlacesNotComparable"/> — a pairing whose places share no axis to
    /// hold equal (docs/design/assembly-model.md &#xA7;2.3) — mean "cannot hold"; a conflict or a
    /// duplicate both prove it could.
    /// </remarks>
    public bool CanHold(Relationship candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!_updater.SupportedRelationships.Contains(candidate.GetType()))
        {
            return false;
        }

        return _updater.Apply(_design.Sketch, new AddRelationship(candidate))
            is not Rejected { Reason: RejectionReason.UnsupportedRelationship or RejectionReason.PlacesNotComparable };
    }

    /// <summary>
    /// Whether the drawing already says this, including the symmetric kinds written the other way
    /// round.
    /// </summary>
    /// <remarks>
    /// The updater's own duplicate check compares records field for field, which does not catch
    /// <c>Flush(a, b)</c> against <c>Flush(b, a)</c> (design &#xA7;3.2's note on
    /// <c>AreStructurallyIdentical</c>). Snapping the same two edges together twice from opposite
    /// directions would otherwise fill the list with the same sentence twice.
    /// </remarks>
    public bool AlreadyStates(Relationship candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        foreach (Relationship existing in _design.Sketch.RelationshipsInOrder)
        {
            if (Relationship.AreStructurallyIdentical(existing, candidate) || SameBothWays(existing, candidate))
            {
                return true;
            }
        }

        return false;
    }

    RelationshipId? _selectedJoint;

    /// <summary>
    /// The joint whose marker was picked, or null: a joint is selected instead of any part, never with
    /// one, and only while it exists (undo can take it away).
    /// </summary>
    public RelationshipId? SelectedJoint =>
        _selectedJoint is { } id && _design.Sketch.Relationships.ContainsKey(id) ? id : null;

    /// <summary>Selects one joint and, since it is one thing that is selected, no part.</summary>
    /// <param name="id">The joint.</param>
    public void SelectJoint(RelationshipId? id)
    {
        RelationshipId? wanted = id is { } one && _design.Sketch.Relationships.GetValueOrDefault(one) is Joint ? id : null;
        if (wanted == SelectedJoint && _selection.IsEmpty)
        {
            return;
        }

        _selectedJoint = wanted;
        _selection = [];
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Selects one entity, or nothing.</summary>
    public void Select(EntityId? id) =>
        ReplaceSelection(id is { } one && _design.Sketch.Find(one) is not null ? [one] : []);

    /// <summary>Selects exactly these entities — the ones that exist — and nothing else.</summary>
    public void SelectAll(IEnumerable<EntityId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ReplaceSelection([.. ids.Where(id => _design.Sketch.Find(id) is not null)]);
    }

    /// <summary>Adds an entity to the selection, or takes it out again.</summary>
    public void ToggleSelected(EntityId id) =>
        ReplaceSelection(_selection.Contains(id) ? _selection.Remove(id) : _selection.Add(id));

    /// <summary>Selects nothing.</summary>
    public void ClearSelection()
    {
        if (_selectedJoint is not null)
        {
            SelectJoint(null);
        }
        else
        {
            ReplaceSelection([]);
        }
    }

    /// <summary>
    /// Starts a gesture. Everything applied until <see cref="EndGesture"/> is one thing a person
    /// did, however many requests it took.
    /// </summary>
    public void BeginGesture(string what)
    {
        _gestureStart = _design;
        _gestureName = what;
    }

    /// <summary>Ends a gesture, announcing it when it changed anything.</summary>
    public void EndGesture()
    {
        Design? start = _gestureStart;
        _gestureStart = null;

        if (start is not null && !ReferenceEquals(start, _design))
        {
            GestureCommitted committed = new(_gestureName, start, _design);
            History.Record(committed);
            GestureCommitted?.Invoke(this, committed);
        }
    }

    /// <summary>
    /// Puts the drawing back as it was before the last gesture, and says so.
    /// </summary>
    /// <remarks>
    /// The design restored is the very one the gesture started from, so the drawing, its names and
    /// its entity ids come back exactly; whatever is selected stays selected if it still exists.
    /// Nothing is undone in the middle of a gesture — a drag has not happened yet until it ends.
    /// </remarks>
    /// <returns>Whether anything was undone.</returns>
    public bool Undo()
    {
        if (InGesture)
        {
            return false;
        }

        if (History.Undo() is not { } gesture)
        {
            Say(EditSeverity.Hint, "There is nothing to undo.");
            return false;
        }

        Restore(gesture.Before);
        Say(EditSeverity.Done, UndoneText(gesture.What));
        return true;
    }

    /// <summary>Puts back the last gesture that was undone, and says so.</summary>
    /// <returns>Whether anything was redone.</returns>
    public bool Redo()
    {
        if (InGesture)
        {
            return false;
        }

        if (History.Redo() is not { } gesture)
        {
            Say(EditSeverity.Hint, "There is nothing to redo.");
            return false;
        }

        Restore(gesture.After);
        Say(EditSeverity.Done, RedoneText(gesture.What));
        return true;
    }

    /// <summary>
    /// Records that the drawing as it is now is what is on disk, so
    /// <see cref="HasUnsavedChanges"/> measures from here.
    /// </summary>
    public void MarkSaved() => _savedSketch = _design.Sketch;

    /// <summary>Replaces the design with one from the history — an edit, not an open.</summary>
    void Restore(Design design)
    {
        _design = design;
        PruneSelection();
        DesignChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Everything the drawing says, in id order, in plain words.</summary>
    public IReadOnlyList<RelationshipEntry> RelationshipEntries() =>
    [
        .. _design.Sketch.RelationshipsInOrder.Select(relationship => new RelationshipEntry(
            relationship.Id,
            RelationshipText.Describe(_design.Sketch, relationship, NameOf, LabelFormat),
            [.. relationship.References.Distinct()])),
    ];

    /// <summary>What to call an entity on screen.</summary>
    public string NameOf(EntityId id) => _design.LabelFor(id) ?? _design.Sketch.Find(id) switch
    {
        Dimension => $"the dimension {id}",
        Segment => $"the line {id}",
        Node => $"the point {id}",
        _ => $"part {id}",
    };

    /// <summary>The layer a new part goes on: the one called "Parts", or the first there is.</summary>
    public LayerId LayerForNewParts()
    {
        foreach (Layer layer in _design.Sketch.Layers)
        {
            if (string.Equals(layer.Name, DesignLayers.Parts, StringComparison.OrdinalIgnoreCase))
            {
                return layer.Id;
            }
        }

        return _design.Sketch.Layers[0].Id;
    }

    static bool SameBothWays(Relationship a, Relationship b) => (a, b) switch
    {
        (Flush first, Flush second) => first.A == second.B && first.B == second.A,
        (Coincident first, Coincident second) => first.A == second.B && first.B == second.A,
        (EqualParam first, EqualParam second) => first.A == second.B && first.B == second.A,
        _ => false,
    };

    static int NextFreePartNumber(Design design)
    {
        int highest = 0;
        IEnumerable<string> names = design.Sketch.Entities.Values
            .Select(entity => entity.Name)
            .Concat(design.Labels.Values);

        foreach (string label in names)
        {
            if (label.StartsWith("Part ", StringComparison.Ordinal)
                && int.TryParse(label[5..], NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            {
                highest = Math.Max(highest, number);
            }
        }

        return highest + 1;
    }

    /// <summary>
    /// The name to give the next part somebody draws, so that nothing on screen is a GUID.
    /// </summary>
    /// <remarks>
    /// The name goes on the entity, in the <see cref="AddEntity"/> request that creates it, rather
    /// than into a side table afterwards: since scene format version 2 that is where a name lives
    /// and what a save writes back, so a part drawn today keeps the name it was drawn with. It is
    /// set on the way in because the application may not touch a sketch (CVS-005) — the only way
    /// to name an entity that already exists is a <see cref="SetName"/> request.
    /// </remarks>
    public string NextPartName() => $"Part {_nextPartNumber++}";

    /// <summary>
    /// "Wall 3": the word and one more than the highest number any entity called that word and a
    /// number already has, so walls and openings count themselves (#18).
    /// </summary>
    public string NextName(string word)
    {
        int highest = 0;
        foreach (Entity entity in _design.Sketch.Entities.Values)
        {
            if (entity.Name.StartsWith(word + " ", StringComparison.Ordinal)
                && int.TryParse(entity.Name.AsSpan(word.Length + 1), out int number))
            {
                highest = Math.Max(highest, number);
            }
        }

        return $"{word} {highest + 1}";
    }

    /// <summary>
    /// The layer with this name, or a new one and the <see cref="AddLayer"/> request that makes it,
    /// to go in the same batch as the first thing drawn on it so that undo takes both away (#18).
    /// </summary>
    public LayerId LayerNamed(string name, out Request? add)
    {
        foreach (Layer layer in _design.Sketch.Layers)
        {
            if (string.Equals(layer.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                add = null;
                return layer.Id;
            }
        }

        Layer made = new(LayerId.New(), name);
        add = new AddLayer(made);
        return made.Id;
    }

    /// <summary>Gives every part that was just added a name, so nothing on screen is a GUID.</summary>
    Design Named(Design design, ChangeSet changes)
    {
        Design named = design;
        foreach (EntityId id in changes.Added.OrderBy(entity => entity))
        {
            if (named.Sketch.Find<Box>(id) is not null && named.LabelFor(id) is null)
            {
                named = named with { Labels = named.Labels.SetItem(id, NextPartName()) };
            }
        }

        return named;
    }

    void PruneSelection()
    {
        ImmutableHashSet<EntityId> alive = [.. _selection.Where(id => _design.Sketch.Find(id) is not null)];
        if (alive.Count != _selection.Count)
        {
            ReplaceSelection(alive);
        }
    }

    void ReplaceSelection(ImmutableHashSet<EntityId> selection)
    {
        if (_selection.SetEquals(selection))
        {
            return;
        }

        _selection = selection;
        if (!selection.IsEmpty)
        {
            _selectedJoint = null;
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    void SetMessage(EditMessage? message)
    {
        if (LastMessage == message)
        {
            return;
        }

        LastMessage = message;
        MessageChanged?.Invoke(this, EventArgs.Empty);
    }
}

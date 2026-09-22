using System.Collections.Immutable;
using System.Globalization;
using Napkin.App.Designs;
using Napkin.Core.Geometry;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.Editing;

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
    readonly IGeometryUpdater _updater;
    Design _design;
    ImmutableHashSet<EntityId> _selection = [];
    Design? _gestureStart;
    string _gestureName = string.Empty;
    int _nextPartNumber = 1;

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
    }

    /// <summary>Raised when the design is replaced — by an edit or by opening another one.</summary>
    public event EventHandler? DesignChanged;

    /// <summary>Raised when a different design is opened, as opposed to edited.</summary>
    public event EventHandler? DesignOpened;

    /// <summary>Raised when the selection changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised when there is something new to say about the last edit.</summary>
    public event EventHandler? MessageChanged;

    /// <summary>Raised when a gesture that changed something ends. #11's undo stack hangs here.</summary>
    public event EventHandler<GestureCommitted>? GestureCommitted;

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

        // Every part gets a name on the way in, so that nothing a person reads — a relationship,
        // a conflict, a message — ever has to fall back to a GUID. A file carries no name per
        // entity yet (docs/file-format.md), so for now they are numbered in id order.
        _design = Named(design, ChangeSet.Empty with { Added = [.. design.Sketch.Entities.Keys] });

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
    public UpdateResult Apply(Request request, string what)
    {
        ArgumentNullException.ThrowIfNull(request);

        Sketch before = _design.Sketch;
        UpdateResult result = _updater.Apply(before, request);

        if (result is Succeeded succeeded)
        {
            _design = Named(_design with { Sketch = succeeded.Sketch }, succeeded.Changes);
            PruneSelection();
            DesignChanged?.Invoke(this, EventArgs.Empty);
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
            _design = Named(_design with { Sketch = succeeded.Sketch }, succeeded.Changes);
            PruneSelection();
            DesignChanged?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    /// <summary>Shows a message that did not come from a request — a refused keystroke, say.</summary>
    public void Say(EditSeverity severity, string text) =>
        SetMessage(EditMessage.Plain(severity, text));

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
    /// review of #35, finding 10). Only <see cref="RejectionReason.UnsupportedRelationship"/>
    /// means "cannot hold"; a conflict or a duplicate both prove it could.
    /// </remarks>
    public bool CanHold(Relationship candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!_updater.SupportedRelationships.Contains(candidate.GetType()))
        {
            return false;
        }

        return _updater.Apply(_design.Sketch, new AddRelationship(candidate))
            is not Rejected { Reason: RejectionReason.UnsupportedRelationship };
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

    /// <summary>Selects one entity, or nothing.</summary>
    public void Select(EntityId? id) =>
        ReplaceSelection(id is { } one && _design.Sketch.Find(one) is not null ? [one] : []);

    /// <summary>Adds an entity to the selection, or takes it out again.</summary>
    public void ToggleSelected(EntityId id) =>
        ReplaceSelection(_selection.Contains(id) ? _selection.Remove(id) : _selection.Add(id));

    /// <summary>Selects nothing.</summary>
    public void ClearSelection() => ReplaceSelection([]);

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
            GestureCommitted?.Invoke(this, new GestureCommitted(_gestureName, start, _design));
        }
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
        foreach (string label in design.Labels.Values)
        {
            if (label.StartsWith("Part ", StringComparison.Ordinal)
                && int.TryParse(label[5..], NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            {
                highest = Math.Max(highest, number);
            }
        }

        return highest + 1;
    }

    /// <summary>Gives every part that was just added a name, so nothing on screen is a GUID.</summary>
    Design Named(Design design, ChangeSet changes)
    {
        Design named = design;
        foreach (EntityId id in changes.Added.OrderBy(entity => entity))
        {
            if (named.Sketch.Find<Box>(id) is not null && named.LabelFor(id) is null)
            {
                named = named with { Labels = named.Labels.SetItem(id, $"Part {_nextPartNumber++}") };
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

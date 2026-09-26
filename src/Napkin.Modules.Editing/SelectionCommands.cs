using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// The commands that act on the selection the same way in every view — delete, pin, duplicate and
/// shape — beside <see cref="SelectionTurn"/> (#88).
/// </summary>
/// <remarks>
/// They used to live in the plan canvas, and the 3D view reached into it to run them, which is how a
/// duplicate made in the 3D view came to be spaced by the plan's grid step (#71). What a command
/// needs from the view that asked — only the grid step in force there — is passed in; everything else
/// is the editor's, and every change is one request through it, one undo step (CVS-005).
/// </remarks>
public static class SelectionCommands
{
    /// <summary>
    /// Selects the parts a list names — a Parts view cell's members, a cut-list row's (parts-view
    /// §5.1, #205) — as a click does: exactly these; with <paramref name="toggle"/> (Ctrl or Cmd), each
    /// one in or out of the selection; with <paramref name="add"/> (Shift), these as well as what is
    /// selected. One selection, the editor's, whichever window asked.
    /// </summary>
    /// <param name="editor">Whose selection.</param>
    /// <param name="ids">The parts.</param>
    /// <param name="toggle">Toggle each instead of selecting.</param>
    /// <param name="add">Add to the selection instead of replacing it.</param>
    public static void Pick(DesignEditor editor, IEnumerable<EntityId> ids, bool toggle, bool add)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(ids);
        if (toggle)
        {
            foreach (EntityId id in ids)
            {
                editor.ToggleSelected(id);
            }

            return;
        }

        editor.SelectAll(add ? editor.Selection.Concat(ids) : ids);
    }

    /// <summary>Removes what is selected, through the updater, relationships and all.</summary>
    public static void Delete(DesignEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (editor.Selection.Count == 0)
        {
            return;
        }

        List<EntityId> doomed = [.. editor.Selection.OrderBy(id => id)];
        string what = doomed.Count == 1
            ? $"Deleted {editor.NameOf(doomed[0])}"
            : $"Deleted {doomed.Count} parts";

        editor.BeginGesture(what);
        editor.Apply(
            Batch.Of([.. doomed.Select(id => (Request)new RemoveEntity(id))]),
            what);
        editor.EndGesture();
    }

    /// <summary>Pins what is selected where it is, or says why it cannot be pinned.</summary>
    public static void Pin(DesignEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (editor.Selection.Count == 0)
        {
            return;
        }

        List<Request> requests = [];
        foreach (EntityId id in editor.Selection.OrderBy(id => id))
        {
            Anchored candidate = new(RelationshipId.New(), id);
            if (editor.CanHold(candidate) && !editor.AlreadyStates(candidate))
            {
                requests.Add(new AddRelationship(candidate));
            }
        }

        if (requests.Count == 0)
        {
            editor.Say(EditSeverity.Hint, "Already pinned.");
            return;
        }

        string what = requests.Count == 1
            ? $"Pinned {editor.NameOf(editor.Selection.OrderBy(id => id).First())}"
            : $"Pinned {requests.Count} parts";

        editor.BeginGesture(what);
        editor.Apply(Batch.Of([.. requests]), what);
        editor.EndGesture();
    }

    /// <summary>
    /// Makes a copy of the selected parts beside them and selects the copies
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.6, #87).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>A duplicate is a value copy.</strong> The blank, its cuts, the part — stock, species,
    /// plan axes, quantity — and the depth are all carried across by the record's own <c>with</c>;
    /// each copy gets a new id, a new anchor and its original's name with a number (#91). Of the relationships, only the ones
    /// <em>among</em> the copied parts come too (<see cref="GroupCopy"/>): copy a leg and the apron
    /// flush to it and the copies are flush to each other; copy one part and it is unrelated until
    /// somebody snaps it, exactly like a part just drawn. Four duplicates of one gusset are equal by
    /// value, so the cut list groups them into one row of four on its own.
    /// </para>
    /// <para>
    /// <strong>Beside them, clear of them</strong> (#71): along the narrower of the selection's two
    /// plan sides — east when it is narrower east–west, north otherwise — by its own extent that way
    /// and one grid step of the view that asked. So the copies never overlap the originals, line up
    /// with them on the other two axes, and have the shortest way to go to be seen. One undo step.
    /// </para>
    /// </remarks>
    /// <param name="editor">The drawing.</param>
    /// <param name="gridStepInches">The grid step in force in the view that asked.</param>
    /// <returns>The first copy's id, or <see langword="null"/> when nothing was copied.</returns>
    public static EntityId? Duplicate(DesignEditor editor, double gridStepInches)
    {
        ArgumentNullException.ThrowIfNull(editor);

        Box[] boxes = SelectedBoxes(editor);
        if (boxes.Length == 0)
        {
            editor.Say(EditSeverity.Hint, "Select a part to duplicate it.");
            return null;
        }

        (Point3 low, Point3 high) = GroupCopy.Extent(boxes);
        (ImmutableList<Request> requests, ImmutableDictionary<EntityId, EntityId> copies) =
            GroupCopy.Duplicate(editor.Sketch, boxes, BesideOffset(low, high, gridStepInches), editor.NameOf);

        string what = boxes.Length == 1 ? $"Duplicated {editor.NameOf(boxes[0].Id)}" : $"Duplicated {boxes.Length} parts";
        editor.BeginGesture(what);
        EntityId? made = null;
        if (editor.Apply(Batch.Of([.. requests]), what) is Succeeded)
        {
            editor.SelectAll(copies.Values);
            made = copies[boxes[0].Id];
            editor.Say(
                EditSeverity.Done,
                boxes.Length == 1 ? $"{what} as {editor.NameOf(made.Value)}." : $"{what}, with the relationships among them.");
        }

        editor.EndGesture();
        return made;
    }

    /// <summary>
    /// Makes mirror copies of the selected parts across the middle of the drawing, east–west or
    /// north–south, and selects them (#87): a left leg's right-hand twin, an apron and the leg it is
    /// flush to at the other end of the table.
    /// </summary>
    /// <remarks>
    /// The mirror is the drawing's centre plane across <paramref name="axis"/> — for a table, its
    /// middle — so one leg mirrored lands where the opposite leg goes, rather than on itself as it
    /// would across its own middle. The relationships among the copied parts are mirrored with them
    /// (<see cref="GroupCopy"/>); a part with cuts is refused by name, because the copy would carry
    /// its cuts the wrong way round. One undo step.
    /// </remarks>
    /// <param name="editor">The drawing.</param>
    /// <param name="axis">The world axis the mirror reverses: X for east–west, Y for north–south.</param>
    /// <returns>The first copy's id, or <see langword="null"/> when nothing was copied.</returns>
    public static EntityId? Mirror(DesignEditor editor, Axis axis)
    {
        ArgumentNullException.ThrowIfNull(editor);

        Box[] boxes = SelectedBoxes(editor);
        if (boxes.Length == 0)
        {
            editor.Say(EditSeverity.Hint, "Select a part to mirror it.");
            return null;
        }

        (Point3 low, Point3 high) = GroupCopy.Extent(editor.Sketch.Entities.Values.OfType<Box>());
        Length plane = (low.Component(axis) + high.Component(axis)).Divide(2, Rounding.HalfToEven);
        string way = axis == Axis.X ? "east–west" : "north–south";
        if (GroupCopy.Mirror(editor.Sketch, boxes, axis, plane, out Box? refused, editor.NameOf) is not { } mirrored)
        {
            editor.Say(
                EditSeverity.Problem,
                $"Mirroring did not happen: {editor.NameOf(refused!.Id)} has cuts, and a mirror copy would carry them the "
                + "wrong way round. Duplicate it and shape the copy instead.");
            return null;
        }

        string what = boxes.Length == 1 ? $"Mirrored {editor.NameOf(boxes[0].Id)} {way}" : $"Mirrored {boxes.Length} parts {way}";
        editor.BeginGesture(what);
        EntityId? made = null;
        if (editor.Apply(Batch.Of([.. mirrored.Requests]), what) is Succeeded)
        {
            editor.SelectAll(mirrored.Copies.Values);
            made = mirrored.Copies[boxes[0].Id];
            editor.Say(EditSeverity.Done, $"{what}, across the middle of the drawing.");
        }

        editor.EndGesture();
        return made;
    }

    /// <summary>
    /// How far a copy of a box is put from it: past its extent along the narrower plan axis, and a
    /// grid step more.
    /// </summary>
    public static Vector3 BesideOffset(Box box, double gridStepInches)
    {
        ArgumentNullException.ThrowIfNull(box);

        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(box);
        return BesideOffset(low, high, gridStepInches);
    }

    /// <summary>How far copies of what spans an extent are put from it: past it along its narrower plan axis, and a grid step more.</summary>
    public static Vector3 BesideOffset(Point3 low, Point3 high, double gridStepInches)
    {
        Length step = new(SnapGrid.UnitsPerStep(gridStepInches));
        Length across = high.X - low.X;
        Length along = high.Y - low.Y;
        return across <= along
            ? Vector3.Along(Axis.X, across + step)
            : Vector3.Along(Axis.Y, along + step);
    }

    /// <summary>The selected boxes, in id order.</summary>
    public static Box[] SelectedBoxes(DesignEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return [.. editor.Selection.OrderBy(id => id).Select(editor.Sketch.Find<Box>).OfType<Box>()];
    }

    /// <summary>What a move of a pinned part says: "Moved Top did not happen: Top is pinned where it is."</summary>
    /// <param name="what">What was attempted: "Moved Top".</param>
    /// <param name="name">The pinned part's name.</param>
    public static string PinnedRefusal(string what, string name) => $"{what} did not happen: {name} is pinned where it is.";

    /// <summary>The offer beside <see cref="PinnedRefusal"/>: unpinning the part, as one undo step.</summary>
    public const string UnpinOffer = "Unpin it";

    /// <summary>
    /// What a move or a resize that left the part exactly where it was says at the drop: that it
    /// stayed put, and — when a pin is why — the way out, unpinning it, as one undo step. Nothing is
    /// stated about a snap the part never reached.
    /// </summary>
    /// <param name="editor">The drawing.</param>
    /// <param name="id">The part.</param>
    /// <param name="what">What was attempted: "Moved Top".</param>
    public static void SayStayedPut(DesignEditor editor, EntityId id, string what)
    {
        ArgumentNullException.ThrowIfNull(editor);

        string name = editor.NameOf(id);
        if (editor.Sketch.RelationshipsInOrder.OfType<Anchored>().FirstOrDefault(pin => pin.Entity == id) is { } pin)
        {
            editor.Show(new EditMessage(
                EditSeverity.Problem,
                PinnedRefusal(what, name),
                [pin.Id],
                new EditOffer(UnpinOffer, new RemoveRelationship(pin.Id), $"Unpinned {name}")));
            return;
        }

        editor.Say(EditSeverity.Hint, $"{name} stayed where it was: what holds it did not let it go further than that.");
    }

    /// <summary>
    /// The part the shape workshop should open, or <see langword="null"/> after saying why there
    /// is none: the workshop's scope is one box (<c>docs/design/shaped-parts-model.md</c> &#xA7;7.1).
    /// </summary>
    public static EntityId? PartToShape(DesignEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (editor.OnlySelectedBox is not { } part)
        {
            editor.Say(
                EditSeverity.Hint,
                editor.Selection.Count == 0
                    ? "Select a part to shape it."
                    : "The shape workshop takes one part at a time; select just the one.");
            return null;
        }

        return part.Id;
    }
}

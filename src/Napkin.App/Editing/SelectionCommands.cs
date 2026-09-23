using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

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
    /// Makes a copy of the selected part beside it and selects it
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>A duplicate is a value copy.</strong> The blank, its cuts, the part — stock, species,
    /// plan axes, quantity — the depth and the name are all carried across by the record's own
    /// <c>with</c>; the copy gets a new id, a new anchor and <em>no relationships</em>. It is
    /// unrelated until somebody snaps it, exactly like a part just drawn. Four duplicates of one
    /// gusset are equal by value, so the cut list groups them into one row of four on its own.
    /// </para>
    /// <para>
    /// <strong>Beside it, clear of it</strong> (#71): along the narrower of its two plan sides —
    /// east when it is narrower east–west, north otherwise — by its own extent that way and one grid
    /// step of the view that asked. So the copy never overlaps the original, lines up with it on the
    /// other two axes, and has the shortest way to go to be seen.
    /// </para>
    /// </remarks>
    /// <param name="editor">The drawing.</param>
    /// <param name="gridStepInches">The grid step in force in the view that asked.</param>
    /// <returns>The copy's id, or <see langword="null"/> when nothing was copied.</returns>
    public static EntityId? Duplicate(DesignEditor editor, double gridStepInches)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (editor.OnlySelectedBox is not { } source)
        {
            editor.Say(
                EditSeverity.Hint,
                editor.Selection.Count == 0
                    ? "Select a part to duplicate it."
                    : "Duplicate copies one part at a time; select just the one.");
            return null;
        }

        Box copy = source with
        {
            Id = EntityId.New(),
            Anchor = source.Anchor + BesideOffset(source, gridStepInches),
        };

        string what = $"Duplicated {editor.NameOf(source.Id)}";
        editor.BeginGesture(what);
        EntityId? made = null;
        if (editor.Apply(new AddEntity(copy), what) is Succeeded)
        {
            editor.Select(copy.Id);
            editor.Say(EditSeverity.Done, $"{what} as {editor.NameOf(copy.Id)}.");
            made = copy.Id;
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
        Length step = new(SnapGrid.UnitsPerStep(gridStepInches));
        Length across = high.X - low.X;
        Length along = high.Y - low.Y;
        return across <= along
            ? Vector3.Along(Axis.X, across + step)
            : Vector3.Along(Axis.Y, along + step);
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

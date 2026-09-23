using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// A quarter turn of the selected part about a world axis: the 3D view's turn command, and the plan
/// canvas's too (<c>docs/design/assembly-model.md</c> &#xA7;8.3, &#xA7;11 decision 12).
/// </summary>
/// <remarks>
/// <para>
/// With 24 orientations there are four answers to "turn it about X", so a turn is a command, not a
/// drag: one <see cref="SetOrientation"/> to <see cref="Orientation.TurnedAbout"/>, through the
/// editor like every other edit, one undo step. No rotation gizmo — ring-dragging would choose
/// among a continuum to land on the same four stops.
/// </para>
/// <para>
/// <strong>In place</strong> (#75). <see cref="SetOrientation"/> keeps the anchor — the local
/// south-west-bottom corner — where it is, so on its own a turn swings the part about that corner,
/// and three of the six face-up results put the part below its anchor: a leg standing on the floor
/// would go through it. So the command sets the part back down where it was: the low corner of its
/// extent — its least X, Y and Z — is the same after the turn as before, and the part still sits on
/// whatever it sat on. That is a <see cref="SetPosition"/> after the turn, in one
/// <see cref="Batch"/>, and exact: an extent is the anchor plus or minus stored sizes. A part pinned
/// where it is cannot be moved, so it turns about its anchor as the kernel's request does, and the
/// message says so.
/// </para>
/// <para>
/// A part held by a place in its own frame — a <see cref="Flush"/>, a <see cref="Coincident"/>, an
/// <see cref="AxisDistance"/>, a <see cref="Centered"/> — is refused a turn by the updater
/// (<see cref="RejectionReason.OrientationWithRelationships"/>, &#xA7;2.4), and the message says to
/// remove them first. That is the updater's rule, and nothing here works round it.
/// </para>
/// </remarks>
public static class SelectionTurn
{
    /// <summary>Turns the one selected part a number of quarter turns about a world axis, right-handed.</summary>
    /// <param name="editor">The drawing.</param>
    /// <param name="axis">The world axis to turn about.</param>
    /// <param name="quarterTurns">How many quarter turns; negative turns the other way.</param>
    /// <returns>What the updater said, or <see langword="null"/> when nothing was asked of it.</returns>
    public static UpdateResult? Turn(DesignEditor editor, Axis axis, int quarterTurns)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (editor.OnlySelectedBox is not { } box)
        {
            editor.Say(
                EditSeverity.Hint,
                editor.Selection.Count == 0
                    ? "Select a part to turn it."
                    : "Turning takes one part at a time; select just the one.");
            return null;
        }

        if (!box.Orientation.IsExact)
        {
            editor.Say(EditSeverity.Problem, $"{editor.NameOf(box.Id)} is not at a quarter turn, and this build turns parts by quarter turns only.");
            return null;
        }

        Orientation turned = box.Orientation.TurnedAbout(axis, quarterTurns);
        bool pinned = IsPinned(editor.Sketch, box.Id);
        string what = $"Turned {editor.NameOf(box.Id)} a quarter turn about {axis}"
                      + (quarterTurns < 0 ? ", the other way" : string.Empty)
                      + (pinned ? ", about the corner it is pinned by" : string.Empty);

        editor.BeginGesture(what);
        UpdateResult result = editor.Apply(RequestFor(box, turned, pinned), what);
        editor.EndGesture();
        return result;
    }

    /// <summary>
    /// What turning a box to an orientation asks of the updater: the turn, then — unless the box is
    /// pinned — the move that puts the low corner of its extent back where it was.
    /// </summary>
    public static Request RequestFor(Box box, Orientation turned, bool pinned)
    {
        ArgumentNullException.ThrowIfNull(box);

        SetOrientation turn = SetOrientation.To(box.Id, turned);
        if (pinned)
        {
            return turn;
        }

        Point3 lowBefore = SpaceSnapResolver.Extent(box).Low;
        Point3 lowAfter = SpaceSnapResolver.Extent(box with { FaceUp = turned.FaceUp, Rotation = turned.Rotation }).Low;
        Vector3 back = lowBefore - lowAfter;
        return back == Vector3.Zero
            ? turn
            : Batch.Of(turn, new SetPosition(box.Id, box.Anchor + back));
    }

    static bool IsPinned(Sketch sketch, EntityId id) =>
        sketch.RelationshipsInOrder.OfType<Anchored>().Any(anchored => anchored.Entity == id);
}

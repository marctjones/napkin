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
        string what = $"Turned {editor.NameOf(box.Id)} a quarter turn about {axis}"
                      + (quarterTurns < 0 ? ", the other way" : string.Empty);

        editor.BeginGesture(what);
        UpdateResult result = editor.Apply(SetOrientation.To(box.Id, turned), what);
        editor.EndGesture();
        return result;
    }
}

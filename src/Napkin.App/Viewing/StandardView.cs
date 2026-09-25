using Avalonia.Input;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>
/// The one source of truth for the standard views (§4.1): their exact directions, the camera that
/// looks along each, their names, and which key asks for which view.
/// </summary>
public static class StandardViews
{
    /// <summary>The six, in menu and key order.</summary>
    public static IReadOnlyList<StandardView> All => StandardViewFrame.All;

    /// <summary>
    /// The fixed direction of a view (§1.1's table), with exact ±1 vectors — no trigonometry at right
    /// angles (§4.1's trap). Screen right, screen up, and the direction from the drawing to the eye.
    /// </summary>
    public static (Vector3d Right, Vector3d Up, Vector3d TowardViewer) Axes(StandardView view)
    {
        (SignedAxis right, SignedAxis up, SignedAxis toward) = StandardViewFrame.Axes(view);
        return (Along(right), Along(up), Along(toward));
    }

    /// <summary>A signed world axis as an exact unit vector.</summary>
    public static Vector3d Along(SignedAxis axis) => axis.Axis switch
    {
        Axis.X => new(axis.Sign, 0, 0),
        Axis.Y => new(0, axis.Sign, 0),
        _ => new(0, 0, axis.Sign),
    };

    /// <summary>
    /// The orthographic camera looking along a view, keeping the given camera's centre, scale and
    /// viewport. The angles come from the 3D view's own view-snap mapping (#132), so a view and the
    /// matching snap button can never disagree about which way is which; <see cref="Camera"/> gives
    /// exact axis vectors at right angles, so the result is exactly <see cref="Axes"/>.
    /// </summary>
    public static Camera CameraFor(StandardView view, Camera keepingCentreAndScale) =>
        ViewSnap.Facing(keepingCentreAndScale, Axes(view).TowardViewer) with { Projection = CameraProjection.Orthographic };

    /// <summary>The view's name, as the menu, the chips and the status bar say it.</summary>
    public static string Name(StandardView view) => StandardViewFrame.Name(view);

    /// <summary>A design view's name: a standard view's, or "3D".</summary>
    public static string Name(DesignView view) => Of(view) is { } standard ? Name(standard) : "3D";

    /// <summary>The standard view a design view is, or null for the 3D view.</summary>
    public static StandardView? Of(DesignView view) =>
        view is >= DesignView.Top and <= DesignView.Right ? (StandardView)(int)view : null;

    /// <summary>The design view that shows a standard view.</summary>
    public static DesignView ToDesignView(StandardView view) => (DesignView)(int)view;

    /// <summary>
    /// The view an unmodified number key asks for (§4.2): 1–6 the standard views, 7 the 3D view, on
    /// the top row or the number pad. Null for any other key, or any digit with a modifier held —
    /// Cmd/Ctrl+1…9 belong to the Samples menu.
    /// </summary>
    public static DesignView? ForKey(Key key, KeyModifiers modifiers) =>
        KeyInput.From(key, modifiers) is { } keystroke && KeyMaps.View.Find(keystroke) is { } command
            ? KeyInput.ViewFor(command)
            : null;
}

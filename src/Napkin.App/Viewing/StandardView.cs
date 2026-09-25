using Avalonia.Input;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>
/// Which way the drawing is looked at: the six standard views (docs/design/standard-views.md §1),
/// each named for the side of the model the eye is on. The number keys and the View menu follow
/// this order, 1–6, with 3D as 7.
/// </summary>
public enum StandardView
{
    Top = 1,
    Bottom,
    Front,
    Back,
    Left,
    Right,
}

/// <summary>
/// The one source of truth for the standard views (§4.1): their exact directions, the camera that
/// looks along each, their names, and which key asks for which view.
/// </summary>
public static class StandardViews
{
    /// <summary>The six, in menu and key order.</summary>
    public static IReadOnlyList<StandardView> All { get; } =
        [StandardView.Top, StandardView.Bottom, StandardView.Front, StandardView.Back, StandardView.Left, StandardView.Right];

    /// <summary>
    /// The fixed direction of a view (§1.1's table), with exact ±1 vectors — no trigonometry at right
    /// angles (§4.1's trap). Screen right, screen up, and the direction from the drawing to the eye.
    /// </summary>
    public static (Vector3d Right, Vector3d Up, Vector3d TowardViewer) Axes(StandardView view) => view switch
    {
        StandardView.Top => (new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
        StandardView.Bottom => (new(1, 0, 0), new(0, -1, 0), new(0, 0, -1)),
        StandardView.Front => (new(1, 0, 0), new(0, 0, 1), new(0, -1, 0)),
        StandardView.Back => (new(-1, 0, 0), new(0, 0, 1), new(0, 1, 0)),
        StandardView.Left => (new(0, -1, 0), new(0, 0, 1), new(-1, 0, 0)),
        StandardView.Right => (new(0, 1, 0), new(0, 0, 1), new(1, 0, 0)),
        _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Not a standard view."),
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
    public static string Name(StandardView view) => view switch
    {
        StandardView.Top => "Top",
        StandardView.Bottom => "Bottom",
        StandardView.Front => "Front",
        StandardView.Back => "Back",
        StandardView.Left => "Left",
        StandardView.Right => "Right",
        _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Not a standard view."),
    };

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

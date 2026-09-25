using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

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

/// <summary>A world axis and which way along it: +X, −Y and so on. Exact — no trigonometry.</summary>
/// <param name="Axis">The world axis.</param>
/// <param name="Sign">+1 or −1.</param>
public readonly record struct SignedAxis(Axis Axis, int Sign)
{
    /// <summary>"+X", "−Z".</summary>
    public override string ToString() => (Sign < 0 ? "−" : "+") + Axis;
}

/// <summary>
/// The pure facts of the standard views (standard-views §1.1, §3.1, §5.3), free of any camera or
/// control: which world axis runs screen right and screen up in each, which way the eye looks, what
/// the view is called, and which world coordinates a person can read off it.
/// </summary>
public static class StandardViewFrame
{
    /// <summary>The six, in menu and key order.</summary>
    public static IReadOnlyList<StandardView> All { get; } =
        [StandardView.Top, StandardView.Bottom, StandardView.Front, StandardView.Back, StandardView.Left, StandardView.Right];

    static readonly SignedAxis PlusX = new(Axis.X, 1), MinusX = new(Axis.X, -1);
    static readonly SignedAxis PlusY = new(Axis.Y, 1), MinusY = new(Axis.Y, -1);
    static readonly SignedAxis PlusZ = new(Axis.Z, 1), MinusZ = new(Axis.Z, -1);

    /// <summary>
    /// §1.1's table: screen right, screen up, and the direction from the drawing to the eye. Nothing
    /// mirrors (§1.2): each is what a person standing on that side sees, Bottom rolled towards them
    /// about its front edge (X right, south up).
    /// </summary>
    public static (SignedAxis Right, SignedAxis Up, SignedAxis TowardViewer) Axes(StandardView view) => view switch
    {
        StandardView.Top => (PlusX, PlusY, PlusZ),
        StandardView.Bottom => (PlusX, MinusY, MinusZ),
        StandardView.Front => (PlusX, PlusZ, MinusY),
        StandardView.Back => (MinusX, PlusZ, PlusY),
        StandardView.Left => (MinusY, PlusZ, MinusX),
        StandardView.Right => (PlusY, PlusZ, PlusX),
        _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Not a standard view."),
    };

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

    /// <summary>Whether the view is an elevation: seen from the side, with up up (Front, Back, Left, Right).</summary>
    public static bool IsElevation(StandardView view) => Axes(view).Up.Axis == Axis.Z;

    /// <summary>
    /// The two world coordinates a view can show (§5.3): along screen right, then along screen up —
    /// x and y in Top and Bottom, x and z in Front and Back, y and z in Left and Right. Never the
    /// third, which the view cannot know.
    /// </summary>
    public static (Axis Across, Axis Upward) Readable(StandardView view)
    {
        (SignedAxis right, SignedAxis up, _) = Axes(view);
        return (right.Axis, up.Axis);
    }
}

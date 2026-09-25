using Napkin.App.Viewing;

namespace Napkin.App.Settings;

/// <summary>
/// The views of a design: the six standard views (docs/design/standard-views.md §1, Top being the
/// plan) and the 3D view, in the order of the View menu and the number keys 1–7.
/// </summary>
public enum DesignView
{
    Top = 1,
    Bottom,
    Front,
    Back,
    Left,
    Right,
    Model,
}

/// <summary>Which view a design opens in.</summary>
public enum OpenDesignsIn
{
    /// <summary>Whichever view the person was last in.</summary>
    LastUsed,
    Plan,
    Model,
}

/// <summary>Which theme the window wears: one of the two, or whichever the operating system is in.</summary>
public enum ThemeChoice
{
    FollowSystem,
    Light,
    Dark,
}

/// <summary>
/// The person's preferences, kept between runs. They belong to the person, not to a design, so they
/// are never written into a project file. Every setting has a default, and a file that says nothing
/// about one gets it.
/// </summary>
public sealed record UserSettings
{
    /// <summary>The file format's version. A file with any other version is not read (beta policy: no converters).</summary>
    /// <remarks>2: <see cref="DesignView"/> gained the six standard views, <c>Plan</c> becoming <c>Top</c>.</remarks>
    public const int CurrentVersion = 2;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>How the 3D view projects the model.</summary>
    public CameraProjection Projection { get; init; } = ModelView.DefaultProjection;

    /// <summary>Whether the rulers on the plan and the scale bar on orthographic 3D show.</summary>
    public bool ShowRulers { get; init; }

    /// <summary>The theme: follow the system unless the person picked one.</summary>
    public ThemeChoice Theme { get; init; } = ThemeChoice.FollowSystem;

    /// <summary>Whether the grid lines are drawn, in the plan and on the 3D ground. Drawing only; see <see cref="SnapToGrid"/>.</summary>
    public bool ShowGrid { get; init; } = true;

    /// <summary>The sheet the drawing is on (#142): cosmetic, in the plan and the 3D view.</summary>
    public SketchPaper SketchPaper { get; init; } = SketchPaper.Napkin;

    /// <summary>The pencil the drawing's lines are in (#142): cosmetic.</summary>
    public SketchLine SketchLine { get; init; } = SketchLine.Carpenter;

    /// <summary>
    /// The blade's saw kerf, in the cut layout and the shopping list (#138). Stored exactly, as a
    /// length; zero is allowed. The default is a practice default, not a fact about the person's blade.
    /// </summary>
    public Napkin.Core.Geometry.Length SawKerf { get; init; } = Napkin.Modules.Furniture.CutLayout.DefaultKerf;

    /// <summary>
    /// Whether the standard views (Bottom–Right) draw the edges a nearer part hides, as light dashes
    /// (docs/design/standard-views.md §2.3). On unless the person turns it off.
    /// </summary>
    public bool ShowHiddenEdges { get; init; } = true;

    /// <summary>Whether drags and tools land on the grid. Separate from whether it is drawn.</summary>
    public bool SnapToGrid { get; init; } = true;

    /// <summary>Which view a design opens in when it is opened (not the one on screen).</summary>
    public OpenDesignsIn OpenIn { get; init; } = OpenDesignsIn.LastUsed;

    /// <summary>The view the person was last in; what <see cref="OpenDesignsIn.LastUsed"/> means.</summary>
    public DesignView LastView { get; init; } = DesignView.Top;

    /// <summary>The view a newly opened design starts in: the plan is Top.</summary>
    public DesignView ViewForNewDesign() => OpenIn switch
    {
        OpenDesignsIn.Plan => DesignView.Top,
        OpenDesignsIn.Model => DesignView.Model,
        _ => LastView,
    };
}

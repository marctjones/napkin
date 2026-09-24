using Napkin.App.Viewing;

namespace Napkin.App.Settings;

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
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>How the 3D view projects the model.</summary>
    public CameraProjection Projection { get; init; } = ModelView.DefaultProjection;

    /// <summary>Whether the rulers on the plan and the scale bar on orthographic 3D show.</summary>
    public bool ShowRulers { get; init; }

    /// <summary>The theme: follow the system unless the person picked one.</summary>
    public ThemeChoice Theme { get; init; } = ThemeChoice.FollowSystem;
}

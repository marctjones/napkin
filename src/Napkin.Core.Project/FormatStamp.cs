namespace Napkin.Core.Project;

/// <summary>
/// What a project file has to say about itself before any of its scene can be believed: the
/// format version, and the units its integers are in.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Exact match, no migration.</strong> The reader accepts only
/// <see cref="Current"/>. Any other version — older <em>or</em> newer — fails before the scene is
/// parsed, naming the file's version and the app's. There is no migration code and no
/// compatibility shim, per the beta policy (DESIGN.md &#xA7;12 and &#xA7;6.4).
/// </para>
/// <para>
/// This stamp heads the scene document, whether that document is a plain <c>scene.json</c> or the
/// <c>scene.json</c> inside a <c>.napkin</c> container. The container's own
/// <see cref="ProjectManifest.ContainerVersion"/> is a second, separate stamp for a second,
/// separate thing: this one versions what a drawing <em>means</em>, and that one versions what the
/// container <em>is</em>. They move independently, and a plain scene file carrying this one is a
/// complete, refusable file with no manifest anywhere near it.
/// </para>
/// </remarks>
/// <param name="FormatVersion">The integer format version, bumped on every change to what the file means.</param>
/// <param name="LengthUnit">The unit lengths are integers of. Always <see cref="InchGrid"/> today.</param>
/// <param name="AngleUnit">The unit angles are integers of. Always <see cref="Arcsecond"/> today.</param>
public sealed record FormatStamp(int FormatVersion, string LengthUnit, string AngleUnit)
{
    /// <summary>The only length unit napkin stores: 1/1024 of an inch (geometry design &#xA7;1.1).</summary>
    public const string InchGrid = "inch/1024";

    /// <summary>The only angle unit napkin stores: the arcsecond (geometry design &#xA7;1.6).</summary>
    public const string Arcsecond = "arcsecond";

    /// <summary>The format version this build writes, and the only one it reads.</summary>
    /// <remarks>
    /// Version 2 added a <c>name</c> to every entity and a <c>part</c> to every box, which is what
    /// a cut list needs and a plan view cannot hold (<c>docs/design/parts-and-cut-list.md</c> §2).
    /// A version-1 file is refused, including one this repository committed: the beta policy has
    /// no converter in it.
    /// </remarks>
    public const int CurrentVersion = 2;

    /// <summary>The stamp this build writes, and the only one it accepts.</summary>
    public static readonly FormatStamp Current = new(CurrentVersion, InchGrid, Arcsecond);
}

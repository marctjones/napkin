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
/// In M1 a project is one plain <c>scene.json</c>, so the stamp is the first thing in that file.
/// In M2 the project becomes a zip container and the same stamp moves into <c>manifest.json</c>,
/// gaining the app version and the project's adopted code; the scene body below it does not
/// change. Keeping the stamp in its own type is what makes that move a change of where one record
/// is read from rather than a change to the reader.
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
    public const int CurrentVersion = 1;

    /// <summary>The stamp this build writes, and the only one it accepts.</summary>
    public static readonly FormatStamp Current = new(CurrentVersion, InchGrid, Arcsecond);
}

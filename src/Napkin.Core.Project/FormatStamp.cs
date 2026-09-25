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
    /// <para>
    /// Version 2 added a <c>name</c> to every entity and a <c>part</c> to every box, which is what
    /// a cut list needs and a plan view cannot hold (<c>docs/design/parts-and-cut-list.md</c> §2).
    /// A version-1 file is refused, including one this repository committed: the beta policy has
    /// no converter in it.
    /// </para>
    /// <para>
    /// Version 3 added a <c>cuts</c> array to every box, which is what a part that is not a plain
    /// rectangle needs (<c>docs/design/shaped-parts-model.md</c> §5). Every version-2 file is
    /// refused, including the two this repository committed until they were rewritten in the same
    /// change; a box with no cuts writes <c>"cuts": []</c>, because the format has no optional
    /// fields.
    /// </para>
    /// <para>
    /// Version 4 put a box in space (<c>docs/design/assembly-model.md</c> &#xA7;10): every box gained
    /// an <c>anchor.z</c>, a <c>depth</c> and a <c>faceUp</c>; a part lost <c>outOfPlane</c>, whose
    /// value is the box's depth; and the <c>corner</c> and <c>boxEdge</c> references gave way to one
    /// <c>feature</c> reference naming one, two or three faces of a box. Every version-3 file is
    /// refused, including the three this repository committed until they were rewritten in the same
    /// change.
    /// </para>
    /// <para>
    /// Version 5 added joinery (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;4.4): the
    /// <c>joint</c> relationship kind, <c>hardware</c> on a part, and <c>fastenerChoices</c> and
    /// <c>supplies</c> at the scene root. Every version-4 file is refused, including the ones this
    /// repository committed until they were rewritten in the same change.
    /// </para>
    /// <para>
    /// Version 6 added the building inputs (issues #18, #19): the project's adopted <c>code</c> and
    /// <c>site</c> values, and a <c>wall</c> on every box — what a wall supports and its stud spacing.
    /// </para>
    /// <para>
    /// Version 7 added <c>roofLiveLoad</c> to the site values (psf, null when not entered), which a
    /// header table's footnote may ask for (docs/rules-engine.md). Every version-6 file is refused.
    /// </para>
    /// <para>
    /// Version 8 added <c>bracing</c> to a wall's inputs (issue #39): the bracing method assigned to
    /// each of its segments, or null when none is. Every version-7 file is refused.
    /// </para>
    /// </remarks>
    public const int CurrentVersion = 8;

    /// <summary>The stamp this build writes, and the only one it accepts.</summary>
    public static readonly FormatStamp Current = new(CurrentVersion, InchGrid, Arcsecond);
}

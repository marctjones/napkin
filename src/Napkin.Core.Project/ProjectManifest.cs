using System.Globalization;
using System.Reflection;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project;

/// <summary>
/// The adopted building code a project is locked to: a pack and the revision of it the project
/// was last computed against (DESIGN.md &#xA7;11, <c>docs/design/rules-engine-model.md</c> &#xA7;7.1).
/// </summary>
/// <remarks>
/// <strong>Reserved, and not interpreted here.</strong> The rules engine is a draft design and
/// nothing in this build knows what a pack id means. The field exists now so that a project saved
/// by an M2 build and opened by an M4 build still says which code it was drawn against, instead
/// of that information having nowhere to live. <see cref="ProjectFile"/> reads it, checks its
/// shape, and hands it back untouched; it never looks a pack up and never refuses a project
/// because a pack is unknown.
/// </remarks>
/// <param name="Pack">The pack's id, for example <c>us-ct-2026</c>. Not empty.</param>
/// <param name="Revision">The revision of that pack. Zero or more.</param>
public sealed record AdoptedCode(string Pack, int Revision);

/// <summary>
/// What a <c>.napkin</c> container says about itself, in its <c>manifest.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Two stamps, for two layers, and that is deliberate.</strong> This manifest's
/// <see cref="ContainerVersion"/> versions the <em>container</em>: which entries there are and
/// what the manifest holds. The scene body inside keeps its own <see cref="FormatStamp"/> —
/// <c>formatVersion</c> and <c>units</c> — which versions what a drawing means. The two move
/// independently: adding a thumbnail entry would bump the container version and leave every scene
/// readable, while renaming a relationship field would bump the format version and leave the
/// container alone.
/// </para>
/// <para>
/// <strong>Exact match on both, and no migration.</strong> A container of any other version —
/// older <em>or</em> newer — is refused before the scene is parsed, naming the version found and
/// the version this build reads. napkin is a pre-1.0 beta indefinitely: there is no migration code
/// and no compatibility shim (DESIGN.md &#xA7;12).
/// </para>
/// </remarks>
/// <param name="ContainerVersion">The integer container version, bumped whenever the container's layout changes.</param>
/// <param name="AppVersion">The build that wrote the file, for a bug report. Never judged on load.</param>
/// <param name="AdoptedCode">The project's adopted code, or <see langword="null"/> when none is chosen.</param>
public sealed record ProjectManifest(int ContainerVersion, string AppVersion, AdoptedCode? AdoptedCode)
{
    /// <summary>The container version this build writes, and the only one it reads.</summary>
    public const int CurrentContainerVersion = 1;

    /// <summary>The manifest this build writes for a project that has not chosen an adopted code.</summary>
    public static ProjectManifest Current => new(CurrentContainerVersion, BuildVersion, null);

    /// <summary>
    /// The version string this build stamps into what it saves: the assembly's informational
    /// version, which CI extends with the commit SHA so a running beta can say exactly which
    /// commit wrote a file (DESIGN.md &#xA7;12).
    /// </summary>
    public static string BuildVersion { get; } =
        typeof(ProjectManifest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? typeof(ProjectManifest).Assembly.GetName().Version?.ToString(fieldCount: 3)
        ?? "unknown";

    /// <summary>This manifest with an adopted code chosen, or with the choice cleared.</summary>
    /// <param name="code">The adopted code, or <see langword="null"/> for none.</param>
    public ProjectManifest With(AdoptedCode? code) => this with { AdoptedCode = code };
}

/// <summary>
/// Everything a <c>.napkin</c> container holds: the drawing, and what the file says about itself.
/// </summary>
/// <remarks>
/// <para>
/// What <see cref="ProjectFile.Load(string)"/> hands back. Adding something to the container later
/// — a thumbnail, assets — adds a member here rather than another return value everywhere.
/// </para>
/// <para>
/// There is deliberately no way to save one of these. A caller does not choose the container
/// version or the app version: those say what wrote the file, and only the build doing the writing
/// knows them. <see cref="ProjectFile.Save(string, Sketch, AdoptedCode?)"/> takes the two things a
/// caller does own — the drawing and the adopted code — and stamps the rest itself.
/// </para>
/// </remarks>
/// <param name="Sketch">The drawing.</param>
/// <param name="Manifest">What the container says about itself.</param>
/// <param name="SceneStamp">What the scene body says about itself.</param>
public sealed record ProjectContents(Sketch Sketch, ProjectManifest Manifest, FormatStamp SceneStamp);

/// <summary>Every name <c>manifest.json</c> uses, in one place, read and written from here.</summary>
internal static class ManifestNames
{
    internal const string ContainerVersion = "containerVersion";
    internal const string AppVersion = "appVersion";
    internal const string AdoptedCode = "adoptedCode";
    internal const string Pack = "pack";
    internal const string Revision = "revision";

    /// <summary>A number the way every message in this assembly spells one.</summary>
    internal static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}

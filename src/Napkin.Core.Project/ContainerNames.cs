namespace Napkin.Core.Project;

/// <summary>The entries a <c>.napkin</c> container holds, and nothing else.</summary>
/// <remarks>
/// <para>
/// Strict, for the same reason an unknown field is a refusal: with the container version pinned
/// there is no legitimate reason for an entry this build does not define, and a reader that
/// silently ignored one would open a file it did not understand. Anything unknown is refused
/// naming the entry.
/// </para>
/// <para>
/// <strong><c>assets/</c> (container version 2).</strong> The survey underlay's image, named
/// <c>assets/&lt;sha256&gt;.png</c> or <c>.jpg</c> (docs/design/permit-set.md §5.4). DESIGN.md &#xA7;6.4's
/// <c>thumbnail.png</c> is still not written or read: a thumbnail means rendering, which lives in the
/// app, so a container holding one is refused like any other unknown entry.
/// </para>
/// </remarks>
internal static class ContainerNames
{
    /// <summary>What the container says about itself.</summary>
    internal const string Manifest = "manifest.json";

    /// <summary>The scene body, exactly as <see cref="SceneReader"/> reads it on its own.</summary>
    internal const string Scene = "scene.json";

    /// <summary>The file extension the app saves under.</summary>
    internal const string Extension = ".napkin";

    /// <summary>The entries every container holds, in the order they are written. Fixed, so that the bytes are.</summary>
    internal static readonly string[] InWriteOrder = [Manifest, Scene];

    /// <summary>
    /// Where images go (container version 2): <c>assets/&lt;sha256&gt;.png</c> or <c>.jpg</c>, written after
    /// the scene in name order, each one the scene refers to and nothing else.
    /// </summary>
    internal const string AssetsDirectory = "assets/";
}

/// <summary>
/// How much a container is allowed to be before this build stops reading it.
/// </summary>
/// <remarks>
/// <para>
/// A zip entry's stated size is written by whoever made the file, so it is a claim and not a
/// measurement. These limits are therefore enforced <em>while</em> the bytes are being
/// decompressed as well as against what the entry claims: a small file that expands to gigabytes
/// — a zip bomb — stops at the limit with a refusal rather than filling memory.
/// </para>
/// <para>
/// The numbers are set for what napkin actually saves. The largest sample scene is about 12 kB;
/// <see cref="MaxSceneBytes"/> is over a thousand times that, which is room for a drawing far
/// larger than a house and still small enough to hold in memory without thinking about it.
/// Raising one of these is a decision to make deliberately, not a default to drift.
/// </para>
/// </remarks>
public static class ContainerLimits
{
    /// <summary>The largest <c>.napkin</c> file this build will open: 32 MiB.</summary>
    public const long MaxContainerBytes = 32L * 1024 * 1024;

    /// <summary>The largest <c>scene.json</c> this build will decompress: 16 MiB.</summary>
    public const long MaxSceneBytes = 16L * 1024 * 1024;

    /// <summary>The largest <c>manifest.json</c> this build will decompress: 64 kiB.</summary>
    public const long MaxManifestBytes = 64L * 1024;

    /// <summary>
    /// The most entries this build will look at: 16. The format defines two, so a container with
    /// more than this is refused before any of them is read, rather than after a long walk.
    /// </summary>
    public const int MaxEntries = 16;

    /// <summary>
    /// The largest image this build will decompress: 8 MiB. A labelled default, well under
    /// <see cref="MaxContainerBytes"/>: a scanned survey page is a few MB.
    /// </summary>
    public const long MaxAssetBytes = 8L * 1024 * 1024;

    /// <summary>The limit that applies to one entry.</summary>
    internal static long For(string entry) => entry switch
    {
        ContainerNames.Manifest => MaxManifestBytes,
        ContainerNames.Scene => MaxSceneBytes,
        _ => MaxAssetBytes,
    };
}

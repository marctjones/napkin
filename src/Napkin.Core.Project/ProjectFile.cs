using System.Collections.Immutable;
using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project;

/// <summary>
/// Opens and saves a whole napkin project: a <c>.napkin</c> file, which is a zip holding
/// <c>manifest.json</c> and <c>scene.json</c>. The one entry point the app's Open and Save use.
/// </summary>
/// <remarks>
/// <para>
/// <strong>A documented zip, on purpose.</strong> The container is an ordinary zip file that any
/// zip tool opens, holding two ordinary JSON files, both documented in
/// <c>docs/file-format.md</c>. Someone with a drawing and no copy of napkin can still get at what
/// it says, which is the whole point of DESIGN.md &#xA7;6.4.
/// </para>
/// <para>
/// <strong>Two stamps, for two layers.</strong> The manifest carries a
/// <c>containerVersion</c> — which entries there are, and what the manifest holds — and the scene
/// inside carries the <c>formatVersion</c> and <c>units</c> it has carried since M1. Both are
/// exact-match: a project of any other version, older or newer, is refused before its scene is
/// parsed, naming what was found and what this build reads. There is no migration code
/// (DESIGN.md &#xA7;12).
/// </para>
/// <para>
/// <strong>Nothing is thrown for a bad file.</strong> Every refusal comes back as
/// <see cref="Refused"/> carrying <see cref="LoadProblem"/>s, exactly as
/// <see cref="SceneReader"/>'s do, so the app has one kind of message to show whichever way a
/// drawing was opened. A save that cannot be done comes back as <see cref="NotSaved"/>.
/// </para>
/// <para>
/// <strong>The plain <c>.scene.json</c> path stays.</strong> <see cref="SceneReader"/> still opens
/// a bare scene file — the hand-written samples, and anything a person writes in a text editor —
/// and this type never tries to. A caller that hands plain JSON to <see cref="Load(string)"/> is
/// told to use the reader rather than quietly sniffed at.
/// </para>
/// </remarks>
public static class ProjectFile
{
    /// <summary>
    /// The timestamp every entry in a saved container carries.
    /// </summary>
    /// <remarks>
    /// The zip epoch, and not the clock. A saved project has to be a function of the drawing and
    /// nothing else: two saves of the same drawing are byte-identical, so a project file diffs
    /// cleanly in git and "did this change?" is a checksum rather than an opinion. A real
    /// modification time would make every save differ from the last.
    /// </remarks>
    internal static readonly DateTimeOffset Timestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The file extension a napkin project is saved under.</summary>
    public static string Extension => ContainerNames.Extension;

    /// <summary>The container version this build writes, and the only one it reads.</summary>
    public static int ContainerVersion => ProjectManifest.CurrentContainerVersion;

    // -------------------------------------------------------------------------------------------
    // Saving
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Saves a drawing as a project file, replacing whatever was at <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// <strong>Atomic.</strong> The whole container is built in memory, written to a temporary
    /// file beside the target, flushed to disk, and only then moved over the target. A failure at
    /// any point — no room, no permission, a pulled cable — leaves the previous file exactly as it
    /// was, and leaves no temporary behind. There is no moment at which the project on disk is
    /// half a drawing.
    /// </remarks>
    /// <param name="path">Where to save. The extension is the caller's business.</param>
    /// <param name="sketch">The drawing to save.</param>
    /// <param name="adoptedCode">The project's adopted code, or <see langword="null"/> for none.</param>
    public static SaveResult Save(string path, Sketch sketch, AdoptedCode? adoptedCode = null)
        => Save(path, sketch, adoptedCode, CreateNew);

    /// <summary>Saves a drawing as a project file into a stream.</summary>
    /// <param name="stream">Where the bytes go. Not closed by this call.</param>
    /// <param name="sketch">The drawing to save.</param>
    /// <param name="adoptedCode">The project's adopted code, or <see langword="null"/> for none.</param>
    public static SaveResult Save(Stream stream, Sketch sketch, AdoptedCode? adoptedCode = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(sketch);

        if (Unsavable(sketch) is { } refusal)
        {
            return refusal;
        }

        byte[] bytes = Pack(sketch, adoptedCode);

        try
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
        catch (Exception exception) when (Writing(exception))
        {
            return Unwritable($"The project could not be written: {exception.Message}");
        }

        return new Saved(string.Empty);
    }

    /// <summary>The container bytes for a drawing, without writing them anywhere.</summary>
    /// <remarks>
    /// The same bytes <see cref="Save(string, Sketch, AdoptedCode?)"/> would write, for a caller
    /// that wants to hash them or hand them to something that is not a file.
    /// </remarks>
    /// <param name="sketch">The drawing to save.</param>
    /// <param name="adoptedCode">The project's adopted code, or <see langword="null"/> for none.</param>
    public static byte[] SaveToBytes(Sketch sketch, AdoptedCode? adoptedCode = null)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return Pack(sketch, adoptedCode);
    }

    /// <summary>
    /// The save, with the way a file is created injectable, so that a test can fail a write part
    /// way through and watch the previous file survive it.
    /// </summary>
    internal static SaveResult Save(
        string path, Sketch sketch, AdoptedCode? adoptedCode, Func<string, Stream> create)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(create);

        if (Unsavable(sketch) is { } refusal)
        {
            return refusal;
        }

        // Built before anything on disk is touched: a sketch the writer cannot spell fails here,
        // with the old file still in place.
        byte[] bytes = Pack(sketch, adoptedCode);

        string full = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(full) ?? Directory.GetCurrentDirectory();

        // The temporary sits in the same directory as the target, because a move between
        // directories may cross a filesystem boundary and stop being atomic.
        string temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(full)}.{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}.tmp");

        try
        {
            using (Stream target = create(temporary))
            {
                target.Write(bytes, 0, bytes.Length);
                target.Flush();

                // Flushed all the way down, so that a crash between here and the move cannot
                // leave a file the operating system had only promised to write.
                if (target is FileStream file)
                {
                    file.Flush(flushToDisk: true);
                }
            }

            File.Move(temporary, full, overwrite: true);
            return new Saved(full);
        }
        catch (Exception exception) when (Writing(exception))
        {
            Discard(temporary);
            return Unwritable($"The project could not be saved to \"{path}\": {exception.Message}");
        }
    }

    private static Stream CreateNew(string path)
        => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

    private static void Discard(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (Exception exception) when (Writing(exception))
        {
            // The save has already failed and is about to say so. A temporary that cannot be
            // deleted is not worth turning into a second, more confusing message.
        }
    }

    private static NotSaved? Unsavable(Sketch sketch)
    {
        // A sketch that does not validate would be written into a file this build refuses to
        // open. Saving it would turn a bug in the editor into a drawing the user cannot get back.
        ValidationResult validation = sketch.Validate();
        return validation.IsValid
            ? null
            : new NotSaved(
            [
                .. validation.Errors.Select(error => new SaveProblem(
                    SaveProblemKind.InvalidSketch,
                    $"The drawing is not one napkin could open again: {error.Message}")),
            ]);
    }

    private static NotSaved Unwritable(string message)
        => new([new SaveProblem(SaveProblemKind.Unwritable, message)]);

    private static bool Writing(Exception exception)
        => exception is IOException or UnauthorizedAccessException or NotSupportedException
            or SecurityException or ObjectDisposedException;

    /// <summary>
    /// The container, byte for byte the same for the same drawing: a fixed entry order, fixed
    /// timestamps, no extra fields, and two documents that are themselves deterministic.
    /// </summary>
    private static byte[] Pack(Sketch sketch, AdoptedCode? adoptedCode)
    {
        byte[] manifest = ManifestJson.Write(ProjectManifest.Current.With(adoptedCode));
        byte[] scene = SceneWriter.WriteToBytes(sketch);

        using MemoryStream buffer = new();

        // Seekable, so that the sizes go in the local headers rather than in trailing data
        // descriptors: one shape of output, not two.
        using (ZipArchive archive = new(buffer, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            foreach (string name in ContainerNames.InWriteOrder)
            {
                Store(archive, name, name == ContainerNames.Manifest ? manifest : scene);
            }
        }

        return buffer.ToArray();
    }

    private static void Store(ZipArchive archive, string name, byte[] bytes)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = Timestamp;

        using Stream target = entry.Open();
        target.Write(bytes, 0, bytes.Length);
    }

    // -------------------------------------------------------------------------------------------
    // Loading
    // -------------------------------------------------------------------------------------------

    /// <summary>Opens a project file, for the relationships the direct updater holds.</summary>
    /// <param name="path">The project to open.</param>
    public static LoadResult Load(string path) => Load(path, DirectUpdater.Instance);

    /// <summary>Opens a project file.</summary>
    /// <param name="path">The project to open.</param>
    /// <param name="updater">
    /// The updater the app is running, whose <see cref="IGeometryUpdater.SupportedRelationships"/>
    /// decide which relationship kinds this build can hold.
    /// </param>
    public static LoadResult Load(string path, IGeometryUpdater updater)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(updater);

        byte[] bytes;
        try
        {
            // The size is judged before the bytes are read, so that pointing napkin at a
            // ten-gigabyte file is a message rather than a wait.
            long length = new FileInfo(path).Length;
            if (length > ContainerLimits.MaxContainerBytes)
            {
                return Refuse(
                    LoadProblemKind.TooLarge,
                    path,
                    $"The file is {Bytes(length)}; napkin opens projects up to "
                    + $"{Bytes(ContainerLimits.MaxContainerBytes)}.");
            }

            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or NotSupportedException or SecurityException)
        {
            return Refuse(LoadProblemKind.Unreadable, path, $"The file could not be read: {exception.Message}");
        }

        return Open(bytes, updater);
    }

    /// <summary>Opens a project from a stream, for the relationships the direct updater holds.</summary>
    /// <param name="stream">The bytes of one <c>.napkin</c> container.</param>
    public static LoadResult Load(Stream stream) => Load(stream, DirectUpdater.Instance);

    /// <summary>Opens a project from a stream.</summary>
    /// <param name="stream">The bytes of one <c>.napkin</c> container.</param>
    /// <param name="updater">
    /// The updater the app is running, whose <see cref="IGeometryUpdater.SupportedRelationships"/>
    /// decide which relationship kinds this build can hold.
    /// </param>
    public static LoadResult Load(Stream stream, IGeometryUpdater updater)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(updater);

        byte[]? bytes;
        try
        {
            bytes = ReadBounded(stream, ContainerLimits.MaxContainerBytes);
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException
                                              or ObjectDisposedException)
        {
            return Refuse(LoadProblemKind.Unreadable, string.Empty, $"The project could not be read: {exception.Message}");
        }

        if (bytes is null)
        {
            return Refuse(
                LoadProblemKind.TooLarge,
                string.Empty,
                $"The project is larger than {Bytes(ContainerLimits.MaxContainerBytes)}, which is as much as "
                + "napkin will open.");
        }

        return Open(bytes, updater);
    }

    private static LoadResult Open(byte[] bytes, IGeometryUpdater updater)
    {
        List<LoadProblem> problems = [];

        using MemoryStream buffer = new(bytes, writable: false);

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);
        }
        catch (InvalidDataException exception)
        {
            return Refuse(
                LoadProblemKind.NotAContainer,
                string.Empty,
                $"This is not a napkin project: a project is a zip container holding {ContainerNames.Manifest} "
                + $"and {ContainerNames.Scene}, and these bytes are not a readable zip ({exception.Message}). "
                + "A plain scene file is opened with SceneReader instead.");
        }

        using (archive)
        {
            Dictionary<string, ZipArchiveEntry> entries = Inspect(archive, problems);
            if (problems.Count > 0)
            {
                return new Refused([.. problems]);
            }

            byte[]? manifestBytes = Extract(entries, ContainerNames.Manifest, problems);
            byte[]? sceneBytes = Extract(entries, ContainerNames.Scene, problems);
            if (manifestBytes is null || sceneBytes is null)
            {
                return new Refused([.. problems]);
            }

            // The manifest is judged before the scene is parsed, so that a container from another
            // version is refused for that reason and not for whatever the scene happens to say.
            ProjectManifest? manifest = ManifestJson.Read(manifestBytes, problems);
            if (manifest is null)
            {
                return new Refused([.. problems]);
            }

            using MemoryStream scene = new(sceneBytes, writable: false);
            return SceneReader.Read(scene, updater) switch
            {
                Loaded loaded => new LoadedProject(new ProjectContents(loaded.Sketch, manifest, loaded.Stamp)),
                Refused refused => Inside(refused),
                LoadResult other => other,
            };
        }
    }

    /// <summary>A scene refusal, with every problem's location said to be inside the container.</summary>
    private static Refused Inside(Refused refused)
        => new(
        [
            .. refused.Problems.Select(problem => problem with
            {
                Location = problem.Location.Length == 0
                    ? ContainerNames.Scene
                    : $"{ContainerNames.Scene}{problem.Location}",
            }),
        ]);

    /// <summary>
    /// Every entry, checked before any of them is opened: a name that could escape the container,
    /// an entry this format does not define, the same name twice, or simply too many.
    /// </summary>
    private static Dictionary<string, ZipArchiveEntry> Inspect(ZipArchive archive, List<LoadProblem> problems)
    {
        Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.Ordinal);

        if (archive.Entries.Count > ContainerLimits.MaxEntries)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.TooLarge,
                string.Empty,
                $"The container holds {ManifestNames.Number(archive.Entries.Count)} entries; a napkin project holds "
                + $"{ManifestNames.Number(ContainerNames.InWriteOrder.Length)}, and napkin will not look at more "
                + $"than {ManifestNames.Number(ContainerLimits.MaxEntries)}."));
            return entries;
        }

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string name = entry.FullName;

            if (!IsSafeName(name))
            {
                // Never written to disk, so this cannot overwrite anything — but a container
                // carrying such a name is not one napkin wrote, and is refused rather than
                // silently tidied up.
                problems.Add(new LoadProblem(
                    LoadProblemKind.UnsafeEntryName,
                    Quote(name),
                    $"The container holds an entry named {Quote(name)}, which is not a plain name inside the "
                    + "container. An entry that is rooted, that steps up with \"..\", that uses a backslash or a "
                    + "drive, or that is a directory is refused."));
                continue;
            }

            if (!ContainerNames.InWriteOrder.Contains(name, StringComparer.Ordinal))
            {
                problems.Add(new LoadProblem(
                    LoadProblemKind.UnknownEntry,
                    name,
                    $"\"{name}\" is not an entry this format defines. A napkin project holds "
                    + $"{string.Join(" and ", ContainerNames.InWriteOrder)} and nothing else: with the container "
                    + "version pinned there is no legitimate reason for another entry."));
                continue;
            }

            if (!entries.TryAdd(name, entry))
            {
                problems.Add(new LoadProblem(
                    LoadProblemKind.DuplicateEntry,
                    name,
                    $"The container holds \"{name}\" more than once, so which of them is the project is not "
                    + "something napkin is willing to guess."));
            }
        }

        return entries;
    }

    /// <summary>
    /// A name that is one plain file inside the container. Everything a zip is allowed to say and
    /// napkin is not willing to hear.
    /// </summary>
    private static bool IsSafeName(string name)
    {
        if (name.Length == 0
            || name.EndsWith('/')
            || name.Contains('\\', StringComparison.Ordinal)
            || name.Contains(':', StringComparison.Ordinal)
            || Path.IsPathRooted(name)
            || name.Any(char.IsControl))
        {
            return false;
        }

        foreach (string segment in name.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// One entry's bytes, decompressed under a limit that is enforced as the bytes arrive.
    /// </summary>
    /// <remarks>
    /// A zip entry's stated size is written by whoever made the file, so it is checked <em>and</em>
    /// the decompressed bytes are counted: a small container that claims to be small and expands
    /// to gigabytes stops at the limit instead of filling memory.
    /// </remarks>
    private static byte[]? Extract(
        Dictionary<string, ZipArchiveEntry> entries, string name, List<LoadProblem> problems)
    {
        if (!entries.TryGetValue(name, out ZipArchiveEntry? entry))
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.MissingEntry,
                name,
                $"The container has no \"{name}\". A napkin project holds "
                + $"{string.Join(" and ", ContainerNames.InWriteOrder)}."));
            return null;
        }

        long limit = ContainerLimits.For(name);
        if (entry.Length > limit)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.TooLarge,
                name,
                $"\"{name}\" says it is {Bytes(entry.Length)}; napkin reads it up to {Bytes(limit)}."));
            return null;
        }

        try
        {
            using Stream source = entry.Open();
            using MemoryStream buffer = new();
            byte[] chunk = new byte[81920];
            long total = 0;

            int read;
            while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
            {
                total += read;
                if (total > limit)
                {
                    problems.Add(new LoadProblem(
                        LoadProblemKind.TooLarge,
                        name,
                        $"\"{name}\" said it was {Bytes(entry.Length)} and is still going past {Bytes(limit)}. "
                        + "A container whose stated sizes are not its real ones is refused."));
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.NotAContainer,
                name,
                $"\"{name}\" could not be read out of the container: {exception.Message}. The zip is damaged or "
                + "was cut short."));
            return null;
        }
    }

    /// <summary>Every byte of a stream, or <see langword="null"/> if there are more than <paramref name="limit"/>.</summary>
    private static byte[]? ReadBounded(Stream stream, long limit)
    {
        if (stream.CanSeek && stream.Length - stream.Position > limit)
        {
            return null;
        }

        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        long total = 0;

        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += read;
            if (total > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static Refused Refuse(LoadProblemKind kind, string location, string message)
        => new(ImmutableList.Create(new LoadProblem(kind, location, message)));

    private static string Quote(string name)
        => $"\"{name.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    /// <summary>A size in the units a person reads, for a message.</summary>
    private static string Bytes(long count) => count switch
    {
        >= 1024 * 1024 => $"{ManifestNames.Number(count / (1024 * 1024))} MB",
        >= 1024 => $"{ManifestNames.Number(count / 1024)} kB",
        _ => $"{ManifestNames.Number(count)} bytes",
    };
}

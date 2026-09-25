using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Napkin.Core.Project;

namespace Napkin.App.Designs;

/// <summary>
/// A design read from a scene file on disk, through <see cref="SceneReader"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the only way a drawing reaches the canvas: the samples in the Samples menu are these
/// too, pointed at the files that ship beside the executable (<see cref="SampleFiles"/>). There is
/// no second, in-code copy of a sample to drift away from the files, and no second path for a file
/// to be trusted by.
/// </para>
/// <para>
/// <strong>It refuses rather than repairs, and it never throws an I/O or format failure past the
/// window.</strong> The reader returns <see cref="Refused"/> for a missing, empty, garbled,
/// wrong-version or dangling-reference file, and that becomes a <see cref="DesignLoadException"/>
/// carrying one line per <see cref="LoadProblem"/>. An I/O or container-format exception the
/// reader did not itself catch (<see cref="ProjectFile.IsFileException"/>) becomes the same thing;
/// a programming error is not caught here (#175) and is left to surface as the bug it is, rather
/// than as a misleading "file refused".
/// </para>
/// <para>
/// <strong>Entities carry a name.</strong> Scene format version 2 put one on every entity, so
/// <see cref="Design.Labels"/> is filled from the file itself and a leg drawn from
/// <c>coffee-table.scene.json</c> says "Leg, south-west" on the canvas. A name left empty is a
/// name the file does not state, and nothing is drawn for it.
/// </para>
/// </remarks>
public sealed class FileDesignSource : IDesignSource
{
    /// <param name="path">The scene file to read. It is read afresh on every <see cref="Load"/>.</param>
    /// <param name="name">
    /// What to call the design — the sample's title in the Samples menu, or the file's own name for
    /// a file a person picked.
    /// </param>
    /// <param name="description">
    /// One line about what it is, with the file's name appended so the status line always says
    /// which file is on screen. Left out — which is what <em>File &#x2192; Open&#x2026;</em> does,
    /// having nothing to say about a file it has never seen — the whole path stands in for it.
    /// </param>
    public FileDesignSource(string path, string? name = null, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Path = path;
        FileName = System.IO.Path.GetFileName(path);
        Name = string.IsNullOrWhiteSpace(name) ? FileName : name;
        Description = string.IsNullOrWhiteSpace(description)
            ? path
            : $"{description}  ({FileName})";
    }

    /// <summary>The file this source reads.</summary>
    public string Path { get; }

    /// <summary>The file's name without its directory, for the title and the status line.</summary>
    public string FileName { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <inheritdoc/>
    public Design Load()
    {
        LoadResult result;
        try
        {
            result = SceneReader.ReadFile(Path);
        }
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            // The reader is written to return a refusal rather than throw for a bad file, so
            // reaching here means the same class of I/O or format failure escaped it (a file that
            // vanished or was locked between the picker and the read, say). Narrowed to that set
            // (#175, ProjectFile.IsFileException) so a genuine programming error is not swallowed
            // and reported as "file refused" — it propagates and is visibly a bug.
            throw new DesignLoadException(
                $"{FileName} could not be read: {exception.Message}",
                exception);
        }

        return result switch
        {
            Loaded loaded => Design.Named(Name, loaded.Sketch),
            Refused refused => throw new DesignLoadException(
                refused.Summary,
                refused.Problems.Select(problem => problem.ToString())),
            _ => throw new DesignLoadException(
                $"{FileName} could not be read: the reader returned {result.GetType().Name}, "
                + "which this build does not know how to show."),
        };
    }
}

/// <summary>
/// Asks the person for a scene file to open, or for where to save one.
/// </summary>
/// <remarks>
/// The seam exists for one reason: the platform's open dialog is native, and the headless GUI suite
/// cannot drive a native dialog (<c>docs/testing/gui-automation.md</c>, "What headless cannot
/// cover"). A workflow therefore substitutes a picker that answers with a path — everything after
/// that point, including the menu item, the shortcut, the reader and the refusal panel, is the real
/// thing. The dialog itself stays for the real-OS smoke layer.
/// </remarks>
public interface ISceneFilePicker
{
    /// <summary>
    /// The scene file to open, or <see langword="null"/> when the person cancelled.
    /// </summary>
    Task<string?> PickSceneFileAsync();

    /// <summary>
    /// Where to save the drawing, or <see langword="null"/> when the person cancelled.
    /// </summary>
    /// <param name="suggestedName">The file name the dialog offers to begin with.</param>
    Task<string?> PickSaveDestinationAsync(string suggestedName);
}

/// <summary>The platform's own open dialog, through Avalonia's storage provider.</summary>
/// <param name="owner">The window the dialog belongs to.</param>
public sealed class StorageProviderScenePicker(TopLevel owner) : ISceneFilePicker
{
    private readonly TopLevel owner = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <summary>The file types the dialog offers.</summary>
    public static FilePickerFileType SceneFiles { get; } = new("napkin scene files")
    {
        Patterns = ["*.scene.json", "*.json"],
        MimeTypes = ["application/json"],
    };

    /// <inheritdoc/>
    public async Task<string?> PickSceneFileAsync()
    {
        IStorageProvider? storage = owner.StorageProvider;
        if (storage is null || !storage.CanOpen)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> chosen = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a napkin design",
            AllowMultiple = false,
            FileTypeFilter = [SceneFiles],
        }).ConfigureAwait(true);

        // A provider that hands back something with no local path — a cloud item on a phone, say —
        // is not something the file reader can open, and pretending otherwise would fail later and
        // further from the cause.
        return chosen.Count == 0 ? null : chosen[0].TryGetLocalPath();
    }

    /// <inheritdoc/>
    public async Task<string?> PickSaveDestinationAsync(string suggestedName)
    {
        IStorageProvider? storage = owner.StorageProvider;
        if (storage is null || !storage.CanSave)
        {
            return null;
        }

        IStorageFile? chosen = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save this napkin design",
            SuggestedFileName = suggestedName,
            DefaultExtension = "scene.json",
            FileTypeChoices = [SceneFiles],
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);

        // The same rule as opening: a destination with no local path is one the writer cannot
        // write to, and saying nothing now is better than failing further from the cause.
        return chosen?.TryGetLocalPath();
    }
}

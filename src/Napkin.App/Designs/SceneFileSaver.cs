using System.Globalization;
using System.Security;
using Napkin.Core.Geometry;
using Napkin.Core.Project;

namespace Napkin.App.Designs;

/// <summary>
/// Writes a sketch to a scene file on disk, through <see cref="SceneWriter"/> — the file
/// <em>File &#x2192; Open&#x2026;</em> reads back through <see cref="SceneReader"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>It refuses a sketch this build could not open again.</strong> The reader refuses a
/// sketch that does not validate, so writing one would turn a bug in the editor into a drawing the
/// person cannot get back. <c>ProjectFile</c> makes the same check before it writes a project.
/// </para>
/// <para>
/// <strong>A failed save leaves the previous file where it was.</strong> The bytes go to a
/// temporary beside the target and are moved over it only once they are all on disk, so a full
/// disk or a pulled drive part way through costs the new save, never the old one.
/// </para>
/// <para>
/// It never throws for anything the file system can do: every failure comes back as a
/// <see cref="SceneNotSaved"/> with a sentence a person can read.
/// </para>
/// </remarks>
public static class SceneFileSaver
{
    /// <summary>Writes the sketch to <paramref name="path"/>, replacing whatever is there.</summary>
    /// <param name="path">The scene file to write.</param>
    /// <param name="sketch">The drawing.</param>
    /// <returns>Where it went, or why it did not.</returns>
    public static SceneSaveResult Save(string path, Sketch sketch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(sketch);

        ValidationResult validation = sketch.Validate();
        if (!validation.IsValid)
        {
            return new SceneNotSaved(
            [
                .. validation.Errors.Select(error =>
                    $"The drawing is not one napkin could open again: {error.Message}"),
            ]);
        }

        string full;
        string directory;
        byte[] bytes;
        try
        {
            full = Path.GetFullPath(path);
            directory = Path.GetDirectoryName(full) ?? Directory.GetCurrentDirectory();

            // Built before anything on disk is touched: a sketch the writer cannot spell fails
            // here, with the old file still in place.
            bytes = SceneWriter.WriteToBytes(sketch);
        }
        catch (Exception exception) when (Writing(exception))
        {
            return new SceneNotSaved([$"{Path.GetFileName(path)} could not be saved: {exception.Message}"]);
        }

        // The temporary sits beside the target, because a move between directories may cross a
        // file system and stop being a single step.
        string temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(full)}.{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}.tmp");

        try
        {
            using (FileStream target = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                target.Write(bytes, 0, bytes.Length);
                target.Flush(flushToDisk: true);
            }

            File.Move(temporary, full, overwrite: true);
            return new SceneSaved(full);
        }
        catch (Exception exception) when (Writing(exception))
        {
            Discard(temporary);
            return new SceneNotSaved([$"{Path.GetFileName(full)} could not be saved: {exception.Message}"]);
        }
    }

    static void Discard(string temporary)
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

    static bool Writing(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or NotSupportedException
            or SecurityException or ArgumentException;
}

/// <summary>What <see cref="SceneFileSaver.Save"/> did.</summary>
public abstract record SceneSaveResult;

/// <summary>The file was written.</summary>
/// <param name="Path">The full path it was written to.</param>
public sealed record SceneSaved(string Path) : SceneSaveResult;

/// <summary>Nothing was written, and whatever was at the path before is still there.</summary>
/// <param name="Problems">One readable line per reason. Never empty.</param>
public sealed record SceneNotSaved(IReadOnlyList<string> Problems) : SceneSaveResult;

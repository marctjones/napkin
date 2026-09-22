using System.Text.Json;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project;

/// <summary>
/// Reads a napkin scene file into a <see cref="Sketch"/>. The one entry point for opening a
/// project in M1.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Strict, and it never repairs.</strong> The format version must be exactly the one this
/// build writes; an unknown or repeated field, a decimal where an integer unit belongs, an id that
/// names nothing or names the wrong kind of entity, and a relationship kind this build's updater
/// cannot hold are each a refusal. Every refusal names what was wrong, because M1's viewer shows
/// the message to the user (issue #6).
/// </para>
/// <para>
/// A sketch comes back only after <see cref="Sketch.Validate"/> and
/// <see cref="RelationshipChecker.Check(Sketch)"/> have both passed on it, so a file whose
/// geometry does not satisfy its own stored relationships is refused with the violations listed
/// rather than opened approximately (geometry design &#xA7;6).
/// </para>
/// <para>
/// This reads one plain scene document. <see cref="ProjectFile"/> reads the same document out of a
/// <c>.napkin</c> container, through this very method — the container wraps the scene, it does not
/// change it — and both doors stay open, because the hand-written samples and anything a person
/// writes in a text editor are plain scene documents. <see cref="SceneWriter"/> is this reader's
/// mirror image.
/// </para>
/// </remarks>
public static class SceneReader
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 64,
    };

    /// <summary>The format version this build reads, and no other.</summary>
    public static int FormatVersion => FormatStamp.CurrentVersion;

    /// <summary>Reads the scene file at a path, for the relationships the direct updater holds.</summary>
    /// <param name="path">The file to read.</param>
    public static LoadResult ReadFile(string path) => ReadFile(path, DirectUpdater.Instance);

    /// <summary>Reads the scene file at a path.</summary>
    /// <param name="path">The file to read.</param>
    /// <param name="updater">
    /// The updater the app is running, whose <see cref="IGeometryUpdater.SupportedRelationships"/>
    /// decide which relationship kinds this build can hold.
    /// </param>
    public static LoadResult ReadFile(string path, IGeometryUpdater updater)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(updater);

        FileStream stream;
        try
        {
            stream = File.OpenRead(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new Refused(
            [
                new LoadProblem(LoadProblemKind.Unreadable, path, $"The file could not be read: {exception.Message}"),
            ]);
        }

        using (stream)
        {
            return Read(stream, updater);
        }
    }

    /// <summary>Reads a scene from a stream, for the relationships the direct updater holds.</summary>
    /// <param name="stream">The bytes of one scene file, in UTF-8.</param>
    public static LoadResult Read(Stream stream) => Read(stream, DirectUpdater.Instance);

    /// <summary>Reads a scene from a stream.</summary>
    /// <param name="stream">The bytes of one scene file, in UTF-8.</param>
    /// <param name="updater">
    /// The updater the app is running, whose <see cref="IGeometryUpdater.SupportedRelationships"/>
    /// decide which relationship kinds this build can hold.
    /// </param>
    public static LoadResult Read(Stream stream, IGeometryUpdater updater)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(updater);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(stream, ParseOptions);
        }
        catch (JsonException exception)
        {
            return new Refused(
            [
                new LoadProblem(
                    LoadProblemKind.Malformed,
                    Position(exception),
                    $"The file is not JSON: {exception.Message}"),
            ]);
        }

        using (document)
        {
            return new SceneBinder().Read(document.RootElement, updater);
        }
    }

    private static string Position(JsonException exception)
        => exception.LineNumber is { } line && exception.BytePositionInLine is { } column
            ? $"line {line + 1}, position {column + 1}"
            : string.Empty;
}

using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project;

/// <summary>What was wrong with a file the reader refused.</summary>
/// <remarks>
/// Every kind is a refusal, never a repair. M1's viewer shows the message to the user, so each one
/// has to name the thing that was wrong.
/// </remarks>
public enum LoadProblemKind
{
    /// <summary>The bytes could not be read at all — no such file, no permission, an I/O error.</summary>
    Unreadable,

    /// <summary>The bytes are not JSON, or a value is of the wrong JSON type.</summary>
    Malformed,

    /// <summary>The file's <c>formatVersion</c> is not the one this build reads.</summary>
    UnsupportedFormatVersion,

    /// <summary>The file states units this build does not store in.</summary>
    UnsupportedUnits,

    /// <summary>A field the format requires is not there.</summary>
    MissingField,

    /// <summary>A field the format does not define. Strict: with the version pinned there is no legitimate reason for one.</summary>
    UnknownField,

    /// <summary>The same field appears twice in one object.</summary>
    DuplicateField,

    /// <summary>A length, angle or count was not a JSON integer — a decimal length is a load failure.</summary>
    NotAnInteger,

    /// <summary>An id was not a GUID in the format's canonical form.</summary>
    NotAnId,

    /// <summary>An entity type, relationship kind, axis, corner, edge or side this build does not know.</summary>
    UnknownValue,

    /// <summary>Two entities, relationships or layers in the file share an id.</summary>
    DuplicateId,

    /// <summary>A value the model does not allow — a non-positive size, an un-normalised rotation.</summary>
    InvalidValue,

    /// <summary>A reference names an id the file does not define.</summary>
    DanglingReference,

    /// <summary>A reference names an entity of the wrong kind — a corner of a node, say.</summary>
    WrongReferenceKind,

    /// <summary>Two relationships say the same thing about the same references (invariant 4).</summary>
    DuplicateRelationship,

    /// <summary>A relationship kind the running app's updater does not support.</summary>
    UnsupportedRelationship,

    /// <summary>The file's geometry does not satisfy the file's own relationships.</summary>
    RelationshipViolated,

    /// <summary>The file's numbers are too large to evaluate its geometry with.</summary>
    OutOfRange,

    /// <summary>The bytes are not a zip container at all, or the zip is truncated or corrupt.</summary>
    NotAContainer,

    /// <summary>The container's <c>containerVersion</c> is not the one this build reads.</summary>
    UnsupportedContainerVersion,

    /// <summary>The container does not hold an entry the format requires.</summary>
    MissingEntry,

    /// <summary>The container holds an entry this format does not define. Strict, for the same reason unknown fields are.</summary>
    UnknownEntry,

    /// <summary>The container holds the same entry name twice.</summary>
    DuplicateEntry,

    /// <summary>An entry's name could escape the container — <c>..</c>, a rooted path, a backslash.</summary>
    UnsafeEntryName,

    /// <summary>The container, or one entry in it, is larger than this build will read.</summary>
    TooLarge,
}

/// <summary>One thing wrong with a file.</summary>
/// <param name="Kind">What kind of problem it is.</param>
/// <param name="Location">Where it is, as a slash-separated path into the document — <c>/entities/3/width</c>.</param>
/// <param name="Message">What was wrong, naming the field, id, kind or value.</param>
public sealed record LoadProblem(LoadProblemKind Kind, string Location, string Message)
{
    /// <summary>The problem on one line, as the viewer shows it.</summary>
    public override string ToString()
        => Location.Length == 0 ? Message : $"{Location}: {Message}";
}

/// <summary>
/// What came of reading a project file: either a sketch, or the reasons it was refused. Never
/// both, and never a partly-built sketch.
/// </summary>
public abstract record LoadResult
{
    private protected LoadResult()
    {
    }

    /// <summary>Whether a sketch came back.</summary>
    public bool IsLoaded => this is Loaded or LoadedProject;
}

/// <summary>
/// The file was read, validated, checked against its own relationships, and accepted.
/// </summary>
/// <param name="Sketch">The sketch the file holds, equal to its contents by value.</param>
/// <param name="Stamp">What the file said about itself.</param>
public sealed record Loaded(Sketch Sketch, FormatStamp Stamp) : LoadResult;

/// <summary>
/// A whole <c>.napkin</c> container was read: its manifest, and the scene inside it.
/// </summary>
/// <remarks>
/// A sibling of <see cref="Loaded"/> rather than a replacement for it, because both ways of
/// opening a design stay: <see cref="SceneReader"/> opens a plain <c>.scene.json</c> — the hand
/// written samples, and anything a person writes with a text editor — and
/// <see cref="ProjectFile"/> opens what the app saves. Both refuse with the same
/// <see cref="Refused"/>, so a caller has one kind of error message to show.
/// </remarks>
/// <param name="Contents">The manifest and the sketch, together.</param>
public sealed record LoadedProject(ProjectContents Contents) : LoadResult
{
    /// <summary>The sketch the container holds.</summary>
    public Sketch Sketch => Contents.Sketch;

    /// <summary>What the container's manifest said about itself.</summary>
    public ProjectManifest Manifest => Contents.Manifest;

    /// <summary>What the scene body said about itself.</summary>
    public FormatStamp Stamp => Contents.SceneStamp;
}

/// <summary>
/// The file was refused. Nothing was opened approximately and nothing was repaired.
/// </summary>
/// <param name="Problems">Everything found wrong, in the order it was found. Never empty.</param>
public sealed record Refused(ImmutableList<LoadProblem> Problems) : LoadResult
{
    /// <summary>
    /// The refusal as a message for the user: the first line says what could not be opened, and
    /// each problem follows on its own line.
    /// </summary>
    public string Summary
        => string.Join(
            Environment.NewLine,
            new[] { Problems.Count == 1 ? "The file could not be opened:" : $"The file could not be opened ({Problems.Count} problems):" }
                .Concat(Problems.Select(problem => $"  {problem}")));

    /// <inheritdoc/>
    public override string ToString() => Summary;
}

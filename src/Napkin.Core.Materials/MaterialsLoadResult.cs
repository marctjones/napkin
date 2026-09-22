using System.Collections.Immutable;

namespace Napkin.Core.Materials;

/// <summary>What was wrong with a data file the reader refused.</summary>
/// <remarks>
/// Every kind is a refusal, never a repair — the same stance <c>Napkin.Core.Project</c> takes on a
/// project file. A reference table with one bad row is not a table with one bad row: it is a table
/// nobody should cut lumber against, so the whole load fails and says which file and which entry.
/// </remarks>
public enum MaterialsProblemKind
{
    /// <summary>The bytes could not be read at all — no such file, no permission, an I/O error.</summary>
    Unreadable,

    /// <summary>The bytes are not JSON, or a value is of the wrong JSON type.</summary>
    Malformed,

    /// <summary>The file's <c>tableVersion</c> is not the one this build reads.</summary>
    UnsupportedTableVersion,

    /// <summary>A field the format requires is not there.</summary>
    MissingField,

    /// <summary>A field the format does not define. Strict, for the same reason the scene reader is.</summary>
    UnknownField,

    /// <summary>The same field appears twice in one object.</summary>
    DuplicateField,

    /// <summary>A category, size class or other named value this build does not know.</summary>
    UnknownValue,

    /// <summary>A required string was empty — including, and especially, a citation field.</summary>
    EmptyValue,

    /// <summary>
    /// A dimension was not text napkin can read as a length, or did not land exactly on the
    /// 1/1024 inch grid. A standard prints tape-measure fractions; one that has to be rounded to
    /// be stored means the row was transcribed wrong.
    /// </summary>
    NotALength,

    /// <summary>A length the model does not allow — a thickness of zero, a negative width.</summary>
    InvalidValue,

    /// <summary>A date was not written as <c>yyyy-mm-dd</c>.</summary>
    NotADate,

    /// <summary>
    /// Two entries have names that normalise to the same key, in one file or across two. Which
    /// "2x4" a design means is not something to guess at.
    /// </summary>
    DuplicateEntry,

    /// <summary>A file names a table id another file has already used.</summary>
    DuplicateTable,

    /// <summary>The file, or the set of files, holds no entries at all.</summary>
    Empty,
}

/// <summary>One thing wrong with a data file.</summary>
/// <param name="Kind">What kind of problem it is.</param>
/// <param name="File">The data file it is in, named so a person can open it.</param>
/// <param name="Location">Where in the document, as a slash-separated path — <c>/entries/3/width</c>.</param>
/// <param name="Message">What was wrong, naming the field, entry or value.</param>
public sealed record MaterialsProblem(MaterialsProblemKind Kind, string File, string Location, string Message)
{
    /// <summary>The problem on one line, naming the file and the entry.</summary>
    public override string ToString()
        => Location.Length == 0 ? $"{File}: {Message}" : $"{File}{Location}: {Message}";
}

/// <summary>
/// What came of reading the reference tables: either a library, or the reasons it was refused.
/// Never both, and never a partly-built library.
/// </summary>
public abstract record MaterialsLoadResult
{
    private protected MaterialsLoadResult()
    {
    }

    /// <summary>Whether a library came back.</summary>
    public bool IsLoaded => this is MaterialsLoaded;
}

/// <summary>The files were read, validated and accepted.</summary>
/// <param name="Library">The library they hold.</param>
public sealed record MaterialsLoaded(MaterialsLibrary Library) : MaterialsLoadResult;

/// <summary>The files were refused. Nothing was repaired and no partial table came back.</summary>
/// <param name="Problems">Everything found wrong, in the order it was found. Never empty.</param>
public sealed record MaterialsRefused(ImmutableList<MaterialsProblem> Problems) : MaterialsLoadResult
{
    /// <summary>The refusal as a message for a person, one problem per line.</summary>
    public string Summary
        => string.Join(
            Environment.NewLine,
            new[]
            {
                Problems.Count == 1
                    ? "The materials library could not be loaded:"
                    : $"The materials library could not be loaded ({Problems.Count} problems):",
            }.Concat(Problems.Select(problem => $"  {problem}")));

    /// <inheritdoc/>
    public override string ToString() => Summary;
}

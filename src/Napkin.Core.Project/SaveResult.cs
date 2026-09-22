using System.Collections.Immutable;

namespace Napkin.Core.Project;

/// <summary>What was wrong when a project could not be saved.</summary>
public enum SaveProblemKind
{
    /// <summary>
    /// The sketch does not satisfy <see cref="Napkin.Core.Geometry.Sketch.Validate"/>, so the file
    /// would be one this build could not open again. Saving it would turn a bug into a lost
    /// drawing.
    /// </summary>
    InvalidSketch,

    /// <summary>The bytes could not be written — no permission, no room, an I/O error.</summary>
    Unwritable,
}

/// <summary>One reason a project was not saved, as a message for the user.</summary>
/// <param name="Kind">What kind of problem it is.</param>
/// <param name="Message">What was wrong, naming it.</param>
public sealed record SaveProblem(SaveProblemKind Kind, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => Message;
}

/// <summary>
/// What came of saving a project: it was written, or it was not and the old file is untouched.
/// Never half of either.
/// </summary>
/// <remarks>
/// Nothing is thrown for a save that cannot be done — a full disk and a read-only folder are
/// things that happen to people, not programming errors. Exceptions are for a caller that passed
/// nonsense arguments.
/// </remarks>
public abstract record SaveResult
{
    private protected SaveResult()
    {
    }

    /// <summary>Whether the project was written.</summary>
    public bool IsSaved => this is Saved;
}

/// <summary>The project was written, in full, and is at <paramref name="Path"/>.</summary>
/// <param name="Path">Where it was written, or the empty string when it was written to a stream.</param>
public sealed record Saved(string Path) : SaveResult;

/// <summary>
/// The project was not written. Whatever was at the path before is still there, byte for byte.
/// </summary>
/// <param name="Problems">Everything found wrong. Never empty.</param>
public sealed record NotSaved(ImmutableList<SaveProblem> Problems) : SaveResult
{
    /// <summary>The refusal as a message for the user, one problem per line.</summary>
    public string Summary
        => string.Join(
            Environment.NewLine,
            new[] { Problems.Count == 1 ? "The project could not be saved:" : $"The project could not be saved ({Problems.Count} problems):" }
                .Concat(Problems.Select(problem => $"  {problem}")));

    /// <inheritdoc/>
    public override string ToString() => Summary;
}

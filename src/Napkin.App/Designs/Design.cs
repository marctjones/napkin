using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.App.Designs;

/// <summary>
/// A drawing the viewer can show: a sketch, a name for the window title, and the part names to
/// draw on it.
/// </summary>
/// <remarks>
/// <para>
/// The labels live here rather than in the canvas because a design may name things the file does
/// not — a new sheet's parts, for one. Scene format version 2 gave every entity a
/// <see cref="Entity.Name"/>, and <see cref="Named"/> is what carries those into this dictionary;
/// nothing in the canvas changed to make that work.
/// </para>
/// <para>
/// A design is a value, like the sketch inside it. The viewer never edits one; M1 is read-only.
/// </para>
/// </remarks>
/// <param name="Name">What this drawing is called, in the window title and the status bar.</param>
/// <param name="Sketch">The geometry.</param>
/// <param name="Labels">
/// Names for the entities that have one; an entity whose name is empty is simply not in it.
/// </param>
public sealed record Design(
    string Name,
    Sketch Sketch,
    ImmutableDictionary<EntityId, string> Labels)
{
    /// <summary>A design with no labels at all — what a file with no names in it produces.</summary>
    public static Design Unlabelled(string name, Sketch sketch) =>
        new(name, sketch, ImmutableDictionary<EntityId, string>.Empty);

    /// <summary>
    /// A design labelled from the sketch's own entity names, which is what a file carries since
    /// scene format version 2. An entity named with an empty string is left unlabelled.
    /// </summary>
    /// <param name="name">What to call the design.</param>
    /// <param name="sketch">The geometry, whose entities carry the names.</param>
    public static Design Named(string name, Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        return new(
            name,
            sketch,
            sketch.Entities.Values
                .Where(entity => entity.Name.Length > 0)
                .ToImmutableDictionary(entity => entity.Id, entity => entity.Name));
    }

    /// <summary>The name to draw on an entity, or null when it has none.</summary>
    public string? LabelFor(EntityId id) => Labels.TryGetValue(id, out string? label) ? label : null;
}

/// <summary>Something the viewer can open.</summary>
/// <remarks>
/// <para>
/// Every source in M1 is a scene file read by <see cref="Napkin.Core.Project.SceneReader"/> — the
/// two shipped samples and anything a person picks through <em>File &#x2192; Open&#x2026;</em> go
/// through the same <see cref="FileDesignSource"/> and the same reader, so there is one way for a
/// drawing to reach the canvas and one place where a bad file is refused.
/// </para>
/// <para>
/// <see cref="Load"/> is allowed to fail: the source throws <see cref="DesignLoadException"/> when
/// the reader refuses the file, and the window shows every problem while leaving whatever is
/// already on screen untouched.
/// </para>
/// </remarks>
public interface IDesignSource
{
    /// <summary>What to call this design in the menu, the window title and the status bar.</summary>
    string Name { get; }

    /// <summary>One line about what it is, for the status bar.</summary>
    string Description { get; }

    /// <summary>Produces the design. May be called more than once.</summary>
    /// <exception cref="DesignLoadException">The design could not be produced.</exception>
    Design Load();
}

/// <summary>A design could not be opened, with every reason it was refused.</summary>
/// <remarks>
/// The reader reports a list of problems, not one line (see
/// <see cref="Napkin.Core.Project.Refused"/>), and the window shows the whole list. Carrying
/// <see cref="Problems"/> beside <see cref="Exception.Message"/> is what lets it: the message is
/// the summary a log wants, and the list is what a person reads.
/// </remarks>
public sealed class DesignLoadException : Exception
{
    /// <inheritdoc cref="DesignLoadException"/>
    public DesignLoadException(string message)
        : this(message, [message])
    {
    }

    /// <inheritdoc cref="DesignLoadException"/>
    /// <param name="message">The whole refusal, on as many lines as it takes.</param>
    /// <param name="problems">
    /// One readable line per thing that was wrong. Never empty: a refusal with no reason in it
    /// would be exactly the silent failure this type exists to prevent.
    /// </param>
    public DesignLoadException(string message, IEnumerable<string> problems)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(problems);
        Problems = [.. problems];
        if (Problems.IsEmpty)
        {
            Problems = [message];
        }
    }

    /// <inheritdoc cref="DesignLoadException"/>
    public DesignLoadException(string message, Exception innerException)
        : base(message, innerException) => Problems = [message];

    /// <summary>Everything that was wrong, one readable line each.</summary>
    public ImmutableArray<string> Problems { get; }
}

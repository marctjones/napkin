using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.App.Designs;

/// <summary>
/// A drawing the viewer can show: a sketch, a name for the window title, and the part names to
/// draw on it.
/// </summary>
/// <remarks>
/// <para>
/// The labels live here rather than in <see cref="Sketch"/> because the geometry kernel has no
/// notion of a part name — an entity has an id and a layer, and nothing else a person would read.
/// When the project format grows a name per entity (#6, #8's cut list needs one too), this
/// dictionary is what it fills in, and nothing in the canvas changes.
/// </para>
/// <para>
/// A design is a value, like the sketch inside it. The viewer never edits one; M1 is read-only.
/// </para>
/// </remarks>
/// <param name="Name">What this drawing is called, in the window title and the status bar.</param>
/// <param name="Sketch">The geometry.</param>
/// <param name="Labels">Names for the entities that have one; entities may be missing from it.</param>
public sealed record Design(
    string Name,
    Sketch Sketch,
    ImmutableDictionary<EntityId, string> Labels)
{
    /// <summary>A design with no labels at all — what a file with no names in it produces.</summary>
    public static Design Unlabelled(string name, Sketch sketch) =>
        new(name, sketch, ImmutableDictionary<EntityId, string>.Empty);

    /// <summary>The name to draw on an entity, or null when it has none.</summary>
    public string? LabelFor(EntityId id) => Labels.TryGetValue(id, out string? label) ? label : null;
}

/// <summary>Something the viewer can open: a built-in sample today, a file tomorrow.</summary>
/// <remarks>
/// <para>
/// The seam exists so that wiring <em>File &#x2192; Open&#x2026;</em> to the project reader (#6) is
/// one new implementation of this interface and one menu item, with nothing in the canvas or the
/// window touched. M1's samples are built in code because the reader and the sample files are being
/// written in parallel (#37); they are hand-computed either way, and the arithmetic in
/// <see cref="Samples.BuiltInDesigns"/> is the same arithmetic the fixtures state.
/// </para>
/// <para>
/// <see cref="Load"/> is allowed to fail: a file source will throw
/// <see cref="DesignLoadException"/> when the reader refuses the file, and the window shows the
/// message while leaving whatever is already on screen untouched.
/// </para>
/// </remarks>
public interface IDesignSource
{
    /// <summary>What to call this design in the menu.</summary>
    string Name { get; }

    /// <summary>One line about what it is, for the status bar.</summary>
    string Description { get; }

    /// <summary>Produces the design. May be called more than once.</summary>
    /// <exception cref="DesignLoadException">The design could not be produced.</exception>
    Design Load();
}

/// <summary>A design could not be opened, with a message naming what was wrong.</summary>
public sealed class DesignLoadException : Exception
{
    /// <inheritdoc cref="DesignLoadException"/>
    public DesignLoadException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="DesignLoadException"/>
    public DesignLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

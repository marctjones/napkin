using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// Edit → Phase (docs/design/renovation-sketches.md §6.1, §8): marks the selection existing, new
/// or demolish in one undo step. A wall's openings keep their own phase — a new window in an
/// existing wall is the point.
/// </summary>
public static class PhaseCommand
{
    /// <summary>The word a phase is said with: "existing", "new", "demolish".</summary>
    public static string Word(Phase phase) => phase switch
    {
        Phase.Existing => "existing",
        Phase.New => "new",
        Phase.Demolish => "demolish",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Not a phase."),
    };

    /// <summary>
    /// The one request that marks every selected entity not already in <paramref name="phase"/>,
    /// or null when there is nothing to change.
    /// </summary>
    public static Request? Plan(Sketch sketch, IEnumerable<EntityId> selection, Phase phase)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(selection);
        ImmutableList<Request> requests =
        [
            .. selection
                .Distinct()
                .Order()
                .Where(id => sketch.Find(id) is { } entity && entity.Phase != phase)
                .Select(id => (Request)new SetPhase(id, phase)),
        ];

        return requests.Count switch
        {
            0 => null,
            1 => requests[0],
            _ => new Batch(requests),
        };
    }

    /// <summary>"Marked Wall 1 existing", or "Marked 3 things demolish" for more than one.</summary>
    public static string Message(IReadOnlyList<string> names, Phase phase)
    {
        ArgumentNullException.ThrowIfNull(names);
        return names.Count == 1 ? $"Marked {names[0]} {Word(phase)}" : $"Marked {names.Count} things {Word(phase)}";
    }

    /// <summary>
    /// The phase every selected entity shares, for the menu's radio mark and the panel's picker;
    /// null when the selection is empty or mixed.
    /// </summary>
    public static Phase? Shared(Sketch sketch, IEnumerable<EntityId> selection)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(selection);
        List<Phase> phases = [.. selection.Select(sketch.Find).OfType<Entity>().Select(entity => entity.Phase).Distinct()];
        return phases.Count == 1 ? phases[0] : null;
    }

    /// <summary>The status bar's words for one selected thing: "Wall 1, existing"; empty for a new one, which reads as today.</summary>
    public static string StatusWords(string name, Phase phase) => phase == Phase.New ? string.Empty : $"{name}, {Word(phase)}";
}

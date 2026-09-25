using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>Whether a project's adopted code is locked or follows the pack's newer revisions.</summary>
public enum CodeLockMode
{
    /// <summary>
    /// Locked to this pack and revision — typically the code in force on the permit application
    /// date (design §0, §7.2). A newer revision is offered as a diff, never applied silently.
    /// </summary>
    Locked,

    /// <summary>Follows the pack: a newer revision of the same pack is taken, with a recompute and a diff shown.</summary>
    Following,
}

/// <summary>
/// What a project stores about its adopted code (design §7.1): the pack identity and revision,
/// locked or following, and the date it was locked. The picker and recompute-on-change are #19;
/// this is the value they build on. A project with no code chosen stores no selection.
/// </summary>
public sealed record CodeSelection
{
    /// <summary>Builds a selection.</summary>
    /// <exception cref="ArgumentException">
    /// A malformed pack id, a revision below 1, a locked selection without its lock date, or a
    /// following selection with one.
    /// </exception>
    public CodeSelection(string packId, int revision, CodeLockMode mode, DateOnly? lockedOn)
    {
        ArgumentNullException.ThrowIfNull(packId);
        if (!PackLoader.PackIdPattern().IsMatch(packId))
        {
            throw new ArgumentException($"'{packId}' is not a pack id.", nameof(packId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(revision, 1);
        if ((mode == CodeLockMode.Locked) != lockedOn.HasValue)
        {
            throw new ArgumentException("A locked selection records the date it was locked; a following selection has none.", nameof(lockedOn));
        }

        PackId = packId;
        Revision = revision;
        Mode = mode;
        LockedOn = lockedOn;
    }

    /// <summary>The pack id stored in the project (design §1.1).</summary>
    public string PackId { get; }

    /// <summary>The pack revision the project's results were computed against.</summary>
    public int Revision { get; }

    /// <summary>Locked or following.</summary>
    public CodeLockMode Mode { get; }

    /// <summary>When the code was locked; null when following.</summary>
    public DateOnly? LockedOn { get; }

    /// <summary>A selection of this loaded pack at its current revision.</summary>
    public static CodeSelection Of(LoadedPack pack, CodeLockMode mode, DateOnly? lockedOn)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return new CodeSelection(pack.Manifest.Id, pack.Manifest.Revision, mode, lockedOn);
    }

    /// <summary>Whether this selection names the pack at exactly this revision; if not, results must be recomputed (design §7.3).</summary>
    public bool IsCurrentFor(LoadedPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return pack.Manifest.Id == PackId && pack.Manifest.Revision == Revision;
    }
}

/// <summary>How one element's result changed between two computations (design §7.3).</summary>
public enum ChangeKind
{
    /// <summary>Still sized, with a different member or stud count.</summary>
    SizedToSized,

    /// <summary>Was sized; is now out of prescriptive scope. Shown first.</summary>
    SizedToOutOfScope,

    /// <summary>Was out of scope; is now sized.</summary>
    OutOfScopeToSized,

    /// <summary>Still out of scope, for a different reason or limit.</summary>
    OutOfScopeChanged,

    /// <summary>Was sized; now there is no answer (input missing or no data).</summary>
    SizedToNoAnswer,

    /// <summary>Was out of scope; now there is no answer (input missing or no data).</summary>
    OutOfScopeToNoAnswer,

    /// <summary>Had no answer; is now sized.</summary>
    NoAnswerToSized,

    /// <summary>Had no answer; is now out of scope.</summary>
    NoAnswerToOutOfScope,

    /// <summary>Still no answer, for a different reason.</summary>
    NoAnswerChanged,

    /// <summary>Same member and studs; only the citation differs (a revision, a renumbered row, another pack).</summary>
    CitationOnly,
}

/// <summary>One element whose result differs.</summary>
public sealed record ResultChange(EntityId Element, HeaderResult Before, HeaderResult After, ChangeKind Kind);

/// <summary>What a total recompute changed: every element is either in <see cref="Changes"/> or counted in <see cref="Unchanged"/>.</summary>
public sealed record RecomputeReport(ValueList<ResultChange> Changes, int Unchanged);

/// <summary>
/// Recompute is total (design §7.3): every element, against the given pack, no incremental
/// shortcut, no stored result reused. Pure functions; wiring them to project events is #19.
/// </summary>
public static class Recompute
{
    /// <summary>Evaluates every element's header request against the pack (or no pack), in order.</summary>
    public static ValueList<KeyValuePair<EntityId, HeaderResult>> Headers(
        LoadedPack? pack, IEnumerable<KeyValuePair<EntityId, HeaderRequest>> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return elements
            .Select(e => new KeyValuePair<EntityId, HeaderResult>(e.Key, RulesEngine.SizeHeader(pack, e.Value)))
            .ToValueList();
    }

    /// <summary>Compares two computations over the same elements.</summary>
    /// <exception cref="ArgumentException">The two sets of elements differ: a recompute that dropped or invented an element.</exception>
    public static RecomputeReport Diff(
        IReadOnlyList<KeyValuePair<EntityId, HeaderResult>> before, IReadOnlyList<KeyValuePair<EntityId, HeaderResult>> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        Dictionary<EntityId, HeaderResult> old = before.ToDictionary(p => p.Key, p => p.Value);
        if (old.Count != after.Count || after.Any(p => !old.ContainsKey(p.Key)))
        {
            throw new ArgumentException("Before and after must cover exactly the same elements; a recompute is total.", nameof(after));
        }

        List<ResultChange> changes = [];
        int unchanged = 0;
        foreach ((EntityId element, HeaderResult now) in after)
        {
            HeaderResult was = old[element];
            if (was.Equals(now))
            {
                unchanged++;
                continue;
            }

            changes.Add(new ResultChange(element, was, now, Classify(was, now)));
        }

        return new RecomputeReport(changes.ToValueList(), unchanged);
    }

    private static ChangeKind Classify(HeaderResult was, HeaderResult now) => (was, now) switch
    {
        (HeaderResult.Sized a, HeaderResult.Sized b) =>
            a.Header == b.Header && a.JackStuds == b.JackStuds && a.KingStuds == b.KingStuds ? ChangeKind.CitationOnly : ChangeKind.SizedToSized,
        (HeaderResult.Sized, HeaderResult.OutOfScope) => ChangeKind.SizedToOutOfScope,
        (HeaderResult.Sized, _) => ChangeKind.SizedToNoAnswer,
        (HeaderResult.OutOfScope, HeaderResult.Sized) => ChangeKind.OutOfScopeToSized,
        (HeaderResult.OutOfScope, HeaderResult.OutOfScope) => ChangeKind.OutOfScopeChanged,
        (HeaderResult.OutOfScope, _) => ChangeKind.OutOfScopeToNoAnswer,
        (_, HeaderResult.Sized) => ChangeKind.NoAnswerToSized,
        (_, HeaderResult.OutOfScope) => ChangeKind.NoAnswerToOutOfScope,
        _ => ChangeKind.NoAnswerChanged,
    };
}

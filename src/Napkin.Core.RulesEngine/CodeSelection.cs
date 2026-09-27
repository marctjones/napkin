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
public static partial class Recompute
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

/// <summary>How one wall line's bracing result changed between two computations (design §7.3).</summary>
public enum BracingChangeKind
{
    /// <summary>Passed; now falls short. Newly flagged, shown first.</summary>
    PassToFail,

    /// <summary>Was out of scope or had no answer; now falls short. Newly flagged.</summary>
    ToFail,

    /// <summary>Passed or fell short; now out of scope. Flagged: the method no longer covers the line.</summary>
    ToOutOfScope,

    /// <summary>Had a result (pass, short, or out of scope); now there is no answer: it can no longer be computed.</summary>
    ToNoAnswer,

    /// <summary>Still short, by a different amount or under a different section.</summary>
    FailChanged,

    /// <summary>Still passes, with different lengths.</summary>
    PassChanged,

    /// <summary>Still out of scope, for a different reason or limit.</summary>
    OutOfScopeChanged,

    /// <summary>Fell short; now passes.</summary>
    FailToPass,

    /// <summary>Was out of scope or had no answer; now passes.</summary>
    ToPass,

    /// <summary>Had no answer; now out of scope.</summary>
    NoAnswerToOutOfScope,

    /// <summary>Still no answer, for a different reason.</summary>
    NoAnswerChanged,

    /// <summary>Same verdict and lengths; only the citation or working differs (another pack, a revision).</summary>
    CitationOnly,
}

/// <summary>One wall line whose bracing result differs.</summary>
public sealed record BracingChange(EntityId Element, BracingResult Before, BracingResult After, BracingChangeKind Kind);

/// <summary>What a total bracing recompute changed: every line is either in <see cref="Changes"/> or counted in <see cref="Unchanged"/>.</summary>
public sealed record BracingRecomputeReport(ValueList<BracingChange> Changes, int Unchanged)
{
    /// <summary>The lines that became flagged: newly short, or newly out of scope.</summary>
    public IEnumerable<BracingChange> NewlyFlagged
        => Changes.Where(c => c.Kind is BracingChangeKind.PassToFail or BracingChangeKind.ToFail
                              || (c.Kind == BracingChangeKind.ToOutOfScope && c.Before is not BracingResult.Fails));

    /// <summary>The lines that had a result and can no longer be computed.</summary>
    public IEnumerable<BracingChange> NoLongerComputable => Changes.Where(c => c.Kind == BracingChangeKind.ToNoAnswer);
}

/// <summary>Total recompute and diff for wall bracing, the counterpart of the header functions above.</summary>
public static partial class Recompute
{
    /// <summary>Checks every wall line against the pack (or no pack), in order.</summary>
    public static ValueList<KeyValuePair<EntityId, BracingResult>> Bracing(
        LoadedPack? pack, IEnumerable<KeyValuePair<EntityId, BracingRequest>> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines
            .Select(e => new KeyValuePair<EntityId, BracingResult>(e.Key, RulesEngine.CheckBracing(pack, e.Value)))
            .ToValueList();
    }

    /// <summary>Compares two computations over the same wall lines.</summary>
    /// <exception cref="ArgumentException">The two sets of lines differ: a recompute that dropped or invented one.</exception>
    public static BracingRecomputeReport DiffBracing(
        IReadOnlyList<KeyValuePair<EntityId, BracingResult>> before, IReadOnlyList<KeyValuePair<EntityId, BracingResult>> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        Dictionary<EntityId, BracingResult> old = before.ToDictionary(p => p.Key, p => p.Value);
        if (old.Count != after.Count || after.Any(p => !old.ContainsKey(p.Key)))
        {
            throw new ArgumentException("Before and after must cover exactly the same wall lines; a recompute is total.", nameof(after));
        }

        List<BracingChange> changes = [];
        int unchanged = 0;
        foreach ((EntityId element, BracingResult now) in after)
        {
            BracingResult was = old[element];
            if (was.Equals(now))
            {
                unchanged++;
                continue;
            }

            changes.Add(new BracingChange(element, was, now, Classify(was, now)));
        }

        return new BracingRecomputeReport(changes.OrderBy(c => c.Kind).ToValueList(), unchanged);
    }

    private static BracingChangeKind Classify(BracingResult was, BracingResult now) => (was, now) switch
    {
        (BracingResult.Passes a, BracingResult.Passes b) => a.Required == b.Required && a.Provided == b.Provided ? BracingChangeKind.CitationOnly : BracingChangeKind.PassChanged,
        (BracingResult.Fails a, BracingResult.Fails b) => a.Required == b.Required && a.Provided == b.Provided ? BracingChangeKind.CitationOnly : BracingChangeKind.FailChanged,
        (BracingResult.Passes, BracingResult.Fails) => BracingChangeKind.PassToFail,
        (_, BracingResult.Fails) => BracingChangeKind.ToFail,
        (BracingResult.Fails, BracingResult.Passes) => BracingChangeKind.FailToPass,
        (_, BracingResult.Passes) => BracingChangeKind.ToPass,
        (BracingResult.OutOfScope, BracingResult.OutOfScope) => BracingChangeKind.OutOfScopeChanged,
        (BracingResult.Passes or BracingResult.Fails, BracingResult.OutOfScope) => BracingChangeKind.ToOutOfScope,
        (_, BracingResult.OutOfScope) => BracingChangeKind.NoAnswerToOutOfScope,
        (BracingResult.Passes or BracingResult.Fails or BracingResult.OutOfScope, _) => BracingChangeKind.ToNoAnswer,
        _ => BracingChangeKind.NoAnswerChanged,
    };
}

/// <summary>Which deck lookup a result answers: the deck (or roof) and the check, "Joists", "Beam", "Ledger", "Footing".</summary>
public readonly record struct DeckCheckKey(EntityId Element, string Check);

/// <summary>How one deck lookup's result changed between two computations (design §7.3, deck-guide-pack §6 slice A).</summary>
public enum DeckChangeKind
{
    /// <summary>Passed; now short. Newly flagged, shown first.</summary>
    PassToShort,

    /// <summary>Was out of scope or had no answer; now short. Newly flagged.</summary>
    ToShort,

    /// <summary>Passed, fell short or was sized; now out of scope. Flagged.</summary>
    ToOutOfScope,

    /// <summary>Had a result (passes, short, sized or out of scope); now input missing or no data: it can no longer be computed.</summary>
    ToNoAnswer,

    /// <summary>Still passes, short or sized, with another allowed span, fastener, spacing, count or footing.</summary>
    RowChanged,

    /// <summary>Still out of scope, for another limit or reason.</summary>
    OutOfScopeChanged,

    /// <summary>Fell short; now passes.</summary>
    ShortToPass,

    /// <summary>Was out of scope or had no answer; now passes or is sized.</summary>
    ToAnswer,

    /// <summary>Had no answer; now out of scope.</summary>
    NoAnswerToOutOfScope,

    /// <summary>Still no answer, for another reason.</summary>
    NoAnswerChanged,

    /// <summary>The same row and the same answer; only the span asked about moved within it. Not a change of the answer.</summary>
    SpanMoved,

    /// <summary>The same answer from another code, table or row (a revision, a renumbered row, another pack).</summary>
    CitationOnly,
}

/// <summary>One deck lookup whose result differs.</summary>
public sealed record DeckChange(DeckCheckKey Key, DeckResult Before, DeckResult After, DeckChangeKind Kind);

/// <summary>What a total deck recompute changed: every lookup is either in <see cref="Changes"/> or counted in <see cref="Unchanged"/>.</summary>
public sealed record DeckRecomputeReport(ValueList<DeckChange> Changes, int Unchanged)
{
    /// <summary>The lookups that became flagged: newly short, or newly out of scope.</summary>
    public IEnumerable<DeckChange> NewlyFlagged
        => Changes.Where(c => c.Kind is DeckChangeKind.PassToShort or DeckChangeKind.ToShort
                              || (c.Kind == DeckChangeKind.ToOutOfScope && c.Before is not DeckResult.Short));

    /// <summary>The lookups that had a result and can no longer be computed.</summary>
    public IEnumerable<DeckChange> NoLongerComputable => Changes.Where(c => c.Kind == DeckChangeKind.ToNoAnswer);
}

/// <summary>The deck lookups' diff, the counterpart of the header and bracing functions above.</summary>
public static partial class Recompute
{
    /// <summary>Compares two computations over the same deck lookups, most serious first.</summary>
    /// <exception cref="ArgumentException">The two sets of lookups differ: a recompute that dropped or invented one.</exception>
    public static DeckRecomputeReport DiffDeck(
        IReadOnlyList<KeyValuePair<DeckCheckKey, DeckResult>> before, IReadOnlyList<KeyValuePair<DeckCheckKey, DeckResult>> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        Dictionary<DeckCheckKey, DeckResult> old = before.ToDictionary(p => p.Key, p => p.Value);
        if (old.Count != after.Count || after.Any(p => !old.ContainsKey(p.Key)))
        {
            throw new ArgumentException("Before and after must cover exactly the same deck lookups; a recompute is total.", nameof(after));
        }

        List<DeckChange> changes = [];
        int unchanged = 0;
        foreach ((DeckCheckKey key, DeckResult now) in after)
        {
            DeckResult was = old[key];
            if (Same(was, now))
            {
                unchanged++;
                continue;
            }

            changes.Add(new DeckChange(key, was, now, Classify(was, now)));
        }

        return new DeckRecomputeReport(changes.OrderBy(c => c.Kind).ToValueList(), unchanged);
    }

    /// <summary>
    /// The same result: the same answer from the same code, table and row, about the same span. Compared by
    /// what a person reads, never by reference (two loads of one pack are different objects).
    /// </summary>
    private static bool Same(DeckResult was, DeckResult now) => (was, now) switch
    {
        (DeckResult.Passes or DeckResult.Short or DeckResult.Sized, _) when was.GetType() == now.GetType()
            => Cited(was) == Cited(now) && Values(was) == Values(now),
        (DeckResult.OutOfScope a, DeckResult.OutOfScope b) => a.Code == b.Code && a.Explanation == b.Explanation,
        _ => was.Equals(now),
    };

    /// <summary>Where an answer came from: code, table and row.</summary>
    private static (AdoptedCodeRef Code, string Table, string Row) Cited(DeckResult result) => result switch
    {
        DeckResult.Passes p => (p.Code, p.Table.Designation, p.Row.Id),
        DeckResult.Short s => (s.Code, s.Table.Designation, s.Row.Id),
        _ => (((DeckResult.Sized)result).Code, ((DeckResult.Sized)result).Table.Designation, ((DeckResult.Sized)result).Row.Id),
    };

    /// <summary>What an answer says: the allowed and actual span or post height, or the fastener, spacing and count, or the footing's sizes.</summary>
    private static (Length Allowed, Length Actual, string Text, Length Spacing, int? Count, FootingSize? Footing) Values(DeckResult result) => result switch
    {
        DeckResult.Passes p => (p.Allowed, p.Actual, string.Empty, Length.Zero, null, null),
        DeckResult.Short s => (s.Allowed, s.Actual, string.Empty, Length.Zero, null, null),
        _ => (Length.Zero, Length.Zero, ((DeckResult.Sized)result).Row.Text, ((DeckResult.Sized)result).Row.Spacing, ((DeckResult.Sized)result).Count, ((DeckResult.Sized)result).Row.Footing),
    };

    private static DeckChangeKind Classify(DeckResult was, DeckResult now) => (was, now) switch
    {
        (DeckResult.Passes or DeckResult.Short or DeckResult.Sized, _) when was.GetType() == now.GetType() =>
            Values(was) with { Actual = Length.Zero } != Values(now) with { Actual = Length.Zero } ? DeckChangeKind.RowChanged
            : Cited(was) != Cited(now) ? DeckChangeKind.CitationOnly
            : DeckChangeKind.SpanMoved,
        (DeckResult.Passes, DeckResult.Short) => DeckChangeKind.PassToShort,
        (_, DeckResult.Short) => DeckChangeKind.ToShort,
        (DeckResult.Short, DeckResult.Passes) => DeckChangeKind.ShortToPass,
        (_, DeckResult.Passes or DeckResult.Sized) => DeckChangeKind.ToAnswer,
        (DeckResult.OutOfScope, DeckResult.OutOfScope) => DeckChangeKind.OutOfScopeChanged,
        (DeckResult.Passes or DeckResult.Short or DeckResult.Sized, DeckResult.OutOfScope) => DeckChangeKind.ToOutOfScope,
        (_, DeckResult.OutOfScope) => DeckChangeKind.NoAnswerToOutOfScope,
        (DeckResult.Passes or DeckResult.Short or DeckResult.Sized or DeckResult.OutOfScope, _) => DeckChangeKind.ToNoAnswer,
        _ => DeckChangeKind.NoAnswerChanged,
    };
}

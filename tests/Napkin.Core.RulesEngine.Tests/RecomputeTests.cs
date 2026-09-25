using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>Code selection and total recompute (design §7), over SYNTHETIC TEST DATA - NOT CODE VALUES.</summary>
public class RecomputeTests
{
    [Fact]
    public void A_code_selection_records_pack_revision_mode_and_lock_date()
    {
        LoadedPack pack = Fx.Load("us-zz-state");
        CodeSelection locked = CodeSelection.Of(pack, CodeLockMode.Locked, new DateOnly(2026, 9, 25));
        Assert.Equal("us-zz-state", locked.PackId);
        Assert.Equal(1, locked.Revision);
        Assert.Equal(CodeLockMode.Locked, locked.Mode);
        Assert.Equal(new DateOnly(2026, 9, 25), locked.LockedOn);
        Assert.True(locked.IsCurrentFor(pack));
        Assert.False(locked.IsCurrentFor(Fx.Load("us-zz-base")));
        Assert.False(new CodeSelection("us-zz-state", 2, CodeLockMode.Following, null).IsCurrentFor(pack));
        Assert.Equal(locked, new CodeSelection("us-zz-state", 1, CodeLockMode.Locked, new DateOnly(2026, 9, 25)));

        Assert.Throws<ArgumentException>(() => new CodeSelection("us-zz-state", 1, CodeLockMode.Locked, null));
        Assert.Throws<ArgumentException>(() => new CodeSelection("us-zz-state", 1, CodeLockMode.Following, new DateOnly(2026, 9, 25)));
        Assert.Throws<ArgumentException>(() => new CodeSelection("Not A Pack", 1, CodeLockMode.Following, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CodeSelection("us-zz-state", 0, CodeLockMode.Following, null));
        Assert.Throws<ArgumentNullException>(() => CodeSelection.Of(null!, CodeLockMode.Following, null));
        Assert.Throws<ArgumentNullException>(() => locked.IsCurrentFor(null!));
    }

    [Fact]
    public void Recompute_evaluates_every_element_and_the_diff_names_every_change()
    {
        EntityId amended = EntityId.New();    // base (1) 2x7 → state (1) 2x8
        EntityId added = EntityId.New();      // base out of scope → state sized by the added row
        EntityId deleted = EntityId.New();    // base sized by floor m2 → state out of scope
        EntityId same = EntityId.New();       // untouched row: identical result
        EntityId below = EntityId.New();      // base sized → state below the amended width domain
        EntityId missing = EntityId.New();    // no snow entered under both: unchanged
        List<KeyValuePair<EntityId, HeaderRequest>> elements =
        [
            new(amended, Fx.Roof(Fx.Ft(7), snow: 20, width: Fx.Ft(15))),
            new(added, Fx.Roof(Fx.Ft(19, 7), snow: 80)),
            new(deleted, Fx.Roof(Fx.Ft(9), snow: 80, supports: "test-roof-floor")),
            new(same, Fx.Roof(Fx.Ft(9))),
            new(below, Fx.Roof(Fx.Ft(4), width: Fx.Ft(10))),
            new(missing, Fx.Roof(Fx.Ft(4), snow: null)),
        ];

        ValueList<KeyValuePair<EntityId, HeaderResult>> before = Recompute.Headers(Fx.Load("us-zz-base"), elements);
        ValueList<KeyValuePair<EntityId, HeaderResult>> after = Recompute.Headers(Fx.Load("us-zz-state"), elements);
        Assert.Equal(elements.Select(e => e.Key), after.Select(r => r.Key));

        RecomputeReport report = Recompute.Diff(before, after);
        Assert.Equal(
            [
                (amended, ChangeKind.SizedToSized),
                (added, ChangeKind.OutOfScopeToSized),
                (deleted, ChangeKind.SizedToOutOfScope),
                (same, ChangeKind.CitationOnly), // same member and studs, now cited to the other pack
                (below, ChangeKind.SizedToOutOfScope),
                (missing, ChangeKind.NoAnswerChanged), // still missing snow, but under the other pack
            ],
            report.Changes.Select(c => (c.Element, c.Kind)));
        Assert.Equal(0, report.Unchanged);

        // Recomputing against the same pack changes nothing and counts every element.
        RecomputeReport again = Recompute.Diff(before, Recompute.Headers(Fx.Load("us-zz-base"), elements));
        Assert.Empty(again.Changes);
        Assert.Equal(elements.Count, again.Unchanged);
    }

    [Fact]
    public void No_pack_to_a_pack_and_back_is_reported_not_hidden()
    {
        EntityId e = EntityId.New();
        List<KeyValuePair<EntityId, HeaderRequest>> elements = [new(e, Fx.Roof(Fx.Ft(9)))];
        var none = Recompute.Headers(null, elements);
        var some = Recompute.Headers(Fx.Load("us-zz-base"), elements);
        Assert.IsType<HeaderResult.NoData>(none[0].Value);
        Assert.Equal(ChangeKind.NoAnswerToSized, Assert.Single(Recompute.Diff(none, some).Changes).Kind);
        Assert.Equal(ChangeKind.SizedToNoAnswer, Assert.Single(Recompute.Diff(some, none).Changes).Kind);
    }

    [Theory]
    [InlineData("us-zz-base", "us-zz-state", 9, 80, "test-roof-floor", ChangeKind.SizedToOutOfScope)]
    [InlineData("us-zz-state", "us-zz-base", 9, 80, "test-roof-floor", ChangeKind.OutOfScopeToSized)]
    public void Change_kinds_are_symmetric(string from, string to, long spanFeet, int snow, string supports, ChangeKind kind)
    {
        EntityId e = EntityId.New();
        List<KeyValuePair<EntityId, HeaderRequest>> elements = [new(e, Fx.Roof(Fx.Ft(spanFeet), snow: snow, supports: supports))];
        Assert.Equal(kind, Assert.Single(Recompute.Diff(Recompute.Headers(Fx.Load(from), elements), Recompute.Headers(Fx.Load(to), elements)).Changes).Kind);
    }

    [Fact]
    public void Every_change_kind_is_classified()
    {
        EntityId e = EntityId.New();
        HeaderResult sized = Fx.Size("us-zz-base", Fx.Roof(Fx.Ft(9)));
        HeaderResult sizedOther = Fx.Size("us-zz-base", Fx.Roof(Fx.Ft(4)));
        HeaderResult sameMemberOtherPack = Fx.Size("us-zz-state", Fx.Roof(Fx.Ft(9)));
        HeaderResult span = Fx.Size("us-zz-base", Fx.Roof(Fx.Ft(40)));
        HeaderResult snow = Fx.Size("us-zz-base", Fx.Roof(Fx.Ft(4), snow: 120));
        HeaderResult missing = Fx.Size("us-zz-base", Fx.Roof(Fx.Ft(4), snow: null));
        HeaderResult noData = RulesEngine.SizeHeader(null, Fx.Roof(Fx.Ft(4)));

        ChangeKind Kind(HeaderResult a, HeaderResult b) => Assert.Single(Recompute.Diff([new(e, a)], [new(e, b)]).Changes).Kind;
        Assert.Equal(ChangeKind.SizedToSized, Kind(sized, sizedOther));
        Assert.Equal(ChangeKind.CitationOnly, Kind(sized, sameMemberOtherPack));
        Assert.Equal(ChangeKind.SizedToOutOfScope, Kind(sized, span));
        Assert.Equal(ChangeKind.OutOfScopeToSized, Kind(span, sized));
        Assert.Equal(ChangeKind.OutOfScopeChanged, Kind(span, snow));
        Assert.Equal(ChangeKind.OutOfScopeToNoAnswer, Kind(span, missing));
        Assert.Equal(ChangeKind.NoAnswerToOutOfScope, Kind(noData, span));
        Assert.Equal(ChangeKind.NoAnswerChanged, Kind(noData, missing));
        Assert.Empty(Recompute.Diff([new(e, sized)], [new(e, Fx.Size("us-zz-base", Fx.Roof(Fx.Ft(9))))]).Changes);
    }

    [Fact]
    public void A_diff_that_drops_or_invents_an_element_is_refused()
    {
        EntityId a = EntityId.New();
        EntityId b = EntityId.New();
        HeaderResult r = Fx.Size("us-zz-base", Fx.Roof(Fx.Ft(9)));
        Assert.Throws<ArgumentException>(() => Recompute.Diff([new(a, r)], [new(b, r)]));
        Assert.Throws<ArgumentException>(() => Recompute.Diff([new(a, r)], [new(a, r), new(b, r)]));
        Assert.Throws<ArgumentNullException>(() => Recompute.Headers(null, null!));
        Assert.Throws<ArgumentNullException>(() => Recompute.Diff(null!, []));
        Assert.Throws<ArgumentNullException>(() => Recompute.Diff([], null!));
    }
}

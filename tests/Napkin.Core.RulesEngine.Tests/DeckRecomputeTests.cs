using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// The deck lookups' recompute diff (#238, deck-guide-pack §6 slice A; design §7.3) over real results from
/// the SYNTHETIC pack us-zz-deck (NOT CODE VALUES): every kind of change, the most serious first, and a
/// result compared by what it says, never by reference.
/// </summary>
public class DeckRecomputeTests
{
    static string Root => Path.Combine(AppContext.BaseDirectory, "DeckPacks");

    static readonly LoadedPack Pack = Fx.Loaded(PackLoader.Load(Root, "us-zz-deck"));

    static readonly DeckCheckKey Key = new(EntityId.New(), "Joists");

    static Length In(long whole) => Length.Inches(whole);

    static DeckResult Joists(long span, string? species = "zz-fir", long spacing = 16, string? supports = "zz-deck", LoadedPack? pack = null)
        => DeckEvaluator.CheckSpan(pack ?? Pack, SpanUse.DeckJoist, new SpanRequest("2x8", In(span), supports, species, In(spacing), null, 30, null, In(120), In(144)));

    static DeckResult Ledger(long joistSpan) => DeckEvaluator.SizeLedger(Pack, "2x8", In(joistSpan), In(144));

    static DeckRecomputeReport Diff(DeckResult before, DeckResult after)
        => Recompute.DiffDeck([KeyValuePair.Create(Key, before)], [KeyValuePair.Create(Key, after)]);

    static DeckChangeKind Kind(DeckResult before, DeckResult after) => Assert.Single(Diff(before, after).Changes).Kind;

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void The_same_answer_recomputed_is_unchanged_even_from_another_load_of_the_pack()
    {
        LoadedPack again = Fx.Loaded(PackLoader.Load(Root, "us-zz-deck"));
        Assert.Equal(1, Diff(Joists(117), Joists(117, pack: again)).Unchanged);
        Assert.Equal(1, Diff(Ledger(117), Ledger(117)).Unchanged);
        Assert.Equal(1, Diff(Joists(117, supports: "zz-porch"), Joists(117, supports: "zz-porch", pack: again)).Unchanged);
        Assert.Equal(1, Diff(Joists(117, species: null), Joists(117, species: null)).Unchanged);
        Assert.Equal(1, Diff(DeckEvaluator.CheckSpan(null, SpanUse.DeckJoist, new SpanRequest("2x8", In(1), null, null, null, null)), DeckEvaluator.CheckSpan(null, SpanUse.DeckJoist, new SpanRequest("2x8", In(1), null, null, null, null))).Unchanged);
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void Every_kind_of_change_is_named()
    {
        DeckResult passes = Joists(117), shortOf = Joists(140), porch = Joists(117, supports: "zz-porch"), wide = Joists(117, spacing: 24);
        DeckResult missing = Joists(117, species: null), noSupports = Joists(117, supports: null);
        DeckResult none = DeckEvaluator.CheckSpan(null, SpanUse.DeckJoist, new SpanRequest("2x8", In(117), null, null, null, null));

        Assert.Equal(DeckChangeKind.PassToShort, Kind(passes, shortOf));
        Assert.Equal(DeckChangeKind.ToShort, Kind(porch, shortOf));
        Assert.Equal(DeckChangeKind.ToOutOfScope, Kind(passes, porch));
        Assert.Equal(DeckChangeKind.ToNoAnswer, Kind(passes, none));
        Assert.Equal(DeckChangeKind.ToNoAnswer, Kind(porch, missing));
        Assert.Equal(DeckChangeKind.RowChanged, Kind(passes, Joists(117, spacing: 12)));
        Assert.Equal(DeckChangeKind.RowChanged, Kind(Ledger(117), Ledger(150)));
        Assert.Equal(DeckChangeKind.OutOfScopeChanged, Kind(porch, wide));
        Assert.Equal(DeckChangeKind.ShortToPass, Kind(shortOf, passes));
        Assert.Equal(DeckChangeKind.ToAnswer, Kind(porch, passes));
        Assert.Equal(DeckChangeKind.ToAnswer, Kind(none, Ledger(117)));
        Assert.Equal(DeckChangeKind.NoAnswerToOutOfScope, Kind(missing, porch));
        Assert.Equal(DeckChangeKind.NoAnswerChanged, Kind(missing, noSupports));
        Assert.Equal(DeckChangeKind.SpanMoved, Kind(passes, Joists(120)));
        Assert.Equal(DeckChangeKind.SpanMoved, Kind(shortOf, Joists(141)));

        // A ledger's answer is its row's fastener, spacing and count: another joist span in the same band is no change.
        Assert.Equal(1, Diff(Ledger(117), Ledger(118)).Unchanged);
    }

    [Fact]
    public void The_same_answer_under_another_revision_is_a_change_of_citation_only()
    {
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Root);
        source.With("packs/us-zz-deck/pack.json", source.Text("packs/us-zz-deck/pack.json").Replace("\"revision\": 1", "\"revision\": 2", StringComparison.Ordinal));
        LoadedPack revised = Fx.Loaded(PackLoader.Load(source, "us-zz-deck"));

        Assert.Equal(DeckChangeKind.CitationOnly, Kind(Joists(117), Joists(117, pack: revised)));
        Assert.Equal(DeckChangeKind.OutOfScopeChanged, Kind(Joists(117, supports: "zz-porch"), Joists(117, supports: "zz-porch", pack: revised)));
    }

    [Fact]
    public void The_report_puts_the_newly_flagged_first_and_counts_what_can_no_longer_be_computed()
    {
        DeckCheckKey beam = Key with { Check = "Beam" }, ledger = Key with { Check = "Ledger" }, footing = Key with { Check = "Footing" };
        DeckResult none = DeckEvaluator.SizeLedger(null, "2x8", In(117), In(144));
        DeckRecomputeReport report = Recompute.DiffDeck(
            [KeyValuePair.Create(Key, Joists(140)), KeyValuePair.Create(beam, Joists(117)), KeyValuePair.Create(ledger, Ledger(117)), KeyValuePair.Create(footing, Joists(117))],
            [KeyValuePair.Create(Key, Joists(117, supports: "zz-porch")), KeyValuePair.Create(beam, Joists(140)), KeyValuePair.Create(ledger, none), KeyValuePair.Create(footing, Joists(117))]);

        Assert.Equal([DeckChangeKind.PassToShort, DeckChangeKind.ToOutOfScope, DeckChangeKind.ToNoAnswer], report.Changes.Select(change => change.Kind));
        Assert.Equal(["Beam"], report.NewlyFlagged.Select(change => change.Key.Check));
        Assert.Equal(["Ledger"], report.NoLongerComputable.Select(change => change.Key.Check));
        Assert.Equal(1, report.Unchanged);

        Assert.Throws<ArgumentException>(() => Recompute.DiffDeck([KeyValuePair.Create(Key, Joists(117))], [KeyValuePair.Create(beam, Joists(117))]));
        Assert.Throws<ArgumentException>(() => Recompute.DiffDeck([KeyValuePair.Create(Key, Joists(117))], []));
    }
}

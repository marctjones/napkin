using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// The fifth header result, a row a person entered by hand (docs/design/manual-code-values.md §3,
/// §6.3, #246): labelled in every string form, never constructed by the engine, never a golden
/// expectation, and classified by the recompute diff before any fallback. SYNTHETIC TEST DATA - NOT
/// CODE VALUES throughout.
/// </summary>
public class EnteredResultTests
{
    static EnteredRow Row(string location = "p. 99, row 9") => new(
        Fx.Banner + ", Test Code 2099", "Table T-99", location, null, "A. Person", new DateOnly(2026, 9, 27), "us-zz-test",
        new ValueList<string>(["the header span: 3'-0\""]));

    static HeaderResult.Entered Entered(int plies = 2) => new(new MemberSpec(plies, "2x13"), 1, 2, Row());

    [Fact]
    public void Every_string_form_starts_with_the_tag_and_says_whose_row_it_is()
    {
        Assert.Equal("ENTERED BY HAND", EnteredRow.Tag);
        Assert.Equal(
            "ENTERED BY HAND — A. Person, 2026-09-27, from SYNTHETIC TEST DATA - NOT CODE VALUES, Test Code 2099, Table T-99, p. 99, row 9. "
            + "Typed from a copy of the code; not napkin's data, not reviewed by napkin.",
            Row().ToString());
        Assert.Equal("ENTERED BY HAND — (2) 2x13, 1 jack, 2 king — " + Row(), Entered().ToString());

        // A location typed with its own full stop does not end the sentence twice.
        Assert.Contains("row 9. Typed", Row("p. 99, row 9.").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_engine_never_answers_with_an_entered_row()
    {
        // Every pack every fixture root holds, real and synthetic, against a grid of requests: the
        // engine returns only its own four answers (§3.3). Only the building module's code check
        // ever makes an Entered, and only where the engine had nothing.
        List<LoadedPack?> packs = [null];
        foreach (string root in new[] { Fx.Root, Fx.BraceRoot, Fx.DeckRoot, Fx.RealRoot })
        {
            packs.AddRange(PackCatalog.Discover(root).OfType<PackLoadResult.Loaded>().Select(loaded => loaded.Pack));
        }

        Assert.True(packs.Count > 5, $"only {packs.Count - 1} packs loaded");
        int asked = 0;
        foreach (LoadedPack? pack in packs)
        {
            string[] supports = [.. (pack is null ? [] : pack.Tables.AsEnumerable()).SelectMany(table => table.Inputs.Where(column => column.Name == "supports").SelectMany(column => column.Values)).Distinct().Append("nothing-declared")];
            List<KeyValuePair<EntityId, HeaderRequest>> elements = [];
            foreach (string what in supports)
            {
                foreach (WallKind kind in Enum.GetValues<WallKind>())
                {
                    foreach (Length span in new[] { Fx.Ft(1), Fx.Ft(4), Fx.Ft(9), Fx.Ft(40) })
                    {
                        foreach (SiteInputs site in new[] { new SiteInputs(null, null, null, null, null, null, null), Fx.Site(snow: 20), Fx.Site(snow: 99, width: Fx.Ft(99), wind: 999, roofLive: 99) })
                        {
                            elements.Add(new(EntityId.New(), new HeaderRequest(what, kind, span, site)));
                        }
                    }
                }
            }

            foreach (KeyValuePair<EntityId, HeaderResult> result in Recompute.Headers(pack, elements))
            {
                Assert.IsNotType<HeaderResult.Entered>(result.Value);
                asked++;
            }
        }

        // At least one supports value per pack, every wall kind, four spans and three sites each.
        Assert.True(asked >= packs.Count * Enum.GetValues<WallKind>().Length * 4 * 3, $"only {asked} requests asked");
    }

    [Fact]
    public void A_golden_file_cannot_expect_an_entered_row()
    {
        // The golden vocabulary is the engine's four answers and gains no "entered" (§3.3).
        string json = File.ReadAllText(Path.Combine(Fx.GoldenRoot, "us-zz-state", "test-header-table.golden.json"));
        int at = json.IndexOf("\"sized\"", StringComparison.Ordinal);
        Assert.True(at > 0);
        string edited = string.Concat(json.AsSpan(0, at), "\"entered\"", json.AsSpan(at + "\"sized\"".Length));
        GoldenFileResult result = GoldenRunner.Run(PackLoader.Load(Fx.Root, "us-zz-state"), edited, "entered.golden.json");
        Assert.False(result.Passed);
        Assert.Contains(result.Problems, problem => problem.Contains("exactly one of sized, outOfScope, inputMissing, noData", StringComparison.Ordinal));
    }

    [Fact]
    public void The_diff_classifies_an_entered_row_before_any_fallback()
    {
        Citation cited = Fx.Sized(Fx.Size("us-zz-state", Fx.Roof(Fx.Ft(7), snow: 20, width: Fx.Ft(15)))).Citation;
        HeaderResult sized = new HeaderResult.Sized(new MemberSpec(2, "2x13"), 1, 2, cited);
        HeaderResult beyond = new HeaderResult.OutOfScope(OutOfScopeReason.SpanExceedsTable, cited, "SYNTHETIC limit.");
        HeaderResult noData = new HeaderResult.NoData(NoDataReason.NoTableForWallKind, null, WallKind.ExteriorBearing, "none");
        HeaderResult missing = new HeaderResult.InputMissing(new ValueList<string>(["side"]), string.Empty, cited.Code, "say it");

        (HeaderResult Before, HeaderResult After, ChangeKind Kind)[] cases =
        [
            (Entered(), Entered(plies: 3), ChangeKind.EnteredChanged),
            (Entered(), beyond, ChangeKind.EnteredToOutOfScope),
            (Entered(), sized, ChangeKind.EnteredToSized),
            (Entered(), noData, ChangeKind.EnteredToNoAnswer),
            (Entered(), missing, ChangeKind.EnteredToNoAnswer),
            (noData, Entered(), ChangeKind.ToEntered),
            (missing, Entered(), ChangeKind.ToEntered),
            (sized, Entered(), ChangeKind.ToEntered),
            (beyond, Entered(), ChangeKind.ToEntered),
        ];
        foreach ((HeaderResult before, HeaderResult after, ChangeKind kind) in cases)
        {
            EntityId element = EntityId.New();
            RecomputeReport report = Recompute.Diff([new(element, before)], [new(element, after)]);
            Assert.Equal(kind, Assert.Single(report.Changes).Kind);
        }

        // A live row that has not changed is not a change: record equality, its name and date stable.
        EntityId same = EntityId.New();
        RecomputeReport unchanged = Recompute.Diff([new(same, Entered())], [new(same, Entered())]);
        Assert.Empty(unchanged.Changes);
        Assert.Equal(1, unchanged.Unchanged);
    }
}

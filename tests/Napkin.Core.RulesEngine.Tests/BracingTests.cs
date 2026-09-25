using System.Text.Json.Nodes;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// The wall-bracing evaluator over the SYNTHETIC packs us-zz-brace-a and us-zz-brace-b
/// (tests/Napkin.Modules.Building.Tests/CodePacks/brace; NOT CODE VALUES). Every expected length is
/// worked by hand in the comments from the pack files' numbers, never read back from the engine.
/// </summary>
/// <remarks>
/// Pack A, section ZZ-BRACE.1: step 2"; per 10'-0" (120") of line, 4'-0" (48") up to 99 mph and
/// 5'-0" (60") up to 199 mph; × 5/4 above 150 mph (f.wind); + 1'-0" for walls over 9'-0" (f.tall);
/// out of scope over 12'-0" (l.tall). zz-panel: minimum 2'-0" (24") for walls ≤ 8'-0", 2'-6" (30")
/// ≤ 12'-0", cap 6'-0" (72"). zz-board: minimum 4'-0" (48"), no cap.
/// Pack B, section ZZ-BRACE-B.7: step 1"; 3'-0" (36") per 8'-0" (96"); zz-panel only, minimum 3'-0", no cap.
/// </remarks>
public class BracingTests
{
    private const string PanelA = "zz-panel";
    private const string Board = "zz-board";
    private const string BracingPathA = "layers/zz-brace-a-2099/bracing/zz-brace.json";

    private static readonly Length Line16 = Length.Inches(192);
    private static readonly Length Height8 = Length.Inches(96);

    private static LoadedPack A => Fx.Loaded(PackLoader.Load(Fx.BraceRoot, "us-zz-brace-a"));

    private static LoadedPack B => Fx.Loaded(PackLoader.Load(Fx.BraceRoot, "us-zz-brace-b"));

    private static SiteInputs Wind(int? mph) => new(null, mph, null, null, null, null, null);

    private static BracingRequest Line(int? wind, Length length, Length height, params (int Inches, string? Method)[] segments)
        => new(
            new BracedWallLine(
                length,
                height,
                segments.Select((s, i) => new BracedSegment($"segment {i + 1}", Length.Inches(s.Inches), s.Method)).ToValueList()),
            Wind(wind));

    private static BracingRequest Segments(int? wind, params Length[] panels)
        => new(
            new BracedWallLine(Line16, Height8, panels.Select((l, i) => new BracedSegment($"segment {i + 1}", l, PanelA)).ToValueList()),
            Wind(wind));

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void Both_synthetic_packs_load_with_their_provisions()
    {
        BracingProvisions a = A.Bracing!;
        Assert.Equal("ZZ-BRACE.1", a.Section);
        Assert.Equal(Length.Inches(2), a.Step);
        Assert.Equal(Length.Inches(120), a.UnitLength);
        Assert.Equal([PanelA, Board], a.Methods.Select(m => m.Id));
        Assert.Equal(["ultimateWindSpeed", "wallHeight"], a.RequiredInputs);
        Assert.Equal(string.Empty, A.StatusLabel);
        Assert.False(A.HasHeaderTables);
        Assert.Equal([PanelA], B.Bracing!.Methods.Select(m => m.Id));
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void A_line_that_provides_enough_passes_with_the_section_and_the_working()
    {
        // Required: 48 × 192 / 120 = 76.8" → up to the 2" step: 78" = 6'-6".
        // Provided: 30 + 60 + 30 = 120" (each ≥ 24" minimum, ≤ 72" cap, already on the step) = 10'-0".
        BracingResult result = RulesEngine.CheckBracing(A, Line(90, Line16, Height8, (30, PanelA), (60, PanelA), (30, PanelA)));
        BracingResult.Passes passes = Assert.IsType<BracingResult.Passes>(result);
        Assert.Equal(Length.Inches(78), passes.Required);
        Assert.Equal(Length.Inches(120), passes.Provided);
        Assert.Equal("q.w99", passes.Citation.RowId);
        Assert.StartsWith("IRC 2099 Section ZZ-BRACE.1, as adopted by ZZ BRACE A row q.w99", passes.Citation.ToString(), StringComparison.Ordinal);
        Assert.Equal(new ExactFraction(48 * 1024 * 192, 120), passes.Working.RequiredExact);
        Assert.Contains("Required: ", string.Join("\n", passes.Working.Lines), StringComparison.Ordinal);
        Assert.Equal("ultimateWindSpeed 90 mph → ≤ 99 mph", Assert.Single(passes.Citation.Trace).ToString());
        Assert.Equal("a", Assert.Single(passes.Citation.Footnotes).Id);
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void One_segment_shorter_by_1_1024_inch_flips_passes_to_fails_by_one_step()
    {
        // Required 78" (above). 40" + 38" = 78" exactly: passes at the boundary.
        BracingResult at = RulesEngine.CheckBracing(A, Segments(90, Length.Inches(40), Length.Inches(38)));
        Assert.Equal(Length.Inches(78), Assert.IsType<BracingResult.Passes>(at).Provided);

        // Widening the opening between them by 1/1024" leaves 37-1023/1024": rounded DOWN to 36",
        // so 40 + 36 = 76" < 78": short by 2" = one step, citing the section applied.
        BracingResult over = RulesEngine.CheckBracing(A, Segments(90, Length.Inches(40), Length.Inches(38) - new Length(1)));
        BracingResult.Fails fails = Assert.IsType<BracingResult.Fails>(over);
        Assert.Equal(Length.Inches(78), fails.Required);
        Assert.Equal(Length.Inches(76), fails.Provided);
        Assert.Equal(Length.Inches(2), fails.Shortfall);
        Assert.Equal("ZZ-BRACE.1", fails.Citation.Table);
        Assert.True(fails.Citation.IsSection);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void A_segment_under_the_minimum_panel_length_contributes_nothing()
    {
        // 23-1023/1024" is under the 24" minimum for an 8'-0" wall (m.h8): 0. 24" exactly counts: 24".
        BracingResult.Fails under = Assert.IsType<BracingResult.Fails>(
            RulesEngine.CheckBracing(A, Segments(90, Length.Inches(24) - new Length(1))));
        SegmentContribution only = Assert.Single(under.Working.Segments);
        Assert.Equal(Length.Zero, only.Contribution);
        Assert.Contains("shorter than the 2'-0\" minimum panel for walls ≤ 8'-0\" (ZZ-BRACE.3 row m.h8)", only.Why, StringComparison.Ordinal);

        BracingResult.Fails at = Assert.IsType<BracingResult.Fails>(RulesEngine.CheckBracing(A, Segments(90, Length.Inches(24))));
        Assert.Equal(Length.Inches(24), at.Provided);

        // A 10'-0" wall is in the ≤ 12'-0" band: 30" minimum, so 29" counts for nothing there.
        BracingResult.Passes tall = Assert.IsType<BracingResult.Passes>(
            RulesEngine.CheckBracing(A, Line(99, Length.Inches(144), Length.Inches(120), (29, PanelA), (97, Board))));
        Assert.Equal(Length.Zero, tall.Working.Segments[0].Contribution);
        Assert.Contains("row m.h12", tall.Working.Segments[0].Why, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void Required_rounds_up_and_each_contribution_rounds_down_to_the_step()
    {
        // 76.8" required → 78" (UP). 31" → 30" and 47" → 46" (DOWN): 76" < 78".
        BracingResult.Fails fails = Assert.IsType<BracingResult.Fails>(
            RulesEngine.CheckBracing(A, Segments(90, Length.Inches(31), Length.Inches(47))));
        Assert.Equal(Length.Inches(78), fails.Required);
        Assert.Equal([Length.Inches(30), Length.Inches(46)], fails.Working.Segments.Select(s => s.Contribution));
        Assert.Equal(Length.Inches(76), fails.Provided);

        // A cap is applied before rounding: 100" of zz-panel counts as the 72" cap.
        BracingResult.Fails capped = Assert.IsType<BracingResult.Fails>(RulesEngine.CheckBracing(A, Segments(160, Length.Inches(100))));
        Assert.Equal(Length.Inches(72), capped.Provided);
        Assert.Contains("capped at 6'-0\"", capped.Working.Segments[0].Why, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void Factors_multiply_and_add_as_the_pack_declares_and_are_cited()
    {
        // 160 mph: q.w199, 60 × 192 / 120 = 96" × 5/4 (f.wind) = 120" = 10'-0".
        BracingResult.Fails wind = Assert.IsType<BracingResult.Fails>(
            RulesEngine.CheckBracing(A, Line(160, Line16, Height8, (30, PanelA), (22, PanelA), (50, null))));
        Assert.Equal(Length.Inches(120), wind.Required);
        FactorApplied factor = Assert.Single(wind.Working.Factors);
        Assert.Equal("factor f.wind × 5/4 because ultimateWindSpeed 160 mph (ultimateWindSpeed above 150 mph) (ZZ-BRACE.2, synthetic p. 4, item 1)", factor.ToString());
        Assert.Equal(Length.Inches(30), wind.Provided);
        Assert.Equal(Length.Inches(90), wind.Shortfall);

        // 10'-0" wall, 12'-0" line: 48 × 144 / 120 = 57.6" + 12" (f.tall) = 69.6" → 70" = 5'-10".
        BracingResult.Passes tall = Assert.IsType<BracingResult.Passes>(
            RulesEngine.CheckBracing(A, Line(99, Length.Inches(144), Length.Inches(120), (29, PanelA), (97, Board))));
        Assert.Equal(Length.Inches(70), tall.Required);
        Assert.Equal("f.tall", Assert.Single(tall.Working.Factors).Id);
        Assert.Equal(Length.Inches(96), tall.Provided);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void An_unassigned_segment_is_not_braced_and_counts_for_nothing()
    {
        BracingResult.Fails fails = Assert.IsType<BracingResult.Fails>(
            RulesEngine.CheckBracing(A, Line(90, Line16, Height8, (60, null), (60, null))));
        Assert.Equal(Length.Zero, fails.Provided);
        Assert.All(fails.Working.Segments, s => Assert.Equal("not braced: no method assigned", s.Why));
        Assert.Equal(Length.Inches(78), fails.Shortfall);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void A_method_the_pack_does_not_have_counts_for_nothing_and_says_so()
    {
        // zz-board is pack A's; under pack B a zz-board segment is re-flagged, not carried over.
        BracingResult.Fails fails = Assert.IsType<BracingResult.Fails>(
            RulesEngine.CheckBracing(B, Line(90, Line16, Height8, (60, Board))));
        Assert.Contains("method 'zz-board' is not one of ZZ BRACE B's methods", fails.Working.Segments[0].Why, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void Out_of_scope_cites_the_limit_and_never_gives_a_length()
    {
        BracingResult.OutOfScope limit = Assert.IsType<BracingResult.OutOfScope>(
            RulesEngine.CheckBracing(A, Line(90, Line16, Length.Inches(145), (60, PanelA))));
        Assert.Equal(OutOfScopeReason.NotPrescriptive, limit.Reason);
        Assert.Equal("l.tall", limit.Limit.RowId);
        Assert.Contains("get an engineer", limit.Explanation, StringComparison.Ordinal);

        BracingResult.OutOfScope wind = Assert.IsType<BracingResult.OutOfScope>(RulesEngine.CheckBracing(A, Segments(200, Length.Inches(60))));
        Assert.Equal(OutOfScopeReason.InputAboveTableBands, wind.Reason);
        Assert.Equal("q.w199", wind.Limit.RowId);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void A_wall_taller_than_a_methods_bands_is_out_of_scope_citing_the_method()
    {
        // Lift pack A's own limit so the method's 12'-0" wall-height domain is what stops a 12'-1" wall.
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Fx.BraceRoot);
        JsonNode node = JsonNode.Parse(source.Text(BracingPathA))!;
        node["limits"]!.AsArray().Clear();
        LoadedPack lifted = Fx.Loaded(PackLoader.Load(source.With(BracingPathA, node.ToJsonString()), "us-zz-brace-a"));
        BracingResult.OutOfScope result = Assert.IsType<BracingResult.OutOfScope>(
            RulesEngine.CheckBracing(lifted, Line(90, Line16, Length.Inches(145), (60, PanelA))));
        Assert.Equal(OutOfScopeReason.InputAboveTableBands, result.Reason);
        Assert.Equal("m.h12", result.Limit.RowId);
        Assert.Equal("ZZ-BRACE.3", result.Limit.Table);

        // An unassigned line of that height is not stopped by a method it does not use.
        Assert.IsType<BracingResult.Fails>(RulesEngine.CheckBracing(lifted, Line(90, Line16, Length.Inches(145), (60, null))));
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void A_missing_site_value_is_named_and_never_defaulted()
    {
        BracingResult.InputMissing missing = Assert.IsType<BracingResult.InputMissing>(RulesEngine.CheckBracing(A, Segments(null, Length.Inches(60))));
        Assert.Equal(["ultimateWindSpeed"], missing.Inputs);
        Assert.Equal("ZZ-BRACE.1", missing.Section);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void No_pack_and_the_shipped_Connecticut_pack_both_say_there_is_no_data()
    {
        Assert.Equal(BracingNoDataReason.NoPackSelected, Assert.IsType<BracingResult.NoData>(RulesEngine.CheckBracing(null, Segments(90, Length.Inches(60)))).Reason);

        LoadedPack ct = Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "RealPacks"), "us-ct-2022"));
        Assert.Null(ct.Bracing);
        BracingResult.NoData none = Assert.IsType<BracingResult.NoData>(RulesEngine.For(ct).CheckBracing(Segments(90, Length.Inches(60))));
        Assert.Equal(BracingNoDataReason.NoBracingProvisions, none.Reason);
        Assert.StartsWith("The loaded pack CT 2022 has no wall-bracing provisions, so napkin cannot check this wall line's bracing.", none.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-008")]
    public void Switching_to_pack_B_recomputes_every_line_and_the_diff_names_what_was_newly_flagged()
    {
        EntityId one = EntityId.New();
        EntityId two = EntityId.New();
        EntityId three = EntityId.New();
        List<KeyValuePair<EntityId, BracingRequest>> lines =
        [
            // A: required 78", provided 120": passes. B: 36 × 192 / 96 = 72" required; the 30" panels
            // are under B's 36" minimum: 60" provided, short 12": newly flagged.
            new(one, Line(90, Line16, Height8, (30, PanelA), (60, PanelA), (30, PanelA))),

            // Unassigned: short under both, by 78" (A) and 72" (B): still short, by a different amount.
            new(two, Line(90, Line16, Height8, (60, null))),

            // 10'-1" wall: A (≤ 12'-0"): 76.8" + 12" (f.tall) = 88.8" → 90"; two 72" panels (≥ 30"
            // minimum, at the cap) give 144": passes. B's limit l.b-tall excludes it: flagged out of scope.
            new(three, Line(90, Line16, Length.Inches(121), (72, PanelA), (72, PanelA))),
        ];

        ValueList<KeyValuePair<EntityId, BracingResult>> before = Recompute.Bracing(A, lines);
        ValueList<KeyValuePair<EntityId, BracingResult>> after = Recompute.Bracing(B, lines);
        BracingRecomputeReport report = Recompute.DiffBracing(before, after);

        Assert.Equal(0, report.Unchanged);
        Assert.Equal(BracingChangeKind.PassToFail, report.Changes.Single(c => c.Element == one).Kind);
        Assert.Equal(BracingChangeKind.FailChanged, report.Changes.Single(c => c.Element == two).Kind);
        Assert.Equal(BracingChangeKind.ToOutOfScope, report.Changes.Single(c => c.Element == three).Kind);
        Assert.Equal([BracingChangeKind.PassToFail, BracingChangeKind.ToOutOfScope], report.NewlyFlagged.Select(c => c.Kind));
        Assert.Equal(Length.Inches(12), ((BracingResult.Fails)report.Changes.Single(c => c.Element == one).After).Shortfall);

        // And to no pack at all: every line can no longer be computed.
        BracingRecomputeReport gone = Recompute.DiffBracing(after, Recompute.Bracing(null, lines));
        Assert.Equal(3, gone.NoLongerComputable.Count());

        // The same pack twice: nothing changed.
        Assert.Equal(3, Recompute.DiffBracing(before, Recompute.Bracing(A, lines)).Unchanged);
        Assert.Throws<ArgumentException>(() => Recompute.DiffBracing(before, after.Take(2).ToValueList()));
    }

    [Theory]
    [InlineData("step", "\"0in\"", "step: must be longer than zero")]
    [InlineData("unitLength", "\"0in\"", "unitLength: must be longer than zero")]
    [InlineData("unitLength", "\"-1in\"", "unitLength: '-1in' is negative")]
    [Trait("Feature", "RUL-005")]
    public void A_non_positive_step_or_unit_is_refused(string field, string value, string message)
        => Refused(n => n[field] = JsonNode.Parse(value), message);

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void Gaps_and_overlaps_in_the_bands_are_refused()
    {
        Refused(n => n["required"]!.AsArray().RemoveAt(1), "gap: for the table, the 'ultimateWindSpeed' bands stop at 99 mph but the column's domain declares max 199 mph.");
        Refused(n => n["required"]![1]!["ultimateWindSpeed"] = 99, "overlap: rows 'q.w199', 'q.w99' have the same inputs.");
        Refused(n => n["methods"]![0]!["minimumPanel"]!["rows"]![1]!["wallHeight"] = "8ft 0in", "overlap: rows 'm.h8', 'm.h12' have the same wall height 8'-0\"");
        Refused(n => n["methods"]![0]!["minimumPanel"]!["rows"]!.AsArray().RemoveAt(1), "gap: the wall-height bands stop at 8'-0\" but the domain declares max 12'-0\"");
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void Every_factor_and_limit_must_be_cited()
    {
        Refused(n => n["factors"]![0]!.AsObject().Remove("section"), "factors[0].section: missing; every factor and limit cites its section in the source.");
        Refused(n => n["limits"]![0]!["location"] = " ", "limits[0].location: blank");
        Refused(n => n["factors"]![0]!["multiply"] = "1.25", "is not a positive exact fraction");
        Refused(n => n["factors"]![0]!["multiply"] = "1/0", "is not a positive exact fraction");
        Refused(n => n["factors"]![0]!["multiply"] = "0", "is not a positive exact fraction");
        Refused(n => n["factors"]![1]!["multiply"] = "2", "a factor has exactly one of 'multiply'");
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void An_unclassified_footnote_or_unknown_input_is_refused()
    {
        Refused(n => n["footnotes"]![0]!.AsObject().Remove("encodedAs"), "footnote is not classified");
        Refused(n => n["inputs"]![0]!["name"] = "headerSpan", "'headerSpan' is not an input napkin can supply to a bracing check");
        Refused(n => n["factors"]![0]!["when"]!["input"] = "frostDepth", "'frostDepth' is not an input napkin can supply to a bracing check");
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void A_second_bracing_file_in_one_layer_is_refused()
    {
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Fx.BraceRoot);
        source.With("layers/zz-brace-a-2099/bracing/again.json", source.Text(BracingPathA));
        PackLoadResult.Invalid invalid = Fx.Invalid(PackLoader.Load(source, "us-zz-brace-a"));
        Assert.Contains(invalid.Problems, p => p.Message.Contains("2 files under bracing/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void The_bracing_golden_files_pass_and_cover_every_row_and_factor()
    {
        string file = Path.Combine(Fx.BraceRoot, "golden", "us-zz-brace-a", "zz-brace.golden.json");
        GoldenFileResult a = GoldenRunner.Run(Fx.BraceRoot, file);
        Assert.True(a.Passed, a.ToString());
        Assert.Equal(8, a.Cases.Count);
        Assert.True(GoldenRunner.Run(Fx.BraceRoot, Path.Combine(Fx.BraceRoot, "golden", "us-zz-brace-b", "zz-brace-b.golden.json")).Passed);

        // A case that expects the wrong shortfall fails and says why; dropping the only case that
        // applies f.tall leaves the factor uncovered.
        string json = File.ReadAllText(file);
        GoldenFileResult wrong = GoldenRunner.Run(PackLoader.Load(Fx.BraceRoot, "us-zz-brace-a"), json.Replace("\"shortfall\": \"7ft 6in\"", "\"shortfall\": \"7ft 4in\"", StringComparison.Ordinal), "edited.golden.json");
        Assert.Contains("expected required 10'-0\", provided 2'-6\", short 7'-4\"", Assert.Single(wrong.Cases, c => !c.Passed).Detail, StringComparison.Ordinal);
        GoldenFileResult uncovered = GoldenRunner.Run(PackLoader.Load(Fx.BraceRoot, "us-zz-brace-a"), json.Replace("\"f.tall\"", "\"f.wind\"", StringComparison.Ordinal), "edited.golden.json");
        Assert.Contains(uncovered.Problems, p => p.Contains("factor 'f.tall' of section ZZ-BRACE.1 is applied by no hand-authored golden case", StringComparison.Ordinal));
    }

    private static void Refused(Action<JsonNode> edit, string message)
    {
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Fx.BraceRoot);
        JsonNode node = JsonNode.Parse(source.Text(BracingPathA))!;
        edit(node);
        PackLoadResult.Invalid invalid = Fx.Invalid(PackLoader.Load(source.With(BracingPathA, node.ToJsonString()), "us-zz-brace-a"));
        Assert.True(
            invalid.Problems.Any(p => p.Message.Contains(message, StringComparison.Ordinal)),
            $"expected a problem containing \"{message}\"; got:\n{invalid}");
    }
}

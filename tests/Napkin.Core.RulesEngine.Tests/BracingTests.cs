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
        Assert.Equal(9, a.Cases.Count);
        Assert.True(GoldenRunner.Run(Fx.BraceRoot, Path.Combine(Fx.BraceRoot, "golden", "us-zz-brace-b", "zz-brace-b.golden.json")).Passed);

        // A case that expects the wrong shortfall fails and says why; dropping the only case that
        // applies f.tall leaves the factor uncovered.
        string json = File.ReadAllText(file);
        GoldenFileResult wrong = GoldenRunner.Run(PackLoader.Load(Fx.BraceRoot, "us-zz-brace-a"), json.Replace("\"shortfall\": \"7ft 6in\"", "\"shortfall\": \"7ft 4in\"", StringComparison.Ordinal), "edited.golden.json");
        Assert.Contains("expected required 10'-0\", provided 2'-6\", short 7'-4\"", Assert.Single(wrong.Cases, c => !c.Passed).Detail, StringComparison.Ordinal);
        GoldenFileResult uncovered = GoldenRunner.Run(PackLoader.Load(Fx.BraceRoot, "us-zz-brace-a"), json.Replace("\"f.tall\"", "\"f.wind\"", StringComparison.Ordinal), "edited.golden.json");
        Assert.Contains(uncovered.Problems, p => p.Contains("factor 'f.tall' of section ZZ-BRACE.1 is applied by no hand-authored golden case", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void A_category_column_selects_base_rows_exactly_and_a_category_it_lacks_is_out_of_scope()
    {
        // Pack A with a seismicDesignCategory column (SYNTHETIC categories ZZ-A and ZZ-B): ZZ-B rows
        // are twice ZZ-A's, and a factor × 3/2 applies when the category equals ZZ-B.
        LoadedPack pack = Fx.Loaded(PackLoader.Load(WithCategory(), "us-zz-brace-a"));
        SiteInputs site = new(null, 90, "ZZ-B", null, null, null, null);
        BracedWallLine line = new(Line16, Height8, ValueList.Of(new BracedSegment("all", Length.Inches(72), PanelA)));

        // ZZ-B ≤ 99 mph: 96" per 120" × 192 = 153.6" × 3/2 = 230.4" → 232" (2" step).
        BracingResult.Fails fails = Assert.IsType<BracingResult.Fails>(RulesEngine.CheckBracing(pack, new BracingRequest(line, site)));
        Assert.Equal(Length.Inches(232), fails.Required);
        Assert.Equal("q.b.w99", fails.Citation.RowId);
        Assert.Equal(["seismicDesignCategory ZZ-B → = ZZ-B", "ultimateWindSpeed 90 mph → ≤ 99 mph"], fails.Citation.Trace.Select(t => t.ToString()));
        Assert.Equal("f.sdc", Assert.Single(fails.Working.Factors).Id);

        BracingResult.OutOfScope other = Assert.IsType<BracingResult.OutOfScope>(
            RulesEngine.CheckBracing(pack, new BracingRequest(line, new SiteInputs(null, 90, "ZZ-C", null, null, null, null))));
        Assert.Equal(OutOfScopeReason.ConditionNotCovered, other.Reason);
        Assert.Null(other.Limit.RowId);

        // Below the column's declared minimum (1 mph): out of scope, citing the lowest row.
        BracingResult.OutOfScope below = Assert.IsType<BracingResult.OutOfScope>(RulesEngine.CheckBracing(A, Segments(0, Length.Inches(60))));
        Assert.Equal(OutOfScopeReason.InputBelowTableBands, below.Reason);
        Assert.Equal("q.w99", below.Limit.RowId);
        Assert.Contains("passes", RulesEngine.CheckBracing(A, Segments(90, Length.Inches(72), Length.Inches(72))).ToString(), StringComparison.Ordinal);
        Assert.Contains("SHORT by", RulesEngine.CheckBracing(A, Segments(90)).ToString(), StringComparison.Ordinal);
        Assert.Contains("out of scope", below.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void Every_malformed_bracing_field_is_refused_with_its_own_message()
    {
        Refused(n => n["kind"] = "header-sizing", "a file under bracing/ is 'wall-bracing', not 'header-sizing'");
        Refused(n => n["source"] = "nope", "source 'nope' is not listed");
        Refused(n => n["inputs"]!.AsArray().Add(JsonNode.Parse("""{ "name": "ultimateWindSpeed", "type": "mph", "band": "upper-bound", "domain": { "min": 1, "max": 199 } }""")), "'ultimateWindSpeed' is declared twice");
        Refused(n => n["inputs"]![0]!.AsObject().Remove("band"), "inputs[0].band: missing required field");
        Refused(n => n["inputs"]![0]!["type"] = "psf", "'ultimateWindSpeed' is 'mph', not 'psf'");
        Refused(n => n["inputs"]![0]!["band"] = "exact", "a bracing column that is not a category uses 'upper-bound' bands");
        Refused(n => n["inputs"]![0]!["domain"]!["min"] = 300, "is above max");
        Refused(n => n["required"]![0]!["ultimateWindSpeed"] = 250, "is outside the column's declared domain");
        Refused(n => n["required"]![1]!["id"] = "q.w99", "row id 'q.w99' appears twice");
        Refused(n => n["factors"]![1]!["id"] = "f.wind", "id 'f.wind' appears twice");
        Refused(n => n["factors"]![0]!["when"]!["equals"] = "x", "a condition has exactly one of 'above'");
        Refused(n => n["factors"]![0]!["when"] = JsonNode.Parse("""{ "input": "ultimateWindSpeed", "equals": "x" }"""), "'ultimateWindSpeed' is not a category; use 'above'");
        Refused(n => n["methods"]![0]!["id"] = "ZZ Panel", "is not a method id");
        Refused(n => n["methods"]![1]!["id"] = "zz-panel", "method id 'zz-panel' appears twice");
        Refused(n => n["methods"]![0]!["cap"] = "0in", "a cap is longer than zero");
        Refused(n => n["methods"]![0]!["minimumPanel"]!["rows"]![0]!["length"] = "0in", "a minimum panel length is longer than zero");
        Refused(n => n["methods"]![0]!["minimumPanel"]!["rows"]![0]!["wallHeight"] = "13ft 0in", "is outside the declared domain");
        Refused(n => n["methods"]![0]!["minimumPanel"]!["rows"]![1]!["id"] = "m.h8", "row id 'm.h8' appears twice");
        Refused(n => n["footnotes"]![0]!["appliesTo"] = "rows", "a bracing footnote applies to the whole section");
        Refused(n => n["footnotes"]!.AsArray().Add(JsonNode.Parse("""{ "id": "a", "text": "again", "encodedAs": "as-rows", "appliesTo": "table" }""")), "footnote 'a' is listed twice");
        Refused(
            n => n["footnotes"]![0] = JsonNode.Parse("""{ "id": "a", "text": "x", "encodedAs": "as-limit", "appliesTo": "table", "limit": { "input": "groundSnowLoad", "above": 5 } }"""),
            "a bracing footnote is 'not-encoded' or 'as-rows'");
        Refused(n => n["surprise"] = 1, "surprise: unknown field");

        InMemoryPackSource category = WithCategory();
        JsonNode node = JsonNode.Parse(category.Text(BracingPathA))!;
        node["inputs"]![1]!["band"] = "upper-bound";
        node["inputs"]![1]!["values"]!.AsArray().Add("ZZ-A");
        node["factors"]![2]!["when"] = JsonNode.Parse("""{ "input": "seismicDesignCategory", "above": 1 }""");
        node["limits"]![0]!["when"] = JsonNode.Parse("""{ "input": "seismicDesignCategory", "equals": "ZZ-Q" }""");
        node["required"]![0]!["seismicDesignCategory"] = "ZZ-Q";
        PackLoadResult.Invalid invalid = Fx.Invalid(PackLoader.Load(category.With(BracingPathA, node.ToJsonString()), "us-zz-brace-a"));
        string all = invalid.ToString();
        Assert.Contains("a category column uses 'exact' bands", all, StringComparison.Ordinal);
        Assert.Contains("'ZZ-A' is listed twice", all, StringComparison.Ordinal);
        Assert.Contains("'seismicDesignCategory' is a category; use 'equals'", all, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void Missing_or_mistyped_structure_is_refused_where_it_is()
    {
        Refused(n => n.AsObject().Remove("step"), "step: missing required field");
        Refused(n => n["inputs"] = 5, "inputs: must be a JSON array");
        Refused(n => n["inputs"]![0] = 5, "inputs[0]: must be a JSON object");
        Refused(n => n["inputs"]![0]!.AsObject().Remove("domain"), "inputs[0].domain: missing required field");
        Refused(n => n["inputs"]![0]!["domain"]!.AsObject().Remove("min"), "inputs[0].domain.min: missing required field");
        Refused(n => n["required"]![0] = 5, "required[0]: must be a JSON object");
        Refused(n => n["required"]![0]!.AsObject().Remove("length"), "length: missing required field");
        Refused(n => n["required"]![0]!.AsObject().Remove("ultimateWindSpeed"), "ultimateWindSpeed: missing required field");
        Refused(n => n["factors"] = 5, "factors: must be a JSON array");
        Refused(n => n["factors"]![0] = 5, "factors[0]: must be a JSON object");
        Refused(n => n["factors"]![0]!.AsObject().Remove("when"), "when: missing required field");
        Refused(n => n["factors"]![0]!["when"] = JsonNode.Parse("""{ "above": 1 }"""), "input: missing required field");
        Refused(n => n["factors"]![0]!["when"]!["above"] = "fast", "above");
        Refused(n => n["factors"]![1]!["add"] = "a foot", "add");
        Refused(n => n["factors"]![0]!["multiply"] = 2, "multiply");
        Refused(n => n["methods"] = 5, "methods: must be a JSON array");
        Refused(n => n["methods"]![0] = 5, "methods[0]: must be a JSON object");
        Refused(n => n["methods"]![0]!.AsObject().Remove("minimumPanel"), "minimumPanel: missing required field");
        Refused(n => n["methods"]![0]!["minimumPanel"]!["rows"] = 5, "rows: must be a JSON array");
        Refused(n => n["methods"]![0]!["minimumPanel"]!["rows"]![0] = 5, "rows[0]: must be a JSON object");
        Refused(n => n["methods"]![0]!["minimumPanel"]!["rows"]![0]!.AsObject().Remove("location"), "location: missing required field");
        Refused(n => n["footnotes"] = 5, "footnotes: must be a JSON array");

        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Fx.BraceRoot);
        Assert.Contains(
            Fx.Invalid(PackLoader.Load(source.With(BracingPathA, "[]"), "us-zz-brace-a")).Problems,
            p => p.Message.Contains("must be a JSON object", StringComparison.Ordinal));

        // A condition on a category the base rows do not use still names an input the check then needs.
        JsonNode node = JsonNode.Parse(InMemoryPackSource.FromDirectory(Fx.BraceRoot).Text(BracingPathA))!;
        node["factors"]![0]!["when"] = JsonNode.Parse("""{ "input": "seismicDesignCategory", "equals": "ZZ-Q" }""");
        LoadedPack withCondition = Fx.Loaded(PackLoader.Load(InMemoryPackSource.FromDirectory(Fx.BraceRoot).With(BracingPathA, node.ToJsonString()), "us-zz-brace-a"));
        Assert.Equal(["seismicDesignCategory"], Assert.IsType<BracingResult.InputMissing>(RulesEngine.CheckBracing(withCondition, Segments(90, Length.Inches(30)))).Inputs);
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void Malformed_bracing_golden_cases_are_file_problems_and_no_data_is_expectable()
    {
        LoadedPack a = A;
        string Golden(string cases) => $$"""{ "pack": "us-zz-brace-a", "section": "ZZ-BRACE.1", "source": "s", "transcriber": { "who": "t", "on": "2026-09-25" }, "cases": [ {{cases}} ] }""";
        const string Line = "\"lineLength\": \"16ft 0in\", \"wallHeight\": \"8ft 0in\"";

        GoldenFileResult twoKinds = GoldenRunner.Run(new PackLoadResult.Loaded(a), Golden($$"""{ "row": "q.w99", "location": "x", "inputs": { "ultimateWindSpeed": 90, {{Line}}, "segments": [] }, "expect": { "passes": {}, "fails": {} } }"""), "g.json");
        Assert.Contains(twoKinds.Problems, p => p.Contains("exactly one of passes, fails", StringComparison.Ordinal));

        GoldenFileResult noRow = GoldenRunner.Run(new PackLoadResult.Loaded(a), Golden($$"""{ "location": "x", "inputs": { "ultimateWindSpeed": 90, {{Line}}, "segments": [] }, "expect": { "passes": { "required": "6ft 6in", "provided": "0in", "factors": [] } } }"""), "g.json");
        Assert.Contains(noRow.Problems, p => p.Contains("names the base row it expects", StringComparison.Ordinal));

        GoldenFileResult badSegment = GoldenRunner.Run(new PackLoadResult.Loaded(a), Golden($$"""{ "row": "q.w99", "location": "x", "inputs": { "ultimateWindSpeed": 90, {{Line}}, "segments": [ { "length": "0in", "method": null } ] }, "expect": { "noData": { "reason": "NoPackSelected" } } }"""), "g.json");
        Assert.Contains(badSegment.Problems, p => p.Contains("a segment is longer than zero", StringComparison.Ordinal));

        GoldenFileResult tooLong = GoldenRunner.Run(new PackLoadResult.Loaded(a), Golden($$"""{ "row": "q.w99", "location": "x", "inputs": { "ultimateWindSpeed": 90, {{Line}}, "segments": [ { "length": "17ft 0in", "method": null } ] }, "expect": { "noData": { "reason": "NoPackSelected" } } }"""), "g.json");
        Assert.Contains(tooLong.Problems, p => p.Contains("the segments fit in the line", StringComparison.Ordinal));

        // Wrong expectations fail with why: a wrong reason, a wrong missing list, the wrong kind.
        GoldenFileResult wrong = GoldenRunner.Run(new PackLoadResult.Loaded(a), Golden(
            $$"""
            { "location": "x", "inputs": { "ultimateWindSpeed": 200, {{Line}}, "segments": [] }, "expect": { "outOfScope": { "reason": "NotPrescriptive", "limitRow": null } } },
            { "location": "x", "inputs": { {{Line}}, "segments": [] }, "expect": { "inputMissing": { "inputs": ["groundSnowLoad"] } } },
            { "location": "x", "inputs": { "ultimateWindSpeed": 90, {{Line}}, "segments": [] }, "expect": { "noData": { "reason": "NoBracingProvisions" } } },
            { "row": "q.w99", "location": "x", "inputs": { "ultimateWindSpeed": 90, {{Line}}, "segments": [] }, "expect": { "passes": { "required": "6ft 6in", "provided": "0in", "factors": [] } } },
            { "location": "x", "inputs": { "ultimateWindSpeed": 90, {{Line}}, "segments": [] }, "expect": { "outOfScope": { "reason": "InputAboveTableBands", "limitRow": null } } },
            { "location": "x", "inputs": { "ultimateWindSpeed": 200, {{Line}}, "segments": [] }, "expect": { "outOfScope": { "reason": "InputAboveTableBands", "limitRow": "q.w99" } } },
            { "location": "x", "inputs": { "ultimateWindSpeed": 90, {{Line}}, "segments": [] }, "expect": { "inputMissing": { "inputs": ["ultimateWindSpeed"] } } }
            """), "g.json");
        Assert.Equal(7, wrong.Cases.Count(c => !c.Passed));
        Assert.Contains("expected NotPrescriptive; got InputAboveTableBands", wrong.Cases[0].Detail, StringComparison.Ordinal);
        Assert.Contains("expected missing [groundSnowLoad]", wrong.Cases[1].Detail, StringComparison.Ordinal);
        Assert.Contains("expected no data; got", wrong.Cases[2].Detail, StringComparison.Ordinal);
        Assert.Contains("expected passes; got", wrong.Cases[3].Detail, StringComparison.Ordinal);
        Assert.Contains("expected out of scope", wrong.Cases[4].Detail, StringComparison.Ordinal);
        Assert.Contains("right reason but cites 'q.w199', expected 'q.w99'", wrong.Cases[5].Detail, StringComparison.Ordinal);
        Assert.Contains("expected input missing; got", wrong.Cases[6].Detail, StringComparison.Ordinal);

        // A pack without bracing: the honest no-data answer is what a golden case can expect.
        LoadedPack ct = Fx.Loaded(PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "RealPacks"), "us-ct-2022"));
        string noData = Golden($$"""{ "location": "x", "inputs": { "ultimateWindSpeed": 90, {{Line}}, "segments": [] }, "expect": { "noData": { "reason": "NoBracingProvisions" } } }""")
            .Replace("us-zz-brace-a", "us-ct-2022", StringComparison.Ordinal);
        GoldenFileResult none = GoldenRunner.Run(new PackLoadResult.Loaded(ct), noData, "ct.json");
        Assert.True(none.Passed, none.ToString());
        GoldenFileResult wrongReason = GoldenRunner.Run(new PackLoadResult.Loaded(ct), noData.Replace("NoBracingProvisions", "NoPackSelected", StringComparison.Ordinal), "ct.json");
        Assert.Contains("expected no data (NoPackSelected); got NoBracingProvisions", Assert.Single(wrongReason.Cases).Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-005")]
    public void A_malformed_wall_line_is_a_caller_bug_not_a_result()
    {
        Assert.Throws<ArgumentException>(() => new BracedSegment("s", Length.Zero, null));
        Assert.Throws<ArgumentException>(() => new BracedWallLine(Line16, Length.Zero, ValueList<BracedSegment>.Empty));
        Assert.Throws<ArgumentException>(() => new BracedWallLine(Length.Inches(10), Height8, ValueList.Of(new BracedSegment("s", Length.Inches(11), null))));
        Assert.Throws<ArgumentNullException>(() => RulesEngine.CheckBracing(A, null!));
        Assert.Throws<ArgumentNullException>(() => RulesEngine.For(A).CheckBracing(null!));
        Assert.Throws<ArgumentNullException>(() => Recompute.Bracing(A, null!));
        Assert.Throws<ArgumentNullException>(() => Recompute.DiffBracing(null!, ValueList<KeyValuePair<EntityId, BracingResult>>.Empty));
        Assert.Throws<ArgumentNullException>(() => Recompute.DiffBracing(ValueList<KeyValuePair<EntityId, BracingResult>>.Empty, null!));
    }

    [Fact]
    [Trait("Feature", "RUL-008")]
    public void Every_pair_of_bracing_results_is_classified()
    {
        EntityId e = EntityId.New();
        BracingResult pass = RulesEngine.CheckBracing(A, Segments(90, Length.Inches(72), Length.Inches(72)));
        BracingResult passMore = RulesEngine.CheckBracing(A, Segments(90, Length.Inches(72), Length.Inches(72), Length.Inches(40)));
        BracingResult fail = RulesEngine.CheckBracing(A, Segments(90, Length.Inches(30)));
        BracingResult failMore = RulesEngine.CheckBracing(A, Segments(90, Length.Inches(40)));
        BracingResult failOther = ((BracingResult.Fails)fail) with { Citation = ((BracingResult.Fails)fail).Citation with { RowLabel = "relabelled" } };
        BracingResult passOther = ((BracingResult.Passes)pass) with { Citation = ((BracingResult.Passes)pass).Citation with { RowLabel = "relabelled" } };
        BracingResult above = RulesEngine.CheckBracing(A, Segments(200, Length.Inches(30)));
        BracingResult tall = RulesEngine.CheckBracing(A, Line(90, Line16, Length.Inches(145), (30, PanelA)));
        BracingResult missing = RulesEngine.CheckBracing(A, Segments(null, Length.Inches(30)));
        BracingResult none = RulesEngine.CheckBracing(null, Segments(90, Length.Inches(30)));

        BracingChangeKind Kind(BracingResult before, BracingResult after)
            => Assert.Single(Recompute.DiffBracing([KeyValuePair.Create(e, before)], [KeyValuePair.Create(e, after)]).Changes).Kind;

        Assert.Equal(BracingChangeKind.PassChanged, Kind(pass, passMore));
        Assert.Equal(BracingChangeKind.CitationOnly, Kind(pass, passOther));
        Assert.Equal(BracingChangeKind.FailChanged, Kind(fail, failMore));
        Assert.Equal(BracingChangeKind.CitationOnly, Kind(fail, failOther));
        Assert.Equal(BracingChangeKind.PassToFail, Kind(pass, fail));
        Assert.Equal(BracingChangeKind.ToFail, Kind(above, fail));
        Assert.Equal(BracingChangeKind.FailToPass, Kind(fail, pass));
        Assert.Equal(BracingChangeKind.ToPass, Kind(missing, pass));
        Assert.Equal(BracingChangeKind.OutOfScopeChanged, Kind(above, tall));
        Assert.Equal(BracingChangeKind.ToOutOfScope, Kind(fail, tall));
        Assert.Equal(BracingChangeKind.NoAnswerToOutOfScope, Kind(none, tall));
        Assert.Equal(BracingChangeKind.ToNoAnswer, Kind(tall, missing));
        Assert.Equal(BracingChangeKind.NoAnswerChanged, Kind(missing, none));
        Assert.Empty(Recompute.DiffBracing([KeyValuePair.Create(e, fail)], [KeyValuePair.Create(e, fail)]).Changes);
    }

    /// <summary>Pack A with a SYNTHETIC seismicDesignCategory column (ZZ-A, ZZ-B), four base rows, and a factor on ZZ-B.</summary>
    private static InMemoryPackSource WithCategory()
    {
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Fx.BraceRoot);
        JsonNode node = JsonNode.Parse(source.Text(BracingPathA))!;
        node["inputs"]!.AsArray().Add(JsonNode.Parse("""{ "name": "seismicDesignCategory", "type": "enum", "band": "exact", "values": ["ZZ-A", "ZZ-B"] }"""));
        node["required"] = JsonNode.Parse(
            """
            [ { "id": "q.a.w99", "ultimateWindSpeed": 99, "seismicDesignCategory": "ZZ-A", "length": "4ft 0in", "location": "s" },
              { "id": "q.a.w199", "ultimateWindSpeed": 199, "seismicDesignCategory": "ZZ-A", "length": "5ft 0in", "location": "s" },
              { "id": "q.b.w99", "ultimateWindSpeed": 99, "seismicDesignCategory": "ZZ-B", "length": "8ft 0in", "location": "s" },
              { "id": "q.b.w199", "ultimateWindSpeed": 199, "seismicDesignCategory": "ZZ-B", "length": "10ft 0in", "location": "s" } ]
            """);
        node["factors"]!.AsArray().Add(JsonNode.Parse("""{ "id": "f.sdc", "section": "ZZ-BRACE.2", "location": "s", "when": { "input": "seismicDesignCategory", "equals": "ZZ-B" }, "multiply": "3/2" }"""));
        return source.With(BracingPathA, node.ToJsonString());
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

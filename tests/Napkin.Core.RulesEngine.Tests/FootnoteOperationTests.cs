using System.Text.Json.Nodes;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// Footnote operations (design §4.4, decided 2026-09-25, Marc, #157): substitute-input and
/// interpolate, declared by an overlay's amend-footnote exactly as Connecticut's R602.7(1) footnote
/// e is shaped. Over the synthetic pack us-zz-interp — SYNTHETIC TEST DATA - NOT CODE VALUES. Its
/// table TEST-HEADER-TABLE has columns at 30, 50 and 70 psf and made-up spans; member M1 is 6'-0" at
/// 30 psf and 4'-0" at 50 psf (width ≤ 20 ft), so the interpolation at 40 psf is 5'-0".
/// </summary>
public class FootnoteOperationTests
{
    private const string Pack = "us-zz-interp";
    private const string OverlayPath = "packs/us-zz-interp/amendments/test-header-table.json";
    private const string TablePath = "layers/zz-interp-2099/tables/test-header-table.json";

    // 1/1024" units: 6'-0" = 72 × 1024 = 73728; 4'-0" = 48 × 1024 = 49152; 5'-0" = 61440.
    private const long SixFeet = 73728;
    private const long FourFeet = 49152;

    private static HeaderRequest Request(Length span, int? snow, int? roofLive = null, Length? width = null)
        => new("test-roof", WallKind.ExteriorBearing, span, Fx.Site(snow, width ?? Fx.Ft(20), wind: null, roofLive: roofLive));

    private static HeaderResult Size(Length span, int? snow, int? roofLive = null, Length? width = null)
        => Fx.Size(Pack, Request(span, snow, roofLive, width));

    private static InMemoryPackSource Edit(string path, Action<JsonNode> edit)
    {
        InMemoryPackSource source = Fx.Source();
        JsonNode node = JsonNode.Parse(source.Text(path))!;
        edit(node);
        return source.With(path, node.ToJsonString());
    }

    private static JsonNode Operation(JsonNode overlay, int index) => overlay["operations"]![0]!["footnote"]!["operations"]![index]!;

    private static void Refused(IPackSource source, string fragment, string pack = Pack)
    {
        PackLoadResult.Invalid invalid = Fx.Invalid(PackLoader.Load(source, pack));
        Assert.True(
            invalid.Problems.Any(p => p.ToString().Contains(fragment, StringComparison.Ordinal)),
            $"expected a problem containing \"{fragment}\"; got:{Environment.NewLine}{invalid}");
    }

    // Loading
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "RUL-006")]
    public void The_overlay_amends_footnote_e_into_two_operations_cited_to_its_own_page()
    {
        LoadedPack pack = Fx.Load(Pack);
        Footnote e = Assert.Single(Assert.Single(pack.Tables).Footnotes);
        Assert.Equal(FootnoteEncoding.AsOperations, e.EncodedAs);
        Assert.StartsWith("SYNTHETIC footnote e (amended)", e.Text, StringComparison.Ordinal);
        Assert.Equal("p. 9", e.Source!.Location);
        Assert.Equal("zz-synthetic-interp-state", e.Source.SourceId);
        Assert.Equal(
            [
                new FootnoteOperation.SubstituteInput("groundSnowLoad", Psf(30), Psf(30), "roofLiveLoad", Psf(20)),
                new FootnoteOperation.Interpolate("groundSnowLoad", Psf(30), Psf(50), "headerSpan"),
            ],
            e.Operations);
        Assert.Empty(pack.Pending);
    }

    [Fact]
    [Trait("Feature", "RUL-006")]
    public void An_amendment_to_a_table_that_is_not_loaded_is_pending_and_the_pack_is_valid()
    {
        LoadedPack pack = Fx.Load("us-zz-pending");
        Assert.False(pack.HasHeaderTables);
        PendingAmendment pending = Assert.Single(pack.Pending);
        Assert.Equal(("TEST-HEADER-TABLE", "e", "p. 9"), (pending.Table, pending.FootnoteId, pending.Source.Location));
        Assert.Equal("packs/us-zz-pending/amendments/test-header-table.json", pending.File);
    }

    [Fact]
    [Trait("Feature", "RUL-001")]
    public void A_footnote_operation_the_text_cannot_support_is_refused_at_load()
    {
        Refused(Edit(OverlayPath, o => Operation(o, 1)["between"] = new JsonArray(30, 70)), "have a band between them; interpolation is only between adjacent columns");
        Refused(Edit(OverlayPath, o => Operation(o, 1)["between"] = new JsonArray(30, 40)), "have no 'groundSnowLoad' band at 40 psf");
        Refused(Edit(OverlayPath, o => Operation(o, 1)["between"] = new JsonArray(50, 30)), "50 psf is not below 30 psf");
        Refused(Edit(OverlayPath, o => Operation(o, 1)["between"] = new JsonArray(30)), "exactly two columns");
        Refused(Edit(OverlayPath, o => Operation(o, 1)["quantity"] = "jackStuds"), "counts never are");
        Refused(Edit(OverlayPath, o => Operation(o, 1)["input"] = "supports"), "is not a banded site input");
        Refused(Edit(OverlayPath, o => Operation(o, 1)["op"] = "extrapolate"), "unknown footnote operation 'extrapolate'");
        Refused(Edit(OverlayPath, o => Operation(o, 0)["use"] = 20), "never less demanding");
        Refused(Edit(OverlayPath, o => Operation(o, 0)["when"]!["input"] = "roofSlope"), "'roofSlope' is not an input a condition can test");
        Refused(Edit(OverlayPath, o => Operation(o, 0)["input"] = "ultimateWindSpeed"), "which is not an upper-bound column of this table");
        Refused(
            Edit(OverlayPath, o => o["operations"]![0]!["footnote"]!["operations"]!.AsArray().Add(Operation(o, 1).DeepClone())),
            "at most one 'interpolate' operation");
        Refused(Edit(OverlayPath, o => o["operations"]![0]!["footnote"]!["appliesTo"] = "rows"), "applies to the whole table");
        Refused(Edit(OverlayPath, o => o["operations"]![0]!["footnote"]!["encodedAs"] = "not-encoded"), "only an 'as-operations' footnote has operations");
        Refused(Edit(OverlayPath, o => o["operations"]![0]!["footnote"]!["id"] = "z"), "has no footnote 'z' in the layer below");
        Refused(Edit(OverlayPath, o => o["operations"]!.AsArray().Add(o["operations"]![0]!.DeepClone())), "two operations in one overlay amend footnote 'e'");
    }

    [Fact]
    [Trait("Feature", "RUL-001")]
    public void A_member_twice_in_one_cell_of_an_interpolated_column_is_refused()
    {
        InMemoryPackSource source = Edit(TablePath, t =>
        {
            JsonNode row = t["rows"]!.AsArray().Single(r => (string)r!["id"]! == "roof.s30.w20.m2")!;
            row["header"]!["plies"] = 1;
        });
        Refused(source, "give the same member in one cell, so interpolation cannot pair them");
    }

    [Fact]
    [Trait("Feature", "RUL-001")]
    public void Two_footnotes_interpolating_the_same_column_are_refused()
    {
        const string f = """
            { "id": "f", "text": "SYNTHETIC f", "encodedAs": "as-operations", "appliesTo": "table",
              "operations": [ { "op": "interpolate", "input": "groundSnowLoad", "between": [50, 70], "quantity": "headerSpan" } ] }
            """;
        InMemoryPackSource source = Edit(TablePath, t => t["footnotes"]!.AsArray().Add(JsonNode.Parse(f)));
        Refused(source, "both declare a 'interpolate' operation on 'groundSnowLoad'");
    }

    // Interpolation, strictly between the declared columns
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void At_40_psf_the_span_is_the_exact_linear_interpolation_between_the_30_and_50_rows()
    {
        // M1: 6'-0" at 30 psf, 4'-0" at 50 psf. Weight (40 − 30) / (50 − 30) = 1/2.
        // 73728 + (49152 − 73728) × 1/2 = 73728 − 12288 = 61440 units = 5'-0".
        HeaderResult.Sized s = Fx.Sized(Size(Fx.Ft(5), 40, roofLive: null));
        Assert.Equal(new MemberSpec(1, "2x13"), s.Header);
        InterpolationTrace i = s.Citation.Interpolation!;
        Assert.Equal(new ExactFraction(1, 2), i.Weight);
        Assert.Equal(ExactFraction.Whole(61440), i.SpanUnits);
        Assert.Equal(Fx.Ft(5), i.SpanShown);
        Assert.Equal(("roof.s30.w20.m1", "roof.s50.w20.m1"), (i.LowerRowId, i.UpperRowId));
        Assert.Equal((Fx.Ft(6), Fx.Ft(4)), (i.LowerSpan, i.UpperSpan));
        Assert.Equal((Psf(30), Psf(50), Psf(40)), (i.Lower, i.Upper, i.Input));
        Assert.Equal("roof.s50.w20.m1", s.Citation.RowId);
    }

    [Fact]
    [Trait("Feature", "RUL-002")]
    public void An_interpolated_citation_shows_both_rows_the_exact_weight_and_the_footnote_verbatim_with_its_page()
    {
        HeaderResult.Sized s = Fx.Sized(Size(Fx.Ft(5), 40));
        InterpolationTrace i = s.Citation.Interpolation!;
        Assert.Equal("e", i.Footnote.Id);
        Assert.StartsWith("SYNTHETIC footnote e (amended): use 30 psf snow below 30 psf", i.Footnote.Text, StringComparison.Ordinal);
        Assert.Equal("p. 9", i.Footnote.Source.Location);
        Assert.Equal("SYNTHETIC TEST SOURCE (interpolation amendments) - NOT A CODE", i.Footnote.Source.Title);
        Assert.Contains(s.Citation.Footnotes, f => f.Id == "e" && f.EncodedAs == FootnoteEncoding.AsOperations);
        Assert.Contains(new BandMatch("groundSnowLoad", "40 psf", "between ≤ 30 psf and ≤ 50 psf: interpolated by footnote e"), s.Citation.Trace);
        Assert.Equal(
            "Interpolated between the 30 psf row (roof.s30.w20.m1) and the 50 psf row (roof.s50.w20.m1) (ZZ INTERP TEST footnote e, p. 9)",
            i.Summary(s.Citation.Code));
        string working = i.ToString();
        Assert.Contains("row roof.s30.w20.m1 (6'-0\")", working, StringComparison.Ordinal);
        Assert.Contains("row roof.s50.w20.m1 (4'-0\")", working, StringComparison.Ordinal);
        Assert.Contains("weight (40 − 30) / (50 − 30) = 1/2", working, StringComparison.Ordinal);
        Assert.Contains("footnote e: \"SYNTHETIC footnote e (amended)", working, StringComparison.Ordinal);
        Assert.Contains("interpolated between rows roof.s30.w20.m1 and roof.s50.w20.m1", s.Citation.RowLabel, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void The_smallest_member_whose_interpolated_span_covers_the_opening_is_chosen()
    {
        // 1/1024" longer than M1's 5'-0" at 40 psf: M2 (9'-0" and 7'-0" → 8'-0").
        HeaderResult.Sized s = Fx.Sized(Size(new Length(61441), 40));
        Assert.Equal(new MemberSpec(2, "2x13"), s.Header);
        Assert.Equal(Fx.Ft(8), s.Citation.Interpolation!.SpanShown);

        // The plain 50 psf column would need M2 already at 5'-0"; interpolation permits M1.
        Assert.Equal(new MemberSpec(2, "2x13"), Fx.Sized(Size(Fx.Ft(5), 50)).Header);
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void Stud_counts_are_not_interpolated_the_larger_of_the_two_rows_is_used()
    {
        // M2 at width ≤ 20 ft: jacks 1 at 30 psf, 3 at 50 psf; kings 1 and 2. Interpolating would give 2 and 1 1/2.
        HeaderResult.Sized s = Fx.Sized(Size(Fx.Ft(6), 40));
        Assert.Equal(new MemberSpec(2, "2x13"), s.Header);
        Assert.Equal((3, 2), (s.JackStuds, s.KingStuds));
        Assert.Contains(new BandMatch("jackStuds", "1 and 3", "3: counts are not interpolated; the larger of the two rows is used"), s.Citation.Trace);
        Assert.Contains(new BandMatch("kingStuds", "1 and 2", "2: counts are not interpolated; the larger of the two rows is used"), s.Citation.Trace);
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void At_31_psf_the_span_is_an_exact_fraction_compared_without_rounding()
    {
        // Weight (31 − 30) / 20 = 1/20. M1: 73728 + (−24576) × 1/20 = 73728 − 1228.8 = 72499.2 = 362496/5 units.
        // Shown rounded down to 1/16" (64 units): ⌊72499.2 / 64⌋ = 1132 → 72448 units = 70 3/4" = 5'-10 3/4".
        HeaderResult.Sized fits = Fx.Sized(Size(new Length(72499), 31));
        Assert.Equal(new MemberSpec(1, "2x13"), fits.Header);
        Assert.Equal(new ExactFraction(1, 20), fits.Citation.Interpolation!.Weight);
        Assert.Equal(new ExactFraction(362496, 5), fits.Citation.Interpolation.SpanUnits);
        Assert.Equal(new Length(72448), fits.Citation.Interpolation.SpanShown);
        Assert.Equal("5'-10 3/4\"", CellValue.Of(fits.Citation.Interpolation.SpanShown).ToString());

        // 72500 > 72499.2: the exact comparison moves to M2, though the rounded display (72448) would already have refused M1 at 72449.
        Assert.Equal(new MemberSpec(2, "2x13"), Fx.Sized(Size(new Length(72500), 31)).Header);
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void At_49_psf_the_weight_is_19_20()
    {
        // 73728 − 24576 × 19/20 = 73728 − 23347.2 = 50380.8 = 251904/5; shown ⌊50380.8 / 64⌋ × 64 = 787 × 64 = 50368 = 4'-1 3/16".
        HeaderResult.Sized s = Fx.Sized(Size(Fx.Ft(4, 1), 49));
        Assert.Equal(new ExactFraction(19, 20), s.Citation.Interpolation!.Weight);
        Assert.Equal(new ExactFraction(251904, 5), s.Citation.Interpolation.SpanUnits);
        Assert.Equal("4'-1 3/16\"", CellValue.Of(s.Citation.Interpolation.SpanShown).ToString());
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void Half_psf_inputs_give_weights_1_40_and_39_40_with_exact_spans_rounded_down_for_display()
    {
        // Ground snow load is whole psf in a request, so 30.5 and 49.5 are checked on the arithmetic itself.
        // 30.5 = 61/2: weight (61/2 − 30) / 20 = 1/40. 73728 − 24576/40 = 73728 − 614.4 = 73113.6 = 365568/5 units = 71.4".
        ExactFraction low = SpanInterpolation.Weight(new ExactFraction(61, 2), 30, 50);
        Assert.Equal(new ExactFraction(1, 40), low);
        ExactFraction lowSpan = SpanInterpolation.Span(SixFeet, FourFeet, low);
        Assert.Equal(new ExactFraction(365568, 5), lowSpan);

        // Rounded down to 1/16": 71.4 × 16 = 1142.4 → 1142/16 = 71 3/8" = 5'-11 3/8" (73088 units); rounding up would give 71 7/16".
        Assert.Equal(new Length(73088), SpanInterpolation.Shown(lowSpan));
        Assert.Equal("5'-11 3/8\"", CellValue.Of(SpanInterpolation.Shown(lowSpan)).ToString());

        // 49.5 = 99/2: weight 39/40. 73728 − 24576 × 39/40 = 73728 − 23961.6 = 49766.4 = 248832/5 units = 48.6".
        ExactFraction high = SpanInterpolation.Weight(new ExactFraction(99, 2), 30, 50);
        Assert.Equal(new ExactFraction(39, 40), high);
        ExactFraction highSpan = SpanInterpolation.Span(SixFeet, FourFeet, high);
        Assert.Equal(new ExactFraction(248832, 5), highSpan);

        // 48.6 × 16 = 777.6 → 777/16 = 48 9/16" = 4'-0 9/16" (49728 units), never 48 5/8".
        Assert.Equal(new Length(49728), SpanInterpolation.Shown(highSpan));
        Assert.Equal("4'-0 9/16\"", CellValue.Of(SpanInterpolation.Shown(highSpan)).ToString());

        // The comparison is on the exact value: 49766 units fits, 49767 does not.
        Assert.True(SpanInterpolation.Covers(highSpan, new Length(49766)));
        Assert.False(SpanInterpolation.Covers(highSpan, new Length(49767)));
    }

    [Fact]
    public void Exact_fractions_reduce_compare_and_floor_exactly()
    {
        Assert.Equal(new ExactFraction(1, 2), new ExactFraction(-3, -6));
        Assert.Equal("-1/2", new ExactFraction(1, -2).ToString());
        Assert.Equal("3", ExactFraction.Whole(3).ToString());
        Assert.Equal(-1, new ExactFraction(-1, 2).Floor());
        Assert.Equal(-1, new ExactFraction(-2, 2).Floor());
        Assert.Equal(2, new ExactFraction(5, 2).Floor());
        Assert.True(new ExactFraction(1, 3) < new ExactFraction(1, 2));
        Assert.True(new ExactFraction(1, 2) > new ExactFraction(1, 3));
        Assert.True(new ExactFraction(1, 2) <= new ExactFraction(2, 4));
        Assert.True(new ExactFraction(2, 4) >= new ExactFraction(1, 2));
        Assert.Throws<DivideByZeroException>(() => new ExactFraction(1, 0));
        Assert.Throws<ArgumentException>(() => SpanInterpolation.Weight(ExactFraction.Whole(40), 50, 30));
        Assert.Equal(new Length(-64), SpanInterpolation.Shown(new ExactFraction(-1, 1)));
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void Other_banded_columns_narrow_both_rows_the_same_way()
    {
        // Width 30 ft → the ≤ 40 ft rows: M1 5'-0" at 30 psf and 3'-6" at 50 psf → 4'-3" at 40 psf.
        HeaderResult.Sized s = Fx.Sized(Size(Fx.Ft(4, 3), 40, width: Fx.Ft(30)));
        Assert.Equal(new MemberSpec(1, "2x13"), s.Header);
        Assert.Equal(("roof.s30.w40.m1", "roof.s50.w40.m1"), (s.Citation.Interpolation!.LowerRowId, s.Citation.Interpolation.UpperRowId));
    }

    [Fact]
    [Trait("Feature", "RUL-003")]
    public void An_opening_longer_than_every_interpolated_span_is_out_of_scope_with_the_working()
    {
        // M3 at 40 psf: 12'-0" and 10'-0" → 11'-0". 11'-1" is longer.
        HeaderResult.OutOfScope o = Fx.OutOfScope(Size(Fx.Ft(11, 1), 40));
        Assert.Equal(OutOfScopeReason.SpanExceedsTable, o.Reason);
        Assert.Equal("roof.s50.w20.m3", o.Limit.RowId);
        Assert.Equal(Fx.Ft(11), o.Limit.Interpolation!.SpanShown);
        Assert.Contains("interpolated by footnote e (11'-0\"", o.Explanation, StringComparison.Ordinal);
    }

    // At a declared column, or outside the pair: the plain table
    // -----------------------------------------------------------------------------------------

    [Theory]
    [Trait("Feature", "RUL-004")]
    [InlineData(30, 6, 0, "roof.s30.w20.m1")]
    [InlineData(50, 4, 0, "roof.s50.w20.m1")]
    [InlineData(60, 3, 0, "roof.s70.w20.m1")]
    [InlineData(60, 3, 1, "roof.s70.w20.m2")]
    [InlineData(70, 3, 0, "roof.s70.w20.m1")]
    public void At_a_declared_column_or_above_the_pair_the_plain_row_is_used(int snow, int feet, int inches, string row)
    {
        HeaderResult.Sized s = Fx.Sized(Size(Fx.Ft(feet, inches), snow));
        Assert.Equal(row, s.Citation.RowId);
        Assert.Null(s.Citation.Interpolation);
        Assert.DoesNotContain(s.Citation.Trace, m => m.Band.Contains("interpolated", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void Between_50_and_70_the_heavier_column_is_used_not_an_interpolation()
    {
        // M1: 4'-0" at 50, 3'-0" at 70. An interpolation at 60 would give 3'-6"; the text permits none, so ≤ 70 psf: M2.
        HeaderResult.Sized s = Fx.Sized(Size(Fx.Ft(3, 6), 60));
        Assert.Equal(new MemberSpec(2, "2x13"), s.Header);
        Assert.Equal(new BandMatch("groundSnowLoad", "60 psf", "≤ 70 psf"), s.Citation.Trace[1]);
    }

    // Below 30 psf: the substitution, only when the roof live load allows it
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void Below_30_psf_with_roof_live_load_20_the_snow_is_taken_as_30_citing_the_footnote()
    {
        HeaderResult.Sized s = Fx.Sized(Size(Fx.Ft(6), 20, roofLive: 20));
        Assert.Equal("roof.s30.w20.m1", s.Citation.RowId);
        Assert.Null(s.Citation.Interpolation);
        Assert.Equal(new BandMatch("groundSnowLoad", "20 psf", "taken as 30 psf by footnote e (roofLiveLoad 20 psf ≤ 20 psf)"), s.Citation.Trace[0]);
        Assert.Contains(s.Citation.Footnotes, f => f.Id == "e" && f.Source.Location == "p. 9");
    }

    [Fact]
    [Trait("Feature", "RUL-003")]
    public void Below_30_psf_with_roof_live_load_21_is_out_of_scope_citing_the_footnote()
    {
        HeaderResult.OutOfScope o = Fx.OutOfScope(Size(Fx.Ft(6), 20, roofLive: 21));
        Assert.Equal(OutOfScopeReason.NarrowedByFootnote, o.Reason);
        Assert.Null(o.Limit.RowId);
        Assert.Equal("footnote e", o.Limit.RowLabel);
        Assert.Contains("\"SYNTHETIC footnote e (amended)", o.Explanation, StringComparison.Ordinal);
        Assert.Contains("roofLiveLoad is 21 psf", o.Explanation, StringComparison.Ordinal);
        Assert.Contains(o.Limit.Footnotes, f => f.Id == "e");
    }

    [Fact]
    [Trait("Feature", "RUL-003")]
    public void Below_30_psf_without_a_roof_live_load_the_input_is_missing_never_assumed()
    {
        HeaderResult.InputMissing m = Assert.IsType<HeaderResult.InputMissing>(Size(Fx.Ft(6), 20, roofLive: null));
        Assert.Equal(["roofLiveLoad"], m.Inputs);
        Assert.Contains("footnote e", m.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(40)]
    [InlineData(60)]
    public void At_or_above_30_psf_the_roof_live_load_is_not_asked_for(int snow)
        => Assert.IsType<HeaderResult.Sized>(Size(Fx.Ft(2), snow, roofLive: null));

    [Fact]
    public void Evaluation_is_deterministic()
    {
        LoadedPack a = Fx.Load(Pack);
        LoadedPack b = Fx.Load(Pack);
        foreach (int snow in new[] { 20, 31, 40, 49, 50, 60 })
        {
            HeaderRequest request = Request(Fx.Ft(5), snow, roofLive: 20);
            Assert.Equal(RulesEngine.For(a).SizeHeader(request), RulesEngine.For(b).SizeHeader(request));
        }
    }

    private static CellValue Psf(int value) => CellValue.Whole(ColumnType.Psf, value);
}

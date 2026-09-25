using System.Reflection;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// Header sizing semantics over the synthetic fixtures (SYNTHETIC TEST DATA - NOT CODE VALUES).
/// Expected values are derived by hand from the synthetic spec: test-roof spans in inches are
/// m1 91, m2 151, m3 211, minus 12 per snow band (33, 66, 99 psf) and 6 per width band (22, 44 ft).
/// </summary>
public class EvaluatorTests
{
    private const string Base = "us-zz-base";

    [Fact]
    [Trait("Feature", "RUL-004")]
    public void A_request_inside_every_band_returns_that_rows_header_and_studs()
    {
        // snow 50 → ≤ 66 band; width 30 ft → ≤ 44 ft band; spans there: m1 73" (6'-1"), m2 133" (11'-1"), m3 193" (16'-1").
        HeaderResult.Sized sized = Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(9))));
        Assert.Equal(new MemberSpec(2, "2x9"), sized.Header);
        Assert.Equal(1, sized.JackStuds);
        Assert.Equal(2, sized.KingStuds);
        Assert.Equal("roof.s66.w44.m2", sized.Citation.RowId);
        Assert.Equal(
            [
                new BandMatch("supports", "test-roof", "= test-roof"),
                new BandMatch("groundSnowLoad", "50 psf", "≤ 66 psf"),
                new BandMatch("buildingWidth", "30'-0\"", "≤ 44'-0\""),
                new BandMatch("headerSpan", "9'-0\"", "≤ 11'-1\""),
            ],
            sized.Citation.Trace);
    }

    [Fact]
    [Trait("Feature", "RUL-002")]
    public void Every_sized_result_carries_a_complete_citation()
    {
        HeaderResult.Sized sized = Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(9))));
        Citation c = sized.Citation;
        Assert.Equal(new AdoptedCodeRef("us-zz-base", 1, "ZZ BASE TEST", "IRC 2099", ReviewStatus.Unreviewed), c.Code);
        Assert.Equal(Fx.Table, c.Table);
        Assert.Equal("roof.s66.w44.m2", c.RowId);
        Assert.Equal("supports = test-roof; groundSnowLoad ≤ 66 psf; buildingWidth ≤ 44'-0\"; headerSpan ≤ 11'-1\" → (2) 2x9, 1 jack, 2 king", c.RowLabel);
        Assert.Equal(CitationLayer.ModelCode, c.Layer);
        Assert.Equal(
            new SourceRef("zz-synthetic-base", "SYNTHETIC TEST SOURCE (base) - NOT A CODE", "synthetic p. 1, row 11", "https://example.invalid/synthetic",
                "synthetic printing, no errata", new DateOnly(2026, 9, 25), new string('0', 64)),
            c.Source);
        Assert.Equal(["a", "b", "c"], c.Footnotes.Select(f => f.Id));
        Assert.Equal(FootnoteEncoding.NotEncoded, c.Footnotes[0].EncodedAs);
        Assert.Contains("IRC 2099 Table TEST-HEADER-TABLE, as adopted by ZZ BASE TEST row roof.s66.w44.m2", c.ToString(), StringComparison.Ordinal);
        Assert.Contains("(2) 2x9, 1 jack, 2 king", sized.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(33, "roof.s33.w44.m1")] // on the 33 psf bound
    [InlineData(34, "roof.s66.w44.m1")] // just above it: the next, more demanding band
    [InlineData(32, "roof.s33.w44.m1")] // just below it
    [InlineData(0, "roof.s33.w44.m1")] // the domain's min, typed by the user
    [InlineData(99, "roof.s99.w44.m1")] // on the top bound
    public void Snow_band_edges_select_by_exact_comparison(int snow, string row)
    {
        HeaderResult.Sized sized = Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(4), snow: snow)));
        Assert.Equal(row, sized.Citation.RowId);
    }

    [Fact]
    public void Snow_just_above_the_top_band_is_out_of_scope_citing_the_limit_row()
    {
        HeaderResult.OutOfScope o = Fx.OutOfScope(Fx.Size(Base, Fx.Roof(Fx.Ft(4), snow: 100)));
        Assert.Equal(OutOfScopeReason.InputAboveTableBands, o.Reason);
        Assert.Equal("roof.s99.w22.m3", o.Limit.RowId); // the longest-span row of the top (99 psf) band
        Assert.Equal(new BandMatch("groundSnowLoad", "100 psf", "above the largest ≤ 99 psf"), o.Limit.Trace[^1]);
        Assert.Contains("groundSnowLoad 100 psf is above the largest band", o.Explanation, StringComparison.Ordinal);
        Assert.Contains("get an engineer", o.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Width_band_edges_select_by_exact_comparison()
    {
        Length onBound = Fx.Ft(22);
        Length justAbove = onBound + new Length(1);
        Length justBelow = onBound - new Length(1);
        Assert.Equal("roof.s66.w22.m1", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(4), width: onBound))).Citation.RowId);
        Assert.Equal("roof.s66.w22.m1", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(4), width: justBelow))).Citation.RowId);
        Assert.Equal("roof.s66.w44.m1", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(4), width: justAbove))).Citation.RowId);

        HeaderResult.OutOfScope o = Fx.OutOfScope(Fx.Size(Base, Fx.Roof(Fx.Ft(4), width: Fx.Ft(44) + new Length(1))));
        Assert.Equal(OutOfScopeReason.InputAboveTableBands, o.Reason);
        Assert.Equal("roof.s66.w44.m3", o.Limit.RowId);
        Assert.Equal(Fx.Ft(44), Length.FeetInches(44, 0));
        Assert.Equal("roof.s66.w44.m1", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(4), width: Fx.Ft(44)))).Citation.RowId);
    }

    [Fact]
    [Trait("Feature", "RUL-003")]
    public void Span_at_a_capacity_is_that_row_and_one_unit_over_is_the_next_or_out_of_scope()
    {
        // 66 psf / 44 ft: m1 6'-1", m2 11'-1", m3 16'-1".
        Assert.Equal("roof.s66.w44.m1", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(6, 1)))).Citation.RowId);
        Assert.Equal("roof.s66.w44.m1", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(6, 1) - new Length(1)))).Citation.RowId);
        Assert.Equal("roof.s66.w44.m2", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(6, 1) + new Length(1)))).Citation.RowId);
        Assert.Equal("roof.s66.w44.m3", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(16, 1)))).Citation.RowId);

        HeaderResult.OutOfScope o = Fx.OutOfScope(Fx.Size(Base, Fx.Roof(Fx.Ft(16, 1) + new Length(1))));
        Assert.Equal(OutOfScopeReason.SpanExceedsTable, o.Reason);
        Assert.Equal("roof.s66.w44.m3", o.Limit.RowId);
        Assert.Equal(CitationLayer.ModelCode, o.Limit.Layer);
        Assert.Equal(new BandMatch("headerSpan", "16'-1 1/1024\"", "above the largest ≤ 16'-1\""), o.Limit.Trace[^1]);
        Assert.Contains("longer than the longest span table TEST-HEADER-TABLE gives for these conditions (16'-1\", row roof.s66.w44.m3)", o.Explanation, StringComparison.Ordinal);
        Assert.Contains("out of prescriptive scope (SpanExceedsTable)", o.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Input_below_the_declared_domain_is_out_of_scope()
    {
        // The state overlay amends the width domain to start at 11 ft.
        HeaderResult.OutOfScope o = Fx.OutOfScope(Fx.Size("us-zz-state", Fx.Roof(Fx.Ft(4), width: Fx.Ft(11) - new Length(1))));
        Assert.Equal(OutOfScopeReason.InputBelowTableBands, o.Reason);
        Assert.Equal("roof.s66.w22.m1", o.Limit.RowId);
        Assert.Equal("roof.s66.w22.m1", Fx.Sized(Fx.Size("us-zz-state", Fx.Roof(Fx.Ft(4), width: Fx.Ft(11)))).Citation.RowId);

        // The same width is fine under the base pack, whose domain starts at 0.
        Assert.Equal("roof.s66.w22.m1", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(4), width: Fx.Ft(10)))).Citation.RowId);
    }

    [Fact]
    public void Span_below_a_capacity_domain_min_is_out_of_scope()
    {
        InMemoryPackSource source = Fx.Source();
        source.With(Fx.TablePath, source.Text(Fx.TablePath).Replace("\r\n", "\n", StringComparison.Ordinal).Replace(
            "\"domain\": {\n        \"min\": \"0in\",\n        \"max\": \"19ft 7in\"",
            "\"domain\": {\n        \"min\": \"1ft 0in\",\n        \"max\": \"19ft 7in\"",
            StringComparison.Ordinal));
        LoadedPack pack = Fx.Loaded(PackLoader.Load(source, Base));
        Assert.Equal(Fx.Ft(1).Units, pack.Tables[0].Inputs.Single(i => i.Name == "headerSpan").Domain!.Min.Magnitude);
        HeaderResult.OutOfScope o = Fx.OutOfScope(RulesEngine.For(pack).SizeHeader(Fx.Roof(Fx.Ft(0, 11))));
        Assert.Equal(OutOfScopeReason.InputBelowTableBands, o.Reason);
        Assert.Equal("roof.s66.w44.m1", o.Limit.RowId);
    }

    [Fact]
    public void A_supports_value_with_no_rows_is_condition_not_covered()
    {
        HeaderResult.OutOfScope o = Fx.OutOfScope(Fx.Size(Base, Fx.Roof(Fx.Ft(4), supports: "test-wall")));
        Assert.Equal(OutOfScopeReason.ConditionNotCovered, o.Reason);
        Assert.Null(o.Limit.RowId);
        Assert.Equal("supports is one of: test-roof, test-roof-floor", o.Limit.RowLabel);
        Assert.Equal("synthetic p. 1", o.Limit.Source.Location);
        Assert.Contains("no rows for supports 'test-wall'", o.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void An_as_limit_footnote_narrows_scope_and_is_cited()
    {
        // Table footnote c: not for wind above 199 mph.
        Assert.IsType<HeaderResult.Sized>(Fx.Size(Base, Fx.Roof(Fx.Ft(4), wind: 199)));
        HeaderResult.OutOfScope c = Fx.OutOfScope(Fx.Size(Base, Fx.Roof(Fx.Ft(4), wind: 200)));
        Assert.Equal(OutOfScopeReason.NarrowedByFootnote, c.Reason);
        Assert.Null(c.Limit.RowId);
        Assert.Equal("footnote c", c.Limit.RowLabel);
        Assert.Contains("Footnote c", c.Explanation, StringComparison.Ordinal);
        Assert.Contains("SYNTHETIC footnote c", c.Explanation, StringComparison.Ordinal);

        // Row footnote d on roof.s99.w44.m3: only for widths up to 40 ft.
        Assert.Equal("roof.s99.w44.m3", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(15), snow: 80, width: Fx.Ft(40)))).Citation.RowId);
        HeaderResult.OutOfScope d = Fx.OutOfScope(Fx.Size(Base, Fx.Roof(Fx.Ft(15), snow: 80, width: Fx.Ft(40) + new Length(1))));
        Assert.Equal(OutOfScopeReason.NarrowedByFootnote, d.Reason);
        Assert.Equal("roof.s99.w44.m3", d.Limit.RowId);
        Assert.Contains(d.Limit.Footnotes, f => f.Id == "d");

        // The footnote narrows only its own row: a shorter span at the same width picks m2 and is sized.
        Assert.Equal("roof.s99.w44.m2", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(10), snow: 80, width: Fx.Ft(41)))).Citation.RowId);
    }

    [Fact]
    public void An_equals_limit_excludes_a_category()
    {
        InMemoryPackSource source = Fx.Source();
        string table = source.Text(Fx.TablePath).Replace("\r\n", "\n", StringComparison.Ordinal).Replace(
            "\"input\": \"ultimateWindSpeed\",\n        \"above\": 199",
            "\"input\": \"supports\",\n        \"equals\": \"test-roof-floor\"",
            StringComparison.Ordinal);
        source.With(Fx.TablePath, table);
        LoadedPack pack = Fx.Loaded(PackLoader.Load(source, Base));
        HeaderResult.OutOfScope o = Fx.OutOfScope(RulesEngine.For(pack).SizeHeader(Fx.Roof(Fx.Ft(4), supports: "test-roof-floor")));
        Assert.Equal(OutOfScopeReason.NarrowedByFootnote, o.Reason);
        Assert.Contains("= test-roof-floor", o.Explanation, StringComparison.Ordinal);
        Assert.IsType<HeaderResult.Sized>(RulesEngine.For(pack).SizeHeader(Fx.Roof(Fx.Ft(4))));
    }

    [Fact]
    public void An_unset_hazard_is_input_missing_never_a_default()
    {
        HeaderResult.InputMissing m = Assert.IsType<HeaderResult.InputMissing>(Fx.Size(Base, Fx.Roof(Fx.Ft(4), snow: null)));
        Assert.Equal(["groundSnowLoad"], m.Inputs);
        Assert.Equal(Fx.Table, m.Table);
        Assert.Equal("us-zz-base", m.Code.PackId);
        Assert.Contains("never assumes a value", m.ToString(), StringComparison.Ordinal);

        SiteInputs nothing = new(null, null, null, null, null, null, null);
        HeaderResult.InputMissing all = Assert.IsType<HeaderResult.InputMissing>(
            Fx.Size(Base, new HeaderRequest("test-roof", WallKind.ExteriorBearing, Fx.Ft(4), nothing)));
        Assert.Equal(["groundSnowLoad", "buildingWidth", "ultimateWindSpeed"], all.Inputs);

        // Inputs the table does not use are not required: seismic and frost are unset throughout.
        Assert.IsType<HeaderResult.Sized>(Fx.Size(Base, Fx.Roof(Fx.Ft(4))));
    }

    [Fact]
    public void No_pack_or_no_table_is_an_honest_no_data()
    {
        HeaderResult.NoData none = Assert.IsType<HeaderResult.NoData>(RulesEngine.SizeHeader(null, Fx.Roof(Fx.Ft(4))));
        Assert.Equal(NoDataReason.NoPackSelected, none.Reason);
        Assert.Null(none.Code);
        Assert.Contains("No adopted code is selected", none.ToString(), StringComparison.Ordinal);

        HeaderResult.NoData empty = Assert.IsType<HeaderResult.NoData>(RulesEngine.SizeHeader(Fx.Load("us-zz-empty"), Fx.Roof(Fx.Ft(4))));
        Assert.Equal(NoDataReason.NoTableForWallKind, empty.Reason);
        Assert.Equal("us-zz-empty", empty.Code!.PackId);
        Assert.Contains("has no header table for exterior-bearing walls", empty.Explanation, StringComparison.Ordinal);

        HeaderRequest interior = new("test-roof", WallKind.InteriorBearing, Fx.Ft(4), Fx.Site());
        HeaderResult.NoData noInterior = Assert.IsType<HeaderResult.NoData>(Fx.Size(Base, interior));
        Assert.Equal(WallKind.InteriorBearing, noInterior.Kind);
    }

    [Fact]
    [Trait("Feature", "RUL-006")]
    public void Amended_added_and_deleted_rows_answer_by_layer()
    {
        // Amended: same inputs, the state's member, cited to the state amendment.
        HeaderResult.Sized amended = Fx.Sized(Fx.Size("us-zz-state", Fx.Roof(Fx.Ft(7), snow: 20, width: Fx.Ft(15))));
        Assert.Equal(new MemberSpec(1, "2x8"), amended.Header);
        Assert.Equal(CitationLayer.StateAmendment, amended.Citation.Layer);
        Assert.Equal("zz-synthetic-state", amended.Citation.Source.SourceId);
        Assert.Contains("ZZ STATE TEST state amendment to Table TEST-HEADER-TABLE", amended.Citation.ToString(), StringComparison.Ordinal);
        HeaderResult.Sized original = Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(7), snow: 20, width: Fx.Ft(15))));
        Assert.Equal(new MemberSpec(1, "2x7"), original.Header);
        Assert.Equal(CitationLayer.ModelCode, original.Citation.Layer);

        // Added: a span only the state's row reaches.
        HeaderResult.Sized added = Fx.Sized(Fx.Size("us-zz-state", Fx.Roof(Fx.Ft(19, 7), snow: 80)));
        Assert.Equal("roof.s99.w44.m4", added.Citation.RowId);
        Assert.Equal(OutOfScopeReason.SpanExceedsTable, Fx.OutOfScope(Fx.Size(Base, Fx.Roof(Fx.Ft(19, 7), snow: 80))).Reason);

        // Deleted: floor m2 (9'-9") is gone; the remaining rows answer, never the deleted values.
        HeaderResult.OutOfScope deleted = Fx.OutOfScope(Fx.Size("us-zz-state", Fx.Roof(Fx.Ft(9), snow: 80, supports: "test-roof-floor")));
        Assert.Equal(OutOfScopeReason.SpanExceedsTable, deleted.Reason);
        Assert.Equal("floor.s99.w44.m1", deleted.Limit.RowId);
        Assert.Equal("floor.s99.w44.m2", Fx.Sized(Fx.Size(Base, Fx.Roof(Fx.Ft(9), snow: 80, supports: "test-roof-floor"))).Citation.RowId);

        // Municipal: the town's row, cited as municipal.
        HeaderResult.Sized town = Fx.Sized(Fx.Size("us-zz-town", Fx.Roof(Fx.Ft(11), snow: 50, width: Fx.Ft(15))));
        Assert.Equal(new MemberSpec(2, "2x10"), town.Header);
        Assert.Equal(CitationLayer.MunicipalAmendment, town.Citation.Layer);
        Assert.Contains("municipal amendment", town.Citation.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Same_pack_same_request_same_result_by_value()
    {
        HeaderRequest request = Fx.Roof(Fx.Ft(9));
        HeaderResult a = Fx.Size("us-zz-state", request);
        HeaderResult b = RulesEngine.For(Fx.Load("us-zz-state")).SizeHeader(request);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(
            Fx.OutOfScope(Fx.Size(Base, Fx.Roof(Fx.Ft(40)))),
            Fx.OutOfScope(Fx.Size(Base, Fx.Roof(Fx.Ft(40)))));
    }

    [Fact]
    public void Invalid_requests_are_caller_bugs_not_results()
    {
        Assert.Throws<ArgumentException>(() => Fx.Roof(Length.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SiteInputs(-1, null, null, null, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SiteInputs(null, -1, null, null, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SiteInputs(null, null, null, -Fx.Ft(1), null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SiteInputs(null, null, null, null, Length.Zero, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SiteInputs(null, null, null, null, null, -1, null));
        Assert.Throws<ArgumentNullException>(() => RulesEngine.For(null!));
        Assert.Throws<ArgumentNullException>(() => RulesEngine.SizeHeader(null, null!));
        Assert.Throws<ArgumentNullException>(() => RulesEngine.For(Fx.Load(Base)).SizeHeader(null!));
        SiteInputs site = new(10, 20, "test-sdc", Fx.Ft(3), Fx.Ft(20), 20, new InputProvenance("synthetic", new DateOnly(2026, 9, 25)));
        Assert.Equal("test-sdc", site.SeismicDesignCategory);
        Assert.Equal(Fx.Ft(3), site.FrostDepth);
        Assert.Equal("synthetic", site.Provenance!.Text);
    }

    [Fact]
    public void Site_inputs_have_no_defaults_to_fall_back_on()
    {
        ConstructorInfo ctor = Assert.Single(typeof(SiteInputs).GetConstructors(), c => c.GetParameters().Length > 1);
        Assert.All(ctor.GetParameters(), p => Assert.False(p.HasDefaultValue, $"{p.Name} has a default value"));
        Assert.DoesNotContain(typeof(SiteInputs).GetConstructors(), c => c.GetParameters().Length == 0);
    }

    [Fact]
    [Trait("Feature", "RUL-003")]
    public void The_result_union_is_closed_with_exactly_four_named_members()
    {
        Type[] members = [.. typeof(HeaderResult).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(HeaderResult))).OrderBy(t => t.Name, StringComparer.Ordinal)];
        Assert.Equal(
            [typeof(HeaderResult.InputMissing), typeof(HeaderResult.NoData), typeof(HeaderResult.OutOfScope), typeof(HeaderResult.Sized)],
            members);
        Assert.All(members, t => Assert.True(t.IsSealed));
        // Only the private constructor and the compiler's copy constructor exist; closure is by
        // convention plus this test (design §3.2), since a record's copy constructor is protected.
        Assert.All(
            typeof(HeaderResult).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            c => Assert.True(c.IsPrivate || c.GetParameters().Select(p => p.ParameterType).SequenceEqual([typeof(HeaderResult)]), c.ToString()));

        Type[] loads = [.. typeof(PackLoadResult).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(PackLoadResult)))];
        Assert.Equal(2, loads.Length);
    }

    [Fact]
    public void No_floating_point_type_appears_anywhere_in_the_public_api()
    {
        Type[] floating = [typeof(double), typeof(float), typeof(decimal), typeof(double?), typeof(float?), typeof(decimal?)];
        foreach (Type type in typeof(HeaderResult).Assembly.GetExportedTypes())
        {
            foreach (PropertyInfo p in type.GetProperties())
            {
                Assert.DoesNotContain(p.PropertyType, floating);
            }

            foreach (MethodBase m in type.GetMethods().Cast<MethodBase>().Concat(type.GetConstructors()).Where(m => m.DeclaringType == type))
            {
                Assert.All(m.GetParameters(), p => Assert.DoesNotContain(p.ParameterType, floating));
                if (m is MethodInfo mi)
                {
                    Assert.DoesNotContain(mi.ReturnType, floating);
                }
            }
        }
    }

    [Fact]
    public void Header_requests_record_what_was_asked()
    {
        HeaderRequest r = Fx.Roof(Fx.Ft(9), snow: 50);
        Assert.Equal("test-roof", r.Supports);
        Assert.Equal(WallKind.ExteriorBearing, r.Kind);
        Assert.Equal(Fx.Ft(9), r.HeaderSpan);
        Assert.Equal(50, r.Site.GroundSnowLoadPsf);
        Assert.Equal(150, r.Site.UltimateWindSpeedMph);
        Assert.Equal(Fx.Ft(30), r.Site.BuildingWidth);
        Assert.Equal("(2) 2x9", new MemberSpec(2, "2x9").ToString());
        Assert.Equal("groundSnowLoad 50 psf → ≤ 66 psf", new BandMatch("groundSnowLoad", "50 psf", "≤ 66 psf").ToString());
        Assert.Equal(RulesEngine.For(Fx.Load(Base)).Code, Fx.Load(Base).Code);
    }
}

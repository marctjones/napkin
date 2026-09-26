using System.Text.Json;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// The real shipped Connecticut 2022 pack (packs/ at the repo root, copied to RealPacks/). Expected
/// values below were typed from Connecticut's document itself (2022 CSBC w/ Errata #1, read
/// 2026-09-25), never copied out of the pack file. The IRC base tables are not loaded, so the pack
/// is valid but sizes nothing.
/// </summary>
public class ConnecticutPackTests
{
    private static string Root => Path.Combine(AppContext.BaseDirectory, "RealPacks");

    private static JsonElement OverlayData()
    {
        string path = Path.Combine(Root, "packs", "us-ct-2022", "ct-overlay-data.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    private static LoadedPack Pack() => Fx.Loaded(PackLoader.Load(Root, "us-ct-2022"));

    [Fact]
    public void The_real_pack_loads_valid_with_its_identity()
    {
        LoadedPack pack = Pack();
        Assert.Equal("us-ct-2022", pack.Manifest.Id);
        Assert.Equal("Connecticut State Building Code 2022", pack.Manifest.Adoption.Name);
        Assert.Equal("IRC 2021", pack.Manifest.BaseCode.ToString());
        Assert.Equal(AppliesTo.PermitApplicationDate, pack.Manifest.Adoption.AppliesTo);
        Assert.Equal(new DateOnly(2022, 10, 1), pack.Manifest.Adoption.InForceFrom);
        Assert.Equal("w/ Errata #1, ED: October 1, 2022", pack.Manifest.Sources[0].Printing);
        Assert.Equal(ReviewStatus.Unreviewed, pack.Manifest.Review.Status);
    }

    [Fact]
    public void The_catalog_lists_it_with_the_base_tables_not_loaded_status()
    {
        LoadedPack pack = Assert.IsType<PackLoadResult.Loaded>(Assert.Single(PackCatalog.Discover(Root))).Pack;
        Assert.Equal("us-ct-2022", pack.Manifest.Id);
        Assert.False(pack.HasHeaderTables);
        Assert.Equal("base tables not loaded", pack.StatusLabel);
        Assert.Equal(string.Empty, Fx.Load("us-zz-state").StatusLabel);
    }

    [Fact]
    public void Sizing_a_header_with_it_is_honestly_no_data()
    {
        HeaderResult result = RulesEngine.SizeHeader(Pack(), Fx.Roof(Fx.Ft(4)));
        HeaderResult.NoData none = Assert.IsType<HeaderResult.NoData>(result);
        Assert.Equal(NoDataReason.NoTableForWallKind, none.Reason);
        Assert.Contains("CT 2022", none.Explanation, StringComparison.Ordinal);
        Assert.Contains("your copy of the code", none.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void The_site_criteria_match_connecticuts_table_R301_2_on_page_131()
    {
        JsonElement entries = OverlayData().GetProperty("siteCriteria").GetProperty("entries");
        Assert.Equal("B", entries.GetProperty("seismicDesignCategory").GetProperty("value").GetString());
        Assert.Equal("3ft 6in", entries.GetProperty("frostLineDepth").GetProperty("value").GetString());
        Assert.Equal(JsonValueKind.Null, entries.GetProperty("groundSnowLoad").GetProperty("value").ValueKind);
        Assert.Equal("As set forth in Appendix AY.", entries.GetProperty("groundSnowLoad").GetProperty("printed").GetString());
        Assert.Equal("As set forth in Appendix AY.", entries.GetProperty("ultimateWindSpeed").GetProperty("printed").GetString());
        Assert.Contains("p. 131", OverlayData().GetProperty("siteCriteria").GetProperty("location").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_frost_depth_is_printed_as_42_inches()
    {
        JsonElement frost = OverlayData().GetProperty("siteCriteria").GetProperty("entries").GetProperty("frostLineDepth");
        Assert.Equal("42\"", frost.GetProperty("printed").GetString());
    }

    [Fact]
    public void The_R602_7_amendments_are_verbatim_cited_and_encoded_as_operations()
    {
        JsonElement amendments = OverlayData().GetProperty("amendments");
        Assert.Equal(2, amendments.GetArrayLength());
        foreach (JsonElement a in amendments.EnumerateArray())
        {
            Assert.Equal("as-operations", a.GetProperty("classification").GetString());
            Assert.Contains("p. 145", a.GetProperty("location").GetString(), StringComparison.Ordinal);
            Assert.Contains(
                "Use 30 psf ground snow load for cases in which ground snow load is less than 30 psf and the roof live load is equal to or less than 20 psf. For ground snow loads between 30 and 50 psf, linear interpolation is permitted.",
                a.GetProperty("text").GetString(),
                StringComparison.Ordinal);
        }

        Assert.Equal("Table R602.7(1), Footnote e", amendments[0].GetProperty("amends").GetString());
        Assert.Equal("Table R602.7(3), Footnote b", amendments[1].GetProperty("amends").GetString());
        Assert.StartsWith("Tabulated values assume #2 grade lumber, wet service and incising for refractory species.", amendments[1].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-006")]
    public void The_footnote_amendments_are_pending_until_the_base_tables_load_and_match_the_recorded_text()
    {
        LoadedPack pack = Pack();
        Assert.Equal(
            [("R602.7(1)", "e"), ("R602.7(3)", "b")],
            pack.Pending.Select(p => (p.Table, p.FootnoteId)).OrderBy(p => p.Table, StringComparer.Ordinal));
        Assert.All(pack.Pending, p => Assert.Equal(("ct-csbc-2022", "p. 145"), (p.Source.SourceId, p.Source.Location)));

        // The encoded footnote text is the text recorded verbatim from p. 145.
        JsonElement amendments = OverlayData().GetProperty("amendments");
        foreach (JsonElement a in amendments.EnumerateArray())
        {
            string file = Path.Combine(Root, "packs", "us-ct-2022", a.GetProperty("encodedIn").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            JsonElement footnote = JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("operations")[0].GetProperty("footnote");
            Assert.Equal(a.GetProperty("text").GetString(), footnote.GetProperty("text").GetString());
        }
    }

    [Fact]
    [Trait("Feature", "RUL-006")]
    public void Once_a_base_table_is_added_connecticuts_footnote_e_interpolates_it()
    {
        // A SYNTHETIC base table (the interpolation fixture's rows, SYNTHETIC TEST DATA - NOT CODE VALUES) named
        // R602.7(1), dropped into the real pack's empty base layer, in memory only. Connecticut's own overlay then applies.
        InMemoryPackSource fixtures = Fx.Source();
        string synthetic = fixtures.Text("layers/zz-interp-2099/tables/test-header-table.json")
            .Replace("\"TEST-HEADER-TABLE\"", "\"R602.7(1)\"", StringComparison.Ordinal)
            .Replace("\"zz-synthetic-interp-base\"", "\"ct-csbc-2022\"", StringComparison.Ordinal);
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Root).With("layers/irc-2021/tables/r602.7-1.json", synthetic);
        LoadedPack pack = Fx.Loaded(PackLoader.Load(source, "us-ct-2022"));
        Assert.Equal(("R602.7(3)", "b"), (Assert.Single(pack.Pending).Table, pack.Pending[0].FootnoteId));

        HeaderRequest request = new("test-roof", WallKind.ExteriorBearing, Fx.Ft(5), Fx.Site(40, Fx.Ft(20), wind: null));
        HeaderResult.Sized s = Fx.Sized(RulesEngine.SizeHeader(pack, request));
        InterpolationTrace i = s.Citation.Interpolation!;
        Assert.Equal(new ExactFraction(1, 2), i.Weight);
        Assert.Equal("p. 145", i.Footnote.Source.Location);
        Assert.Equal(OverlayData().GetProperty("amendments")[0].GetProperty("text").GetString(), i.Footnote.Text);
        Assert.Equal(
            "Interpolated between the 30 psf row (roof.s30.w20.m1) and the 50 psf row (roof.s50.w20.m1) (CT 2022 footnote e, p. 145)",
            i.Summary(s.Citation.Code));

        HeaderRequest light = new("test-roof", WallKind.ExteriorBearing, Fx.Ft(5), Fx.Site(25, Fx.Ft(20), wind: null, roofLive: 25));
        Assert.Equal(OutOfScopeReason.NarrowedByFootnote, Fx.OutOfScope(RulesEngine.SizeHeader(pack, light)).Reason);
    }

    [Fact]
    public void Appendix_AY_is_recorded_as_transcribed_into_the_site_values_file()
    {
        JsonElement ay = OverlayData().GetProperty("appendixAy");
        Assert.True(ay.GetProperty("valuesTranscribed").GetBoolean());
        Assert.Contains("pp. 157-160", ay.GetProperty("location").GetString(), StringComparison.Ordinal);
        Assert.Contains("site-values.json", ay.GetProperty("why").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Rows typed from the rendered pages 157-160 of Connecticut's document (read 2026-09-26), never
    /// from the pack file: the first and last towns, a town on each page, the coastal maximum, the
    /// three rows whose printed Vasd (101) does not follow from their Vult, and the misspelt town as
    /// printed.
    /// </summary>
    [Theory]
    [InlineData("Andover", 120, 93, 30, true, 157)]
    [InlineData("Barkamsted", 115, 89, 35, false, 157)]
    [InlineData("Bloomfield", 120, 93, 30, true, 157)]
    [InlineData("Canaan", 115, 89, 40, false, 157)]
    [InlineData("Groton", 128, 99, 30, true, 158)]
    [InlineData("Hartford", 120, 93, 30, true, 158)]
    [InlineData("Ledyard", 126, 101, 30, true, 158)]
    [InlineData("North Stonington", 127, 101, 30, true, 159)]
    [InlineData("Stonington", 129, 100, 30, true, 159)]
    [InlineData("Voluntown", 125, 101, 30, true, 160)]
    [InlineData("Woodstock", 120, 93, 40, true, 160)]
    public void Appendix_AY_rows_match_connecticuts_pages(string town, int vult, int vasd, int pg, bool hurricane, int page)
    {
        MunicipalitySite site = Pack().Site!.Find(town)!;

        Assert.Equal((town, vult, vasd, pg, hurricane), (site.Name, site.UltimateWindSpeedMph, site.NominalWindSpeedMph, site.GroundSnowLoadPsf, site.HurricaneProne));
        Assert.Equal($"Appendix AY, p. {page} (footer 'Page - {page}')", site.Source.Location);
        Assert.Equal("ct-csbc-2022", site.Source.SourceId);
        Assert.Equal("1325ee87f4bc5a0adbe6013dd6ef2def79229bcd189ea59388d1e03899618947", site.Source.Sha256);
    }

    [Fact]
    public void Appendix_AY_has_every_one_of_its_169_towns_in_the_order_printed_and_the_statewide_seismic_category()
    {
        SiteValuesTable site = Pack().Site!;

        Assert.Equal(169, site.Municipalities.Count);
        Assert.Equal("Andover", site.Municipalities[0].Name);
        Assert.Equal("Woodstock", site.Municipalities[^1].Name);
        Assert.Equal([.. site.Municipalities.Select(town => town.Name).Order(StringComparer.Ordinal)], site.Municipalities.Select(town => town.Name));
        Assert.Equal("B", site.SeismicDesignCategory);
        Assert.Contains("p. 131", site.SeismicSource!.Location, StringComparison.Ordinal);
        Assert.Null(site.Find("Springfield"));
        Assert.Equal("Bloomfield", site.Find("bloomfield")!.Name);
    }

    [Fact]
    public void Pack_locations_are_beside_the_executable_and_in_the_user_config_directory()
    {
        string none(string _) => string.Empty;
        string @base = Path.Combine("app", "bin");
        string home = Path.Combine("home", "u");
        Assert.Equal(
            [Path.Combine(@base, "packs"), Path.Combine(home, ".config", "napkin", "packs")],
            PackLocations.All(@base, PackLocations.Platform.Other, none, home));
        Assert.Equal(
            Path.Combine("x", "napkin", "packs"),
            PackLocations.All(@base, PackLocations.Platform.Other, k => k == "XDG_CONFIG_HOME" ? "x" : null, home)[1]);
        Assert.Equal(
            Path.Combine(home, "Library", "Application Support", "napkin", "packs"),
            PackLocations.All(@base, PackLocations.Platform.MacOS, none, home)[1]);
        Assert.Equal(
            Path.Combine("ad", "napkin", "packs"),
            PackLocations.All(@base, PackLocations.Platform.Windows, k => k == "APPDATA" ? "ad" : null, home)[1]);
        Assert.Equal(
            Path.Combine(home, "AppData", "Roaming", "napkin", "packs"),
            PackLocations.All(@base, PackLocations.Platform.Windows, none, home)[1]);
        Assert.Equal(2, PackLocations.All().Count);
    }
}

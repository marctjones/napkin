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
    public void The_R602_7_amendments_are_verbatim_cited_and_not_encoded()
    {
        JsonElement amendments = OverlayData().GetProperty("amendments");
        Assert.Equal(2, amendments.GetArrayLength());
        foreach (JsonElement a in amendments.EnumerateArray())
        {
            Assert.Equal("not-encoded", a.GetProperty("classification").GetString());
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
    public void Appendix_AY_is_recorded_as_a_location_only_with_no_values()
    {
        JsonElement ay = OverlayData().GetProperty("appendixAy");
        Assert.False(ay.GetProperty("valuesTranscribed").GetBoolean());
        Assert.Contains("pp. 157-160", ay.GetProperty("location").GetString(), StringComparison.Ordinal);
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

using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The town offer (#210) on the real Connecticut pack: Bloomfield's values as Connecticut's
/// Appendix AY prints them on p. 157 (120 mph, 30 psf; read from the page 2026-09-26) and the
/// statewide seismic category B of Table R301.2, p. 131.
/// </summary>
public class SiteOfferTests
{
    static readonly LoadedPack Connecticut = Assert.IsType<PackLoadResult.Loaded>(
        PackLoader.Load(Path.Combine(AppContext.BaseDirectory, "RealPacks"), "us-ct-2022")).Pack;

    static readonly MunicipalitySite Bloomfield = Connecticut.Site!.Find("Bloomfield")!;

    [Fact]
    public void The_offer_says_the_values_and_where_they_are_printed()
    {
        Assert.Equal(
            "Bloomfield, as CT 2022 prints it: ground snow load 30 psf and ultimate wind speed 120 mph (Appendix AY, p. 157 (footer 'Page - 157')); "
            + "seismic design category B statewide (Table R301.2 CLIMATIC AND GEOGRAPHIC DESIGN CRITERIA (Amd), p. 131 (footer 'Page - 131'); Appendix AY has no seismic column).",
            SiteOffer.Text("CT 2022", Connecticut.Site!, Bloomfield));
    }

    [Fact]
    public void Accepting_sets_snow_wind_and_seismic_keeps_the_rest_and_records_the_source()
    {
        SiteValues typed = SiteValues.NotEntered with { FrostDepth = Length.Inches(42), GroundSnowLoadPsf = 50, RoofLiveLoadPsf = 20 };

        SiteValues accepted = SiteOffer.Accept(typed, "CT 2022", Connecticut.Site!, Bloomfield, new DateOnly(2026, 9, 26));

        Assert.Equal((30, 120, "B"), (accepted.GroundSnowLoadPsf, accepted.UltimateWindSpeedMph, accepted.SeismicDesignCategory));
        Assert.Equal((Length.Inches(42), 20), (accepted.FrostDepth!.Value, accepted.RoofLiveLoadPsf!.Value));
        Assert.Null(accepted.BuildingWidth);
        Assert.Equal(new DateOnly(2026, 9, 26), accepted.Source!.On);
        Assert.StartsWith("CT 2022, Bloomfield: Appendix AY, p. 157", accepted.Source.Text, StringComparison.Ordinal);
        Assert.Contains("; seismic design category, Table R301.2", accepted.Source.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pack_that_prints_no_statewide_category_offers_none_and_keeps_what_was_typed()
    {
        SiteValuesTable noSeismic = Connecticut.Site! with { SeismicDesignCategory = null, SeismicSource = null };

        Assert.DoesNotContain("seismic", SiteOffer.Text("CT 2022", noSeismic, Bloomfield), StringComparison.Ordinal);
        SiteValues accepted = SiteOffer.Accept(SiteValues.NotEntered with { SeismicDesignCategory = "C" }, "CT 2022", noSeismic, Bloomfield, new DateOnly(2026, 9, 26));
        Assert.Equal("C", accepted.SeismicDesignCategory);
        Assert.Equal("CT 2022, Bloomfield: Appendix AY, p. 157 (footer 'Page - 157')", accepted.Source!.Text);
    }
}

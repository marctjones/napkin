using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building;

/// <summary>
/// The site values an adopted code publishes for a town, offered to the person with their citation
/// (#210). An offer is only words and a button: nothing is set until the person accepts it, and what
/// they accept is written into the site values with where it came from, like a value they typed.
/// </summary>
public static class SiteOffer
{
    /// <summary>
    /// The offer in a sentence: "Bloomfield, as CT 2022 prints it: ground snow load 30 psf and ultimate
    /// wind speed 120 mph (Appendix AY, p. 157 …); seismic design category B statewide (Table R301.2 …)."
    /// </summary>
    /// <param name="pack">The adopted code's pack.</param>
    /// <param name="town">The town.</param>
    public static string Text(LoadedPack pack, MunicipalitySite town)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(town);

        string seismic = pack.Site is { SeismicDesignCategory: { } category, SeismicSource: { } at }
            ? $"; seismic design category {category} statewide ({at.Location})"
            : string.Empty;
        return $"{town.Name}, as {pack.Manifest.Adoption.ShortName} prints it: ground snow load {town.GroundSnowLoadPsf} psf and "
               + $"ultimate wind speed {town.UltimateWindSpeedMph} mph ({town.Source.Location}){seismic}.";
    }

    /// <summary>
    /// The site values once the offer is accepted: the town's snow load and wind speed and the
    /// statewide seismic category replace what was there, every other value is kept, and the source
    /// names the code, the town and the pages, dated today.
    /// </summary>
    /// <param name="current">The site values now.</param>
    /// <param name="pack">The adopted code's pack.</param>
    /// <param name="town">The town.</param>
    /// <param name="today">The date the offer was accepted.</param>
    public static SiteValues Accept(SiteValues current, LoadedPack pack, MunicipalitySite town, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(town);

        string? category = pack.Site?.SeismicDesignCategory;
        string seismicAt = pack.Site?.SeismicSource is { } at ? $"; seismic design category, {at.Location}" : string.Empty;
        return current with
        {
            GroundSnowLoadPsf = town.GroundSnowLoadPsf,
            UltimateWindSpeedMph = town.UltimateWindSpeedMph,
            SeismicDesignCategory = category ?? current.SeismicDesignCategory,
            Source = new SiteSource($"{pack.Manifest.Adoption.ShortName}, {town.Name}: {town.Source.Location}{seismicAt}", today),
        };
    }
}

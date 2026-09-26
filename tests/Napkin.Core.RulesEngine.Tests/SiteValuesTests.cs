namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// A pack's per-municipality site values (#210) are read as strictly as every other pack file: each
/// malformed file below is refused naming what is wrong, and a pack without the file has none.
/// </summary>
public class SiteValuesTests
{
    private const string File = "packs/us-ct-2022/site-values.json";

    private static InMemoryPackSource Real() => InMemoryPackSource.FromDirectory(Path.Combine(AppContext.BaseDirectory, "RealPacks"));

    private static string Minimal(string town = """{ "name": "Town", "ultimateWindSpeedMph": 120, "nominalWindSpeedMph": 93, "groundSnowLoadPsf": 30, "hurricaneProne": true, "location": "p. 1" }""", string extra = "", string statewide = """{ "seismicDesignCategory": { "value": "B", "location": "p. 2" } }""", string source = "ct-csbc-2022", string kind = "site-values")
        => $$"""
            { "schemaVersion": 1, "kind": "{{kind}}", "source": "{{source}}", "title": "T"{{extra}},
              "statewide": {{statewide}},
              "municipalities": [ {{town}} ] }
            """;

    private static string Refusal(string text)
    {
        PackLoadResult.Invalid invalid = Fx.Invalid(PackLoader.Load(Real().With(File, text), "us-ct-2022"));
        return string.Join("\n", invalid.Problems.Select(problem => problem.Message));
    }

    [Fact]
    public void A_minimal_file_loads_with_its_citation_resolved_from_the_manifest()
    {
        LoadedPack pack = Fx.Loaded(PackLoader.Load(Real().With(File, Minimal()), "us-ct-2022"));

        MunicipalitySite town = Assert.Single(pack.Site!.Municipalities);
        Assert.Equal("p. 1", town.Source.Location);
        Assert.Equal("p. 2", pack.Site.SeismicSource!.Location);
        Assert.Equal("T", pack.Site.Title);
    }

    [Fact]
    public void A_pack_without_the_file_has_no_site_values_and_no_statewide_category_is_allowed()
    {
        Assert.Null(Fx.Loaded(PackLoader.Load(Real().Without(File), "us-ct-2022")).Site);

        SiteValuesTable site = Fx.Loaded(PackLoader.Load(Real().With(File, Minimal(statewide: "{}")), "us-ct-2022")).Site!;
        Assert.Null(site.SeismicDesignCategory);
        Assert.Null(site.SeismicSource);
    }

    [Theory]
    [InlineData("kind", "is 'site-values', not 'tables'")]
    [InlineData("source", "nowhere")]
    [InlineData("unknown", "surprise: unknown field")]
    [InlineData("zero", "groundSnowLoadPsf")]
    [InlineData("bool", "hurricaneProne: must be true or false")]
    [InlineData("twice", "'TOWN' is listed twice")]
    [InlineData("row", "must be a JSON object")]
    [InlineData("empty", "municipalities: must have at least 1 item(s)")]
    [InlineData("statewide", "statewide.seismicDesignCategory.note: unknown field")]
    public void A_malformed_file_is_refused_naming_the_problem(string fault, string message)
    {
        const string town = """{ "name": "Town", "ultimateWindSpeedMph": 120, "nominalWindSpeedMph": 93, "groundSnowLoadPsf": 30, "hurricaneProne": true, "location": "p. 1" }""";
        string text = fault switch
        {
            "kind" => Minimal(kind: "tables"),
            "source" => Minimal(source: "nowhere"),
            "unknown" => Minimal(extra: ", \"surprise\": 1"),
            "zero" => Minimal(town.Replace("\"groundSnowLoadPsf\": 30", "\"groundSnowLoadPsf\": 0", StringComparison.Ordinal)),
            "bool" => Minimal(town.Replace("true", "\"Yes\"", StringComparison.Ordinal)),
            "twice" => Minimal($"{town}, {town.Replace("Town", "TOWN", StringComparison.Ordinal)}"),
            "row" => Minimal("42"),
            "empty" => Minimal(string.Empty),
            _ => Minimal(statewide: """{ "seismicDesignCategory": { "value": "B", "location": "p. 2", "note": 1 } }"""),
        };

        Assert.Contains(message, Refusal(text), StringComparison.Ordinal);
    }
}

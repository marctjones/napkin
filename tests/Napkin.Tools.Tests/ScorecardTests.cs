using Napkin.Tools.Features;

namespace Napkin.Tools.Tests;

/// <summary>
/// End to end over the probe run: the same source the fixture TRX was produced from, joined to
/// that TRX. This is the proof that reading traits from source and outcomes from TRX agree.
/// </summary>
public class ScorecardTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, FeatureStatus.NotStarted)]
    [InlineData(1, 0, 1, 0, FeatureStatus.Planned)]
    [InlineData(2, 1, 1, 0, FeatureStatus.Partial)]
    [InlineData(2, 2, 0, 0, FeatureStatus.Passing)]
    [InlineData(3, 2, 0, 1, FeatureStatus.Failing)]
    [InlineData(3, 0, 2, 1, FeatureStatus.Failing)]
    public void TheStatusRules(int tests, int passed, int skipped, int failed, FeatureStatus expected) =>
        Assert.Equal(expected, Scorecard.Classify(tests, passed, skipped, failed));

    [Fact]
    public void JudgesEveryCataloguedFeatureFromTheProbeRun()
    {
        var scorecard = BuildFromProbe();

        Assert.Equal(FeatureStatus.Passing, StatusOf(scorecard, "GEO-LEN-001"));
        Assert.Equal(FeatureStatus.Planned, StatusOf(scorecard, "GEO-PT-001"));
        Assert.Equal(FeatureStatus.NotStarted, StatusOf(scorecard, "DECK-SPAN-001"));

        // Two passing tests and one skipped test carry the class-level id.
        Assert.Equal(FeatureStatus.Partial, StatusOf(scorecard, "CLS-LEVEL-001"));
    }

    [Fact]
    public void ListsFeatureIdsThatTestsClaimButTheCatalogDoesNotDefine()
    {
        var scorecard = BuildFromProbe();

        Assert.Equal(
            ["GEO-LEN-002", "GEO-LEN-003", "GEO-NEST-001"],
            scorecard.Orphans.Select(orphan => orphan.FeatureId).ToArray());
        Assert.Equal(
            "TraitProbe.Probe+Nested.Inner",
            Assert.Single(scorecard.Orphans.Single(orphan => orphan.FeatureId == "GEO-NEST-001").TestIds));
    }

    [Fact]
    public void RollsUpByMilestoneInMilestoneOrder()
    {
        var scorecard = BuildFromProbe();

        Assert.Equal(["M1", "M2", "M3"], scorecard.ByMilestone.Select(rollup => rollup.Key).ToArray());

        var m1 = scorecard.ByMilestone[0];
        Assert.Equal(2, m1.Total);
        Assert.Equal(1, m1.Of(FeatureStatus.Passing));
        Assert.Equal(1, m1.Of(FeatureStatus.Partial));
        Assert.Equal(50.0, m1.PassingPercent);
    }

    [Fact]
    public void RollsUpByArea()
    {
        var scorecard = BuildFromProbe();

        var decks = scorecard.ByArea.Single(rollup => rollup.Key == "decks");
        Assert.Equal(1, decks.Of(FeatureStatus.NotStarted));
        Assert.Equal(3, scorecard.ByArea.Single(rollup => rollup.Key == "geometry").Total);
    }

    [Fact]
    public void AFailingTestMakesItsFeatureFailing()
    {
        using var scratch = Fixture.NewDirectory();
        var trx = scratch.Write("run.trx", FailingRun());
        var catalog = new Catalog
        {
            Features = [new Feature { Id = "GEO-LEN-001", Area = "geometry", Milestone = "M1" }],
        };
        var claims = new[]
        {
            new FeatureClaim("Napkin.Sample.Thing", "Works", "GEO-LEN-001", "Thing.cs", 1),
        };

        var scorecard = Scorecard.Build(catalog, claims, TestResults.Load([trx]), []);

        Assert.Equal(FeatureStatus.Failing, scorecard.Rows[0].Status);
    }

    [Fact]
    public void TheMarkdownNamesEveryFeatureAndItsStatus()
    {
        var markdown = ScorecardWriter.ToMarkdown(BuildFromProbe());

        Assert.Contains("## Feature scorecard", markdown, StringComparison.Ordinal);
        Assert.Contains("`DECK-SPAN-001`", markdown, StringComparison.Ordinal);
        Assert.Contains("Not started", markdown, StringComparison.Ordinal);
        Assert.Contains("never gates", markdown, StringComparison.Ordinal);
        Assert.Contains("absent from the catalog", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void TheJsonCarriesTheSameCountsAsTheRollups()
    {
        var scorecard = BuildFromProbe();

        var json = ScorecardWriter.ToJson(scorecard);

        Assert.Contains("\"id\": \"GEO-LEN-001\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"Passing\"", json, StringComparison.Ordinal);
        Assert.Contains("\"orphans\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyCatalogRendersAnHonestlyEmptyReport()
    {
        var markdown = ScorecardWriter.ToMarkdown(
            Scorecard.Build(new Catalog(), [], TestResults.Load([]), []));

        Assert.Contains("No features are catalogued yet", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ACatalogIsMergedFromEveryJsonFileInTheDirectory()
    {
        var catalog = Catalog.Load(Fixture.Path("catalog"), out var warnings);

        Assert.Equal(4, catalog.Features.Count);
        Assert.Empty(warnings);

        // Sorted by id, so the report is stable whatever order the files are read in.
        Assert.Equal(
            ["CLS-LEVEL-001", "DECK-SPAN-001", "GEO-LEN-001", "GEO-PT-001"],
            catalog.Features.Select(feature => feature.Id).ToArray());
    }

    [Fact]
    public void ADuplicateIdIsAWarningRatherThanAFailure()
    {
        using var scratch = Fixture.NewDirectory();
        scratch.Write("a.json", One("GEO-LEN-001"));
        scratch.Write("b.json", One("GEO-LEN-001"));

        var catalog = Catalog.Load(scratch.Path, out var warnings);

        Assert.Single(catalog.Features);
        Assert.Contains("already defined", Assert.Single(warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingCatalogDirectoryIsAnEmptyCatalog()
    {
        var catalog = Catalog.Load(
            Path.Combine(Path.GetTempPath(), "no-such-catalog-xyz"),
            out var warnings);

        Assert.Empty(catalog.Features);
        Assert.Empty(warnings);
    }

    [Fact]
    public void AnUnknownCatalogSchemaIsAnInputError()
    {
        using var scratch = Fixture.NewDirectory();
        scratch.Write("a.json", """{ "schema": 7, "features": [] }""");

        Assert.Throws<InputException>(() => Catalog.Load(scratch.Path, out _));
    }

    private static ScorecardResult BuildFromProbe()
    {
        var catalog = Catalog.Load(Fixture.Path("catalog"), out var warnings);
        var claims = TraitScanner.ScanSource(Fixture.Text("Probe.cs.txt"), "Probe.cs");
        var results = TestResults.Load([Fixture.Path("probe.trx")]);
        return Scorecard.Build(catalog, claims, results, warnings);
    }

    private static FeatureStatus StatusOf(ScorecardResult scorecard, string id) =>
        scorecard.Rows.Single(row => row.Feature.Id == id).Status;

    private static string One(string id) =>
        $$"""
        { "schema": 1, "features": [ { "id": "{{id}}", "area": "geometry", "milestone": "M1" } ] }
        """;

    private static string FailingRun() =>
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testId="11111111-1111-1111-1111-111111111111"
                            testName="Napkin.Sample.Thing.Works" outcome="Failed" />
          </Results>
          <TestDefinitions>
            <UnitTest name="Napkin.Sample.Thing.Works" id="11111111-1111-1111-1111-111111111111">
              <TestMethod className="Napkin.Sample.Thing" name="Works" />
            </UnitTest>
          </TestDefinitions>
        </TestRun>
        """;
}

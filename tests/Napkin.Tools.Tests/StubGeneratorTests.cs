using Napkin.Tools.Features;

namespace Napkin.Tools.Tests;

public class StubGeneratorTests
{
    [Fact]
    public void WritesOneSkippedFactPerUnclaimedFeature()
    {
        var source = StubGenerator.Render(TwoFeatures(), []);

        Assert.Contains(
            "[Fact(Skip = \"planned: GEO-LEN-001 — Lengths are exact\")]",
            source,
            StringComparison.Ordinal);
        Assert.Contains("[Trait(\"Feature\", \"GEO-LEN-001\")]", source, StringComparison.Ordinal);
        Assert.Contains("public void GEO_LEN_001()", source, StringComparison.Ordinal);
        Assert.Contains("public void DECK_SPAN_001()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AFeatureAHandWrittenTestAlreadyClaimsGetsNoStub()
    {
        var claims = new[]
        {
            new FeatureClaim("Napkin.Core.Geometry.Tests.LengthTests", "Exact", "GEO-LEN-001",
                "LengthTests.cs", 12),
        };

        var source = StubGenerator.Render(TwoFeatures(), claims);

        Assert.DoesNotContain("GEO-LEN-001", source, StringComparison.Ordinal);
        Assert.Contains("DECK-SPAN-001", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGeneratedFileIsValidCSharpEvenWithNothingToPlan()
    {
        var source = StubGenerator.Render(new Catalog(), []);

        Assert.Contains($"namespace {StubGenerator.Namespace};", source, StringComparison.Ordinal);
        Assert.Contains($"public class {StubGenerator.ClassName}", source, StringComparison.Ordinal);
        Assert.Contains("catalog under features/ is empty", source, StringComparison.Ordinal);
        Assert.Contains(
            "Nothing to plan",
            StubGenerator.Render(
                TwoFeatures(),
                TwoFeatures().Features
                    .Select(feature => new FeatureClaim("T", "M", feature.Id, "T.cs", 1))
                    .ToList()),
            StringComparison.Ordinal);
    }

    [Fact]
    public void RegeneratingWithoutACatalogChangeProducesTheSameBytes()
    {
        var first = StubGenerator.Render(TwoFeatures(), []);
        var second = StubGenerator.Render(TwoFeatures(), []);

        Assert.Equal(first, second);
    }

    [Fact]
    public void OutputIsSortedByIdSoFileOrderCannotChangeIt()
    {
        var forwards = StubGenerator.Render(TwoFeatures(), []);
        var backwards = new Catalog { Features = [.. TwoFeatures().Features.AsEnumerable().Reverse()] };

        Assert.Equal(forwards, StubGenerator.Render(backwards, []));
        Assert.True(
            forwards.IndexOf("DECK_SPAN_001", StringComparison.Ordinal) <
            forwards.IndexOf("GEO_LEN_001", StringComparison.Ordinal));
    }

    [Fact]
    public void WritingTwiceLeavesTheFileUntouchedTheSecondTime()
    {
        using var scratch = Fixture.NewDirectory();
        var path = Path.Combine(scratch.Path, "PlannedFeatures.g.cs");
        var source = StubGenerator.Render(TwoFeatures(), []);

        Assert.True(StubGenerator.Write(path, source));
        var written = File.ReadAllBytes(path);

        Assert.False(StubGenerator.Write(path, source));
        Assert.Equal(written, File.ReadAllBytes(path));
    }

    [Fact]
    public void TheGeneratedFileDoesNotCountAsAHandWrittenClaim()
    {
        using var scratch = Fixture.NewDirectory();
        var generated = Path.Combine(scratch.Path, "PlannedFeatures.g.cs");
        StubGenerator.Write(generated, StubGenerator.Render(TwoFeatures(), []));
        scratch.Write("LengthTests.cs", "// nothing here");

        var handWritten = TraitScanner.Scan(
            TraitScanner.FindSourceFiles(scratch.Path).Where(path => !TraitScanner.IsGenerated(path)));

        Assert.Empty(handWritten);

        // ...and regenerating from its own output keeps every stub, rather than deciding the
        // features are covered because the stubs carry their ids.
        Assert.Equal(
            StubGenerator.Render(TwoFeatures(), []),
            StubGenerator.Render(TwoFeatures(), handWritten));
    }

    [Fact]
    public void TheGeneratedStubsAreThemselvesReadableByTheScanner()
    {
        var source = StubGenerator.Render(TwoFeatures(), []);

        var claims = TraitScanner.ScanSource(source, "PlannedFeatures.g.cs");

        Assert.Equal(
            ["DECK-SPAN-001", "GEO-LEN-001"],
            claims.Select(claim => claim.FeatureId).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        Assert.Equal("Napkin.Features.Tests.PlannedFeatures.GEO_LEN_001", claims[1].TestId);
    }

    [Theory]
    [InlineData("GEO-LEN-001", "GEO_LEN_001")]
    [InlineData("geo.len/001", "geo_len_001")]
    [InlineData("001-A", "_001_A")]
    public void IdsBecomeValidMethodNames(string id, string expected) =>
        Assert.Equal(expected, StubGenerator.MethodName(id));

    [Fact]
    public void ATitleWithAQuoteOrABackslashDoesNotBreakTheSkipString()
    {
        var catalog = new Catalog
        {
            Features =
            [
                new Feature { Id = "ODD-001", Title = "a \"quoted\" path C:\\temp" },
            ],
        };

        var source = StubGenerator.Render(catalog, []);

        Assert.Contains(
            "[Fact(Skip = \"planned: ODD-001 — a \\\"quoted\\\" path C:\\\\temp\")]",
            source,
            StringComparison.Ordinal);
    }

    private static Catalog TwoFeatures() => new()
    {
        Features =
        [
            new Feature
            {
                Id = "GEO-LEN-001",
                Area = "geometry",
                Title = "Lengths are exact",
                Milestone = "M1",
            },
            new Feature
            {
                Id = "DECK-SPAN-001",
                Area = "decks",
                Title = "Joist spans come from the adopted code",
                Milestone = "M3",
            },
        ],
    };
}

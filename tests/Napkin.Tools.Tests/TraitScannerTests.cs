using Napkin.Tools.Features;

namespace Napkin.Tools.Tests;

/// <summary>
/// The evidence that the source scan is a sound substitute for traits in the test results: the
/// fixture source and the fixture TRX are the same probe run, so the ids found here join onto the
/// outcomes found there. <see cref="ScorecardTests"/> closes that loop.
/// </summary>
public class TraitScannerTests
{
    [Fact]
    public void TrxCarriesNoTraitsAtAll()
    {
        // This is why the scanner exists. If a future toolchain starts writing traits into TRX,
        // this test fails and the design can be revisited.
        Assert.DoesNotContain("Feature", Fixture.Text("probe.trx"), StringComparison.Ordinal);
    }

    [Fact]
    public void FindsEveryClaimInTheProbeSource()
    {
        var claims = Scan();

        Assert.Equal(
            [
                "GEO-001",
                "GEO-002",
                "GEO-003",
                "GEO-004",
                "GEO-010",
                "GEO-NEST-001",
            ],
            claims.Select(claim => claim.FeatureId).Distinct().OrderBy(id => id).ToArray());
    }

    [Fact]
    public void AMethodCanClaimSeveralFeatures()
    {
        var ids = Scan()
            .Where(claim => claim.MethodName == "Passes")
            .Select(claim => claim.FeatureId)
            .ToList();

        Assert.Contains("GEO-001", ids);
        Assert.Contains("GEO-002", ids);
    }

    [Fact]
    public void ATraitInTheSameAttributeGroupAsAFactIsFound() =>
        // `[Fact, Trait("Feature", "GEO-001")]` in the fixture.
        Assert.Contains(Scan(), claim => claim.FeatureId == "GEO-001");

    [Fact]
    public void AClassLevelTraitAppliesToEveryMethodInTheClass()
    {
        var claimed = Scan()
            .Where(claim => claim.FeatureId == "GEO-010")
            .Select(claim => claim.MethodName)
            .ToHashSet();

        Assert.Contains("Passes", claimed);
        Assert.Contains("Skipped", claimed);
        Assert.Contains("TheoryCase", claimed);
    }

    [Fact]
    public void NestedTypesAreNamedTheWayVsTestNamesThem()
    {
        var claim = Assert.Single(Scan(), item => item.FeatureId == "GEO-NEST-001");

        // Exactly the className the fixture TRX carries for that test.
        Assert.Equal("TraitProbe.Probe+Nested", claim.TypeName);
        Assert.Equal("TraitProbe.Probe+Nested.Inner", claim.TestId);
        Assert.Contains($"className=\"{claim.TypeName}\" name=\"Inner\"", Fixture.Text("probe.trx"));
    }

    [Fact]
    public void XunitV3AlsoCarriesNoTraitsIntoTheResults()
    {
        // tests/Napkin.App.GuiTests runs on xunit v3 (Avalonia.Headless.XUnit requires it) while
        // the other test projects stay on 2.5.3, so the mechanism has to work for both. It does,
        // because neither version's TRX carries traits and both write the same className + name.
        Assert.DoesNotContain("Feature", Fixture.Text("v3probe.trx"), StringComparison.Ordinal);
        Assert.Contains(
            "className=\"V3Probe.ShellWorkflows+Nested\" name=\"Inner\"",
            Fixture.Text("v3probe.trx"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AGuiWorkflowAttributeClaimsItsFeatureToo()
    {
        // `[GuiWorkflow("GUI-SHELL-01")]` (issue #33) takes the id as its first argument rather
        // than spelling out a Feature trait, and publishes the trait at run time — where nothing
        // can read it. The scanner therefore knows the shorthand.
        var claims = ScanV3();

        var workflow = Assert.Single(claims, claim => claim.FeatureId == "GUI-SHELL-01");
        Assert.Equal("V3Probe.ShellWorkflows.Shell_opens_and_renders", workflow.TestId);
    }

    [Fact]
    public void APlainFeatureTraitStillWorksInAV3Project() =>
        Assert.Contains(ScanV3(), claim => claim.FeatureId == "GUI-SHELL-99");

    [Fact]
    public void TheClaimsFoundInV3SourceJoinOntoTheV3Results()
    {
        var results = TestResults.Load([Fixture.Path("v3probe.trx")]);

        foreach (var claim in ScanV3())
        {
            Assert.Equal(TestOutcome.Passed, results.OutcomeOf(claim.TestId));
        }
    }

    [Fact]
    public void DeclaringTheWorkflowAttributeIsNotItselfAClaim()
    {
        var claims = TraitScanner.ScanSource(
            """
            namespace Napkin.App.GuiTests.Harness;

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class GuiWorkflowAttribute : FactAttribute
            {
                public GuiWorkflowAttribute(string featureId) => FeatureId = featureId;

                public string FeatureId { get; }
            }
            """,
            "GuiWorkflowAttribute.cs");

        Assert.Empty(claims);
    }

    [Fact]
    public void FileScopedAndBlockNamespacesBothWork()
    {
        var blockScoped = TraitScanner.ScanSource(
            """
            namespace Napkin.Sample.Tests
            {
                public class Thing
                {
                    [Fact]
                    [Trait("Feature", "SMPL-001")]
                    public void Works() { }
                }
            }
            """,
            "Thing.cs");

        Assert.Equal("Napkin.Sample.Tests.Thing.Works", Assert.Single(blockScoped).TestId);
    }

    [Fact]
    public void ATraitInACommentOrAStringIsNotAClaim()
    {
        var claims = TraitScanner.ScanSource(
            """
            namespace Napkin.Sample.Tests;

            public class Thing
            {
                // [Trait("Feature", "COMMENTED-001")]
                /* [Trait("Feature", "BLOCK-001")] */
                [Fact]
                public void Works()
                {
                    var text = "[Trait(\"Feature\", \"STRING-001\")]";
                    Assert.NotNull(text);
                }
            }
            """,
            "Thing.cs");

        Assert.Empty(claims);
    }

    [Fact]
    public void StatementsInsideAMethodBodyAreNotMistakenForMethods()
    {
        var claims = TraitScanner.ScanSource(
            """
            namespace Napkin.Sample.Tests;

            public class Thing
            {
                [Fact]
                [Trait("Feature", "SMPL-001")]
                public void Works()
                {
                    Assert.Equal(1, Compute(1));
                    if (true) { Assert.True(Compute(2) > 0); }
                }

                private static int Compute(int value) => value;
            }
            """,
            "Thing.cs");

        var claim = Assert.Single(claims);
        Assert.Equal("Works", claim.MethodName);
    }

    [Fact]
    public void AGenericMethodIsStillRecognised()
    {
        var claims = TraitScanner.ScanSource(
            """
            namespace Napkin.Sample.Tests;

            public class Thing
            {
                [Fact]
                [Trait("Feature", "SMPL-001")]
                public void Works<TValue>() where TValue : class { }
            }
            """,
            "Thing.cs");

        Assert.Equal("Works", Assert.Single(claims).MethodName);
    }

    [Fact]
    public void BuildOutputIsNotScanned()
    {
        using var scratch = Fixture.NewDirectory();
        scratch.Write("Project/Real.cs", "// real");
        scratch.Write("Project/obj/Debug/Generated.cs", "// generated");
        scratch.Write("Project/bin/Debug/Copied.cs", "// copied");

        var files = TraitScanner.FindSourceFiles(scratch.Path);

        Assert.Equal("Real.cs", Path.GetFileName(Assert.Single(files)));
    }

    [Theory]
    [InlineData("PlannedFeatures.g.cs", true)]
    [InlineData("LengthTests.cs", false)]
    public void GeneratedFilesAreRecognisedByName(string name, bool generated) =>
        Assert.Equal(generated, TraitScanner.IsGenerated(name));

    private static IReadOnlyList<FeatureClaim> Scan() =>
        TraitScanner.ScanSource(Fixture.Text("Probe.cs.txt"), "Probe.cs");

    private static IReadOnlyList<FeatureClaim> ScanV3() =>
        TraitScanner.ScanSource(Fixture.Text("V3Probe.cs.txt"), "V3Probe.cs");
}

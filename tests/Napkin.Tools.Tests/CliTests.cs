using Napkin.Tools.Commands;

namespace Napkin.Tools.Tests;

/// <summary>
/// The commands as a user meets them: arguments in, files and exit codes out.
/// </summary>
public class CliTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void HelpIsAvailableAndSucceeds(string argument)
    {
        var run = Run(argument);

        Assert.Equal(ExitCode.Ok, run.Code);
        Assert.Contains("ratchet check", run.Output, StringComparison.Ordinal);
        Assert.Contains("scorecard report", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void EachCommandCanPrintItsOwnHelp() =>
        Assert.Equal(ExitCode.Ok, Run("ratchet", "check", "--help").Code);

    [Fact]
    public void AnUnknownCommandIsAUsageError()
    {
        var run = Run("ratchet", "polish");

        Assert.Equal(ExitCode.UsageError, run.Code);
        Assert.Contains("unknown command", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownOptionIsAUsageErrorRatherThanBeingIgnored()
    {
        var run = Run("ratchet", "check", "--allow-everything");

        Assert.Equal(ExitCode.UsageError, run.Code);
        Assert.Contains("unknown option", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void LoweringWithoutAReasonIsRefused()
    {
        var run = Run("ratchet", "update", "--allow-lower");

        Assert.Equal(ExitCode.UsageError, run.Code);
        Assert.Contains("--reason", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AReasonWithoutAllowLowerIsRefused()
    {
        var run = Run("ratchet", "update", "--reason", "because");

        Assert.Equal(ExitCode.UsageError, run.Code);
        Assert.Contains("--allow-lower", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckRefusesTheUpdateOnlyOptions()
    {
        var run = Run("ratchet", "check", "--allow-lower");

        Assert.Equal(ExitCode.UsageError, run.Code);
    }

    [Fact]
    public void RunningTheRatchetWithNoCoverageAtAllIsAnInputError()
    {
        using var repo = NewRepo();

        var run = Run("ratchet", "check", "--root", repo.Path);

        Assert.Equal(ExitCode.InputError, run.Code);
        Assert.Contains("coverage.cobertura.xml", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckPassesAndThenFailsAsTheBaselineIsRaisedPastTheRun()
    {
        using var repo = NewRepo();
        File.Copy(
            Fixture.Path("branchy.cobertura.xml"),
            repo.File("artifacts/test-results/run/coverage.cobertura.xml"));

        // No baseline entry yet: the check fails and says how to create one.
        var missing = Run("ratchet", "check", "--root", repo.Path);
        Assert.Equal(ExitCode.GateFailed, missing.Code);
        Assert.Contains("ratchet update", missing.Error, StringComparison.Ordinal);

        var update = Run("ratchet", "update", "--root", repo.Path);
        Assert.Equal(ExitCode.Ok, update.Code);
        Assert.True(File.Exists(Path.Combine(repo.Path, "ratchet", "baseline.json")));

        // With the floors at what the run achieved, the same run passes.
        Assert.Equal(ExitCode.Ok, Run("ratchet", "check", "--root", repo.Path).Code);

        // A second update changes nothing, which keeps the working tree clean in CI.
        Assert.Contains(
            "already up to date",
            Run("ratchet", "update", "--root", repo.Path).Output,
            StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateBootstrapsTheBaselineEvenWhenEveryAssemblyIsStillAPlaceholder()
    {
        using var repo = NewRepo();
        File.Copy(
            Fixture.Path("placeholder.cobertura.xml"),
            repo.File("artifacts/test-results/run/coverage.cobertura.xml"));
        var path = Path.Combine(repo.Path, "ratchet", "baseline.json");

        Assert.Equal(ExitCode.Ok, Run("ratchet", "update", "--root", repo.Path).Code);

        // An empty `coverage` map is the honest answer, and the file exists to be raised later.
        Assert.Contains("\"coverage\": {}", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal(ExitCode.Ok, Run("ratchet", "check", "--root", repo.Path).Code);
    }

    [Fact]
    public void UpdateRefusesToLowerAFloorAndSaysWhatToPass()
    {
        using var repo = NewRepo();
        File.Copy(
            Fixture.Path("branchy.cobertura.xml"),
            repo.File("artifacts/test-results/run/coverage.cobertura.xml"));
        repo.Write("ratchet/baseline.json", """
            {
              "schema": 1,
              "coverage": { "Napkin.Probe.Branchy": { "line": 99.0, "branch": 99.0 } },
              "gui": { "workflowsPassed": 0, "workflowIds": [] },
              "log": []
            }
            """);

        var refused = Run("ratchet", "update", "--root", repo.Path);
        Assert.Equal(ExitCode.GateFailed, refused.Code);
        Assert.Contains("--allow-lower", refused.Error, StringComparison.Ordinal);

        var allowed = Run(
            "ratchet", "update", "--root", repo.Path,
            "--allow-lower", "--reason", "dropped the half-written switch");
        Assert.Equal(ExitCode.Ok, allowed.Code);

        var written = File.ReadAllText(Path.Combine(repo.Path, "ratchet", "baseline.json"));
        Assert.Contains("dropped the half-written switch", written, StringComparison.Ordinal);
    }

    [Fact]
    public void TheScorecardNeverFailsEvenWithNothingToReport()
    {
        using var repo = NewRepo();

        var run = Run("scorecard", "report", "--root", repo.Path);

        Assert.Equal(ExitCode.Ok, run.Code);
        Assert.Contains("Feature scorecard", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheScorecardReportsTheProbeRun()
    {
        using var repo = NewRepo();
        Directory.CreateDirectory(Path.Combine(repo.Path, "features"));
        File.Copy(
            Fixture.Path(Path.Combine("catalog", "probe-features.json")),
            repo.File("features/catalog.json"));
        File.Copy(Fixture.Path("probe.trx"), repo.File("artifacts/test-results/run.trx"));
        File.Copy(Fixture.Path("Probe.cs.txt"), repo.File("tests/Probe/Probe.cs"));

        var json = Path.Combine(repo.Path, "artifacts", "scorecard.json");
        var run = Run("scorecard", "report", "--root", repo.Path, "--json", json);

        Assert.Equal(ExitCode.Ok, run.Code);
        Assert.Contains("`DECK-001`", run.Output, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"Passing\"", File.ReadAllText(json), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSummaryIsAppendedToTheGitHubJobSummaryFile()
    {
        using var repo = NewRepo();
        var summary = repo.File("summary.md");
        var previous = Environment.GetEnvironmentVariable(ScorecardCommand.SummaryVariable);
        Environment.SetEnvironmentVariable(ScorecardCommand.SummaryVariable, summary);
        try
        {
            Assert.Equal(
                ExitCode.Ok,
                Run("scorecard", "report", "--root", repo.Path, "--summary").Code);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ScorecardCommand.SummaryVariable, previous);
        }

        Assert.Contains("Feature scorecard", File.ReadAllText(summary), StringComparison.Ordinal);
    }

    [Fact]
    public void StubsAreGeneratedFromTheCatalogAndAreIdempotent()
    {
        using var repo = NewRepo();
        File.Copy(
            Fixture.Path(Path.Combine("catalog", "probe-features.json")),
            repo.File("features/catalog.json"));
        var output = repo.File("tests/Napkin.Features.Tests/PlannedFeatures.g.cs");

        Assert.Equal(ExitCode.Ok, Run("scorecard", "stubs", "--root", repo.Path).Code);
        var first = File.ReadAllBytes(output);
        Assert.Contains("GEO-001", System.Text.Encoding.UTF8.GetString(first),
            StringComparison.Ordinal);

        var again = Run("scorecard", "stubs", "--root", repo.Path);
        Assert.Contains("already up to date", again.Output, StringComparison.Ordinal);
        Assert.Equal(first, File.ReadAllBytes(output));
    }

    [Fact]
    public void StubsSkipFeaturesAHandWrittenTestAlreadyClaims()
    {
        using var repo = NewRepo();
        File.Copy(
            Fixture.Path(Path.Combine("catalog", "probe-features.json")),
            repo.File("features/catalog.json"));
        repo.Write("tests/Napkin.Core.Geometry.Tests/LengthTests.cs", """
            namespace Napkin.Core.Geometry.Tests;

            public class LengthTests
            {
                [Fact]
                [Trait("Feature", "GEO-001")]
                public void Exact() { }
            }
            """);

        Assert.Equal(ExitCode.Ok, Run("scorecard", "stubs", "--root", repo.Path).Code);

        var generated = File.ReadAllText(
            Path.Combine(repo.Path, "tests", "Napkin.Features.Tests", "PlannedFeatures.g.cs"));
        Assert.DoesNotContain("GEO-001", generated, StringComparison.Ordinal);
        Assert.Contains("DECK-001", generated, StringComparison.Ordinal);
    }

    /// <summary>A scratch directory that looks enough like the repository to run a command in.</summary>
    private static Fixture.Scratch NewRepo()
    {
        var repo = Fixture.NewDirectory();
        File.WriteAllText(Path.Combine(repo.Path, RepoLayout.RootMarker), string.Empty);
        return repo;
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Cli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}

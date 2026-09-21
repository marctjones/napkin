using Napkin.Tools.Features;

namespace Napkin.Tools.Commands;

/// <summary>Wires <c>scorecard report</c> and <c>scorecard stubs</c> to files and the console.</summary>
public static class ScorecardCommand
{
    private static readonly HashSet<string> Valued =
        ["--root", "--catalog-dir", "--tests-dir", "--results-dir", "--json", "--output"];

    private static readonly HashSet<string> Flags = ["--summary"];

    /// <summary>The environment variable GitHub Actions exposes for the job summary.</summary>
    public const string SummaryVariable = "GITHUB_STEP_SUMMARY";

    public static int Report(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var parsed = CommandLine.Parse(args, Valued, Flags);
        if (parsed.WantsHelp)
        {
            output.WriteLine(Cli.Usage);
            return ExitCode.Ok;
        }

        var layout = Cli.ResolveRoot(parsed);
        var catalog = Catalog.Load(CatalogDirectory(layout, parsed), out var warnings);
        var claims = TraitScanner.Scan(TraitScanner.FindSourceFiles(TestsDirectory(layout, parsed)));

        var resultsDirectory = parsed.Value("--results-dir") ?? layout.TestResultsDirectory;
        var trx = TestResults.FindTrxFiles(resultsDirectory);
        var results = TestResults.Load(trx);
        if (trx.Count == 0)
        {
            error.WriteLine(
                $"note: no *.trx under {layout.Relative(resultsDirectory)} — every feature will " +
                "read as not yet demonstrated.");
        }

        var scorecard = Scorecard.Build(catalog, claims, results, warnings);
        var markdown = ScorecardWriter.ToMarkdown(scorecard);
        output.Write(markdown);

        if (parsed.Has("--summary"))
        {
            var summary = Environment.GetEnvironmentVariable(SummaryVariable);
            if (string.IsNullOrEmpty(summary))
            {
                error.WriteLine($"note: --summary given but ${SummaryVariable} is not set.");
            }
            else
            {
                File.AppendAllText(summary, markdown);
            }
        }

        var json = parsed.Value("--json");
        if (json is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(json))!);
            File.WriteAllText(json, ScorecardWriter.ToJson(scorecard));
            error.WriteLine($"note: wrote {layout.Relative(json)}.");
        }

        // Progress is never a failure. Only an unreadable input leaves this method non-zero, and
        // that arrives as an InputException rather than a return value.
        return ExitCode.Ok;
    }

    public static int Stubs(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var parsed = CommandLine.Parse(args, Valued, Flags);
        if (parsed.WantsHelp)
        {
            output.WriteLine(Cli.Usage);
            return ExitCode.Ok;
        }

        var layout = Cli.ResolveRoot(parsed);
        var catalogDirectory = CatalogDirectory(layout, parsed);
        var catalog = Catalog.Load(catalogDirectory, out var warnings);
        foreach (var warning in warnings)
        {
            error.WriteLine($"note: {warning}");
        }

        if (catalog.Features.Count == 0)
        {
            error.WriteLine(
                $"note: no features found under {layout.Relative(catalogDirectory)}; writing an " +
                "empty stub file.");
        }

        // Hand-written only: a stub must never count as the test that covers its own feature.
        var handWritten = TraitScanner.Scan(
            TraitScanner.FindSourceFiles(TestsDirectory(layout, parsed))
                .Where(path => !TraitScanner.IsGenerated(path)));

        var path = parsed.Value("--output") ?? layout.StubsPath;
        var content = StubGenerator.Render(catalog, handWritten);
        var planned = catalog.Features.Count -
            catalog.Features.Count(feature =>
                handWritten.Any(claim => claim.FeatureId == feature.Id));

        if (StubGenerator.Write(path, content))
        {
            output.WriteLine($"scorecard stubs: wrote {layout.Relative(path)} ({planned} stub(s)).");
        }
        else
        {
            output.WriteLine(
                $"scorecard stubs: {layout.Relative(path)} is already up to date ({planned} stub(s)).");
        }

        return ExitCode.Ok;
    }

    private static string CatalogDirectory(RepoLayout layout, CommandLine parsed) =>
        parsed.Value("--catalog-dir") ?? layout.CatalogDirectory;

    private static string TestsDirectory(RepoLayout layout, CommandLine parsed) =>
        parsed.Value("--tests-dir") ?? layout.TestsDirectory;
}

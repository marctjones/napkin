namespace Napkin.Tools.Commands;

/// <summary>
/// The entry point's real body, kept out of `Program.cs` so the tests can run a command with
/// captured output instead of starting a process.
/// </summary>
public static class Cli
{
    public const string Usage = """
        napkin tools — repository tooling for the test infrastructure.

        Usage:
          napkin-tools ratchet check     [options]
          napkin-tools ratchet update    [--allow-lower --reason "<why>"] [options]
          napkin-tools scorecard report  [--summary] [--json <path>] [options]
          napkin-tools scorecard stubs   [options]

        ratchet check
          Fails when a baselined assembly's line or branch coverage has fallen below its floor,
          when an assembly with coverable lines has no baseline entry, or when the GUI workflow
          run lost ground. This is a deliberate gate on pull requests; it never gates a release.

        ratchet update
          Raises the floors in ratchet/baseline.json to what this run achieved. Lowering a floor
          takes --allow-lower together with --reason, and is recorded in the baseline's log.

        scorecard report
          Prints how much of the feature catalog the test suite proves. It measures progress and
          never fails a build: the only non-zero exit is an unreadable input.

        scorecard stubs
          Regenerates tests/Napkin.Features.Tests/PlannedFeatures.g.cs — one skipped fact per
          catalogued feature that no hand-written test claims yet.

        Common options:
          --root <path>          Repository root (default: the nearest napkin.sln above the
                                 working directory).
          --baseline <path>      Default: ratchet/baseline.json
          --results-dir <path>   TRX and coverage from `dotnet test --results-directory`.
                                 Default: artifacts/test-results
          --gui-metrics <path>   Default: artifacts/gui-metrics.json
          --catalog-dir <path>   Default: features
          --tests-dir <path>     Source scanned for [Trait("Feature", "<ID>")]. Default: tests
          --output <path>        scorecard stubs only. Default:
                                 tests/Napkin.Features.Tests/PlannedFeatures.g.cs
          --summary              scorecard report only: also append to $GITHUB_STEP_SUMMARY.
          --json <path>          scorecard report only: also write the report as JSON.
          -h, --help             This text.

        Exit codes:
          0  the command succeeded
          1  a gate failed, or a lowering was refused
          2  the command line was wrong
          3  an input file was missing, unreadable or malformed
        """;

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Count == 0 || args[0] is "-h" or "--help" or "help")
            {
                output.WriteLine(Usage);
                return ExitCode.Ok;
            }

            var rest = args.Skip(2).ToList();
            var command = args.Count > 1 ? $"{args[0]} {args[1]}" : args[0];

            return command switch
            {
                "ratchet check" => RatchetCommand.Check(rest, output, error),
                "ratchet update" => RatchetCommand.Update(rest, output, error),
                "scorecard report" => ScorecardCommand.Report(rest, output, error),
                "scorecard stubs" => ScorecardCommand.Stubs(rest, output, error),
                _ => Unknown(command, output, error),
            };
        }
        catch (UsageException exception)
        {
            error.WriteLine($"napkin-tools: {exception.Message}");
            error.WriteLine("Run `napkin-tools --help` for usage.");
            return ExitCode.UsageError;
        }
        catch (InputException exception)
        {
            error.WriteLine($"napkin-tools: {exception.Message}");
            return ExitCode.InputError;
        }
    }

    private static int Unknown(string command, TextWriter output, TextWriter error)
    {
        error.WriteLine($"napkin-tools: unknown command `{command}`.");
        output.WriteLine(Usage);
        return ExitCode.UsageError;
    }

    /// <summary>Resolves `--root`, falling back to the nearest `napkin.sln` above the cwd.</summary>
    internal static RepoLayout ResolveRoot(CommandLine parsed)
    {
        var root = parsed.Value("--root");
        if (root is not null)
        {
            return new RepoLayout(root);
        }

        return RepoLayout.Discover(Directory.GetCurrentDirectory())
            ?? throw new UsageException(
                $"no {RepoLayout.RootMarker} found above the working directory; pass --root.");
    }
}

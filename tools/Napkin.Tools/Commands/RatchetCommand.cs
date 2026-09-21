using Napkin.Tools.Coverage;
using Napkin.Tools.Ratchet;

namespace Napkin.Tools.Commands;

/// <summary>Wires <c>ratchet check</c> and <c>ratchet update</c> to files and the console.</summary>
public static class RatchetCommand
{
    private static readonly HashSet<string> Valued =
        ["--root", "--baseline", "--results-dir", "--gui-metrics", "--reason"];

    private static readonly HashSet<string> Flags = ["--allow-lower"];

    public static int Check(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var parsed = CommandLine.Parse(args, Valued, Flags);
        if (parsed.WantsHelp)
        {
            output.WriteLine(Cli.Usage);
            return ExitCode.Ok;
        }

        Reject(parsed, "--allow-lower", "--reason");

        var (layout, baseline, measured, gui) = LoadEverything(parsed, output);
        var result = RatchetCheck.Run(baseline, measured, gui);

        WriteTable(output, result.Rows);
        foreach (var notice in result.Notices)
        {
            output.WriteLine($"note: {notice}");
        }

        if (result.Passed)
        {
            output.WriteLine("ratchet check: passed.");
            return ExitCode.Ok;
        }

        error.WriteLine();
        error.WriteLine("ratchet check: FAILED.");
        foreach (var failure in result.Failures)
        {
            error.WriteLine($"  - {failure}");
        }

        error.WriteLine();
        error.WriteLine(
            $"The floors live in {layout.Relative(BaselinePath(layout, parsed))}. Raise coverage, " +
            "or lower a floor on purpose with:");
        error.WriteLine(
            "  dotnet run --project tools/Napkin.Tools -- ratchet update --allow-lower " +
            "--reason \"why this is acceptable\"");
        return ExitCode.GateFailed;
    }

    public static int Update(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var parsed = CommandLine.Parse(args, Valued, Flags);
        if (parsed.WantsHelp)
        {
            output.WriteLine(Cli.Usage);
            return ExitCode.Ok;
        }

        var allowLower = parsed.Has("--allow-lower");
        var reason = parsed.Value("--reason");

        if (allowLower && string.IsNullOrWhiteSpace(reason))
        {
            throw new UsageException(
                "--allow-lower needs --reason \"<why>\"; the reason is written into the baseline.");
        }

        if (!allowLower && reason is not null)
        {
            throw new UsageException("--reason only applies together with --allow-lower.");
        }

        var (layout, baseline, measured, gui) = LoadEverything(parsed, output);
        var path = BaselinePath(layout, parsed);
        var result = RatchetUpdate.Apply(
            baseline,
            measured,
            gui,
            allowLower,
            reason,
            DateOnly.FromDateTime(DateTime.UtcNow));

        if (result.Blocked)
        {
            error.WriteLine("ratchet update: refused — this would lower a floor.");
            foreach (var blocked in result.BlockedLowerings)
            {
                error.WriteLine($"  - {blocked}");
            }

            error.WriteLine();
            error.WriteLine(
                "Re-run with --allow-lower --reason \"<why>\" if the drop is deliberate. The " +
                "reason is recorded in the baseline's log.");
            return ExitCode.GateFailed;
        }

        // A first run on a repository whose assemblies are all placeholders has nothing to raise,
        // but the file should still exist so the next run has something to compare against.
        if (!result.Changed && File.Exists(path))
        {
            output.WriteLine($"ratchet update: {layout.Relative(path)} is already up to date.");
            return ExitCode.Ok;
        }

        baseline.Save(path);
        output.WriteLine($"ratchet update: wrote {layout.Relative(path)}.");
        foreach (var change in result.Changes)
        {
            output.WriteLine($"  - {change}");
        }

        return ExitCode.Ok;
    }

    private static (RepoLayout Layout, Baseline Baseline, IReadOnlyList<AssemblyCoverage> Measured,
        GuiMetrics? Gui) LoadEverything(CommandLine parsed, TextWriter output)
    {
        var layout = Cli.ResolveRoot(parsed);
        var baseline = Baseline.Load(BaselinePath(layout, parsed));

        var resultsDirectory = parsed.Value("--results-dir") ?? layout.TestResultsDirectory;
        var reports = CoverageMerger.FindReports(resultsDirectory);
        if (reports.Count == 0)
        {
            throw new InputException(
                $"no {CoverageMerger.ReportFileName} under {layout.Relative(resultsDirectory)}. Run " +
                "the tests with coverage first:\n" +
                "  dotnet test napkin.sln --settings ratchet/coverage.runsettings " +
                "--collect:\"XPlat Code Coverage\" --logger trx " +
                "--results-directory artifacts/test-results");
        }

        output.WriteLine(
            $"Read {reports.Count} coverage report(s) from {layout.Relative(resultsDirectory)}.");

        var guiPath = parsed.Value("--gui-metrics") ?? layout.GuiMetricsPath;
        return (layout, baseline, CoverageMerger.Merge(reports), GuiMetrics.Load(guiPath));
    }

    private static string BaselinePath(RepoLayout layout, CommandLine parsed) =>
        parsed.Value("--baseline") ?? layout.BaselinePath;

    private static void Reject(CommandLine parsed, params string[] names)
    {
        foreach (var name in names)
        {
            if (parsed.Has(name))
            {
                throw new UsageException($"{name} applies to `ratchet update`, not `ratchet check`.");
            }
        }
    }

    private static void WriteTable(TextWriter output, IReadOnlyList<RatchetCheck.Row> rows)
    {
        if (rows.Count == 0)
        {
            output.WriteLine("No assemblies were instrumented.");
            return;
        }

        var width = Math.Max(8, rows.Max(row => row.Assembly.Length));
        output.WriteLine();
        output.WriteLine($"{"Assembly".PadRight(width)}  {"line",8}  {"floor",8}  {"branch",8}  {"floor",8}");
        output.WriteLine(new string('-', width + 42));

        foreach (var row in rows)
        {
            output.WriteLine(
                $"{row.Assembly.PadRight(width)}  " +
                $"{Cell(row.LinePercent),8}  {Floor(row.Floor?.Line),8}  " +
                $"{Cell(row.BranchPercent),8}  {Floor(row.Floor?.Branch),8}");
        }

        output.WriteLine();
    }

    private static string Cell(double? value) =>
        value is null ? "n/a" : $"{RatchetCheck.Format(value.Value)}%";

    private static string Floor(double? value) =>
        value is null ? "-" : $"{RatchetCheck.Format(value.Value)}%";
}

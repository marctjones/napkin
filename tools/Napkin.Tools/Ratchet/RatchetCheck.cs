using System.Globalization;
using Napkin.Tools.Coverage;

namespace Napkin.Tools.Ratchet;

/// <summary>
/// Compares a measured run against the committed floors. Pure: it reads no files and writes no
/// output, so every rule below is directly testable.
/// </summary>
public static class RatchetCheck
{
    /// <summary>What a check concluded.</summary>
    /// <param name="Failures">Reasons the gate should fail; empty means it passes.</param>
    /// <param name="Notices">Things worth saying that are not failures.</param>
    /// <param name="Rows">One row per measured assembly, for the printed table.</param>
    public sealed record Result(
        IReadOnlyList<string> Failures,
        IReadOnlyList<string> Notices,
        IReadOnlyList<Row> Rows)
    {
        public bool Passed => Failures.Count == 0;
    }

    /// <summary>One assembly's measured coverage beside its floors.</summary>
    public sealed record Row(
        string Assembly,
        double? LinePercent,
        double? BranchPercent,
        Baseline.CoverageFloor? Floor,
        bool NotApplicable);

    /// <summary>
    /// Applies every ratchet rule. <paramref name="guiMetrics"/> is null when the GUI suite did
    /// not run — the usual case for a unit-test-only job.
    /// </summary>
    public static Result Run(
        Baseline baseline,
        IReadOnlyList<AssemblyCoverage> measured,
        GuiMetrics? guiMetrics)
    {
        var failures = new List<string>();
        var notices = new List<string>();
        var rows = new List<Row>();

        var byAssembly = measured.ToDictionary(item => item.Assembly, StringComparer.Ordinal);

        foreach (var assembly in measured)
        {
            baseline.Coverage.TryGetValue(assembly.Assembly, out var floor);
            rows.Add(new Row(
                assembly.Assembly,
                assembly.LinePercent,
                assembly.BranchPercent,
                floor,
                assembly.IsNotApplicable));

            if (assembly.IsNotApplicable)
            {
                // A placeholder assembly: instrumented, nothing to cover. It never fails, and it
                // is never written to the baseline, so the entry appears the day it has code.
                notices.Add(
                    $"{assembly.Assembly}: no coverable lines yet — reported N/A, not gated.");
                continue;
            }

            if (floor is null)
            {
                failures.Add(
                    $"{assembly.Assembly}: has coverable lines but no baseline entry. " +
                    "Run `dotnet run --project tools/Napkin.Tools -- ratchet update` and commit " +
                    "ratchet/baseline.json.");
                continue;
            }

            CheckMetric(failures, assembly.Assembly, "line", assembly.LinePercent, floor.Line);
            CheckMetric(failures, assembly.Assembly, "branch", assembly.BranchPercent, floor.Branch);
        }

        foreach (var (assembly, floor) in baseline.Coverage)
        {
            if (byAssembly.ContainsKey(assembly))
            {
                continue;
            }

            failures.Add(
                $"{assembly}: baselined at {Format(floor.Line)}% line / {Format(floor.Branch)}% branch " +
                "but no coverage was measured for it. Run the whole test suite with coverage, or " +
                "lower the floor deliberately with `ratchet update --allow-lower --reason \"...\"`.");
        }

        CheckGui(failures, notices, baseline.Gui, guiMetrics);

        return new Result(failures, notices, rows);
    }

    private static void CheckMetric(
        List<string> failures,
        string assembly,
        string metric,
        double? measured,
        double floor)
    {
        if (measured is null)
        {
            // No branches in the assembly at all: there is nothing this floor can be measured
            // against. Floors written by `ratchet update` are 0.0 in that case, so this is quiet.
            if (floor > Baseline.Tolerance)
            {
                failures.Add(
                    $"{assembly}: {metric} floor is {Format(floor)}% but the run measured no " +
                    $"{metric} data at all.");
            }

            return;
        }

        if (measured.Value < floor - Baseline.Tolerance)
        {
            failures.Add(
                $"{assembly}: {metric} coverage {Format(measured.Value)}% is below the floor of " +
                $"{Format(floor)}% (tolerance {Format(Baseline.Tolerance)} points).");
        }
    }

    private static void CheckGui(
        List<string> failures,
        List<string> notices,
        Baseline.GuiFloor floor,
        GuiMetrics? metrics)
    {
        if (metrics is null)
        {
            if (floor.RequiresWorkflows)
            {
                failures.Add(
                    $"GUI: the baseline requires {floor.WorkflowsPassed} passing workflow(s) but " +
                    "artifacts/gui-metrics.json is absent — the GUI suite did not report.");
            }
            else
            {
                notices.Add("GUI: no artifacts/gui-metrics.json and no GUI floor — nothing to check.");
            }

            return;
        }

        if (metrics.WorkflowsPassed < floor.WorkflowsPassed)
        {
            failures.Add(
                $"GUI: {metrics.WorkflowsPassed} workflow(s) passed, below the floor of " +
                $"{floor.WorkflowsPassed}.");
        }

        var passed = new HashSet<string>(metrics.WorkflowIds, StringComparer.Ordinal);
        var missing = floor.WorkflowIds
            .Where(id => !passed.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (missing.Count > 0)
        {
            failures.Add(
                $"GUI: workflow(s) {string.Join(", ", missing)} are in the baseline but did not " +
                "pass in this run.");
        }
    }

    /// <summary>Two decimals, invariant — the same text in a message, a table and a file.</summary>
    public static string Format(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);
}

using Napkin.Tools.Coverage;

namespace Napkin.Tools.Ratchet;

/// <summary>
/// Raises the floors to what a run actually achieved. Lowering is possible but never accidental:
/// it takes `--allow-lower` and a written reason, which is recorded in the baseline's log.
/// </summary>
public static class RatchetUpdate
{
    /// <summary>What an update did, or refused to do.</summary>
    /// <param name="Changes">Human-readable lines describing every floor that moved.</param>
    /// <param name="BlockedLowerings">Lowerings refused for want of `--allow-lower`.</param>
    public sealed record Result(
        IReadOnlyList<string> Changes,
        IReadOnlyList<string> BlockedLowerings)
    {
        public bool Blocked => BlockedLowerings.Count > 0;

        public bool Changed => Changes.Count > 0;
    }

    /// <summary>
    /// Mutates <paramref name="baseline"/> in place. The caller saves it only when the result is
    /// not <see cref="Result.Blocked"/>.
    /// </summary>
    /// <param name="today">Injected so the log entry is deterministic under test.</param>
    public static Result Apply(
        Baseline baseline,
        IReadOnlyList<AssemblyCoverage> measured,
        GuiMetrics? guiMetrics,
        bool allowLower,
        string? reason,
        DateOnly today)
    {
        var changes = new List<string>();
        var blocked = new List<string>();
        var lowered = false;

        foreach (var assembly in measured)
        {
            if (assembly.IsNotApplicable)
            {
                // Deliberately not written. An entry of 0/0 would make the check pass trivially
                // for ever; leaving it out is what makes the check say "run ratchet update" on
                // the first pull request that gives the assembly real code.
                continue;
            }

            var line = FloorTo2(assembly.LinePercent ?? 0);
            var branch = FloorTo2(assembly.BranchPercent ?? 0);

            if (!baseline.Coverage.TryGetValue(assembly.Assembly, out var floor))
            {
                baseline.Coverage[assembly.Assembly] = new Baseline.CoverageFloor
                {
                    Line = line,
                    Branch = branch,
                };
                changes.Add(
                    $"{assembly.Assembly}: new entry at {RatchetCheck.Format(line)}% line / " +
                    $"{RatchetCheck.Format(branch)}% branch.");
                continue;
            }

            lowered |= Move(changes, blocked, allowLower, assembly.Assembly, "line", floor.Line, line,
                value => floor.Line = value);
            lowered |= Move(changes, blocked, allowLower, assembly.Assembly, "branch", floor.Branch, branch,
                value => floor.Branch = value);
        }

        lowered |= UpdateGui(changes, blocked, allowLower, baseline.Gui, guiMetrics);

        if (blocked.Count > 0)
        {
            return new Result([], blocked);
        }

        if (lowered)
        {
            baseline.Log.Add(new Baseline.LogEntry
            {
                Date = today.ToString("yyyy-MM-dd"),
                Reason = reason ?? string.Empty,
            });
        }

        return new Result(changes, []);
    }

    private static bool Move(
        List<string> changes,
        List<string> blocked,
        bool allowLower,
        string assembly,
        string metric,
        double current,
        double measured,
        Action<double> set)
    {
        if (measured > current)
        {
            set(measured);
            changes.Add(
                $"{assembly}: {metric} floor raised {RatchetCheck.Format(current)}% -> " +
                $"{RatchetCheck.Format(measured)}%.");
            return false;
        }

        if (measured >= current - Baseline.Tolerance)
        {
            // Within noise: leave the higher floor standing rather than sanding it down.
            return false;
        }

        if (!allowLower)
        {
            blocked.Add(
                $"{assembly}: {metric} coverage {RatchetCheck.Format(measured)}% is below the " +
                $"floor of {RatchetCheck.Format(current)}%.");
            return false;
        }

        set(measured);
        changes.Add(
            $"{assembly}: {metric} floor LOWERED {RatchetCheck.Format(current)}% -> " +
            $"{RatchetCheck.Format(measured)}%.");
        return true;
    }

    private static bool UpdateGui(
        List<string> changes,
        List<string> blocked,
        bool allowLower,
        Baseline.GuiFloor floor,
        GuiMetrics? metrics)
    {
        if (metrics is null)
        {
            // No GUI run in this invocation: leave the GUI floor exactly as committed.
            return false;
        }

        var passed = new HashSet<string>(metrics.WorkflowIds, StringComparer.Ordinal);
        var dropped = floor.WorkflowIds
            .Where(id => !passed.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        var fewer = metrics.WorkflowsPassed < floor.WorkflowsPassed;

        if ((dropped.Count > 0 || fewer) && !allowLower)
        {
            if (fewer)
            {
                blocked.Add(
                    $"GUI: {metrics.WorkflowsPassed} workflow(s) passed, below the floor of " +
                    $"{floor.WorkflowsPassed}.");
            }

            if (dropped.Count > 0)
            {
                blocked.Add($"GUI: workflow(s) {string.Join(", ", dropped)} no longer pass.");
            }

            return false;
        }

        var lowered = dropped.Count > 0 || fewer;
        var added = metrics.WorkflowIds
            .Where(id => !floor.WorkflowIds.Contains(id, StringComparer.Ordinal))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        if (metrics.WorkflowsPassed != floor.WorkflowsPassed)
        {
            changes.Add(
                $"GUI: workflows passed {(lowered ? "LOWERED" : "raised")} " +
                $"{floor.WorkflowsPassed} -> {metrics.WorkflowsPassed}.");
            floor.WorkflowsPassed = metrics.WorkflowsPassed;
        }

        if (added.Count > 0)
        {
            changes.Add($"GUI: workflow(s) {string.Join(", ", added)} added to the baseline.");
        }

        if (dropped.Count > 0)
        {
            changes.Add($"GUI: workflow(s) {string.Join(", ", dropped)} REMOVED from the baseline.");
        }

        floor.WorkflowIds = [.. metrics.WorkflowIds.Distinct(StringComparer.Ordinal).OrderBy(
            id => id,
            StringComparer.Ordinal)];
        return lowered;
    }

    /// <summary>
    /// Truncates to two decimals rather than rounding, so a recorded floor is never above the
    /// coverage that was actually measured.
    /// </summary>
    public static double FloorTo2(double value) => Math.Floor(value * 100) / 100;
}

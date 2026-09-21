using Napkin.Tools.Coverage;
using Napkin.Tools.Ratchet;

namespace Napkin.Tools.Tests;

public class RatchetCheckTests
{
    [Fact]
    public void PassesWhenCoverageMatchesTheFloor()
    {
        var result = RatchetCheck.Run(Baseline(80, 70), [Measured(80, 70)], guiMetrics: null);

        Assert.True(result.Passed);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void ADropWithinTheToleranceIsNoise()
    {
        // 0.1 percentage points: coverlet's own rounding, not a regression.
        var result = RatchetCheck.Run(Baseline(80, 70), [Measured(79.95, 70)], guiMetrics: null);

        Assert.True(result.Passed);
    }

    [Fact]
    public void ADropBeyondTheToleranceFails()
    {
        var result = RatchetCheck.Run(Baseline(80, 70), [Measured(79.5, 70)], guiMetrics: null);

        Assert.False(result.Passed);
        Assert.Contains("line coverage", Assert.Single(result.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void BranchCoverageIsGatedToo()
    {
        var result = RatchetCheck.Run(Baseline(80, 70), [Measured(80, 60)], guiMetrics: null);

        Assert.False(result.Passed);
        Assert.Contains("branch coverage", Assert.Single(result.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void AnAssemblyWithCoverableLinesButNoBaselineEntryFailsAndSaysWhatToRun()
    {
        var result = RatchetCheck.Run(
            new Baseline(),
            [new AssemblyCoverage("Napkin.Core.Geometry", 5, 10, 0, 0)],
            guiMetrics: null);

        Assert.False(result.Passed);
        Assert.Contains("ratchet update", Assert.Single(result.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void AnAssemblyWithNoCoverableLinesNeverFails()
    {
        var result = RatchetCheck.Run(
            new Baseline(),
            [new AssemblyCoverage("Napkin.Core.Geometry", 0, 0, 0, 0)],
            guiMetrics: null);

        Assert.True(result.Passed);
        Assert.Contains(
            result.Notices,
            notice => notice.Contains("N/A", StringComparison.Ordinal));
        Assert.True(Assert.Single(result.Rows).NotApplicable);
    }

    [Fact]
    public void ABaselinedAssemblyThatWasNotMeasuredAtAllFails()
    {
        var result = RatchetCheck.Run(Baseline(80, 70), [], guiMetrics: null);

        Assert.False(result.Passed);
        Assert.Contains(
            "no coverage was measured",
            Assert.Single(result.Failures),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ABranchFloorOfZeroDoesNotFailBranchlessCode()
    {
        var baseline = Baseline(80, 0);

        var result = RatchetCheck.Run(
            baseline,
            [new AssemblyCoverage("Napkin.Sample", 80, 100, 0, 0)],
            guiMetrics: null);

        Assert.True(result.Passed);
    }

    [Fact]
    public void WithoutGuiMetricsAndWithoutAGuiFloorTheCheckOnlyMentionsIt()
    {
        var result = RatchetCheck.Run(new Baseline(), [], guiMetrics: null);

        Assert.True(result.Passed);
        Assert.Contains("GUI", Assert.Single(result.Notices), StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutGuiMetricsButWithAGuiFloorTheCheckFails()
    {
        var baseline = new Baseline();
        baseline.Gui.WorkflowsPassed = 3;

        var result = RatchetCheck.Run(baseline, [], guiMetrics: null);

        Assert.False(result.Passed);
        Assert.Contains("gui-metrics.json", Assert.Single(result.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void FewerPassingWorkflowsThanTheFloorFails()
    {
        var baseline = new Baseline();
        baseline.Gui.WorkflowsPassed = 3;

        var result = RatchetCheck.Run(
            baseline,
            [],
            new GuiMetrics { WorkflowsPassed = 2, WorkflowIds = ["GUI-A", "GUI-B"] });

        Assert.False(result.Passed);
        Assert.Contains("below the floor", Assert.Single(result.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void ABaselinedWorkflowThatNoLongerPassesFails()
    {
        var baseline = new Baseline();
        baseline.Gui.WorkflowsPassed = 2;
        baseline.Gui.WorkflowIds = ["GUI-DRAW-001", "GUI-SAVE-001"];

        var result = RatchetCheck.Run(
            baseline,
            [],
            new GuiMetrics { WorkflowsPassed = 2, WorkflowIds = ["GUI-DRAW-001", "GUI-NEW-001"] });

        Assert.False(result.Passed);
        Assert.Contains("GUI-SAVE-001", Assert.Single(result.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void MoreWorkflowsThanTheFloorIsFine()
    {
        var baseline = new Baseline();
        baseline.Gui.WorkflowsPassed = 1;
        baseline.Gui.WorkflowIds = ["GUI-DRAW-001"];

        var result = RatchetCheck.Run(
            baseline,
            [],
            new GuiMetrics { WorkflowsPassed = 4, WorkflowIds = ["GUI-DRAW-001", "GUI-NEW-001"] });

        Assert.True(result.Passed);
    }

    private static Baseline Baseline(double line, double branch)
    {
        var baseline = new Ratchet.Baseline();
        baseline.Coverage["Napkin.Sample"] = new Ratchet.Baseline.CoverageFloor
        {
            Line = line,
            Branch = branch,
        };
        return baseline;
    }

    /// <summary>An assembly measured at exactly the given percentages, out of 10000 units.</summary>
    private static AssemblyCoverage Measured(double line, double branch) =>
        new("Napkin.Sample", (int)Math.Round(line * 100), 10000, (int)Math.Round(branch * 100), 10000);
}

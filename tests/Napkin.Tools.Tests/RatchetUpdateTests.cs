using Napkin.Tools.Coverage;
using Napkin.Tools.Ratchet;

namespace Napkin.Tools.Tests;

public class RatchetUpdateTests
{
    private static readonly DateOnly Today = new(2026, 9, 21);

    [Fact]
    public void AddsAnEntryForAnAssemblyThatHasNoneYet()
    {
        var baseline = new Baseline();

        var result = RatchetUpdate.Apply(
            baseline,
            [Measured(82.456, 61.5)],
            guiMetrics: null,
            allowLower: false,
            reason: null,
            Today);

        Assert.False(result.Blocked);
        var floor = baseline.Coverage["Napkin.Sample"];

        // Truncated, not rounded: a floor is never above what was measured.
        Assert.Equal(82.45, floor.Line);
        Assert.Equal(61.5, floor.Branch);
        Assert.Empty(baseline.Log);
    }

    [Fact]
    public void RaisesAFloorAndSaysSo()
    {
        var baseline = WithFloor(50, 40);

        var result = RatchetUpdate.Apply(
            baseline, [Measured(75, 60)], null, allowLower: false, reason: null, Today);

        Assert.False(result.Blocked);
        Assert.Equal(75, baseline.Coverage["Napkin.Sample"].Line);
        Assert.Equal(60, baseline.Coverage["Napkin.Sample"].Branch);
        Assert.Contains(result.Changes, change => change.Contains("raised", StringComparison.Ordinal));
        Assert.Empty(baseline.Log);
    }

    [Fact]
    public void RefusesToLowerAFloorWithoutPermission()
    {
        var baseline = WithFloor(80, 70);

        var result = RatchetUpdate.Apply(
            baseline, [Measured(60, 70)], null, allowLower: false, reason: null, Today);

        Assert.True(result.Blocked);
        Assert.Equal(80, baseline.Coverage["Napkin.Sample"].Line);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void LowersWithPermissionAndRecordsTheReason()
    {
        var baseline = WithFloor(80, 70);

        var result = RatchetUpdate.Apply(
            baseline,
            [Measured(60, 70)],
            null,
            allowLower: true,
            reason: "deleted the dead DXF writer and its tests",
            Today);

        Assert.False(result.Blocked);
        Assert.Equal(60, baseline.Coverage["Napkin.Sample"].Line);
        var entry = Assert.Single(baseline.Log);
        Assert.Equal("2026-09-21", entry.Date);
        Assert.Equal("deleted the dead DXF writer and its tests", entry.Reason);
    }

    [Fact]
    public void ADropWithinTheToleranceLeavesTheHigherFloorStanding()
    {
        var baseline = WithFloor(80, 70);

        var result = RatchetUpdate.Apply(
            baseline, [Measured(79.95, 70)], null, allowLower: false, reason: null, Today);

        Assert.False(result.Blocked);
        Assert.Equal(80, baseline.Coverage["Napkin.Sample"].Line);
        Assert.Empty(baseline.Log);
    }

    [Fact]
    public void AnAssemblyWithNoCoverableLinesIsNotWrittenToTheBaseline()
    {
        // Writing a 0/0 entry today would make the check pass trivially for ever. Leaving it out
        // is what makes the check demand a refresh on the first pull request that adds real code.
        var baseline = new Baseline();

        RatchetUpdate.Apply(
            baseline,
            [new AssemblyCoverage("Napkin.Core.Geometry", 0, 0, 0, 0)],
            null,
            allowLower: false,
            reason: null,
            Today);

        Assert.Empty(baseline.Coverage);
    }

    [Fact]
    public void RaisesTheGuiFloorFromAWorkflowRun()
    {
        var baseline = new Baseline();

        RatchetUpdate.Apply(
            baseline,
            [],
            new GuiMetrics { WorkflowsPassed = 2, WorkflowIds = ["GUI-B", "GUI-A"] },
            allowLower: false,
            reason: null,
            Today);

        Assert.Equal(2, baseline.Gui.WorkflowsPassed);
        Assert.Equal(["GUI-A", "GUI-B"], baseline.Gui.WorkflowIds);
    }

    [Fact]
    public void RefusesToDropAGuiWorkflowWithoutPermission()
    {
        var baseline = new Baseline();
        baseline.Gui.WorkflowsPassed = 2;
        baseline.Gui.WorkflowIds = ["GUI-A", "GUI-B"];

        var result = RatchetUpdate.Apply(
            baseline,
            [],
            new GuiMetrics { WorkflowsPassed = 2, WorkflowIds = ["GUI-A", "GUI-C"] },
            allowLower: false,
            reason: null,
            Today);

        Assert.True(result.Blocked);
        Assert.Equal(["GUI-A", "GUI-B"], baseline.Gui.WorkflowIds);
    }

    [Fact]
    public void WithoutAWorkflowRunTheGuiFloorIsLeftAlone()
    {
        var baseline = new Baseline();
        baseline.Gui.WorkflowsPassed = 5;
        baseline.Gui.WorkflowIds = ["GUI-A"];

        RatchetUpdate.Apply(baseline, [], null, allowLower: false, reason: null, Today);

        Assert.Equal(5, baseline.Gui.WorkflowsPassed);
        Assert.Equal(["GUI-A"], baseline.Gui.WorkflowIds);
    }

    [Fact]
    public void TheBaselineSurvivesASaveAndLoadUnchanged()
    {
        using var scratch = Fixture.NewDirectory();
        var path = Path.Combine(scratch.Path, "baseline.json");
        var baseline = WithFloor(82.45, 61.5);
        baseline.Gui.WorkflowsPassed = 1;
        baseline.Gui.WorkflowIds = ["GUI-A"];
        baseline.Log.Add(new Baseline.LogEntry { Date = "2026-09-21", Reason = "because" });

        baseline.Save(path);
        var reloaded = Baseline.Load(path);

        Assert.Equal(82.45, reloaded.Coverage["Napkin.Sample"].Line);
        Assert.Equal(61.5, reloaded.Coverage["Napkin.Sample"].Branch);
        Assert.Equal(1, reloaded.Gui.WorkflowsPassed);
        Assert.Equal(["GUI-A"], reloaded.Gui.WorkflowIds);
        Assert.Equal("because", Assert.Single(reloaded.Log).Reason);
        Assert.EndsWith("\n", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingBaselineFileIsAnEmptyBaselineRatherThanAnError()
    {
        using var scratch = Fixture.NewDirectory();

        var baseline = Baseline.Load(Path.Combine(scratch.Path, "nothing-here.json"));

        Assert.Empty(baseline.Coverage);
        Assert.Equal(Baseline.CurrentSchema, baseline.Schema);
    }

    [Fact]
    public void AnUnknownBaselineSchemaIsAnInputError()
    {
        using var scratch = Fixture.NewDirectory();
        var path = scratch.Write("baseline.json", """{ "schema": 99, "coverage": {} }""");

        var exception = Assert.Throws<InputException>(() => Baseline.Load(path));
        Assert.Contains("schema 99", exception.Message, StringComparison.Ordinal);
    }

    private static Baseline WithFloor(double line, double branch)
    {
        var baseline = new Baseline();
        baseline.Coverage["Napkin.Sample"] = new Baseline.CoverageFloor
        {
            Line = line,
            Branch = branch,
        };
        return baseline;
    }

    private static AssemblyCoverage Measured(double line, double branch) =>
        new("Napkin.Sample", (int)Math.Round(line * 1000), 100000,
            (int)Math.Round(branch * 1000), 100000);
}

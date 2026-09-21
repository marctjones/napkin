namespace Napkin.Tools.Features;

/// <summary>Where a catalogued feature stands, judged only by the tests that claim it.</summary>
public enum FeatureStatus
{
    /// <summary>No test carries the id at all.</summary>
    NotStarted,

    /// <summary>Every test carrying the id is skipped — a generated stub, or a deliberate skip.</summary>
    Planned,

    /// <summary>Some tests pass and some are skipped; none fail.</summary>
    Partial,

    /// <summary>At least one test passes and none fails or is skipped.</summary>
    Passing,

    /// <summary>At least one test carrying the id fails.</summary>
    Failing,
}

/// <summary>One line of the scorecard.</summary>
public sealed record FeatureRow(
    Feature Feature,
    FeatureStatus Status,
    IReadOnlyList<string> TestIds,
    int Passed,
    int Skipped,
    int Failed);

/// <summary>Counts for one area or milestone.</summary>
public sealed record Rollup(string Key, IReadOnlyDictionary<FeatureStatus, int> Counts)
{
    public int Total => Counts.Values.Sum();

    public int Of(FeatureStatus status) => Counts.TryGetValue(status, out var count) ? count : 0;

    /// <summary>Share of the group that is passing, in percentage points.</summary>
    public double PassingPercent => Total == 0 ? 0 : 100.0 * Of(FeatureStatus.Passing) / Total;
}

/// <summary>A feature id a test claims that the catalog does not define.</summary>
public sealed record OrphanClaim(string FeatureId, IReadOnlyList<string> TestIds);

/// <summary>The whole scorecard: rows, rollups and orphans.</summary>
public sealed record ScorecardResult(
    IReadOnlyList<FeatureRow> Rows,
    IReadOnlyList<Rollup> ByMilestone,
    IReadOnlyList<Rollup> ByArea,
    Rollup Overall,
    IReadOnlyList<OrphanClaim> Orphans,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Joins the catalog, the claims found in test source and the outcomes in the TRX files.
/// </summary>
/// <remarks>
/// This measures progress; it never gates. A feature with no test is information about what to
/// build next, not a fault, so <c>scorecard report</c> exits 0 whatever it finds.
/// </remarks>
public static class Scorecard
{
    /// <summary>Milestones sort M1..M5 then backlog, then anything unrecognised, alphabetically.</summary>
    private static readonly string[] MilestoneOrder = ["M1", "M2", "M3", "M4", "M5", "backlog"];

    public static ScorecardResult Build(
        Catalog catalog,
        IReadOnlyList<FeatureClaim> claims,
        TestResults results,
        IReadOnlyList<string> warnings)
    {
        var byFeature = claims
            .GroupBy(claim => claim.FeatureId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var rows = new List<FeatureRow>();
        foreach (var feature in catalog.Features)
        {
            var testIds = byFeature.TryGetValue(feature.Id, out var found)
                ? found.Select(claim => claim.TestId).Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal).ToList()
                : [];

            var passed = 0;
            var skipped = 0;
            var failed = 0;
            foreach (var testId in testIds)
            {
                switch (results.OutcomeOf(testId))
                {
                    case TestOutcome.Passed:
                        passed++;
                        break;
                    case TestOutcome.Failed:
                        failed++;
                        break;
                    default:
                        skipped++;
                        break;
                }
            }

            rows.Add(new FeatureRow(feature, Classify(testIds.Count, passed, skipped, failed),
                testIds, passed, skipped, failed));
        }

        var catalogued = catalog.Features
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.Ordinal);
        var orphans = byFeature
            .Where(pair => !catalogued.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new OrphanClaim(
                pair.Key,
                pair.Value.Select(claim => claim.TestId).Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal).ToList()))
            .ToList();

        return new ScorecardResult(
            rows,
            GroupBy(rows, row => Blank(row.Feature.Milestone), MilestoneRank),
            GroupBy(rows, row => Blank(row.Feature.Area), _ => 0),
            new Rollup("all", Count(rows)),
            orphans,
            warnings);
    }

    /// <summary>The status rules from issue #34, in one place.</summary>
    public static FeatureStatus Classify(int tests, int passed, int skipped, int failed)
    {
        if (tests == 0)
        {
            return FeatureStatus.NotStarted;
        }

        if (failed > 0)
        {
            return FeatureStatus.Failing;
        }

        if (passed == 0)
        {
            return FeatureStatus.Planned;
        }

        return skipped > 0 ? FeatureStatus.Partial : FeatureStatus.Passing;
    }

    private static string Blank(string value) =>
        string.IsNullOrWhiteSpace(value) ? "(unset)" : value;

    private static IReadOnlyList<Rollup> GroupBy(
        IReadOnlyList<FeatureRow> rows,
        Func<FeatureRow, string> key,
        Func<string, int> rank) =>
        rows.GroupBy(key, StringComparer.Ordinal)
            .Select(group => new Rollup(group.Key, Count(group.ToList())))
            .OrderBy(rollup => rank(rollup.Key))
            .ThenBy(rollup => rollup.Key, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyDictionary<FeatureStatus, int> Count(IReadOnlyList<FeatureRow> rows) =>
        Enum.GetValues<FeatureStatus>()
            .ToDictionary(status => status, status => rows.Count(row => row.Status == status));

    private static int MilestoneRank(string milestone)
    {
        var index = Array.IndexOf(MilestoneOrder, milestone);
        return index < 0 ? MilestoneOrder.Length : index;
    }
}

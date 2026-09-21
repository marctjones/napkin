using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Napkin.Tools.Features;

/// <summary>Renders a <see cref="ScorecardResult"/> as Markdown or as JSON.</summary>
public static class ScorecardWriter
{
    private static readonly FeatureStatus[] Order =
    [
        FeatureStatus.Passing,
        FeatureStatus.Partial,
        FeatureStatus.Planned,
        FeatureStatus.NotStarted,
        FeatureStatus.Failing,
    ];

    /// <summary>
    /// GitHub renders this straight into the job summary, so it is written to be read there:
    /// rollups first, the long per-feature table last and collapsed.
    /// </summary>
    public static string ToMarkdown(ScorecardResult result)
    {
        var markdown = new StringBuilder();
        markdown.Append("## Feature scorecard\n\n");
        markdown.Append(
            "How much of the design the test suite actually proves. This measures progress; it " +
            "never gates a pull request, a tag or a release.\n\n");

        if (result.Rows.Count == 0)
        {
            markdown.Append(
                "_No features are catalogued yet. Add them under `features/*.json`._\n\n");
        }
        else
        {
            markdown.Append(
                $"**{result.Overall.Of(FeatureStatus.Passing)} of {result.Overall.Total} " +
                $"features passing ({Percent(result.Overall.PassingPercent)}%).**\n\n");

            AppendRollup(markdown, "By milestone", result.ByMilestone);
            AppendRollup(markdown, "By area", result.ByArea);

            markdown.Append("<details>\n<summary>Every feature</summary>\n\n");
            markdown.Append("| Feature | Milestone | Area | Status | Tests | Title |\n");
            markdown.Append("|---|---|---|---|---|---|\n");
            foreach (var row in result.Rows)
            {
                markdown.Append(
                    $"| `{row.Feature.Id}` | {Escape(row.Feature.Milestone)} | " +
                    $"{Escape(row.Feature.Area)} | {Name(row.Status)} | {row.TestIds.Count} | " +
                    $"{Escape(row.Feature.Title)} |\n");
            }

            markdown.Append("\n</details>\n\n");
        }

        if (result.Orphans.Count > 0)
        {
            markdown.Append("### Feature ids claimed by tests but absent from the catalog\n\n");
            foreach (var orphan in result.Orphans)
            {
                markdown.Append($"- `{orphan.FeatureId}` — {string.Join(", ", orphan.TestIds)}\n");
            }

            markdown.Append('\n');
        }

        if (result.Warnings.Count > 0)
        {
            markdown.Append("### Catalog warnings\n\n");
            foreach (var warning in result.Warnings)
            {
                markdown.Append($"- {Escape(warning)}\n");
            }

            markdown.Append('\n');
        }

        return markdown.ToString();
    }

    /// <summary>The same content as data, for anything that wants to chart it over time.</summary>
    public static string ToJson(ScorecardResult result)
    {
        var document = new
        {
            schema = 1,
            overall = Counts(result.Overall),
            byMilestone = result.ByMilestone.Select(rollup => new
            {
                key = rollup.Key,
                counts = Counts(rollup),
            }),
            byArea = result.ByArea.Select(rollup => new
            {
                key = rollup.Key,
                counts = Counts(rollup),
            }),
            features = result.Rows.Select(row => new
            {
                id = row.Feature.Id,
                area = row.Feature.Area,
                milestone = row.Feature.Milestone,
                kind = row.Feature.Kind,
                issue = row.Feature.Issue,
                status = Name(row.Status),
                tests = row.TestIds,
                passed = row.Passed,
                skipped = row.Skipped,
                failed = row.Failed,
            }),
            orphans = result.Orphans.Select(orphan => new
            {
                id = orphan.FeatureId,
                tests = orphan.TestIds,
            }),
            warnings = result.Warnings,
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true })
            + "\n";
    }

    /// <summary>The status name as it appears in output: "Not started", not "NotStarted".</summary>
    public static string Name(FeatureStatus status) => status switch
    {
        FeatureStatus.NotStarted => "Not started",
        _ => status.ToString(),
    };

    private static object Counts(Rollup rollup) => new
    {
        total = rollup.Total,
        passing = rollup.Of(FeatureStatus.Passing),
        partial = rollup.Of(FeatureStatus.Partial),
        planned = rollup.Of(FeatureStatus.Planned),
        notStarted = rollup.Of(FeatureStatus.NotStarted),
        failing = rollup.Of(FeatureStatus.Failing),
        passingPercent = Math.Round(rollup.PassingPercent, 1),
    };

    private static void AppendRollup(StringBuilder markdown, string heading, IReadOnlyList<Rollup> rollups)
    {
        markdown.Append($"### {heading}\n\n");
        markdown.Append("| | Total | Passing | Partial | Planned | Not started | Failing | % passing |\n");
        markdown.Append("|---|---:|---:|---:|---:|---:|---:|---:|\n");
        foreach (var rollup in rollups)
        {
            markdown.Append($"| {Escape(rollup.Key)} | {rollup.Total} |");
            foreach (var status in Order)
            {
                markdown.Append($" {rollup.Of(status)} |");
            }

            markdown.Append($" {Percent(rollup.PassingPercent)}% |\n");
        }

        markdown.Append('\n');
    }

    private static string Percent(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>A title with a pipe in it must not break the table.</summary>
    private static string Escape(string? value) =>
        (value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal);
}

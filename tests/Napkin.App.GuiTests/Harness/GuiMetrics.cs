using System.Text.Json;
using System.Text.Json.Serialization;

namespace Napkin.App.GuiTests.Harness;

/// <summary>The shape written to <c>artifacts/gui-metrics.json</c>.</summary>
/// <param name="WorkflowsPassed">How many workflow scenarios passed.</param>
/// <param name="ActionsExecuted">
/// How many simulated input actions those workflows performed in total. Waits, window resizes and
/// assertions are not input and are not counted.
/// </param>
/// <param name="WorkflowIds">The feature ids of the passing workflows, sorted.</param>
public sealed record GuiMetricsDocument(
    [property: JsonPropertyName("workflowsPassed")] int WorkflowsPassed,
    [property: JsonPropertyName("actionsExecuted")] int ActionsExecuted,
    [property: JsonPropertyName("workflowIds")] IReadOnlyList<string> WorkflowIds);

/// <summary>
/// Collects what each passing workflow did and writes it where the coverage ratchet looks.
/// </summary>
/// <remarks>
/// <para>
/// Only workflows that ran to completion <em>and</em> satisfied <see cref="WorkflowRule"/> are
/// recorded, and only workflows against the application count: the harness's own self-tests do not
/// claim a feature id and are invisible here.
/// </para>
/// <para>
/// The file is rewritten after every workflow, so a run that is interrupted still leaves the truth
/// about what passed. Ids are sorted and each id counts once, so the same set of tests always
/// produces byte-identical output — which is what makes a ratchet comparison meaningful.
/// </para>
/// </remarks>
public static class GuiMetrics
{
    static readonly object Gate = new();
    static readonly SortedDictionary<string, int> Workflows = new(StringComparer.Ordinal);
    static bool _startedThisRun;

    // Line endings are pinned so the file is byte-identical on Windows and macOS runners.
    static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        NewLine = "\n",
    };

    /// <summary>Records a workflow that passed, and rewrites the metrics file.</summary>
    /// <param name="featureId">The feature the workflow claims, e.g. <c>GUI-SHELL-01</c>.</param>
    /// <param name="inputActions">How many simulated input actions it performed.</param>
    public static void RecordPassed(string featureId, int inputActions)
    {
        lock (Gate)
        {
            // The first record of a process discards whatever a previous run left behind, so a
            // stale file can never inflate the ratchet.
            if (!_startedThisRun)
            {
                _startedThisRun = true;
                Workflows.Clear();
            }

            Workflows[featureId] = inputActions;
            Write();
        }
    }

    /// <summary>The document as it stands, for the harness's own tests.</summary>
    public static GuiMetricsDocument Current
    {
        get
        {
            lock (Gate)
            {
                return Snapshot();
            }
        }
    }

    static GuiMetricsDocument Snapshot() => new(
        Workflows.Count,
        Workflows.Values.Sum(),
        Workflows.Keys.ToList());

    static void Write()
    {
        Directory.CreateDirectory(RepositoryLayout.ArtifactsDirectory);
        File.WriteAllText(
            RepositoryLayout.MetricsPath,
            JsonSerializer.Serialize(Snapshot(), Format) + "\n");
    }
}

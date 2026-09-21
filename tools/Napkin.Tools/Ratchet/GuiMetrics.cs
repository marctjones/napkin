using System.Text.Json;
using System.Text.Json.Serialization;

namespace Napkin.Tools.Ratchet;

/// <summary>
/// What a GUI workflow run reports back, written to `artifacts/gui-metrics.json` by the GUI
/// automation suite (issue #33). Absent on a unit-test-only run.
/// </summary>
public sealed class GuiMetrics
{
    [JsonPropertyName("workflowsPassed")]
    public int WorkflowsPassed { get; set; }

    [JsonPropertyName("actionsExecuted")]
    public int ActionsExecuted { get; set; }

    [JsonPropertyName("workflowIds")]
    public List<string> WorkflowIds { get; set; } = [];

    private static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Loads the metrics, or returns null when the GUI suite did not run.</summary>
    /// <exception cref="InputException">The file exists but is not readable.</exception>
    public static GuiMetrics? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GuiMetrics>(File.ReadAllText(path), Options)
                ?? throw new InputException($"{path}: the GUI metrics file is empty.");
        }
        catch (JsonException exception)
        {
            throw new InputException($"{path}: not readable as JSON — {exception.Message}");
        }
    }
}

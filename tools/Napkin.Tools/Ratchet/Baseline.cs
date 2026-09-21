using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Napkin.Tools.Ratchet;

/// <summary>
/// The committed floors: what coverage and how many GUI workflows the repository has already
/// achieved. `ratchet check` compares a run against this; `ratchet update` raises it.
/// </summary>
public sealed class Baseline
{
    /// <summary>The only schema version this tool understands.</summary>
    public const int CurrentSchema = 1;

    /// <summary>Coverage is compared in percentage points, so a floor of 82.5 means 82.5%.</summary>
    public const double Tolerance = 0.1;

    [JsonPropertyName("schema")]
    public int Schema { get; set; } = CurrentSchema;

    /// <summary>Assembly name to its line and branch floors, in percentage points.</summary>
    [JsonPropertyName("coverage")]
    public SortedDictionary<string, CoverageFloor> Coverage { get; set; } =
        new(StringComparer.Ordinal);

    [JsonPropertyName("gui")]
    public GuiFloor Gui { get; set; } = new();

    /// <summary>Every time a floor was lowered, with the reason given at the time.</summary>
    [JsonPropertyName("log")]
    public List<LogEntry> Log { get; set; } = [];

    private static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Loads the baseline, or returns an empty one when the file does not exist yet.</summary>
    /// <exception cref="InputException">The file exists but is not a schema 1 baseline.</exception>
    public static Baseline Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Baseline();
        }

        Baseline? baseline;
        try
        {
            baseline = JsonSerializer.Deserialize<Baseline>(File.ReadAllText(path), Options);
        }
        catch (JsonException exception)
        {
            throw new InputException($"{path}: not readable as JSON — {exception.Message}");
        }

        if (baseline is null)
        {
            throw new InputException($"{path}: the baseline is empty.");
        }

        if (baseline.Schema != CurrentSchema)
        {
            throw new InputException(
                $"{path}: schema {baseline.Schema} is not supported; this tool reads schema {CurrentSchema}.");
        }

        baseline.Coverage = new SortedDictionary<string, CoverageFloor>(
            baseline.Coverage,
            StringComparer.Ordinal);
        return baseline;
    }

    /// <summary>Writes the baseline as UTF-8 JSON with a trailing newline, sorted for a clean diff.</summary>
    public void Save(string path)
    {
        Gui.WorkflowIds.Sort(StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(this, Options) + "\n";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, json.ReplaceLineEndings("\n"), new UTF8Encoding(false));
    }

    /// <summary>One assembly's floors, in percentage points.</summary>
    public sealed class CoverageFloor
    {
        [JsonPropertyName("line")]
        public double Line { get; set; }

        [JsonPropertyName("branch")]
        public double Branch { get; set; }
    }

    /// <summary>What the GUI workflow suite has already proved (issue #33).</summary>
    public sealed class GuiFloor
    {
        [JsonPropertyName("workflowsPassed")]
        public int WorkflowsPassed { get; set; }

        [JsonPropertyName("workflowIds")]
        public List<string> WorkflowIds { get; set; } = [];

        /// <summary>True when a run with no GUI metrics at all should fail the check.</summary>
        [JsonIgnore]
        public bool RequiresWorkflows => WorkflowsPassed > 0 || WorkflowIds.Count > 0;
    }

    /// <summary>A deliberate lowering of a floor, kept so the history is visible in the file.</summary>
    public sealed class LogEntry
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}

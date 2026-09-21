using System.Text.Json;
using System.Text.Json.Serialization;

namespace Napkin.Tools.Features;

/// <summary>One catalogued feature: a thing the design promises, with a test that will prove it.</summary>
public sealed class Feature
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("acceptance")]
    public string Acceptance { get; set; } = string.Empty;

    /// <summary>M1-M5, or "backlog".</summary>
    [JsonPropertyName("milestone")]
    public string Milestone { get; set; } = string.Empty;

    [JsonPropertyName("issue")]
    public int? Issue { get; set; }

    /// <summary>unit, golden or workflow.</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;
}

/// <summary>The merge of every `features/*.json`.</summary>
public sealed class Catalog
{
    public const int CurrentSchema = 1;

    [JsonPropertyName("schema")]
    public int Schema { get; set; } = CurrentSchema;

    [JsonPropertyName("features")]
    public List<Feature> Features { get; set; } = [];

    private static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads and merges every `*.json` in <paramref name="directory"/>, in file-name order. A
    /// missing directory is an empty catalog, not an error: the catalog lands in its own pull
    /// request and the tool has to be usable before it does.
    /// </summary>
    /// <param name="warnings">Duplicate ids and the like — never fatal, the scorecard never gates.</param>
    /// <exception cref="InputException">A file is not readable as a schema 1 catalog.</exception>
    public static Catalog Load(string directory, out IReadOnlyList<string> warnings)
    {
        var notes = new List<string>();
        var merged = new Catalog();
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!Directory.Exists(directory))
        {
            warnings = notes;
            return merged;
        }

        var files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);

        foreach (var file in files)
        {
            Catalog? part;
            try
            {
                part = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(file), Options);
            }
            catch (JsonException exception)
            {
                throw new InputException($"{file}: not readable as JSON — {exception.Message}");
            }

            if (part is null)
            {
                throw new InputException($"{file}: the catalog file is empty.");
            }

            if (part.Schema != CurrentSchema)
            {
                throw new InputException(
                    $"{file}: schema {part.Schema} is not supported; this tool reads schema " +
                    $"{CurrentSchema}.");
            }

            var name = Path.GetFileName(file);
            foreach (var feature in part.Features)
            {
                if (string.IsNullOrWhiteSpace(feature.Id))
                {
                    notes.Add($"{name}: a feature has no id and was ignored.");
                    continue;
                }

                if (seen.TryGetValue(feature.Id, out var firstFile))
                {
                    notes.Add(
                        $"{name}: feature {feature.Id} is already defined in {firstFile}; the " +
                        "first definition wins.");
                    continue;
                }

                seen[feature.Id] = name;
                merged.Features.Add(feature);
            }
        }

        merged.Features.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        warnings = notes;
        return merged;
    }
}

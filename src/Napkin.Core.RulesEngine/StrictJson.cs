using System.Globalization;
using System.Text.Json;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>Where a problem was found: file, table and row or operation.</summary>
internal readonly record struct Where(string File, string? Table = null, string? Row = null);

/// <summary>Collects every problem in a load, so a transcriber fixes a pack in one pass (design §9.2).</summary>
internal sealed class ProblemList
{
    private readonly List<PackProblem> items = [];

    public int Count => items.Count;

    public IReadOnlyList<PackProblem> Items => items;

    public void Add(Where where, string message) => items.Add(new PackProblem(where.File, where.Table, where.Row, message));
}

/// <summary>
/// Strict reading of one JSON object: duplicate keys, unknown fields, non-integer numbers,
/// off-grid lengths and unknown enum values are all problems, never guesses (design §1.5, §9.1).
/// </summary>
internal sealed class JsonObj
{
    private readonly Dictionary<string, JsonElement> properties = new(StringComparer.Ordinal);
    private readonly HashSet<string> used = new(StringComparer.Ordinal);
    private readonly string path;

    private JsonObj(JsonElement element, string path, Where where, ProblemList problems)
    {
        this.path = path;
        Where = where;
        Problems = problems;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
            {
                problems.Add(where, $"{Child(property.Name)}: duplicate key; each field may appear once.");
            }
        }
    }

    /// <summary>Where problems are reported; narrowed once a table's designation is known.</summary>
    public Where Where { get; set; }

    public ProblemList Problems { get; }

    public static JsonObj? Create(JsonElement element, string path, Where where, ProblemList problems)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            problems.Add(where, $"{Display(path)}: must be a JSON object.");
            return null;
        }

        return new JsonObj(element, path, where, problems);
    }

    public string Child(string name) => path.Length == 0 ? name : $"{path}.{name}";

    public bool Has(string name) => properties.ContainsKey(name);

    public IEnumerable<string> Names => properties.Keys;

    public JsonElement? Get(string name, bool required = true)
    {
        used.Add(name);
        if (properties.TryGetValue(name, out JsonElement value))
        {
            return value;
        }

        if (required)
        {
            Problems.Add(Where, $"{Child(name)}: missing required field.");
        }

        return null;
    }

    public void MarkUsed(string name) => used.Add(name);

    public string? String(string name, bool required = true, bool nullable = false)
    {
        JsonElement? e = Get(name, required);
        return e is null ? null : ReadString(e.Value, Child(name), Where, Problems, nullable);
    }

    public int? Int(string name, int min = 0)
    {
        JsonElement? e = Get(name);
        return e is null ? null : ReadInt(e.Value, Child(name), Where, Problems, min);
    }

    public DateOnly? Date(string name, bool nullable = false)
    {
        JsonElement? e = Get(name);
        if (e is null || (nullable && e.Value.ValueKind == JsonValueKind.Null))
        {
            return null;
        }

        string? text = ReadString(e.Value, Child(name), Where, Problems, nullable: false);
        if (text is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            return date;
        }

        Problems.Add(Where, $"{Child(name)}: '{text}' is not a date written yyyy-MM-dd.");
        return null;
    }

    public T? Enum<T>(string name, IReadOnlyDictionary<string, T> map)
        where T : struct
    {
        JsonElement? e = Get(name);
        return e is null ? null : ReadEnum(e.Value, Child(name), Where, Problems, map);
    }

    public JsonObj? Obj(string name, bool required = true)
    {
        JsonElement? e = Get(name, required);
        return e is null ? null : Create(e.Value, Child(name), Where, Problems);
    }

    public IReadOnlyList<JsonElement>? Array(string name, int minItems = 0, bool required = true)
    {
        JsonElement? e = Get(name, required);
        if (e is null)
        {
            return null;
        }

        if (e.Value.ValueKind != JsonValueKind.Array)
        {
            Problems.Add(Where, $"{Child(name)}: must be a JSON array.");
            return null;
        }

        List<JsonElement> items = [.. e.Value.EnumerateArray()];
        if (items.Count < minItems)
        {
            Problems.Add(Where, $"{Child(name)}: must have at least {minItems} item(s).");
            return null;
        }

        return items;
    }

    /// <summary>Reports every field that was never read: unknown fields are a load error (design §1.6).</summary>
    public void Done()
    {
        foreach (string name in properties.Keys.Where(n => !used.Contains(n)).OrderBy(n => n, StringComparer.Ordinal))
        {
            Problems.Add(Where, $"{Child(name)}: unknown field.");
        }
    }

    public static string Display(string path) => path.Length == 0 ? "(file)" : path;

    public static string? ReadString(JsonElement e, string path, Where where, ProblemList problems, bool nullable = false)
    {
        if (e.ValueKind == JsonValueKind.String)
        {
            string value = e.GetString()!;
            if (value.Length == 0)
            {
                problems.Add(where, $"{Display(path)}: must not be empty.");
                return null;
            }

            return value;
        }

        if (nullable && e.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        problems.Add(where, $"{Display(path)}: must be a string{(nullable ? " or null" : string.Empty)}.");
        return null;
    }

    /// <summary>
    /// A whole number written without a decimal point or exponent. <c>6.0</c> is a load error, not
    /// a rounding (design §1.5).
    /// </summary>
    public static int? ReadInt(JsonElement e, string path, Where where, ProblemList problems, int min = 0)
    {
        if (e.ValueKind != JsonValueKind.Number)
        {
            problems.Add(where, $"{Display(path)}: must be a whole number.");
            return null;
        }

        string raw = e.GetRawText();
        bool digitsOnly = raw.Length > 0 && raw.Select((c, i) => char.IsAsciiDigit(c) || (i == 0 && c == '-')).All(ok => ok);
        if (!digitsOnly || !e.TryGetInt32(out int value))
        {
            problems.Add(where, $"{Display(path)}: must be a whole number written without a decimal point or exponent (found {raw}).");
            return null;
        }

        if (value < min)
        {
            problems.Add(where, $"{Display(path)}: must be at least {min} (found {value}).");
            return null;
        }

        return value;
    }

    /// <summary>
    /// An exact length in feet-inch text ("6ft 0in", "3-1/2in"). Text that is not understood, is
    /// negative, or does not land on the 1/1024″ grid is a load error (design §1.5).
    /// </summary>
    public static Length? ReadLength(JsonElement e, string path, Where where, ProblemList problems)
    {
        string? text = ReadString(e, path, where, problems);
        if (text is null)
        {
            return null;
        }

        if (!Length.TryParse(text, out Length value, out bool wasRounded))
        {
            problems.Add(where, $"{Display(path)}: '{text}' is not a length (write it like \"6ft 0in\" or \"3-1/2in\").");
            return null;
        }

        if (wasRounded)
        {
            problems.Add(where, $"{Display(path)}: '{text}' is not exact on the 1/1024-inch grid; pack lengths must be exact.");
            return null;
        }

        if (value < Length.Zero)
        {
            problems.Add(where, $"{Display(path)}: '{text}' is negative.");
            return null;
        }

        return value;
    }

    public static T? ReadEnum<T>(JsonElement e, string path, Where where, ProblemList problems, IReadOnlyDictionary<string, T> map)
        where T : struct
    {
        string? text = ReadString(e, path, where, problems);
        if (text is null)
        {
            return null;
        }

        if (map.TryGetValue(text, out T value))
        {
            return value;
        }

        string allowed = string.Join(", ", map.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => $"'{k}'"));
        problems.Add(where, $"{Display(path)}: '{text}' is not one of {allowed}.");
        return null;
    }
}

/// <summary>Reads a whole pack file as JSON, with size and depth limits and strict syntax.</summary>
internal static class JsonFile
{
    public static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32,
    };

    public static JsonDocument? Parse(IPackSource source, string file, long maxBytes, ProblemList problems)
    {
        Where where = new(file);
        long? length = source.FileLength(file);
        if (length is null)
        {
            problems.Add(where, "file not found.");
            return null;
        }

        if (length.Value > maxBytes)
        {
            problems.Add(where, $"file is {length.Value} bytes; a pack file may be at most {maxBytes} bytes.");
            return null;
        }

        try
        {
            byte[] bytes = source.ReadFile(file);
            ReadOnlyMemory<byte> json = bytes.AsMemory();
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                json = json[3..]; // a UTF-8 byte-order mark, as some Windows editors write
            }

            return JsonDocument.Parse(json, Options);
        }
        catch (JsonException ex)
        {
            problems.Add(where, $"not valid JSON: {ex.Message}");
            return null;
        }
    }
}

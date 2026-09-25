using System.Text.Json;

namespace Napkin.Core.Materials;

/// <summary>
/// The fields of one JSON object, with each one taken at most once so that whatever is left over
/// at the end is exactly the set of fields this format does not define.
/// </summary>
/// <remarks>
/// <see cref="JsonDocument"/> keeps both halves of a repeated field rather than rejecting it, so
/// duplicate detection has to happen here: <see cref="TryAdd"/> refuses the second one. Same
/// technique as <c>Napkin.Core.Project</c>'s scene binder, for the same reason.
/// </remarks>
internal sealed class JsonFields(string path)
{
    private readonly Dictionary<string, JsonElement> _fields = new(StringComparer.Ordinal);
    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    /// <summary>Where this object is in the document.</summary>
    public string Path { get; } = path;

    /// <summary>The fields nothing asked for — every one of them an unknown field.</summary>
    public IEnumerable<string> Unused => _fields.Keys.Where(name => !_taken.Contains(name)).Order(StringComparer.Ordinal);

    /// <summary>Records a field, or reports that it is the second one of its name.</summary>
    public bool TryAdd(JsonProperty property) => _fields.TryAdd(property.Name, property.Value);

    /// <summary>Takes a field, marking it as one this format defines.</summary>
    public JsonElement? Take(string name)
    {
        _taken.Add(name);
        return _fields.TryGetValue(name, out JsonElement element) ? element : null;
    }
}

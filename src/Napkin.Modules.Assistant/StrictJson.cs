using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Napkin.Modules.Assistant;

/// <summary>
/// The one strict reading both proposal parsers share (docs/design/llm-assistant.md &#xA7;4.3): an
/// object must have exactly the members its schema names, each once — an unknown member, a missing
/// one or one written twice refuses the whole reply, and nothing is defaulted.
/// </summary>
internal static class StrictJson
{
    /// <summary>An object's members, when it has exactly <paramref name="expected"/>, each once.</summary>
    /// <param name="element">The JSON value that should be the object.</param>
    /// <param name="expected">The members it must have, and no other.</param>
    /// <param name="where">What the object is, for the reason: "the reply", "part 3", "edit 2".</param>
    /// <param name="members">The members by name, when it has exactly those.</param>
    /// <param name="why">Why it was refused, in napkin's words, when it was.</param>
    public static bool TryMembers(
        JsonElement element,
        IReadOnlyList<string> expected,
        string where,
        [NotNullWhen(true)] out Dictionary<string, JsonElement>? members,
        [NotNullWhen(false)] out string? why)
    {
        members = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            why = $"{where} is not an object";
            return false;
        }

        Dictionary<string, JsonElement> found = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!expected.Contains(property.Name, StringComparer.Ordinal))
            {
                why = $"{where} has a member napkin does not know: \"{property.Name}\"";
                return false;
            }

            if (!found.TryAdd(property.Name, property.Value))
            {
                why = $"{where} has \"{property.Name}\" twice";
                return false;
            }
        }

        if (expected.FirstOrDefault(name => !found.ContainsKey(name)) is { } missing)
        {
            why = $"{where} has no \"{missing}\"";
            return false;
        }

        members = found;
        why = null;
        return true;
    }
}

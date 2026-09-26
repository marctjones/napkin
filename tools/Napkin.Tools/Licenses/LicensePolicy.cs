using System.Text.Json;

namespace Napkin.Tools.Licenses;

/// <summary>One package the allowlist alone cannot pass, allowed by a person who read its license.</summary>
/// <param name="Package">The package id.</param>
/// <param name="Version">The exact version read; a new version is a new read.</param>
/// <param name="License">What the license text says it is, as read.</param>
/// <param name="Justification">Why it is allowed, in words.</param>
public sealed record LicenseException(string Package, string Version, string License, string Justification);

/// <summary>
/// DESIGN.md §2.1 as data (#2): the SPDX identifiers napkin's dependencies may carry, and the
/// exceptions, each with a written reason, committed in <c>licenses/policy.json</c>.
/// </summary>
public sealed record LicensePolicy(IReadOnlySet<string> Allowed, IReadOnlyList<LicenseException> Exceptions)
{
    /// <summary>Where the policy lives, relative to the repository root.</summary>
    public const string RelativePath = "licenses/policy.json";

    /// <summary>The exception for a package at a version, or null.</summary>
    public LicenseException? ExceptionFor(string package, string version) =>
        Exceptions.FirstOrDefault(exception =>
            string.Equals(exception.Package, package, StringComparison.OrdinalIgnoreCase) && exception.Version == version);

    /// <summary>Reads and checks the policy file; every exception needs a license and a justification.</summary>
    public static LicensePolicy Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new InputException($"no license policy at {path}.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            HashSet<string> allowed = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement id in root.GetProperty("allowed").EnumerateArray())
            {
                allowed.Add(id.GetString()!);
            }

            List<LicenseException> exceptions = [];
            foreach (JsonElement entry in root.GetProperty("exceptions").EnumerateArray())
            {
                LicenseException exception = new(
                    Text(entry, "package"),
                    Text(entry, "version"),
                    Text(entry, "license"),
                    Text(entry, "justification"));
                exceptions.Add(exception);
            }

            return new LicensePolicy(allowed, exceptions);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InputException($"{path} is not a license policy: {exception.Message}");
        }
    }

    static string Text(JsonElement entry, string name)
    {
        string value = entry.GetProperty(name).GetString() ?? string.Empty;
        return value.Trim().Length > 0
            ? value
            : throw new InvalidOperationException($"an exception has an empty \"{name}\"");
    }
}

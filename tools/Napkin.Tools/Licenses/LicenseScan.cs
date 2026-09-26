using System.Text.Json;
using System.Text.RegularExpressions;

namespace Napkin.Tools.Licenses;

/// <summary>What a package says about its license in its nuspec.</summary>
public enum DeclaredKind
{
    /// <summary>An SPDX expression (<c>&lt;license type="expression"&gt;</c>).</summary>
    Expression,

    /// <summary>A file inside the package (<c>&lt;license type="file"&gt;</c>): someone must read it.</summary>
    File,

    /// <summary>Only a deprecated <c>&lt;licenseUrl&gt;</c>: someone must read what it points at.</summary>
    Url,

    /// <summary>Nothing at all, or no nuspec found in any package folder.</summary>
    None,
}

/// <summary>One restored package and the license it declares.</summary>
/// <param name="Id">The package id, as the assets file spells it.</param>
/// <param name="Version">The resolved version.</param>
/// <param name="Kind">How the license is declared.</param>
/// <param name="Value">The expression, the file name or the URL; empty for none.</param>
/// <param name="Projects">The projects that restore it, repository-relative, sorted.</param>
public sealed record PackageLicense(string Id, string Version, DeclaredKind Kind, string Value, IReadOnlyList<string> Projects)
{
    /// <summary>How the declaration reads in a report: "MIT", "file LICENSE", "url https://…", "none".</summary>
    public string Declared => Kind switch
    {
        DeclaredKind.Expression => Value,
        DeclaredKind.File => $"file {Value}",
        DeclaredKind.Url => $"url {Value}",
        _ => "none",
    };
}

/// <summary>
/// Every NuGet package the repository restores, direct and transitive (#2): read from each project's
/// <c>obj/project.assets.json</c> under <c>src</c>, <c>tests</c> and <c>tools</c>, and each package's
/// license from its nuspec in the restore's package folders.
/// </summary>
/// <remarks>
/// The assets file is what the build actually resolved, so a transitive package the project file
/// never names is still seen. It exists only after a restore, which is why the gate runs this after
/// the build.
/// </remarks>
public static class LicenseScan
{
    /// <summary>The directories scanned; anything else under the root (worktrees, artifacts) is not the build.</summary>
    public static readonly IReadOnlyList<string> ScannedDirectories = ["src", "tests", "tools"];

    static readonly Regex LicenseElement = new("<license\\s+type=\"(?<type>[^\"]+)\"[^>]*>(?<value>[^<]*)</license>", RegexOptions.CultureInvariant);
    static readonly Regex LicenseUrlElement = new("<licenseUrl>(?<value>[^<]*)</licenseUrl>", RegexOptions.CultureInvariant);

    /// <summary>The packages, by id then version.</summary>
    /// <param name="root">The repository root.</param>
    public static IReadOnlyList<PackageLicense> Of(string root)
    {
        Dictionary<(string Id, string Version), (DeclaredKind Kind, string Value, SortedSet<string> Projects)> found = [];
        int assetsFiles = 0;
        foreach (string directory in ScannedDirectories.Select(name => Path.Combine(root, name)).Where(Directory.Exists))
        {
            foreach (string assets in Directory.EnumerateFiles(directory, "project.assets.json", SearchOption.AllDirectories)
                         .Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == "obj"))
            {
                assetsFiles++;
                string project = Path.GetRelativePath(root, Path.GetDirectoryName(Path.GetDirectoryName(assets))!).Replace('\\', '/');
                using JsonDocument document = ReadJson(assets);
                JsonElement top = document.RootElement;
                string[] folders = top.TryGetProperty("packageFolders", out JsonElement packageFolders)
                    ? [.. packageFolders.EnumerateObject().Select(folder => folder.Name)]
                    : [];
                if (!top.TryGetProperty("libraries", out JsonElement libraries))
                {
                    throw new InputException($"{assets} has no \"libraries\"; restore again.");
                }

                foreach (JsonProperty library in libraries.EnumerateObject())
                {
                    if (library.Value.GetProperty("type").GetString() != "package")
                    {
                        continue;
                    }

                    string[] idAndVersion = library.Name.Split('/');
                    (string id, string version) = (idAndVersion[0], idAndVersion[1]);
                    if (!found.TryGetValue((id, version), out var entry))
                    {
                        string path = library.Value.GetProperty("path").GetString()!;
                        (DeclaredKind kind, string value) = Declaration(folders, path, id);
                        entry = (kind, value, []);
                        found[(id, version)] = entry;
                    }

                    entry.Projects.Add(project);
                }
            }
        }

        if (assetsFiles == 0)
        {
            throw new InputException($"no obj/project.assets.json under {string.Join(", ", ScannedDirectories)}; run `dotnet restore napkin.sln` first.");
        }

        return
        [
            .. found
                .OrderBy(pair => pair.Key.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.Key.Version, StringComparer.Ordinal)
                .Select(pair => new PackageLicense(pair.Key.Id, pair.Key.Version, pair.Value.Kind, pair.Value.Value, [.. pair.Value.Projects])),
        ];
    }

    /// <summary>What one nuspec declares, from the first package folder that holds it.</summary>
    internal static (DeclaredKind Kind, string Value) Declaration(IEnumerable<string> folders, string packagePath, string id)
    {
        foreach (string folder in folders)
        {
            string nuspec = Path.Combine(folder, packagePath, id.ToLowerInvariant() + ".nuspec");
            if (!File.Exists(nuspec))
            {
                continue;
            }

            return Parse(File.ReadAllText(nuspec));
        }

        return (DeclaredKind.None, string.Empty);
    }

    /// <summary>What a nuspec's text declares: the license element wins over the deprecated URL.</summary>
    public static (DeclaredKind Kind, string Value) Parse(string nuspec)
    {
        Match license = LicenseElement.Match(nuspec);
        if (license.Success)
        {
            string value = license.Groups["value"].Value.Trim();
            return license.Groups["type"].Value switch
            {
                "expression" => (DeclaredKind.Expression, value),
                "file" => (DeclaredKind.File, value),
                _ => (DeclaredKind.None, string.Empty),
            };
        }

        Match url = LicenseUrlElement.Match(nuspec);
        return url.Success && url.Groups["value"].Value.Trim() is { Length: > 0 } link
            ? (DeclaredKind.Url, link)
            : (DeclaredKind.None, string.Empty);
    }

    static JsonDocument ReadJson(string path)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new InputException($"{path} is not valid JSON: {exception.Message}");
        }
    }
}

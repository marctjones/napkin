using System.Reflection;
using System.Text.RegularExpressions;

namespace Napkin.Core.Geometry.Tests;

/// <summary>The version is set in one place and every assembly reports it (DESIGN.md §12, #30).</summary>
public class VersionTests
{
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "napkin.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException($"napkin.sln not found above {AppContext.BaseDirectory}.");
    }

    private static string PropsText() => File.ReadAllText(Path.Combine(RepositoryRoot(), "Directory.Build.props"));

    [Fact]
    [Trait("Feature", "REL-004")]
    public void Version_is_set_in_exactly_one_place()
    {
        var props = PropsText();
        Assert.Matches(@"<VersionPrefix>0\.\d+\.0</VersionPrefix>", props);
        Assert.Contains("<VersionSuffix>beta</VersionSuffix>", props);

        var separator = Path.DirectorySeparatorChar;
        var strays = Directory.EnumerateFiles(RepositoryRoot(), "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{separator}obj{separator}") && !p.Contains($"{separator}bin{separator}"))
            .Where(p => Regex.IsMatch(
                File.ReadAllText(p),
                @"<(Version|VersionPrefix|VersionSuffix|AssemblyVersion|FileVersion|InformationalVersion)>"))
            .ToList();

        Assert.Empty(strays);
    }

    [Fact]
    [Trait("Feature", "REL-004")]
    public void Assembly_reports_the_repository_version()
    {
        var prefix = Regex.Match(PropsText(), @"<VersionPrefix>([^<]+)</VersionPrefix>").Groups[1].Value;
        var info = typeof(Length).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.StartsWith($"{prefix}-beta", info);

        // CI passes the commit SHA as SourceRevisionId; locally there is none.
        var sha = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(sha))
        {
            Assert.Contains("+" + sha[..7], info);
        }
    }
}

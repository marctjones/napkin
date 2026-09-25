using System.Text.RegularExpressions;

namespace Napkin.Tools.Tests;

/// <summary>
/// macOS 26 kills, at launch and with no message (exit 137), an executable FILE whose name ends in
/// ".app": it takes it for a malformed application bundle. The same bytes under any other name
/// run. napkin's first downloadable build died this way, with every CI check green (issue #56),
/// so the rule is pinned here where a rename cannot quietly undo it.
/// </summary>
public partial class ExecutableNameTests
{
    [GeneratedRegex(@"<OutputType>\s*(Exe|WinExe)\s*</OutputType>")]
    private static partial Regex ExecutableOutput();

    [GeneratedRegex(@"<AssemblyName>\s*([^<]+?)\s*</AssemblyName>")]
    private static partial Regex ExplicitAssemblyName();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "napkin.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"napkin.sln not found above {AppContext.BaseDirectory}.");
    }

    [Fact]
    public void NoProgramIsNamedLikeAnApplicationBundle()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();
        var checkedPrograms = 0;

        foreach (var folder in new[] { "src", "tools" })
        {
            foreach (var project in Directory.EnumerateFiles(
                         Path.Combine(root, folder), "*.csproj", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(project);
                if (!ExecutableOutput().IsMatch(text))
                {
                    continue;
                }

                checkedPrograms++;
                var match = ExplicitAssemblyName().Match(text);
                var name = match.Success ? match.Groups[1].Value : Path.GetFileNameWithoutExtension(project);
                if (name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Path.GetRelativePath(root, project)} builds an executable named '{name}'.");
                }
            }
        }

        Assert.True(checkedPrograms >= 2, "Expected to find at least the app and the tools executable.");
        Assert.Empty(offenders);
    }

    [Fact]
    public void TheAppExecutableIsCalledNapkin()
    {
        var project = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "src", "Napkin.App", "Napkin.App.csproj"));

        Assert.Equal("napkin", ExplicitAssemblyName().Match(project).Groups[1].Value);
    }
}

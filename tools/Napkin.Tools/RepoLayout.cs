namespace Napkin.Tools;

/// <summary>
/// Where the tool looks for things, relative to the repository root. Every path is a default
/// that a command-line option can override, so the tool is testable against fixture trees.
/// </summary>
public sealed class RepoLayout
{
    /// <summary>The file that marks the repository root.</summary>
    public const string RootMarker = "napkin.sln";

    public RepoLayout(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    /// <summary>The committed coverage floors (issue #32).</summary>
    public string BaselinePath => Path.Combine(Root, "ratchet", "baseline.json");

    /// <summary>Where `dotnet test --results-directory` is told to write TRX and coverage.</summary>
    public string TestResultsDirectory => Path.Combine(Root, "artifacts", "test-results");

    /// <summary>Written by the GUI workflow run (issue #33); absent on a unit-test-only run.</summary>
    public string GuiMetricsPath => Path.Combine(Root, "artifacts", "gui-metrics.json");

    /// <summary>The feature catalog: every `features/*.json` merged (issue #34).</summary>
    public string CatalogDirectory => Path.Combine(Root, "features");

    /// <summary>The source tree scanned for `[Trait("Feature", "...")]`.</summary>
    public string TestsDirectory => Path.Combine(Root, "tests");

    /// <summary>The generated skip stubs — the checklist of features nothing tests yet.</summary>
    public string StubsPath =>
        Path.Combine(Root, "tests", "Napkin.Features.Tests", "PlannedFeatures.g.cs");

    /// <summary>
    /// Walks up from <paramref name="start"/> until it finds the directory holding
    /// <see cref="RootMarker"/>. Returns null when there is none.
    /// </summary>
    public static RepoLayout? Discover(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, RootMarker)))
            {
                return new RepoLayout(directory.FullName);
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Makes a path readable in a message by trimming the repository root off it and using `/`
    /// throughout, so a message reads the same on Windows as everywhere else (#181's CI caught
    /// this: a test asserted the `/`-separated form a Unix runner had printed).
    /// </summary>
    public string Relative(string path)
    {
        var full = Path.GetFullPath(path);
        var trimmed = full.StartsWith(Root, StringComparison.Ordinal)
            ? full[Root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : full;
        return trimmed.Replace(Path.DirectorySeparatorChar, '/');
    }
}

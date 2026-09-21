namespace Napkin.App.GuiTests.Harness;

/// <summary>
/// Finds the places in the repository that a GUI test run writes to. The test binary lives deep
/// under <c>tests/…/bin/Debug/net10.0</c>, so the root is found by walking up to the solution file
/// rather than being configured anywhere.
/// </summary>
public static class RepositoryLayout
{
    static readonly Lazy<string> Root = new(FindRoot);

    /// <summary>The repository root — the directory holding <c>napkin.sln</c>.</summary>
    public static string RepositoryRoot => Root.Value;

    /// <summary>The <c>artifacts/</c> directory. Build output, never committed.</summary>
    public static string ArtifactsDirectory => Path.Combine(RepositoryRoot, "artifacts");

    /// <summary>Where rendered frames are written, for CI to upload.</summary>
    public static string FramesDirectory => Path.Combine(ArtifactsDirectory, "gui-frames");

    /// <summary>Where this run's workflow metrics are written, for the ratchet to check.</summary>
    public static string MetricsPath => Path.Combine(ArtifactsDirectory, "gui-metrics.json");

    /// <summary>
    /// The committed <c>samples/</c> directory, which holds each fixture's scene file, its design
    /// document and the hand-derived <c>*.expected.json</c> a test asserts against.
    /// </summary>
    /// <remarks>
    /// The <em>scene</em> files a workflow opens come from beside the application
    /// (<c>Napkin.App.Designs.SampleFiles</c>), because what the viewer ships is part of what is
    /// under test. The <em>expectations</em> are read from here: they are the repository's
    /// hand-computed answers, never shipped with the app and never regenerated from napkin's
    /// output (samples/README.md).
    /// </remarks>
    public static string SamplesDirectory => Path.Combine(RepositoryRoot, "samples");

    static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "napkin.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No napkin.sln found above {AppContext.BaseDirectory}; cannot locate the repository " +
            "root, so there is nowhere to write artifacts/gui-metrics.json.");
    }
}

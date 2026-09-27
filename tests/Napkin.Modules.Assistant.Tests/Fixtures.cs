using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>Where the tests find the repository, the samples and the shipped packs.</summary>
internal static class Fixtures
{
    /// <summary>The real shipped packs, copied beside the test binary and read by the loader the app uses.</summary>
    public static readonly string RealPacks = Path.Combine(AppContext.BaseDirectory, "RealPacks");

    /// <summary>The repository's root: the folder holding napkin.sln, found by walking up (the VersionTests pattern).</summary>
    public static string RepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "napkin.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException($"napkin.sln not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>A file in the repository, as on disk now, line breaks as <c>\n</c>.</summary>
    public static string RepositoryText(string relative)
        => File.ReadAllText(Path.Combine(RepositoryRoot(), relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>A sample, read by the scene reader the app uses, as a design named from its entities.</summary>
    public static Design Sample(string name)
    {
        LoadResult result = SceneReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "samples", $"{name}.scene.json"));
        if (result is Refused refused)
        {
            Assert.Fail($"{name}.scene.json was refused: {refused.Summary}");
        }

        return Design.Named(name, Assert.IsType<Loaded>(result).Sketch);
    }

    /// <summary>The shipped packs, as the app discovers them.</summary>
    public static CodePacks ShippedPacks() => CodePacks.Discover([RealPacks]);

    /// <summary>The id of the entity with this name in a design.</summary>
    public static EntityId Named(Design design, string name)
        => Assert.Single(design.Sketch.Entities.Values, entity => entity.Name == name).Id;
}

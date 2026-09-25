using System.Text.Json.Nodes;

using Napkin.Core.Project;
using Napkin.Tools.Commands;

namespace Napkin.Tools.Tests;

/// <summary>
/// <c>samples restamp</c> (#181): a no-op on a tree already at the current format version, and a
/// faithful reconstruction of a sample stripped back to an older one.
/// </summary>
public class SamplesRestampTests
{
    private const int CurrentVersion = 10;

    [Fact]
    public void RunningItOnCurrentSamplesIsANoOpWithNoDiff()
    {
        using var scratch = Fixture.NewDirectory();
        string samplesDir = System.IO.Path.Combine(scratch.Path, "samples");
        Directory.CreateDirectory(samplesDir);
        string scenePath = System.IO.Path.Combine(samplesDir, "coffee-table.scene.json");
        string expectedPath = System.IO.Path.Combine(samplesDir, "coffee-table.expected.json");
        string sceneBefore = Fixture.Text("samples/coffee-table.scene.json");
        string expectedBefore = Fixture.Text("samples/coffee-table.expected.json");
        File.WriteAllText(scenePath, sceneBefore);
        File.WriteAllText(expectedPath, expectedBefore);

        var (code, output, _) = Run("samples", "restamp", "--root", scratch.Path);

        Assert.Equal(ExitCode.Ok, code);
        Assert.Contains("0 scene(s) restamped, 1 already at version 10", output, StringComparison.Ordinal);
        Assert.Contains("0 expectation file(s) restamped, 1 already current", output, StringComparison.Ordinal);
        Assert.Equal(sceneBefore, File.ReadAllText(scenePath));
        Assert.Equal(expectedBefore, File.ReadAllText(expectedPath));
    }

    [Fact]
    public void ASampleStrippedBackToFormatVersionFourRestampsToExactlyTheOriginal()
    {
        JsonNode original = JsonNode.Parse(Fixture.Text("samples/coffee-table.scene.json"))!;
        JsonNode stripped = StripToVersion4(original);

        using var scratch = Fixture.NewDirectory();
        string samplesDir = System.IO.Path.Combine(scratch.Path, "samples");
        Directory.CreateDirectory(samplesDir);
        string scenePath = System.IO.Path.Combine(samplesDir, "coffee-table.scene.json");
        File.WriteAllText(scenePath, stripped.ToJsonString());

        var (code, output, error) = Run("samples", "restamp", "--root", scratch.Path);

        Assert.Equal(ExitCode.Ok, code);
        Assert.Equal(string.Empty, error);
        Assert.Contains("restamped samples/coffee-table.scene.json to format version 10", output, StringComparison.Ordinal);

        JsonNode restamped = JsonNode.Parse(File.ReadAllText(scenePath))!;
        Assert.True(JsonNode.DeepEquals(original, restamped), $"restamped:\n{restamped.ToJsonString()}\n\noriginal:\n{original.ToJsonString()}");

        // Byte-identical with a fresh save of the real, committed sample — not just semantically
        // equal — because RestampScene writes through the real SceneWriter, not the JsonNode text.
        string canonical = SceneWriter.WriteToText(((Loaded)SceneReader.ReadFile(Fixture.Path("samples/coffee-table.scene.json"))).Sketch);
        Assert.Equal(canonical, File.ReadAllText(scenePath));
    }

    [Fact]
    public void ExpectationsRestampChangesOnlyTheFormatVersionField()
    {
        string before = Fixture.Text("samples/coffee-table.expected.json");
        // A textual downgrade, matching how RestampExpectations itself edits the stamp, so the
        // fixture stays byte-for-byte what a real committed expectations file looks like — not a
        // JsonNode re-serialisation with different whitespace.
        string stamp = $"\"formatVersion\": {CurrentVersion}";
        Assert.Contains(stamp, before, StringComparison.Ordinal);
        string lowered = before.Replace(stamp, "\"formatVersion\": 6", StringComparison.Ordinal);

        using var scratch = Fixture.NewDirectory();
        string samplesDir = System.IO.Path.Combine(scratch.Path, "samples");
        Directory.CreateDirectory(samplesDir);
        // An expectations file needs no matching scene to be restamped on its own.
        string expectedPath = System.IO.Path.Combine(samplesDir, "coffee-table.expected.json");
        File.WriteAllText(expectedPath, lowered);

        var (code, _, _) = Run("samples", "restamp", "--root", scratch.Path);

        Assert.Equal(ExitCode.Ok, code);
        Assert.Equal(before, File.ReadAllText(expectedPath));
    }

    [Fact]
    public void AFormatVersionOlderThanFourIsRefusedNotGuessedAt()
    {
        JsonNode original = JsonNode.Parse(Fixture.Text("samples/coffee-table.scene.json"))!;
        JsonNode ancient = original.DeepClone();
        ancient["formatVersion"] = 3;

        using var scratch = Fixture.NewDirectory();
        string samplesDir = System.IO.Path.Combine(scratch.Path, "samples");
        Directory.CreateDirectory(samplesDir);
        string scenePath = System.IO.Path.Combine(samplesDir, "coffee-table.scene.json");
        string beforeText = ancient.ToJsonString();
        File.WriteAllText(scenePath, beforeText);

        var (code, _, error) = Run("samples", "restamp", "--root", scratch.Path);

        Assert.Equal(ExitCode.InputError, code);
        Assert.Contains("predates version 4", error, StringComparison.Ordinal);
        Assert.Equal(beforeText, File.ReadAllText(scenePath));
    }

    /// <summary>Undoes, by hand, exactly what versions 5-10 added — the mirror of `ApplyVersion`.</summary>
    private static JsonNode StripToVersion4(JsonNode original)
    {
        JsonNode stripped = original.DeepClone();
        JsonObject root = stripped.AsObject();

        root.Remove("fastenerChoices");
        root.Remove("supplies");
        root.Remove("code");
        JsonObject? site = root["site"]?.AsObject();
        root.Remove("site");

        foreach (JsonNode? entity in root["entities"]!.AsArray())
        {
            JsonObject box = entity!.AsObject();
            box.Remove("phase");
            if (box["type"]!.GetValue<string>() != "box")
            {
                continue;
            }

            box.Remove("wall");
            box.Remove("room");
            if (box["part"] is JsonObject part)
            {
                part.Remove("hardware");
                part.Remove("rough");
            }
        }

        root["formatVersion"] = 4;
        Assert.NotNull(site); // sanity: the fixture really has a site object to strip.
        return stripped;
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Cli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}

using System.Text.Json.Nodes;

using Napkin.Tools.Commands;

namespace Napkin.Tools.Tests;

/// <summary>
/// <c>samples restamp</c> (#181): a no-op on a tree already at the current format version, and a
/// faithful reconstruction of a sample stripped back to an older one.
/// </summary>
public class SamplesRestampTests
{
    private const int CurrentVersion = 8;

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
        Assert.Contains("0 scene(s) restamped, 1 already at version 8", output, StringComparison.Ordinal);
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
        Assert.Contains("restamped samples/coffee-table.scene.json to format version 8", output, StringComparison.Ordinal);

        JsonNode restamped = JsonNode.Parse(File.ReadAllText(scenePath))!;
        Assert.True(JsonNode.DeepEquals(original, restamped), $"restamped:\n{restamped.ToJsonString()}\n\noriginal:\n{original.ToJsonString()}");
    }

    [Fact]
    public void ExpectationsRestampChangesOnlyTheFormatVersionField()
    {
        JsonNode before = JsonNode.Parse(Fixture.Text("samples/coffee-table.expected.json"))!;
        JsonNode lowered = before.DeepClone();
        lowered["formatVersion"] = 6;

        using var scratch = Fixture.NewDirectory();
        string samplesDir = System.IO.Path.Combine(scratch.Path, "samples");
        Directory.CreateDirectory(samplesDir);
        // An expectations file needs no matching scene to be restamped on its own.
        string expectedPath = System.IO.Path.Combine(samplesDir, "coffee-table.expected.json");
        File.WriteAllText(expectedPath, lowered.ToJsonString());

        var (code, _, _) = Run("samples", "restamp", "--root", scratch.Path);

        Assert.Equal(ExitCode.Ok, code);
        JsonNode after = JsonNode.Parse(File.ReadAllText(expectedPath))!;
        Assert.Equal(CurrentVersion, after["formatVersion"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(before, after), "restamping should change nothing but formatVersion, which was already 8 in `before`.");
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

    /// <summary>Undoes, by hand, exactly what versions 5-8 added — the mirror of `ApplyVersion`.</summary>
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
            if (box["type"]!.GetValue<string>() != "box")
            {
                continue;
            }

            box.Remove("wall");
            if (box["part"] is JsonObject part)
            {
                part.Remove("hardware");
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

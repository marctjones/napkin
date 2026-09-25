using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Napkin.Core.Project;

namespace Napkin.Tools.Commands;

/// <summary>
/// <c>samples restamp</c> (#181): brings every <c>samples/*.scene.json</c> up to the reader's
/// current format version, without hand-editing 15 files and 15 expectations on every bump.
/// </summary>
/// <remarks>
/// <para>
/// The strict reader (<see cref="FormatStamp"/>) accepts only <see cref="FormatStamp.CurrentVersion"/>,
/// so it cannot read an older sample to migrate it in memory (that would be the migration code the
/// beta policy refuses, DESIGN.md &#xA7;12). Instead this rewrites the JSON directly: it bumps
/// <c>formatVersion</c> and inserts, as <c>null</c> or empty, exactly the fields each version
/// between the sample's and the current one added — the same fields <c>samples/README.md</c>
/// documents by hand today — then verifies the result by handing it to the real
/// <see cref="SceneReader"/>. A sample already at the current version is left untouched, so running
/// this on an up-to-date tree is a no-op with no diff.
/// </para>
/// <para>
/// Only versions 5 and up are additive-only (joinery, then the building inputs, added nothing but
/// null defaults and empty lists — see <c>samples/README.md</c>'s per-version notes). Versions 2-4
/// changed what existing fields meant (a box moved into space, a corner reference became a feature
/// reference), so a sample older than version 4 is refused with a message to rewrite it by hand,
/// the way the samples themselves were rewritten when those versions landed.
/// </para>
/// </remarks>
public static class SamplesCommand
{
    /// <summary>The oldest format version this command can restamp forward from.</summary>
    internal const int OldestSupportedVersion = 4;

    static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly HashSet<string> Valued = ["--root"];
    private static readonly HashSet<string> Flags = [];

    public static int Restamp(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        CommandLine parsed = CommandLine.Parse(args, Valued, Flags);
        if (parsed.WantsHelp)
        {
            output.WriteLine(Cli.Usage);
            return ExitCode.Ok;
        }

        RepoLayout layout = Cli.ResolveRoot(parsed);
        string samplesDir = Path.Combine(layout.Root, "samples");

        if (!Directory.Exists(samplesDir))
        {
            error.WriteLine($"napkin-tools: no samples directory at {layout.Relative(samplesDir)}.");
            return ExitCode.InputError;
        }

        List<string> problems = [];
        int scenesChanged = 0;
        int scenesSkipped = 0;

        foreach (string path in Directory.GetFiles(samplesDir, "*.scene.json").Order(StringComparer.Ordinal))
        {
            RestampResult result = RestampScene(path);
            if (result.Problem is { } problem)
            {
                problems.Add(problem);
                continue;
            }

            if (result.Changed)
            {
                scenesChanged++;
                output.WriteLine($"restamped {layout.Relative(path)} to format version {FormatStamp.CurrentVersion}.");
            }
            else
            {
                scenesSkipped++;
            }
        }

        int expectationsChanged = 0;
        int expectationsSkipped = 0;
        foreach (string path in Directory.GetFiles(samplesDir, "*.expected.json").Order(StringComparer.Ordinal))
        {
            bool changed = RestampExpectations(path);
            if (changed)
            {
                expectationsChanged++;
                output.WriteLine($"restamped {layout.Relative(path)} to format version {FormatStamp.CurrentVersion}.");
            }
            else
            {
                expectationsSkipped++;
            }
        }

        if (problems.Count > 0)
        {
            foreach (string problem in problems)
            {
                error.WriteLine($"napkin-tools: {problem}");
            }

            return ExitCode.InputError;
        }

        output.WriteLine(
            $"samples restamp: {scenesChanged} scene(s) restamped, {scenesSkipped} already at version {FormatStamp.CurrentVersion}; "
            + $"{expectationsChanged} expectation file(s) restamped, {expectationsSkipped} already current.");
        return ExitCode.Ok;
    }

    /// <summary>Restamps one <c>*.scene.json</c>, or reports why it could not.</summary>
    internal static RestampResult RestampScene(string path)
    {
        string text = File.ReadAllText(path);
        JsonNode root = JsonNode.Parse(text) ?? throw new InvalidOperationException($"{path}: empty JSON document.");
        int version = ReadVersion(root, path);

        if (version == FormatStamp.CurrentVersion)
        {
            return new RestampResult(Changed: false, Problem: null);
        }

        if (version > FormatStamp.CurrentVersion)
        {
            return new RestampResult(Changed: false, Problem: $"{path}: format version {version} is newer than this build's {FormatStamp.CurrentVersion}.");
        }

        if (version < OldestSupportedVersion)
        {
            return new RestampResult(Changed: false, Problem:
                $"{path}: format version {version} predates version {OldestSupportedVersion} (boxes in space), which changed what existing fields mean, not just added new ones. Rewrite it by hand, the way the samples were rewritten for that bump.");
        }

        for (int next = version + 1; next <= FormatStamp.CurrentVersion; next++)
        {
            ApplyVersion(root, next);
        }

        root["formatVersion"] = FormatStamp.CurrentVersion;
        string bumped = root.ToJsonString(WriteOptions) + "\n";

        // Bumped-and-field-filled is only good enough to feed the strict reader, not to write:
        // JsonNode's default encoder escapes every non-ASCII character and `'`/`&`/`<`/`>`, and it
        // does not reproduce the writer's own field order. Reading it back and writing *that* with
        // the real SceneWriter is the hybrid the design calls for — it gives byte-identity with a
        // fresh save for free, the same guarantee samples/*.scene.json is already held to
        // (SaveReopenTests).
        LoadResult loaded = SceneReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(bumped)));
        if (loaded is not Loaded { Sketch: var sketch })
        {
            Refused refused = (Refused)loaded;
            return new RestampResult(Changed: false, Problem: $"{path}: restamped file was refused by the strict reader: {refused.Summary}");
        }

        File.WriteAllText(path, SceneWriter.WriteToText(sketch));
        return new RestampResult(Changed: true, Problem: null);
    }

    /// <summary>
    /// The expectations file states the format version too (<c>samples/README.md</c>), purely as
    /// a fact the reader test checks the scene against; nothing else in it depends on the version.
    /// </summary>
    internal static bool RestampExpectations(string path)
    {
        string text = File.ReadAllText(path);
        JsonNode root = JsonNode.Parse(text) ?? throw new InvalidOperationException($"{path}: empty JSON document.");
        int version = ReadVersion(root, path);
        if (version == FormatStamp.CurrentVersion)
        {
            return false;
        }

        // A textual replace of just the stamp, not a JsonNode round trip: nothing else in this
        // file depends on the version (samples/README.md), so nothing else should be able to move
        // — no re-encoded punctuation, no re-ordered fields.
        string stamp = $"\"formatVersion\": {version}";
        if (!text.Contains(stamp, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path}: expected to find {stamp} verbatim to replace it.");
        }

        File.WriteAllText(path, text.Replace(stamp, $"\"formatVersion\": {FormatStamp.CurrentVersion}", StringComparison.Ordinal));
        return true;
    }

    static int ReadVersion(JsonNode root, string path) =>
        root["formatVersion"]?.GetValue<int>()
            ?? throw new InvalidOperationException($"{path}: no \"formatVersion\" field.");

    /// <summary>
    /// The fields one version bump adds, as null or empty — the same list
    /// <c>samples/README.md</c>'s per-version notes give in prose.
    /// </summary>
    static void ApplyVersion(JsonNode root, int version)
    {
        switch (version)
        {
            case 5:
                EnsureArray(root, "fastenerChoices");
                EnsureArray(root, "supplies");
                foreach (JsonNode? entity in Entities(root))
                {
                    if (entity?["part"] is JsonObject part)
                    {
                        EnsureArray(part, "hardware");
                    }
                }

                break;

            case 6:
                EnsureNull(root.AsObject(), "code");
                if (root["site"] is null)
                {
                    root["site"] = new JsonObject
                    {
                        ["groundSnowLoad"] = null,
                        ["ultimateWindSpeed"] = null,
                        ["seismicDesignCategory"] = null,
                        ["frostDepth"] = null,
                        ["buildingWidth"] = null,
                        ["source"] = null,
                    };
                }

                foreach (JsonNode? entity in Entities(root))
                {
                    if (entity is JsonObject box && box["type"]?.GetValue<string>() == "box")
                    {
                        EnsureNull(box, "wall");
                    }
                }

                break;

            case 7:
                EnsureNull(root["site"]!.AsObject(), "roofLiveLoad");
                break;

            case 8:
                foreach (JsonNode? entity in Entities(root))
                {
                    if (entity?["wall"] is JsonObject wall)
                    {
                        EnsureNull(wall, "bracing");
                    }
                }

                break;

            default:
                throw new InvalidOperationException($"samples restamp does not know what format version {version} added.");
        }
    }

    static IEnumerable<JsonNode?> Entities(JsonNode root) => root["entities"]?.AsArray() ?? [];

    static void EnsureArray(JsonNode owner, string field)
    {
        if (owner[field] is null)
        {
            owner[field] = new JsonArray();
        }
    }

    static void EnsureNull(JsonObject owner, string field)
    {
        if (!owner.ContainsKey(field))
        {
            owner[field] = null;
        }
    }

    internal readonly record struct RestampResult(bool Changed, string? Problem);
}

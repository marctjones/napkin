using System.Text;

namespace Napkin.App.GuiTests;

/// <summary>
/// Scene files the reader refuses, written to disk so that the viewer can be asked to open them.
/// </summary>
/// <remarks>
/// <para>
/// These are the viewer's fixtures, not the reader's: what is under test here is what the
/// <em>window</em> does with a refusal, so each one only has to be a file the reader will turn
/// down, for a reason a person can read.
/// </para>
/// <para>
/// <strong>Why <see cref="SeveralFaults"/> is three unknown-or-unreadable fields rather than the
/// catalogue's "unknown field, dangling id and unsupported relationship kind".</strong> The reader
/// works in stages and stops after the first stage that found anything (see
/// <c>SceneBinder.Read</c>): field-level faults are all found together, but a dangling id is only
/// looked for once the fields are clean, and an unsupported relationship kind only once the sketch
/// validates. A file carrying all three therefore reports one of them, not three. Three faults of
/// the same stage is the way to make the reader hand the window a list, which is what the window's
/// side of the promise — <em>every</em> problem shown, not the first — needs to be tested with.
/// The other two kinds get their own file below and their own opening.
/// </para>
/// </remarks>
public static class BadScenes
{
    const string LayerId = "00000000-0000-0000-0000-000000000001";
    const string BoxId = "0192f1a0-0000-4000-8000-00000000000a";
    const string RelationshipId = "0192f1a0-0000-4000-8000-00000000001a";
    const string MissingId = "0192f1a0-0000-4000-8000-0000000000ff";

    /// <summary>A 30&#x2033; by 4&#x2033; box with its width driven: a file that opens.</summary>
    public const string Good = """
        {
          "formatVersion": 6,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Shelf",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 30720, "height": 4096, "depth": 768, "faceUp": "top", "rotation": 0,
              "part": null, "wall": null, "cuts": [] }
          ],
          "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "source": null },
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "paramValue",
              "param": { "kind": "boxWidth", "box": "0192f1a0-0000-4000-8000-00000000000a" }, "value": 30720 }
          ]
        }
        """;

    /// <summary>
    /// Three faults the reader finds in one pass: a field it does not know on the layer, another on
    /// the relationship, and a width written as a decimal.
    /// </summary>
    public static string SeveralFaults => Good
        .Replace(
            "\"name\": \"Default\"",
            "\"name\": \"Default\", \"visible\": true",
            StringComparison.Ordinal)
        .Replace("\"width\": 30720", "\"width\": 30720.5", StringComparison.Ordinal)
        .Replace("\"value\": 30720 }", "\"value\": 30720, \"note\": \"about 30\" }", StringComparison.Ordinal);

    /// <summary>A driving size on a box the file does not contain.</summary>
    public static string DanglingReference => Good
        .Replace($"\"box\": \"{BoxId}\"", $"\"box\": \"{MissingId}\"", StringComparison.Ordinal);

    /// <summary>A file stamped with a format version this build does not read.</summary>
    public static string WrongVersion => Good
        .Replace("\"formatVersion\": 6", "\"formatVersion\": 3", StringComparison.Ordinal);

    /// <summary>
    /// A relationship the format defines but this build's updater cannot hold: <c>distance</c> is
    /// reserved for the constraint solver, which is not in M1.
    /// </summary>
    /// <remarks>
    /// Two boxes ten inches apart, with the gap between them stated as a <c>distance</c>. Every
    /// earlier stage of the reader passes — the fields are known, the ids resolve, the sketch
    /// validates — so this is the third of the catalogue's three faults, reached on its own.
    /// </remarks>
    public const string UnsupportedRelationshipKind = """
        {
          "formatVersion": 6,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "West shelf",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 30720, "height": 4096, "depth": 768, "faceUp": "top", "rotation": 0, "part": null, "wall": null, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000b", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "East shelf",
              "anchor": { "x": 40960, "y": 0, "z": 0 }, "width": 30720, "height": 4096, "depth": 768, "faceUp": "top", "rotation": 0, "part": null, "wall": null, "cuts": [] }
          ],
          "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "source": null },
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "distance",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["south", "east"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["south", "west"] },
              "value": 10240 }
          ]
        }
        """;

    /// <summary>Nothing at all.</summary>
    public const string Empty = "";

    /// <summary>Bytes that are not JSON.</summary>
    public const string NotJson = "this is a drawing, honestly";

    /// <summary>Where these fixtures are written. Removed when the test process exits.</summary>
    static readonly Lazy<string> Scratch = new(() =>
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"napkin-gui-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A leftover temp directory is not worth failing a test run over.
            }
        };

        return directory;
    });

    /// <summary>Writes a scene file and returns its path.</summary>
    /// <param name="fileName">What to call it — it shows up in the refusal the window displays.</param>
    /// <param name="json">The bytes, however wrong.</param>
    public static string Write(string fileName, string json)
    {
        string path = Path.Combine(Scratch.Value, fileName);
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    /// <summary>A path in the scratch directory that nothing was ever written to.</summary>
    public static string MissingFile(string fileName) => Path.Combine(Scratch.Value, fileName);
}

using System.IO.Compression;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Containers built by hand, for the faults a container can have. Each test starts from a good
/// one and introduces exactly one thing wrong, the way the reader's own tests do.
/// </summary>
internal static class Containers
{
    /// <summary>The manifest a good container carries, with no adopted code chosen.</summary>
    internal static string GoodManifest =>
        $$"""
        {
          "containerVersion": {{ProjectManifest.CurrentContainerVersion}},
          "appVersion": "0.0.0-test",
          "adoptedCode": null
        }
        """;

    /// <summary>A small, valid drawing: one box, its width driven.</summary>
    internal static Sketch OneBox
    {
        get
        {
            Box box = new(
                new EntityId(Guid.Parse("0192f1a0-0000-4000-8000-00000000000a")),
                LayerId.Default,
                Point2.Origin,
                new Length(30720),
                new Length(4096),
                Angle.Zero);

            return Sketch.Empty
                .WithEntity(box)
                .WithRelationship(new ParamValue(
                    new RelationshipId(Guid.Parse("0192f1a0-0000-4000-8000-00000000001a")),
                    new BoxWidthRef(box.Id),
                    box.Width));
        }
    }

    /// <summary>The scene document a good container carries.</summary>
    internal static byte[] GoodScene => SceneWriter.WriteToBytes(OneBox);

    /// <summary>A container holding exactly these entries, in this order, duplicates and all.</summary>
    internal static byte[] Zip(params (string Name, byte[] Bytes)[] entries)
    {
        using MemoryStream buffer = new();

        using (ZipArchive archive = new(buffer, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            foreach ((string name, byte[] bytes) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using Stream target = entry.Open();
                target.Write(bytes, 0, bytes.Length);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>A good container, then one thing changed by the caller.</summary>
    internal static byte[] With(string? manifest = null, byte[]? scene = null)
        => Zip(
            ("manifest.json", Utf8(manifest ?? GoodManifest)),
            ("scene.json", scene ?? GoodScene));

    internal static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>Opens a container's bytes, holding every relationship kind the model has.</summary>
    internal static LoadResult Open(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, writable: false);
        return ProjectFile.Load(stream, new SceneWriterTests.EveryKindUpdater());
    }

    /// <summary>Opens a container and asserts it was refused for one stated reason.</summary>
    internal static LoadProblem Reject(byte[] bytes, LoadProblemKind kind, params string[] mustName)
    {
        Refused refused = Assert.IsType<Refused>(Open(bytes));
        LoadProblem problem = Assert.Single(refused.Problems.Where(candidate => candidate.Kind == kind));

        foreach (string fragment in mustName)
        {
            Assert.Contains(fragment, $"{problem.Location} {problem.Message}", StringComparison.Ordinal);
        }

        // The message the user is shown has to carry it: "something is wrong with this file" is
        // not a message.
        Assert.Contains(problem.Message, refused.Summary, StringComparison.Ordinal);
        return problem;
    }

    /// <summary>The one replacement a test makes, asserted to have actually changed something.</summary>
    internal static string Swap(this string json, string original, string replacement)
    {
        Assert.Contains(original, json, StringComparison.Ordinal);
        return json.Replace(original, replacement, StringComparison.Ordinal);
    }
}

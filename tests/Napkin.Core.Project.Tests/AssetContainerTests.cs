using System.IO.Compression;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Container version 2 (docs/design/permit-set.md §5.4, §6, #224): the survey image travels in
/// assets/&lt;sha256&gt;.png|.jpg beside the scene, and the two agree or the container is refused. The Project
/// layer never decodes an image, so a "PNG" here is the PNG signature and some bytes.
/// </summary>
public class AssetContainerTests
{
    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 9, 9];
    static readonly string PngHash = ProjectAssets.Hash(Png);

    static Sketch WithUnderlay(string hash) => Containers.OneBox with
    {
        Site = SiteValues.NotEntered with { Underlay = new SurveyUnderlay(hash, new Pixel(0, 0), new Pixel(100, 0), Point2.Origin, new Point2(Length.Feet(100), Length.Zero), Length.Feet(100), "survey.png") },
    };

    static byte[] Scene(Sketch sketch) => SceneWriter.WriteToBytes(sketch);

    static byte[] Zip(params (string Name, byte[] Bytes)[] extra) => Containers.Zip([("manifest.json", Containers.Utf8(Containers.GoodManifest)), ("scene.json", Scene(WithUnderlay(PngHash))), .. extra]);

    [Fact]
    [Trait("Feature", "PRJ-006")]
    public void The_underlays_image_is_saved_beside_the_scene_and_comes_back()
    {
        Dictionary<string, byte[]> assets = new() { [PngHash] = Png, [ProjectAssets.Hash(Jpeg)] = Jpeg };
        byte[] bytes = ProjectFile.SaveToBytes(WithUnderlay(PngHash), assets: assets);

        // Only the image the underlay names is written; the other is not.
        using (ZipArchive archive = new(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            Assert.Equal(["manifest.json", "scene.json", $"assets/{PngHash}.png"], archive.Entries.Select(entry => entry.FullName));
        }

        LoadedProject opened = Assert.IsType<LoadedProject>(Containers.Open(bytes));
        Assert.Equal(WithUnderlay(PngHash), opened.Sketch);
        Assert.Equal(Png, opened.Contents.Assets[PngHash]);
        Assert.Equal([".png", ".jpg", null], new[] { Png, Jpeg, "GIF89a"u8.ToArray() }.Select(image => ProjectAssets.Extension(image)));
        Assert.Empty(Assert.IsType<LoadedProject>(Containers.Open(ProjectFile.SaveToBytes(Containers.OneBox))).Contents.Assets);
    }

    [Fact]
    public void A_drawing_whose_image_is_not_here_is_not_saved()
    {
        Assert.IsType<NotSaved>(ProjectFile.Save(new MemoryStream(), WithUnderlay(PngHash)));
        Assert.IsType<NotSaved>(ProjectFile.Save(new MemoryStream(), WithUnderlay(PngHash), assets: new Dictionary<string, byte[]> { [PngHash] = Jpeg }));
        Assert.IsType<NotSaved>(ProjectFile.Save(new MemoryStream(), WithUnderlay(PngHash), assets: new Dictionary<string, byte[]> { [PngHash] = "not an image"u8.ToArray() }));
        Assert.IsType<Saved>(ProjectFile.Save(new MemoryStream(), WithUnderlay(PngHash), assets: new Dictionary<string, byte[]> { [PngHash] = Png }));
    }

    [Fact]
    public void An_image_that_is_not_what_its_name_says_or_is_not_referred_to_is_refused()
    {
        Assert.IsType<LoadedProject>(Containers.Open(Zip(($"assets/{PngHash}.png", Png))));
        Containers.Reject(Zip(($"assets/{PngHash}.jpg", Png)), LoadProblemKind.InvalidValue, "JPEG");
        Containers.Reject(Zip(($"assets/{PngHash}.png", [.. Png, 5])), LoadProblemKind.InvalidValue, "SHA-256");
        Containers.Reject(Zip(), LoadProblemKind.MissingEntry, PngHash);
        Containers.Reject(Zip(($"assets/{PngHash}.png", Png), ($"assets/{ProjectAssets.Hash(Jpeg)}.jpg", Jpeg)), LoadProblemKind.UnknownEntry, "refers to");
        Containers.Reject(Zip(("assets/survey.png", Png)), LoadProblemKind.UnknownEntry, "assets/survey.png");
    }

    [Fact]
    public void An_image_over_the_limit_is_refused_as_too_large()
    {
        byte[] huge = new byte[ContainerLimits.MaxAssetBytes + 1];
        Png.CopyTo(huge, 0);
        Containers.Reject(Zip(($"assets/{ProjectAssets.Hash(huge)}.png", huge)), LoadProblemKind.TooLarge, "assets/");
    }
}

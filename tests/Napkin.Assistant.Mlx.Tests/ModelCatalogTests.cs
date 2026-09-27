using System.Text.RegularExpressions;

namespace Napkin.Assistant.Mlx.Tests;

/// <summary>
/// The catalog is data read from the Hugging Face Hub (docs/design/mlx-runtime.md §6.1): these
/// goldens were hand-checked on 2026-09-27 against <c>/api/models/&lt;repo&gt;/tree/&lt;commit&gt;</c>
/// (sizes, LFS <c>oid</c>s), the <c>x-linked-etag</c>/<c>x-linked-size</c> of a <c>HEAD</c> on each
/// LFS file, the bytes of every other file at the commit (SHA-256 and git blob SHA-1, the latter equal
/// to the tree's <c>oid</c>), and <c>/api/models/&lt;repo&gt;/revision/&lt;commit&gt;</c>'s card data.
/// Nothing here touches the network; an edit to a value in the catalog is a red test.
/// </summary>
[Trait("Feature", "AST-008")]
public class ModelCatalogTests
{
    [Fact]
    public void TheDefaultIsQwen3_4B_4bitAtItsPinnedCommitFileForFile()
    {
        CatalogModel model = ModelCatalog.Default;

        Assert.Same(ModelCatalog.Qwen3_4B_4bit, model);
        Assert.Equal("mlx-community/Qwen3-4B-4bit", model.Repo);
        Assert.Equal("4dcb3d101c2a062e5c1d4bb173588c54ea6c4d25", model.Commit);
        Assert.Equal("apache-2.0", model.Licence);
        Assert.Equal("https://huggingface.co/Qwen/Qwen3-4B/blob/main/LICENSE", model.LicenceLink);
        Assert.Equal(
            [
                ("model.safetensors", 2_263_022_529L, "e240c0bdc0ebb0681bf0da0f98d9719fd6ebe269a3633f81542c13e81345651d"),
                ("tokenizer.json", 11_422_654L, "aeb13307a71acd8fe81861d94ad54ab689df773318809eed3cbe794b4492dae4"),
                ("vocab.json", 2_776_833L, "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910"),
                ("model.safetensors.index.json", 63_924L, "f7825defe5865d179c3b593173d37056be5f202dcb7153985cf74e75ecf1628b"),
                ("tokenizer_config.json", 9_706L, "253153d0738ceb4c668d2eff957714dd2bea0b56de772a9fdccd96cbf517e6a0"),
                ("config.json", 937L, "b5efdcf3b0035a3638e7228dad4d85f5c4a23f156eb7cdb0b44c8366a5d34d9b"),
                ("added_tokens.json", 707L, "c0284b582e14987fbd3d5a2cb2bd139084371ed9acbae488829a1c900833c680"),
                ("special_tokens_map.json", 613L, "76862e765266b85aa9459767e33cbaf13970f327a0e88d1c65846c2ddd3a1ecd"),
            ],
            Rows(model));
        Assert.Equal(2_277_297_903L, model.TotalBytes);
        Assert.Equal(2_263_022_529L, model.WeightBytes);
        Assert.Equal("8 files, 2,277,297,903 bytes (2.28 GB)", model.FilesLine);
    }

    [Fact]
    public void TheNeverThinksAlternativeIsQwen3_4B_Instruct_2507FileForFile()
    {
        CatalogModel model = ModelCatalog.Qwen3_4B_Instruct_2507_4bit;

        Assert.Equal("mlx-community/Qwen3-4B-Instruct-2507-4bit", model.Repo);
        Assert.Equal("50d427756c6b1b2fe0c0a10f67fbda1fc8e82c1b", model.Commit);
        Assert.Equal("apache-2.0", model.Licence);
        Assert.Equal("https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507/blob/main/LICENSE", model.LicenceLink);
        Assert.Equal(
            [
                ("model.safetensors", 2_263_022_417L, "2a73c6c248601ab904e035548abd8e6abb65ea27dcb5f342fb0a8910eb44173f"),
                ("tokenizer.json", 11_422_654L, "aeb13307a71acd8fe81861d94ad54ab689df773318809eed3cbe794b4492dae4"),
                ("vocab.json", 2_776_833L, "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910"),
                ("model.safetensors.index.json", 63_964L, "388d811b8b7c2608dd04cce1bcb04a8bf715d19b42790894e6d3427ff429a777"),
                ("tokenizer_config.json", 5_440L, "4397cc477eb6d79715ccd2000accd6b3531928f30029665832fa1b255f24d2b9"),
                ("chat_template.jinja", 4_040L, "40c21f34cf67d8c760ef72f8ad3ae5afad514299d4b06e91dd9a8d705af7b541"),
                ("config.json", 938L, "574349e5a343236546fda55e4744a76e181f534182d7dc60ff1bad7e7a502849"),
                ("added_tokens.json", 707L, "c0284b582e14987fbd3d5a2cb2bd139084371ed9acbae488829a1c900833c680"),
                ("special_tokens_map.json", 613L, "76862e765266b85aa9459767e33cbaf13970f327a0e88d1c65846c2ddd3a1ecd"),
                ("generation_config.json", 238L, "835fffe355c9438e7a25be099b3fccaa98350b83451f9fd2d99512e74f1ade48"),
            ],
            Rows(model));
        Assert.Equal(2_277_297_844L, model.TotalBytes);
    }

    [Fact]
    public void TheMitAlternativeIsPhi4MiniInstructFileForFileAndNoPythonFile()
    {
        CatalogModel model = ModelCatalog.Phi_4_mini_instruct_4bit;

        Assert.Equal("mlx-community/Phi-4-mini-instruct-4bit", model.Repo);
        Assert.Equal("ac1c269cb4222a4e136a3d09edad301056c1f36a", model.Commit);
        Assert.Equal("mit", model.Licence);
        Assert.Equal("https://huggingface.co/microsoft/Phi-4-mini-instruct/resolve/main/LICENSE", model.LicenceLink);
        Assert.Equal(
            [
                ("model.safetensors", 2_158_100_796L, "9dcfcdc0a579494283f2d2317ad686123782ce461d1111690ede7c904ff3f87b"),
                ("tokenizer.json", 15_524_095L, "382cc235b56c725945e149cc25f191da667c836655efd0857b004320e90e91ea"),
                ("vocab.json", 3_910_310L, "6cb65a857824fa6615bb1782d95d882617a8bbce1da0317118586b36f39e98bd"),
                ("model.safetensors.index.json", 32_554L, "958e2e0939857e9fb398e9e3d5344a84e345b00da2e4e7c396024d6a91bc1ab7"),
                ("config.json", 3_298L, "431eafdf55dc6d4a76dc3e9f22b0d06eac42d9ac6dc7cbd7ff8ca1e38f18cdb1"),
                ("tokenizer_config.json", 2_962L, "76c79ba1828e98a574123dd5de047587219ed8c1ab20702d340bc10a7a32b6bd"),
                ("special_tokens_map.json", 587L, "aff38493227d813e29fcf8406e8e90062f1f031aa47d589325e9c31d89ac7cc3"),
                ("added_tokens.json", 249L, "d4f2aceb0f20b71dd1f4bcc7e052e4412946bf281840b8f83d39f259571af486"),
            ],
            Rows(model));
        Assert.Equal(2_177_574_851L, model.TotalBytes);
        Assert.DoesNotContain(model.Files, file => file.Name.EndsWith(".py", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCatalogIsTheThreeModelsDefaultFirst() =>
        Assert.Equal([ModelCatalog.Qwen3_4B_4bit, ModelCatalog.Qwen3_4B_Instruct_2507_4bit, ModelCatalog.Phi_4_mini_instruct_4bit], ModelCatalog.All);

    [Fact]
    public void EveryEntryHasTheShapeADownloadNeeds()
    {
        foreach (CatalogModel model in ModelCatalog.All)
        {
            Assert.Matches("^[0-9a-f]{40}$", model.Commit);
            Assert.Equal(model.Files.Sum(file => file.Bytes), model.TotalBytes);
            Assert.All(ModelFolder.RequiredFiles, name => Assert.Contains(model.Files, file => file.Name == name));
            Assert.Contains(model.Files, file => file.IsWeights);
            foreach (CatalogFile file in model.Files)
            {
                Assert.Matches("^[0-9a-f]{64}$", file.Sha256);
                Assert.True(file.Bytes > 0);
                Assert.Contains(ModelCatalog.LoaderPatterns, pattern => Regex.IsMatch(file.Name, "^" + Regex.Escape(pattern).Replace(@"\*", "[^/]+", StringComparison.Ordinal) + "$"));
                Assert.DoesNotContain('/', file.Name);
                Assert.Equal(new Uri($"https://huggingface.co/{model.Repo}/resolve/{model.Commit}/{file.Name}"), model.FileUrl(file));
                Assert.True(ModelDownload.IsAllowed(model.FileUrl(file)));
            }
        }
    }

    [Fact]
    public void TheLoadersPatternsAreMlxSwiftLmsOwn() =>
        Assert.Equal(["*.safetensors", "*.json", "*.jinja"], ModelCatalog.LoaderPatterns);

    [Fact]
    public void AModelIsNamedAndFoldedAsTheDownloadNamesIt()
    {
        CatalogModel model = ModelCatalog.Default;

        Assert.Equal("mlx-community", model.Owner);
        Assert.Equal("Qwen3-4B-4bit", model.Name);
        Assert.Equal("mlx-community--Qwen3-4B-4bit--4dcb3d101c2a", model.FolderName);
        Assert.Equal("https://huggingface.co/mlx-community/Qwen3-4B-4bit/blob/4dcb3d101c2a062e5c1d4bb173588c54ea6c4d25/README.md", model.CardUrl);
        Assert.Equal("mlx-community--Phi-4-mini-instruct-4bit--ac1c269cb422", ModelCatalog.Phi_4_mini_instruct_4bit.FolderName);
    }

    [Fact]
    public void AFolderIsTracedToItsCatalogModelByNameAlone()
    {
        Assert.Same(ModelCatalog.Qwen3_4B_Instruct_2507_4bit, ModelCatalog.ForFolder("mlx-community--Qwen3-4B-Instruct-2507-4bit--50d427756c6b"));
        Assert.Null(ModelCatalog.ForFolder("mlx-community--Qwen3-4B-4bit--000000000000"));
        Assert.Null(ModelCatalog.ForFolder("Qwen3-4B-4bit"));
    }

    [Fact]
    public void DownloadedModelsLiveInModelsUnderTheConfigDirectory() =>
        Assert.Equal(Path.Combine("cfg", "napkin", "models"), ModelCatalog.ModelsDirectory(Path.Combine("cfg", "napkin")));

    [Theory]
    [InlineData(2_277_297_903L, "2.28 GB")]
    [InlineData(1_000_000_000L, "1.00 GB")]
    [InlineData(11_422_654L, "11.4 MB")]
    [InlineData(1_000_000L, "1.0 MB")]
    [InlineData(999_999L, "999,999 bytes")]
    [InlineData(0L, "0 bytes")]
    public void SizesAreWrittenAsTheHubWritesThem(long bytes, string shown) => Assert.Equal(shown, ModelCatalog.Size(bytes));

    [Theory]
    [InlineData("model.py")]
    [InlineData("README.md")]
    [InlineData("merges.txt")]
    [InlineData(".gitattributes")]
    [InlineData(".hidden.json")]
    [InlineData("sub/config.json")]
    [InlineData("..\\config.json")]
    [InlineData("../config.json")]
    [InlineData("")]
    public void AFileOutsideTheLoadersPatternsIsNotACatalogFile(string name)
    {
        ArgumentException refused = Assert.Throws<ArgumentException>(() => new CatalogFile(name, 1, new string('a', 64)));
        Assert.StartsWith($"{name} is not a file napkin fetches", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void AFileWithNoSizeIsRefused(long bytes) =>
        Assert.StartsWith("config.json has no size.", Assert.Throws<ArgumentException>(() => new CatalogFile("config.json", bytes, new string('a', 64))).Message, StringComparison.Ordinal);

    [Theory]
    [InlineData("E240C0BDC0EBB0681BF0DA0F98D9719FD6EBE269A3633F81542C13E81345651D")]
    [InlineData("e240c0bd")]
    [InlineData("bb9a9794962c1adf3234c9c4ffe113edbcdc4a34")]
    public void AHashThatIsNotASha256IsRefused(string sha) =>
        Assert.StartsWith("config.json's SHA-256 is not 64 lowercase hex digits", Assert.Throws<ArgumentException>(() => new CatalogFile("config.json", 1, sha)).Message, StringComparison.Ordinal);

    [Fact]
    public void AFileNeedsANameAndAHash()
    {
        Assert.Throws<ArgumentNullException>(() => new CatalogFile(null!, 1, new string('a', 64)));
        Assert.Throws<ArgumentNullException>(() => new CatalogFile("config.json", 1, null!));
    }

    [Theory]
    [InlineData("Qwen3-4B-4bit")]
    [InlineData("mlx-community/Qwen3/4B")]
    [InlineData("/Qwen3-4B-4bit")]
    [InlineData("mlx-community/..")]
    public void ARepositoryIdIsOwnerSlashName(string repo) =>
        Assert.StartsWith($"{repo} is not a repository id", Assert.Throws<ArgumentException>(() => Model(repo: repo)).Message, StringComparison.Ordinal);

    [Theory]
    [InlineData("main")]
    [InlineData("4dcb3d101c2a")]
    [InlineData("4DCB3D101C2A062E5C1D4BB173588C54EA6C4D25")]
    [InlineData("refs/pr/1")]
    public void OnlyAFullCommitIsPinned(string commit) =>
        Assert.StartsWith($"{commit} is not a full commit", Assert.Throws<ArgumentException>(() => Model(commit: commit)).Message, StringComparison.Ordinal);

    [Theory]
    [InlineData("http://huggingface.co/Qwen/Qwen3-4B/blob/main/LICENSE")]
    [InlineData("LICENSE")]
    public void TheLicenceLinkIsAnHttpsLink(string link) =>
        Assert.StartsWith($"{link} is not an https link", Assert.Throws<ArgumentException>(() => Model(licenceLink: link)).Message, StringComparison.Ordinal);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void TheLicenceIsStated(string licence) => Assert.Throws<ArgumentException>(() => Model(licence: licence));

    [Fact]
    public void AFileListedTwiceIsRefused()
    {
        CatalogFile[] files = [.. Files(), new CatalogFile("CONFIG.json", 1, new string('b', 64))];

        Assert.Equal("mlx-community/Qwen3-4B-4bit names a file twice. (Parameter 'files')", Assert.Throws<ArgumentException>(() => Model(files: files)).Message);
    }

    [Theory]
    [InlineData("config.json")]
    [InlineData("tokenizer.json")]
    [InlineData("tokenizer_config.json")]
    public void AnEntryWithoutAFileTheBridgeNeedsIsRefused(string name) =>
        Assert.Equal(
            $"mlx-community/Qwen3-4B-4bit does not list {name}, which the bridge needs. (Parameter 'files')",
            Assert.Throws<ArgumentException>(() => Model(files: Files().Where(file => file.Name != name))).Message);

    [Fact]
    public void AnEntryWithoutWeightsIsRefused() =>
        Assert.Equal(
            "mlx-community/Qwen3-4B-4bit lists no .safetensors weights. (Parameter 'files')",
            Assert.Throws<ArgumentException>(() => Model(files: Files().Where(file => !file.IsWeights))).Message);

    [Fact]
    public void AnEntryNeedsEveryPart()
    {
        Assert.Throws<ArgumentNullException>(() => new CatalogModel(null!, Commit, "mit", Link, Files()));
        Assert.Throws<ArgumentNullException>(() => new CatalogModel("a/b", null!, "mit", Link, Files()));
        Assert.Throws<ArgumentNullException>(() => new CatalogModel("a/b", Commit, null!, Link, Files()));
        Assert.Throws<ArgumentNullException>(() => new CatalogModel("a/b", Commit, "mit", null!, Files()));
        Assert.Throws<ArgumentNullException>(() => new CatalogModel("a/b", Commit, "mit", Link, null!));
        Assert.Throws<ArgumentNullException>(() => ModelCatalog.Default.FileUrl(null!));
    }

    private const string Commit = "4dcb3d101c2a062e5c1d4bb173588c54ea6c4d25";

    private const string Link = "https://huggingface.co/Qwen/Qwen3-4B/blob/main/LICENSE";

    private static (string Name, long Bytes, string Sha256)[] Rows(CatalogModel model) =>
        [.. model.Files.Select(file => (file.Name, file.Bytes, file.Sha256))];

    private static CatalogFile[] Files() =>
    [
        new("model.safetensors", 10, new string('0', 64)),
        new("config.json", 2, new string('1', 64)),
        new("tokenizer.json", 2, new string('2', 64)),
        new("tokenizer_config.json", 2, new string('3', 64)),
    ];

    private static CatalogModel Model(
        string repo = "mlx-community/Qwen3-4B-4bit",
        string commit = Commit,
        string licence = "apache-2.0",
        string licenceLink = Link,
        IEnumerable<CatalogFile>? files = null) =>
        new(repo, commit, licence, licenceLink, files ?? Files());
}

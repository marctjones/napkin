using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Napkin.Assistant.Mlx;

/// <summary>
/// The models napkin offers to download, as data (docs/design/mlx-runtime.md §5, §6.1, issue #242):
/// each one a Hugging Face repository at a pinned commit, its licence as its model card states it,
/// and every file napkin fetches with its size and SHA-256.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value here was read from the Hugging Face Hub on 2026-09-27 (11:52 UTC)</b>, for the
/// pinned commit and nothing else:
/// </para>
/// <list type="bullet">
/// <item><description>the file list, sizes, and each LFS file's SHA-256 (<c>lfs.oid</c>) from
/// <c>https://huggingface.co/api/models/&lt;repo&gt;/tree/&lt;commit&gt;</c>; the LFS SHA-256 and size
/// again from the <c>x-linked-etag</c> and <c>x-linked-size</c> headers of a <c>HEAD</c> on the file's
/// resolve URL (<c>x-repo-commit</c> equal to the commit) — the weights themselves were never
/// fetched;</description></item>
/// <item><description>every other file's SHA-256 hashed from its bytes, fetched from the resolve URL at
/// the commit (<c>x-repo-commit</c> checked), whose git blob SHA-1 was computed too and equals the
/// tree's <c>oid</c>;</description></item>
/// <item><description>the licence (<c>cardData.license</c>) and its link (<c>cardData.license_link</c>)
/// from <c>https://huggingface.co/api/models/&lt;repo&gt;/revision/&lt;commit&gt;?blobs=true</c>, the
/// model card's front matter at that commit.</description></item>
/// </list>
/// <para>
/// Every value equals what docs/design/mlx-runtime.md §6.1 recorded when the note was written.
/// Only the files mlx-swift-lm's loader asks for are listed — <see cref="LoaderPatterns"/> — so
/// <c>.gitattributes</c>, <c>README.md</c>, <c>merges.txt</c> and every <c>.py</c> file in a
/// repository are never fetched.
/// </para>
/// </remarks>
public static class ModelCatalog
{
    /// <summary>The only host a download starts at.</summary>
    public const string Host = "huggingface.co";

    /// <summary>The folder under napkin's config directory that downloaded models live in (§6.4, decision 5).</summary>
    public const string ModelsFolderName = "models";

    /// <summary>
    /// The files mlx-swift-lm's loader wants from a repository: <c>modelDownloadPatterns =
    /// ["*.safetensors"] + tokenizerDownloadPatterns</c>, <c>tokenizerDownloadPatterns = ["*.json",
    /// "*.jinja"]</c> (<c>Libraries/MLXLMCommon/ModelFactory.swift</c> at the pinned commit
    /// <c>ee673d6a71d76e67b532dc7eaf91d92edc3bb8bb</c>, read 2026-09-27). A catalog file must match one.
    /// </summary>
    public static ImmutableArray<string> LoaderPatterns { get; } = ["*.safetensors", "*.json", "*.jinja"];

    /// <summary>
    /// <c>mlx-community/Qwen3-4B-4bit</c> at <c>4dcb3d10…</c> (2025-04-28): the default (decision 4),
    /// converted from <c>Qwen/Qwen3-4B</c>; card licence <c>apache-2.0</c>. 8 files, 2,277,297,903 bytes.
    /// </summary>
    public static CatalogModel Qwen3_4B_4bit { get; } = new(
        "mlx-community/Qwen3-4B-4bit",
        "4dcb3d101c2a062e5c1d4bb173588c54ea6c4d25",
        "apache-2.0",
        "https://huggingface.co/Qwen/Qwen3-4B/blob/main/LICENSE",
        [
            new("model.safetensors", 2_263_022_529, "e240c0bdc0ebb0681bf0da0f98d9719fd6ebe269a3633f81542c13e81345651d"),
            new("tokenizer.json", 11_422_654, "aeb13307a71acd8fe81861d94ad54ab689df773318809eed3cbe794b4492dae4"),
            new("vocab.json", 2_776_833, "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910"),
            new("model.safetensors.index.json", 63_924, "f7825defe5865d179c3b593173d37056be5f202dcb7153985cf74e75ecf1628b"),
            new("tokenizer_config.json", 9_706, "253153d0738ceb4c668d2eff957714dd2bea0b56de772a9fdccd96cbf517e6a0"),
            new("config.json", 937, "b5efdcf3b0035a3638e7228dad4d85f5c4a23f156eb7cdb0b44c8366a5d34d9b"),
            new("added_tokens.json", 707, "c0284b582e14987fbd3d5a2cb2bd139084371ed9acbae488829a1c900833c680"),
            new("special_tokens_map.json", 613, "76862e765266b85aa9459767e33cbaf13970f327a0e88d1c65846c2ddd3a1ecd"),
        ]);

    /// <summary>
    /// <c>mlx-community/Qwen3-4B-Instruct-2507-4bit</c> at <c>50d42775…</c> (2026-01-02): the
    /// never-thinks alternative (decision 4), converted from <c>Qwen/Qwen3-4B-Instruct-2507</c>; card
    /// licence <c>apache-2.0</c>. 10 files, 2,277,297,844 bytes.
    /// </summary>
    public static CatalogModel Qwen3_4B_Instruct_2507_4bit { get; } = new(
        "mlx-community/Qwen3-4B-Instruct-2507-4bit",
        "50d427756c6b1b2fe0c0a10f67fbda1fc8e82c1b",
        "apache-2.0",
        "https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507/blob/main/LICENSE",
        [
            new("model.safetensors", 2_263_022_417, "2a73c6c248601ab904e035548abd8e6abb65ea27dcb5f342fb0a8910eb44173f"),
            new("tokenizer.json", 11_422_654, "aeb13307a71acd8fe81861d94ad54ab689df773318809eed3cbe794b4492dae4"),
            new("vocab.json", 2_776_833, "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910"),
            new("model.safetensors.index.json", 63_964, "388d811b8b7c2608dd04cce1bcb04a8bf715d19b42790894e6d3427ff429a777"),
            new("tokenizer_config.json", 5_440, "4397cc477eb6d79715ccd2000accd6b3531928f30029665832fa1b255f24d2b9"),
            new("chat_template.jinja", 4_040, "40c21f34cf67d8c760ef72f8ad3ae5afad514299d4b06e91dd9a8d705af7b541"),
            new("config.json", 938, "574349e5a343236546fda55e4744a76e181f534182d7dc60ff1bad7e7a502849"),
            new("added_tokens.json", 707, "c0284b582e14987fbd3d5a2cb2bd139084371ed9acbae488829a1c900833c680"),
            new("special_tokens_map.json", 613, "76862e765266b85aa9459767e33cbaf13970f327a0e88d1c65846c2ddd3a1ecd"),
            new("generation_config.json", 238, "835fffe355c9438e7a25be099b3fccaa98350b83451f9fd2d99512e74f1ade48"),
        ]);

    /// <summary>
    /// <c>mlx-community/Phi-4-mini-instruct-4bit</c> at <c>ac1c269c…</c> (2025-03-05): the MIT
    /// alternative, converted from <c>microsoft/Phi-4-mini-instruct</c>; card licence <c>mit</c>. 8
    /// files, 2,177,574,851 bytes. The repository also holds <c>configuration_phi3.py</c>,
    /// <c>modeling_phi3.py</c> and <c>sample_finetune.py</c>, which napkin never fetches.
    /// </summary>
    public static CatalogModel Phi_4_mini_instruct_4bit { get; } = new(
        "mlx-community/Phi-4-mini-instruct-4bit",
        "ac1c269cb4222a4e136a3d09edad301056c1f36a",
        "mit",
        "https://huggingface.co/microsoft/Phi-4-mini-instruct/resolve/main/LICENSE",
        [
            new("model.safetensors", 2_158_100_796, "9dcfcdc0a579494283f2d2317ad686123782ce461d1111690ede7c904ff3f87b"),
            new("tokenizer.json", 15_524_095, "382cc235b56c725945e149cc25f191da667c836655efd0857b004320e90e91ea"),
            new("vocab.json", 3_910_310, "6cb65a857824fa6615bb1782d95d882617a8bbce1da0317118586b36f39e98bd"),
            new("model.safetensors.index.json", 32_554, "958e2e0939857e9fb398e9e3d5344a84e345b00da2e4e7c396024d6a91bc1ab7"),
            new("config.json", 3_298, "431eafdf55dc6d4a76dc3e9f22b0d06eac42d9ac6dc7cbd7ff8ca1e38f18cdb1"),
            new("tokenizer_config.json", 2_962, "76c79ba1828e98a574123dd5de047587219ed8c1ab20702d340bc10a7a32b6bd"),
            new("special_tokens_map.json", 587, "aff38493227d813e29fcf8406e8e90062f1f031aa47d589325e9c31d89ac7cc3"),
            new("added_tokens.json", 249, "d4f2aceb0f20b71dd1f4bcc7e052e4412946bf281840b8f83d39f259571af486"),
        ]);

    /// <summary>The three models, the default first.</summary>
    public static ImmutableArray<CatalogModel> All { get; } = [Qwen3_4B_4bit, Qwen3_4B_Instruct_2507_4bit, Phi_4_mini_instruct_4bit];

    /// <summary>The model the dialog's Download… offers (decision 4).</summary>
    public static CatalogModel Default => Qwen3_4B_4bit;

    /// <summary>The catalog model whose download made a folder of this name (<c>&lt;owner&gt;--&lt;name&gt;--&lt;commit12&gt;</c>), or null.</summary>
    /// <param name="folderName">A folder's own name, not its path.</param>
    public static CatalogModel? ForFolder(string folderName) => All.FirstOrDefault(model => model.FolderName == folderName);

    /// <summary>Where downloaded models live under napkin's config directory: <c>&lt;config&gt;/models</c> (§6.4).</summary>
    /// <param name="configDirectory">napkin's per-user config directory.</param>
    public static string ModelsDirectory(string configDirectory) => Path.Combine(configDirectory, ModelsFolderName);

    /// <summary>A size for people: "2.28 GB", "11.4 MB", or "9,706 bytes" below a megabyte (decimal units, as the Hub prints them).</summary>
    /// <param name="bytes">A count of bytes.</param>
    public static string Size(long bytes) => bytes switch
    {
        >= 1_000_000_000 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1e9:0.00} GB"),
        >= 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1e6:0.0} MB"),
        _ => Bytes(bytes) + " bytes",
    };

    /// <summary>A count of bytes with thousands separators: "2,277,297,903".</summary>
    /// <param name="bytes">A count of bytes.</param>
    public static string Bytes(long bytes) => bytes.ToString("N0", CultureInfo.InvariantCulture);
}

/// <summary>One file of a <see cref="CatalogModel"/>: its name in the repository, its size, and its SHA-256.</summary>
public sealed partial class CatalogFile
{
    /// <summary>A file napkin will fetch.</summary>
    /// <param name="name">A plain file name at the repository's root that matches one of <see cref="ModelCatalog.LoaderPatterns"/>.</param>
    /// <param name="bytes">Its size, more than nothing.</param>
    /// <param name="sha256">Its SHA-256, 64 lowercase hex digits.</param>
    /// <exception cref="ArgumentException">The name, size or hash is not one napkin will fetch.</exception>
    public CatalogFile(string name, long bytes, string sha256)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(sha256);
        if (!FileName().IsMatch(name))
        {
            throw new ArgumentException($"{name} is not a file napkin fetches: a plain name ending .safetensors, .json or .jinja.", nameof(name));
        }

        if (bytes <= 0)
        {
            throw new ArgumentException($"{name} has no size.", nameof(bytes));
        }

        if (!Sha256Hex().IsMatch(sha256))
        {
            throw new ArgumentException($"{name}'s SHA-256 is not 64 lowercase hex digits: {sha256}.", nameof(sha256));
        }

        Name = name;
        Bytes = bytes;
        Sha256 = sha256;
    }

    /// <summary>The file's name at the repository's root, and in the downloaded folder.</summary>
    public string Name { get; }

    /// <summary>Its size in bytes.</summary>
    public long Bytes { get; }

    /// <summary>Its SHA-256, lowercase hex.</summary>
    public string Sha256 { get; }

    /// <summary>Whether this file is weights (<c>*.safetensors</c>).</summary>
    public bool IsWeights => Name.EndsWith(".safetensors", StringComparison.Ordinal);

    /// <summary>A name with no folder, no leading dot, and one of the loader's extensions.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9._-]*\.(safetensors|json|jinja)$", RegexOptions.CultureInvariant)]
    private static partial Regex FileName();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Hex();
}

/// <summary>
/// A model napkin can download: a Hugging Face repository at one commit, its licence as its card
/// states it, and the files to fetch (docs/design/mlx-runtime.md §6.1).
/// </summary>
public sealed partial class CatalogModel
{
    /// <summary>A catalog entry, checked so a download can only ever be of a pinned commit into a folder the bridge can read.</summary>
    /// <param name="repo"><c>owner/name</c>.</param>
    /// <param name="commit">The full 40-hex commit — never a branch, so never <c>main</c>.</param>
    /// <param name="licence">The model card's <c>license:</c>, verbatim.</param>
    /// <param name="licenceLink">The card's <c>license_link:</c>, an https URL.</param>
    /// <param name="files">The files to fetch, in order: distinct names, among them every file <see cref="ModelFolder"/> requires and at least one <c>*.safetensors</c>.</param>
    /// <exception cref="ArgumentException">Any of the above does not hold.</exception>
    public CatalogModel(string repo, string commit, string licence, string licenceLink, IEnumerable<CatalogFile> files)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentException.ThrowIfNullOrWhiteSpace(licence);
        ArgumentNullException.ThrowIfNull(licenceLink);
        ArgumentNullException.ThrowIfNull(files);
        Match parts = RepoId().Match(repo);
        if (!parts.Success)
        {
            throw new ArgumentException($"{repo} is not a repository id of the form owner/name.", nameof(repo));
        }

        if (!CommitId().IsMatch(commit))
        {
            throw new ArgumentException($"{commit} is not a full commit (40 lowercase hex digits); napkin downloads only a pinned commit.", nameof(commit));
        }

        if (!Uri.TryCreate(licenceLink, UriKind.Absolute, out Uri? link) || link.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException($"{licenceLink} is not an https link.", nameof(licenceLink));
        }

        ImmutableArray<CatalogFile> list = [.. files];
        if (list.Select(file => file.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Length)
        {
            throw new ArgumentException($"{repo} names a file twice.", nameof(files));
        }

        if (ModelFolder.RequiredFiles.FirstOrDefault(name => !list.Any(file => file.Name == name)) is { } missing)
        {
            throw new ArgumentException($"{repo} does not list {missing}, which the bridge needs.", nameof(files));
        }

        if (!list.Any(file => file.IsWeights))
        {
            throw new ArgumentException($"{repo} lists no .safetensors weights.", nameof(files));
        }

        Repo = repo;
        Owner = parts.Groups["owner"].Value;
        Name = parts.Groups["name"].Value;
        Commit = commit;
        Licence = licence;
        LicenceLink = licenceLink;
        Files = list;
    }

    /// <summary><c>owner/name</c>, as the Hub writes it.</summary>
    public string Repo { get; }

    /// <summary>The repository's owner: <c>mlx-community</c>.</summary>
    public string Owner { get; }

    /// <summary>The repository's name, which is also the model's name for people: <c>Qwen3-4B-4bit</c>.</summary>
    public string Name { get; }

    /// <summary>The pinned commit, 40 hex digits.</summary>
    public string Commit { get; }

    /// <summary>The model card's <c>license:</c> at the commit, verbatim: <c>apache-2.0</c>.</summary>
    public string Licence { get; }

    /// <summary>The model card's <c>license_link:</c> at the commit.</summary>
    public string LicenceLink { get; }

    /// <summary>The files to fetch, in the order they are fetched.</summary>
    public ImmutableArray<CatalogFile> Files { get; }

    /// <summary>Every file's bytes together.</summary>
    public long TotalBytes => Files.Sum(file => file.Bytes);

    /// <summary>The weights' bytes (<c>*.safetensors</c>) — what a load reads and napkin's memory rule weighs.</summary>
    public long WeightBytes => Files.Where(file => file.IsWeights).Sum(file => file.Bytes);

    /// <summary>The downloaded folder's name: <c>&lt;owner&gt;--&lt;name&gt;--&lt;commit[..12]&gt;</c> (§6.4).</summary>
    public string FolderName => $"{Owner}--{Name}--{Commit[..12]}";

    /// <summary>The model card at the pinned commit, for the person to read.</summary>
    public string CardUrl => $"https://{ModelCatalog.Host}/{Repo}/blob/{Commit}/README.md";

    /// <summary>"8 files, 2,277,297,903 bytes (2.28 GB)".</summary>
    public string FilesLine => $"{Files.Length} files, {ModelCatalog.Bytes(TotalBytes)} bytes ({ModelCatalog.Size(TotalBytes)})";

    /// <summary>Where a file is fetched from: <c>https://huggingface.co/&lt;repo&gt;/resolve/&lt;commit&gt;/&lt;file&gt;</c> — the commit, never a branch.</summary>
    /// <param name="file">One of <see cref="Files"/>.</param>
    public Uri FileUrl(CatalogFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new Uri($"https://{ModelCatalog.Host}/{Repo}/resolve/{Commit}/{file.Name}");
    }

    [GeneratedRegex("^(?<owner>[A-Za-z0-9][A-Za-z0-9_.-]*)/(?<name>[A-Za-z0-9][A-Za-z0-9_.-]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex RepoId();

    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitId();
}

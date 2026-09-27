using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Napkin.Assistant.Mlx;

/// <summary>
/// A model folder napkin has checked before the bridge reads it (docs/design/mlx-runtime.md §4.1,
/// §13.1 item 5): an mlx-community folder holding <c>config.json</c>, <c>tokenizer.json</c>, a
/// <c>tokenizer_config.json</c> that names its <c>tokenizer_class</c>, and <c>*.safetensors</c>
/// weights — and what it says about itself, for the dialog's line.
/// </summary>
/// <remarks>
/// <para>
/// The refusals are the bridge's own checks (<c>ModelFolder.validate</c> in
/// <c>native/NapkinMlx/Sources/NapkinMlx/Bridge.swift</c>), in its order and its words, so whichever
/// side refuses a folder the person reads the same sentence. They matter more than tidiness:
/// swift-transformers calls <c>fatalError</c> — taking napkin with it — on a folder whose
/// <c>tokenizer_config.json</c> is missing or names no <c>tokenizer_class</c>. One check is napkin's
/// alone: <c>config.json</c> must be a JSON object, since napkin reads it for the line.
/// </para>
/// <para>
/// A folder's name is shown as <see cref="DisplayName"/>: the repository's name for a folder napkin's
/// download made (<c>&lt;owner&gt;--&lt;name&gt;--&lt;commit12&gt;</c>, §6.4), otherwise the folder's
/// own name.
/// </para>
/// </remarks>
public sealed partial class ModelFolder
{
    /// <summary>The line's words when the folder's <c>README.md</c> states no licence.</summary>
    public const string LicenceNotStated = "licence not stated in the folder";

    /// <summary>The files the bridge needs by name, in the order a missing one is named.</summary>
    public static readonly IReadOnlyList<string> RequiredFiles = ["config.json", "tokenizer.json", "tokenizer_config.json"];

    private ModelFolder(string path, string? modelType, int? bits, int? groupSize, string? licence, long weightBytes)
    {
        Path = path;
        ModelType = modelType;
        QuantizationBits = bits;
        QuantizationGroupSize = groupSize;
        Licence = licence;
        WeightBytes = weightBytes;
        Name = System.IO.Path.GetFileName(path);
        Match download = DownloadedName().Match(Name);
        DisplayName = download.Success ? download.Groups["name"].Value : Name;
    }

    /// <summary>The folder's full path, as napkin passes it to the bridge.</summary>
    public string Path { get; }

    /// <summary>The folder's own name.</summary>
    public string Name { get; }

    /// <summary>The model's name for people: "Qwen3-4B-4bit" for <c>mlx-community--Qwen3-4B-4bit--4dcb3d101c2a</c>, else <see cref="Name"/>.</summary>
    public string DisplayName { get; }

    /// <summary><c>config.json</c>'s <c>model_type</c> ("qwen3"), or null when it states none.</summary>
    public string? ModelType { get; }

    /// <summary><c>config.json</c>'s <c>quantization.bits</c>, or null.</summary>
    public int? QuantizationBits { get; }

    /// <summary><c>config.json</c>'s <c>quantization.group_size</c>, or null.</summary>
    public int? QuantizationGroupSize { get; }

    /// <summary>
    /// The catalog's licence for a folder napkin's download made (its name is a <see cref="CatalogModel.FolderName"/>;
    /// such a folder has no <c>README.md</c>, since the loader does not want the card — mlx-runtime.md §4.4);
    /// otherwise the <c>license:</c> line of <c>README.md</c>'s front matter (the model card's); null when neither states one.
    /// </summary>
    public string? Licence { get; }

    /// <summary>The bytes of every <c>*.safetensors</c> file in the folder — what the load reads, and what napkin's memory rule weighs.</summary>
    public long WeightBytes { get; }

    /// <summary>The folder's path with the home folder written <c>~</c>, for the note's whereabouts line.</summary>
    public string HomeRelativePath => HomeRelative(Path, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>The dialog's line: "qwen3, 4-bit (group 64), licence: apache-2.0".</summary>
    public string Description
    {
        get
        {
            string quantization = QuantizationBits is not { } bits
                ? "quantization not stated"
                : QuantizationGroupSize is { } group
                    ? string.Create(CultureInfo.InvariantCulture, $"{bits}-bit (group {group})")
                    : string.Create(CultureInfo.InvariantCulture, $"{bits}-bit");
            string licence = Licence is { } stated ? $"licence: {stated}" : LicenceNotStated;
            return $"{ModelType ?? "model type not stated"}, {quantization}, {licence}";
        }
    }

    /// <summary>Checks <paramref name="path"/> as the bridge will, and reads what the folder says about itself.</summary>
    /// <param name="path">The folder, as typed or chosen.</param>
    /// <param name="folder">The checked folder, when it passes.</param>
    /// <param name="refusal">Why it does not, naming the file, when it does not.</param>
    /// <returns>Whether the folder passes.</returns>
    public static bool TryParse(
        string? path,
        [NotNullWhen(true)] out ModelFolder? folder,
        [NotNullWhen(false)] out string? refusal)
    {
        folder = null;
        refusal = Refusal(path, out string full, out JsonElement config);
        if (refusal is not null)
        {
            return false;
        }

        (int? bits, int? group) = Quantization(config);
        folder = new ModelFolder(
            full,
            StringIn(config, "model_type"),
            bits,
            group,
            ModelCatalog.ForFolder(System.IO.Path.GetFileName(full))?.Licence ?? ReadmeLicence(System.IO.Path.Combine(full, "README.md")),
            Directory.EnumerateFiles(full, "*.safetensors").Sum(file => new FileInfo(file).Length));
        return true;
    }

    /// <summary><paramref name="path"/> with a leading <paramref name="home"/> written <c>~</c>; unchanged when it is not under it.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="home">The home folder; empty when there is none.</param>
    public static string HomeRelative(string path, string home)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(home);
        string trimmed = home.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        if (trimmed.Length == 0)
        {
            return path;
        }

        if (path == trimmed)
        {
            return "~";
        }

        return path.StartsWith(trimmed + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? "~" + path[trimmed.Length..]
            : path;
    }

    /// <summary>The bridge's checks, in its order and words (Bridge.swift, <c>ModelFolder.validate</c>), then napkin's own on <c>config.json</c>.</summary>
    private static string? Refusal(string? path, out string full, out JsonElement config)
    {
        full = string.Empty;
        config = default;
        if (string.IsNullOrWhiteSpace(path))
        {
            return "No model folder was given.";
        }

        full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path.Trim()));
        if (full.EndsWith(ModelDownload.StagingSuffix, StringComparison.OrdinalIgnoreCase))
        {
            // napkin's own check, before the bridge's: a download is assembled here file by file and
            // is not a model until every file has passed and the folder has its own name.
            return $"{full} is a download napkin has not finished; press Download… to finish it.";
        }

        if (!Directory.Exists(full))
        {
            return !File.Exists(full) ? $"There is no folder at {full}." : $"{full} is a file, not a model folder.";
        }

        foreach (string name in RequiredFiles)
        {
            if (!File.Exists(System.IO.Path.Combine(full, name)))
            {
                return $"The folder {full} has no {name}.";
            }
        }

        string tokenizerConfig = System.IO.Path.Combine(full, "tokenizer_config.json");
        if (ReadObject(tokenizerConfig) is not { } tokenizer)
        {
            return $"The folder's tokenizer_config.json is not a JSON object: {tokenizerConfig}.";
        }

        if (StringIn(tokenizer, "tokenizer_class") is null)
        {
            return "The folder's tokenizer_config.json names no tokenizer_class, which napkin needs to read the tokenizer from the folder alone.";
        }

        if (!Directory.EnumerateFiles(full, "*.safetensors").Any())
        {
            return $"The folder {full} has no .safetensors weights.";
        }

        string configFile = System.IO.Path.Combine(full, "config.json");
        if (ReadObject(configFile) is not { } read)
        {
            return $"The folder's config.json is not a JSON object: {configFile}.";
        }

        config = read;
        return null;
    }

    /// <summary>A file's JSON, when it is readable and an object; null otherwise (the bridge treats an unreadable file the same way).</summary>
    private static JsonElement? ReadObject(string file)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(file));
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static (int? Bits, int? Group) Quantization(JsonElement config) =>
        config.TryGetProperty("quantization", out JsonElement quantization) && quantization.ValueKind == JsonValueKind.Object
            ? (IntIn(quantization, "bits"), IntIn(quantization, "group_size"))
            : (null, null);

    private static string? StringIn(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? IntIn(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : null;

    /// <summary>
    /// The model card's <c>license:</c> — a top-level key of <c>README.md</c>'s YAML front matter
    /// (the block between the first two <c>---</c> lines), quotes removed; null when there is no
    /// README, no front matter, or no such key.
    /// </summary>
    private static string? ReadmeLicence(string readme)
    {
        if (!File.Exists(readme))
        {
            return null;
        }

        try
        {
            using StreamReader reader = new(readme);
            if (reader.ReadLine()?.TrimEnd() != "---")
            {
                return null;
            }

            for (string? line = reader.ReadLine(); line is not null && line.TrimEnd() != "---"; line = reader.ReadLine())
            {
                if (line.StartsWith("license:", StringComparison.Ordinal))
                {
                    string value = line["license:".Length..].Trim().Trim('"', '\'');
                    return value.Length == 0 ? null : value;
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable card states nothing; it is not a file the loader needs.
            return null;
        }
    }

    /// <summary>A folder napkin's download names <c>&lt;owner&gt;--&lt;name&gt;--&lt;commit[..12]&gt;</c> (mlx-runtime.md §6.4).</summary>
    [GeneratedRegex("^(?<owner>.+?)--(?<name>.+)--(?<commit>[0-9a-f]{12})$", RegexOptions.CultureInvariant)]
    private static partial Regex DownloadedName();
}

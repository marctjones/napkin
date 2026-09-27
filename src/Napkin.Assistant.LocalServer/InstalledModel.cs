using System.Collections.Immutable;
using System.Globalization;

namespace Napkin.Assistant.LocalServer;

/// <summary>Which API a program on this machine speaks (docs/design/llm-assistant.md §5.2).</summary>
public enum Dialect
{
    /// <summary>
    /// Ollama's own API: <c>GET /api/tags</c>, <c>POST /api/show</c>, <c>POST /api/chat</c>
    /// (github.com/ollama/ollama docs/api.md and docs/openapi.yaml, read 2026-09-27).
    /// </summary>
    Ollama,

    /// <summary>
    /// The OpenAI-compatible API llama.cpp's server speaks: <c>GET /v1/models</c>,
    /// <c>POST /v1/chat/completions</c> (github.com/ggml-org/llama.cpp tools/server/README.md, read
    /// 2026-09-27).
    /// </summary>
    OpenAiCompatible,
}

/// <summary>
/// A model a program on this machine reports having, exactly as it reports it: every field is
/// read from the program's own reply, and a field it did not send is null — never filled in from
/// the model's name (docs/design/llm-assistant.md §5.3: the dialog "reads <c>quantization_level</c>
/// from <c>/api/show</c> rather than asserting it").
/// </summary>
/// <param name="Name">The name to ask it by: Ollama's <c>name</c>, or llama-server's <c>id</c> (the model file's path, or its <c>--alias</c>).</param>
public sealed record InstalledModel(string Name)
{
    /// <summary>Its size on disk in bytes: Ollama's <c>size</c>, llama-server's <c>meta.size</c>.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Its parameter count as the program says it: Ollama's <c>details.parameter_size</c> ("4.0B"), llama-server's <c>meta.n_params</c>.</summary>
    public string? ParameterSize { get; init; }

    /// <summary>Its quantization as the program says it: Ollama's <c>details.quantization_level</c> ("Q4_K_M"). llama-server does not report one.</summary>
    public string? QuantizationLevel { get; init; }

    /// <summary>Its file format as the program says it: Ollama's <c>details.format</c> ("gguf").</summary>
    public string? Format { get; init; }

    /// <summary>
    /// Ollama's <c>remote_host</c>: "URL of the upstream Ollama host, if the model is remote"
    /// (docs/openapi.yaml, <c>ModelSummary</c>, read 2026-09-27). Present means the model does not
    /// run on this machine.
    /// </summary>
    public string? RemoteHost { get; init; }

    /// <summary>Ollama's <c>remote_model</c>: "Name of the upstream model, if the model is remote" (same page).</summary>
    public string? RemoteModel { get; init; }

    /// <summary>The license text Ollama's <c>/api/show</c> returns ("The license of the model"); llama-server does not report one.</summary>
    public string? License { get; init; }

    /// <summary>
    /// The context length the model declares: from Ollama's <c>/api/show</c> <c>model_info</c>, the
    /// <c>&lt;architecture&gt;.context_length</c> entry (docs/api.md's example has
    /// <c>"llama.context_length": 8192</c> beside <c>"general.architecture": "llama"</c>); from
    /// llama-server, <c>meta.n_ctx_train</c>.
    /// </summary>
    public long? ContextLength { get; init; }

    /// <summary>Ollama's <c>/api/show</c> <c>capabilities</c> ("completion", "thinking", …), as read; empty when not reported.</summary>
    public ImmutableArray<string> Capabilities { get; init; } = [];

    /// <summary>Whether the program says this model runs somewhere else (<see cref="RemoteHost"/> or <see cref="RemoteModel"/> present).</summary>
    public bool IsRemote => RemoteHost is not null || RemoteModel is not null;

    /// <summary>The most of a license the list shows before cutting it; the whole text is the row's tooltip.</summary>
    public const int LicenseShown = 72;

    /// <summary>
    /// The model's line in the dialog: its name, then what the program said about it, each field
    /// "not reported" when it said nothing, and napkin's memory warning when it applies.
    /// </summary>
    /// <param name="machineMemoryBytes">The machine's memory, for napkin's own rule (<see cref="Guidance.TooBig"/>); null to leave the rule out.</param>
    public string Describe(long? machineMemoryBytes = null)
    {
        if (IsRemote)
        {
            return $"{Name} — {Guidance.RunsElsewhere(RemoteHost)}";
        }

        List<string> parts =
        [
            SizeBytes is { } size ? Guidance.Size(size) : "size not reported",
            ParameterSize is { } parameters ? $"{parameters} parameters" : "parameters not reported",
            QuantizationLevel ?? "quantization not reported",
            License is { } license ? $"license: {ShortLicense(license)}" : "license not reported",
        ];
        if (ContextLength is { } context)
        {
            parts.Add($"context {context.ToString("N0", CultureInfo.InvariantCulture)} tokens");
        }

        string line = $"{Name} — {string.Join(", ", parts)}";
        return SizeBytes is { } bytes && machineMemoryBytes is { } memory && Guidance.TooBig(bytes, memory)
            ? $"{line} — {Guidance.TooBigWords}"
            : line;
    }

    /// <summary>The start of a license as read, its whitespace folded to single spaces, cut at <see cref="LicenseShown"/> characters with an ellipsis.</summary>
    public static string ShortLicense(string license)
    {
        ArgumentNullException.ThrowIfNull(license);
        string folded = string.Join(' ', license.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return folded.Length <= LicenseShown ? folded : folded[..LicenseShown].TrimEnd() + "…";
    }
}

/// <summary>What a program on this machine said when asked what it has.</summary>
/// <param name="Endpoint">Where it was asked.</param>
/// <param name="Dialect">The API it answered in, or null when it did not answer as either.</param>
/// <param name="Models">The models it reported, in its order.</param>
/// <param name="Refusal">Why nothing was listed, in a sentence shown as it is; null when <paramref name="Dialect"/> is known.</param>
public sealed record ServerListing(LocalEndpoint Endpoint, Dialect? Dialect, ImmutableArray<InstalledModel> Models, string? Refusal)
{
    /// <summary>A program that answered in <paramref name="dialect"/>.</summary>
    public static ServerListing Answered(LocalEndpoint endpoint, Dialect dialect, IEnumerable<InstalledModel> models) =>
        new(endpoint, dialect, [.. models], null);

    /// <summary>Nothing listed, and why.</summary>
    public static ServerListing Refused(LocalEndpoint endpoint, string reason) => new(endpoint, null, [], reason);

    /// <summary>
    /// The model a name asks for: the one of exactly that name, or — for a name with no tag — the
    /// one tagged <c>latest</c> ("The tag is optional and, if not provided, will default to
    /// <c>latest</c>", docs/api.md "Model names", read 2026-09-27).
    /// </summary>
    public InstalledModel? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Models.FirstOrDefault(model => model.Name == name)
            ?? (name.Contains(':', StringComparison.Ordinal) ? null : Models.FirstOrDefault(model => model.Name == name + ":latest"));
    }
}

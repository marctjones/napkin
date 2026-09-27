using System.Collections.Immutable;
using System.Globalization;

namespace Napkin.Assistant.LocalServer;

/// <summary>
/// The words of the <em>Where the model runs…</em> dialog that are about getting and choosing a
/// model (docs/design/llm-assistant.md §5.4, §5.5, §8), kept here rather than in the app so they are
/// tested, and each fact beside its source. napkin downloads nothing: these lines tell the person
/// how to get a model themselves.
/// </summary>
public static class Guidance
{
    /// <summary>
    /// napkin's own allowance beyond a model's file, in bytes, for the memory rule — "the model file
    /// plus about a gigabyte must fit" (§5.4). napkin's number, not Ollama's: its README and FAQ state
    /// no RAM figure (the FAQ says requirements vary by model size).
    /// </summary>
    public const long Allowance = 1_000_000_000;

    /// <summary>
    /// The five install lines (§5.5). Every command, size, quantization and license here was read on
    /// 2026-09-27: installing and starting — Ollama's docs/quickstart.mdx ("Download Ollama for macOS,
    /// Windows, or Linux", linking https://ollama.com/download; "Open the app, or get started from
    /// your terminal") and docs/cli.mdx ("Start Ollama: ollama serve", "Download a model: ollama
    /// pull …"), github.com/ollama/ollama, published as docs.ollama.com; the models — each tag's own page on
    /// ollama.com/library (qwen3:4b-q4_K_M: qwen3, 4.02B parameters, Q4_K_M, 2.6GB, Apache License
    /// 2.0; qwen3:8b: 8.19B, Q4_K_M, 5.2GB, Apache License 2.0; phi4-mini: 3.84B, Q4_K_M, 2.5GB, MIT
    /// License) and each model card on Hugging Face (Qwen/Qwen3-4B and Qwen/Qwen3-8B "license:
    /// apache-2.0"; microsoft/Phi-4-mini-instruct "license: mit"). <c>qwen3:4b-q4_K_M</c>, not
    /// <c>qwen3:4b</c>: on 2026-09-27 the library's <c>qwen3:4b</c> has the same digest as
    /// <c>qwen3:4b-thinking-2507-q4_K_M</c>, a different model whose card says it "supports only
    /// thinking mode" (docs/design/llm-assistant.md §16).
    /// </summary>
    public static readonly ImmutableArray<string> InstallLines =
    [
        "Install Ollama from https://ollama.com/download (macOS, Windows or Linux) and start it — open the app, or run: ollama serve",
        "In a terminal: ollama pull qwen3:4b-q4_K_M — Qwen3-4B, Q4_K_M, 2.6 GB, Apache-2.0 (https://huggingface.co/Qwen/Qwen3-4B)",
        "With memory to spare: ollama pull qwen3:8b — Qwen3-8B, Q4_K_M, 5.2 GB, Apache-2.0 (https://huggingface.co/Qwen/Qwen3-8B)",
        "Or, under the MIT license: ollama pull phi4-mini — Phi-4-mini, Q4_K_M, 2.5 GB, MIT (https://huggingface.co/microsoft/Phi-4-mini-instruct)",
        "Then press Check here and pick it from the list. napkin downloads nothing itself.",
    ];

    /// <summary>
    /// Where each program answers, as installed (<see cref="LocalEndpoint.OllamaDefault"/>,
    /// <see cref="LocalEndpoint.LlamaServerDefault"/>).
    /// </summary>
    public static readonly string AddressHint =
        $"Ollama answers at {LocalEndpoint.OllamaDefault}; llama.cpp's llama-server at {LocalEndpoint.LlamaServerDefault}. Only this machine's addresses are accepted.";

    /// <summary>
    /// The Ollama-cloud sentence (§5.5, §13.4). Ollama can run models in its own cloud (docs.ollama.com/cloud);
    /// its FAQ, "How do I disable Ollama Cloud features?", gives <c>OLLAMA_NO_CLOUD=1</c> (read
    /// 2026-09-27). The middle sentence is §16's: <c>/api/tags</c> documents <c>remote_host</c>, so a
    /// model Ollama marks remote is refused, but napkin cannot promise every Ollama marks one.
    /// </summary>
    public const string CloudSentence =
        "Ollama can also run models in its own cloud. napkin will not use a model Ollama reports as remote, but cannot be sure every "
        + "Ollama reports it. To be sure nothing leaves this machine, set OLLAMA_NO_CLOUD=1 before starting Ollama — see its FAQ: "
        + "https://docs.ollama.com/faq";

    /// <summary>What a model napkin's memory rule says will not fit is marked with.</summary>
    public const string TooBigWords = "too big for this machine by napkin's rule";

    /// <summary>A size as Ollama's own pages print one: decimal gigabytes to one place ("2.6 GB"), or whole megabytes below a gigabyte.</summary>
    public static string Size(long bytes) => bytes >= 1_000_000_000
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes / 1e9:0.0} GB")
        : string.Create(CultureInfo.InvariantCulture, $"{bytes / 1e6:0} MB");

    /// <summary>
    /// napkin's memory rule (§5.4): a model whose file plus <see cref="Allowance"/> is more than the
    /// machine's memory is "too big for this machine" — a warning, never a refusal, since the program
    /// may page it.
    /// </summary>
    public static bool TooBig(long sizeBytes, long machineMemoryBytes) => sizeBytes + Allowance > machineMemoryBytes;

    /// <summary>The dialog's memory line: the machine's memory, and napkin's rule labelled as napkin's.</summary>
    public static string MemoryLine(long machineMemoryBytes) =>
        $"This machine has {Size(machineMemoryBytes)} of memory. napkin's own rule, not Ollama's: a model's file plus about 1 GB should fit in it.";

    /// <summary>What the list says of a model the program reports as running somewhere else.</summary>
    public static string RunsElsewhere(string? remoteHost) =>
        $"runs at {remoteHost ?? "another host"}, not on this machine; napkin will not use it";
}

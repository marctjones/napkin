namespace Napkin.Assistant.Mlx;

/// <summary>
/// The C ABI of <c>native/NapkinMlx/include/napkin_mlx.h</c> (version 1), one method per function,
/// with C# types (docs/design/mlx-runtime.md §1.4, §4.1). <see cref="NativeMlx"/> P/Invokes the real
/// bridge; <see cref="FakeNativeMlx"/> scripts replies and records calls, so no test loads the
/// library or needs Metal.
/// </summary>
/// <remarks>
/// Every method blocks the calling thread for as long as the bridge works — a load reads gigabytes,
/// a generation runs token by token — so a caller runs them on a thread-pool thread, never on the UI
/// thread (§1.5). The bridge answers one generation at a time per model and says
/// <see cref="MlxStatus.Busy"/> to a second. What the bridge returns (an error sentence, a result) is
/// read and freed inside the call; nothing native outlives it except a loaded model's handle, which
/// <see cref="Unload"/> releases.
/// </remarks>
public interface INativeMlx
{
    /// <summary><c>napkin_mlx_abi_version</c>: the header's version, 1 for this napkin (<see cref="MlxBridge.AbiVersion"/>).</summary>
    int AbiVersion();

    /// <summary>
    /// <c>napkin_mlx_init</c>, once per process: checks <paramref name="metallibPath"/> is the
    /// <c>mlx.metallib</c> beside the bridge (MLX 0.31 loads its Metal library from nowhere else —
    /// mlx-runtime.md §13.1 item 1), probes the Metal device and runs one tiny evaluation on the GPU.
    /// A second call after <see cref="MlxStatus.Ok"/> is a no-op.
    /// </summary>
    /// <param name="metallibPath">The <c>mlx.metallib</c> beside <c>libNapkinMlx.dylib</c> (<see cref="MlxBridge.MetallibPath"/>).</param>
    /// <param name="error">The bridge's sentence on any status but <see cref="MlxStatus.Ok"/>; null on OK.</param>
    MlxStatus Init(string metallibPath, out string? error);

    /// <summary><c>napkin_mlx_device</c>: Metal and the machine's memory, from Metal and sysctl only; needs no model and no init.</summary>
    /// <param name="info">What the bridge reported; <c>default</c> unless OK.</param>
    /// <param name="error">The bridge's sentence on any status but OK.</param>
    MlxStatus Device(out MlxDeviceInfo info, out string? error);

    /// <summary>
    /// <c>napkin_mlx_load</c>: loads an mlx-community folder. Not interruptible inside the library;
    /// <paramref name="cancel"/> is read before and after, and a cancel seen after drops the model and
    /// says <see cref="MlxStatus.Cancelled"/>. Requires a successful <see cref="Init"/>.
    /// </summary>
    /// <param name="modelFolder">The folder, as <see cref="ModelFolder"/> checked it.</param>
    /// <param name="cancel">The flag the bridge polls; napkin owns and frees it.</param>
    /// <param name="handle">The loaded model on OK; <see cref="MlxModelHandle.None"/> otherwise.</param>
    /// <param name="error">The bridge's sentence on any status but OK.</param>
    MlxStatus Load(string modelFolder, CancelFlag cancel, out MlxModelHandle handle, out string? error);

    /// <summary>
    /// <c>napkin_mlx_generate</c>: one reply. With <paramref name="jsonSchema"/> the bridge constrains
    /// every token to the schema (greedy under the grammar, mlx-runtime.md §2) and the reply is the
    /// JSON document; without it, free text sampled at <paramref name="temperature"/>.
    /// </summary>
    /// <param name="model">A handle <see cref="Load"/> returned and <see cref="Unload"/> has not released.</param>
    /// <param name="system">The system message, alone.</param>
    /// <param name="user">The user message.</param>
    /// <param name="jsonSchema">The schema as JSON text, passed through verbatim; null asks for text.</param>
    /// <param name="maxTokens">At least 1.</param>
    /// <param name="temperature">Sampling temperature (text only; a schema is decoded greedily).</param>
    /// <param name="topP">Nucleus sampling.</param>
    /// <param name="enableThinking">The chat template's <c>enable_thinking</c> flag (Qwen3 reads it; napkin sends false, §5.2).</param>
    /// <param name="cancel">Polled every token.</param>
    /// <param name="result">The reply on OK; null otherwise.</param>
    /// <param name="error">The bridge's sentence on any status but OK.</param>
    MlxStatus Generate(
        MlxModelHandle model,
        string system,
        string user,
        string? jsonSchema,
        int maxTokens,
        float temperature,
        float topP,
        bool enableThinking,
        CancelFlag cancel,
        out MlxGeneration? result,
        out string? error);

    /// <summary><c>napkin_mlx_unload</c>: releases the model; the handle is dead at once (a generation still running on it finishes first).</summary>
    void Unload(MlxModelHandle model);
}

/// <summary><c>napkin_mlx_status</c>, value for value (a test reads the header and holds them equal).</summary>
public enum MlxStatus
{
    /// <summary><c>NAPKIN_MLX_OK</c>.</summary>
    Ok = 0,

    /// <summary><c>NAPKIN_MLX_ERROR</c>: the sentence says what; nothing was produced.</summary>
    Error = 1,

    /// <summary><c>NAPKIN_MLX_CANCELLED</c>: the cancel flag was set; nothing was produced.</summary>
    Cancelled = 2,

    /// <summary><c>NAPKIN_MLX_BUSY</c>: a generation is already running on this model.</summary>
    Busy = 3,

    /// <summary><c>NAPKIN_MLX_NO_METAL</c>: no Metal device (headless, virtualized, or not Apple silicon).</summary>
    NoMetal = 4,

    /// <summary><c>NAPKIN_MLX_INCOMPLETE</c>: guided, and <c>max_tokens</c> ran out before the schema was satisfied.</summary>
    Incomplete = 5,
}

/// <summary><c>napkin_mlx_result.stop_reason</c>: why a generation ended.</summary>
public enum MlxStopReason
{
    /// <summary>0: the model's stop token.</summary>
    StopToken = 0,

    /// <summary>1: <c>max_tokens</c> ran out.</summary>
    Length = 1,

    /// <summary>2: the cancel flag.</summary>
    Cancelled = 2,

    /// <summary>3: guided, and the grammar accepted the document.</summary>
    SchemaComplete = 3,
}

/// <summary>A loaded model, as the bridge names it: an opaque pointer, never dereferenced by napkin.</summary>
/// <param name="Value">The pointer; zero is no model.</param>
public readonly record struct MlxModelHandle(nint Value)
{
    /// <summary>No model.</summary>
    public static MlxModelHandle None => default;

    /// <summary>Whether this is <see cref="None"/>.</summary>
    public bool IsNone => Value == 0;
}

/// <summary><c>napkin_mlx_device_info</c>, read: what the dialog's memory line says (mlx-runtime.md §4.4).</summary>
/// <param name="HasMetal">Whether Metal found a device.</param>
/// <param name="Architecture">Metal's <c>device.architecture.name</c>, or "Unknown".</param>
/// <param name="MemoryBytes">The machine's memory (<c>hw.memsize</c>).</param>
/// <param name="RecommendedWorkingSetBytes">Metal's <c>recommendedMaxWorkingSetSize</c>; 0 if none.</param>
/// <param name="MlxSwiftLmRevision">The mlx-swift-lm commit the bridge was built against.</param>
public readonly record struct MlxDeviceInfo(
    bool HasMetal,
    string Architecture,
    ulong MemoryBytes,
    ulong RecommendedWorkingSetBytes,
    string MlxSwiftLmRevision);

/// <summary><c>napkin_mlx_result</c>, read and freed: one reply and what it cost.</summary>
/// <param name="Text">The whole reply (guided: the JSON document).</param>
/// <param name="PromptTokens">Tokens in the prompt.</param>
/// <param name="GeneratedTokens">Tokens generated.</param>
/// <param name="StopReason">Why it ended.</param>
/// <param name="Prompt">Time spent on the prompt (guided: to the first emitted text).</param>
/// <param name="Generation">Time spent generating.</param>
public sealed record MlxGeneration(
    string Text,
    int PromptTokens,
    int GeneratedTokens,
    MlxStopReason StopReason,
    TimeSpan Prompt,
    TimeSpan Generation);

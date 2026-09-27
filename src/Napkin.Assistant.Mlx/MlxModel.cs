using System.Diagnostics;
using System.Globalization;

using Napkin.Assistant.LocalServer;
using Napkin.Modules.Assistant;

namespace Napkin.Assistant.Mlx;

/// <summary>Where a <see cref="MlxModel"/>'s weights are.</summary>
public enum MlxLoadState
{
    /// <summary>Not loaded: nothing asked yet, or the last load was refused (the next question tries again).</summary>
    NotLoaded,

    /// <summary>The bridge is reading the weights; <see cref="MlxModel.LoadingLine"/> says so.</summary>
    Loading,

    /// <summary>Loaded; every question is one generation.</summary>
    Loaded,
}

/// <summary>
/// A model run inside napkin's own process by MLX on an Apple silicon Mac, through the Swift bridge
/// (docs/design/mlx-runtime.md §4.1). Nothing leaves the machine: the weights are a folder on disk
/// and the bridge opens no socket.
/// </summary>
/// <remarks>
/// <para>
/// <b>Loading is once, and lazy.</b> The first <see cref="AskAsync"/>, <see cref="TestAsync"/> or
/// <see cref="LoadAsync"/> checks the bridge's interface version, initialises it with the
/// <c>mlx.metallib</c> beside it and loads the folder — on a thread-pool thread, under a lock, once.
/// A question that arrives while the weights load waits for the same load. A refused load is not
/// remembered: the next question tries again. The model stays loaded until <see cref="Dispose"/>.
/// </para>
/// <para>
/// <b>The two messages</b> are slice C's (<see cref="LocalServerModel.UserMessage"/>): the system
/// prompt alone, then <c>Context:</c> + the pack + <c>Question:</c>. Thinking is always off (Qwen3's
/// template flag, §5.2); the schema goes through verbatim as the bridge's <c>json_schema</c>; an answer
/// may use <see cref="TextMaxTokens"/>, a proposal <see cref="ProposalMaxTokens"/>.
/// </para>
/// <para>
/// <b>Time and cancelling.</b> napkin's question limit (<see cref="Timeout"/>, slice C's 30 s) starts
/// <i>after</i> the load — a first load reads gigabytes and cannot sit under it (§8.2). No reply in
/// time is a <see cref="ModelReply.Refused"/> (<see cref="NoReply"/>), as for a program on this
/// machine; the person's cancel is an <see cref="OperationCanceledException"/> and no reply is
/// surfaced. The bridge sees either at its next token. A load cannot be interrupted inside the
/// library, so a cancel during it returns at once while the load finishes behind it, ready for the
/// next question.
/// </para>
/// <para>
/// <b>Refusals</b>: the bridge's own sentence for an error, word for word; napkin's sentence for
/// running out of tokens under a schema (<see cref="RanOutOfRoom"/>), no Metal device
/// (<see cref="MlxAvailability.NoMetal"/>) and a generation already running
/// (<see cref="StillAnswering"/>); the loader's words when the bridge will not load at all.
/// </para>
/// </remarks>
public sealed class MlxModel : IAssistantModel, IDisposable
{
    /// <summary>The most tokens an answer may use (napkin's number, §4.1).</summary>
    public const int TextMaxTokens = 1024;

    /// <summary>The most tokens a proposal may use (napkin's number, §4.1).</summary>
    public const int ProposalMaxTokens = 2048;

    /// <summary>Nucleus sampling: off (§4.1).</summary>
    public const float TopP = 1.0f;

    /// <summary><see cref="MlxStatus.Incomplete"/>: the schema was not satisfied within <see cref="ProposalMaxTokens"/>.</summary>
    public const string RanOutOfRoom = "The assistant ran out of room before finishing the proposal.";

    /// <summary><see cref="MlxStatus.Busy"/>: the bridge answers one question at a time per model.</summary>
    public const string StillAnswering = "The assistant is still answering the last question.";

    private const string Closed = "The model was closed before it answered.";

    private readonly INativeMlx _native;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _closing = new();
    private readonly CancelFlag _loadCancel = new();
    private Task<Loaded>? _loading;
    private long _loadStarted;
    private TimeSpan? _loadTime;
    private MlxModelHandle _handle;
    private bool _disposed;

    /// <summary>A model in <paramref name="folder"/>, run by <paramref name="native"/>.</summary>
    /// <param name="native">The bridge: <see cref="NativeMlx"/>, or a <see cref="FakeNativeMlx"/> in a test.</param>
    /// <param name="folder">The checked model folder.</param>
    /// <param name="temperature">The sampling temperature for answers (a proposal is greedy under its schema, §2).</param>
    /// <param name="metallibPath">The <c>mlx.metallib</c> beside the bridge; null for <see cref="MlxBridge.MetallibPath"/> of this process.</param>
    /// <param name="timeout">napkin's limit on one question, counted after the load; null for <see cref="DefaultTimeout"/>.</param>
    public MlxModel(
        INativeMlx native,
        ModelFolder folder,
        double temperature = LocalServerModel.DefaultTemperature,
        string? metallibPath = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(folder);
        if (!double.IsFinite(temperature) || temperature < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), temperature, "A temperature is a number from zero up.");
        }

        TimeSpan limit = timeout ?? DefaultTimeout;
        if (limit <= TimeSpan.Zero && limit != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "A time limit is longer than zero.");
        }

        _native = native;
        Folder = folder;
        Temperature = temperature;
        MetallibPath = metallibPath ?? MlxBridge.MetallibPath(MlxBridge.BaseDirectory);
        Timeout = limit;
    }

    /// <summary>napkin's limit on one question, as for a program on this machine (llm-assistant.md §5.2).</summary>
    public static TimeSpan DefaultTimeout => LocalServerModel.DefaultTimeout;

    /// <summary>The model's folder.</summary>
    public ModelFolder Folder { get; }

    /// <summary>The temperature answers are sampled at.</summary>
    public double Temperature { get; }

    /// <summary>What <see cref="INativeMlx.Init"/> is given.</summary>
    public string MetallibPath { get; }

    /// <summary>napkin's limit on one question, after the load.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Where the weights are.</summary>
    public MlxLoadState State
    {
        get
        {
            lock (_gate)
            {
                return _loading switch
                {
                    null => MlxLoadState.NotLoaded,
                    { IsCompleted: false } => MlxLoadState.Loading,
                    _ => _handle.IsNone ? MlxLoadState.NotLoaded : MlxLoadState.Loaded,
                };
            }
        }
    }

    /// <summary>How long the load took, once loaded; null before.</summary>
    public TimeSpan? LoadTime
    {
        get
        {
            lock (_gate)
            {
                return _loadTime;
            }
        }
    }

    /// <summary>While loading, the note's thinking line: "loading the model (2.3 GB)… 12 s"; null otherwise.</summary>
    public string? LoadingLine
    {
        get
        {
            lock (_gate)
            {
                if (_loading is not { IsCompleted: false })
                {
                    return null;
                }

                int seconds = (int)Stopwatch.GetElapsedTime(_loadStarted).TotalSeconds;
                return string.Create(CultureInfo.InvariantCulture, $"loading the model ({Folder.WeightBytes / 1e9:0.0} GB)… {seconds} s");
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>"In napkin (MLX): Qwen3-4B-4bit from ~/Library/Application Support/napkin/models/mlx-community--Qwen3-4B-4bit--4dcb3d101c2a — nothing leaves this machine." (§4.1)</remarks>
    public string Whereabouts => $"In napkin (MLX): {Folder.DisplayName} from {Folder.HomeRelativePath} — nothing leaves this machine.";

    /// <summary>What a question that ran out of time is refused with.</summary>
    public string NoReply => string.Create(CultureInfo.InvariantCulture, $"No reply in {Timeout.TotalSeconds:0.#} s from {Folder.DisplayName} in napkin (MLX).");

    /// <summary>
    /// Loads the model if it is not loaded (checking the bridge's version and initialising it first),
    /// or waits for the load already under way. Cancelling returns at once; the load goes on.
    /// </summary>
    /// <param name="cancel">Stops waiting.</param>
    /// <returns>Null once loaded; otherwise why not, in the bridge's or napkin's words.</returns>
    public async Task<string?> LoadAsync(CancellationToken cancel)
    {
        Loaded loaded = await WaitForLoadAsync(cancel).ConfigureAwait(false);
        return loaded.Refusal;
    }

    /// <inheritdoc/>
    public async Task<ModelReply> AskAsync(ModelRequest request, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(request);
        Loaded loaded = await WaitForLoadAsync(cancel).ConfigureAwait(false);
        if (loaded.Refusal is { } refusal)
        {
            return new ModelReply.Refused(refusal);
        }

        return await GenerateAsync(loaded.Handle, request, cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// The dialog's Test (llm-assistant.md §8): loads the model if need be and asks it to reply "ok"
    /// with nothing about any design; the line gives the load's seconds too when this test loaded it.
    /// </summary>
    /// <param name="cancel">Cancels the test; the task then cancels.</param>
    public async Task<TestResult> TestAsync(CancellationToken cancel)
    {
        long started = Stopwatch.GetTimestamp();
        bool loadedBefore = State == MlxLoadState.Loaded;
        if (await LoadAsync(cancel).ConfigureAwait(false) is { } refusal)
        {
            return new TestResult(false, Stopwatch.GetElapsedTime(started), refusal);
        }

        TimeSpan loading = Stopwatch.GetElapsedTime(started);
        long asked = Stopwatch.GetTimestamp();
        ModelReply reply = await AskAsync(LocalServerModel.TestRequest, cancel).ConfigureAwait(false);
        TimeSpan answering = Stopwatch.GetElapsedTime(asked);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        if (reply is not ModelReply.Text text)
        {
            return new TestResult(false, elapsed, ((ModelReply.Refused)reply).Reason);
        }

        string said = Shorten(text.Answer);
        return new TestResult(
            true,
            elapsed,
            loadedBefore
                ? string.Create(CultureInfo.InvariantCulture, $"{Folder.DisplayName} replied in {answering.TotalSeconds:0.0} s: “{said}”.")
                : string.Create(CultureInfo.InvariantCulture, $"{Folder.DisplayName} loaded in {loading.TotalSeconds:0.0} s and replied in {answering.TotalSeconds:0.0} s: “{said}”."));
    }

    /// <summary>Unloads the model, once; a load still under way is dropped when it finishes, and a question still out is cancelled.</summary>
    public void Dispose()
    {
        MlxModelHandle handle;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            handle = _handle;
            _handle = MlxModelHandle.None;
        }

        // Neither is disposed while something may still be reading it: a load in the bridge polls
        // the flag (the P/Invoke holds its own reference to the SafeHandle), and a question still out
        // links its limit to _closing.
        _loadCancel.Set();
        _closing.Cancel();
        if (!handle.IsNone)
        {
            _native.Unload(handle);
        }
    }

    /// <summary>A bridge status other than OK as the sentence the note shows: the bridge's own for an error (word for word), napkin's for the three it words itself.</summary>
    /// <param name="status">The status.</param>
    /// <param name="error">The bridge's sentence, if it gave one.</param>
    internal static string Refusal(MlxStatus status, string? error) => status switch
    {
        MlxStatus.Incomplete => RanOutOfRoom,
        MlxStatus.NoMetal => MlxAvailability.NoMetal,
        MlxStatus.Busy => StillAnswering,
        _ => error ?? string.Create(CultureInfo.InvariantCulture, $"The MLX bridge answered with status {(int)status} ({status}) and did not say why."),
    };

    private async Task<Loaded> WaitForLoadAsync(CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        Loaded loaded = await StartLoad().WaitAsync(cancel).ConfigureAwait(false);
        if (loaded.IsClosed)
        {
            throw new OperationCanceledException(Closed);
        }

        return loaded;
    }

    /// <summary>The load under way or done; a new one when there is none, or the last was refused.</summary>
    private Task<Loaded> StartLoad()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loading is null || _loading.IsFaulted || (_loading.IsCompletedSuccessfully && _loading.Result.Refusal is not null))
            {
                _loadStarted = Stopwatch.GetTimestamp();
                _loading = Task.Run(LoadCore);
            }

            return _loading;
        }
    }

    /// <summary>Version, init, load — on a thread-pool thread, blocking it.</summary>
    private Loaded LoadCore()
    {
        try
        {
            int abi = _native.AbiVersion();
            if (abi != MlxBridge.AbiVersion)
            {
                return Loaded.Refuse(MlxAvailability.WrongAbi(abi));
            }

            MlxStatus init = _native.Init(MetallibPath, out string? initError);
            if (init != MlxStatus.Ok)
            {
                return Loaded.Refuse(Refusal(init, initError));
            }

            MlxStatus status = _native.Load(Folder.Path, _loadCancel, out MlxModelHandle handle, out string? error);
            if (status == MlxStatus.Cancelled && _loadCancel.IsSet)
            {
                return Loaded.WasClosed;
            }

            if (status != MlxStatus.Ok)
            {
                return Loaded.Refuse(Refusal(status, error));
            }

            lock (_gate)
            {
                if (!_disposed)
                {
                    _handle = handle;
                    _loadTime = Stopwatch.GetElapsedTime(_loadStarted);
                    return new Loaded(handle, null, false);
                }
            }

            // Closed while the bridge was loading, after it last read the flag: the handle is ours to drop.
            _native.Unload(handle);
            return Loaded.WasClosed;
        }
        catch (Exception exception) when (MlxAvailability.IsLoadFailure(exception))
        {
            return Loaded.Refuse(MlxAvailability.CouldNotLoad(exception));
        }
    }

    private async Task<ModelReply> GenerateAsync(MlxModelHandle handle, ModelRequest request, CancellationToken cancel)
    {
        string user = LocalServerModel.UserMessage(request);
        bool proposal = request.Schema is not null;
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancel, _closing.Token);
        limit.CancelAfter(Timeout);
        using CancelFlag flag = new();
        (MlxStatus status, MlxGeneration? result, string? error) outcome;
        using (flag.SetWhenCancelled(limit.Token))
        {
            outcome = await Task.Run(() =>
            {
                MlxStatus status = _native.Generate(
                    handle,
                    request.System,
                    user,
                    request.Schema,
                    proposal ? ProposalMaxTokens : TextMaxTokens,
                    (float)Temperature,
                    TopP,
                    enableThinking: false,
                    flag,
                    out MlxGeneration? result,
                    out string? error);
                return (status, result, error);
            }).ConfigureAwait(false);
        }

        cancel.ThrowIfCancellationRequested();
        if (_closing.IsCancellationRequested)
        {
            throw new OperationCanceledException(Closed);
        }

        return outcome.status switch
        {
            MlxStatus.Ok when outcome.result is { } result => Reply(result, proposal),
            MlxStatus.Ok => new ModelReply.Refused($"The MLX bridge said {Folder.DisplayName} answered but gave no reply."),
            MlxStatus.Cancelled when limit.IsCancellationRequested => new ModelReply.Refused(NoReply),
            _ => new ModelReply.Refused(Refusal(outcome.status, outcome.error)),
        };
    }

    private ModelReply Reply(MlxGeneration result, bool proposal)
    {
        string text = result.Text.Trim();
        if (text.Length == 0)
        {
            return new ModelReply.Refused($"{Folder.DisplayName} replied with nothing.");
        }

        return proposal ? new ModelReply.Json(text) : new ModelReply.Text(text);
    }

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";

    /// <summary>A load's outcome: a handle, a refusal, or closed before it finished.</summary>
    private sealed record Loaded(MlxModelHandle Handle, string? Refusal, bool IsClosed)
    {
        public static Loaded WasClosed { get; } = new(MlxModelHandle.None, null, true);

        public static Loaded Refuse(string refusal) => new(MlxModelHandle.None, refusal, false);
    }
}

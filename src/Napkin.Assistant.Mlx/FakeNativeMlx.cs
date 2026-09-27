using System.Collections.Immutable;
using System.Diagnostics;

namespace Napkin.Assistant.Mlx;

/// <summary>One scripted reply of a <see cref="FakeNativeMlx"/>.</summary>
/// <param name="Status">What the fake bridge says.</param>
/// <param name="Text">The reply's text when <paramref name="Status"/> is OK.</param>
/// <param name="Error">The sentence for any other status; null for the bridge's own sentence for that status.</param>
/// <param name="Delay">
/// How long the generation takes, polling the cancel flag as the bridge does every token; a flag set
/// meanwhile ends it <see cref="MlxStatus.Cancelled"/>. <see cref="Timeout.InfiniteTimeSpan"/> waits
/// for the flag.
/// </param>
public sealed record FakeMlxReply(MlxStatus Status, string Text, string? Error, TimeSpan Delay)
{
    /// <summary>An answer (or, when the request carried a schema, a document).</summary>
    public static FakeMlxReply Answer(string text, TimeSpan delay = default) => new(MlxStatus.Ok, text, null, delay);

    /// <summary>A status other than OK, with the bridge's sentence or <paramref name="error"/>.</summary>
    public static FakeMlxReply Refuse(MlxStatus status, string? error = null) => new(status, string.Empty, error, TimeSpan.Zero);

    /// <summary>A generation that runs until its cancel flag is set, then says <see cref="MlxStatus.Cancelled"/>.</summary>
    public static FakeMlxReply UntilCancelled() => new(MlxStatus.Ok, string.Empty, null, Timeout.InfiniteTimeSpan);
}

/// <summary>Every argument of one <see cref="INativeMlx.Generate"/> call a <see cref="FakeNativeMlx"/> received.</summary>
/// <param name="Model">The handle.</param>
/// <param name="System">The system message.</param>
/// <param name="User">The user message.</param>
/// <param name="JsonSchema">The schema, or null.</param>
/// <param name="MaxTokens">The token limit.</param>
/// <param name="Temperature">The temperature.</param>
/// <param name="TopP">Nucleus sampling.</param>
/// <param name="EnableThinking">The chat template's thinking flag.</param>
public sealed record FakeMlxRequest(
    MlxModelHandle Model,
    string System,
    string User,
    string? JsonSchema,
    int MaxTokens,
    float Temperature,
    float TopP,
    bool EnableThinking);

/// <summary>
/// A bridge that behaves like <c>libNapkinMlx.dylib</c> without being it: scripted statuses and
/// replies, every call recorded, the bridge's own rules kept — init before load, one generation at
/// a time per model (<see cref="MlxStatus.Busy"/>), the cancel flag polled while a reply is
/// "generating", a handle dead once unloaded — and the bridge's own sentences
/// (<c>native/NapkinMlx/Sources/NapkinMlx/Errors.swift</c> and <c>Bridge.swift</c>).
/// </summary>
/// <remarks>
/// It lives in the assembly, not in a test project, for the reason <c>ScriptedModel</c> lives in the
/// module (llm-assistant.md §2.1, mlx-runtime.md §9 slice B): the unit tests and the GUI suite both
/// need it, and neither may load the real library or need Metal. It is a legitimate
/// <see cref="INativeMlx"/>, not a leak of test code into the product; napkin never constructs it.
/// Scripting properties are read at each call, so a test may change one between calls.
/// </remarks>
public sealed class FakeNativeMlx : INativeMlx
{
    /// <summary>The bridge's sentence for a model that is not loaded (Bridge.swift).</summary>
    public const string NotLoadedSentence = "napkin_mlx_generate was given a model that is not loaded (unloaded already?).";

    /// <summary>The bridge's sentence for a load before a successful init (Bridge.swift).</summary>
    public const string NotInitialisedSentence = "napkin_mlx_init has not succeeded in this process, so no model can be loaded.";

    /// <summary>What a generation gets once the script has no reply left.</summary>
    public const string ScriptRanOut = "The fake MLX bridge has no reply left for this request.";

    private readonly object _gate = new();
    private readonly Queue<FakeMlxReply> _script;
    private readonly HashSet<nint> _live = [];
    private readonly HashSet<nint> _generating = [];
    private ImmutableList<string> _inits = [];
    private ImmutableList<string> _loads = [];
    private ImmutableList<FakeMlxRequest> _requests = [];
    private ImmutableList<MlxStatus> _outcomes = [];
    private ImmutableList<MlxModelHandle> _unloads = [];
    private int _abiCalls;
    private int _deviceCalls;
    private bool _initialised;
    private nint _nextHandle = 1;

    /// <summary>A fake bridge that replies from <paramref name="script"/>, in order.</summary>
    /// <param name="script">The generations' replies.</param>
    public FakeNativeMlx(params FakeMlxReply[] script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = new Queue<FakeMlxReply>(script);
    }

    /// <summary>A Mac with Metal: 24 GiB, Metal recommending 17 GiB.</summary>
    public static MlxDeviceInfo AppleSilicon { get; } = new(true, "applegpu_fake", 24UL << 30, 17UL << 30, "fake");

    /// <summary>What <see cref="AbiVersion"/> says; this napkin's by default.</summary>
    public int Abi { get; set; } = MlxBridge.AbiVersion;

    /// <summary>When set, every call throws it — a bridge the runtime cannot load (<see cref="DllNotFoundException"/>).</summary>
    public Exception? Throws { get; set; }

    /// <summary>What the first successful-until-then <see cref="Init"/> says.</summary>
    public MlxStatus InitStatus { get; set; } = MlxStatus.Ok;

    /// <summary><see cref="InitStatus"/>'s sentence; null for the bridge's own.</summary>
    public string? InitError { get; set; }

    /// <summary>What <see cref="Device"/> says.</summary>
    public MlxStatus DeviceStatus { get; set; } = MlxStatus.Ok;

    /// <summary><see cref="DeviceStatus"/>'s sentence; null for the bridge's own.</summary>
    public string? DeviceError { get; set; }

    /// <summary>What <see cref="Device"/> reports on OK.</summary>
    public MlxDeviceInfo DeviceInfo { get; set; } = AppleSilicon;

    /// <summary>What <see cref="Load"/> says once it has "read" the folder.</summary>
    public MlxStatus LoadStatus { get; set; } = MlxStatus.Ok;

    /// <summary><see cref="LoadStatus"/>'s sentence; null for the bridge's own.</summary>
    public string? LoadError { get; set; }

    /// <summary>How long a load takes. Not interruptible, as in the library: the flag is read before and after.</summary>
    public TimeSpan LoadDelay { get; set; }

    /// <summary>When set, a load waits for it before its <see cref="LoadDelay"/> — so a test can hold a load open.</summary>
    public ManualResetEventSlim? LoadGate { get; set; }

    /// <summary>How many times <see cref="AbiVersion"/> was called.</summary>
    public int AbiCalls => Volatile.Read(ref _abiCalls);

    /// <summary>How many times <see cref="Device"/> was called.</summary>
    public int DeviceCalls => Volatile.Read(ref _deviceCalls);

    /// <summary>The metallib path of every <see cref="Init"/>, in order.</summary>
    public ImmutableList<string> Inits
    {
        get
        {
            lock (_gate)
            {
                return _inits;
            }
        }
    }

    /// <summary>The folder of every <see cref="Load"/>, in order.</summary>
    public ImmutableList<string> Loads
    {
        get
        {
            lock (_gate)
            {
                return _loads;
            }
        }
    }

    /// <summary>Every <see cref="Generate"/> call's arguments, in order.</summary>
    public ImmutableList<FakeMlxRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests;
            }
        }
    }

    /// <summary>What every finished <see cref="Generate"/> returned, in the order they finished.</summary>
    public ImmutableList<MlxStatus> Outcomes
    {
        get
        {
            lock (_gate)
            {
                return _outcomes;
            }
        }
    }

    /// <summary>Every handle <see cref="Unload"/> was given, in order.</summary>
    public ImmutableList<MlxModelHandle> Unloads
    {
        get
        {
            lock (_gate)
            {
                return _unloads;
            }
        }
    }

    /// <summary>How many models are loaded and not unloaded.</summary>
    public int LiveModels
    {
        get
        {
            lock (_gate)
            {
                return _live.Count;
            }
        }
    }

    /// <summary>The bridge's sentence for a status when nothing more specific is known (Errors.swift, <c>sentence(for:)</c>).</summary>
    /// <param name="status">A status other than OK.</param>
    public static string SentenceFor(MlxStatus status) => status switch
    {
        MlxStatus.Error => "The MLX bridge failed.",
        MlxStatus.Cancelled => "Cancelled; nothing was produced.",
        MlxStatus.Busy => "This model is already answering; it answers one request at a time.",
        MlxStatus.NoMetal => "MLX found no Metal device on this Mac (a headless or virtualized session, or not Apple silicon).",
        MlxStatus.Incomplete => "The reply ran out of tokens before it matched the schema, so there is no proposal.",
        _ => $"Unknown status {(int)status}.",
    };

    /// <inheritdoc/>
    public int AbiVersion()
    {
        ThrowIfScripted();
        Interlocked.Increment(ref _abiCalls);
        return Abi;
    }

    /// <inheritdoc/>
    public MlxStatus Init(string metallibPath, out string? error)
    {
        ThrowIfScripted();
        lock (_gate)
        {
            _inits = _inits.Add(metallibPath);
            if (!_initialised && InitStatus != MlxStatus.Ok)
            {
                error = InitError ?? SentenceFor(InitStatus);
                return InitStatus;
            }

            _initialised = true;
            error = null;
            return MlxStatus.Ok;
        }
    }

    /// <inheritdoc/>
    public MlxStatus Device(out MlxDeviceInfo info, out string? error)
    {
        ThrowIfScripted();
        Interlocked.Increment(ref _deviceCalls);
        if (DeviceStatus != MlxStatus.Ok)
        {
            info = default;
            error = DeviceError ?? SentenceFor(DeviceStatus);
            return DeviceStatus;
        }

        info = DeviceInfo;
        error = null;
        return MlxStatus.Ok;
    }

    /// <inheritdoc/>
    public MlxStatus Load(string modelFolder, CancelFlag cancel, out MlxModelHandle handle, out string? error)
    {
        ThrowIfScripted();
        ArgumentNullException.ThrowIfNull(cancel);
        handle = MlxModelHandle.None;
        lock (_gate)
        {
            _loads = _loads.Add(modelFolder);
            if (!_initialised)
            {
                error = NotInitialisedSentence;
                return MlxStatus.Error;
            }
        }

        if (cancel.IsSet)
        {
            error = SentenceFor(MlxStatus.Cancelled);
            return MlxStatus.Cancelled;
        }

        LoadGate?.Wait();
        if (LoadDelay > TimeSpan.Zero)
        {
            Thread.Sleep(LoadDelay);
        }

        if (cancel.IsSet)
        {
            error = SentenceFor(MlxStatus.Cancelled);
            return MlxStatus.Cancelled;
        }

        if (LoadStatus != MlxStatus.Ok)
        {
            error = LoadError ?? SentenceFor(LoadStatus);
            return LoadStatus;
        }

        lock (_gate)
        {
            handle = new MlxModelHandle(_nextHandle++);
            _live.Add(handle.Value);
        }

        error = null;
        return MlxStatus.Ok;
    }

    /// <inheritdoc/>
    public MlxStatus Generate(
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
        out string? error)
    {
        ThrowIfScripted();
        ArgumentNullException.ThrowIfNull(cancel);
        result = null;
        FakeMlxReply? reply;
        lock (_gate)
        {
            _requests = _requests.Add(new FakeMlxRequest(model, system, user, jsonSchema, maxTokens, temperature, topP, enableThinking));
            if (!_live.Contains(model.Value))
            {
                return Finish(MlxStatus.Error, NotLoadedSentence, out error);
            }

            if (!_generating.Add(model.Value))
            {
                return Finish(MlxStatus.Busy, SentenceFor(MlxStatus.Busy), out error);
            }

            _script.TryDequeue(out reply);
        }

        try
        {
            if (reply is null)
            {
                return Finish(MlxStatus.Error, ScriptRanOut, out error);
            }

            if (!Generating(reply.Delay, cancel))
            {
                return Finish(MlxStatus.Cancelled, SentenceFor(MlxStatus.Cancelled), out error);
            }

            if (reply.Status != MlxStatus.Ok)
            {
                return Finish(reply.Status, reply.Error ?? SentenceFor(reply.Status), out error);
            }

            result = new MlxGeneration(
                reply.Text,
                (system.Length + user.Length) / 4,
                reply.Text.Length / 4,
                jsonSchema is null ? MlxStopReason.StopToken : MlxStopReason.SchemaComplete,
                TimeSpan.Zero,
                reply.Delay);
            return Finish(MlxStatus.Ok, null, out error);
        }
        finally
        {
            lock (_gate)
            {
                _generating.Remove(model.Value);
            }
        }
    }

    /// <inheritdoc/>
    public void Unload(MlxModelHandle model)
    {
        ThrowIfScripted();
        lock (_gate)
        {
            _unloads = _unloads.Add(model);
            _live.Remove(model.Value);
        }
    }

    /// <summary>"Generates" for <paramref name="delay"/>, polling the flag as the bridge does every token: false when the flag was set first.</summary>
    private static bool Generating(TimeSpan delay, CancelFlag cancel)
    {
        long started = Stopwatch.GetTimestamp();
        while (!cancel.IsSet)
        {
            if (delay != Timeout.InfiniteTimeSpan && Stopwatch.GetElapsedTime(started) >= delay)
            {
                return true;
            }

            Thread.Sleep(2);
        }

        return false;
    }

    private MlxStatus Finish(MlxStatus status, string? sentence, out string? error)
    {
        lock (_gate)
        {
            _outcomes = _outcomes.Add(status);
        }

        error = sentence;
        return status;
    }

    private void ThrowIfScripted()
    {
        if (Throws is { } exception)
        {
            throw exception;
        }
    }
}

using System.Text.RegularExpressions;

using Napkin.Assistant.LocalServer;
using Napkin.Modules.Assistant;

namespace Napkin.Assistant.Mlx.Tests;

/// <summary>
/// The MLX runtime through <see cref="FakeNativeMlx"/> (mlx-runtime.md §7.2): loading once and off
/// the calling thread, the two messages and the flags every question carries, every status as the
/// reply the note shows, the time limit counted after the load, cancelling, and putting the model away.
/// </summary>
[Trait("Feature", "AST-007")]
public sealed class MlxModelTests : IDisposable
{
    private const string Schema = """{"type":"object","properties":{"parts":{"type":"array"}},"required":["parts"]}""";

    private static readonly ModelRequest Answer = new("You are napkin's assistant.", "[1] Leg — 2 x 16 x 3/4", "why is the leg 16 inches?", null);
    private static readonly ModelRequest Proposal = new("Propose rough planks.", string.Empty, "a bench, 4 feet long", Schema);

    private readonly TempModel _model = TempModel.Complete();

    public void Dispose() => _model.Dispose();

    [Fact]
    public async Task TheFirstQuestionLoadsOffTheCallingThreadAndLaterOnesDoNot()
    {
        using ManualResetEventSlim gate = new();
        FakeNativeMlx native = new(FakeMlxReply.Answer("ok"), FakeMlxReply.Answer("again")) { LoadGate = gate };
        using MlxModel model = new(native, _model.Folder(), metallibPath: "/app/native/mlx.metallib");
        Assert.Equal(MlxLoadState.NotLoaded, model.State);

        Task<ModelReply> first = model.AskAsync(Answer, CancellationToken.None);
        await Eventually.True(() => native.Loads.Count == 1, "the load to start");

        // The load is held open on a pool thread; the question has returned a task and waits.
        Assert.False(first.IsCompleted);
        Assert.Equal(MlxLoadState.Loading, model.State);
        Task<string?> alsoWaiting = model.LoadAsync(CancellationToken.None);
        Assert.False(alsoWaiting.IsCompleted);

        gate.Set();
        Assert.Equal(new ModelReply.Text("ok"), await first);
        Assert.Null(await alsoWaiting);
        Assert.Equal(new ModelReply.Text("again"), await model.AskAsync(Answer, CancellationToken.None));

        Assert.Equal(1, native.AbiCalls);
        Assert.Equal(["/app/native/mlx.metallib"], native.Inits);
        Assert.Equal([_model.Path], native.Loads);
        Assert.Equal(MlxLoadState.Loaded, model.State);
        Assert.NotNull(model.LoadTime);
        Assert.Null(model.LoadingLine);
    }

    [Fact]
    public async Task AnAnswerIsAskedWithTheTwoMessagesThinkingOffAndNapkinsNumbers()
    {
        FakeNativeMlx native = new(FakeMlxReply.Answer("  Because the table says so.\n"));
        using MlxModel model = new(native, _model.Folder(), temperature: 0.35);

        ModelReply reply = await model.AskAsync(Answer, CancellationToken.None);

        Assert.Equal(new ModelReply.Text("Because the table says so."), reply);
        FakeMlxRequest asked = Assert.Single(native.Requests);
        Assert.Equal(Answer.System, asked.System);
        Assert.Equal(LocalServerModel.UserMessage(Answer), asked.User);
        Assert.Equal("Context:\n[1] Leg — 2 x 16 x 3/4\n\nQuestion: why is the leg 16 inches?", asked.User);
        Assert.Null(asked.JsonSchema);
        Assert.Equal(1024, asked.MaxTokens);
        Assert.Equal(0.35f, asked.Temperature);
        Assert.Equal(1.0f, asked.TopP);
        Assert.False(asked.EnableThinking);
    }

    [Fact]
    public async Task AProposalPassesTheSchemaThroughVerbatimAndIsADocument()
    {
        FakeNativeMlx native = new(FakeMlxReply.Answer("""{"parts":[]}"""));
        using MlxModel model = new(native, _model.Folder());

        ModelReply reply = await model.AskAsync(Proposal, CancellationToken.None);

        Assert.Equal(new ModelReply.Json("""{"parts":[]}"""), reply);
        FakeMlxRequest asked = Assert.Single(native.Requests);
        Assert.Same(Schema, asked.JsonSchema);
        Assert.Equal("Question: a bench, 4 feet long", asked.User);
        Assert.Equal(2048, asked.MaxTokens);
        Assert.Equal(0.2f, asked.Temperature);
        Assert.False(asked.EnableThinking);
    }

    [Theory]
    [InlineData(MlxStatus.Incomplete, "The assistant ran out of room before finishing the proposal.")]
    [InlineData(MlxStatus.NoMetal, "MLX found no Metal device on this Mac.")]
    [InlineData(MlxStatus.Busy, "The assistant is still answering the last question.")]
    [InlineData(MlxStatus.Error, "The model could not answer: the prompt is longer than the context window.")]
    [InlineData(MlxStatus.Cancelled, "Cancelled; nothing was produced.")]
    [InlineData((MlxStatus)9, "Unknown status 9.")]
    public async Task EveryStatusIsTheSentenceTheNoteShows(MlxStatus status, string sentence)
    {
        string? bridgeSays = status == MlxStatus.Error ? "The model could not answer: the prompt is longer than the context window." : null;
        FakeNativeMlx native = new(FakeMlxReply.Refuse(status, bridgeSays));
        using MlxModel model = new(native, _model.Folder());

        Assert.Equal(new ModelReply.Refused(sentence), await model.AskAsync(Proposal, CancellationToken.None));
    }

    [Theory]
    [InlineData(MlxStatus.Error, null, "The MLX bridge answered with status 1 (Error) and did not say why.")]
    [InlineData(MlxStatus.Error, "There is no folder at /x.", "There is no folder at /x.")]
    [InlineData((MlxStatus)7, null, "The MLX bridge answered with status 7 (7) and did not say why.")]
    [InlineData(MlxStatus.Busy, "This model is already answering; it answers one request at a time.", "The assistant is still answering the last question.")]
    public void ARefusalIsTheBridgesSentenceUnlessNapkinWordsTheStatus(MlxStatus status, string? error, string refusal) =>
        Assert.Equal(refusal, MlxModel.Refusal(status, error));

    [Fact]
    public async Task AnEmptyReplyIsRefused()
    {
        using MlxModel model = new(new FakeNativeMlx(FakeMlxReply.Answer(" \n ")), _model.Folder());

        Assert.Equal(new ModelReply.Refused("Qwen3-4B-4bit replied with nothing."), await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public async Task AnOkWithNoReplyIsRefused()
    {
        using MlxModel model = new(new OkWithoutResult(), _model.Folder());

        Assert.Equal(
            new ModelReply.Refused("The MLX bridge said Qwen3-4B-4bit answered but gave no reply."),
            await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public async Task AnInitTheBridgeRefusesIsItsSentenceAndIsTriedAgainNextTime()
    {
        FakeNativeMlx native = new(FakeMlxReply.Answer("ok"))
        {
            InitStatus = MlxStatus.Error,
            InitError = "MLX loads its Metal library only from beside the bridge, /app/native/mlx.metallib; napkin passed /elsewhere/mlx.metallib.",
        };
        using MlxModel model = new(native, _model.Folder());

        Assert.Equal(new ModelReply.Refused(native.InitError!), await model.AskAsync(Answer, CancellationToken.None));
        Assert.Equal(MlxLoadState.NotLoaded, model.State);
        Assert.Empty(native.Loads);

        native.InitStatus = MlxStatus.Ok;
        Assert.Equal(new ModelReply.Text("ok"), await model.AskAsync(Answer, CancellationToken.None));
        Assert.Equal(2, native.Inits.Count);
    }

    [Fact]
    public async Task NoMetalAtInitIsNapkinsSentence()
    {
        using MlxModel model = new(new FakeNativeMlx { InitStatus = MlxStatus.NoMetal }, _model.Folder());

        Assert.Equal(new ModelReply.Refused("MLX found no Metal device on this Mac."), await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public async Task ALoadTheBridgeRefusesIsItsSentenceAndIsTriedAgainNextTime()
    {
        FakeNativeMlx native = new(FakeMlxReply.Answer("ok"))
        {
            LoadStatus = MlxStatus.Error,
            LoadError = "napkin could not load the model in /models/qwen: Unsupported model type.",
        };
        using MlxModel model = new(native, _model.Folder());

        Assert.Equal("napkin could not load the model in /models/qwen: Unsupported model type.", await model.LoadAsync(CancellationToken.None));
        Assert.Equal(MlxLoadState.NotLoaded, model.State);
        Assert.Null(model.LoadTime);

        native.LoadStatus = MlxStatus.Ok;
        Assert.Equal(new ModelReply.Text("ok"), await model.AskAsync(Answer, CancellationToken.None));
        Assert.Equal(2, native.Loads.Count);
        Assert.Single(native.Inits.Distinct());
    }

    [Fact]
    public async Task ABridgeOfAnotherInterfaceVersionIsNotInitialised()
    {
        FakeNativeMlx native = new() { Abi = 3 };
        using MlxModel model = new(native, _model.Folder());

        Assert.Equal(
            new ModelReply.Refused("The MLX bridge speaks version 3 of its interface; this napkin speaks version 1."),
            await model.AskAsync(Answer, CancellationToken.None));
        Assert.Empty(native.Inits);
    }

    [Fact]
    public async Task ABridgeThatWillNotLoadIsRefusedInTheLoadersWords()
    {
        FakeNativeMlx native = new() { Throws = new DllNotFoundException("Unable to load shared library '/app/native/libNapkinMlx.dylib' or one of its dependencies.") };
        using MlxModel model = new(native, _model.Folder());

        Assert.Equal(
            new ModelReply.Refused("napkin could not load the MLX bridge: Unable to load shared library '/app/native/libNapkinMlx.dylib' or one of its dependencies."),
            await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public async Task ABugInALoadIsNotHiddenAndTheNextQuestionLoadsAfresh()
    {
        FakeNativeMlx native = new(FakeMlxReply.Answer("ok")) { Throws = new InvalidOperationException("a bug") };
        using MlxModel model = new(native, _model.Folder());

        await Assert.ThrowsAsync<InvalidOperationException>(() => model.AskAsync(Answer, CancellationToken.None));

        native.Throws = null;
        Assert.Equal(new ModelReply.Text("ok"), await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public async Task EscapeWhileGeneratingCancelsAtTheNextTokenAndSurfacesNoReply()
    {
        FakeNativeMlx native = new(FakeMlxReply.UntilCancelled());
        using MlxModel model = new(native, _model.Folder());
        using CancellationTokenSource escape = new();

        Task<ModelReply> asking = model.AskAsync(Answer, escape.Token);
        await Eventually.True(() => native.Requests.Count == 1, "the generation to start");
        escape.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => asking);
        Assert.Equal([MlxStatus.Cancelled], native.Outcomes);
    }

    [Fact]
    public async Task AQuestionCancelledBeforeItIsAskedLoadsNothing()
    {
        FakeNativeMlx native = new();
        using MlxModel model = new(native, _model.Folder());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.AskAsync(Answer, new CancellationToken(canceled: true)));
        Assert.Equal(0, native.AbiCalls);
    }

    [Fact]
    public async Task EscapeDuringTheLoadReturnsAtOnceAndTheLoadIsKeptForTheNextQuestion()
    {
        using ManualResetEventSlim gate = new();
        FakeNativeMlx native = new(FakeMlxReply.Answer("ok")) { LoadGate = gate };
        using MlxModel model = new(native, _model.Folder());
        using CancellationTokenSource escape = new();

        Task<ModelReply> asking = model.AskAsync(Answer, escape.Token);
        await Eventually.True(() => native.Loads.Count == 1, "the load to start");
        escape.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => asking);
        Assert.Equal(MlxLoadState.Loading, model.State);

        gate.Set();
        Assert.Equal(new ModelReply.Text("ok"), await model.AskAsync(Answer, CancellationToken.None));
        Assert.Single(native.Loads);
    }

    [Fact]
    public async Task TheTimeLimitStartsAfterTheLoad()
    {
        FakeNativeMlx native = new(FakeMlxReply.Answer("ok", TimeSpan.FromMilliseconds(50))) { LoadDelay = TimeSpan.FromMilliseconds(900) };
        using MlxModel model = new(native, _model.Folder(), timeout: TimeSpan.FromMilliseconds(400));

        Assert.Equal(new ModelReply.Text("ok"), await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public async Task NoReplyInTimeIsRefusedAsNoReply()
    {
        FakeNativeMlx native = new(FakeMlxReply.UntilCancelled());
        using MlxModel model = new(native, _model.Folder(), timeout: TimeSpan.FromMilliseconds(200));

        ModelReply reply = await model.AskAsync(Answer, CancellationToken.None);

        Assert.Equal(new ModelReply.Refused("No reply in 0.2 s from Qwen3-4B-4bit in napkin (MLX)."), reply);
        Assert.Equal([MlxStatus.Cancelled], native.Outcomes);
    }

    [Fact]
    public void TheDefaultLimitIsSliceCs30Seconds()
    {
        using MlxModel model = new(new FakeNativeMlx(), _model.Folder());

        Assert.Equal(TimeSpan.FromSeconds(30), model.Timeout);
        Assert.Equal(LocalServerModel.DefaultTimeout, MlxModel.DefaultTimeout);
        Assert.Equal("No reply in 30 s from Qwen3-4B-4bit in napkin (MLX).", model.NoReply);
        Assert.Equal(0.2, model.Temperature);
        Assert.Equal(MlxBridge.MetallibPath(AppContext.BaseDirectory), model.MetallibPath);
    }

    [Fact]
    public async Task ASecondQuestionWhileTheFirstIsAnsweredIsBusy()
    {
        FakeNativeMlx native = new(FakeMlxReply.UntilCancelled(), FakeMlxReply.Answer("unused"));
        using MlxModel model = new(native, _model.Folder());
        using CancellationTokenSource first = new();

        Task<ModelReply> answering = model.AskAsync(Answer, first.Token);
        await Eventually.True(() => native.Requests.Count == 1, "the first generation to start");

        Assert.Equal(new ModelReply.Refused("The assistant is still answering the last question."), await model.AskAsync(Answer, CancellationToken.None));

        first.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => answering);
    }

    [Fact]
    public void TheWhereaboutsLineNamesTheModelAndItsFolder()
    {
        using TempModel downloaded = TempModel.Complete("mlx-community--Qwen3-4B-4bit--4dcb3d101c2a");
        ModelFolder folder = downloaded.Folder();
        using MlxModel model = new(new FakeNativeMlx(), folder);

        Assert.Equal($"In napkin (MLX): Qwen3-4B-4bit from {folder.HomeRelativePath} — nothing leaves this machine.", model.Whereabouts);
    }

    [Fact]
    public async Task WhileLoadingTheLineSaysHowBigAndHowLong()
    {
        using TempModel big = TempModel.Complete();
        using (FileStream weights = new(big.File("model.safetensors"), FileMode.Create))
        {
            weights.SetLength(160_000_000);
        }

        using ManualResetEventSlim gate = new();
        FakeNativeMlx native = new() { LoadGate = gate };
        using MlxModel model = new(native, big.Folder());
        Assert.Null(model.LoadingLine);

        Task<string?> loading = model.LoadAsync(CancellationToken.None);
        await Eventually.True(() => native.Loads.Count == 1, "the load to start");

        Assert.Matches(new Regex(@"^loading the model \(0\.2 GB\)… \d+ s$"), model.LoadingLine);
        gate.Set();
        Assert.Null(await loading);
        Assert.Null(model.LoadingLine);
    }

    [Fact]
    public async Task TestLoadsThenAsksForOkAndSaysHowLongEachTook()
    {
        FakeNativeMlx native = new(FakeMlxReply.Answer("ok"), FakeMlxReply.Answer("ok, and a very long answer that runs past forty characters"));
        using MlxModel model = new(native, _model.Folder());

        TestResult first = await model.TestAsync(CancellationToken.None);
        TestResult second = await model.TestAsync(CancellationToken.None);

        Assert.True(first.Replied);
        Assert.Matches(new Regex(@"^Qwen3-4B-4bit loaded in \d+\.\d s and replied in \d+\.\d s: “ok”\.$"), first.Line);
        Assert.True(second.Replied);
        Assert.Matches(new Regex(@"^Qwen3-4B-4bit replied in \d+\.\d s: “ok, and a very long answer that runs pas…”\.$"), second.Line);
        Assert.Equal(LocalServerModel.TestRequest.System, native.Requests[0].System);
        Assert.Equal("Question: Reply with ok.", native.Requests[0].User);
    }

    [Fact]
    public async Task TestReportsARefusedLoadOrReplyAsItIs()
    {
        FakeNativeMlx native = new(FakeMlxReply.Refuse(MlxStatus.Error, "The model could not answer: out of memory."))
        {
            LoadStatus = MlxStatus.Error,
            LoadError = "napkin could not load the model in /m: no weights.",
        };
        using MlxModel model = new(native, _model.Folder());

        TestResult load = await model.TestAsync(CancellationToken.None);
        native.LoadStatus = MlxStatus.Ok;
        TestResult reply = await model.TestAsync(CancellationToken.None);

        Assert.Equal((false, "napkin could not load the model in /m: no weights."), (load.Replied, load.Line));
        Assert.Equal((false, "The model could not answer: out of memory."), (reply.Replied, reply.Line));
    }

    [Fact]
    public async Task DisposeUnloadsExactlyOnce()
    {
        FakeNativeMlx native = new(FakeMlxReply.Answer("ok"));
        MlxModel model = new(native, _model.Folder());
        await model.AskAsync(Answer, CancellationToken.None);

        model.Dispose();
        model.Dispose();

        Assert.Equal([new MlxModelHandle(1)], native.Unloads);
        Assert.Equal(0, native.LiveModels);
        Assert.Equal(MlxLoadState.NotLoaded, model.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public void DisposingAModelNeverLoadedUnloadsNothing()
    {
        FakeNativeMlx native = new();
        new MlxModel(native, _model.Folder()).Dispose();

        Assert.Empty(native.Unloads);
    }

    [Fact]
    public async Task DisposeDuringTheLoadDropsTheModelTheBridgeWasLoading()
    {
        using ManualResetEventSlim gate = new();
        FakeNativeMlx native = new() { LoadGate = gate };
        MlxModel model = new(native, _model.Folder());

        Task<ModelReply> asking = model.AskAsync(Answer, CancellationToken.None);
        await Eventually.True(() => native.Loads.Count == 1, "the load to start");
        model.Dispose();
        gate.Set();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => asking);
        Assert.Equal(0, native.LiveModels);
    }

    [Fact]
    public async Task ALoadThatFinishesJustAfterDisposeIsUnloadedByNapkin()
    {
        FakeNativeMlx inner = new();
        DisposingOnLoad native = new(inner);
        MlxModel model = new(native, _model.Folder());
        native.Model = model;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.AskAsync(Answer, CancellationToken.None));

        Assert.Equal([new MlxModelHandle(1)], inner.Unloads);
        Assert.Equal(0, inner.LiveModels);
    }

    [Fact]
    public async Task DisposeWhileAnsweringCancelsTheQuestion()
    {
        FakeNativeMlx native = new(FakeMlxReply.UntilCancelled());
        MlxModel model = new(native, _model.Folder());

        Task<ModelReply> asking = model.AskAsync(Answer, CancellationToken.None);
        await Eventually.True(() => native.Requests.Count == 1, "the generation to start");
        model.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => asking);
        Assert.Equal([MlxStatus.Cancelled], native.Outcomes);
        Assert.Single(native.Unloads);
    }

    [Fact]
    public void AModelNeedsABridgeAFolderASaneTemperatureAndATimeLimit()
    {
        ModelFolder folder = _model.Folder();
        FakeNativeMlx native = new();

        Assert.Throws<ArgumentNullException>(() => new MlxModel(null!, folder));
        Assert.Throws<ArgumentNullException>(() => new MlxModel(native, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MlxModel(native, folder, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MlxModel(native, folder, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MlxModel(native, folder, timeout: TimeSpan.Zero));
        using MlxModel forever = new(native, folder, timeout: Timeout.InfiniteTimeSpan);
        Assert.Equal(Timeout.InfiniteTimeSpan, forever.Timeout);
    }

    [Fact]
    public async Task ANullRequestIsRefusedAtOnce()
    {
        using MlxModel model = new(new FakeNativeMlx(), _model.Folder());

        await Assert.ThrowsAsync<ArgumentNullException>(() => model.AskAsync(null!, CancellationToken.None));
    }

    /// <summary>A bridge that says OK and returns no result: what napkin does with a broken promise.</summary>
    private sealed class OkWithoutResult : INativeMlx
    {
        private readonly FakeNativeMlx _inner = new();

        public int AbiVersion() => _inner.AbiVersion();

        public MlxStatus Init(string metallibPath, out string? error) => _inner.Init(metallibPath, out error);

        public MlxStatus Device(out MlxDeviceInfo info, out string? error) => _inner.Device(out info, out error);

        public MlxStatus Load(string modelFolder, CancelFlag cancel, out MlxModelHandle handle, out string? error) =>
            _inner.Load(modelFolder, cancel, out handle, out error);

        public MlxStatus Generate(MlxModelHandle model, string system, string user, string? jsonSchema, int maxTokens, float temperature, float topP, bool enableThinking, CancelFlag cancel, out MlxGeneration? result, out string? error)
        {
            result = null;
            error = null;
            return MlxStatus.Ok;
        }

        public void Unload(MlxModelHandle model) => _inner.Unload(model);
    }

    /// <summary>A bridge whose load finishes OK just after the model was closed — after it last read the cancel flag.</summary>
    private sealed class DisposingOnLoad(FakeNativeMlx inner) : INativeMlx
    {
        public MlxModel? Model { get; set; }

        public int AbiVersion() => inner.AbiVersion();

        public MlxStatus Init(string metallibPath, out string? error) => inner.Init(metallibPath, out error);

        public MlxStatus Device(out MlxDeviceInfo info, out string? error) => inner.Device(out info, out error);

        public MlxStatus Load(string modelFolder, CancelFlag cancel, out MlxModelHandle handle, out string? error)
        {
            MlxStatus status = inner.Load(modelFolder, cancel, out handle, out error);
            Model!.Dispose();
            return status;
        }

        public MlxStatus Generate(MlxModelHandle model, string system, string user, string? jsonSchema, int maxTokens, float temperature, float topP, bool enableThinking, CancelFlag cancel, out MlxGeneration? result, out string? error) =>
            inner.Generate(model, system, user, jsonSchema, maxTokens, temperature, topP, enableThinking, cancel, out result, out error);

        public void Unload(MlxModelHandle model) => inner.Unload(model);
    }
}

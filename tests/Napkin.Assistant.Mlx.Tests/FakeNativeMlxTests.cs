using System.Text.RegularExpressions;

namespace Napkin.Assistant.Mlx.Tests;

/// <summary>
/// The fake keeps the bridge's rules and speaks its sentences, so a test (or a GUI workflow) that
/// passes through it has passed through what the real bridge would have said.
/// </summary>
public class FakeNativeMlxTests
{
    [Fact]
    public void ItLoadsOnlyAfterASuccessfulInitAsTheBridgeDoes()
    {
        FakeNativeMlx native = new() { InitStatus = MlxStatus.NoMetal };
        using CancelFlag flag = new();

        Assert.Equal(MlxStatus.Error, native.Load("/m", flag, out MlxModelHandle before, out string? notYet));
        Assert.Equal(FakeNativeMlx.NotInitialisedSentence, notYet);
        Assert.True(before.IsNone);

        Assert.Equal(MlxStatus.NoMetal, native.Init("/app/native/mlx.metallib", out string? noMetal));
        Assert.Equal("MLX found no Metal device on this Mac (a headless or virtualized session, or not Apple silicon).", noMetal);

        native.InitStatus = MlxStatus.Ok;
        Assert.Equal(MlxStatus.Ok, native.Init("/app/native/mlx.metallib", out string? none));
        Assert.Null(none);

        // Latched on success: a later init is a no-op, whatever it would have said.
        native.InitStatus = MlxStatus.Error;
        Assert.Equal(MlxStatus.Ok, native.Init("/app/native/mlx.metallib", out _));

        Assert.Equal(MlxStatus.Ok, native.Load("/m", flag, out MlxModelHandle handle, out string? loaded));
        Assert.Null(loaded);
        Assert.Equal(new MlxModelHandle(1), handle);
        Assert.Equal(["/m", "/m"], native.Loads);
        Assert.Equal(1, native.LiveModels);
    }

    [Fact]
    public void ACancelSeenBeforeOrAfterTheLoadIsCancelled()
    {
        FakeNativeMlx native = Initialised();
        using CancelFlag set = new();
        set.Set();

        Assert.Equal(MlxStatus.Cancelled, native.Load("/m", set, out _, out string? before));
        Assert.Equal("Cancelled; nothing was produced.", before);

        using ManualResetEventSlim gate = new();
        native.LoadGate = gate;
        using CancelFlag during = new();
        Task<MlxStatus> loading = Task.Run(() => native.Load("/m", during, out _, out _));
        during.Set();
        gate.Set();

        Assert.Equal(MlxStatus.Cancelled, loading.Result);
        Assert.Equal(0, native.LiveModels);
    }

    [Fact]
    public void ALoadCanBeRefusedInItsOwnWordsOrTheBridges()
    {
        FakeNativeMlx native = Initialised();
        using CancelFlag flag = new();
        native.LoadStatus = MlxStatus.Error;

        Assert.Equal(MlxStatus.Error, native.Load("/m", flag, out _, out string? bridge));
        Assert.Equal("The MLX bridge failed.", bridge);

        native.LoadError = "napkin could not load the model in /m: no.";
        Assert.Equal(MlxStatus.Error, native.Load("/m", flag, out _, out string? own));
        Assert.Equal("napkin could not load the model in /m: no.", own);
    }

    [Fact]
    public void ALoadTakesItsDelay()
    {
        FakeNativeMlx native = Initialised();
        native.LoadDelay = TimeSpan.FromMilliseconds(30);
        using CancelFlag flag = new();
        DateTime started = DateTime.UtcNow;

        Assert.Equal(MlxStatus.Ok, native.Load("/m", flag, out _, out _));
        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(25));
    }

    [Fact]
    public void AGenerationRecordsEveryArgumentAndReportsWhatItMade()
    {
        FakeNativeMlx native = Initialised(FakeMlxReply.Answer("{}"), FakeMlxReply.Answer("an answer", TimeSpan.FromMilliseconds(20)));
        MlxModelHandle model = Loaded(native);
        using CancelFlag flag = new();

        Assert.Equal(MlxStatus.Ok, native.Generate(model, "sys", "user", "{\"type\":\"object\"}", 2048, 0.2f, 1f, false, flag, out MlxGeneration? document, out string? none));
        Assert.Equal(MlxStatus.Ok, native.Generate(model, "sys", "a user message", null, 1024, 0.7f, 0.8f, true, flag, out MlxGeneration? answer, out _));

        Assert.Null(none);
        Assert.Equal(new MlxGeneration("{}", 1, 0, MlxStopReason.SchemaComplete, TimeSpan.Zero, TimeSpan.Zero), document);
        Assert.Equal(new MlxGeneration("an answer", 4, 2, MlxStopReason.StopToken, TimeSpan.Zero, TimeSpan.FromMilliseconds(20)), answer);
        Assert.Equal(
            [
                new FakeMlxRequest(model, "sys", "user", "{\"type\":\"object\"}", 2048, 0.2f, 1f, false),
                new FakeMlxRequest(model, "sys", "a user message", null, 1024, 0.7f, 0.8f, true),
            ],
            native.Requests);
        Assert.Equal([MlxStatus.Ok, MlxStatus.Ok], native.Outcomes);
    }

    [Fact]
    public void AGenerationOnAModelNotLoadedIsTheBridgesError()
    {
        FakeNativeMlx native = Initialised(FakeMlxReply.Answer("unused"));
        MlxModelHandle model = Loaded(native);
        native.Unload(model);
        using CancelFlag flag = new();

        Assert.Equal(MlxStatus.Error, native.Generate(model, "s", "u", null, 8, 0f, 1f, false, flag, out MlxGeneration? result, out string? error));
        Assert.Null(result);
        Assert.Equal("napkin_mlx_generate was given a model that is not loaded (unloaded already?).", error);
        Assert.Equal([model], native.Unloads);
    }

    [Fact]
    public void AScriptThatRanOutIsAnError()
    {
        FakeNativeMlx native = Initialised();
        MlxModelHandle model = Loaded(native);
        using CancelFlag flag = new();

        Assert.Equal(MlxStatus.Error, native.Generate(model, "s", "u", null, 8, 0f, 1f, false, flag, out _, out string? error));
        Assert.Equal(FakeNativeMlx.ScriptRanOut, error);
    }

    [Fact]
    public async Task OneGenerationAtATimePerModelTheSecondIsBusy()
    {
        FakeNativeMlx native = Initialised(FakeMlxReply.UntilCancelled(), FakeMlxReply.Answer("second"));
        MlxModelHandle model = Loaded(native);
        using CancelFlag first = new();
        using CancelFlag second = new();

        Task<MlxStatus> running = Task.Run(() => native.Generate(model, "s", "u", null, 8, 0f, 1f, false, first, out _, out _));
        await Eventually.True(() => native.Requests.Count == 1, "the first generation to start");

        Assert.Equal(MlxStatus.Busy, native.Generate(model, "s", "u", null, 8, 0f, 1f, false, second, out _, out string? busy));
        Assert.Equal("This model is already answering; it answers one request at a time.", busy);

        first.Set();
        Assert.Equal(MlxStatus.Cancelled, await running);
        Assert.Equal(MlxStatus.Ok, native.Generate(model, "s", "u", null, 8, 0f, 1f, false, second, out MlxGeneration? after, out _));
        Assert.Equal("second", after!.Text);
    }

    [Fact]
    public void AFlagSetBeforeTheFirstTokenCancelsAtOnce()
    {
        FakeNativeMlx native = Initialised(FakeMlxReply.Answer("never"));
        MlxModelHandle model = Loaded(native);
        using CancelFlag flag = new();
        flag.Set();

        Assert.Equal(MlxStatus.Cancelled, native.Generate(model, "s", "u", null, 8, 0f, 1f, false, flag, out _, out string? error));
        Assert.Equal("Cancelled; nothing was produced.", error);
    }

    [Fact]
    public void AScriptedStatusCarriesItsOwnSentenceOrTheBridges()
    {
        FakeNativeMlx native = Initialised(FakeMlxReply.Refuse(MlxStatus.Incomplete), FakeMlxReply.Refuse(MlxStatus.Error, "The model could not answer: x."));
        MlxModelHandle model = Loaded(native);
        using CancelFlag flag = new();

        Assert.Equal(MlxStatus.Incomplete, native.Generate(model, "s", "u", "{}", 8, 0f, 1f, false, flag, out _, out string? incomplete));
        Assert.Equal(MlxStatus.Error, native.Generate(model, "s", "u", null, 8, 0f, 1f, false, flag, out _, out string? error));

        Assert.Equal("The reply ran out of tokens before it matched the schema, so there is no proposal.", incomplete);
        Assert.Equal("The model could not answer: x.", error);
    }

    [Fact]
    public void TheDeviceIsAppleSiliconUnlessScriptedOtherwise()
    {
        FakeNativeMlx native = new();

        Assert.Equal(MlxStatus.Ok, native.Device(out MlxDeviceInfo info, out string? none));
        Assert.Equal(FakeNativeMlx.AppleSilicon, info);
        Assert.Null(none);

        native.DeviceStatus = MlxStatus.NoMetal;
        Assert.Equal(MlxStatus.NoMetal, native.Device(out MlxDeviceInfo nothing, out string? error));
        Assert.Equal(default, nothing);
        Assert.Equal(FakeNativeMlx.SentenceFor(MlxStatus.NoMetal), error);
        Assert.Equal(2, native.DeviceCalls);
    }

    [Fact]
    public void ABridgeThatWillNotLoadThrowsFromEveryCall()
    {
        DllNotFoundException missing = new("Unable to load shared library 'NapkinMlx'.");
        FakeNativeMlx native = new() { Throws = missing };
        using CancelFlag flag = new();

        Assert.Same(missing, Assert.Throws<DllNotFoundException>(() => native.AbiVersion()));
        Assert.Throws<DllNotFoundException>(() => native.Init("/m", out _));
        Assert.Throws<DllNotFoundException>(() => native.Device(out _, out _));
        Assert.Throws<DllNotFoundException>(() => native.Load("/m", flag, out _, out _));
        Assert.Throws<DllNotFoundException>(() => native.Generate(new MlxModelHandle(1), "s", "u", null, 8, 0f, 1f, false, flag, out _, out _));
        Assert.Throws<DllNotFoundException>(() => native.Unload(new MlxModelHandle(1)));
    }

    [Theory]
    [InlineData(MlxStatus.Error, "The MLX bridge failed.")]
    [InlineData(MlxStatus.Cancelled, "Cancelled; nothing was produced.")]
    [InlineData(MlxStatus.Busy, "This model is already answering; it answers one request at a time.")]
    [InlineData(MlxStatus.NoMetal, "MLX found no Metal device on this Mac (a headless or virtualized session, or not Apple silicon).")]
    [InlineData(MlxStatus.Incomplete, "The reply ran out of tokens before it matched the schema, so there is no proposal.")]
    [InlineData((MlxStatus)42, "Unknown status 42.")]
    public void EachStatusHasTheBridgesSentence(MlxStatus status, string sentence) =>
        Assert.Equal(sentence, FakeNativeMlx.SentenceFor(status));

    [Fact]
    public void TheSentencesAreTheBridgesOwnWords()
    {
        string swift = Repository.Text("native/NapkinMlx/Sources/NapkinMlx/Errors.swift") + Repository.Text("native/NapkinMlx/Sources/NapkinMlx/Bridge.swift");

        foreach (MlxStatus status in new[] { MlxStatus.Error, MlxStatus.Cancelled, MlxStatus.Busy, MlxStatus.NoMetal, MlxStatus.Incomplete })
        {
            Assert.Contains($"\"{FakeNativeMlx.SentenceFor(status)}\"", swift, StringComparison.Ordinal);
        }

        Assert.Contains($"\"{FakeNativeMlx.NotLoadedSentence}\"", swift, StringComparison.Ordinal);
        Assert.Contains($"\"{FakeNativeMlx.NotInitialisedSentence}\"", swift, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelFoldersRefusalsAreTheBridgesChecksInItsOrder()
    {
        string bridge = Repository.Text("native/NapkinMlx/Sources/NapkinMlx/Bridge.swift");
        string validate = bridge[bridge.IndexOf("static func validate", StringComparison.Ordinal)..];
        validate = validate[..validate.IndexOf("return nil", StringComparison.Ordinal)];
        string[] sentences = [.. Regex.Matches(validate, @"\.failed\(\s*""([^""]+)""").Select(match => Regex.Replace(match.Groups[1].Value, @"\\\([^)]*\)", "{}"))];
        string napkin = Repository.Text("src/Napkin.Assistant.Mlx/ModelFolder.cs");
        string[] ours = [.. Regex.Matches(napkin, @"\$?""([^""\n]*)""").Select(match => Regex.Replace(match.Groups[1].Value, @"\{[^}]*\}", "{}"))];

        Assert.Equal(6, sentences.Length);
        Assert.Equal(sentences, ours.Where(sentence => sentences.Contains(sentence)).Distinct());
    }

    private static FakeNativeMlx Initialised(params FakeMlxReply[] script)
    {
        FakeNativeMlx native = new(script);
        Assert.Equal(MlxStatus.Ok, native.Init("/app/native/mlx.metallib", out _));
        return native;
    }

    private static MlxModelHandle Loaded(FakeNativeMlx native)
    {
        using CancelFlag flag = new();
        Assert.Equal(MlxStatus.Ok, native.Load("/m", flag, out MlxModelHandle handle, out _));
        return handle;
    }
}

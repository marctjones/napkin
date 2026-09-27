using System.Runtime.InteropServices;
using System.Text.Json;

using Napkin.Assistant.Mlx;
using Napkin.Tools.Commands;

namespace Napkin.Tools.Tests;

/// <summary>
/// <c>assistant mlx-smoke</c> through <see cref="FakeNativeMlx"/>: what it says at each step, where it
/// stops, and its exit codes. The real bridge is loaded only when a person runs it on a Mac.
/// </summary>
public class AssistantSmokeTests
{
    private const string App = "/app";

    [Fact]
    public void WithoutTheBridgeItSaysWhichFileIsMissingAndHowToBuildIt()
    {
        var run = Smoke(Host(new FakeNativeMlx(), fileExists: _ => false));

        Assert.Equal(ExitCode.InputError, run.Code);
        Assert.Contains($"mlx-smoke: MLX is not in this build of napkin: {Path.Combine(App, "native", "libNapkinMlx.dylib")} is missing.", run.Error, StringComparison.Ordinal);
        Assert.Contains("tools/scripts/build-mlx.sh", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void OffAppleSiliconItSaysSoAndLoadsNothing()
    {
        FakeNativeMlx native = new();

        var run = Smoke(Host(native, isMacOS: false));

        Assert.Equal(ExitCode.InputError, run.Code);
        Assert.Contains(MlxAvailability.NotAppleSilicon, run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("build-mlx.sh", run.Error, StringComparison.Ordinal);
        Assert.Equal(0, native.AbiCalls);
    }

    [Fact]
    public void WithoutAModelItReachesInitAndSaysWhatALoadWouldNeed()
    {
        FakeNativeMlx native = new();

        var run = Smoke(Host(native));

        Assert.Equal(ExitCode.Ok, run.Code);
        Assert.Contains("abi       1", run.Output, StringComparison.Ordinal);
        Assert.Contains("device    Metal, applegpu_fake, 24.0 GiB memory, Metal recommends up to 17.0 GiB; mlx-swift-lm fake", run.Output, StringComparison.Ordinal);
        Assert.Contains($"init      OK in ", run.Output, StringComparison.Ordinal);
        Assert.Contains("model     none given (--model <folder>)", run.Output, StringComparison.Ordinal);
        Assert.Contains("nothing was downloaded", run.Output, StringComparison.Ordinal);
        Assert.Equal([Path.Combine(App, "native", "mlx.metallib")], native.Inits);
        Assert.Empty(native.Loads);
        Assert.Empty(run.Error);
    }

    [Fact]
    public void NoMetalDeviceStopsItWithTheDeviceStillShown()
    {
        FakeNativeMlx native = new() { DeviceInfo = FakeNativeMlx.AppleSilicon with { HasMetal = false } };

        var run = Smoke(Host(native));

        Assert.Equal(ExitCode.GateFailed, run.Code);
        Assert.Contains("device    no Metal, applegpu_fake", run.Output, StringComparison.Ordinal);
        Assert.Contains("mlx-smoke: MLX found no Metal device on this Mac.", run.Error, StringComparison.Ordinal);
        Assert.Empty(native.Inits);
    }

    [Fact]
    public void ABridgeThatWillNotLoadStopsItInTheLoadersWords()
    {
        var run = Smoke(Host(new FakeNativeMlx { Throws = new DllNotFoundException("dlopen(/app/native/libNapkinMlx.dylib): no such file") }));

        Assert.Equal(ExitCode.GateFailed, run.Code);
        Assert.Contains("mlx-smoke: napkin could not load the MLX bridge: dlopen(/app/native/libNapkinMlx.dylib): no such file", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInitTheBridgeRefusesStopsItInTheBridgesWords()
    {
        var run = Smoke(Host(new FakeNativeMlx { InitStatus = MlxStatus.Error, InitError = "There is no Metal library at /app/native/mlx.metallib." }));

        Assert.Equal(ExitCode.GateFailed, run.Code);
        Assert.Contains("mlx-smoke: init Error: There is no Metal library at /app/native/mlx.metallib.", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AModelFolderThatIsNotThereIsRefusedByName()
    {
        using var tree = Fixture.NewDirectory();
        string missing = Path.Combine(tree.Path, "Qwen3-4B-4bit");

        var run = Smoke(Host(new FakeNativeMlx()), "--model", missing);

        Assert.Equal(ExitCode.InputError, run.Code);
        Assert.Contains($"mlx-smoke: There is no folder at {missing}.", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void WithAModelItLoadsAsksForTextAndASketchAndUnloads()
    {
        using var tree = Fixture.NewDirectory();
        string folder = ModelFolderIn(tree);
        FakeNativeMlx native = new(FakeMlxReply.Answer("ok"), FakeMlxReply.Answer("""{"parts":[],"note":"a bench"}"""));
        string root = RepositoryRoot();

        var run = Smoke(Host(native), "--model", folder, "--root", root);

        Assert.Equal(ExitCode.Ok, run.Code);
        Assert.Contains($"model     {folder}: qwen3, 4-bit (group 64), licence not stated in the folder; 7 bytes of weights", run.Output, StringComparison.Ordinal);
        Assert.Contains("load      OK in ", run.Output, StringComparison.Ordinal);
        Assert.Matches(@"text      \d+\.\d\d s: ok", run.Output);
        Assert.Matches(@"sketch    \d+\.\d\d s: \{""parts"":\[\],""note"":""a bench""\}", run.Output);
        Assert.Contains("unload    done", run.Output, StringComparison.Ordinal);
        Assert.Equal([folder], native.Loads);
        Assert.Equal(AssistantSmoke.SketchSchema(root, out _), native.Requests[1].JsonSchema);
        Assert.Single(native.Unloads);
    }

    [Fact]
    public void ARefusedLoadOrAnswerIsPrintedAsItIs()
    {
        using var tree = Fixture.NewDirectory();
        string folder = ModelFolderIn(tree);

        var load = Smoke(Host(new FakeNativeMlx { LoadStatus = MlxStatus.Error, LoadError = "napkin could not load the model: bad weights." }), "--model", folder, "--root", RepositoryRoot());
        var answers = Smoke(Host(new FakeNativeMlx(FakeMlxReply.Refuse(MlxStatus.Error, "The model could not answer: x."), FakeMlxReply.Refuse(MlxStatus.Incomplete))), "--model", folder, "--root", tree.Path);

        Assert.Equal(ExitCode.GateFailed, load.Code);
        Assert.Contains("mlx-smoke: napkin could not load the model: bad weights.", load.Error, StringComparison.Ordinal);
        Assert.Equal(ExitCode.Ok, answers.Code);
        Assert.Matches(@"text      refused after \d+\.\d\d s: The model could not answer: x\.", answers.Output);
        Assert.Contains($"sketch    skipped: {Path.Combine(tree.Path, AssistantSmoke.SchemasFile)} is not there", answers.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSketchSchemaIsTheBridgesExactBytes()
    {
        string? schema = AssistantSmoke.SketchSchema(RepositoryRoot(), out string? missing);

        Assert.Null(missing);
        Assert.NotNull(schema);
        Assert.StartsWith("{\"type\":\"object\",\"properties\":{\"parts\":", schema, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(schema);
        Assert.Equal(["parts", "note"], document.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void ASchemasFileWithoutTheSketchStringSaysSo()
    {
        using var tree = Fixture.NewDirectory();
        tree.Write(AssistantSmoke.SchemasFile, "public enum NapkinSchemas {}\n");

        Assert.Null(AssistantSmoke.SketchSchema(tree.Path, out string? missing));
        Assert.Contains("has no `static let sketch", missing, StringComparison.Ordinal);
    }

    [Fact]
    public void ItIsACommandWithItsOwnHelp()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        Assert.Equal(ExitCode.Ok, Cli.Run(["assistant", "mlx-smoke", "--help"], output, error));
        Assert.Contains("napkin-tools assistant mlx-smoke [--model <folder>]", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(ExitCode.UsageError, Cli.Run(["assistant", "mlx-smoke", "--download"], output, error));
    }

    private static MlxSmokeHost Host(FakeNativeMlx native, bool isMacOS = true, Func<string, bool>? fileExists = null) =>
        new(native, App, isMacOS, Architecture.Arm64, fileExists ?? (_ => true));

    private static (int Code, string Output, string Error) Smoke(MlxSmokeHost host, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = AssistantSmoke.MlxSmoke(args, output, error, host);
        return (code, output.ToString(), error.ToString());
    }

    private static string ModelFolderIn(Fixture.Scratch tree)
    {
        tree.Write("Qwen3-4B-4bit/config.json", """{"model_type": "qwen3", "quantization": {"group_size": 64, "bits": 4}}""");
        tree.Write("Qwen3-4B-4bit/tokenizer.json", "{}");
        tree.Write("Qwen3-4B-4bit/tokenizer_config.json", """{"tokenizer_class": "Qwen2Tokenizer"}""");
        tree.Write("Qwen3-4B-4bit/model.safetensors", "weights");
        return Path.Combine(tree.Path, "Qwen3-4B-4bit");
    }

    private static string RepositoryRoot() =>
        RepoLayout.Discover(AppContext.BaseDirectory)?.Root ?? throw new InvalidOperationException("napkin.sln not found above the test binary.");
}

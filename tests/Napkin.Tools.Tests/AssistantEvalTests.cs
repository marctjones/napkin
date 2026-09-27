using System.Runtime.InteropServices;

using Napkin.Assistant.Mlx;
using Napkin.Tools.Commands;

namespace Napkin.Tools.Tests;

/// <summary>
/// <c>assistant eval</c> (docs/design/llm-assistant.md §11.4): the twenty cases run through
/// <c>ScriptedModel</c> offline (the mode <c>gate.sh</c> would run if it ever did — it does not),
/// and the command line around the two live modes, through a fake so nothing opens a socket or
/// loads the real MLX bridge.
/// </summary>
public class AssistantEvalTests
{
    [Fact]
    public void The_twenty_scripted_cases_all_pass_and_the_command_exits_zero()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int code = Cli.Run(["assistant", "eval", "--scripted", "--root", RepositoryRoot()], output, error);

        Assert.Equal(ExitCode.Ok, code);
        Assert.Empty(error.ToString());
        string text = output.ToString();
        Assert.Contains("assistant eval — 20 case(s), scripted", text, StringComparison.Ordinal);
        Assert.Equal(20, CountLines(text, "PASS  "));
        Assert.Equal(0, CountLines(text, "FAIL  "));
        Assert.Contains("20 case(s): 20 passed, 0 failed (0 model-refused, 2 unparsed).", text, StringComparison.Ordinal);
        Assert.Contains("gates nothing", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_case_id_appears_exactly_once()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        string[] files = Directory.GetFiles(Path.Combine(RepositoryRoot(), "tests", "Napkin.Modules.Assistant.Tests", "Eval"), "*.json");
        Assert.Equal(20, files.Length);

        Cli.Run(["assistant", "eval", "--scripted", "--root", RepositoryRoot()], output, error);
        string text = output.ToString();
        string[] lines = text.Split('\n');
        foreach (string file in files)
        {
            string id = Path.GetFileNameWithoutExtension(file)[3..]; // "01-ask-header-no-data" -> "ask-header-no-data"
            Assert.Equal(1, lines.Count(line => line.StartsWith("PASS  " + id + " ", StringComparison.Ordinal) || line.StartsWith("FAIL  " + id + " ", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void Exactly_one_mode_is_required()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        Assert.Equal(ExitCode.UsageError, Cli.Run(["assistant", "eval", "--root", RepositoryRoot()], output, error));
        Assert.Contains("needs exactly one of", error.ToString(), StringComparison.Ordinal);

        error = new StringWriter();
        Assert.Equal(
            ExitCode.UsageError,
            Cli.Run(["assistant", "eval", "--scripted", "--endpoint", "http://127.0.0.1:11434", "--model", "qwen3:4b", "--root", RepositoryRoot()], output, error));

        error = new StringWriter();
        Assert.Equal(ExitCode.UsageError, Cli.Run(["assistant", "eval", "--endpoint", "http://127.0.0.1:11434", "--root", RepositoryRoot()], output, error));
        Assert.Contains("--endpoint and --model are given together", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_bad_endpoint_is_refused_by_name()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int code = Cli.Run(["assistant", "eval", "--endpoint", "https://example.com", "--model", "qwen3:4b", "--root", RepositoryRoot()], output, error);

        Assert.Equal(ExitCode.InputError, code);
        Assert.Contains("is not this machine", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void With_no_eval_directory_it_is_an_input_error()
    {
        using var tree = Fixture.NewDirectory();
        File.WriteAllText(Path.Combine(tree.Path, "napkin.sln"), string.Empty);
        var output = new StringWriter();
        var error = new StringWriter();

        int code = Cli.Run(["assistant", "eval", "--scripted", "--root", tree.Path], output, error);

        Assert.Equal(ExitCode.InputError, code);
        Assert.Contains("no eval directory at", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_malformed_case_file_is_reported_by_name_and_stops_the_run()
    {
        using var tree = Fixture.NewDirectory();
        File.WriteAllText(Path.Combine(tree.Path, "napkin.sln"), string.Empty);
        tree.Write(Path.Combine("tests", "Napkin.Modules.Assistant.Tests", "Eval", "bad.json"), """{"sample":"x"}""");
        var output = new StringWriter();
        var error = new StringWriter();

        int code = Cli.Run(["assistant", "eval", "--scripted", "--root", tree.Path], output, error);

        Assert.Equal(ExitCode.InputError, code);
        Assert.Contains("bad.json", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("no \"id\"", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Without_the_bridge_mlx_is_refused_the_same_way_mlx_smoke_is()
    {
        using var tree = Fixture.NewDirectory();
        string missing = Path.Combine(tree.Path, "nowhere");
        var output = new StringWriter();
        var error = new StringWriter();
        MlxSmokeHost host = new(new FakeNativeMlx(), "/app", true, Architecture.Arm64, _ => false);

        int code = AssistantEval.Eval(["--mlx", missing, "--root", RepositoryRoot()], output, error, host);

        Assert.Equal(ExitCode.InputError, code);
        Assert.Contains($"There is no folder at {missing}.", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Off_apple_silicon_mlx_is_refused_once_the_folder_parses()
    {
        using var tree = Fixture.NewDirectory();
        string folder = ModelFolderIn(tree);
        var output = new StringWriter();
        var error = new StringWriter();
        MlxSmokeHost host = new(new FakeNativeMlx(), "/app", false, Architecture.X64, _ => true);

        int code = AssistantEval.Eval(["--mlx", folder, "--root", RepositoryRoot()], output, error, host);

        Assert.Equal(ExitCode.InputError, code);
        Assert.Contains(MlxAvailability.NotAppleSilicon, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void On_the_fake_bridge_mlx_loads_the_model_and_runs_every_case()
    {
        using var tree = Fixture.NewDirectory();
        string folder = ModelFolderIn(tree);
        FakeNativeMlx native = new([.. Enumerable.Range(0, 20).Select(_ => FakeMlxReply.Answer("napkin did not give me that."))]);
        MlxSmokeHost host = new(native, "/app", true, Architecture.Arm64, _ => true);
        var output = new StringWriter();
        var error = new StringWriter();

        int code = AssistantEval.Eval(["--mlx", folder, "--root", RepositoryRoot()], output, error, host);

        Assert.Equal(ExitCode.Ok, code);
        Assert.Contains("assistant eval — 20 case(s), --mlx", output.ToString(), StringComparison.Ordinal);
        Assert.Contains(folder, output.ToString(), StringComparison.Ordinal);
        Assert.Single(native.Unloads);
    }

    private static int CountLines(string text, string marker)
        => text.Split('\n').Count(line => line.Contains(marker, StringComparison.Ordinal));

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

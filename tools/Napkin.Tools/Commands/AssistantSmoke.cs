using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

using Napkin.Assistant.LocalServer;
using Napkin.Assistant.Mlx;
using Napkin.Modules.Assistant;

namespace Napkin.Tools.Commands;

/// <summary>What <c>assistant mlx-smoke</c> runs against: this process and the real bridge, or a test's fake.</summary>
/// <param name="Native">The bridge.</param>
/// <param name="BaseDirectory">The folder whose <c>native/</c> holds the bridge's three files.</param>
/// <param name="IsMacOS">Whether this is macOS.</param>
/// <param name="ProcessArchitecture">This process's architecture.</param>
/// <param name="FileExists">Whether a file exists.</param>
public sealed record MlxSmokeHost(INativeMlx Native, string BaseDirectory, bool IsMacOS, Architecture ProcessArchitecture, Func<string, bool> FileExists)
{
    /// <summary>This process: <see cref="NativeMlx"/> (which loads nothing until asked), beside this tool's executable.</summary>
    public static MlxSmokeHost ThisProcess() =>
        new(new NativeMlx(), MlxBridge.BaseDirectory, OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture, File.Exists);
}

/// <summary>
/// <c>assistant mlx-smoke</c> (docs/design/mlx-runtime.md §7.4): napkin's own MLX runtime on the real
/// bridge — the one place outside the app that loads <c>libNapkinMlx.dylib</c>, run by a person on an
/// Apple silicon Mac, never by the gate or CI. It says what it finds step by step: the bridge's
/// files, its interface version and this Mac's Metal device, <c>init</c>, and — given a model folder
/// — the load with its time and this process's memory, a text answer ("Reply with ok."), a proposal
/// against the sketch schema the bridge pre-compiles, and the unload.
/// </summary>
/// <remarks>
/// It never downloads anything. Exit codes: 0 when it ran to the end, whatever the model answered
/// (or when no model folder was given: the model-free part ran); 1 when the bridge was found but
/// could not run here (it would not load, speaks another interface, found no Metal device, or
/// refused <c>init</c> or the load); 3 when there is no bridge beside this tool or the model folder
/// is refused.
/// </remarks>
public static class AssistantSmoke
{
    /// <summary>Where the sketch schema is, as the bridge compiles it at every load (mlx-runtime.md §13.2 item 4).</summary>
    public const string SchemasFile = "native/NapkinMlx/Sources/NapkinMlxSchemas/Schemas.swift";

    /// <summary>The sketch proposal the smoke asks for.</summary>
    public const string SketchDescription = "a plank bench, 4 feet long and 16 inches tall: a top on two legs with a stretcher between.";

    private static readonly HashSet<string> Valued = ["--root", "--model"];
    private static readonly HashSet<string> Flags = [];

    /// <summary><c>assistant mlx-smoke [--model &lt;folder&gt;] [--root &lt;path&gt;]</c> in this process.</summary>
    public static int MlxSmoke(IReadOnlyList<string> args, TextWriter output, TextWriter error) =>
        MlxSmoke(args, output, error, MlxSmokeHost.ThisProcess());

    /// <summary>The smoke against <paramref name="host"/>.</summary>
    public static int MlxSmoke(IReadOnlyList<string> args, TextWriter output, TextWriter error, MlxSmokeHost host)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(host);
        CommandLine parsed = CommandLine.Parse(args, Valued, Flags);
        if (parsed.WantsHelp)
        {
            output.WriteLine(Cli.Usage);
            return ExitCode.Ok;
        }

        string native = MlxBridge.NativeDirectory(host.BaseDirectory);
        output.WriteLine("assistant mlx-smoke — napkin's MLX runtime on the real bridge, in this process");
        output.WriteLine($"bridge    {native}");
        if (MlxAvailability.Check(host.IsMacOS, host.ProcessArchitecture, host.FileExists, host.BaseDirectory) is { } unavailable)
        {
            error.WriteLine($"mlx-smoke: {unavailable}");
            if (unavailable != MlxAvailability.NotAppleSilicon)
            {
                error.WriteLine("mlx-smoke: build the bridge with tools/scripts/build-mlx.sh, then run this again (dotnet builds copy native/NapkinMlx/out/ beside this tool).");
            }

            return ExitCode.InputError;
        }

        MlxProbe probe = MlxAvailability.Probe(host.Native);
        if (probe.Device is { } device)
        {
            output.WriteLine($"abi       {MlxBridge.AbiVersion}");
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"device    {(device.HasMetal ? "Metal" : "no Metal")}, {device.Architecture}, {Gib(device.MemoryBytes)} GiB memory, Metal recommends up to {Gib(device.RecommendedWorkingSetBytes)} GiB; mlx-swift-lm {device.MlxSwiftLmRevision}"));
        }

        if (probe.Refusal is { } refusal)
        {
            error.WriteLine($"mlx-smoke: {refusal}");
            return ExitCode.GateFailed;
        }

        string metallib = MlxBridge.MetallibPath(host.BaseDirectory);
        long started = Stopwatch.GetTimestamp();
        MlxStatus init = host.Native.Init(metallib, out string? initError);
        if (init != MlxStatus.Ok)
        {
            error.WriteLine($"mlx-smoke: init {init}: {initError}");
            return ExitCode.GateFailed;
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"init      OK in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:0} ms ({metallib})"));

        if (parsed.Value("--model") is not { } path)
        {
            output.WriteLine("model     none given (--model <folder>): the load, a text answer and a sketch proposal need an mlx-community model folder");
            output.WriteLine("          holding config.json, tokenizer.json, tokenizer_config.json and *.safetensors. Nothing was loaded and nothing was downloaded.");
            return ExitCode.Ok;
        }

        if (!ModelFolder.TryParse(path, out ModelFolder? folder, out string? folderRefusal))
        {
            error.WriteLine($"mlx-smoke: {folderRefusal}");
            return ExitCode.InputError;
        }

        string? schema = SketchSchema(Cli.ResolveRoot(parsed).Root, out string? schemaMissing);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"model     {folder.Path}: {folder.Description}; {folder.WeightBytes:N0} bytes of weights"));
        using MlxModel model = new(host.Native, folder, metallibPath: metallib);

        started = Stopwatch.GetTimestamp();
        if (model.LoadAsync(CancellationToken.None).GetAwaiter().GetResult() is { } loadRefusal)
        {
            error.WriteLine($"mlx-smoke: {loadRefusal}");
            return ExitCode.GateFailed;
        }

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"load      OK in {Stopwatch.GetElapsedTime(started).TotalSeconds:0.00} s; this process's working set is now {Environment.WorkingSet / 1e9:0.00} GB"));

        Ask(output, "text     ", model, LocalServerModel.TestRequest);
        if (schema is null)
        {
            output.WriteLine($"sketch    skipped: {schemaMissing}");
        }
        else
        {
            Ask(output, "sketch   ", model, new ModelRequest(AssistantPrompts.Sketch, string.Empty, SketchDescription, schema));
        }

        model.Dispose();
        output.WriteLine("unload    done");
        output.WriteLine("For MLX's own load, memory and tokens-per-second lines, run with NAPKIN_MLX_DIAGNOSTICS=1 in the environment.");
        return ExitCode.Ok;
    }

    /// <summary>
    /// The sketch schema's exact text from the bridge's <c>Schemas.swift</c> (a Swift raw multi-line
    /// string: the lines between <c>static let sketch = #"""</c> and the closing <c>"""#</c>, less the
    /// closing delimiter's indentation) — the bytes the bridge pre-compiles at load, so the smoke's
    /// proposal uses the load-time constraint as napkin's will.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="missing">Why there is none, when there is none.</param>
    public static string? SketchSchema(string root, out string? missing)
    {
        string file = Path.Combine(root, SchemasFile);
        if (!File.Exists(file))
        {
            missing = $"{file} is not there; run from the repository or pass --root.";
            return null;
        }

        string[] lines = File.ReadAllLines(file);
        int opening = Array.FindIndex(lines, line => line.TrimEnd().EndsWith("static let sketch = #\"\"\"", StringComparison.Ordinal));
        int closing = opening < 0 ? -1 : Array.FindIndex(lines, opening + 1, line => line.Trim() == "\"\"\"#");
        if (closing < 0)
        {
            missing = $"{file} has no `static let sketch = #\"\"\"` … `\"\"\"#` string.";
            return null;
        }

        int indent = lines[closing].Length - lines[closing].TrimStart().Length;
        missing = null;
        return string.Join("\n", lines[(opening + 1)..closing].Select(line => line.Length >= indent ? line[indent..] : line.TrimStart()));
    }

    private static void Ask(TextWriter output, string label, MlxModel model, ModelRequest request)
    {
        long started = Stopwatch.GetTimestamp();
        ModelReply reply = model.AskAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        string seconds = string.Create(CultureInfo.InvariantCulture, $"{Stopwatch.GetElapsedTime(started).TotalSeconds:0.00} s");
        output.WriteLine(reply switch
        {
            ModelReply.Text text => $"{label} {seconds}: {text.Answer}",
            ModelReply.Json json => $"{label} {seconds}: {json.Document}",
            ModelReply.Refused refused => $"{label} refused after {seconds}: {refused.Reason}",
            _ => $"{label} {reply}",
        });
    }

    private static string Gib(ulong bytes) => (bytes / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture);
}

using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using Napkin.Assistant.LocalServer;
using Napkin.Assistant.Mlx;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Modules.Assistant;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

namespace Napkin.Tools.Commands;

/// <summary>What the reply asks of a case, closed to the two the eval set covers so far (docs/design/llm-assistant.md §11.4, §17.5).</summary>
public enum EvalTask
{
    /// <summary>Ask or Explain this result: a text answer, guarded.</summary>
    Answer,

    /// <summary>Sketch from words: a JSON proposal, parsed strictly.</summary>
    Sketch,
}

/// <summary>
/// One case of the offline eval set, read from <c>tests/Napkin.Modules.Assistant.Tests/Eval/*.json</c>.
/// </summary>
/// <param name="Id">The case's name, unique in the set.</param>
/// <param name="FileName">The file it came from, for a message.</param>
/// <param name="Sample">The sample it builds a context pack from (<c>samples/*.scene.json</c>).</param>
/// <param name="Select">Entities to select, by the name the pack shows them under (<see cref="DesignWords.NameOf"/>).</param>
/// <param name="Lists">Open lists to attach: <c>cutList</c>, <c>shoppingList</c>.</param>
/// <param name="Question">The question asked.</param>
/// <param name="Task">Whether the reply is a text answer or a sketch proposal.</param>
/// <param name="Reply">
/// The reply a scripted run gives (docs/design/llm-assistant.md §11.4's <c>--scripted</c>): the
/// answer's text, or the proposal's JSON, written by hand against real product text. A run against a
/// live model never uses this field — the model's own reply is checked instead.
/// </param>
/// <param name="ExpectRefused">
/// Answer task: whether the answer guard must refuse at least one sentence (an adversarial case that
/// tries to make the model state an unsupported number) rather than none at all.
/// </param>
/// <param name="ExpectReference">Answer task: whether a kept sentence must refer to at least one pack item.</param>
/// <param name="Forbidden">Tokens that must never appear in a <em>kept</em> sentence — numbers a good answer would not state.</param>
/// <param name="ExpectParses">Sketch task: whether the reply must parse as a <see cref="SketchProposal"/> (false for a malformed-reply case).</param>
/// <param name="ExpectedParts">Sketch task: the exact number of accepted (non-refused) plan lines a parsed proposal must produce.</param>
/// <param name="Description">A one-line note on what the case is testing, for a person reading the case file; optional, never printed.</param>
public sealed record EvalCase(
    string Id,
    string FileName,
    string Sample,
    ImmutableArray<string> Select,
    ImmutableArray<string> Lists,
    string Question,
    EvalTask Task,
    string Reply,
    bool ExpectRefused,
    bool ExpectReference,
    ImmutableArray<string> Forbidden,
    bool ExpectParses,
    int? ExpectedParts,
    string? Description);

/// <summary>What running one case found.</summary>
/// <param name="Case">The case.</param>
/// <param name="Passed">Whether it met its checks.</param>
/// <param name="ModelRefused">Whether the runtime itself declined to answer (<see cref="ModelReply.Refused"/>).</param>
/// <param name="GuardRefused">Answer task: how many sentences the answer guard refused.</param>
/// <param name="Parsed">Sketch task: whether the reply parsed as a proposal; null for an answer task.</param>
/// <param name="Detail">One line describing the outcome, printed in the table.</param>
/// <param name="Elapsed">How long the model took to answer.</param>
public sealed record EvalResult(EvalCase Case, bool Passed, bool ModelRefused, int GuardRefused, bool? Parsed, string Detail, TimeSpan Elapsed);

/// <summary>
/// <c>assistant eval</c> (docs/design/llm-assistant.md §11.4): the twenty offline cases against
/// whichever runtime is asked for — <c>--scripted</c> (the fake model, its own expected replies, and
/// so the only mode the unit tests and <c>gate.sh</c> ever run), <c>--endpoint</c>/<c>--model</c> (a
/// real Ollama or llama-server), or <c>--mlx &lt;folder&gt;</c> (the real MLX bridge, mlx-runtime.md
/// §17.5). It measures; it never gates: whatever the scores, the command exits 0 once it has run the
/// cases, the same stance as <c>scorecard report</c> (docs/testing/scorecard.md). Only the command
/// line itself (an unknown option, no mode chosen, an unreadable case, a refused model folder) is a
/// real tool failure.
/// </summary>
public static class AssistantEval
{
    private static readonly HashSet<string> Valued = ["--root", "--endpoint", "--model", "--mlx"];
    private static readonly HashSet<string> Flags = ["--scripted"];

    private static readonly JsonSerializerOptions CaseJson = new() { PropertyNameCaseInsensitive = true };

    private static readonly Regex RefPlaceholder = new(@"\[\[ref:(.*?)\]\]", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary><c>assistant eval [--scripted | --endpoint &lt;url&gt; --model &lt;name&gt; | --mlx &lt;folder&gt;] [--root &lt;path&gt;]</c> in this process.</summary>
    public static int Eval(IReadOnlyList<string> args, TextWriter output, TextWriter error) =>
        Eval(args, output, error, MlxSmokeHost.ThisProcess());

    /// <summary>The eval against <paramref name="mlxHost"/> (a test's fake, or <see cref="MlxSmokeHost.ThisProcess"/>).</summary>
    public static int Eval(IReadOnlyList<string> args, TextWriter output, TextWriter error, MlxSmokeHost mlxHost)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(mlxHost);

        CommandLine parsed = CommandLine.Parse(args, Valued, Flags);
        if (parsed.WantsHelp)
        {
            output.WriteLine(Cli.Usage);
            return ExitCode.Ok;
        }

        bool scripted = parsed.Has("--scripted");
        string? endpoint = parsed.Value("--endpoint");
        string? modelName = parsed.Value("--model");
        string? mlxFolder = parsed.Value("--mlx");
        int modesGiven = (scripted ? 1 : 0) + (endpoint is not null || modelName is not null ? 1 : 0) + (mlxFolder is not null ? 1 : 0);
        if (modesGiven != 1)
        {
            error.WriteLine("napkin-tools: assistant eval needs exactly one of --scripted, --endpoint/--model, or --mlx <folder>.");
            return ExitCode.UsageError;
        }

        if ((endpoint is null) != (modelName is null))
        {
            error.WriteLine("napkin-tools: --endpoint and --model are given together.");
            return ExitCode.UsageError;
        }

        RepoLayout layout = Cli.ResolveRoot(parsed);
        if (!Directory.Exists(layout.EvalDirectory))
        {
            error.WriteLine($"napkin-tools: no eval directory at {layout.Relative(layout.EvalDirectory)}.");
            return ExitCode.InputError;
        }

        string[] files = [.. Directory.GetFiles(layout.EvalDirectory, "*.json").OrderBy(f => f, StringComparer.Ordinal)];
        if (files.Length == 0)
        {
            error.WriteLine($"napkin-tools: no *.json cases in {layout.Relative(layout.EvalDirectory)}.");
            return ExitCode.InputError;
        }

        List<EvalCase> cases = [];
        foreach (string file in files)
        {
            try
            {
                cases.Add(LoadCase(file));
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                error.WriteLine($"napkin-tools: {layout.Relative(file)}: {exception.Message}");
                return ExitCode.InputError;
            }
        }

        CodePacks packs = CodePacks.Discover([layout.PacksDirectory]);

        IAssistantModel? shared = null;
        string whereabouts;
        string modelLine;
        try
        {
            if (scripted)
            {
                whereabouts = ScriptedModel.ScriptedWhereabouts;
                modelLine = "scripted replies (the eval's own expected answers)";
            }
            else if (mlxFolder is not null)
            {
                (shared, string? line, int? failureCode, string? message) = LoadMlx(mlxHost, mlxFolder, layout);
                if (failureCode is { } code)
                {
                    error.WriteLine($"napkin-tools: {message}");
                    return code;
                }

                whereabouts = shared!.Whereabouts;
                modelLine = line!;
            }
            else
            {
                LocalEndpoint address;
                try
                {
                    address = LocalEndpoint.Parse(endpoint);
                }
                catch (ArgumentException exception)
                {
                    error.WriteLine($"napkin-tools: {exception.Message}");
                    return ExitCode.InputError;
                }

                shared = new LocalServerModel(address, modelName!);
                whereabouts = shared.Whereabouts;
                modelLine = DescribeEndpointModel(address, modelName!);
            }

            output.WriteLine($"assistant eval — {cases.Count} case(s), {(scripted ? "scripted" : mlxFolder is not null ? "--mlx" : "--endpoint")}");
            output.WriteLine($"model     {modelLine}");
            output.WriteLine();

            List<EvalResult> results = [];
            foreach (EvalCase eval in cases)
            {
                EvalResult result = RunCase(layout, packs, scripted, shared, eval);
                results.Add(result);
                output.WriteLine($"{(result.Passed ? "PASS" : "FAIL")}  {eval.Id,-40} {result.Detail}");
            }

            output.WriteLine();
            int passed = results.Count(r => r.Passed);
            int refused = results.Count(r => r.ModelRefused);
            int unparsed = results.Count(r => r.Case.Task == EvalTask.Sketch && r.Parsed == false);
            output.WriteLine(
                $"{results.Count} case(s): {passed} passed, {results.Count - passed} failed "
                + $"({refused} model-refused, {unparsed} unparsed). Whereabouts: {whereabouts}");
            output.WriteLine("This measures answer quality; it gates nothing (docs/testing/scorecard.md's stance) and is never run by gate.sh or CI.");
            return ExitCode.Ok;
        }
        finally
        {
            (shared as IDisposable)?.Dispose();
        }
    }

    private static string DescribeEndpointModel(LocalEndpoint endpoint, string model)
    {
        try
        {
            using LocalProgram program = new(endpoint);
            ServerListing listing = program.ListAsync(CancellationToken.None).GetAwaiter().GetResult();
            return listing.Find(model)?.Describe() ?? $"{model} — not listed by {endpoint} ({listing.Refusal ?? "no matching name"})";
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return $"{model} at {endpoint} — could not be listed: {exception.Message}";
        }
    }

    /// <summary>
    /// The <c>--mlx &lt;folder&gt;</c> interface #243 left for this slice (mlx-runtime.md §17.5):
    /// the folder checked with <see cref="ModelFolder.TryParse"/> (exit 3 refused), then the bridge
    /// beside this tool exactly as <see cref="AssistantSmoke"/> loads it — availability, the device
    /// probe, <c>init</c>, the model loaded — before any case is asked.
    /// </summary>
    private static (MlxModel? Model, string? ModelLine, int? FailureCode, string? Message) LoadMlx(MlxSmokeHost host, string folder, RepoLayout layout)
    {
        if (!ModelFolder.TryParse(folder, out ModelFolder? parsed, out string? folderRefusal))
        {
            return (null, null, ExitCode.InputError, folderRefusal);
        }

        if (MlxAvailability.Check(host.IsMacOS, host.ProcessArchitecture, host.FileExists, host.BaseDirectory) is { } unavailable)
        {
            string hint = unavailable == MlxAvailability.NotAppleSilicon
                ? unavailable
                : $"{unavailable} Build the bridge with tools/scripts/build-mlx.sh, then run this again.";
            return (null, null, ExitCode.InputError, hint);
        }

        MlxProbe probe = MlxAvailability.Probe(host.Native);
        if (probe.Refusal is { } refusal)
        {
            return (null, null, ExitCode.GateFailed, refusal);
        }

        string metallib = MlxBridge.MetallibPath(host.BaseDirectory);
        if (host.Native.Init(metallib, out string? initError) is var status && status != MlxStatus.Ok)
        {
            return (null, null, ExitCode.GateFailed, $"init {status}: {initError}");
        }

        MlxModel model = new(host.Native, parsed, metallibPath: metallib);
        if (model.LoadAsync(CancellationToken.None).GetAwaiter().GetResult() is { } loadRefusal)
        {
            model.Dispose();
            return (null, null, ExitCode.GateFailed, loadRefusal);
        }

        return (model, $"{parsed.Path}: {parsed.Description}", null, null);
    }

    private static EvalResult RunCase(RepoLayout layout, CodePacks packs, bool scripted, IAssistantModel? shared, EvalCase eval)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            ContextPack pack = BuildPack(layout, packs, eval);
            string schema = eval.Task == EvalTask.Sketch ? SketchProposal.Schema : string.Empty;
            ModelRequest request = eval.Task == EvalTask.Answer
                ? ModelRequest.ForAnswer(AssistantPrompts.Ask, pack, eval.Question)
                : ModelRequest.ForProposal(AssistantPrompts.Sketch, pack, eval.Question, schema);

            IAssistantModel model = scripted ? new ScriptedModel(BuildScriptedReply(eval, pack)) : shared!;
            ModelReply reply = model.AskAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

            if (reply is ModelReply.Refused refused)
            {
                return new EvalResult(eval, Passed: false, ModelRefused: true, GuardRefused: 0, Parsed: null, $"model refused: {refused.Reason}", elapsed);
            }

            return eval.Task == EvalTask.Answer
                ? CheckAnswer(eval, pack, (ModelReply.Text)EnsureKind<ModelReply.Text>(reply, "a text answer"), elapsed)
                : CheckSketch(eval, (ModelReply.Json)EnsureKind<ModelReply.Json>(reply, "a JSON proposal"), elapsed);
        }
        catch (InvalidOperationException exception)
        {
            return new EvalResult(eval, Passed: false, ModelRefused: false, GuardRefused: 0, Parsed: null, $"case error: {exception.Message}", Stopwatch.GetElapsedTime(started));
        }
    }

    private static object EnsureKind<T>(ModelReply reply, string wanted)
        where T : ModelReply
        => reply as T ?? throw new InvalidOperationException($"the reply was not {wanted}: {reply}");

    private static EvalResult CheckAnswer(EvalCase eval, ContextPack pack, ModelReply.Text text, TimeSpan elapsed)
    {
        GuardedAnswer guarded = AnswerGuard.Check(text.Answer, pack);
        string keptText = string.Join(" ", guarded.Sentences.Where(s => s.Kept).Select(s => s.Text));

        bool refusedOk = eval.ExpectRefused ? guarded.Refused >= 1 : guarded.Refused == 0;
        bool referenceOk = !eval.ExpectReference || guarded.References.Length > 0;
        List<string> leaked = [.. eval.Forbidden.Where(token => StatesNumber(keptText, token))];
        bool pass = refusedOk && referenceOk && leaked.Count == 0;

        List<string> problems = [];
        if (!refusedOk)
        {
            string why = string.Join(
                " / ",
                guarded.Sentences.Where(s => !s.Kept).Select(s => $"\"{s.Text}\" said {string.Join(",", s.Unsupported)} referred to {string.Join(",", s.MissingReferences)}"));
            problems.Add(
                eval.ExpectRefused
                    ? "expected the guard to refuse a sentence but it kept everything"
                    : $"the guard refused {guarded.Refused} sentence(s): {why}");
        }

        if (!referenceOk)
        {
            problems.Add("no kept sentence referred to a pack item");
        }

        if (leaked.Count > 0)
        {
            problems.Add($"kept text still said {string.Join(", ", leaked)}");
        }

        string detail = pass
            ? $"kept, {guarded.Refused} refused, refs [{string.Join(",", guarded.References)}]"
            : string.Join("; ", problems);
        return new EvalResult(eval, pass, ModelRefused: false, guarded.Refused, Parsed: null, detail, elapsed);
    }

    /// <summary>
    /// Whether <paramref name="keptText"/> states <paramref name="forbidden"/> as a number, the same
    /// way <see cref="AnswerGuard"/> reads one — so a forbidden value written a different way still
    /// counts, and a stray digit inside a <c>[12]</c> reference (which <see cref="NumberTokens.In"/>
    /// blanks before it tokenises) never does. A forbidden entry with no number in it (a phrase) is
    /// still checked by a plain substring, so "two 2x4" can be forbidden as written.
    /// </summary>
    private static bool StatesNumber(string keptText, string forbidden)
    {
        ImmutableArray<NumberToken> wanted = NumberTokens.In(forbidden);
        if (wanted.IsEmpty)
        {
            return keptText.Contains(forbidden, StringComparison.Ordinal);
        }

        ImmutableHashSet<string> keys = [.. NumberTokens.In(keptText).Select(token => token.Key)];
        return wanted.Any(token => keys.Contains(token.Key));
    }

    private static EvalResult CheckSketch(EvalCase eval, ModelReply.Json json, TimeSpan elapsed)
    {
        bool parses = SketchProposal.TryParse(json.Document, out SketchProposal? proposal, out string? why);
        if (!eval.ExpectParses)
        {
            bool adversarialPass = !parses;
            return new EvalResult(eval, adversarialPass, false, 0, parses, adversarialPass ? $"refused to parse: {why}" : "parsed, but the case expected it not to", elapsed);
        }

        if (!parses || proposal is null)
        {
            return new EvalResult(eval, false, false, 0, false, $"did not parse: {why}", elapsed);
        }

        int next = 1;
        ProposalPlan plan = proposal.Plan(Sketch.Empty, LayerId.Default, () => $"Part {next++}");
        int accepted = plan.Lines.Count(line => !line.Refused);
        bool partsOk = eval.ExpectedParts is not { } wanted || accepted == wanted;
        string detail = partsOk
            ? $"parsed, {accepted} part(s)"
            : $"parsed, {accepted} part(s), expected {eval.ExpectedParts}";
        return new EvalResult(eval, partsOk, false, 0, true, detail, elapsed);
    }

    private static ScriptedReply BuildScriptedReply(EvalCase eval, ContextPack pack)
    {
        string resolved = ResolveRefs(eval.Reply, pack, eval.FileName);
        return eval.Task == EvalTask.Answer ? ScriptedReply.Text(resolved) : ScriptedReply.Json(resolved);
    }

    /// <summary>
    /// Replaces every <c>[[ref:substring]]</c> in a scripted reply with <c>[n]</c> for the pack item
    /// whose text contains <c>substring</c> — so a case names what it means to cite, and the item
    /// number is always the real one for the pack actually built, never a number typed by hand that
    /// a doc edit or a pack change could quietly leave wrong.
    /// </summary>
    private static string ResolveRefs(string reply, ContextPack pack, string fileName)
    {
        return RefPlaceholder.Replace(reply, match =>
        {
            string needle = match.Groups[1].Value;
            ContextItem item = pack.Items.FirstOrDefault(item => item.Text.Contains(needle, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"{fileName}: no pack item contains \"{needle}\".");
            return string.Create(CultureInfo.InvariantCulture, $"[{item.N}]");
        });
    }

    private static ContextPack BuildPack(RepoLayout layout, CodePacks packs, EvalCase eval)
    {
        string scenePath = Path.Combine(layout.SamplesDirectory, $"{eval.Sample}.scene.json");
        if (!File.Exists(scenePath))
        {
            throw new InvalidOperationException($"{eval.FileName}: no sample at {layout.Relative(scenePath)}.");
        }

        LoadResult result = SceneReader.ReadFile(scenePath);
        if (result is not Loaded loaded)
        {
            throw new InvalidOperationException($"{eval.FileName}: sample \"{eval.Sample}\" was refused: {((Refused)result).Summary}");
        }

        Design design = Design.Named(eval.Sample, loaded.Sketch);

        List<EntityId> selection = [];
        foreach (string name in eval.Select)
        {
            Entity found = design.Sketch.Entities.Values.FirstOrDefault(entity => DesignWords.NameOf(design, entity.Id) == name)
                ?? throw new InvalidOperationException($"{eval.FileName}: \"{name}\" names no entity in {eval.Sample}.");
            selection.Add(found.Id);
        }

        ImmutableArray<OpeningCheck> headers = CodeCheck.Of(design.Sketch, packs);
        ImmutableArray<WallBracingCheck> bracing = BracingCheck.Of(design.Sketch, packs);
        ImmutableArray<DeckChecks> decks = DeckCheck.Of(design.Sketch, packs);
        ContextChecks checks = new(packs.Resolve(design.Sketch.Code), headers, bracing, decks);

        List<OpenList> lists = [];
        if (eval.Lists.Length > 0)
        {
            ImmutableArray<CutListRow> rows = CutList.Of(design.Sketch, MaterialsLibrary.Shipped);
            foreach (string list in eval.Lists)
            {
                lists.Add(list switch
                {
                    "cutList" => OpenList.CutList(rows),
                    "shoppingList" => OpenList.ShoppingList(ShoppingList.Of(rows)),
                    _ => throw new InvalidOperationException($"{eval.FileName}: unknown list \"{list}\" (cutList or shoppingList)."),
                });
            }
        }

        return ContextPack.For(design, selection, checks, lists, eval.Question);
    }

    private static EvalCase LoadCase(string path)
    {
        string fileName = Path.GetFileName(path);
        CaseDto dto = JsonSerializer.Deserialize<CaseDto>(File.ReadAllText(path), CaseJson)
            ?? throw new InvalidOperationException($"{fileName}: empty JSON document.");

        string id = dto.Id ?? throw new InvalidOperationException($"{fileName}: no \"id\".");
        string sample = dto.Sample ?? throw new InvalidOperationException($"{fileName}: no \"sample\".");
        string question = dto.Question ?? throw new InvalidOperationException($"{fileName}: no \"question\".");
        string reply = dto.Reply ?? throw new InvalidOperationException($"{fileName}: no \"reply\".");
        EvalTask task = dto.Task switch
        {
            "answer" => EvalTask.Answer,
            "sketch" => EvalTask.Sketch,
            var other => throw new InvalidOperationException($"{fileName}: \"task\" is \"{other}\", not \"answer\" or \"sketch\"."),
        };

        return new EvalCase(
            id,
            fileName,
            sample,
            [.. dto.Select ?? []],
            [.. dto.Lists ?? []],
            question,
            task,
            reply,
            dto.ExpectRefused,
            dto.ExpectReference,
            [.. dto.Forbidden ?? []],
            dto.ExpectParses,
            dto.ExpectedParts,
            dto.Description);
    }

    private sealed class CaseDto
    {
        public string? Id { get; set; }

        public string? Sample { get; set; }

        public string[]? Select { get; set; }

        public string[]? Lists { get; set; }

        public string? Question { get; set; }

        public string? Task { get; set; }

        public string? Reply { get; set; }

        public bool ExpectRefused { get; set; }

        public bool ExpectReference { get; set; } = true;

        public string[]? Forbidden { get; set; }

        public bool ExpectParses { get; set; } = true;

        public int? ExpectedParts { get; set; }

        public string? Description { get; set; }
    }
}

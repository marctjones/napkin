using System.Diagnostics;
using System.Globalization;

using Napkin.Modules.Assistant;

namespace Napkin.Assistant.LocalServer;

/// <summary>What the dialog's Test button reports (docs/design/llm-assistant.md §8): whether the model replied, how long the round trip took, and the line to show.</summary>
/// <param name="Replied">Whether a reply came back.</param>
/// <param name="Elapsed">The round trip.</param>
/// <param name="Line">The dialog's line: the model's reply and the time, or the refusal as it is.</param>
public sealed record TestResult(bool Replied, TimeSpan Elapsed, string Line);

/// <summary>
/// A model run by a program the person installed on this machine — Ollama, or llama.cpp's
/// llama-server — asked over loopback HTTP (docs/design/llm-assistant.md §2.2, §5.2, §6.1). No
/// native code and no model runtime is in napkin's process: the model runs in the other program.
/// </summary>
/// <remarks>
/// <para>
/// <b>Loopback only.</b> The address is a <see cref="LocalEndpoint"/>, which cannot name another
/// machine; the handler napkin builds connects only to loopback addresses, follows no redirect and
/// uses no proxy (<see cref="LoopbackHttp"/>); and a model Ollama reports as remote
/// (<c>remote_host</c>) is refused before anything is sent to it.
/// </para>
/// <para>
/// <b>Which API</b> is probed on the first question (<see cref="LocalProgram"/>: <c>/api/tags</c>,
/// then <c>/v1/models</c>) and remembered for the session once a probe succeeds; a failed probe is
/// not remembered, so starting Ollama after napkin works on the next question. Under Ollama every
/// question also reads <c>/api/tags</c>, so a model pulled, removed or remote since the last
/// question is seen at once.
/// </para>
/// <para>
/// <b>Failures are refusals</b> in napkin's words or the program's own (<see cref="ModelReply.Refused"/>),
/// shown word for word on the note; the person's Escape is an <see cref="OperationCanceledException"/>,
/// and no reply is surfaced. Nothing is remembered between questions (§13.11).
/// </para>
/// </remarks>
public sealed class LocalServerModel : IAssistantModel, IDisposable
{
    /// <summary>napkin's own default temperature for answers (§5.2), not a model card's.</summary>
    public const double DefaultTemperature = 0.2;

    /// <summary>napkin's own limit on one question, probe and answer together (§5.2, §12.1).</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>What the Test button asks: a reply of "ok", nothing about any design.</summary>
    public static readonly ModelRequest TestRequest = new("Reply with the single word ok and nothing else.", string.Empty, "Reply with ok.", null);

    private readonly LocalProgram _server;
    private Dialect? _dialect;

    /// <summary>A model at an address typed as text; the address is refused, with its reason as the exception's message, unless it is this machine.</summary>
    /// <inheritdoc cref="LocalServerModel(LocalEndpoint, string, double, HttpMessageHandler?, TimeSpan?)"/>
    public LocalServerModel(string endpoint, string model, double temperature = DefaultTemperature, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
        : this(LocalEndpoint.Parse(endpoint), model, temperature, handler, timeout)
    {
    }

    /// <summary>A model the program at <paramref name="endpoint"/> runs.</summary>
    /// <param name="endpoint">The program's address on this machine.</param>
    /// <param name="model">The model's name as the program lists it ("qwen3:4b-q4_K_M", or llama-server's model id).</param>
    /// <param name="temperature">The sampling temperature sent with every question.</param>
    /// <param name="handler">How requests travel: null for napkin's loopback-only handler; a test passes a stub. A handler given here is not disposed.</param>
    /// <param name="timeout">napkin's limit on one question; <see cref="DefaultTimeout"/> when null.</param>
    public LocalServerModel(LocalEndpoint endpoint, string model, double temperature = DefaultTemperature, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (!double.IsFinite(temperature) || temperature < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), temperature, "A temperature is a number from zero up.");
        }

        Model = model.Trim();
        Temperature = temperature;
        _server = new LocalProgram(endpoint, handler, timeout);
    }

    /// <summary>The program's address.</summary>
    public LocalEndpoint Endpoint => _server.Endpoint;

    /// <summary>The model's name as the program lists it.</summary>
    public string Model { get; }

    /// <summary>The temperature sent with every question.</summary>
    public double Temperature { get; }

    /// <summary>napkin's limit on one question.</summary>
    public TimeSpan Timeout => _server.Timeout;

    /// <summary>The API the program answered in, once a probe has succeeded; null before.</summary>
    public Dialect? KnownDialect => _dialect;

    /// <inheritdoc/>
    /// <remarks>"Local: qwen3:4b-q4_K_M at 127.0.0.1:11434 — nothing leaves this machine." (§6.1)</remarks>
    public string Whereabouts => $"Local: {Model} at {Endpoint.HostAndPort} — nothing leaves this machine.";

    /// <summary>What a question that ran out of time is refused with.</summary>
    public string NoReply => string.Create(CultureInfo.InvariantCulture, $"No reply in {LocalProgram.Seconds(Timeout)} s from {Model} at {Endpoint.HostAndPort}.");

    /// <summary>
    /// The user message both dialects send: "Context:", the numbered context, a blank line, then
    /// "Question: " and the question. The system prompt goes alone in the system message.
    /// </summary>
    public static string UserMessage(ModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Wire.UserMessage(request);
    }

    /// <inheritdoc/>
    public async Task<ModelReply> AskAsync(ModelRequest request, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancel.ThrowIfCancellationRequested();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(Timeout);
        try
        {
            Dialect dialect;
            if (_dialect == Dialect.OpenAiCompatible)
            {
                dialect = Dialect.OpenAiCompatible;
            }
            else
            {
                ServerListing listing = await _server.ProbeAsync(timeout.Token).ConfigureAwait(false);
                if (listing.Refusal is { } refusal)
                {
                    return new ModelReply.Refused(refusal);
                }

                dialect = listing.Dialect!.Value;
                if (dialect == Dialect.Ollama && RefusalFor(listing) is { } notHere)
                {
                    _dialect = dialect;
                    return new ModelReply.Refused(notHere);
                }
            }

            _dialect = dialect;
            return dialect == Dialect.Ollama
                ? await ChatAsync("api/chat", Wire.OllamaChat(Model, request, Temperature), Wire.OllamaContent, "Ollama", request, timeout.Token).ConfigureAwait(false)
                : await ChatAsync("v1/chat/completions", Wire.OpenAiChat(Model, request, Temperature), Wire.OpenAiContent, "The server", request, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            return new ModelReply.Refused(NoReply);
        }
        catch (HttpRequestException exception)
        {
            return new ModelReply.Refused(_server.NothingAnswered(exception));
        }
    }

    /// <summary>
    /// The Test button (§8): asks the model to reply "ok" and reports the round trip in seconds, or
    /// the refusal as it is. Nothing about any design is sent.
    /// </summary>
    /// <param name="cancel">Cancels the test; the task then cancels.</param>
    public async Task<TestResult> TestAsync(CancellationToken cancel)
    {
        long started = Stopwatch.GetTimestamp();
        ModelReply reply = await AskAsync(TestRequest, cancel).ConfigureAwait(false);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        return reply is ModelReply.Text text
            ? new TestResult(true, elapsed, string.Create(CultureInfo.InvariantCulture, $"{Model} replied in {elapsed.TotalSeconds:0.0} s: “{Shorten(text.Answer)}”."))
            : new TestResult(false, elapsed, ((ModelReply.Refused)reply).Reason);
    }

    /// <summary>
    /// Why an Ollama listing rules this model out: not installed, or — by Ollama's own
    /// <c>remote_host</c>/<c>remote_model</c> — running somewhere else; null when it is here.
    /// </summary>
    private string? RefusalFor(ServerListing listing)
    {
        if (listing.Find(Model) is not { } found)
        {
            return $"Ollama at {Endpoint.HostAndPort} has no model named {Model}. Pull it (ollama pull {Model}) or choose another in Assistant → Where the model runs…";
        }

        return found.IsRemote
            ? $"Ollama says {found.Name} {Guidance.RunsElsewhere(found.RemoteHost)}, so nothing was sent. Choose a model on this machine in Assistant → Where the model runs…"
            : null;
    }

    private async Task<ModelReply> ChatAsync(
        string path,
        byte[] body,
        Func<System.Text.Json.JsonElement?, string?> content,
        string who,
        ModelRequest request,
        CancellationToken cancel)
    {
        WireReply reply = await Wire.SendAsync(_server.Http, HttpMethod.Post, path, body, cancel).ConfigureAwait(false);
        if (!reply.Ok)
        {
            return new ModelReply.Refused($"{who} at {Endpoint.HostAndPort} said: {reply.Complaint}");
        }

        if (content(reply.Json) is not { } answer)
        {
            return new ModelReply.Refused($"The reply from {Model} at {Endpoint.HostAndPort} was not one napkin could read.");
        }

        answer = answer.Trim();
        if (answer.Length == 0)
        {
            return new ModelReply.Refused($"{Model} at {Endpoint.HostAndPort} replied with nothing.");
        }

        return request.Schema is null ? new ModelReply.Text(answer) : new ModelReply.Json(answer);
    }

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";

    /// <inheritdoc/>
    public void Dispose() => _server.Dispose();
}

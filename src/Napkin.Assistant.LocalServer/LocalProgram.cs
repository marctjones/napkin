using System.Collections.Immutable;
using System.Globalization;

namespace Napkin.Assistant.LocalServer;

/// <summary>
/// A program on this machine that runs models — Ollama, or llama.cpp's llama-server — asked what it
/// has (docs/design/llm-assistant.md §5.2, §8's <em>Where the model runs…</em> dialog). Only what it
/// says is shown; nothing is guessed from a model's name, and nothing is ever downloaded.
/// </summary>
/// <remarks>
/// Which API a program speaks is probed the same way everywhere: <c>GET /api/tags</c> first — a
/// <c>models</c> array is Ollama — then <c>GET /v1/models</c> — a <c>data</c> array is an
/// OpenAI-compatible server such as llama-server. Both are reads with no side effect.
/// </remarks>
public sealed class LocalProgram : IDisposable
{
    private readonly HttpClient _http;

    /// <summary>A program at <paramref name="endpoint"/>.</summary>
    /// <param name="endpoint">Its address on this machine.</param>
    /// <param name="handler">
    /// How requests travel: null for napkin's own loopback-only handler (<see cref="LoopbackHttp"/>);
    /// a test passes a stub so nothing opens a socket. A handler given here is not disposed.
    /// </param>
    /// <param name="timeout">How long one call may take in all; <see cref="LocalServerModel.DefaultTimeout"/> when null.</param>
    public LocalProgram(LocalEndpoint endpoint, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        TimeSpan limit = timeout ?? LocalServerModel.DefaultTimeout;
        if (limit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), limit, "A timeout is longer than zero.");
        }

        Endpoint = endpoint;
        Timeout = limit;

        // One timeout, napkin's own (a linked token per call), so a timeout and a cancel are told apart.
        _http = handler is null ? new HttpClient(LoopbackHttp.CreateHandler(), disposeHandler: true) : new HttpClient(handler, disposeHandler: false);
        _http.BaseAddress = endpoint.BaseUri;
        _http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
    }

    /// <summary>Where it is.</summary>
    public LocalEndpoint Endpoint { get; }

    /// <summary>How long one call may take in all.</summary>
    public TimeSpan Timeout { get; }

    internal HttpClient Http => _http;

    /// <summary>What a call that ran out of time is refused with.</summary>
    public string NoAnswer => string.Create(CultureInfo.InvariantCulture, $"No answer in {Seconds(Timeout)} s from {Endpoint}.");

    /// <summary>What a call nothing answered is refused with.</summary>
    public string NothingAnswered(HttpRequestException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return $"Nothing answered at {Endpoint} ({exception.Message}). Start Ollama or llama-server, then try again.";
    }

    /// <summary>
    /// What the program has: the probe's listing and, from Ollama, each local model's
    /// <c>/api/show</c> (license, capabilities, context length). A model Ollama reports as remote is
    /// not asked about. Never throws for the program's sake: a program that is not running, answers
    /// oddly or takes too long is a <see cref="ServerListing.Refused"/> with the reason.
    /// </summary>
    /// <param name="cancel">Cancels the listing; the task then cancels.</param>
    public async Task<ServerListing> ListAsync(CancellationToken cancel)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(Timeout);
        try
        {
            ServerListing listing = await ProbeAsync(timeout.Token).ConfigureAwait(false);
            if (listing.Dialect != Dialect.Ollama)
            {
                return listing;
            }

            List<InstalledModel> shown = [];
            foreach (InstalledModel model in listing.Models)
            {
                shown.Add(model.IsRemote ? model : await ShowAsync(model, timeout.Token).ConfigureAwait(false));
            }

            return listing with { Models = [.. shown] };
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            return ServerListing.Refused(Endpoint, NoAnswer);
        }
        catch (HttpRequestException exception)
        {
            return ServerListing.Refused(Endpoint, NothingAnswered(exception));
        }
    }

    /// <summary>
    /// Which API the program speaks, and what it lists: <c>GET /api/tags</c>, then
    /// <c>GET /v1/models</c>. Refused when it answered neither way; transport failures and
    /// cancellation are the caller's to catch.
    /// </summary>
    internal async Task<ServerListing> ProbeAsync(CancellationToken cancel)
    {
        WireReply tags = await Wire.SendAsync(_http, HttpMethod.Get, "api/tags", null, cancel).ConfigureAwait(false);
        if (tags.Ok && Wire.OllamaModels(tags.Json) is { } ollama)
        {
            return ServerListing.Answered(Endpoint, Dialect.Ollama, ollama);
        }

        WireReply models = await Wire.SendAsync(_http, HttpMethod.Get, "v1/models", null, cancel).ConfigureAwait(false);
        if (models.Ok && Wire.OpenAiModels(models.Json) is { } openAi)
        {
            return ServerListing.Answered(Endpoint, Dialect.OpenAiCompatible, openAi);
        }

        return ServerListing.Refused(
            Endpoint,
            models.Error is { Length: > 0 } said
                ? $"{Endpoint} said: {said}"
                : $"{Endpoint} answered, but not as Ollama (GET /api/tags) or as an OpenAI-compatible server such as llama-server (GET /v1/models).");
    }

    /// <summary>A model with what Ollama's <c>POST /api/show</c> adds to it; the model as it was when Ollama would not show it.</summary>
    internal async Task<InstalledModel> ShowAsync(InstalledModel model, CancellationToken cancel)
    {
        WireReply show = await Wire.SendAsync(_http, HttpMethod.Post, "api/show", Wire.OllamaShow(model.Name), cancel).ConfigureAwait(false);
        return show.Ok ? Wire.WithShow(model, show.Json) : model;
    }

    /// <summary>A duration in seconds as the note says one: "30", "0.05".</summary>
    internal static string Seconds(TimeSpan span) => span.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();
}

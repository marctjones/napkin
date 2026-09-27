using System.Net;
using System.Text;
using System.Text.Json;

namespace Napkin.Assistant.LocalServer.Tests;

/// <summary>A request as the stub received it.</summary>
public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body)
{
    /// <summary>The path asked for, like <c>/api/chat</c>.</summary>
    public string Path => Uri.AbsolutePath;

    /// <summary>The body as JSON.</summary>
    public JsonElement Json => JsonDocument.Parse(Body ?? throw new InvalidOperationException("The request had no body.")).RootElement.Clone();

    public override string ToString() => $"{Method} {Path}";
}

/// <summary>
/// A stand-in for Ollama or llama-server that never opens a socket: an <see cref="HttpMessageHandler"/>
/// that records every request and answers from a function. The bodies it answers with are written by
/// hand in each program's documented shape (<see cref="Documented"/>), never recorded from a real model.
/// </summary>
public sealed class StubServer : HttpMessageHandler
{
    private readonly Func<RecordedRequest, CancellationToken, Task<HttpResponseMessage>> _answer;
    private readonly List<RecordedRequest> _requests = [];

    public StubServer(Func<RecordedRequest, HttpResponseMessage> answer)
        : this((request, _) => Task.FromResult(answer(request)))
    {
    }

    public StubServer(Func<RecordedRequest, CancellationToken, Task<HttpResponseMessage>> answer)
    {
        _answer = answer;
    }

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>The requests to one path.</summary>
    public IReadOnlyList<RecordedRequest> To(string path) => [.. Requests.Where(request => request.Path == path)];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        RecordedRequest recorded = new(request.Method, request.RequestUri!, body);
        lock (_requests)
        {
            _requests.Add(recorded);
        }

        return await _answer(recorded, cancellationToken);
    }

    /// <summary>A JSON reply.</summary>
    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>A 404 with a plain body, as a program that has no such path answers.</summary>
    public static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound) { Content = new StringContent("404 page not found") };

    /// <summary>
    /// An Ollama: <c>/api/tags</c>, <c>/api/show</c> and <c>/api/chat</c> answered with
    /// <see cref="Documented"/>'s bodies; <paramref name="chat"/> is the assistant's reply text.
    /// </summary>
    public static StubServer Ollama(string chat = "Ground snow load is entered, never guessed [1].", string tags = Documented.OllamaTags) =>
        new(request => request.Path switch
        {
            "/api/tags" => Json(tags),
            "/api/show" => Json(Documented.OllamaShow),
            "/api/chat" => Json(Documented.OllamaChat(chat)),
            _ => NotFound(),
        });

    /// <summary>A llama-server: 404 on Ollama's paths, <c>/v1/models</c> and <c>/v1/chat/completions</c> answered.</summary>
    public static StubServer LlamaServer(string chat = "ok") =>
        new(request => request.Path switch
        {
            "/v1/models" => Json(Documented.LlamaModels),
            "/v1/chat/completions" => Json(Documented.OpenAiChat(chat)),
            _ => NotFound(),
        });
}

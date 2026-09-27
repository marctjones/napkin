using System.Net;
using System.Text;
using System.Text.Json;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// An Ollama that never opens a socket, for the assistant workflows (docs/design/llm-assistant.md
/// §11.3): an <see cref="HttpMessageHandler"/> the window's <see cref="MainWindow.AssistantHttp"/> is
/// set to, recording every request and answering in the shapes Ollama documents — written by hand,
/// never recorded from a program or a model. Shapes read on 2026-09-27 from
/// https://github.com/ollama/ollama/blob/main/docs/api.md ("List Local Models", "Show Model
/// Information", "Chat request (No streaming)") and
/// https://github.com/ollama/ollama/blob/main/docs/openapi.yaml (<c>ModelSummary</c>'s
/// <c>remote_model</c>/<c>remote_host</c>, <c>ShowResponse</c>'s <c>license</c>). Sizes and digests
/// are made up for the test.
/// </summary>
public sealed class OllamaStub : HttpMessageHandler
{
    /// <summary>What <c>GET /api/tags</c> lists: one local model, and one Ollama marks as remote.</summary>
    public const string Tags = """
        {
          "models": [
            {
              "name": "qwen3:4b-q4_K_M",
              "model": "qwen3:4b-q4_K_M",
              "modified_at": "2026-09-20T08:06:48.639712648-04:00",
              "size": 2620788019,
              "digest": "2bfd38a7daaf0000000000000000000000000000000000000000000000000000",
              "details": { "parent_model": "", "format": "gguf", "family": "qwen3", "families": ["qwen3"], "parameter_size": "4.0B", "quantization_level": "Q4_K_M" }
            },
            {
              "name": "gpt-oss:120b-cloud",
              "model": "gpt-oss:120b-cloud",
              "remote_model": "gpt-oss:120b",
              "remote_host": "https://ollama.com:443",
              "modified_at": "2026-09-21T09:00:00.000000000-04:00",
              "size": 384,
              "digest": "0000000000000000000000000000000000000000000000000000000000000000",
              "details": { "parent_model": "", "format": "", "family": "gptoss", "families": ["gptoss"], "parameter_size": "116.8B", "quantization_level": "MXFP4" }
            }
          ]
        }
        """;

    /// <summary>What <c>POST /api/show</c> answers: the license text starts the Apache License 2.0, as a model's does.</summary>
    public const string Show = """
        {
          "license": "                                 Apache License\n                           Version 2.0, January 2004\n",
          "details": { "parent_model": "", "format": "gguf", "family": "qwen3", "families": ["qwen3"], "parameter_size": "4.0B", "quantization_level": "Q4_K_M" },
          "model_info": { "general.architecture": "qwen3", "qwen3.context_length": 40960 },
          "capabilities": ["completion", "tools", "thinking"]
        }
        """;

    private readonly List<(string Path, string? Body)> _requests = [];

    /// <summary>The model's answer to anything but the dialog's Test.</summary>
    public string Answer { get; set; } = "ok";

    /// <summary>Every request received, as its path and body.</summary>
    public IReadOnlyList<(string Path, string? Body)> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string path = request.RequestUri!.AbsolutePath;
        lock (_requests)
        {
            _requests.Add((path, body));
        }

        return path switch
        {
            "/api/tags" => Json(Tags),
            "/api/show" => Json(Show),
            "/api/chat" => Json(JsonSerializer.Serialize(new
            {
                model = "qwen3:4b-q4_K_M",
                created_at = "2026-09-27T14:13:43.416799Z",
                message = new { role = "assistant", content = body!.Contains("Reply with ok.", StringComparison.Ordinal) ? "ok" : Answer },
                done_reason = "stop",
                done = true,
            })),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("404 page not found") },
        };
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

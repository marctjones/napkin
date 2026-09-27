using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

using Napkin.Modules.Assistant;

namespace Napkin.Assistant.LocalServer;

/// <summary>A reply as it came back: its status, its body's text, and the body as JSON when it is JSON.</summary>
internal sealed record WireReply(HttpStatusCode Status, string Text, JsonElement? Json)
{
    public bool Ok => Status == HttpStatusCode.OK;

    /// <summary>
    /// The program's own error message: Ollama's <c>{"error": "…"}</c> (docs/api/errors.mdx: "with
    /// the error message in the <c>error</c> property") or llama-server's OpenAI-shaped
    /// <c>{"error": {"code", "message", "type"}}</c> (tools/server/README.md "API errors"), both read
    /// 2026-09-27; null when the body carries neither.
    /// </summary>
    public string? Error => Json is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("error", out JsonElement error)
        ? error.ValueKind switch
        {
            JsonValueKind.String => error.GetString(),
            JsonValueKind.Object when error.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String => message.GetString(),
            _ => null,
        }
        : null;

    /// <summary>What the program said went wrong: its own message, or its status when it gave none.</summary>
    public string Complaint => Error is { Length: > 0 } error
        ? error
        : string.Create(CultureInfo.InvariantCulture, $"HTTP {(int)Status} {Status}");
}

/// <summary>The two dialects' request bodies and reply readers (docs/design/llm-assistant.md §5.2), exactly as their docs give them.</summary>
internal static class Wire
{
    /// <summary>Sends one request and reads the whole reply under <paramref name="cancel"/>.</summary>
    public static async Task<WireReply> SendAsync(HttpClient http, HttpMethod method, string path, byte[]? body, CancellationToken cancel)
    {
        using HttpRequestMessage message = new(method, path);
        if (body is not null)
        {
            message.Content = new ByteArrayContent(body);
            message.Content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
        }

        using HttpResponseMessage response = await http.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancel).ConfigureAwait(false);
        string text = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        return new WireReply(response.StatusCode, text, TryParse(text));
    }

    private static JsonElement? TryParse(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The user message: the numbered context, then the question — the system prompt says "the
    /// numbered context below", and the person's own words stay out of the system message.
    /// </summary>
    public static string UserMessage(ModelRequest request) =>
        request.Context.Length == 0
            ? $"Question: {request.Question}"
            : $"Context:\n{request.Context}\n\nQuestion: {request.Question}";

    /// <summary>
    /// Ollama's <c>POST /api/chat</c> body (docs/api.md "Generate a chat completion", read
    /// 2026-09-27): <c>model</c>; <c>messages</c> (system, user); <c>stream: false</c> ("the response
    /// will be returned as a single response object"); <c>think: false</c> ("should the model think
    /// before responding?"); <c>options.temperature</c>; and for a proposal <c>format</c> = the schema,
    /// written through as JSON ("Format can be <c>json</c> or a JSON schema"). <c>keep_alive</c> is left
    /// to the program's default.
    /// </summary>
    public static byte[] OllamaChat(string model, ModelRequest request, double temperature) => Write(json =>
    {
        json.WriteString("model", model);
        WriteMessages(json, request);
        json.WriteBoolean("stream", false);
        json.WriteBoolean("think", false);
        json.WriteStartObject("options");
        json.WriteNumber("temperature", temperature);
        json.WriteEndObject();
        if (request.Schema is { } schema)
        {
            json.WritePropertyName("format");
            json.WriteRawValue(schema);
        }
    });

    /// <summary>
    /// llama-server's <c>POST /v1/chat/completions</c> body (tools/server/README.md, read 2026-09-27):
    /// <c>model</c>, <c>messages</c>, <c>stream: false</c>, <c>temperature</c>;
    /// <c>chat_template_kwargs: {"enable_thinking": false}</c> ("Allows sending additional parameters
    /// to the json templating system. For example: <c>{"enable_thinking": false}</c>") — the
    /// counterpart of Ollama's <c>think: false</c>; and for a proposal <c>response_format</c> =
    /// <c>{"type": "json_object", "schema": …}</c>, the README's "schema-constrained JSON" form, with
    /// the schema directly under <c>response_format</c>.
    /// </summary>
    public static byte[] OpenAiChat(string model, ModelRequest request, double temperature) => Write(json =>
    {
        json.WriteString("model", model);
        WriteMessages(json, request);
        json.WriteBoolean("stream", false);
        json.WriteNumber("temperature", temperature);
        json.WriteStartObject("chat_template_kwargs");
        json.WriteBoolean("enable_thinking", false);
        json.WriteEndObject();
        if (request.Schema is { } schema)
        {
            json.WriteStartObject("response_format");
            json.WriteString("type", "json_object");
            json.WritePropertyName("schema");
            json.WriteRawValue(schema);
            json.WriteEndObject();
        }
    });

    /// <summary>Ollama's <c>POST /api/show</c> body: <c>model</c>, "name of the model to show" (docs/api.md and openapi.yaml <c>ShowRequest</c>).</summary>
    public static byte[] OllamaShow(string model) => Write(json => json.WriteString("model", model));

    /// <summary>Ollama's reply text: <c>message.content</c> (docs/api.md "Chat request (No streaming)").</summary>
    public static string? OllamaContent(JsonElement? reply) =>
        reply is { ValueKind: JsonValueKind.Object } root
        && root.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.Object
        && message.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.String
            ? content.GetString()
            : null;

    /// <summary>An OpenAI-compatible reply's text: <c>choices[0].message.content</c>.</summary>
    public static string? OpenAiContent(JsonElement? reply) =>
        reply is { ValueKind: JsonValueKind.Object } root
        && root.TryGetProperty("choices", out JsonElement choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
        && choices[0].ValueKind == JsonValueKind.Object
        && choices[0].TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.Object
        && message.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.String
            ? content.GetString()
            : null;

    /// <summary>
    /// Ollama's <c>GET /api/tags</c> <c>models</c> (docs/api.md "List Local Models"; openapi.yaml
    /// <c>ModelSummary</c>): <c>name</c>, <c>size</c>, <c>details.format</c>,
    /// <c>details.parameter_size</c>, <c>details.quantization_level</c>, <c>remote_model</c>,
    /// <c>remote_host</c>. Null when the reply has no <c>models</c> array — it is not Ollama.
    /// </summary>
    public static ImmutableArray<InstalledModel>? OllamaModels(JsonElement? reply)
    {
        if (reply is not { ValueKind: JsonValueKind.Object } root
            || !root.TryGetProperty("models", out JsonElement models) || models.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return [.. models.EnumerateArray()
            .Where(model => model.ValueKind == JsonValueKind.Object && StringIn(model, "name") is not null)
            .Select(model =>
            {
                JsonElement? details = model.TryGetProperty("details", out JsonElement d) && d.ValueKind == JsonValueKind.Object ? d : null;
                return new InstalledModel(StringIn(model, "name")!)
                {
                    SizeBytes = LongIn(model, "size"),
                    Format = details is { } f ? StringIn(f, "format") : null,
                    ParameterSize = details is { } p ? StringIn(p, "parameter_size") : null,
                    QuantizationLevel = details is { } q ? StringIn(q, "quantization_level") : null,
                    RemoteModel = StringIn(model, "remote_model"),
                    RemoteHost = StringIn(model, "remote_host"),
                };
            })];
    }

    /// <summary>
    /// Adds what Ollama's <c>POST /api/show</c> says (docs/api.md "Show Model Information";
    /// openapi.yaml <c>ShowResponse</c>): <c>license</c>, <c>details.quantization_level</c> and
    /// <c>details.parameter_size</c> where <c>/api/tags</c> gave none, <c>capabilities</c>, and the
    /// context length from <c>model_info</c>.
    /// </summary>
    public static InstalledModel WithShow(InstalledModel model, JsonElement? reply)
    {
        if (reply is not { ValueKind: JsonValueKind.Object } root)
        {
            return model;
        }

        JsonElement? details = root.TryGetProperty("details", out JsonElement d) && d.ValueKind == JsonValueKind.Object ? d : null;
        long? context = null;
        if (root.TryGetProperty("model_info", out JsonElement info) && info.ValueKind == JsonValueKind.Object
            && StringIn(info, "general.architecture") is { } architecture)
        {
            context = LongIn(info, architecture + ".context_length");
        }

        ImmutableArray<string> capabilities = root.TryGetProperty("capabilities", out JsonElement caps) && caps.ValueKind == JsonValueKind.Array
            ? [.. caps.EnumerateArray().Where(cap => cap.ValueKind == JsonValueKind.String).Select(cap => cap.GetString()!)]
            : model.Capabilities;

        return model with
        {
            License = StringIn(root, "license") ?? model.License,
            QuantizationLevel = model.QuantizationLevel ?? (details is { } q ? StringIn(q, "quantization_level") : null),
            ParameterSize = model.ParameterSize ?? (details is { } p ? StringIn(p, "parameter_size") : null),
            ContextLength = context ?? model.ContextLength,
            Capabilities = capabilities,
        };
    }

    /// <summary>
    /// llama-server's <c>GET /v1/models</c> <c>data</c> (tools/server/README.md "OpenAI-compatible
    /// Model Info API": "The returned list always has one single element. The <c>meta</c> field can
    /// be <c>null</c>"): <c>id</c>, and from <c>meta</c> <c>size</c>, <c>n_params</c> and
    /// <c>n_ctx_train</c>. It reports no quantization and no license. Null when the reply has no
    /// <c>data</c> array.
    /// </summary>
    public static ImmutableArray<InstalledModel>? OpenAiModels(JsonElement? reply)
    {
        if (reply is not { ValueKind: JsonValueKind.Object } root
            || !root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return [.. data.EnumerateArray()
            .Where(model => model.ValueKind == JsonValueKind.Object && StringIn(model, "id") is not null)
            .Select(model =>
            {
                JsonElement? meta = model.TryGetProperty("meta", out JsonElement m) && m.ValueKind == JsonValueKind.Object ? m : null;
                return new InstalledModel(StringIn(model, "id")!)
                {
                    SizeBytes = meta is { } s ? LongIn(s, "size") : null,
                    ParameterSize = meta is { } p && LongIn(p, "n_params") is { } count ? count.ToString("N0", CultureInfo.InvariantCulture) : null,
                    ContextLength = meta is { } c ? LongIn(c, "n_ctx_train") : null,
                };
            })];
    }

    private static void WriteMessages(Utf8JsonWriter json, ModelRequest request)
    {
        json.WriteStartArray("messages");
        json.WriteStartObject();
        json.WriteString("role", "system");
        json.WriteString("content", request.System);
        json.WriteEndObject();
        json.WriteStartObject();
        json.WriteString("role", "user");
        json.WriteString("content", UserMessage(request));
        json.WriteEndObject();
        json.WriteEndArray();
    }

    private static byte[] Write(Action<Utf8JsonWriter> properties)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter json = new(stream))
        {
            json.WriteStartObject();
            properties(json);
            json.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static string? StringIn(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? LongIn(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) ? number : null;
}

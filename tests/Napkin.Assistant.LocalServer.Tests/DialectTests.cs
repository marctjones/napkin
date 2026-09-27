using System.Net;
using System.Text.Json;

using Napkin.Modules.Assistant;

namespace Napkin.Assistant.LocalServer.Tests;

/// <summary>
/// The two dialects exactly as their docs give them (docs/design/llm-assistant.md §5.2, §11.2):
/// every request body asserted field by field, the schema present only for a proposal and passed
/// through as it is, and each reply read from where the docs put the answer. Stub replies:
/// <see cref="Documented"/>.
/// </summary>
public class DialectTests
{
    private const string Schema = """{"type":"object","properties":{"parts":{"type":"array"}},"required":["parts"],"additionalProperties":false}""";

    private static readonly ModelRequest Answer = new("SYSTEM PROMPT", "[1] Site: ground snow load not entered.", "what is ground snow load", null);
    private static readonly ModelRequest Proposal = Answer with { Question = "a bench 4 ft long", Schema = Schema };

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task Ollama_is_asked_on_api_chat_with_every_field_the_note_names_and_no_format_for_an_answer()
    {
        StubServer stub = StubServer.Ollama(chat: "Ground snow load is entered from the building department, never guessed [1].");
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", 0.2, stub);

        ModelReply reply = await model.AskAsync(Answer, CancellationToken.None);

        Assert.Equal(new ModelReply.Text("Ground snow load is entered from the building department, never guessed [1]."), reply);
        Assert.Equal(["GET /api/tags", "POST /api/chat"], stub.Requests.Select(request => request.ToString()));
        Assert.Equal(new Uri("http://127.0.0.1:11434/api/chat"), stub.Requests[1].Uri);

        JsonElement body = stub.Requests[1].Json;
        Assert.Equal(["model", "messages", "stream", "think", "options"], body.EnumerateObject().Select(property => property.Name));
        Assert.Equal("qwen3:4b-q4_K_M", body.GetProperty("model").GetString());
        Assert.False(body.GetProperty("stream").GetBoolean());
        Assert.Equal(JsonValueKind.False, body.GetProperty("think").ValueKind);
        Assert.Equal(0.2, body.GetProperty("options").GetProperty("temperature").GetDouble());
        // §16.1 item 3, §17 (the slice-C carry-over, issue #232): Ollama's own default context
        // window (4,096 tokens) is smaller than napkin's 6,000-word pack can need, so every
        // question raises it through the FAQ's own options.num_ctx.
        Assert.Equal(LocalServerModel.OllamaContextLength, body.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(["temperature", "num_ctx"], body.GetProperty("options").EnumerateObject().Select(property => property.Name));
        Assert.False(body.TryGetProperty("format", out _));
        AssertMessages(body, Answer);
        Assert.Equal(Dialect.Ollama, model.KnownDialect);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task Ollama_gets_the_proposals_schema_as_format_exactly_and_the_reply_is_json_unparsed()
    {
        StubServer stub = StubServer.Ollama(chat: """{"parts": []}""");
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", 0.2, stub);

        ModelReply reply = await model.AskAsync(Proposal, CancellationToken.None);

        Assert.Equal(new ModelReply.Json("""{"parts": []}"""), reply);
        JsonElement body = stub.To("/api/chat").Single().Json;
        Assert.Equal(["model", "messages", "stream", "think", "options", "format"], body.EnumerateObject().Select(property => property.Name));
        Assert.Equal(JsonValueKind.Object, body.GetProperty("format").ValueKind);
        Assert.Equal(Schema, body.GetProperty("format").GetRawText());
        AssertMessages(body, Proposal);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task A_llama_server_is_found_by_its_models_list_and_asked_on_v1_chat_completions()
    {
        StubServer stub = StubServer.LlamaServer(chat: "  The answer [1].\n");
        using LocalServerModel model = new("http://127.0.0.1:8080", "qwen3-4b", 0.5, stub);

        ModelReply reply = await model.AskAsync(Answer, CancellationToken.None);

        Assert.Equal(new ModelReply.Text("The answer [1]."), reply);
        Assert.Equal(["GET /api/tags", "GET /v1/models", "POST /v1/chat/completions"], stub.Requests.Select(request => request.ToString()));
        JsonElement body = stub.Requests[2].Json;
        Assert.Equal(["model", "messages", "stream", "temperature", "chat_template_kwargs"], body.EnumerateObject().Select(property => property.Name));
        Assert.Equal("qwen3-4b", body.GetProperty("model").GetString());
        Assert.False(body.GetProperty("stream").GetBoolean());
        Assert.Equal(0.5, body.GetProperty("temperature").GetDouble());
        Assert.Equal("""{"enable_thinking":false}""", body.GetProperty("chat_template_kwargs").GetRawText());
        Assert.False(body.TryGetProperty("response_format", out _));

        // §16.1 item 3, §17: llama-server's context is fixed at server start (-c/--ctx-size), so
        // napkin sends nothing for it here — unlike Ollama's options.num_ctx above.
        Assert.False(body.TryGetProperty("num_ctx", out _));
        Assert.False(body.TryGetProperty("options", out _));
        AssertMessages(body, Answer);
        Assert.Equal(Dialect.OpenAiCompatible, model.KnownDialect);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task A_llama_server_gets_the_schema_under_response_format_and_the_dialect_is_remembered()
    {
        StubServer stub = StubServer.LlamaServer(chat: """{"parts":[]}""");
        using LocalServerModel model = new("http://127.0.0.1:8080", "qwen3-4b", handler: stub);

        Assert.Equal(new ModelReply.Json("""{"parts":[]}"""), await model.AskAsync(Proposal, CancellationToken.None));
        Assert.Equal(new ModelReply.Json("""{"parts":[]}"""), await model.AskAsync(Proposal, CancellationToken.None));

        // Probed once; the second question goes straight to the chat.
        Assert.Equal(
            ["GET /api/tags", "GET /v1/models", "POST /v1/chat/completions", "POST /v1/chat/completions"],
            stub.Requests.Select(request => request.ToString()));
        JsonElement format = stub.Requests[3].Json.GetProperty("response_format");
        Assert.Equal(["type", "schema"], format.EnumerateObject().Select(property => property.Name));
        Assert.Equal("json_object", format.GetProperty("type").GetString());
        Assert.Equal(Schema, format.GetProperty("schema").GetRawText());
    }

    [Fact]
    public async Task A_schema_that_is_not_json_is_napkins_own_bug_and_is_not_hidden()
    {
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: StubServer.Ollama());
        await Assert.ThrowsAnyAsync<Exception>(() => model.AskAsync(Answer with { Schema = "{not json" }, CancellationToken.None));
    }

    [Fact]
    public void The_user_message_is_the_numbered_context_then_the_question()
    {
        Assert.Equal(
            "Context:\n[1] Site: ground snow load not entered.\n\nQuestion: what is ground snow load",
            LocalServerModel.UserMessage(Answer));
        Assert.Equal("Question: Reply with ok.", LocalServerModel.UserMessage(LocalServerModel.TestRequest));
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task A_model_ollama_reports_as_remote_is_refused_and_nothing_is_sent_to_it()
    {
        StubServer stub = StubServer.Ollama();
        using LocalServerModel model = new("http://127.0.0.1:11434", "gpt-oss:120b-cloud", handler: stub);

        ModelReply reply = await model.AskAsync(Answer, CancellationToken.None);

        Assert.Equal(
            new ModelReply.Refused("Ollama says gpt-oss:120b-cloud runs at https://ollama.com:443, not on this machine; napkin will not use it, so nothing was sent. Choose a model on this machine in Assistant → Where the model runs…"),
            reply);
        Assert.Empty(stub.To("/api/chat"));
    }

    [Theory]
    [InlineData("qwen3:4b")]
    [InlineData("llama3.2")]
    public async Task A_model_ollama_does_not_have_is_refused_before_anything_is_sent(string name)
    {
        StubServer stub = StubServer.Ollama();
        using LocalServerModel model = new("http://127.0.0.1:11434", name, handler: stub);

        ModelReply reply = await model.AskAsync(Answer, CancellationToken.None);

        Assert.Equal(
            new ModelReply.Refused($"Ollama at 127.0.0.1:11434 has no model named {name}. Pull it (ollama pull {name}) or choose another in Assistant → Where the model runs…"),
            reply);
        Assert.Empty(stub.To("/api/chat"));
    }

    [Fact]
    public async Task A_name_without_a_tag_asks_for_the_latest_as_ollama_does()
    {
        string tags = """{"models": [{"name": "phi4-mini:latest", "model": "phi4-mini:latest", "size": 2491876774, "details": {"format": "gguf", "parameter_size": "3.8B", "quantization_level": "Q4_K_M"}}]}""";
        StubServer stub = StubServer.Ollama(chat: "ok", tags: tags);
        using LocalServerModel model = new("http://localhost:11434", "phi4-mini", handler: stub);

        Assert.Equal(new ModelReply.Text("ok"), await model.AskAsync(Answer, CancellationToken.None));
        Assert.Equal("phi4-mini", stub.To("/api/chat").Single().Json.GetProperty("model").GetString());
    }

    [Theory]
    [Trait("Feature", "AST-006")]
    [InlineData(HttpStatusCode.NotFound, "model 'qwen3:4b-q4_K_M' not found", "Ollama at 127.0.0.1:11434 said: model 'qwen3:4b-q4_K_M' not found")]
    [InlineData(HttpStatusCode.InternalServerError, "the model failed to generate a response", "Ollama at 127.0.0.1:11434 said: the model failed to generate a response")]
    public async Task Ollamas_own_error_is_the_refusal_word_for_word(HttpStatusCode status, string error, string reason)
    {
        StubServer stub = new(request => request.Path == "/api/chat"
            ? StubServer.Json(Documented.OllamaError(error), status)
            : StubServer.Json(Documented.OllamaTags));
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: stub);

        Assert.Equal(new ModelReply.Refused(reason), await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public async Task A_llama_servers_error_object_is_the_refusal_word_for_word()
    {
        StubServer stub = new(request => request.Path switch
        {
            "/v1/models" => StubServer.Json(Documented.LlamaModels),
            "/v1/chat/completions" => StubServer.Json(Documented.OpenAiError(503, "Loading model", "unavailable_error"), HttpStatusCode.ServiceUnavailable),
            _ => StubServer.NotFound(),
        });
        using LocalServerModel model = new("http://127.0.0.1:8080", "qwen3-4b", handler: stub);

        Assert.Equal(new ModelReply.Refused("The server at 127.0.0.1:8080 said: Loading model"), await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public async Task An_error_with_no_message_is_said_by_its_status()
    {
        StubServer stub = new(request => request.Path == "/api/chat"
            ? new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>bad gateway</html>") }
            : StubServer.Json(Documented.OllamaTags));
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: stub);

        Assert.Equal(new ModelReply.Refused("Ollama at 127.0.0.1:11434 said: HTTP 502 BadGateway"), await model.AskAsync(Answer, CancellationToken.None));
    }

    [Theory]
    [InlineData("""{"message": {"role": "assistant"}}""")]
    [InlineData("""{"message": "hello"}""")]
    [InlineData("""{"done": true}""")]
    [InlineData("""["not", "an", "object"]""")]
    [InlineData("not json at all")]
    public async Task An_ollama_reply_with_no_readable_content_is_refused(string chat)
    {
        StubServer stub = new(request => request.Path == "/api/chat" ? StubServer.Json(chat) : StubServer.Json(Documented.OllamaTags));
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: stub);

        Assert.Equal(
            new ModelReply.Refused("The reply from qwen3:4b-q4_K_M at 127.0.0.1:11434 was not one napkin could read."),
            await model.AskAsync(Answer, CancellationToken.None));
    }

    [Theory]
    [InlineData("""{"choices": []}""")]
    [InlineData("""{"choices": ["x"]}""")]
    [InlineData("""{"choices": [{"message": "x"}]}""")]
    [InlineData("""{"choices": [{"message": {"content": null}}]}""")]
    [InlineData("""{"id": "chatcmpl-1"}""")]
    public async Task An_openai_reply_with_no_readable_content_is_refused(string chat)
    {
        StubServer stub = new(request => request.Path switch
        {
            "/v1/models" => StubServer.Json(Documented.LlamaModels),
            "/v1/chat/completions" => StubServer.Json(chat),
            _ => StubServer.NotFound(),
        });
        using LocalServerModel model = new("http://127.0.0.1:8080", "qwen3-4b", handler: stub);

        Assert.Equal(
            new ModelReply.Refused("The reply from qwen3-4b at 127.0.0.1:8080 was not one napkin could read."),
            await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public async Task An_empty_answer_is_refused_rather_than_shown_as_nothing()
    {
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: StubServer.Ollama(chat: "  \n "));

        Assert.Equal(
            new ModelReply.Refused("qwen3:4b-q4_K_M at 127.0.0.1:11434 replied with nothing."),
            await model.AskAsync(Answer, CancellationToken.None));
    }

    [Fact]
    public void A_model_needs_a_name_and_a_temperature_from_zero_up()
    {
        Assert.ThrowsAny<ArgumentException>(() => new LocalServerModel("http://127.0.0.1:11434", " ", handler: StubServer.Ollama()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalServerModel("http://127.0.0.1:11434", "m", -0.1, StubServer.Ollama()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalServerModel("http://127.0.0.1:11434", "m", double.NaN, StubServer.Ollama()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalServerModel("http://127.0.0.1:11434", "m", 0.2, StubServer.Ollama(), TimeSpan.Zero));

        using LocalServerModel model = new(LocalEndpoint.Parse("localhost:11434"), "  qwen3:8b ", 0, StubServer.Ollama());
        Assert.Equal("qwen3:8b", model.Model);
        Assert.Equal(0, model.Temperature);
        Assert.Equal(LocalServerModel.DefaultTimeout, model.Timeout);
        Assert.Null(model.KnownDialect);
    }

    private static void AssertMessages(JsonElement body, ModelRequest request)
    {
        JsonElement[] messages = [.. body.GetProperty("messages").EnumerateArray()];
        Assert.Equal(2, messages.Length);
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(request.System, messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal(LocalServerModel.UserMessage(request), messages[1].GetProperty("content").GetString());
        Assert.All(messages, message => Assert.Equal(["role", "content"], message.EnumerateObject().Select(property => property.Name)));
    }
}

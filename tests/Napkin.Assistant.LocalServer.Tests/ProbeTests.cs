using System.Net;

using Napkin.Modules.Assistant;

namespace Napkin.Assistant.LocalServer.Tests;

/// <summary>
/// The probe, the timeout and cancellation (docs/design/llm-assistant.md §5.2, §11.2): which API a
/// program speaks is found once and remembered, a failed probe is not; nothing answering, answering
/// neither way, or answering too late is a refusal in napkin's words; the person's Escape cancels
/// and surfaces no reply.
/// </summary>
public class ProbeTests
{
    private static readonly ModelRequest Question = new("SYSTEM", "[1] context", "a question", null);

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task A_program_answering_neither_way_is_refused_with_its_address()
    {
        StubServer stub = new(_ => StubServer.NotFound());
        using LocalServerModel model = new("http://127.0.0.1:9000", "anything", handler: stub);

        Assert.Equal(
            new ModelReply.Refused("http://127.0.0.1:9000 answered, but not as Ollama (GET /api/tags) or as an OpenAI-compatible server such as llama-server (GET /v1/models)."),
            await model.AskAsync(Question, CancellationToken.None));
        Assert.Equal(["GET /api/tags", "GET /v1/models"], stub.Requests.Select(request => request.ToString()));
        Assert.Null(model.KnownDialect);
    }

    [Fact]
    public async Task A_program_that_says_why_it_cannot_list_is_refused_in_its_words()
    {
        StubServer stub = new(request => request.Path == "/v1/models"
            ? StubServer.Json(Documented.OpenAiError(503, "Loading model", "unavailable_error"), HttpStatusCode.ServiceUnavailable)
            : StubServer.NotFound());
        using LocalServerModel model = new("http://127.0.0.1:8080", "qwen3-4b", handler: stub);

        Assert.Equal(new ModelReply.Refused("http://127.0.0.1:8080 said: Loading model"), await model.AskAsync(Question, CancellationToken.None));
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task Nothing_answering_is_refused_and_not_remembered_so_starting_ollama_later_works()
    {
        bool running = false;
        StubServer stub = new(request => running
            ? request.Path switch
            {
                "/api/tags" => StubServer.Json(Documented.OllamaTags),
                "/api/chat" => StubServer.Json(Documented.OllamaChat("ok")),
                _ => StubServer.NotFound(),
            }
            : throw new HttpRequestException("Connection refused (127.0.0.1:11434)"));
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: stub);

        Assert.Equal(
            new ModelReply.Refused("Nothing answered at http://127.0.0.1:11434 (Connection refused (127.0.0.1:11434)). Start Ollama or llama-server, then try again."),
            await model.AskAsync(Question, CancellationToken.None));
        Assert.Null(model.KnownDialect);

        running = true;
        Assert.Equal(new ModelReply.Text("ok"), await model.AskAsync(Question, CancellationToken.None));
        Assert.Equal(Dialect.Ollama, model.KnownDialect);
    }

    [Fact]
    public async Task Under_ollama_every_question_reads_the_list_again_so_a_model_removed_since_is_seen()
    {
        string tags = Documented.OllamaTags;
        StubServer stub = new(request => request.Path switch
        {
            "/api/tags" => StubServer.Json(tags),
            "/api/chat" => StubServer.Json(Documented.OllamaChat("ok")),
            _ => StubServer.NotFound(),
        });
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: stub);

        Assert.Equal(new ModelReply.Text("ok"), await model.AskAsync(Question, CancellationToken.None));
        tags = """{"models": []}""";
        Assert.IsType<ModelReply.Refused>(await model.AskAsync(Question, CancellationToken.None));
        Assert.Equal(["GET /api/tags", "POST /api/chat", "GET /api/tags"], stub.Requests.Select(request => request.ToString()));
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task A_reply_later_than_the_timeout_is_refused_as_no_reply()
    {
        StubServer stub = new(async (request, cancel) =>
        {
            if (request.Path == "/api/tags")
            {
                return StubServer.Json(Documented.OllamaTags);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancel);
            return StubServer.Json(Documented.OllamaChat("too late"));
        });
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: stub, timeout: TimeSpan.FromMilliseconds(50));

        ModelReply reply = await model.AskAsync(Question, CancellationToken.None);

        Assert.Equal(new ModelReply.Refused("No reply in 0.05 s from qwen3:4b-q4_K_M at 127.0.0.1:11434."), reply);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public void The_default_timeout_is_napkins_thirty_seconds_and_says_so()
    {
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: StubServer.Ollama());

        Assert.Equal(TimeSpan.FromSeconds(30), model.Timeout);
        Assert.Equal("No reply in 30 s from qwen3:4b-q4_K_M at 127.0.0.1:11434.", model.NoReply);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task Escape_cancels_the_question_and_no_reply_is_surfaced()
    {
        TaskCompletionSource asked = new();
        StubServer stub = new(async (request, cancel) =>
        {
            if (request.Path == "/api/tags")
            {
                return StubServer.Json(Documented.OllamaTags);
            }

            asked.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancel);
            return StubServer.Json(Documented.OllamaChat("never"));
        });
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: stub);
        using CancellationTokenSource escape = new();

        Task<ModelReply> question = model.AskAsync(Question, escape.Token);
        await asked.Task;
        escape.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => question);
    }

    [Fact]
    public async Task A_question_cancelled_before_it_is_asked_sends_nothing()
    {
        StubServer stub = StubServer.Ollama();
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: stub);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.AskAsync(Question, new CancellationToken(canceled: true)));
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task The_test_button_asks_for_ok_and_reports_the_round_trip()
    {
        StubServer stub = StubServer.Ollama(chat: "ok");
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: stub);

        TestResult result = await model.TestAsync(CancellationToken.None);

        Assert.True(result.Replied);
        Assert.Equal($"qwen3:4b-q4_K_M replied in {result.Elapsed.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s: “ok”.", result.Line);
        string user = stub.To("/api/chat").Single().Json.GetProperty("messages")[1].GetProperty("content").GetString()!;
        Assert.Equal("Question: Reply with ok.", user);
    }

    [Fact]
    public async Task The_test_button_shows_a_long_reply_cut_and_a_refusal_as_it_is()
    {
        using LocalServerModel chatty = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: StubServer.Ollama(chat: new string('o', 45) + "k"));
        Assert.EndsWith($"“{new string('o', 40)}…”.", (await chatty.TestAsync(CancellationToken.None)).Line, StringComparison.Ordinal);

        using LocalServerModel missing = new("http://127.0.0.1:11434", "qwen3:14b", handler: StubServer.Ollama());
        TestResult refused = await missing.TestAsync(CancellationToken.None);
        Assert.False(refused.Replied);
        Assert.Equal("Ollama at 127.0.0.1:11434 has no model named qwen3:14b. Pull it (ollama pull qwen3:14b) or choose another in Assistant → Where the model runs…", refused.Line);
    }
}

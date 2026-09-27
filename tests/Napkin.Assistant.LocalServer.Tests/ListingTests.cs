using System.Net;

namespace Napkin.Assistant.LocalServer.Tests;

/// <summary>
/// The dialog's model list (docs/design/llm-assistant.md §5.2, §5.4, §5.5, §8): what the program
/// reports — size, quantization, license — as read and never guessed from a name; a remote model
/// marked and not asked about; napkin's memory rule labelled as napkin's.
/// </summary>
public class ListingTests
{
    private const long Sixteen = 16_000_000_000;

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task Ollama_lists_its_models_and_each_local_ones_license_comes_from_api_show()
    {
        StubServer stub = StubServer.Ollama();
        using LocalProgram program = new(LocalEndpoint.Parse("http://127.0.0.1:11434"), stub);

        ServerListing listing = await program.ListAsync(CancellationToken.None);

        Assert.Equal(Dialect.Ollama, listing.Dialect);
        Assert.Null(listing.Refusal);
        Assert.Equal(["qwen3:4b-q4_K_M", "gpt-oss:120b-cloud"], listing.Models.Select(model => model.Name));

        InstalledModel local = listing.Models[0];
        Assert.Equal(2620788019, local.SizeBytes);
        Assert.Equal("gguf", local.Format);
        Assert.Equal("4.0B", local.ParameterSize);
        Assert.Equal("Q4_K_M", local.QuantizationLevel);
        Assert.StartsWith("                                 Apache License\n", local.License, StringComparison.Ordinal);
        Assert.Equal(40960, local.ContextLength);
        Assert.Equal(["completion", "tools", "thinking"], local.Capabilities);
        Assert.False(local.IsRemote);
        Assert.Equal(
            "qwen3:4b-q4_K_M — 2.6 GB, 4.0B parameters, Q4_K_M, license: Apache License Version 2.0, January 2004 http://www.apache.org/licenses/…, context 40,960 tokens",
            local.Describe(Sixteen));

        // /api/show is asked with "model", the documented field — and never about the remote one.
        RecordedRequest show = Assert.Single(stub.To("/api/show"));
        Assert.Equal("""{"model":"qwen3:4b-q4_K_M"}""", show.Body);

        InstalledModel remote = listing.Models[1];
        Assert.True(remote.IsRemote);
        Assert.Equal("https://ollama.com:443", remote.RemoteHost);
        Assert.Equal("gpt-oss:120b", remote.RemoteModel);
        Assert.Null(remote.License);
        Assert.Equal("gpt-oss:120b-cloud — runs at https://ollama.com:443, not on this machine; napkin will not use it", remote.Describe(Sixteen));
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task A_llama_server_lists_its_one_model_with_what_it_reports_and_says_what_it_does_not()
    {
        StubServer stub = StubServer.LlamaServer();
        using LocalProgram program = new(LocalEndpoint.Parse("http://127.0.0.1:8080"), stub);

        ServerListing listing = await program.ListAsync(CancellationToken.None);

        Assert.Equal(Dialect.OpenAiCompatible, listing.Dialect);
        InstalledModel model = Assert.Single(listing.Models);
        Assert.Equal("../models/Qwen3-4B-Q4_K_M.gguf", model.Name);
        Assert.Equal(
            "../models/Qwen3-4B-Q4_K_M.gguf — 2.5 GB, 4,022,468,096 parameters, quantization not reported, license not reported, context 40,960 tokens",
            model.Describe(Sixteen));
        Assert.Empty(stub.To("/api/show"));
    }

    [Fact]
    public async Task A_llama_server_still_loading_lists_its_model_with_nothing_else_reported()
    {
        StubServer stub = new(request => request.Path == "/v1/models" ? StubServer.Json(Documented.LlamaModelsLoading) : StubServer.NotFound());
        using LocalProgram program = new(LocalEndpoint.Parse("http://127.0.0.1:8080"), stub);

        InstalledModel model = Assert.Single((await program.ListAsync(CancellationToken.None)).Models);

        Assert.Equal("qwen3-4b — size not reported, parameters not reported, quantization not reported, license not reported", model.Describe(Sixteen));
    }

    [Fact]
    public async Task A_model_ollama_will_not_show_keeps_what_the_list_said()
    {
        StubServer stub = new(request => request.Path == "/api/tags"
            ? StubServer.Json(Documented.OllamaTags)
            : StubServer.Json(Documented.OllamaError("model not found"), HttpStatusCode.NotFound));
        using LocalProgram program = new(LocalEndpoint.Parse("http://127.0.0.1:11434"), stub);

        InstalledModel local = (await program.ListAsync(CancellationToken.None)).Models[0];

        Assert.Null(local.License);
        Assert.Equal("Q4_K_M", local.QuantizationLevel);
        Assert.Equal("qwen3:4b-q4_K_M — 2.6 GB, 4.0B parameters, Q4_K_M, license not reported", local.Describe(Sixteen));
    }

    [Fact]
    public async Task Api_show_fills_what_the_list_left_out_and_odd_fields_are_ignored()
    {
        string tags = """{"models": [{"name": "m:1", "details": "odd"}, {"model": "no name"}, 7]}""";
        string show = """{"license": 5, "details": {"quantization_level": "Q8_0", "parameter_size": "8.2B"}, "model_info": {"general.architecture": "qwen3"}, "capabilities": ["completion", 3]}""";
        StubServer stub = new(request => request.Path == "/api/tags" ? StubServer.Json(tags) : StubServer.Json(show));
        using LocalProgram program = new(LocalEndpoint.Parse("http://127.0.0.1:11434"), stub);

        InstalledModel model = Assert.Single((await program.ListAsync(CancellationToken.None)).Models);

        Assert.Equal("m:1", model.Name);
        Assert.Null(model.SizeBytes);
        Assert.Null(model.License);
        Assert.Null(model.ContextLength);
        Assert.Equal("Q8_0", model.QuantizationLevel);
        Assert.Equal("8.2B", model.ParameterSize);
        Assert.Equal(["completion"], model.Capabilities);
    }

    [Fact]
    public async Task A_show_reply_that_is_not_an_object_changes_nothing()
    {
        StubServer stub = new(request => request.Path == "/api/tags" ? StubServer.Json(Documented.OllamaTags) : StubServer.Json("[1]"));
        using LocalProgram program = new(LocalEndpoint.Parse("http://127.0.0.1:11434"), stub);

        InstalledModel local = (await program.ListAsync(CancellationToken.None)).Models[0];

        Assert.Null(local.License);
        Assert.Empty(local.Capabilities);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task A_program_that_is_not_running_or_too_slow_is_a_listing_refusal_never_a_throw()
    {
        using LocalProgram down = new(LocalEndpoint.Parse("http://127.0.0.1:11434"), new StubServer(_ => throw new HttpRequestException("Connection refused")));
        ServerListing refused = await down.ListAsync(CancellationToken.None);
        Assert.Null(refused.Dialect);
        Assert.Empty(refused.Models);
        Assert.Equal("Nothing answered at http://127.0.0.1:11434 (Connection refused). Start Ollama or llama-server, then try again.", refused.Refusal);

        StubServer slow = new(async (_, cancel) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancel);
            return StubServer.NotFound();
        });
        using LocalProgram late = new(LocalEndpoint.Parse("http://127.0.0.1:11434"), slow, TimeSpan.FromMilliseconds(40));
        Assert.Equal("No answer in 0.04 s from http://127.0.0.1:11434.", (await late.ListAsync(CancellationToken.None)).Refusal);

        using CancellationTokenSource closed = new();
        closed.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => late.ListAsync(closed.Token));
    }

    [Fact]
    public void Find_takes_the_exact_name_or_for_a_bare_name_its_latest()
    {
        LocalEndpoint endpoint = LocalEndpoint.Parse(LocalEndpoint.OllamaDefault);
        ServerListing listing = ServerListing.Answered(endpoint, Dialect.Ollama, [new InstalledModel("qwen3:latest"), new InstalledModel("qwen3:8b")]);

        Assert.Equal("qwen3:8b", listing.Find("qwen3:8b")?.Name);
        Assert.Equal("qwen3:latest", listing.Find("qwen3")?.Name);
        Assert.Null(listing.Find("qwen3:4b"));
        Assert.Null(ServerListing.Refused(endpoint, "no").Find("qwen3"));
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public void A_model_too_big_by_napkins_rule_is_marked_and_still_listed()
    {
        InstalledModel big = new("qwen3:8b") { SizeBytes = 5_200_000_000, ParameterSize = "8.2B", QuantizationLevel = "Q4_K_M" };

        Assert.Equal("qwen3:8b — 5.2 GB, 8.2B parameters, Q4_K_M, license not reported", big.Describe(Sixteen));
        Assert.Equal("qwen3:8b — 5.2 GB, 8.2B parameters, Q4_K_M, license not reported — too big for this machine by napkin's rule", big.Describe(6_000_000_000));
        Assert.Equal("qwen3:8b — 5.2 GB, 8.2B parameters, Q4_K_M, license not reported", big.Describe());
        Assert.Equal("x — runs at another host, not on this machine; napkin will not use it", new InstalledModel("x") { RemoteModel = "x" }.Describe());
    }

    [Theory]
    [InlineData(2_620_788_019, "2.6 GB")]
    [InlineData(1_000_000_000, "1.0 GB")]
    [InlineData(999_999_999, "1000 MB")]
    [InlineData(850_400_000, "850 MB")]
    [InlineData(17_179_869_184, "17.2 GB")]
    public void A_size_is_said_in_decimal_units_as_ollamas_pages_say_it(long bytes, string said) =>
        Assert.Equal(said, Guidance.Size(bytes));

    [Fact]
    [Trait("Feature", "AST-006")]
    public void Napkins_memory_rule_is_the_file_plus_about_a_gigabyte_and_says_it_is_napkins()
    {
        Assert.False(Guidance.TooBig(2_600_000_000, 3_600_000_000));
        Assert.True(Guidance.TooBig(2_600_000_001, 3_600_000_000));
        Assert.Equal(
            "This machine has 17.2 GB of memory. napkin's own rule, not Ollama's: a model's file plus about 1 GB should fit in it.",
            Guidance.MemoryLine(17_179_869_184));
    }

    [Fact]
    public void A_long_license_is_folded_and_cut_and_a_short_one_is_whole()
    {
        Assert.Equal("MIT License", InstalledModel.ShortLicense("  MIT\n  License \n"));
        string cut = InstalledModel.ShortLicense(new string('a', 100));
        Assert.Equal(InstalledModel.LicenseShown + 1, cut.Length);
        Assert.EndsWith("…", cut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public void The_dialog_says_how_to_get_a_model_and_that_napkin_downloads_nothing()
    {
        Assert.Equal(5, Guidance.InstallLines.Length);
        Assert.Contains("https://ollama.com/download", Guidance.InstallLines[0], StringComparison.Ordinal);
        Assert.Contains("ollama pull qwen3:4b-q4_K_M — Qwen3-4B, Q4_K_M, 2.6 GB, Apache-2.0 (https://huggingface.co/Qwen/Qwen3-4B)", Guidance.InstallLines[1], StringComparison.Ordinal);
        Assert.Contains("ollama pull qwen3:8b — Qwen3-8B, Q4_K_M, 5.2 GB, Apache-2.0", Guidance.InstallLines[2], StringComparison.Ordinal);
        Assert.Contains("ollama pull phi4-mini — Phi-4-mini, Q4_K_M, 2.5 GB, MIT", Guidance.InstallLines[3], StringComparison.Ordinal);
        Assert.EndsWith("napkin downloads nothing itself.", Guidance.InstallLines[4], StringComparison.Ordinal);
        Assert.Contains("OLLAMA_NO_CLOUD=1", Guidance.CloudSentence, StringComparison.Ordinal);
        Assert.StartsWith("Ollama can also run models in its own cloud.", Guidance.CloudSentence, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:11434", Guidance.AddressHint, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:8080", Guidance.AddressHint, StringComparison.Ordinal);
    }

    [Fact]
    public void A_program_needs_an_address_and_a_timeout_longer_than_zero()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalProgram(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalProgram(LocalEndpoint.Parse(LocalEndpoint.OllamaDefault), StubServer.Ollama(), TimeSpan.FromSeconds(-1)));

        using LocalProgram program = new(LocalEndpoint.Parse(LocalEndpoint.OllamaDefault));
        Assert.Equal(LocalServerModel.DefaultTimeout, program.Timeout);
    }
}

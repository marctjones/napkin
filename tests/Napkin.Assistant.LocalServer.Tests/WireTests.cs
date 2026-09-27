using System.Net;
using System.Text.Json;

namespace Napkin.Assistant.LocalServer.Tests;

/// <summary>
/// The readers are strict about shape and lenient about nothing else: a field of the wrong kind is
/// "not reported", never coerced, and a body that is not what the docs describe is not that
/// program's answer.
/// </summary>
public class WireTests
{
    private static JsonElement? Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Theory]
    [InlineData("""{"error": "model not found"}""", "model not found")]
    [InlineData("""{"error": {"code": 401, "message": "Invalid API Key", "type": "authentication_error"}}""", "Invalid API Key")]
    [InlineData("""{"error": {"code": 500}}""", null)]
    [InlineData("""{"error": {"message": 5}}""", null)]
    [InlineData("""{"error": 5}""", null)]
    [InlineData("""{"detail": "x"}""", null)]
    [InlineData("""["error"]""", null)]
    public void The_programs_own_error_message_is_read_from_either_documented_shape(string body, string? error) =>
        Assert.Equal(error, new WireReply(HttpStatusCode.BadRequest, body, Parse(body)).Error);

    [Fact]
    public void An_error_with_no_message_is_said_by_its_status()
    {
        Assert.Equal("HTTP 404 NotFound", new WireReply(HttpStatusCode.NotFound, "404 page not found", null).Complaint);
        Assert.Equal("HTTP 400 BadRequest", new WireReply(HttpStatusCode.BadRequest, """{"error": ""}""", Parse("""{"error": ""}""")).Complaint);
    }

    [Theory]
    [InlineData("""{"models": "none"}""")]
    [InlineData("""{"data": []}""")]
    [InlineData("""[]""")]
    public void A_reply_without_a_models_array_is_not_ollama(string body) => Assert.Null(Wire.OllamaModels(Parse(body)));

    [Theory]
    [InlineData("""{"data": {"id": "m"}}""")]
    [InlineData("""{"models": []}""")]
    [InlineData("\"list\"")]
    public void A_reply_without_a_data_array_is_not_an_openai_models_list(string body) => Assert.Null(Wire.OpenAiModels(Parse(body)));

    [Fact]
    public void Missing_or_wrongly_typed_fields_are_not_reported_rather_than_guessed()
    {
        InstalledModel tagged = Assert.Single(Wire.OllamaModels(Parse("""{"models": [{"name": "m:1", "size": "big", "details": {"parameter_size": 4, "quantization_level": null}}]}"""))!.Value);
        Assert.Null(tagged.SizeBytes);
        Assert.Null(tagged.ParameterSize);
        Assert.Null(tagged.QuantizationLevel);
        Assert.Null(tagged.Format);

        InstalledModel served = Assert.Single(Wire.OpenAiModels(Parse("""{"data": [{"id": "m"}, {"id": 3}, "x", {"id": "n", "meta": {"size": 1.5, "n_params": "many"}}]}"""))!.Value.Skip(1));
        Assert.Equal("n", served.Name);
        Assert.Null(served.SizeBytes);
        Assert.Null(served.ParameterSize);
        Assert.Null(served.ContextLength);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"details": "x", "model_info": "y", "capabilities": "z"}""")]
    [InlineData("""{"model_info": {"general.architecture": 7, "qwen3.context_length": 40960}}""")]
    public void A_show_reply_that_says_little_adds_little(string body)
    {
        InstalledModel before = new("m:1") { QuantizationLevel = "Q4_K_M", License = "MIT", ContextLength = 8192, Capabilities = ["completion"] };

        InstalledModel after = Wire.WithShow(before, Parse(body));

        Assert.Equal("Q4_K_M", after.QuantizationLevel);
        Assert.Equal("MIT", after.License);
        Assert.Equal(8192, after.ContextLength);
        Assert.Equal(["completion"], after.Capabilities);
        Assert.Same(before, Wire.WithShow(before, null));
    }

    [Fact]
    public void The_show_request_names_the_model_by_the_documented_field()
    {
        Assert.Equal("""{"model":"qwen3:4b-q4_K_M"}""", System.Text.Encoding.UTF8.GetString(Wire.OllamaShow("qwen3:4b-q4_K_M")));
    }
}

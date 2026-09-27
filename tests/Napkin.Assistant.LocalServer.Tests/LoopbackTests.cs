using System.Net;

using Napkin.Modules.Assistant;

namespace Napkin.Assistant.LocalServer.Tests;

/// <summary>
/// Loopback only (docs/design/llm-assistant.md §6.1, §11.2): an address that is not this machine is
/// refused before anything is built on it, the handler napkin builds follows no redirect and no
/// proxy, and a connection resolves its host and keeps only loopback addresses.
/// </summary>
public class LoopbackTests
{
    [Theory]
    [Trait("Feature", "AST-006")]
    [InlineData("http://127.0.0.1:11434", "127.0.0.1", 11434, "127.0.0.1:11434")]
    [InlineData("http://[::1]:8080", "::1", 8080, "[::1]:8080")]
    [InlineData("http://localhost:11434", "localhost", 11434, "localhost:11434")]
    [InlineData("  http://LOCALHOST:11434/  ", "localhost", 11434, "localhost:11434")]
    [InlineData("127.0.0.1:8080", "127.0.0.1", 8080, "127.0.0.1:8080")]
    [InlineData("[::1]:11434", "::1", 11434, "[::1]:11434")]
    [InlineData("http://127.0.0.2:11434", "127.0.0.2", 11434, "127.0.0.2:11434")]
    [InlineData("http://127.0.0.1", "127.0.0.1", 80, "127.0.0.1:80")]
    public void An_address_on_this_machine_is_accepted(string typed, string host, int port, string hostAndPort)
    {
        Assert.True(LocalEndpoint.TryParse(typed, out LocalEndpoint? endpoint, out string? refusal));
        Assert.Null(refusal);
        Assert.Equal(host, endpoint.Host);
        Assert.Equal(port, endpoint.Port);
        Assert.Equal(hostAndPort, endpoint.HostAndPort);
        Assert.Equal("http://" + hostAndPort, endpoint.ToString());
        Assert.Equal(new Uri("http://" + hostAndPort + "/"), endpoint.BaseUri);
    }

    [Theory]
    [Trait("Feature", "AST-006")]
    [InlineData("http://192.168.1.5:11434", "192.168.1.5 is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.")]
    [InlineData("https://example.com", "example.com is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.")]
    [InlineData("http://0.0.0.0:11434", "0.0.0.0 is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.")]
    [InlineData("http://localhost.example.com:11434", "localhost.example.com is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.")]
    [InlineData("http://127.0.0.1.nip.io:11434", "127.0.0.1.nip.io is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.")]
    [InlineData("http://[2001:db8::1]:11434", "2001:db8::1 is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.")]
    [InlineData("ollama.com", "ollama.com is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.")]
    [InlineData("https://127.0.0.1:11434", "napkin talks to the program on this machine over plain http://, not https://.")]
    [InlineData("ftp://localhost:21", "napkin talks to the program on this machine over plain http://, not ftp://.")]
    [InlineData("http://127.0.0.1:8080/v1", "Type only the address and port, like http://127.0.0.1:11434: no user name, path or query.")]
    [InlineData("http://me:secret@127.0.0.1:11434", "Type only the address and port, like http://127.0.0.1:11434: no user name, path or query.")]
    [InlineData("http://127.0.0.1:11434/?next=example.com", "Type only the address and port, like http://127.0.0.1:11434: no user name, path or query.")]
    [InlineData("http://127.0.0.1:11434/#x", "Type only the address and port, like http://127.0.0.1:11434: no user name, path or query.")]
    [InlineData("", "Type the address of the program on this machine, like http://127.0.0.1:11434.")]
    [InlineData("   ", "Type the address of the program on this machine, like http://127.0.0.1:11434.")]
    [InlineData("http://", "napkin cannot read \"http://\" as an address; type one like http://127.0.0.1:11434.")]
    [InlineData("http://exa mple:1", "napkin cannot read \"http://exa mple:1\" as an address; type one like http://127.0.0.1:11434.")]
    public void Any_other_address_is_refused_with_a_plain_reason(string typed, string reason)
    {
        Assert.False(LocalEndpoint.TryParse(typed, out LocalEndpoint? endpoint, out string? refusal));
        Assert.Null(endpoint);
        Assert.Equal(reason, refusal);
    }

    [Fact]
    public void A_null_address_is_refused_like_an_empty_one()
    {
        Assert.False(LocalEndpoint.TryParse(null, out _, out string? refusal));
        Assert.Equal("Type the address of the program on this machine, like http://127.0.0.1:11434.", refusal);
    }

    [Theory]
    [Trait("Feature", "AST-006")]
    [InlineData("http://192.168.1.5:11434", "192.168.1.5 is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.")]
    [InlineData("https://example.com", "example.com is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.")]
    public void The_model_refuses_another_machine_at_construction_and_sends_nothing(string typed, string reason)
    {
        StubServer stub = StubServer.Ollama();

        ArgumentException refused = Assert.Throws<ArgumentException>(() => new LocalServerModel(typed, "qwen3:4b-q4_K_M", handler: stub));

        Assert.StartsWith(reason, refused.Message, StringComparison.Ordinal);
        Assert.Empty(stub.Requests);
    }

    [Theory]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("http://[::1]:8080")]
    [InlineData("http://localhost:11434")]
    public void The_model_is_built_on_an_address_on_this_machine(string typed)
    {
        using LocalServerModel model = new(typed, "qwen3:4b-q4_K_M", handler: StubServer.Ollama());
        Assert.Equal(LocalEndpoint.Parse(typed), model.Endpoint);
    }

    [Fact]
    public void Parse_throws_the_refusal_as_its_message()
    {
        ArgumentException refused = Assert.Throws<ArgumentException>(() => LocalEndpoint.Parse("http://10.0.0.1:11434"));
        Assert.StartsWith("10.0.0.1 is not this machine.", refused.Message, StringComparison.Ordinal);
        Assert.Equal(LocalEndpoint.Parse("http://127.0.0.1:11434"), LocalEndpoint.Parse(LocalEndpoint.OllamaDefault));
        Assert.Equal(8080, LocalEndpoint.Parse(LocalEndpoint.LlamaServerDefault).Port);
    }

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("LocalHost", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.255.255.254", true)]
    [InlineData("::1", true)]
    [InlineData("localhost.", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("::", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("example.com", false)]
    public void A_host_is_this_machine_only_when_it_is_localhost_or_a_loopback_address(string host, bool loopback) =>
        Assert.Equal(loopback, LocalEndpoint.IsLoopbackHost(host));

    [Fact]
    [Trait("Feature", "AST-006")]
    public void Napkins_own_handler_follows_no_redirect_uses_no_proxy_and_connects_through_the_loopback_check()
    {
        using SocketsHttpHandler handler = LoopbackHttp.CreateHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.NotNull(handler.ConnectCallback);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public async Task A_name_is_resolved_and_only_its_loopback_addresses_are_connected_to()
    {
        IPAddress[]? opened = null;
        using Stream stream = await LoopbackHttp.ConnectAsync(
            new DnsEndPoint("localhost", 11434),
            (host, _) => Task.FromResult<IPAddress[]>([IPAddress.Parse("192.168.1.5"), IPAddress.IPv6Loopback, IPAddress.Loopback]),
            (addresses, port, _) =>
            {
                opened = addresses;
                Assert.Equal(11434, port);
                return ValueTask.FromResult<Stream>(new MemoryStream());
            },
            CancellationToken.None);

        Assert.Equal([IPAddress.IPv6Loopback, IPAddress.Loopback], opened);
    }

    [Theory]
    [Trait("Feature", "AST-006")]
    [InlineData("localhost")]
    [InlineData("my-laptop.local")]
    public async Task A_name_that_resolves_to_no_loopback_address_is_refused_before_anything_is_opened(string host)
    {
        bool opened = false;
        HttpRequestException refused = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await LoopbackHttp.ConnectAsync(
                new DnsEndPoint(host, 11434),
                (_, _) => Task.FromResult<IPAddress[]>([IPAddress.Parse("192.168.1.5")]),
                (_, _, _) =>
                {
                    opened = true;
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                },
                CancellationToken.None));

        Assert.False(opened);
        Assert.Equal($"{host} does not resolve to this machine, so napkin did not connect to it.", refused.Message);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("[::1]", true)]
    [InlineData("::1", true)]
    [InlineData("10.1.2.3", false)]
    public async Task An_address_is_taken_as_it_is_without_resolving(string host, bool loopback)
    {
        bool resolved = false;
        bool opened = false;
        Task connect = LoopbackHttp.ConnectAsync(
            new DnsEndPoint(host, 8080),
            (_, _) =>
            {
                resolved = true;
                return Task.FromResult<IPAddress[]>([IPAddress.Loopback]);
            },
            (_, _, _) =>
            {
                opened = true;
                return ValueTask.FromResult<Stream>(new MemoryStream());
            },
            CancellationToken.None).AsTask();

        if (loopback)
        {
            await connect;
        }
        else
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => connect);
        }

        Assert.False(resolved);
        Assert.Equal(loopback, opened);
    }

    [Fact]
    public void The_whereabouts_line_names_the_model_and_the_address_and_says_nothing_leaves()
    {
        using LocalServerModel model = new("http://127.0.0.1:11434", "qwen3:4b-q4_K_M", handler: StubServer.Ollama());
        Assert.Equal("Local: qwen3:4b-q4_K_M at 127.0.0.1:11434 — nothing leaves this machine.", model.Whereabouts);
        Assert.IsAssignableFrom<IAssistantModel>(model);
    }
}

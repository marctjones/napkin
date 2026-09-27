using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Napkin.Assistant.LocalServer;

/// <summary>
/// The address of a program on this machine that runs a model — loopback only, by construction
/// (docs/design/llm-assistant.md §6.1): an address whose host is not <c>127.0.0.1</c> (or another
/// IPv4 loopback address), <c>::1</c> or <c>localhost</c> cannot be made into one, so nothing built
/// on it can be pointed at another machine. "A URL field that took anything would be a network
/// path with a friendly name."
/// </summary>
/// <remarks>
/// This is the first of three layers. The second is the connection itself
/// (<see cref="LoopbackHttp"/>): it connects only to a loopback address, whatever a name resolves
/// to, and follows no redirect and no proxy. The third is <see cref="LocalServerModel"/> refusing a
/// model the program itself reports as running somewhere else.
/// </remarks>
public sealed record LocalEndpoint
{
    /// <summary>
    /// Ollama's address as installed: "Ollama binds 127.0.0.1 port 11434 by default"
    /// (docs.ollama.com/faq, "How can I expose Ollama on my network?", read 2026-09-27).
    /// </summary>
    public const string OllamaDefault = "http://127.0.0.1:11434";

    /// <summary>
    /// llama.cpp's server as started: <c>--host</c> "(default: 127.0.0.1)", <c>--port</c> "(default:
    /// 8080)" (github.com/ggml-org/llama.cpp tools/server/README.md, "Server-specific params", read
    /// 2026-09-27).
    /// </summary>
    public const string LlamaServerDefault = "http://127.0.0.1:8080";

    private LocalEndpoint(string host, int port)
    {
        Host = host;
        Port = port;
    }

    /// <summary>The host as the address names it, without brackets: <c>127.0.0.1</c>, <c>::1</c> or <c>localhost</c>.</summary>
    public string Host { get; }

    /// <summary>The port.</summary>
    public int Port { get; }

    /// <summary>The host and port as the note's last line says them: <c>127.0.0.1:11434</c>, <c>[::1]:8080</c>.</summary>
    public string HostAndPort => Host.Contains(':', StringComparison.Ordinal) ? $"[{Host}]:{Port}" : $"{Host}:{Port}";

    /// <summary>The address every request is made relative to.</summary>
    public Uri BaseUri => new($"http://{HostAndPort}/");

    /// <summary>The address as a person types it: <c>http://127.0.0.1:11434</c>.</summary>
    public override string ToString() => $"http://{HostAndPort}";

    /// <summary>The address in <paramref name="text"/>, or an <see cref="ArgumentException"/> whose message is the refusal.</summary>
    /// <param name="text">What the person typed, with or without <c>http://</c>.</param>
    public static LocalEndpoint Parse(string? text) =>
        TryParse(text, out LocalEndpoint? endpoint, out string? refusal) ? endpoint : throw new ArgumentException(refusal, nameof(text));

    /// <summary>Reads an address, refusing anything that is not a plain <c>http://</c> address of this machine.</summary>
    /// <param name="text">What the person typed, with or without <c>http://</c>.</param>
    /// <param name="endpoint">The address, when it is one.</param>
    /// <param name="refusal">Why not, in a sentence the dialog shows as it is.</param>
    public static bool TryParse(string? text, [NotNullWhen(true)] out LocalEndpoint? endpoint, [NotNullWhen(false)] out string? refusal)
    {
        endpoint = null;
        string typed = text?.Trim() ?? string.Empty;
        if (typed.Length == 0)
        {
            refusal = $"Type the address of the program on this machine, like {OllamaDefault}.";
            return false;
        }

        string withScheme = typed.Contains("://", StringComparison.Ordinal) ? typed : "http://" + typed;
        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out Uri? uri)
            || uri.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6 or UriHostNameType.Dns))
        {
            refusal = $"napkin cannot read \"{typed}\" as an address; type one like {OllamaDefault}.";
            return false;
        }

        string host = uri.Host.StartsWith('[') ? uri.Host[1..^1] : uri.Host;
        if (!IsLoopbackHost(host))
        {
            refusal = $"{host} is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp)
        {
            refusal = $"napkin talks to the program on this machine over plain http://, not {uri.Scheme}://.";
            return false;
        }

        if (uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            refusal = $"Type only the address and port, like {OllamaDefault}: no user name, path or query.";
            return false;
        }

        endpoint = new LocalEndpoint(host, uri.Port);
        refusal = null;
        return true;
    }

    /// <summary>Whether a host, as an address names it (no brackets), is this machine: exactly <c>localhost</c>, or a loopback IP address.</summary>
    public static bool IsLoopbackHost(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address));
    }
}

using System.Net;
using System.Net.Sockets;

namespace Napkin.Assistant.LocalServer;

/// <summary>
/// The only HTTP handler napkin's local runtime builds for itself (docs/design/llm-assistant.md
/// §6.1): the second of the three loopback layers <see cref="LocalEndpoint"/> describes. Whatever
/// an address says, the connection is made to a loopback address or not at all.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><b>No redirect is followed.</b> A program on this machine answering "302, go
/// to some other host" would otherwise carry the question — the design, in words — off the machine
/// through an address that said 127.0.0.1.</description></item>
/// <item><description><b>No proxy is used.</b> A system or environment proxy would receive every
/// request, loopback address or not.</description></item>
/// <item><description><b>Every connection resolves its host and keeps only loopback
/// addresses.</b> <c>localhost</c> is a name; this is what makes "localhost, resolving to
/// loopback" true rather than assumed. A name that resolves to nothing on this machine is refused
/// before a socket exists.</description></item>
/// </list>
/// </remarks>
public static class LoopbackHttp
{
    /// <summary>A handler that connects only to this machine, follows no redirect and uses no proxy.</summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectCallback = (context, cancel) => ConnectAsync(context.DnsEndPoint, ResolveAsync, OpenAsync, cancel),
    };

    /// <summary>What a connection to a host that is not this machine is refused with.</summary>
    public static string NotThisMachine(string host) =>
        $"{host} does not resolve to this machine, so napkin did not connect to it.";

    /// <summary>
    /// Resolves <paramref name="endpoint"/>'s host (an IP address is taken as it is), keeps only the
    /// loopback addresses, and connects to those; with none left, refuses before anything is opened.
    /// </summary>
    /// <param name="endpoint">The host and port the request is for.</param>
    /// <param name="resolve">How a name becomes addresses.</param>
    /// <param name="open">How a connection is opened to the loopback addresses that are left.</param>
    /// <param name="cancel">Cancels the resolution and the connection.</param>
    internal static async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endpoint,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        Func<IPAddress[], int, CancellationToken, ValueTask<Stream>> open,
        CancellationToken cancel)
    {
        string host = endpoint.Host.StartsWith('[') ? endpoint.Host[1..^1] : endpoint.Host;
        IPAddress[] addresses = IPAddress.TryParse(host, out IPAddress? literal)
            ? [literal]
            : await resolve(host, cancel).ConfigureAwait(false);
        IPAddress[] loopback = [.. addresses.Where(IPAddress.IsLoopback)];
        if (loopback.Length == 0)
        {
            throw new HttpRequestException(NotThisMachine(host));
        }

        return await open(loopback, endpoint.Port, cancel).ConfigureAwait(false);
    }

    private static Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancel) => Dns.GetHostAddressesAsync(host, cancel);

    private static async ValueTask<Stream> OpenAsync(IPAddress[] addresses, int port, CancellationToken cancel)
    {
        Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, port, cancel).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Napkin.Assistant.Mlx.Tests;

/// <summary>
/// A Hugging Face Hub that never opens a socket (docs/design/mlx-runtime.md §7.2): an
/// <see cref="HttpMessageHandler"/> that records every request and answers from a table of small
/// fake files whose hashes the test computes. It answers as the Hub did on 2026-09-27 for the
/// shapes that matter — a large file <c>302</c> to <c>us.aws.cdn.hf.co</c>, a small one
/// <c>307</c> to a relative <c>/api/resolve-cache/…</c>, <c>Range</c> honoured with <c>206</c> and
/// <c>Content-Range</c> — unless a test gives it another answer.
/// </summary>
internal sealed class HubStub : HttpMessageHandler
{
    public const string Cdn = "https://us.aws.cdn.hf.co";

    private readonly List<(Uri Url, RangeHeaderValue? Range)> _requests = [];

    public HubStub(CatalogModel model, IReadOnlyDictionary<string, byte[]> files)
    {
        Model = model;
        Files = new Dictionary<string, byte[]>(files);
    }

    public CatalogModel Model { get; }

    /// <summary>What each file's bytes are, as served; a test may change one.</summary>
    public Dictionary<string, byte[]> Files { get; }

    /// <summary>Files served through a redirect to the CDN, as the Hub serves LFS files.</summary>
    public HashSet<string> ViaCdn { get; } = ["model.safetensors"];

    /// <summary>Files served through a relative redirect on huggingface.co, as the Hub serves small files.</summary>
    public HashSet<string> ViaResolveCache { get; } = ["config.json"];

    /// <summary>When set, answers every request in place of the table.</summary>
    public Func<HttpRequestMessage, HttpResponseMessage>? Answer { get; set; }

    /// <summary>Whether Range is ignored (every answer a whole 200).</summary>
    public bool IgnoreRange { get; set; }

    public IReadOnlyList<(Uri Url, RangeHeaderValue? Range)> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    public IReadOnlyList<Uri> Urls => [.. Requests.Select(request => request.Url)];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_requests)
        {
            _requests.Add((request.RequestUri!, request.Headers.Range));
        }

        return Task.FromResult(Answer?.Invoke(request) ?? FromTable(request));
    }

    private HttpResponseMessage FromTable(HttpRequestMessage request)
    {
        Uri url = request.RequestUri!;
        string prefix = $"/{Model.Repo}/resolve/{Model.Commit}/";
        if (url.Host == "huggingface.co" && url.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            string name = url.AbsolutePath[prefix.Length..];
            if (ViaCdn.Contains(name))
            {
                return Redirect(HttpStatusCode.Found, $"{Cdn}/repos/aa/bb/{name}?X-Amz-Signature=made-up");
            }

            if (ViaResolveCache.Contains(name))
            {
                return Redirect(HttpStatusCode.TemporaryRedirect, $"/api/resolve-cache/models/{Model.Repo}/{Model.Commit}/{name}?etag=made-up");
            }

            return Serve(name, request);
        }

        if (url.GetLeftPart(UriPartial.Authority) == Cdn)
        {
            return Serve(Path.GetFileName(url.AbsolutePath), request);
        }

        if (url.Host == "huggingface.co" && url.AbsolutePath.StartsWith("/api/resolve-cache/", StringComparison.Ordinal))
        {
            return Serve(Path.GetFileName(url.AbsolutePath), request);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private HttpResponseMessage Serve(string name, HttpRequestMessage request)
    {
        byte[] bytes = Files[name];
        long from = !IgnoreRange && request.Headers.Range?.Ranges.Single().From is { } start ? start : 0;
        return from == 0 ? Whole(bytes) : Partial(bytes, from);
    }

    public static HttpResponseMessage Whole(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    public static HttpResponseMessage Partial(byte[] bytes, long from)
    {
        HttpResponseMessage response = new(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes[(int)from..]) };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, bytes.Length - 1, bytes.Length);
        return response;
    }

    public static HttpResponseMessage Redirect(HttpStatusCode status, string? location)
    {
        HttpResponseMessage response = new(status);
        if (location is not null)
        {
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        }

        return response;
    }

    /// <summary>A body with no declared length, as a chunked answer has.</summary>
    public static HttpResponseMessage Unsized(Stream body) => new(HttpStatusCode.OK) { Content = new StreamContent(body) };
}

/// <summary>A small model to download: the four files the bridge needs, one of them several buffers long.</summary>
internal static class SmallModel
{
    public static readonly byte[] Config = Encoding.UTF8.GetBytes(TempModel.QwenConfig);
    public static readonly byte[] Tokenizer = Encoding.UTF8.GetBytes("""{"model": {"type": "BPE"}}""");
    public static readonly byte[] TokenizerConfig = Encoding.UTF8.GetBytes(TempModel.TokenizerConfig);
    public static readonly byte[] Weights = [.. Enumerable.Range(0, 200_000).Select(i => (byte)(i * 31 % 251))];

    public static Dictionary<string, byte[]> Files() => new()
    {
        ["model.safetensors"] = Weights,
        ["tokenizer.json"] = Tokenizer,
        ["tokenizer_config.json"] = TokenizerConfig,
        ["config.json"] = Config,
    };

    /// <summary>The real default's repository and commit, with these four files and their hashes.</summary>
    public static CatalogModel Catalog() => Catalog(Files());

    public static CatalogModel Catalog(IReadOnlyDictionary<string, byte[]> files) => new(
        ModelCatalog.Default.Repo,
        ModelCatalog.Default.Commit,
        ModelCatalog.Default.Licence,
        ModelCatalog.Default.LicenceLink,
        files.Select(pair => new CatalogFile(pair.Key, pair.Value.Length, Sha(pair.Value))));

    public static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

/// <summary>A stream that cannot seek, so its content has no declared length.</summary>
internal sealed class Unseekable(byte[] bytes) : Stream
{
    private readonly MemoryStream _inner = new(bytes);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>A body that gives its first bytes, then either waits until the read is cancelled or throws.</summary>
internal sealed class Stalling(byte[] first, Exception? failure = null) : Stream
{
    private bool _gave;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_gave)
        {
            _gave = true;
            first.CopyTo(buffer);
            return first.Length;
        }

        if (failure is not null)
        {
            throw failure;
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>Records every report as it is made, on the reporting thread (not a <see cref="Progress{T}"/>, which posts).</summary>
internal sealed class Reports : IProgress<DownloadProgress>
{
    private readonly List<DownloadProgress> _seen = [];

    public IReadOnlyList<DownloadProgress> Seen
    {
        get
        {
            lock (_seen)
            {
                return [.. _seen];
            }
        }
    }

    public void Report(DownloadProgress value)
    {
        lock (_seen)
        {
            _seen.Add(value);
        }
    }
}

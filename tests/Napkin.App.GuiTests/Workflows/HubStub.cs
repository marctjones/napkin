using System.Net;

using Napkin.Assistant.Mlx;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// A Hugging Face Hub that never opens a socket, for the download workflow (docs/design/mlx-runtime.md
/// §7.3): an <see cref="HttpMessageHandler"/> the window's <see cref="MainWindow.AssistantHttp"/> is set
/// to, recording every request and serving small fake files whose hashes the workflow computed and
/// put in its own catalog entry — the real catalog's 2.3 GB are never served. The large files are
/// answered as the Hub answered them on 2026-09-27: a <c>302</c> from the pinned resolve URL to
/// <c>us.aws.cdn.hf.co</c>, which serves the bytes; every other file straight from the resolve URL.
/// </summary>
public sealed class HubStub : HttpMessageHandler
{
    /// <summary>The content server the large files are redirected to.</summary>
    public const string Cdn = "https://us.aws.cdn.hf.co";

    private readonly CatalogModel _model;
    private readonly Dictionary<string, byte[]> _files;
    private readonly HashSet<string> _corrupt = [];
    private readonly List<Uri> _requests = [];

    /// <param name="model">The catalog entry whose pinned URLs are served.</param>
    /// <param name="files">Each file's bytes.</param>
    public HubStub(CatalogModel model, IReadOnlyDictionary<string, byte[]> files)
    {
        _model = model;
        _files = new Dictionary<string, byte[]>(files);
    }

    /// <summary>Files served through the CDN, as the Hub serves its LFS files.</summary>
    public HashSet<string> ViaCdn { get; } = ["model.safetensors", "tokenizer.json"];

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<Uri> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Serve <paramref name="name"/> with one byte wrong (its length unchanged), or right again.</summary>
    public void Corrupt(string name, bool corrupt = true)
    {
        lock (_corrupt)
        {
            if (corrupt)
            {
                _corrupt.Add(name);
            }
            else
            {
                _corrupt.Remove(name);
            }
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri url = request.RequestUri!;
        lock (_requests)
        {
            _requests.Add(url);
        }

        string pinned = $"/{_model.Repo}/resolve/{_model.Commit}/";
        HttpResponseMessage response;
        if (url.Host == "huggingface.co" && url.AbsolutePath.StartsWith(pinned, StringComparison.Ordinal))
        {
            string name = url.AbsolutePath[pinned.Length..];
            response = ViaCdn.Contains(name)
                ? new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri($"{Cdn}/repos/made-up/{name}?X-Amz-Signature=made-up") } }
                : Serve(name);
        }
        else if (url.GetLeftPart(UriPartial.Authority) == Cdn)
        {
            response = Serve(Path.GetFileName(url.AbsolutePath));
        }
        else
        {
            response = new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return Task.FromResult(response);
    }

    private HttpResponseMessage Serve(string name)
    {
        byte[] bytes = [.. _files[name]];
        lock (_corrupt)
        {
            if (_corrupt.Contains(name))
            {
                bytes[^1] ^= 0x20;
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    }
}

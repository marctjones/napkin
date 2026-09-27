using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Napkin.Assistant.Mlx;

/// <summary>
/// The consented download of one <see cref="CatalogModel"/> (docs/design/mlx-runtime.md §6.2,
/// issue #242): every file fetched from its pinned-commit URL on huggingface.co, redirects followed
/// by napkin's own code and only to the hosts the Hub documents, resumed from what already arrived,
/// checked against the catalog's size and SHA-256, and moved into place only when every file passed.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><b>Nothing without consent.</b> <see cref="RunAsync"/> throws before any request
/// unless it is told the person consented; the only caller that says so is the consent sheet's
/// Download button. Nothing here runs on launch, and constructing one sends nothing.</description></item>
/// <item><description><b>Pinned URLs, allow-listed redirects.</b> The first request for a file is
/// always <see cref="CatalogModel.FileUrl"/>; the handler follows no redirect itself, and napkin
/// follows one only over HTTPS, on the default port, to a host in <see cref="AllowedHosts"/> — any
/// other is refused with the host named.</description></item>
/// <item><description><b>Resume.</b> A file arrives as <c>&lt;name&gt;.part</c> in <see cref="Staging"/>;
/// a later run asks for the rest with <c>Range: bytes=&lt;length&gt;-</c> (on every hop, the content
/// server's too), appends a <c>206</c> whose <c>Content-Range</c> starts there, and starts the file
/// again on a <c>200</c>. What already arrived is hashed first, so a resumed file is checked
/// whole.</description></item>
/// <item><description><b>Checked.</b> SHA-256 is computed as bytes arrive; more bytes than the catalog's
/// size are never written; a short file is kept to resume; a file whose hash differs is deleted and
/// refused, naming it and both hashes.</description></item>
/// <item><description><b>Whole or not at all.</b> Files are assembled in <see cref="Staging"/>
/// (<c>&lt;folder&gt;.downloading</c>, which <see cref="ModelFolder"/> refuses) and the folder is renamed
/// to <see cref="Destination"/> only after the last file passed, so a folder that exists is one that
/// is whole.</description></item>
/// <item><description><b>Cancel</b> leaves what arrived for a later run and touches nothing
/// else.</description></item>
/// </list>
/// </remarks>
public sealed class ModelDownload
{
    /// <summary>The suffix of the folder a download is assembled in before it is whole.</summary>
    public const string StagingSuffix = ".downloading";

    /// <summary>The suffix of a file still arriving.</summary>
    public const string PartSuffix = ".part";

    /// <summary>The most redirects napkin follows for one file.</summary>
    public const int MaxRedirects = 5;

    /// <summary>What <see cref="RunAsync"/> throws when it is not told the person consented.</summary>
    public const string NoConsent =
        "napkin downloads a model only after the person has read the consent sheet, ticked \"Download these files from huggingface.co\" and pressed Download.";

    private const int BufferSize = 81920;

    private readonly HttpMessageHandler? _handler;

    /// <summary>A download of <paramref name="model"/> into <paramref name="modelsDirectory"/>. Nothing is sent.</summary>
    /// <param name="model">The catalog entry.</param>
    /// <param name="modelsDirectory">The folder downloaded models live in (<see cref="ModelCatalog.ModelsDirectory"/>).</param>
    /// <param name="handler">
    /// null for napkin's own handler (<see cref="CreateHandler"/>); a test's stub otherwise. napkin's
    /// redirect and host rules hold whichever handler carries the requests.
    /// </param>
    public ModelDownload(CatalogModel model, string modelsDirectory, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelsDirectory);
        Model = model;
        Destination = Path.Combine(Path.GetFullPath(modelsDirectory), model.FolderName);
        Staging = Destination + StagingSuffix;
        _handler = handler;
    }

    /// <summary>
    /// The hosts a download may be redirected to: the Hub's own list of hostnames that downloads
    /// "follow HTTP redirects from huggingface.co to" — <c>huggingface.co</c>, the Xet hosts, the CDN
    /// edges and the LFS CDNs — read 2026-09-27 from
    /// https://huggingface.co/docs/hub/en/models-downloading ("Downloading behind a proxy or firewall").
    /// On that day an LFS file answered <c>302</c> to <c>us.aws.cdn.hf.co</c> and any other file
    /// <c>307</c> to a relative <c>/api/resolve-cache/…</c> on huggingface.co.
    /// </summary>
    public static ImmutableArray<string> AllowedHosts { get; } =
    [
        "huggingface.co",
        "cas-server.xethub.hf.co",
        "cas-server.xethub-eu.hf.co",
        "transfer.xethub.hf.co",
        "transfer.xethub-eu.hf.co",
        "us.aws.cdn.hf.co",
        "us.gcp.cdn.hf.co",
        "cdn-lfs-us-1.hf.co",
        "cdn-lfs-eu-1.hf.co",
    ];

    /// <summary>The model being downloaded.</summary>
    public CatalogModel Model { get; }

    /// <summary>The folder the model will be in when every file has passed: <c>&lt;models&gt;/&lt;owner&gt;--&lt;name&gt;--&lt;commit12&gt;</c>.</summary>
    public string Destination { get; }

    /// <summary>The folder the files are assembled in until then: <see cref="Destination"/> + <c>.downloading</c>.</summary>
    public string Staging { get; }

    /// <summary>
    /// Whether <see cref="Destination"/> holds every catalog file at its catalog size — the dialog's
    /// <em>Downloaded</em> state. Sizes only: the hashes were checked as the files landed, and a folder
    /// only gets this name after they all passed.
    /// </summary>
    public bool IsComplete => Directory.Exists(Destination)
        && Model.Files.All(file => new FileInfo(Path.Combine(Destination, file.Name)) is { Exists: true } info && info.Length == file.Bytes);

    /// <summary>
    /// napkin's own handler: follows no redirect (napkin's code does, against <see cref="AllowedHosts"/>),
    /// decompresses nothing (the hash is of the bytes as published), and uses no proxy, so the
    /// consent sheet's "huggingface.co and its content servers … and nothing else" stays true.
    /// </summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseProxy = false,
    };

    /// <summary>Whether napkin will send a request to <paramref name="url"/>: HTTPS, the default port, a host in <see cref="AllowedHosts"/>.</summary>
    /// <param name="url">An absolute URL.</param>
    public static bool IsAllowed(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.IsAbsoluteUri
            && url.Scheme == Uri.UriSchemeHttps
            && url.IsDefaultPort
            && AllowedHosts.Contains(url.IdnHost, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Downloads every file, checks each, and moves the folder into place; or says why not. A
    /// refusal and a network or disk failure are a <see cref="DownloadResult"/> with no folder, and
    /// leave what had arrived for the next run (a file that failed its hash is deleted).
    /// </summary>
    /// <param name="consented">Whether the person ticked the consent box and pressed Download. False throws before anything is sent.</param>
    /// <param name="progress">Told the file, and the bytes so far, as they arrive.</param>
    /// <param name="cancel">Stops at the next read; what had arrived is kept.</param>
    /// <returns>The folder and "Downloaded and checked: n files.", or the refusal.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="consented"/> is false.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancel"/> was cancelled.</exception>
    public async Task<DownloadResult> RunAsync(bool consented, IProgress<DownloadProgress>? progress = null, CancellationToken cancel = default)
    {
        if (!consented)
        {
            throw new InvalidOperationException(NoConsent);
        }

        if (IsComplete)
        {
            return new DownloadResult(Destination, $"Already downloaded and checked: {Model.Files.Length} files in {Destination}.");
        }

        if (Directory.Exists(Destination) || File.Exists(Destination))
        {
            return new DownloadResult(null, $"There is already something at {Destination} that is not a whole download; move it away, or choose it with Choose….");
        }

        try
        {
            Directory.CreateDirectory(Staging);
            using HttpClient http = _handler is null
                ? new HttpClient(CreateHandler(), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan }
                : new HttpClient(_handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
            long before = 0;
            for (int index = 0; index < Model.Files.Length; index++)
            {
                await FetchAsync(http, index, before, progress, cancel).ConfigureAwait(false);
                before += Model.Files[index].Bytes;
            }

            Directory.Move(Staging, Destination);
            return new DownloadResult(Destination, $"Downloaded and checked: {Model.Files.Length} files.");
        }
        catch (Refused refused)
        {
            return new DownloadResult(null, refused.Message);
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            return new DownloadResult(null, $"The download stopped: {failure.Message} What had arrived is kept; press Download again to resume.");
        }
    }

    /// <summary>One file: kept if it already passed, else fetched (resumed where it stopped), checked, and given its name.</summary>
    private async Task FetchAsync(HttpClient http, int index, long before, IProgress<DownloadProgress>? progress, CancellationToken cancel)
    {
        CatalogFile file = Model.Files[index];
        string final = Path.Combine(Staging, file.Name);
        string part = final + PartSuffix;
        byte[] buffer = new byte[BufferSize];
        void Report(long have) => progress?.Report(new DownloadProgress(file.Name, index + 1, Model.Files.Length, have, file.Bytes, before + have, Model.TotalBytes));

        if (File.Exists(final))
        {
            // It passed on an earlier run; checked again, since it has sat on disk since.
            using IncrementalHash again = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (FileStream kept = new(final, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true))
            {
                await HashAsync(kept, again, buffer, cancel).ConfigureAwait(false);
            }

            if (Convert.ToHexStringLower(again.GetHashAndReset()) == file.Sha256)
            {
                Report(file.Bytes);
                return;
            }

            File.Delete(final);
        }

        using IncrementalHash sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await FillAsync(http, file, part, sha, buffer, Report, cancel).ConfigureAwait(false);
        }
        catch (Refused refused) when (refused.Discard)
        {
            File.Delete(part);
            throw;
        }

        File.Move(part, final);
    }

    /// <summary>
    /// Brings <paramref name="part"/> to the file's whole size from where it stopped and checks its
    /// SHA-256; a refusal that marks the bytes bad (<see cref="Refused.Discard"/>) has the caller delete them.
    /// </summary>
    private async Task FillAsync(HttpClient http, CatalogFile file, string part, IncrementalHash sha, byte[] buffer, Action<long> report, CancellationToken cancel)
    {
        await using (FileStream output = new(part, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, BufferSize, useAsync: true))
        {
            long have = output.Length;
            if (have > file.Bytes)
            {
                output.SetLength(0);
                have = 0;
            }

            await HashAsync(output, sha, buffer, cancel).ConfigureAwait(false);
            report(have);
            if (have < file.Bytes)
            {
                (HttpResponseMessage response, Uri from) = await SendAsync(http, Model.FileUrl(file), have, file.Name, cancel).ConfigureAwait(false);
                using (response)
                {
                    bool resumed = have > 0
                        && response.StatusCode == HttpStatusCode.PartialContent
                        && response.Content.Headers.ContentRange?.From == have;
                    if (!resumed)
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            throw new Refused($"{from.Host} answered {(int)response.StatusCode} ({response.ReasonPhrase}) for {file.Name}; napkin kept nothing from that answer.");
                        }

                        output.SetLength(0);
                        sha.GetHashAndReset();
                        have = 0;
                    }

                    if (response.Content.Headers.ContentLength is { } offered && offered != file.Bytes - have)
                    {
                        throw new Refused($"{from.Host} offered {ModelCatalog.Bytes(offered)} bytes of {file.Name} where napkin expected {ModelCatalog.Bytes(file.Bytes - have)}; nothing of it was written.");
                    }

                    Stream body = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
                    await using (body.ConfigureAwait(false))
                    {
                        int read;
                        while ((read = await body.ReadAsync(buffer, cancel).ConfigureAwait(false)) > 0)
                        {
                            if (have + read > file.Bytes)
                            {
                                throw new Refused($"{from.Host} sent more than the {ModelCatalog.Bytes(file.Bytes)} bytes of {file.Name} the catalog lists; napkin stopped and deleted what had arrived.", discard: true);
                            }

                            await output.WriteAsync(buffer.AsMemory(0, read), cancel).ConfigureAwait(false);
                            sha.AppendData(buffer, 0, read);
                            have += read;
                            report(have);
                        }
                    }

                    if (have < file.Bytes)
                    {
                        throw new Refused($"The download of {file.Name} stopped at {ModelCatalog.Bytes(have)} of {ModelCatalog.Bytes(file.Bytes)} bytes. What had arrived is kept; press Download again to resume.");
                    }
                }
            }
        }

        string actual = Convert.ToHexStringLower(sha.GetHashAndReset());
        if (actual != file.Sha256)
        {
            throw new Refused($"{file.Name} did not match its SHA-256: the catalog says {file.Sha256}, the bytes that arrived hash to {actual}. napkin deleted them.", discard: true);
        }
    }

    /// <summary>Feeds the rest of <paramref name="stream"/>, from its position, to <paramref name="sha"/>.</summary>
    private static async Task HashAsync(Stream stream, IncrementalHash sha, byte[] buffer, CancellationToken cancel)
    {
        int read;
        while ((read = await stream.ReadAsync(buffer, cancel).ConfigureAwait(false)) > 0)
        {
            sha.AppendData(buffer, 0, read);
        }
    }

    /// <summary>
    /// GETs <paramref name="url"/> (with <c>Range</c> from <paramref name="from"/> when it is past the
    /// start), following redirects here rather than in the handler: a relative <c>Location</c> is taken
    /// against the URL that gave it, and every hop must pass <see cref="IsAllowed"/>.
    /// </summary>
    private static async Task<(HttpResponseMessage Response, Uri Url)> SendAsync(HttpClient http, Uri url, long from, string name, CancellationToken cancel)
    {
        for (int hop = 0; ; hop++)
        {
            HttpRequestMessage request = new(HttpMethod.Get, url);
            if (from > 0)
            {
                request.Headers.Range = new RangeHeaderValue(from, null);
            }

            HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
            {
                return (response, url);
            }

            Uri? location = response.Headers.Location;
            response.Dispose();
            if (location is null)
            {
                throw new Refused($"{url.Host} redirected {name} without saying where to; napkin did not guess.");
            }

            Uri next = location.IsAbsoluteUri ? location : new Uri(url, location);
            if (!IsAllowed(next))
            {
                throw new Refused($"napkin refused a redirect of {name} to {next.GetLeftPart(UriPartial.Authority)}: it downloads only over HTTPS from huggingface.co and the content servers the Hub documents.");
            }

            if (hop == MaxRedirects)
            {
                throw new Refused($"{name} was redirected more than {MaxRedirects} times; napkin stopped following.");
            }

            url = next;
        }
    }

    /// <summary>A refusal on the way: its message is the sentence the person reads.</summary>
    /// <param name="message">The sentence.</param>
    /// <param name="discard">Whether the file's bytes are bad and are deleted, rather than kept to resume.</param>
    private sealed class Refused(string message, bool discard = false) : Exception(message)
    {
        /// <summary>Whether the file's bytes are bad and are deleted, rather than kept to resume.</summary>
        public bool Discard { get; } = discard;
    }
}

/// <summary>Where a download is: the file arriving, and the bytes so far of it and of the whole.</summary>
/// <param name="File">The file's name.</param>
/// <param name="FileNumber">Which file it is, from 1.</param>
/// <param name="FileCount">How many files there are.</param>
/// <param name="FileBytes">The file's bytes so far.</param>
/// <param name="FileTotal">The file's size.</param>
/// <param name="Bytes">Every file's bytes so far.</param>
/// <param name="Total">Every file's size together.</param>
public sealed record DownloadProgress(string File, int FileNumber, int FileCount, long FileBytes, long FileTotal, long Bytes, long Total)
{
    /// <summary>How far, from 0 to 1.</summary>
    public double Fraction => (double)Bytes / Total;

    /// <summary>"model.safetensors (1 of 8): 1.20 GB of 2.26 GB; 1.20 GB of 2.28 GB in all".</summary>
    public string Line =>
        $"{File} ({FileNumber} of {FileCount}): {ModelCatalog.Size(FileBytes)} of {ModelCatalog.Size(FileTotal)}; {ModelCatalog.Size(Bytes)} of {ModelCatalog.Size(Total)} in all";
}

/// <summary>How a download ended, other than by being cancelled.</summary>
/// <param name="Folder">The whole, checked folder; null when the download was refused or failed.</param>
/// <param name="Line">What the sheet says: "Downloaded and checked: 8 files." or why not.</param>
public sealed record DownloadResult(string? Folder, string Line)
{
    /// <summary>Whether the folder is there, whole and checked.</summary>
    public bool Succeeded => Folder is not null;
}

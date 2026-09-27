using System.Net;
using System.Text;

namespace Napkin.Assistant.Mlx.Tests;

/// <summary>
/// The consented download (docs/design/mlx-runtime.md §6.2, §7.2), through <see cref="HubStub"/>:
/// no test opens a socket, and the files are small fakes whose hashes the test computes.
/// </summary>
[Trait("Feature", "AST-008")]
public sealed class ModelDownloadTests : IDisposable
{
    private readonly string _models = Path.Combine(Path.GetTempPath(), "napkin-download-tests-" + Guid.NewGuid().ToString("N"), "models");
    private readonly CatalogModel _catalog = SmallModel.Catalog();
    private readonly HubStub _hub;

    public ModelDownloadTests() => _hub = new HubStub(_catalog, SmallModel.Files());

    private string Destination => Path.Combine(_models, "mlx-community--Qwen3-4B-4bit--4dcb3d101c2a");

    private string Staging => Destination + ".downloading";

    public void Dispose()
    {
        _hub.Dispose();
        try
        {
            Directory.Delete(Path.GetDirectoryName(_models)!, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // The test made nothing on disk.
        }
    }

    [Fact]
    public async Task NothingIsSentOrMadeWithoutConsent()
    {
        ModelDownload download = new(_catalog, _models, _hub);

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(() => download.RunAsync(consented: false));

        Assert.Equal(ModelDownload.NoConsent, refused.Message);
        Assert.Empty(_hub.Requests);
        Assert.False(Directory.Exists(_models));
    }

    [Fact]
    public async Task EveryFileComesFromItsPinnedUrlIsCheckedAndTheFolderAppearsWhole()
    {
        _hub.ViaCdn.Clear();
        _hub.ViaResolveCache.Clear();
        Reports reports = new();

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true, reports);

        Assert.True(result.Succeeded);
        Assert.Equal(Destination, result.Folder);
        Assert.Equal("Downloaded and checked: 4 files.", result.Line);
        Assert.Equal(_catalog.Files.Select(_catalog.FileUrl), _hub.Urls);
        Assert.All(_hub.Requests, request => Assert.Null(request.Range));
        Assert.All(_catalog.Files, file => Assert.Equal(SmallModel.Files()[file.Name], File.ReadAllBytes(Path.Combine(Destination, file.Name))));
        Assert.Equal(_catalog.Files.Length, Directory.GetFiles(Destination).Length);
        Assert.False(Directory.Exists(Staging));
        Assert.True(ModelFolder.TryParse(Destination, out ModelFolder? folder, out _));
        Assert.Equal("qwen3, 4-bit (group 64), licence: apache-2.0", folder.Description);
        DownloadProgress last = reports.Seen[^1];
        Assert.Equal(new DownloadProgress("config.json", 4, 4, SmallModel.Config.Length, SmallModel.Config.Length, _catalog.TotalBytes, _catalog.TotalBytes), last);
        Assert.Equal(1.0, last.Fraction);
        Assert.Contains(reports.Seen, seen => seen.File == "model.safetensors" && seen.FileBytes > 0 && seen.FileBytes < seen.FileTotal);
        Assert.Equal(reports.Seen.Select(seen => seen.Bytes).Order(), reports.Seen.Select(seen => seen.Bytes));
    }

    [Fact]
    public async Task RedirectsAreFollowedToTheHubsDocumentedHostsRelativeOnesAgainstTheirOwnUrl()
    {
        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.True(result.Succeeded, result.Line);
        Assert.Equal(
            [
                _catalog.FileUrl(_catalog.Files[0]),
                new Uri($"{HubStub.Cdn}/repos/aa/bb/model.safetensors?X-Amz-Signature=made-up"),
                _catalog.FileUrl(_catalog.Files[1]),
                _catalog.FileUrl(_catalog.Files[2]),
                _catalog.FileUrl(_catalog.Files[3]),
                new Uri($"https://huggingface.co/api/resolve-cache/models/{_catalog.Repo}/{_catalog.Commit}/config.json?etag=made-up"),
            ],
            _hub.Urls);
    }

    [Theory]
    [InlineData("https://evil.example/model.safetensors", "https://evil.example")]
    [InlineData("http://us.aws.cdn.hf.co/model.safetensors", "http://us.aws.cdn.hf.co")]
    [InlineData("https://us.aws.cdn.hf.co:8443/model.safetensors", "https://us.aws.cdn.hf.co:8443")]
    [InlineData("https://huggingface.co.evil.example/model.safetensors", "https://huggingface.co.evil.example")]
    public async Task ARedirectAnywhereElseIsRefusedNamingWhere(string location, string named)
    {
        _hub.Answer = request => request.RequestUri!.Host == "huggingface.co" ? HubStub.Redirect(HttpStatusCode.Found, location) : throw new InvalidOperationException("followed");

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.False(result.Succeeded);
        Assert.Null(result.Folder);
        Assert.Equal($"napkin refused a redirect of model.safetensors to {named}: it downloads only over HTTPS from huggingface.co and the content servers the Hub documents.", result.Line);
        Assert.Single(_hub.Requests);
        Assert.False(Directory.Exists(Destination));
    }

    [Fact]
    public async Task ASchemeRelativeRedirectToAnotherHostIsRefused()
    {
        // "//host/path" is taken against the URL that gave it (Unix) or as a UNC file URL (Windows);
        // either way it is not an allowed HTTPS host, and it is refused naming the host.
        _hub.Answer = request => request.RequestUri!.Host == "huggingface.co" ? HubStub.Redirect(HttpStatusCode.Found, "//evil.example/model.safetensors") : throw new InvalidOperationException("followed");

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.StartsWith("napkin refused a redirect of model.safetensors to ", result.Line, StringComparison.Ordinal);
        Assert.Contains("evil.example", result.Line, StringComparison.Ordinal);
        Assert.Single(_hub.Requests);
    }

    [Fact]
    public async Task ARedirectThatSaysNowhereIsRefused()
    {
        _hub.Answer = _ => HubStub.Redirect(HttpStatusCode.MovedPermanently, null);

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("huggingface.co redirected model.safetensors without saying where to; napkin did not guess.", result.Line);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.SeeOther)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task RedirectsAreFollowedAtMostFiveTimes(HttpStatusCode status)
    {
        _hub.Answer = _ => HubStub.Redirect(status, "https://us.gcp.cdn.hf.co/again");

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("model.safetensors was redirected more than 5 times; napkin stopped following.", result.Line);
        Assert.Equal(ModelDownload.MaxRedirects + 1, _hub.Requests.Count);
    }

    [Fact]
    public async Task AResumeAsksForTheRestOnEveryHopAndAppendsIt()
    {
        Directory.CreateDirectory(Staging);
        File.WriteAllBytes(Path.Combine(Staging, "model.safetensors.part"), SmallModel.Weights[..100_000]);
        Reports reports = new();

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true, reports);

        Assert.True(result.Succeeded, result.Line);
        Assert.Equal(100_000, _hub.Requests[0].Range!.Ranges.Single().From);
        Assert.Equal(100_000, _hub.Requests[1].Range!.Ranges.Single().From);
        Assert.Null(_hub.Requests[1].Range!.Ranges.Single().To);
        Assert.Equal(HubStub.Cdn, _hub.Requests[1].Url.GetLeftPart(UriPartial.Authority));
        Assert.Equal(SmallModel.Weights, File.ReadAllBytes(Path.Combine(Destination, "model.safetensors")));
        Assert.Equal(100_000, reports.Seen[0].FileBytes);
    }

    [Fact]
    public async Task AWholeAnswerToAResumeStartsTheFileAgain()
    {
        Directory.CreateDirectory(Staging);
        File.WriteAllBytes(Path.Combine(Staging, "model.safetensors.part"), new byte[100_000]);
        _hub.IgnoreRange = true;

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.True(result.Succeeded, result.Line);
        Assert.NotNull(_hub.Requests[0].Range);
        Assert.Equal(SmallModel.Weights, File.ReadAllBytes(Path.Combine(Destination, "model.safetensors")));
    }

    [Theory]
    [InlineData(0L, true)]
    [InlineData(50_000L, true)]
    [InlineData(100_000L, false)]
    public async Task APartialAnswerFromAnywhereButWhereTheFileStoppedIsRefused(long from, bool withRange)
    {
        Directory.CreateDirectory(Staging);
        string part = Path.Combine(Staging, "model.safetensors.part");
        File.WriteAllBytes(part, SmallModel.Weights[..100_000]);
        _hub.Answer = _ =>
        {
            HttpResponseMessage partial = HubStub.Partial(SmallModel.Weights, from);
            if (!withRange)
            {
                partial.Content.Headers.ContentRange = null;
            }

            return partial;
        };

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("huggingface.co answered 206 (Partial Content) for model.safetensors; napkin kept nothing from that answer.", result.Line);
        Assert.Equal(SmallModel.Weights[..100_000], File.ReadAllBytes(part));
    }

    [Fact]
    public async Task APartialAnswerToAFreshRequestIsRefused()
    {
        _hub.Answer = _ => HubStub.Partial(SmallModel.Weights, 0);

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("huggingface.co answered 206 (Partial Content) for model.safetensors; napkin kept nothing from that answer.", result.Line);
    }

    [Fact]
    public async Task AFileWhoseHashDiffersIsDeletedAndRefusedNamingItAndBothHashes()
    {
        byte[] wrong = [.. SmallModel.Tokenizer];
        wrong[5] ^= 0xFF;
        _hub.Files["tokenizer.json"] = wrong;

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal(
            $"tokenizer.json did not match its SHA-256: the catalog says {SmallModel.Sha(SmallModel.Tokenizer)}, the bytes that arrived hash to {SmallModel.Sha(wrong)}. napkin deleted them.",
            result.Line);
        Assert.False(File.Exists(Path.Combine(Staging, "tokenizer.json.part")));
        Assert.False(File.Exists(Path.Combine(Staging, "tokenizer.json")));
        Assert.True(File.Exists(Path.Combine(Staging, "model.safetensors")));
        Assert.False(Directory.Exists(Destination));
    }

    [Fact]
    public async Task APartialDownloadNeverParsesAsAModelFolder()
    {
        CatalogModel withVocab = SmallModel.Catalog(new Dictionary<string, byte[]>(SmallModel.Files()) { ["vocab.json"] = [4, 5, 6] });
        using HubStub hub = new(withVocab, new Dictionary<string, byte[]>(SmallModel.Files()) { ["vocab.json"] = [1, 2, 3] });

        DownloadResult result = await new ModelDownload(withVocab, _models, hub).RunAsync(consented: true);

        Assert.False(result.Succeeded);
        Assert.All(ModelFolder.RequiredFiles.Append("model.safetensors"), name => Assert.True(File.Exists(Path.Combine(Staging, name))));
        Assert.False(ModelFolder.TryParse(Staging, out _, out string? refusal));
        Assert.Equal($"{Staging} is a download napkin has not finished; press Download… to finish it.", refusal);
        Assert.False(ModelFolder.TryParse(Destination, out _, out _));
    }

    [Fact]
    public async Task AShortFileIsRefusedOnSizeAndKeptSoTheNextRunResumesIt()
    {
        string part = Path.Combine(Staging, "model.safetensors.part");
        _hub.Answer = _ => HubStub.Unsized(new Unseekable(SmallModel.Weights[..120_000]));

        DownloadResult first = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("The download of model.safetensors stopped at 120,000 of 200,000 bytes. What had arrived is kept; press Download again to resume.", first.Line);
        Assert.Equal(SmallModel.Weights[..120_000], File.ReadAllBytes(part));

        _hub.Answer = null;
        DownloadResult second = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.True(second.Succeeded, second.Line);
        Assert.Equal(120_000, _hub.Requests[1].Range!.Ranges.Single().From);
    }

    [Fact]
    public async Task AnAnswerOfTheWrongLengthIsRefusedBeforeAByteIsWritten()
    {
        _hub.Answer = _ => HubStub.Whole(new byte[10]);

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("huggingface.co offered 10 bytes of model.safetensors where napkin expected 200,000; nothing of it was written.", result.Line);
        Assert.Equal(0, new FileInfo(Path.Combine(Staging, "model.safetensors.part")).Length);
    }

    [Fact]
    public async Task MoreBytesThanTheCatalogListsAreNeverWritten()
    {
        _hub.Answer = _ => HubStub.Unsized(new Unseekable([.. SmallModel.Weights, .. new byte[100_000]]));

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("huggingface.co sent more than the 200,000 bytes of model.safetensors the catalog lists; napkin stopped and deleted what had arrived.", result.Line);
        Assert.False(File.Exists(Path.Combine(Staging, "model.safetensors.part")));
    }

    [Fact]
    public async Task AnyOtherAnswerIsRefusedWithItsStatus()
    {
        _hub.Answer = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("huggingface.co answered 401 (Unauthorized) for model.safetensors; napkin kept nothing from that answer.", result.Line);
    }

    [Fact]
    public async Task ARefusalFromTheContentServerNamesIt()
    {
        _hub.Answer = request => request.RequestUri!.Host == "huggingface.co"
            ? HubStub.Redirect(HttpStatusCode.Found, $"{HubStub.Cdn}/x/model.safetensors")
            : new HttpResponseMessage(HttpStatusCode.Forbidden);

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("us.aws.cdn.hf.co answered 403 (Forbidden) for model.safetensors; napkin kept nothing from that answer.", result.Line);
    }

    [Fact]
    public async Task CancelLeavesWhatArrivedAndNothingElse()
    {
        _hub.ViaCdn.Clear();
        _hub.Answer = _ => HubStub.Unsized(new Stalling(SmallModel.Weights[..70_000]));
        Reports reports = new();
        using CancellationTokenSource cancel = new();

        Task<DownloadResult> running = new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true, reports, cancel.Token);
        await Eventually.True(() => reports.Seen.Any(seen => seen.FileBytes == 70_000), "the first bytes to arrive");
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(SmallModel.Weights[..70_000], File.ReadAllBytes(Path.Combine(Staging, "model.safetensors.part")));
        Assert.False(Directory.Exists(Destination));
        Assert.Single(_hub.Requests);
    }

    public static TheoryData<Exception> Failures => new()
    {
        new IOException("The response ended prematurely."),
        new UnauthorizedAccessException("Access to the path is denied."),
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task ANetworkOrDiskFailureIsSaidAndWhatArrivedIsKept(Exception failure)
    {
        _hub.Answer = _ => HubStub.Unsized(new Stalling(SmallModel.Weights[..70_000], failure));

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal($"The download stopped: {failure.Message} What had arrived is kept; press Download again to resume.", result.Line);
        Assert.Equal(SmallModel.Weights[..70_000], File.ReadAllBytes(Path.Combine(Staging, "model.safetensors.part")));
    }

    [Fact]
    public async Task AConnectionThatFailsIsSaid()
    {
        _hub.Answer = _ => throw new HttpRequestException("No such host is known. (huggingface.co:443)");

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal("The download stopped: No such host is known. (huggingface.co:443) What had arrived is kept; press Download again to resume.", result.Line);
    }

    [Fact]
    public async Task AFileThatAlreadyPassedIsCheckedAgainAndNotFetched()
    {
        Directory.CreateDirectory(Staging);
        File.WriteAllBytes(Path.Combine(Staging, "model.safetensors"), SmallModel.Weights);
        Reports reports = new();

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true, reports);

        Assert.True(result.Succeeded, result.Line);
        Assert.DoesNotContain(_hub.Urls, url => url.AbsolutePath.EndsWith("model.safetensors", StringComparison.Ordinal));
        Assert.Equal(new DownloadProgress("model.safetensors", 1, 4, 200_000, 200_000, 200_000, _catalog.TotalBytes), reports.Seen[0]);
    }

    [Fact]
    public async Task AFileThatPassedButChangedSinceIsFetchedAgain()
    {
        Directory.CreateDirectory(Staging);
        File.WriteAllBytes(Path.Combine(Staging, "config.json"), Encoding.UTF8.GetBytes("{}"));

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.True(result.Succeeded, result.Line);
        Assert.Equal(SmallModel.Config, File.ReadAllBytes(Path.Combine(Destination, "config.json")));
    }

    [Fact]
    public async Task APartLongerThanTheFileStartsAgain()
    {
        Directory.CreateDirectory(Staging);
        File.WriteAllBytes(Path.Combine(Staging, "model.safetensors.part"), new byte[300_000]);

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.True(result.Succeeded, result.Line);
        Assert.Null(_hub.Requests[0].Range);
    }

    [Fact]
    public async Task AWholePartIsCheckedWithoutARequest()
    {
        Directory.CreateDirectory(Staging);
        File.WriteAllBytes(Path.Combine(Staging, "model.safetensors.part"), SmallModel.Weights);

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.True(result.Succeeded, result.Line);
        Assert.DoesNotContain(_hub.Urls, url => url.AbsolutePath.EndsWith("model.safetensors", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AWholeDownloadNeedsNoRequest()
    {
        await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);
        int before = _hub.Requests.Count;

        DownloadResult again = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal(Destination, again.Folder);
        Assert.Equal($"Already downloaded and checked: 4 files in {Destination}.", again.Line);
        Assert.Equal(before, _hub.Requests.Count);
    }

    [Fact]
    public async Task AFolderAlreadyAtTheDestinationIsNotTouched()
    {
        Directory.CreateDirectory(Destination);
        File.WriteAllText(Path.Combine(Destination, "mine.txt"), "mine");

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.Equal($"There is already something at {Destination} that is not a whole download; move it away, or choose it with Choose….", result.Line);
        Assert.Empty(_hub.Requests);
        Assert.False(Directory.Exists(Staging));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(Destination, "mine.txt")));
    }

    [Fact]
    public async Task AFileAtTheDestinationIsNotTouched()
    {
        Directory.CreateDirectory(_models);
        File.WriteAllText(Destination, "mine");

        DownloadResult result = await new ModelDownload(_catalog, _models, _hub).RunAsync(consented: true);

        Assert.False(result.Succeeded);
        Assert.Empty(_hub.Requests);
    }

    [Fact]
    public async Task WholeMeansEveryFileAtItsSize()
    {
        ModelDownload download = new(_catalog, _models, _hub);
        Assert.False(download.IsComplete);

        await download.RunAsync(consented: true);
        Assert.True(download.IsComplete);

        File.WriteAllBytes(Path.Combine(Destination, "config.json"), [1]);
        Assert.False(download.IsComplete);

        File.Delete(Path.Combine(Destination, "config.json"));
        Assert.False(download.IsComplete);
    }

    [Fact]
    public void TheFoldersAreNamedForTheRepositoryAndCommit()
    {
        ModelDownload download = new(ModelCatalog.Default, _models);

        Assert.Same(ModelCatalog.Default, download.Model);
        Assert.Equal(Destination, download.Destination);
        Assert.Equal(Staging, download.Staging);
    }

    [Fact]
    public void ADownloadNeedsAModelAndAPlace()
    {
        Assert.Throws<ArgumentNullException>(() => new ModelDownload(null!, _models));
        Assert.Throws<ArgumentException>(() => new ModelDownload(ModelCatalog.Default, " "));
        Assert.Throws<ArgumentNullException>(() => ModelDownload.IsAllowed(null!));
    }

    [Fact]
    public void NapkinsHandlerFollowsNoRedirectDecompressesNothingAndUsesNoProxy()
    {
        using SocketsHttpHandler handler = ModelDownload.CreateHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.Equal(DecompressionMethods.None, handler.AutomaticDecompression);
        Assert.False(handler.UseProxy);
    }

    [Fact]
    public void TheAllowedHostsAreTheHubsDocumentedList() =>
        Assert.Equal(
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
            ],
            ModelDownload.AllowedHosts);

    [Theory]
    [InlineData("https://huggingface.co/x", true)]
    [InlineData("https://HuggingFace.co/x", true)]
    [InlineData("https://cdn-lfs-eu-1.hf.co/x", true)]
    [InlineData("https://huggingface.co:443/x", true)]
    [InlineData("http://huggingface.co/x", false)]
    [InlineData("https://huggingface.co:444/x", false)]
    [InlineData("https://hf.co/x", false)]
    [InlineData("https://evil.hf.co/x", false)]
    [InlineData("ftp://huggingface.co/x", false)]
    public void OnlyHttpsToAnAllowedHostOnItsOwnPortIsSent(string url, bool allowed) =>
        Assert.Equal(allowed, ModelDownload.IsAllowed(new Uri(url)));

    [Fact]
    public void ARelativeUrlIsNeverSent() => Assert.False(ModelDownload.IsAllowed(new Uri("/x", UriKind.Relative)));

    [Fact]
    public void ProgressSaysTheFileAndTheWhole()
    {
        DownloadProgress progress = new("model.safetensors", 1, 8, 1_200_000_000, 2_263_022_529, 1_200_000_000, 2_277_297_903);

        Assert.Equal("model.safetensors (1 of 8): 1.20 GB of 2.26 GB; 1.20 GB of 2.28 GB in all", progress.Line);
        Assert.Equal(1_200_000_000 / 2_277_297_903.0, progress.Fraction);
    }
}

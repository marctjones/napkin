using Avalonia.Controls;
using Avalonia.Interactivity;

using Napkin.Assistant.Mlx;

namespace Napkin.App;

/// <summary>
/// The consent sheet (docs/design/mlx-runtime.md &#xA7;6.3, decision 11, issue #242): what a download
/// will fetch, exactly, and the one place a download starts. Nothing is sent until the box
/// <em>"Download these files from huggingface.co"</em> is ticked and <em>Download</em> is pressed; the
/// box is cleared after every attempt, so each download is asked for anew. <em>Cancel</em> stops a
/// running download (what arrived is kept, to resume); closing the sheet — Escape, or the dialog
/// closing — stops it too.
/// </summary>
public partial class DownloadSheet : UserControl
{
    /// <summary>The sheet's sentence about the network (&#xA7;6.3).</summary>
    public const string NetworkSentence =
        "napkin will connect to huggingface.co and its content servers for this download and for nothing else; nothing about you or your design is sent.";

    private ModelDownload? _download;
    private CancellationTokenSource? _cancel;
    private bool _running;
    private bool _succeeded;

    /// <summary>An empty sheet; <see cref="Open"/> fills it.</summary>
    public DownloadSheet()
    {
        InitializeComponent();
        NetworkText.Text = NetworkSentence;
    }

    /// <summary>Raised with the folder when a download has put every file in place and checked it.</summary>
    public event EventHandler<string>? Downloaded;

    /// <summary>Raised when the sheet is put away.</summary>
    public event EventHandler? Dismissed;

    /// <summary>The download the sheet describes, once opened.</summary>
    public ModelDownload? Download => _download;

    /// <summary>Whether a download is running.</summary>
    public bool IsRunning => _running;

    /// <summary>The consent box.</summary>
    public CheckBox Consent => ConsentBox;

    /// <summary>The Download button, enabled only while the box is ticked.</summary>
    public Button DownloadAction => DownloadButton;

    /// <summary>The Cancel (while running: stop; otherwise: close) button.</summary>
    public Button CancelAction => CancelButton;

    /// <summary>The sheet's title.</summary>
    public string Title => TitleText.Text ?? string.Empty;

    /// <summary>The repository and commit line.</summary>
    public string RepositoryLine => RepositoryText.Text ?? string.Empty;

    /// <summary>"8 files, 2,277,297,903 bytes (2.28 GB):".</summary>
    public string FilesLine => FilesText.Text ?? string.Empty;

    /// <summary>One row per file: its name and size, its SHA-256, its URL.</summary>
    public IReadOnlyList<string> FileRows => [.. FileLines.Items.Cast<object?>().Select(item => item?.ToString() ?? string.Empty)];

    /// <summary>The licence line.</summary>
    public string LicenceLine => LicenceText.Text ?? string.Empty;

    /// <summary>The model card link's address.</summary>
    public Uri? CardLinkAddress => CardLink.NavigateUri;

    /// <summary>Where the folder will be put.</summary>
    public string DestinationLine => DestinationText.Text ?? string.Empty;

    /// <summary>The network sentence as shown.</summary>
    public string NetworkLine => NetworkText.Text ?? string.Empty;

    /// <summary>The progress line, empty until a download starts.</summary>
    public string ProgressLine => ProgressText.IsVisible ? ProgressText.Text ?? string.Empty : string.Empty;

    /// <summary>The progress bar's value, 0 to 100.</summary>
    public double ProgressPercent => Bar.Value;

    /// <summary>How the last download ended — "Downloaded and checked: 8 files." or why not — empty until one has.</summary>
    public string ResultLine => ResultText.IsVisible ? ResultText.Text ?? string.Empty : string.Empty;

    /// <summary>Fills the sheet for <paramref name="download"/>, the box unticked, and shows it. Sends nothing.</summary>
    /// <param name="download">What would be downloaded, and where to.</param>
    public void Open(ModelDownload download)
    {
        ArgumentNullException.ThrowIfNull(download);
        _download = download;
        _succeeded = false;
        CatalogModel model = download.Model;
        TitleText.Text = $"Download {model.Name} from huggingface.co";
        RepositoryText.Text = $"Repository {model.Repo}, at commit {model.Commit} — a fixed commit, never a branch that can move.";
        FilesText.Text = model.FilesLine + ", each checked against its SHA-256 as it lands:";
        FileLines.ItemsSource = model.Files.Select(file => Row(model, file)).ToArray();
        LicenceText.Text = $"Licence: {model.Licence}, as the model card at that commit states it. The licence: {model.LicenceLink}";
        CardLink.Content = model.CardUrl;
        CardLink.NavigateUri = new Uri(model.CardUrl);
        DestinationText.Text = $"Into: {download.Destination} (the folder appears only when every file has passed)";
        ConsentBox.IsChecked = false;
        ConsentBox.IsEnabled = true;
        Bar.IsVisible = false;
        Bar.Value = 0;
        Say(ProgressText, null);
        Say(ResultText, null);
        UpdateButtons();
        IsVisible = true;
    }

    /// <summary>
    /// Runs the download if the box is ticked, off the UI thread, the progress line polled while it
    /// runs; says how it ended; clears the box.
    /// </summary>
    public async Task DownloadAsync()
    {
        if (_download is not { } download || _running || ConsentBox.IsChecked != true)
        {
            return;
        }

        _running = true;
        ConsentBox.IsEnabled = false;
        UpdateButtons();
        Say(ResultText, null);
        Bar.IsVisible = true;
        Bar.Value = 0;
        Say(ProgressText, "Connecting to huggingface.co…");
        using CancellationTokenSource cancel = new();
        _cancel = cancel;
        LatestProgress latest = new();
        try
        {
            Task<DownloadResult> running = Task.Run(() => download.RunAsync(consented: true, latest, cancel.Token));
            while (!running.IsCompleted)
            {
                Show(latest.Value);
                await Task.WhenAny(running, Task.Delay(100)).ConfigureAwait(true);
            }

            Show(latest.Value);
            DownloadResult result = await running.ConfigureAwait(true);
            Say(ResultText, result.Line);
            if (result.Folder is { } folder)
            {
                _succeeded = true;
                Downloaded?.Invoke(this, folder);
            }
        }
        catch (OperationCanceledException)
        {
            Say(ResultText, $"Stopped. What had arrived is kept in {download.Staging}, so Download carries on from there.");
        }
        finally
        {
            _cancel = null;
            _running = false;
            ConsentBox.IsChecked = false;
            ConsentBox.IsEnabled = !_succeeded;
            UpdateButtons();
        }
    }

    /// <summary>Puts the sheet away, stopping a download that is running.</summary>
    public void Close()
    {
        _cancel?.Cancel();
        if (!IsVisible)
        {
            return;
        }

        IsVisible = false;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void OnConsentChanged(object? sender, RoutedEventArgs e) => UpdateButtons();

    private async void OnDownloadClicked(object? sender, RoutedEventArgs e) => await DownloadAsync().ConfigureAwait(true);

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (_running)
        {
            _cancel?.Cancel();
            return;
        }

        Close();
    }

    private void UpdateButtons()
    {
        DownloadButton.IsEnabled = !_running && !_succeeded && ConsentBox.IsChecked == true;
        CancelButton.Content = _succeeded ? "Close" : "Cancel";
    }

    private void Show(DownloadProgress? progress)
    {
        if (progress is null)
        {
            return;
        }

        Bar.Value = progress.Fraction * 100;
        Say(ProgressText, progress.Line);
    }

    /// <summary>A file's row: name and size, SHA-256, and the exact address it is fetched from.</summary>
    private static string Row(CatalogModel model, CatalogFile file) =>
        $"{file.Name} — {ModelCatalog.Bytes(file.Bytes)} bytes\nSHA-256 {file.Sha256}\n{model.FileUrl(file)}";

    private static void Say(TextBlock line, string? text)
    {
        line.Text = text ?? string.Empty;
        line.IsVisible = !string.IsNullOrEmpty(text);
    }

    /// <summary>The last report, kept for the UI thread's poll (a <see cref="Progress{T}"/> would post to it instead).</summary>
    private sealed class LatestProgress : IProgress<DownloadProgress>
    {
        private DownloadProgress? _value;

        public DownloadProgress? Value => Volatile.Read(ref _value);

        public void Report(DownloadProgress value) => Volatile.Write(ref _value, value);
    }
}

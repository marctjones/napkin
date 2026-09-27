using System.Collections.Immutable;
using System.Globalization;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

using Napkin.App.Settings;
using Napkin.Assistant.LocalServer;
using Napkin.Assistant.Mlx;

namespace Napkin.App;

/// <summary>
/// <em>Assistant &#x2192; Where the model runs&#x2026;</em> (docs/design/llm-assistant.md &#xA7;8, issue
/// #231; the MLX choice, docs/design/mlx-runtime.md &#xA7;4.4, #241): none, a program on this machine
/// at a loopback address, or a model run inside napkin by MLX on this Mac; the models a program
/// reports, with the size, quantization and license it reports, or a folder's own; a Test that
/// asks for "ok"; napkin's memory rule; how to get a model; and the Ollama-cloud sentence.
/// </summary>
/// <remarks>
/// Not modal, like the code window. Nothing is saved until <em>Use these settings</em>; the owner
/// then writes the settings and rebuilds its model (<see cref="Apply"/>). Every word about getting
/// and choosing a model comes from <see cref="Guidance"/>, where each fact sits beside its source.
/// </remarks>
public partial class AssistantWindow : Window
{
    /// <summary>
    /// What the temperature line says. napkin's 0.2 is its own number (&#xA7;5.2); the card's is read
    /// from huggingface.co/Qwen/Qwen3-4B, "For non-thinking mode, we suggest using Temperature=0.7"
    /// (read 2026-09-27).
    /// </summary>
    public const string TemperatureHint =
        "napkin's default is 0.2, its own choice for short, plain answers. Qwen3's model card suggests 0.7 without thinking.";

    private readonly CancellationTokenSource _closing = new();
    private ImmutableArray<InstalledModel> _listed = [];
    private int _busy;
    private bool _mlxProbed;
    private MlxProbe? _mlxProbe;
    private CatalogModel _downloadModel = ModelCatalog.Default;
    private string _modelsDirectory = ModelCatalog.ModelsDirectory(SettingsStore.ConfigDirectory());

    /// <summary>An empty dialog, for the designer and for a test.</summary>
    public AssistantWindow()
    {
        InitializeComponent();
        AddressHintText.Text = Guidance.AddressHint;
        TemperatureHintText.Text = TemperatureHint;
        InstallLines.ItemsSource = Guidance.InstallLines;
        CloudText.Text = Guidance.CloudSentence;
        MemoryText.Text = Guidance.MemoryLine(MachineMemory);
        Sheet.Downloaded += (_, folder) =>
        {
            MlxFolderBox.Text = folder;
            UpdateDownloadButton();
        };
        Sheet.Dismissed += (_, _) =>
        {
            Page.IsEnabled = true;
            UpdateDownloadButton();
        };
        AddHandler(KeyDownEvent, OnSheetKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Closed += (_, _) =>
        {
            _closing.Cancel();
            Sheet.Close();
        };
        ShowSettings(AssistantSettings.None);
    }

    /// <summary>
    /// How a local program is reached and how a model is downloaded: null for napkin's own handlers
    /// (the loopback-only one, and <see cref="ModelDownload.CreateHandler"/>); the GUI suite sets a
    /// stub. A download's redirect and host rules are napkin's code, not the handler's, so they hold
    /// under a stub too.
    /// </summary>
    public HttpMessageHandler? Http { get; set; }

    /// <summary>The model Download… offers: <see cref="ModelCatalog.Default"/> unless a test says otherwise.</summary>
    public CatalogModel DownloadModel
    {
        get => _downloadModel;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _downloadModel = value;
            UpdateDownloadButton();
            UpdateMlxMemoryLine();
        }
    }

    /// <summary>Where downloaded models live (decision 5): <c>&lt;config&gt;/models</c>; the owner sets it beside its settings file.</summary>
    public string ModelsDirectory
    {
        get => _modelsDirectory;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _modelsDirectory = value;
            UpdateDownloadButton();
        }
    }

    /// <summary>The Download… button: "Download Qwen3-4B-4bit (2.28 GB, apache-2.0)…", or "Downloaded", disabled, once the folder is whole.</summary>
    public Button MlxDownload => MlxDownloadButton;

    /// <summary>The consent sheet.</summary>
    public DownloadSheet ConsentSheet => Sheet;

    /// <summary>Whether the consent sheet is up.</summary>
    public bool IsConsentSheetOpen => Sheet.IsVisible;

    /// <summary>The MLX bridge Test uses: null for the real one (<see cref="NativeMlx"/>); the GUI suite sets a fake.</summary>
    public INativeMlx? Mlx { get; set; }

    /// <summary>
    /// Why MLX cannot run in this process, or null when it can: <see cref="MlxAvailability.ForThisProcess"/>
    /// by default. The GUI suite overrides it so the dialog's third radio is enabled on every CI
    /// platform, not only Apple silicon (docs/design/mlx-runtime.md &#xA7;7.3).
    /// </summary>
    public Func<string?> MlxUnavailable { get; set; } = MlxAvailability.ForThisProcess;

    /// <summary>Saves the chosen settings and answers the whereabouts line the note will now show; set by the owner.</summary>
    public Func<AssistantSettings, string>? Apply { get; set; }

    /// <summary>
    /// The machine's memory for napkin's rule (&#xA7;5.4): <c>GC.GetGCMemoryInfo().TotalAvailableMemoryBytes</c>
    /// unless a test says otherwise.
    /// </summary>
    public long MachineMemory
    {
        get => _machineMemory;
        set
        {
            _machineMemory = value;
            MemoryText.Text = Guidance.MemoryLine(value);
        }
    }

    private long _machineMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    /// <summary>Whether a Check or a Test is out.</summary>
    public bool IsBusy => _busy > 0;

    /// <summary>The None choice.</summary>
    public RadioButton NoneChoice => NoneRadio;

    /// <summary>The local-program choice.</summary>
    public RadioButton LocalChoice => LocalRadio;

    /// <summary>The MLX choice: "In napkin, on this Mac (MLX)".</summary>
    public RadioButton MlxChoice => MlxRadio;

    /// <summary>Why MLX is disabled, as shown; empty when it is available.</summary>
    public string MlxUnavailableLine => MlxUnavailableText.IsVisible ? MlxUnavailableText.Text ?? string.Empty : string.Empty;

    /// <summary>The model folder field.</summary>
    public TextBox MlxFolderField => MlxFolderBox;

    /// <summary>What the folder says about itself, or the refusal naming what it is missing.</summary>
    public string MlxFolderLine => MlxFolderText.IsVisible ? MlxFolderText.Text ?? string.Empty : string.Empty;

    /// <summary>The memory line, from the bridge's device probe.</summary>
    public string MlxMemoryLine => MlxMemoryText.IsVisible ? MlxMemoryText.Text ?? string.Empty : string.Empty;

    /// <summary>The MLX Test button.</summary>
    public Button MlxTest => MlxTestButton;

    /// <summary>What the last MLX Test reported, or is reporting while loading, as shown.</summary>
    public string MlxTestLine => MlxTestText.IsVisible ? MlxTestText.Text ?? string.Empty : string.Empty;

    /// <summary>The address field.</summary>
    public TextBox AddressField => AddressBox;

    /// <summary>The Check button.</summary>
    public Button Check => CheckButton;

    /// <summary>What the last Check found or refused, as shown.</summary>
    public string CheckLine => CheckText.IsVisible ? CheckText.Text ?? string.Empty : string.Empty;

    /// <summary>The model list.</summary>
    public ListBox ModelPicker => ModelList;

    /// <summary>The model list's rows as shown.</summary>
    public IReadOnlyList<string> ModelRows => [.. ModelList.Items.Cast<object?>().Select(item => item?.ToString() ?? string.Empty)];

    /// <summary>The memory line.</summary>
    public string MemoryLine => MemoryText.Text ?? string.Empty;

    /// <summary>The model name field.</summary>
    public TextBox ModelField => ModelBox;

    /// <summary>The temperature field.</summary>
    public TextBox TemperatureField => TemperatureBox;

    /// <summary>The Test button.</summary>
    public Button Test => TestButton;

    /// <summary>What the last Test reported, as shown.</summary>
    public string TestLine => TestText.IsVisible ? TestText.Text ?? string.Empty : string.Empty;

    /// <summary>The Use these settings button.</summary>
    public Button Use => UseButton;

    /// <summary>What Use these settings said, as shown.</summary>
    public string SavedLine => SavedText.IsVisible ? SavedText.Text ?? string.Empty : string.Empty;

    /// <summary>The install lines as shown.</summary>
    public IReadOnlyList<string> InstallTexts => [.. InstallLines.Items.Cast<object?>().Select(item => item?.ToString() ?? string.Empty)];

    /// <summary>The Ollama-cloud sentence as shown.</summary>
    public string CloudLine => CloudText.Text ?? string.Empty;

    /// <summary>Fills the dialog from the person's settings.</summary>
    public void ShowSettings(AssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        NoneRadio.IsChecked = settings.Provider == AssistantProvider.None;
        LocalRadio.IsChecked = settings.Provider == AssistantProvider.LocalServer;
        string? mlxRefusal = MlxUnavailable();
        MlxRadio.IsEnabled = mlxRefusal is null;
        Say(MlxUnavailableText, mlxRefusal);
        MlxRadio.IsChecked = settings.Provider == AssistantProvider.Mlx;
        AddressBox.Text = settings.Endpoint ?? LocalEndpoint.OllamaDefault;
        ModelBox.Text = settings.Model ?? string.Empty;
        MlxFolderBox.Text = settings.ModelFolder ?? string.Empty;
        TemperatureBox.Text = settings.Temperature.ToString(CultureInfo.InvariantCulture);
        LocalPanel.IsEnabled = LocalRadio.IsChecked == true;
        MlxPanel.IsEnabled = MlxRadio.IsChecked == true && MlxRadio.IsEnabled;
        TemperaturePanel.IsEnabled = LocalPanel.IsEnabled || MlxPanel.IsEnabled;
        UpdateMlxFolderLine();
        UpdateDownloadButton();
    }

    private void OnProviderChanged(object? sender, RoutedEventArgs e)
    {
        LocalPanel.IsEnabled = LocalRadio.IsChecked == true;
        MlxPanel.IsEnabled = MlxRadio.IsChecked == true && MlxRadio.IsEnabled;
        TemperaturePanel.IsEnabled = LocalPanel.IsEnabled || MlxPanel.IsEnabled;
        if (MlxPanel.IsEnabled)
        {
            UpdateDownloadButton();
        }

        if (MlxPanel.IsEnabled && !_mlxProbed)
        {
            _mlxProbed = true;
            _ = ProbeMlxDeviceAsync();
        }
    }

    /// <summary>
    /// Download… reads "Downloaded" and is disabled when the catalog model's folder is already whole
    /// under <see cref="ModelsDirectory"/> (every file at its size); otherwise it names the model,
    /// its size and its licence. Deleting the folder in Finder brings it back.
    /// </summary>
    private void UpdateDownloadButton()
    {
        bool whole = new ModelDownload(DownloadModel, ModelsDirectory, Http).IsComplete;
        MlxDownloadButton.Content = whole
            ? "Downloaded"
            : $"Download {DownloadModel.Name} ({ModelCatalog.Size(DownloadModel.TotalBytes)}, {DownloadModel.Licence})…";
        MlxDownloadButton.IsEnabled = !whole;
    }

    private void OnMlxDownloadClicked(object? sender, RoutedEventArgs e) => OpenConsentSheet();

    /// <summary>
    /// Lays the consent sheet over the page for the catalog model (&#xA7;6.3). Sends nothing: only the
    /// sheet's Download, with its box ticked, starts a download.
    /// </summary>
    public void OpenConsentSheet()
    {
        Page.IsEnabled = false;
        Sheet.Open(new ModelDownload(DownloadModel, ModelsDirectory, Http));
    }

    /// <summary>Escape puts the sheet away (stopping a download), before anything on the page sees the key.</summary>
    private void OnSheetKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape && Sheet.IsVisible)
        {
            Sheet.Close();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Asks the bridge, off the UI thread since it dlopens the library, what this Mac's Metal device
    /// and memory are, for the dialog's memory line (docs/design/mlx-runtime.md &#xA7;4.4). A refusal
    /// (no Metal device, or the bridge would not load) is shown in its place.
    /// </summary>
    private async Task ProbeMlxDeviceAsync()
    {
        Say(MlxMemoryText, "Checking this Mac…");
        _mlxProbe = await Task.Run(() => MlxAvailability.Probe(Mlx ?? new NativeMlx())).ConfigureAwait(true);
        UpdateMlxMemoryLine();
    }

    /// <summary>
    /// The memory line (&#xA7;4.4): this Mac's memory and Metal's figure, and the weights of the model
    /// that would run — the typed folder's when it parses, else the download's — with napkin's own
    /// "too big" rule, labelled as napkin's. The probe's refusal in its place when MLX cannot run.
    /// </summary>
    private void UpdateMlxMemoryLine()
    {
        if (_mlxProbe is not { } probe)
        {
            return;
        }

        if (probe.Refusal is not null || probe.Device is not { } device)
        {
            Say(MlxMemoryText, probe.Refusal);
            return;
        }

        (string name, long weights) = ModelFolder.TryParse(MlxFolderBox.Text, out ModelFolder? folder, out _)
            ? (folder.DisplayName, folder.WeightBytes)
            : (DownloadModel.Name, DownloadModel.WeightBytes);
        string fit = Guidance.TooBig(weights, (long)device.MemoryBytes) ? $" — {Guidance.TooBigWords}" : string.Empty;
        Say(
            MlxMemoryText,
            string.Create(
                CultureInfo.InvariantCulture,
                $"This Mac: {device.MemoryBytes / 1073741824.0:0.#} GiB memory, Metal recommends up to {device.RecommendedWorkingSetBytes / 1073741824.0:0.#} GiB for the GPU (Apple's number). {name} needs {ModelCatalog.Size(weights)} for its weights plus its working memory (napkin's estimate){fit}."));
    }

    private void OnMlxFolderChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        UpdateMlxFolderLine();
        UpdateMlxMemoryLine();
    }

    /// <summary>What the typed folder says about itself, or the refusal naming what is wrong with it.</summary>
    private void UpdateMlxFolderLine() => Say(
        MlxFolderText,
        ModelFolder.TryParse(MlxFolderBox.Text, out ModelFolder? folder, out string? refusal) ? folder.Description : refusal);

    private async void OnChooseMlxFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> chosen = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the model folder",
            AllowMultiple = false,
        }).ConfigureAwait(true);
        if (chosen.Count == 0 || chosen[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        MlxFolderBox.Text = path;
    }

    private async void OnCheckClicked(object? sender, RoutedEventArgs e) => await CheckAsync().ConfigureAwait(true);

    private async void OnTestClicked(object? sender, RoutedEventArgs e) => await TestAsync().ConfigureAwait(true);

    private async void OnMlxTestClicked(object? sender, RoutedEventArgs e) => await MlxTestAsync().ConfigureAwait(true);

    private void OnUseClicked(object? sender, RoutedEventArgs e) => UseSettings();

    /// <summary>
    /// Asks the program at the address what it has and lists it as read. An address that is not
    /// this machine is refused here, before anything is sent.
    /// </summary>
    public async Task CheckAsync()
    {
        _listed = [];
        ModelList.ItemsSource = Array.Empty<string>();
        if (!LocalEndpoint.TryParse(AddressBox.Text, out LocalEndpoint? endpoint, out string? refusal))
        {
            Say(CheckText, refusal);
            return;
        }

        Say(CheckText, $"Asking {endpoint}…");
        _busy++;
        try
        {
            using LocalProgram program = new(endpoint, Http);
            ServerListing listing = await program.ListAsync(_closing.Token).ConfigureAwait(true);
            _listed = listing.Models;
            ModelList.ItemsSource = listing.Models.Select(model => model.Describe(MachineMemory)).ToArray();
            Say(CheckText, listing.Refusal ?? Found(listing));
        }
        catch (OperationCanceledException)
        {
            // The dialog closed while the program was being asked.
        }
        finally
        {
            _busy--;
        }
    }

    /// <summary>Asks the chosen model for "ok" and reports the round trip, or why not.</summary>
    public async Task TestAsync()
    {
        if (Chosen(out string? why) is not { } chosen)
        {
            Say(TestText, why);
            return;
        }

        Say(TestText, $"Asking {chosen.Model}…");
        _busy++;
        try
        {
            using LocalServerModel model = new(LocalEndpoint.Parse(chosen.Endpoint), chosen.Model!, chosen.Temperature, Http);
            TestResult result = await model.TestAsync(_closing.Token).ConfigureAwait(true);
            Say(TestText, result.Line);
        }
        catch (OperationCanceledException)
        {
            // The dialog closed while the model was being asked.
        }
        finally
        {
            _busy--;
        }
    }

    /// <summary>
    /// Loads the folder if need be and asks the model to reply "ok", the loading line polled while
    /// the bridge reads the weights (docs/design/mlx-runtime.md &#xA7;4.4, &#xA7;7.3).
    /// </summary>
    public async Task MlxTestAsync()
    {
        if (!ModelFolder.TryParse(MlxFolderBox.Text, out ModelFolder? folder, out string? folderWhy))
        {
            Say(MlxTestText, folderWhy);
            return;
        }

        if (Temperature(out string? temperatureWhy) is not { } temperature)
        {
            Say(MlxTestText, temperatureWhy);
            return;
        }

        _busy++;
        MlxModel model = new(Mlx ?? new NativeMlx(), folder, temperature);
        Say(MlxTestText, model.LoadingLine ?? string.Create(CultureInfo.InvariantCulture, $"loading the model ({folder.WeightBytes / 1e9:0.0} GB)… 0 s"));
        try
        {
            Task<TestResult> testTask = model.TestAsync(_closing.Token);
            while (!testTask.IsCompleted)
            {
                if (model.LoadingLine is { } line)
                {
                    Say(MlxTestText, line);
                }

                await Task.WhenAny(testTask, Task.Delay(100)).ConfigureAwait(true);
            }

            TestResult result = await testTask.ConfigureAwait(true);
            Say(MlxTestText, result.Line);
        }
        catch (OperationCanceledException)
        {
            // The dialog closed while the model was being asked.
        }
        finally
        {
            model.Dispose();
            _busy--;
        }
    }

    /// <summary>Saves the choice through the owner, or says why it cannot be saved.</summary>
    public void UseSettings()
    {
        AssistantSettings? settings;
        string? why = null;
        if (NoneRadio.IsChecked == true)
        {
            settings = new AssistantSettings(
                AssistantProvider.None,
                CurrentAddress(),
                CurrentModelName(),
                Temperature(out _) ?? LocalServerModel.DefaultTemperature,
                CurrentMlxFolder());
        }
        else if (MlxRadio.IsChecked == true)
        {
            settings = MlxChosen(out why);
        }
        else
        {
            settings = Chosen(out why);
        }

        if (settings is null)
        {
            Say(SavedText, why);
            return;
        }

        string whereabouts = Apply?.Invoke(settings) ?? string.Empty;
        Say(SavedText, $"Saved. The note now ends: {whereabouts}");
    }

    /// <summary>The typed address, or Ollama's default when nothing was typed.</summary>
    private string CurrentAddress() => AddressBox.Text?.Trim() is { Length: > 0 } address ? address : LocalEndpoint.OllamaDefault;

    /// <summary>The typed model name, or null.</summary>
    private string? CurrentModelName() => ModelBox.Text?.Trim() is { Length: > 0 } name ? name : null;

    /// <summary>The typed MLX folder, or null.</summary>
    private string? CurrentMlxFolder() => MlxFolderBox.Text?.Trim() is { Length: > 0 } folder ? folder : null;

    /// <summary>Selecting a row fills the model's name; a model the program says is remote is not taken.</summary>
    private void OnModelChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (ModelList.SelectedIndex is int index && index >= 0 && index < _listed.Length)
        {
            InstalledModel model = _listed[index];
            if (model.IsRemote)
            {
                Say(CheckText, $"{model.Name} {Guidance.RunsElsewhere(model.RemoteHost)}.");
                return;
            }

            ModelBox.Text = model.Name;
        }
    }

    /// <summary>The local settings the fields describe, or null with the reason.</summary>
    private AssistantSettings? Chosen(out string? why)
    {
        if (!LocalEndpoint.TryParse(AddressBox.Text, out LocalEndpoint? endpoint, out why))
        {
            return null;
        }

        string model = ModelBox.Text?.Trim() ?? string.Empty;
        if (model.Length == 0)
        {
            why = "Type a model's name, or press Check and pick one from the list.";
            return null;
        }

        if (Temperature(out why) is not { } temperature)
        {
            return null;
        }

        if (_listed.FirstOrDefault(listed => listed.Name == model) is { IsRemote: true } remote)
        {
            why = $"{remote.Name} {Guidance.RunsElsewhere(remote.RemoteHost)}.";
            return null;
        }

        return new AssistantSettings(AssistantProvider.LocalServer, endpoint.ToString(), model, temperature, CurrentMlxFolder());
    }

    /// <summary>The MLX settings the fields describe, or null with the reason.</summary>
    private AssistantSettings? MlxChosen(out string? why)
    {
        if (!ModelFolder.TryParse(MlxFolderBox.Text, out ModelFolder? folder, out why))
        {
            return null;
        }

        if (Temperature(out why) is not { } temperature)
        {
            return null;
        }

        return new AssistantSettings(AssistantProvider.Mlx, CurrentAddress(), CurrentModelName(), temperature, folder.Path);
    }

    private double? Temperature(out string? why)
    {
        why = null;
        if (double.TryParse(TemperatureBox.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double temperature)
            && double.IsFinite(temperature) && temperature >= 0)
        {
            return temperature;
        }

        why = "The temperature is a number from 0 up, like 0.2.";
        return null;
    }

    private static string Found(ServerListing listing)
    {
        string who = listing.Dialect == Dialect.Ollama ? "Ollama" : "An OpenAI-compatible server (llama-server)";
        return listing.Models.Length switch
        {
            0 => $"{who} at {listing.Endpoint} has no models yet: pull one as below, then Check again.",
            1 => $"{who} at {listing.Endpoint} has 1 model, as it reports it:",
            int n => $"{who} at {listing.Endpoint} has {n} models, as it reports them:",
        };
    }

    private static void Say(TextBlock line, string? text)
    {
        line.Text = text ?? string.Empty;
        line.IsVisible = !string.IsNullOrEmpty(text);
    }
}

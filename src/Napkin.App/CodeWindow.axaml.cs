using System.Globalization;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;

using Design = Napkin.App.Designs.Design;

namespace Napkin.App;

/// <summary>
/// The project's adopted code and site values (issues #18, #19): the picker over the packs napkin
/// found, lock or follow, and the site's hazard values as the person types them.
/// </summary>
/// <remarks>
/// Not modal, like the cut list: the drawing goes on being edited with it open. Every change is a
/// request through the owner's editor (<see cref="ApplyRequest"/>), so undo covers it, and the
/// owner recomputes every header and says what changed.
/// </remarks>
public partial class CodeWindow : Window
{
    /// <summary>What the first row of the picker says.</summary>
    public const string NoCodeRow = "No code chosen";

    private readonly List<LoadedPack?> _rows = [];
    private bool _filling;

    /// <summary>An empty window, for the designer and for a test.</summary>
    public CodeWindow()
    {
        InitializeComponent();
        foreach (TextBox box in SiteBoxes)
        {
            box.AddHandler(
                KeyDownEvent,
                (_, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        ApplySite();
                        e.Handled = true;
                    }
                },
                handledEventsToo: true);
        }
    }

    /// <summary>How this window changes the design: set by the owner to put a request through its editor.</summary>
    public Action<Request, string>? ApplyRequest { get; set; }

    /// <summary>The packs napkin found, and the folders it looked in.</summary>
    public CodePacks Packs { get; set; } = CodePacks.None;

    /// <summary>The packs roots looked in, said when no pack was found.</summary>
    public IReadOnlyList<string> PackRoots { get; set; } = [];

    /// <summary>The date a lock is recorded on; today unless a test says otherwise.</summary>
    public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>The design whose code and site are shown.</summary>
    public Design? Design { get; private set; }

    /// <summary>The picker's rows as on screen, for the GUI suite.</summary>
    public IReadOnlyList<string> PackRows => [.. PackList.Items.Cast<object?>().Select(item => item?.ToString() ?? string.Empty)];

    /// <summary>The picker.</summary>
    public ListBox PackPicker => PackList;

    /// <summary>The lock toggle.</summary>
    public CheckBox LockToggle => LockBox;

    /// <summary>The line under the lock toggle.</summary>
    public string LockText => LockNote.Text ?? string.Empty;

    /// <summary>What the project's code resolves to, or why nothing.</summary>
    public string StatusText => CodeStatus.Text ?? string.Empty;

    /// <summary>The packs that did not load, with their problems; empty when none.</summary>
    public string ProblemsText => PackProblems.IsVisible ? PackProblems.Text ?? string.Empty : string.Empty;

    /// <summary>Why the site values were not applied; empty when they were.</summary>
    public string SiteErrorText => SiteError.IsVisible ? SiteError.Text ?? string.Empty : string.Empty;

    /// <summary>The ground snow load field.</summary>
    public TextBox SnowField => SnowBox;

    /// <summary>The roof live load field (psf), asked for only when a table's footnote needs it.</summary>
    public TextBox RoofLiveLoadField => RoofLiveBox;

    /// <summary>The building width field.</summary>
    public TextBox WidthField => WidthBox;

    /// <summary>The apply button for the site values.</summary>
    public Button ApplySiteControl => ApplySiteButton;

    private IEnumerable<TextBox> SiteBoxes => [SnowBox, WindBox, SeismicBox, FrostBox, WidthBox, RoofLiveBox, SourceBox];

    /// <summary>
    /// A pack as the picker lists it (#19): "&lt;shortName&gt; — &lt;baseCode&gt;, in force &lt;from&gt;[ to
    /// &lt;to&gt;]", its pack id and revision, and what napkin can do with it.
    /// </summary>
    public static string Row(LoadedPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        Adoption adoption = pack.Manifest.Adoption;
        string window = $"in force {Date(adoption.InForceFrom)}" + (adoption.InForceTo is { } to ? $" to {Date(to)}" : string.Empty);
        string status = pack.StatusLabel.Length > 0 ? $": {pack.StatusLabel}" : string.Empty;
        string review = pack.Manifest.Review.Status == ReviewStatus.SignedOff ? string.Empty : " (UNREVIEWED)";
        return $"{adoption.ShortName} — {pack.Manifest.BaseCode}, {window} (pack {pack.Manifest.Id} rev {pack.Manifest.Revision}){status}{review}";
    }

    private static string Date(DateOnly date) => date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>Shows a design's code and site, or empties the window when there is none.</summary>
    public void ShowDesign(Design? design)
    {
        Design = design;
        Sketch sketch = design?.Sketch ?? Sketch.Empty;
        Title = design is null ? "Adopted code and site" : $"Adopted code and site — {design.Name}";
        DesignHeadline.Text = design is null ? "No design is open." : $"What {design.Name} is checked against";

        _filling = true;
        try
        {
            FillPicker(sketch.Code);
            FillSite(sketch.Site);
        }
        finally
        {
            _filling = false;
        }
    }

    private void FillPicker(CodeChoice? code)
    {
        _rows.Clear();
        _rows.Add(null);
        _rows.AddRange(Packs.Loaded
            .OrderBy(pack => pack.Manifest.Jurisdiction.Country, StringComparer.Ordinal)
            .ThenBy(pack => pack.Manifest.Jurisdiction.State, StringComparer.Ordinal)
            .ThenBy(pack => pack.Manifest.Jurisdiction.Municipality ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(pack => pack.Manifest.Adoption.InForceFrom)
            .ThenBy(pack => pack.Manifest.Id, StringComparer.Ordinal)
            .ThenBy(pack => pack.Manifest.Revision));
        PackList.ItemsSource = _rows.Select(pack => pack is null ? NoCodeRow : Row(pack)).ToArray();

        CodeResolution resolved = Packs.Resolve(code);
        int index = code is null ? 0 : _rows.FindIndex(pack => pack is not null && ReferenceEquals(pack, resolved.Pack));
        if (index < 0)
        {
            index = _rows.FindIndex(pack => pack is not null && pack.Manifest.Id == code!.PackId && pack.Manifest.Revision == code.Revision);
        }

        PackList.SelectedIndex = index;

        List<string> problems = [.. Packs.Invalid.Select(invalid => $"Pack {invalid.PackId} did not load: {string.Join("; ", invalid.Problems)}")];
        if (Packs.Loaded.IsEmpty && Packs.Invalid.IsEmpty)
        {
            problems.Add($"No code packs were found in {string.Join(" or ", PackRoots)}. docs/rules-engine.md says how to add one.");
        }

        PackProblems.Text = string.Join("\n", problems);
        PackProblems.IsVisible = problems.Count > 0;

        LockBox.IsEnabled = code is not null;
        LockBox.IsChecked = code?.Mode == CodeMode.Locked;
        LockNote.Text = code switch
        {
            null => "Choose a code to lock it or let it follow.",
            { Mode: CodeMode.Locked, LockedOn: { } on } => $"Locked on {on:yyyy-MM-dd} to pack {code.PackId} revision {code.Revision}.",
            _ => $"Following pack {code.PackId}: a newer revision is used when one is installed, and napkin says what changed.",
        };
        CodeStatus.Text = resolved.Pack is { } pack
            ? $"Checking against {pack.Code}." + (pack.HasHeaderTables ? string.Empty : " Its base tables are not loaded: no header can be sized until they are (docs/rules-engine.md says how to add them).")
            : resolved.Problem;
    }

    private void FillSite(SiteValues site)
    {
        SnowBox.Text = site.GroundSnowLoadPsf?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        WindBox.Text = site.UltimateWindSpeedMph?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SeismicBox.Text = site.SeismicDesignCategory ?? string.Empty;
        FrostBox.Text = site.FrostDepth is { } frost ? frost.Format(new FeetInchesFormat(16)).Text : string.Empty;
        WidthBox.Text = site.BuildingWidth is { } width ? width.Format(new FeetInchesFormat(16)).Text : string.Empty;
        RoofLiveBox.Text = site.RoofLiveLoadPsf?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SourceBox.Text = site.Source?.Text ?? string.Empty;
        SiteError.IsVisible = false;
    }

    private void OnPackChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (_filling || Design is null || PackList.SelectedIndex < 0 || PackList.SelectedIndex >= _rows.Count)
        {
            return;
        }

        Choose(_rows[PackList.SelectedIndex]);
    }

    /// <summary>Makes a pack the project's code (null for none), locked or following as the toggle says.</summary>
    public void Choose(LoadedPack? pack)
    {
        CodeChoice? now = Design?.Sketch.Code;
        if (pack is null)
        {
            if (now is not null)
            {
                ApplyRequest?.Invoke(new SetCode(null), "Cleared the adopted code");
            }

            return;
        }

        bool locked = LockBox.IsChecked == true || now is null;
        CodeChoice choice = new(pack.Manifest.Id, pack.Manifest.Revision, locked ? CodeMode.Locked : CodeMode.Following, locked ? Today() : null);
        if (now is not null && now.PackId == choice.PackId && now.Revision == choice.Revision && now.Mode == choice.Mode)
        {
            return;
        }

        ApplyRequest?.Invoke(new SetCode(choice), $"Chose {pack.Manifest.Adoption.ShortName} as the adopted code");
    }

    private void OnLockChanged(object? sender, RoutedEventArgs e)
    {
        if (_filling || Design?.Sketch.Code is not { } code)
        {
            return;
        }

        bool locked = LockBox.IsChecked == true;
        if (locked == (code.Mode == CodeMode.Locked))
        {
            return;
        }

        // Locking takes the revision in use now: a following project has been using the newest.
        int revision = Packs.Resolve(code).Pack?.Manifest.Revision ?? code.Revision;
        ApplyRequest?.Invoke(
            new SetCode(locked ? code with { Mode = CodeMode.Locked, Revision = revision, LockedOn = Today() } : code with { Mode = CodeMode.Following, LockedOn = null }),
            locked ? $"Locked the code to revision {revision}" : "Let the code follow new revisions");
    }

    private void OnApplySiteClicked(object? sender, RoutedEventArgs e) => ApplySite();

    /// <summary>Reads the site fields and applies them; an empty field is "not entered".</summary>
    public void ApplySite()
    {
        if (Design is null)
        {
            return;
        }

        List<string> wrong = [];
        int? snow = Whole(SnowBox.Text, "The ground snow load", "psf", wrong);
        int? wind = Whole(WindBox.Text, "The wind speed", "mph", wrong);
        Length? frost = Distance(FrostBox.Text, "The frost depth", allowZero: true, wrong);
        Length? width = Distance(WidthBox.Text, "The building width", allowZero: false, wrong);
        int? roofLive = Whole(RoofLiveBox.Text, "The roof live load", "psf", wrong);
        string? seismic = string.IsNullOrWhiteSpace(SeismicBox.Text) ? null : SeismicBox.Text.Trim();
        string? source = string.IsNullOrWhiteSpace(SourceBox.Text) ? null : SourceBox.Text.Trim();

        SiteError.Text = string.Join(" ", wrong);
        SiteError.IsVisible = wrong.Count > 0;
        if (wrong.Count > 0)
        {
            return;
        }

        SiteSource? from = source is null ? null : new SiteSource(source, Design.Sketch.Site.Source?.Text == source ? Design.Sketch.Site.Source.On : Today());
        SiteValues site = new(snow, wind, seismic, frost, width, roofLive, from);
        if (site != Design.Sketch.Site)
        {
            ApplyRequest?.Invoke(new SetSite(site), "Set the site values");
        }
    }

    private static int? Whole(string? text, string what, string unit, List<string> wrong)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            return value;
        }

        wrong.Add($"{what} is a whole number of {unit}, like 30; \"{text.Trim()}\" is not.");
        return null;
    }

    private static Length? Distance(string? text, string what, bool allowZero, List<string> wrong)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (Length.TryParse(text, out Length value, out _) && (value > Length.Zero || (allowZero && value == Length.Zero)))
        {
            return value;
        }

        wrong.Add($"{what} is a length like 24' or 3' 6\"; \"{text.Trim()}\" is not.");
        return null;
    }
}

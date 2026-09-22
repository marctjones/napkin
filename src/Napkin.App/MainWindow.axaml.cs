using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Napkin.App.Designs;
using Design = Napkin.App.Designs.Design;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;

namespace Napkin.App;

/// <summary>
/// napkin's window: a menu, the drawing, and a status line.
/// </summary>
/// <remarks>
/// <para>
/// M1 is a viewer. The window opens a design, hands it to the canvas and reports where the view is;
/// it never edits anything, and there is no path from here to a <see cref="Sketch"/> that differs
/// from the one the design source produced.
/// </para>
/// <para>
/// Every design comes from a file. The Samples menu lists the scene files that shipped beside the
/// executable (<see cref="SampleFiles"/>) and <em>File &#x2192; Open&#x2026;</em> opens any other
/// one; both go through <see cref="FileDesignSource"/> and the same reader, so a sample and a file
/// a person picked are trusted exactly as far as each other.
/// </para>
/// <para>
/// <strong>A refusal changes nothing.</strong> <see cref="ShowDesign"/> loads before it assigns, so
/// a source that throws leaves the canvas, the view transform, the title and the status line as
/// they were, and every problem the reader found is listed in a panel over the drawing until the
/// person dismisses it.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    readonly List<MenuItem> _sampleItems = [];
    ISceneFilePicker _filePicker;
    bool _opening;

    public MainWindow()
    {
        InitializeComponent();

        _filePicker = new StorageProviderScenePicker(this);

        BuildSamplesMenu();
        BuildKeyBindings();

        DrawingCanvas.ViewChanged += (_, _) => UpdateZoomReadout();
        DrawingCanvas.PointerWorldPositionChanged += (_, point) => UpdateCursorReadout(point);
        RefusalPanel.PointerPressed += (_, e) =>
        {
            DismissRefusal();
            e.Handled = true;
        };

        ApplyRefusalPalette();
        ActualThemeVariantChanged += (_, _) => ApplyRefusalPalette();

        // The canvas takes focus when the window opens so the arrow keys steer the drawing, not
        // the menu bar. Anything a person clicks afterwards is welcome to take it.
        Opened += (_, _) => DrawingCanvas.Focus();

        // Keys that reach the window with the menu focused still steer the view, so arrowing after
        // a menu click does what it looks like it should.
        AddHandler(KeyDownEvent, OnShellKeyDown, RoutingStrategies.Bubble);

        if (Samples.Count > 0)
        {
            ShowDesign(Samples[0]);
        }
        else
        {
            // A build that shipped without its samples still starts, with an empty sheet and a
            // status line that says why rather than a stack trace.
            DesignText.Text =
                $"No sample designs found in {SampleFiles.SampleDirectory}. "
                + "Use File → Open… to open a scene file.";
        }

        UpdateZoomReadout();
        UpdateCursorReadout(null);
    }

    /// <summary>The sample scene files the Samples menu offers, in menu order.</summary>
    public IReadOnlyList<IDesignSource> Samples { get; } = SampleFiles.All;

    /// <summary>
    /// How <em>File &#x2192; Open&#x2026;</em> asks for a file. The platform's own dialog by
    /// default; the GUI suite substitutes one that answers with a path, because a native dialog is
    /// not something a headless test can drive.
    /// </summary>
    public ISceneFilePicker FilePicker
    {
        get => _filePicker;
        set => _filePicker = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The canvas, for the GUI suite to read the view transform off.</summary>
    public CanvasView Canvas => DrawingCanvas;

    /// <summary>The menu bar.</summary>
    public Menu MenuBar => MainMenu;

    /// <summary>The Samples menu, whose items are one per <see cref="Samples"/> entry.</summary>
    public MenuItem SamplesMenuItem => SamplesMenu;

    /// <summary>The <em>File &#x2192; Open&#x2026;</em> item.</summary>
    public MenuItem OpenMenuEntry => OpenMenuItem;

    /// <summary>The status line at the foot of the window.</summary>
    public Border StatusLine => StatusBar;

    /// <summary>The status line's description of the open design.</summary>
    public TextBlock DesignReadout => DesignText;

    /// <summary>The status line's cursor position, in feet, inches and fractions.</summary>
    public TextBlock CursorReadout => CursorText;

    /// <summary>The status line's zoom percentage.</summary>
    public TextBlock ZoomReadout => ZoomText;

    /// <summary>The panel that lists why a file was refused. Hidden until one is.</summary>
    public Border Refusal => RefusalPanel;

    /// <summary>Whether a refusal is on screen.</summary>
    public bool IsRefusalShowing => RefusalPanel.IsVisible;

    /// <summary>
    /// What the refusal panel is listing, one line per problem the reader found. Empty when no
    /// refusal is showing.
    /// </summary>
    public IReadOnlyList<string> RefusalProblems { get; private set; } = [];

    /// <summary>The line above the problems: which file could not be opened.</summary>
    public string RefusalHeadlineText => RefusalHeadline.Text ?? string.Empty;

    /// <summary>The design on screen, or null before the first one is opened.</summary>
    public Design? CurrentDesign => DrawingCanvas.Design;

    /// <summary>
    /// Opens a design and frames it.
    /// </summary>
    /// <remarks>
    /// A source that refuses — which is what the reader does to a file it does not trust — leaves
    /// the canvas exactly as it was and lists every problem in the refusal panel. Nothing is ever
    /// opened approximately, and the status line goes on describing what is really on screen.
    /// </remarks>
    /// <returns>Whether the design was opened.</returns>
    public bool ShowDesign(IDesignSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Design design;
        try
        {
            // Loaded first, assigned second: until this returns, nothing about the window has
            // changed, so there is no partly-opened state for a failure to leave behind.
            design = source.Load();
        }
        catch (DesignLoadException failure)
        {
            ShowRefusal(source.Name, failure.Problems);
            return false;
        }

        DismissRefusal();
        DrawingCanvas.Design = design;
        Title = $"napkin — {design.Name}";
        DesignText.Text = $"{design.Name} — {source.Description}";

        foreach (MenuItem item in _sampleItems)
        {
            item.Icon = ReferenceEquals(item.Tag, source)
                ? new TextBlock { Text = "✓" }
                : null;
        }

        return true;
    }

    /// <summary>
    /// Asks for a file and opens it. Cancelling changes nothing at all.
    /// </summary>
    /// <remarks>
    /// The picker is awaited rather than blocked on, because the platform dialog is asynchronous.
    /// Anything it throws becomes a visible refusal: the one thing this path must never do is take
    /// the application down between a person choosing a file and seeing what happened to it.
    /// </remarks>
    public async Task OpenFileAsync()
    {
        if (_opening)
        {
            return;
        }

        _opening = true;
        try
        {
            string? path = await FilePicker.PickSceneFileAsync().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(path))
            {
                ShowDesign(new FileDesignSource(path));
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ShowRefusal("the file you chose", [exception.Message]);
        }
        finally
        {
            _opening = false;
        }
    }

    /// <summary>Takes the refusal panel off the drawing.</summary>
    public void DismissRefusal()
    {
        if (!RefusalPanel.IsVisible)
        {
            return;
        }

        RefusalPanel.IsVisible = false;
        RefusalProblemList.Children.Clear();
        RefusalProblems = [];
        RefusalHeadline.Text = string.Empty;
    }

    void ShowRefusal(string what, IReadOnlyList<string> problems)
    {
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        RefusalProblems = [.. problems];
        RefusalHeadline.Text = problems.Count == 1
            ? $"Could not open {what}:"
            : $"Could not open {what} ({problems.Count} problems):";

        // One line per problem, all of them. Showing the first and hiding the rest would make a
        // file look like it had one thing wrong with it when it had four.
        RefusalProblemList.Children.Clear();
        foreach (string problem in RefusalProblems)
        {
            RefusalProblemList.Children.Add(new TextBlock
            {
                Text = problem,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(palette.Label),
            });
        }

        RefusalPanel.IsVisible = true;
    }

    /// <summary>
    /// Lights the refusal panel from the same palette the drawing uses, so it reads as a note on
    /// the paper in either theme.
    /// </summary>
    void ApplyRefusalPalette()
    {
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        RefusalPanel.Background = new SolidColorBrush(palette.Background);
        RefusalPanel.BorderBrush = new SolidColorBrush(palette.Dimension);
        RefusalHeadline.Foreground = new SolidColorBrush(palette.Dimension);
        RefusalDismissHint.Foreground = new SolidColorBrush(palette.Label);

        foreach (Control line in RefusalProblemList.Children)
        {
            if (line is TextBlock text)
            {
                text.Foreground = new SolidColorBrush(palette.Label);
            }
        }
    }

    void BuildSamplesMenu()
    {
        KeyModifiers command = CommandModifier;
        for (int i = 0; i < Samples.Count; i++)
        {
            IDesignSource source = Samples[i];
            MenuItem item = new()
            {
                Header = source.Name,
                Tag = source,
            };

            // The first nine samples get a command-digit shortcut; past that the menu is the way
            // in, because there is no tenth digit to give.
            if (i < 9)
            {
                item.InputGesture = new KeyGesture(Key.D1 + i, command);
            }

            item.Click += (_, _) => ShowDesign(source);
            _sampleItems.Add(item);
        }

        SamplesMenu.ItemsSource = _sampleItems;
        OpenMenuItem.InputGesture = new KeyGesture(Key.O, command);
        ZoomToFitMenuItem.InputGesture = new KeyGesture(Key.D0, command);
        ZoomInMenuItem.InputGesture = new KeyGesture(Key.OemPlus);
        ZoomOutMenuItem.InputGesture = new KeyGesture(Key.OemMinus);
    }

    void BuildKeyBindings()
    {
        KeyModifiers command = CommandModifier;
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.O, command),
            Command = new RelayCommand(() => _ = OpenFileAsync()),
        });

        for (int i = 0; i < Samples.Count && i < 9; i++)
        {
            IDesignSource source = Samples[i];
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.D1 + i, command),
                Command = new RelayCommand(() => ShowDesign(source)),
            });
        }
    }

    /// <summary>
    /// Control on Windows, Command on macOS, read from the platform rather than hardcoded.
    /// </summary>
    static KeyModifiers CommandModifier =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers
        ?? KeyModifiers.Control;

    void OnShellKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape && IsRefusalShowing)
        {
            DismissRefusal();
            e.Handled = true;
            return;
        }

        if (!DrawingCanvas.IsFocused && DrawingCanvas.HandleViewKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    void OnOpenClicked(object? sender, RoutedEventArgs e) => _ = OpenFileAsync();

    void OnZoomToFitClicked(object? sender, RoutedEventArgs e) => DrawingCanvas.ZoomToFit();

    void OnZoomInClicked(object? sender, RoutedEventArgs e) => DrawingCanvas.ZoomIn();

    void OnZoomOutClicked(object? sender, RoutedEventArgs e) => DrawingCanvas.ZoomOut();

    void OnExitClicked(object? sender, RoutedEventArgs e) => Close();

    void UpdateZoomReadout() => ZoomText.Text = string.Create(
        CultureInfo.InvariantCulture,
        $"Zoom {DrawingCanvas.View.ZoomPercent:0.#}%");

    void UpdateCursorReadout(Point2? point) => CursorText.Text = point is { } position
        ? $"x {Show(position.X)}   y {Show(position.Y)}"
        : "x —   y —";

    /// <summary>
    /// A coordinate as a person reads a tape measure, marked with &#x2248; when the text is not the
    /// stored value (docs/design/geometry-model.md &#xA7;1.4).
    /// </summary>
    string Show(Length value)
    {
        FormattedLength formatted = value.Format(DrawingCanvas.LabelFormat);
        return formatted.IsExact ? formatted.Text : "≈" + formatted.Text;
    }
}

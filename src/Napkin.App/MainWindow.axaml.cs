using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
/// The samples come from <see cref="BuiltInDesigns.All"/> through <see cref="IDesignSource"/>. When
/// the scene reader (#6) lands, <em>File &#x2192; Open&#x2026;</em> becomes a second implementation
/// of that interface and <see cref="ShowDesign"/> is unchanged — including its failure path, which
/// already leaves whatever is on screen alone and says what was wrong.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    readonly List<MenuItem> _sampleItems = [];

    public MainWindow()
    {
        InitializeComponent();

        BuildSamplesMenu();
        BuildKeyBindings();

        DrawingCanvas.ViewChanged += (_, _) => UpdateZoomReadout();
        DrawingCanvas.PointerWorldPositionChanged += (_, point) => UpdateCursorReadout(point);

        // The canvas takes focus when the window opens so the arrow keys steer the drawing, not
        // the menu bar. Anything a person clicks afterwards is welcome to take it.
        Opened += (_, _) => DrawingCanvas.Focus();

        // Keys that reach the window with the menu focused still steer the view, so arrowing after
        // a menu click does what it looks like it should.
        AddHandler(KeyDownEvent, OnShellKeyDown, RoutingStrategies.Bubble);

        ShowDesign(Samples[0]);
        UpdateZoomReadout();
        UpdateCursorReadout(null);
    }

    /// <summary>The designs the Samples menu offers.</summary>
    public IReadOnlyList<IDesignSource> Samples { get; } = BuiltInDesigns.All;

    /// <summary>The canvas, for the GUI suite to read the view transform off.</summary>
    public CanvasView Canvas => DrawingCanvas;

    /// <summary>The menu bar.</summary>
    public Menu MenuBar => MainMenu;

    /// <summary>The Samples menu, whose items are one per <see cref="Samples"/> entry.</summary>
    public MenuItem SamplesMenuItem => SamplesMenu;

    /// <summary>The status line at the foot of the window.</summary>
    public Border StatusLine => StatusBar;

    /// <summary>The status line's description of the open design.</summary>
    public TextBlock DesignReadout => DesignText;

    /// <summary>The status line's cursor position, in feet, inches and fractions.</summary>
    public TextBlock CursorReadout => CursorText;

    /// <summary>The status line's zoom percentage.</summary>
    public TextBlock ZoomReadout => ZoomText;

    /// <summary>The design on screen, or null before the first one is opened.</summary>
    public Design? CurrentDesign => DrawingCanvas.Design;

    /// <summary>
    /// Opens a design and frames it.
    /// </summary>
    /// <remarks>
    /// A source that refuses — which is what the reader will do to a file it does not trust —
    /// leaves the canvas exactly as it was and says what was wrong on the status line. Nothing is
    /// ever opened approximately.
    /// </remarks>
    public void ShowDesign(IDesignSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Design design;
        try
        {
            design = source.Load();
        }
        catch (DesignLoadException failure)
        {
            DesignText.Text = $"Could not open {source.Name}: {failure.Message}";
            return;
        }

        DrawingCanvas.Design = design;
        Title = $"napkin — {design.Name}";
        DesignText.Text = $"{design.Name} — {source.Description}";

        foreach (MenuItem item in _sampleItems)
        {
            item.Icon = ReferenceEquals(item.Tag, source)
                ? new TextBlock { Text = "✓" }
                : null;
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
                InputGesture = new KeyGesture(Key.D1 + i, command),
            };
            item.Click += (_, _) => ShowDesign(source);
            _sampleItems.Add(item);
        }

        SamplesMenu.ItemsSource = _sampleItems;
        ZoomToFitMenuItem.InputGesture = new KeyGesture(Key.D0, command);
        ZoomInMenuItem.InputGesture = new KeyGesture(Key.OemPlus);
        ZoomOutMenuItem.InputGesture = new KeyGesture(Key.OemMinus);
    }

    void BuildKeyBindings()
    {
        KeyModifiers command = CommandModifier;
        for (int i = 0; i < Samples.Count; i++)
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
        if (!e.Handled && !DrawingCanvas.IsFocused && DrawingCanvas.HandleViewKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

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

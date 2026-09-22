using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Napkin.App.Designs;
using Design = Napkin.App.Designs.Design;
using Napkin.App.Editing;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;

namespace Napkin.App;

/// <summary>
/// napkin's window: a menu, the drawing, and a status line.
/// </summary>
/// <remarks>
/// <para>
/// The window opens a design, hands it to the <see cref="DesignEditor"/> the canvas draws and
/// edits, and reports what the last edit did. It never touches geometry itself: every change in
/// the application is a <see cref="Request"/> put to an <see cref="IGeometryUpdater"/> by the
/// editor, and nothing here — or in the canvas — can reach an entity to change it (CVS-005).
/// </para>
/// <para>
/// Every design comes from a file, or from <em>File &#x2192; New</em>. The Samples menu lists the
/// scene files that shipped beside the executable (<see cref="SampleFiles"/>) and
/// <em>File &#x2192; Open&#x2026;</em> opens any other one; both go through
/// <see cref="FileDesignSource"/> and the same reader, so a sample and a file a person picked are
/// trusted exactly as far as each other.
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
    CutListWindow? _cutList;
    bool _opening;
    EntityId? _editingBox;
    SizeAxis _editingAxis = SizeAxis.Width;

    public MainWindow()
    {
        InitializeComponent();

        _filePicker = new StorageProviderScenePicker(this);

        DrawingCanvas.Editor = Editor;
        Editor.MessageChanged += (_, _) => UpdateMessageBar();
        Editor.DesignChanged += (_, _) => OnDesignChanged();
        Editor.SelectionChanged += (_, _) => OnSelectionChanged();

        BuildSamplesMenu();
        BuildKeyBindings();

        DrawingCanvas.ViewChanged += (_, _) =>
        {
            UpdateZoomReadout();
            PlaceDimensionEditor();
        };
        DrawingCanvas.PointerWorldPositionChanged += (_, point) => UpdateCursorReadout(point);
        DrawingCanvas.ToolChanged += (_, _) => UpdateToolButtons();
        DrawingCanvas.DimensionEditRequested += (_, request) =>
            OpenDimensionEditor(request.Box, request.Axis);

        RefusalPanel.PointerPressed += (_, e) =>
        {
            DismissRefusal();
            e.Handled = true;
        };

        DimensionEntryBox.KeyDown += OnDimensionEntryKeyDown;
        DimensionEntryBox.LostFocus += (_, _) => CloseDimensionEditor(focusCanvas: false);

        ApplyEditingPalette();
        ActualThemeVariantChanged += (_, _) => ApplyEditingPalette();

        // The canvas takes focus when the window opens so the keys steer the drawing, not the
        // menu bar. Anything a person clicks afterwards is welcome to take it.
        Opened += (_, _) => DrawingCanvas.Focus();

        // The cut list is a reading of this drawing, so it goes when the drawing does rather than
        // being left behind as a window with no design under it.
        Closed += (_, _) => _cutList?.Close();

        // Keys that reach the window with the menu focused still steer the view, so arrowing after
        // a menu click does what it looks like it should.
        AddHandler(KeyDownEvent, OnShellKeyDown, RoutingStrategies.Bubble);

        if (Samples.Count > 0)
        {
            ShowDesign(Samples[0]);
        }
        else
        {
            // A build that shipped without its samples still starts, on a blank sheet, with a
            // status line that says why rather than a stack trace.
            ShowDesign(new NewSheet());
            DesignText.Text =
                $"No sample designs found in {SampleFiles.SampleDirectory}. "
                + "Use File → Open… to open a scene file, or press R and drag to draw a part.";
        }

        UpdateToolButtons();
        UpdateZoomReadout();
        UpdateCursorReadout(null);
    }

    /// <summary>The drawing being edited, and the one place a sketch is ever replaced.</summary>
    public DesignEditor Editor { get; } = new();

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

    /// <summary>The <em>File &#x2192; New</em> item.</summary>
    public MenuItem NewMenuEntry => NewMenuItem;

    /// <summary>The tool control that floats over the drawing.</summary>
    public Border ToolControl => ToolBar;

    /// <summary>The rectangle tool's button.</summary>
    public ToggleButton RectangleToolControl => RectangleToolButton;

    /// <summary>The select tool's button.</summary>
    public ToggleButton SelectToolControl => SelectToolButton;

    /// <summary>The status line at the foot of the window.</summary>
    public Border StatusLine => StatusBar;

    /// <summary>The status line's description of the open design.</summary>
    public TextBlock DesignReadout => DesignText;

    /// <summary>The status line's cursor position, in feet, inches and fractions.</summary>
    public TextBlock CursorReadout => CursorText;

    /// <summary>The status line's zoom percentage.</summary>
    public TextBlock ZoomReadout => ZoomText;

    /// <summary>What the last edit did, as it is showing now. Empty when nothing is showing.</summary>
    public string MessageOnScreen => MessageBar.IsVisible ? MessageText.Text ?? string.Empty : string.Empty;

    /// <summary>Whether the last message offers a way out of a conflict.</summary>
    public bool IsOfferingToRemoveRelationship => MessageOfferButton.IsVisible;

    /// <summary>The wording of that offer.</summary>
    public string RemoveOfferText => MessageOfferButton.Content as string ?? string.Empty;

    /// <summary>The button that takes the offer.</summary>
    public Button RemoveOfferButton => MessageOfferButton;

    /// <summary>The relationship list panel.</summary>
    public Border Relationships => RelationshipsPanel;

    /// <summary>What the relationship list is showing, one line each.</summary>
    public IReadOnlyList<string> RelationshipsOnScreen =>
    [
        .. RelationshipsList.Children.OfType<TextBlock>().Select(line => line.Text ?? string.Empty),
    ];

    /// <summary>The inline dimension field, when one is open.</summary>
    public TextBox DimensionField => DimensionEntryBox;

    /// <summary>Whether a dimension is open for typing.</summary>
    public bool IsEditingDimension => DimensionEditor.IsVisible;

    /// <summary>What the dimension field is complaining about, or empty when it is not.</summary>
    public string DimensionFieldError =>
        DimensionEditorError.IsVisible ? DimensionEditorError.Text ?? string.Empty : string.Empty;

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

    /// <summary>The design on screen.</summary>
    public Design? CurrentDesign => Editor.Design;

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
        CloseDimensionEditor(focusCanvas: false);
        Editor.Open(design);
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

    /// <summary>Starts a blank sheet.</summary>
    public void NewSheetCommand() => ShowDesign(new NewSheet());

    /// <summary>The cut-list window, when one is open.</summary>
    public CutListWindow? CutList => _cutList;

    /// <summary>The <em>View &#x2192; Cut list</em> item.</summary>
    public MenuItem CutListMenuEntry => CutListMenuItem;

    /// <summary>
    /// Opens the cut list for the design on screen, or brings the open one forward.
    /// </summary>
    /// <remarks>
    /// One window, not one per invocation: a second cut list of the same design would be two
    /// things to keep in step and nothing to gain by it. It is not modal — the drawing goes on
    /// being edited with it open, and every edit re-reads it.
    /// </remarks>
    /// <returns>The window.</returns>
    public CutListWindow OpenCutList()
    {
        if (_cutList is null)
        {
            _cutList = new CutListWindow();
            _cutList.Closed += (_, _) => _cutList = null;
        }

        _cutList.ShowDesign(CurrentDesign);
        _cutList.Show(this);
        _cutList.Activate();
        return _cutList;
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

    /// <summary>
    /// Opens one of a selected part's dimensions for typing, over the label it is editing.
    /// </summary>
    public void OpenDimensionEditor(EntityId box, SizeAxis axis)
    {
        if (Editor.Design.Sketch.Find<Box>(box) is not { } part)
        {
            return;
        }

        _editingBox = box;
        _editingAxis = axis;

        Length current = axis == SizeAxis.Width ? part.Width : part.Height;
        DimensionEditorCaption.Text = $"{Editor.NameOf(box)} — {(axis == SizeAxis.Width ? "width" : "height")}";
        DimensionEntryBox.Text = current.Format(Editor.LabelFormat).Text;
        DimensionEditorError.IsVisible = false;
        DimensionEditor.IsVisible = true;

        PlaceDimensionEditor();
        DimensionEntryBox.Focus();
        DimensionEntryBox.SelectAll();
    }

    /// <summary>
    /// Applies what is in the dimension field.
    /// </summary>
    /// <remarks>
    /// Text that is not a length is explained beside the field and changes nothing at all — the
    /// part keeps the size it had and the next entry is read exactly as if the bad one had never
    /// happened (GUI-DRAW-03). A conflict is explained the same way, in the same place, because
    /// from where the person is sitting the two are the same event: what I typed did not take.
    /// </remarks>
    /// <returns>Whether the drawing changed.</returns>
    public bool ApplyDimensionEntry()
    {
        if (_editingBox is not { } box)
        {
            return false;
        }

        if (DimensionEntry.Interpret(DimensionEntryBox.Text) is not ReadableLength readable)
        {
            UnreadableText unreadable = (UnreadableText)DimensionEntry.Interpret(DimensionEntryBox.Text);
            ShowDimensionError(unreadable.Message);
            Editor.Say(EditSeverity.Problem, unreadable.Message);
            return false;
        }

        ParamRef size = SelectionDimensions.ParamFor(box, _editingAxis);
        string what = $"Set {Editor.NameOf(box)}'s {(_editingAxis == SizeAxis.Width ? "width" : "height")} "
                      + $"to {readable.Value.Format(Editor.LabelFormat).Text}";

        Editor.BeginGesture(what);
        UpdateResult result = Editor.Apply(
            DimensionEntry.RequestFor(Editor.Design.Sketch, size, readable.Value),
            what);
        Editor.EndGesture();

        if (result is Succeeded)
        {
            CloseDimensionEditor(focusCanvas: true);
            return true;
        }

        // It did not take. The message bar has the whole explanation; the field repeats it where
        // the person is looking, and keeps what they typed so they can change one character.
        ShowDimensionError(Editor.LastMessage?.Text ?? "That did not take.");
        return false;
    }

    /// <summary>Closes the dimension field without applying anything.</summary>
    public void CloseDimensionEditor(bool focusCanvas)
    {
        if (!DimensionEditor.IsVisible)
        {
            return;
        }

        DimensionEditor.IsVisible = false;
        DimensionEditorError.IsVisible = false;
        _editingBox = null;

        if (focusCanvas)
        {
            DrawingCanvas.Focus();
        }
    }

    /// <summary>Takes a conflict's way out: removes the relationship the message offered.</summary>
    public void TakeRemoveOffer()
    {
        if (Editor.LastMessage?.OfferToRemove is not { } id)
        {
            return;
        }

        string what = $"Removed: {RelationshipText.Describe(Editor.Design.Sketch, Editor.Design.Sketch.Find(id)!, Editor.NameOf, Editor.LabelFormat)}";
        Editor.BeginGesture(what);
        Editor.Apply(new RemoveRelationship(id), what);
        Editor.EndGesture();
    }

    void OnDimensionEntryKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ApplyDimensionEntry();
                e.Handled = true;
                break;

            case Key.Escape:
                CloseDimensionEditor(focusCanvas: true);
                e.Handled = true;
                break;
        }
    }

    void ShowDimensionError(string message)
    {
        DimensionEditorError.Text = message;
        DimensionEditorError.IsVisible = true;
        DimensionEntryBox.Focus();
        DimensionEntryBox.SelectAll();
    }

    /// <summary>Puts the dimension field over the label it is editing.</summary>
    void PlaceDimensionEditor()
    {
        if (!DimensionEditor.IsVisible || _editingBox is not { } box)
        {
            return;
        }

        Point at = DrawingCanvas.SelectionDimensionLabelAt(box, _editingAxis)
                   ?? new Point(DrawingCanvas.Bounds.Width / 2, DrawingCanvas.Bounds.Height / 2);

        double left = Math.Clamp(at.X - 80, 8, Math.Max(8, DrawingCanvas.Bounds.Width - 260));
        double top = Math.Clamp(at.Y + 10, 8, Math.Max(8, DrawingCanvas.Bounds.Height - 120));

        Avalonia.Controls.Canvas.SetLeft(DimensionEditor, left);
        Avalonia.Controls.Canvas.SetTop(DimensionEditor, top);
    }

    void OnDesignChanged()
    {
        UpdateRelationships();
        PlaceDimensionEditor();

        // The cut list follows the drawing: widen a part with the list open and the row changes,
        // because both are readings of one design rather than a drawing and a snapshot of it.
        _cutList?.ShowDesign(CurrentDesign);
    }

    void OnSelectionChanged()
    {
        if (_editingBox is { } box && !Editor.Selection.Contains(box))
        {
            CloseDimensionEditor(focusCanvas: false);
        }

        UpdateMenuEnablement();
    }

    void UpdateMenuEnablement()
    {
        bool anything = Editor.Selection.Count > 0;
        DeleteMenuItem.IsEnabled = anything;
        PinMenuItem.IsEnabled = anything;
    }

    void UpdateMessageBar()
    {
        EditMessage? message = Editor.LastMessage;
        if (message is null)
        {
            MessageBar.IsVisible = false;
            MessageOfferButton.IsVisible = false;
            return;
        }

        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        MessageText.Text = message.Text;
        MessageText.Foreground = new SolidColorBrush(message.Severity switch
        {
            EditSeverity.Problem => palette.Snap,
            EditSeverity.Hint => palette.Dimension,
            _ => palette.Label,
        });

        MessageOfferButton.IsVisible = message.OfferToRemove is not null;
        MessageOfferButton.Content = message.OfferText ?? string.Empty;
        MessageBar.IsVisible = true;
    }

    void UpdateRelationships()
    {
        IReadOnlyList<RelationshipEntry> entries = Editor.RelationshipEntries();
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);

        RelationshipsList.Children.Clear();
        foreach (RelationshipEntry entry in entries)
        {
            RelationshipsList.Children.Add(new TextBlock
            {
                Text = entry.Text,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(palette.Label),
            });
        }

        RelationshipsHeadline.Text = entries.Count == 1
            ? "Relationships — 1"
            : $"Relationships — {entries.Count}";
        RelationshipsPanel.IsVisible = entries.Count > 0;
    }

    void UpdateToolButtons()
    {
        SelectToolButton.IsChecked = DrawingCanvas.Tool == EditTool.Select;
        RectangleToolButton.IsChecked = DrawingCanvas.Tool == EditTool.Rectangle;
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
    /// Lights the panels that sit on the drawing from the same palette the drawing uses, so they
    /// read as notes on the paper in either theme.
    /// </summary>
    void ApplyEditingPalette()
    {
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        SolidColorBrush paper = new(palette.Background);
        SolidColorBrush edge = new(palette.Dimension);

        RefusalPanel.Background = paper;
        RefusalPanel.BorderBrush = edge;
        RefusalHeadline.Foreground = edge;
        RefusalDismissHint.Foreground = new SolidColorBrush(palette.Label);

        ToolBar.Background = paper;
        ToolBar.BorderBrush = new SolidColorBrush(palette.GridMajor);

        RelationshipsPanel.Background = paper;
        RelationshipsPanel.BorderBrush = new SolidColorBrush(palette.GridMajor);
        RelationshipsHeadline.Foreground = edge;

        DimensionEditor.Background = paper;
        DimensionEditor.BorderBrush = new SolidColorBrush(palette.Selection);
        DimensionEditorCaption.Foreground = new SolidColorBrush(palette.Selection);
        DimensionEditorError.Foreground = new SolidColorBrush(palette.Snap);
        DimensionEditorHint.Foreground = new SolidColorBrush(palette.Label);

        foreach (Control line in RefusalProblemList.Children)
        {
            if (line is TextBlock text)
            {
                text.Foreground = new SolidColorBrush(palette.Label);
            }
        }

        UpdateMessageBar();
        UpdateRelationships();
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
        NewMenuItem.InputGesture = new KeyGesture(Key.N, command);
        OpenMenuItem.InputGesture = new KeyGesture(Key.O, command);
        ZoomToFitMenuItem.InputGesture = new KeyGesture(Key.D0, command);
        ZoomInMenuItem.InputGesture = new KeyGesture(Key.OemPlus);
        ZoomOutMenuItem.InputGesture = new KeyGesture(Key.OemMinus);
        SelectToolMenuItem.InputGesture = new KeyGesture(Key.S);
        RectangleToolMenuItem.InputGesture = new KeyGesture(Key.R);
        PinMenuItem.InputGesture = new KeyGesture(Key.P);
        DeleteMenuItem.InputGesture = new KeyGesture(Key.Delete);
        UpdateMenuEnablement();
    }

    void BuildKeyBindings()
    {
        KeyModifiers command = CommandModifier;
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.N, command),
            Command = new RelayCommand(NewSheetCommand),
        });
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.O, command),
            Command = new RelayCommand(() => _ = OpenFileAsync()),
        });
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.L, command),
            Command = new RelayCommand(() => OpenCutList()),
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

        if (!DrawingCanvas.IsFocused && !IsEditingDimension
            && DrawingCanvas.HandleViewKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    void OnNewClicked(object? sender, RoutedEventArgs e) => NewSheetCommand();

    void OnOpenClicked(object? sender, RoutedEventArgs e) => _ = OpenFileAsync();

    void OnSelectToolClicked(object? sender, RoutedEventArgs e)
    {
        DrawingCanvas.Tool = EditTool.Select;
        UpdateToolButtons();
        DrawingCanvas.Focus();
    }

    void OnRectangleToolClicked(object? sender, RoutedEventArgs e)
    {
        DrawingCanvas.Tool = EditTool.Rectangle;
        UpdateToolButtons();
        DrawingCanvas.Focus();
    }

    void OnPinClicked(object? sender, RoutedEventArgs e) => DrawingCanvas.PinSelection();

    void OnDeleteClicked(object? sender, RoutedEventArgs e) => DrawingCanvas.DeleteSelection();

    void OnMessageOfferClicked(object? sender, RoutedEventArgs e) => TakeRemoveOffer();

    void OnCutListClicked(object? sender, RoutedEventArgs e) => OpenCutList();

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

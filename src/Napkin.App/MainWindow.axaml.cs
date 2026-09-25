using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Napkin.App.Designs;
using Design = Napkin.Modules.Editing.Design;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Napkin.Modules.Furniture;
using Napkin.Modules.Editing;

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
    /// <summary>
    /// What the properties panel offers when a plain box is about to become a part: lying flat,
    /// its depth its thickness. Nothing about a box says which it is, so something has to be first.
    /// </summary>
    static readonly Part DefaultPart = new(
        Stock: null,
        Species: null,
        Quantity: 1,
        new PlanAxes(PartDimension.Length, PartDimension.Width));

    /// <summary>How much of the drawing's right side the side column can cover, in pixels (#90).</summary>
    const double SidePanelsReserve = 280;

    readonly List<MenuItem> _sampleItems = [];
    ISceneFilePicker _filePicker;
    CutListWindow? _cutList;
    bool _opening;
    bool _saving;
    bool _closeConfirmed;
    string? _documentPath;
    Func<Task>? _afterAnswer;
    bool _showingProperties;
    RelationshipEntry? _rowUnderPointer;
    Box? _propertiesShown;
    EntityId? _editingBox;
    SizeAxis _editingAxis = SizeAxis.Width;

    /// <summary>The window on the person's real settings file.</summary>
    public MainWindow() : this(SettingsStore.ForUser())
    {
    }

    /// <summary>The window on a given settings store; tests pass one that is not the person's.</summary>
    public MainWindow(SettingsStore settings)
    {
        Settings = settings;
        InitializeComponent();
        ExtendTitleBarIntoBench();

        _filePicker = new StorageProviderScenePicker(this);

        DrawingCanvas.Editor = Editor;

        // The side column's widest (the list's MaxWidth) and its margins: both views frame the
        // drawing beside it rather than under it (#90).
        DrawingCanvas.FitReserveRight = SidePanelsReserve;
        ModelDrawing.FitReserveRight = SidePanelsReserve;
        Editor.MessageChanged += (_, _) =>
        {
            // A change of a header result is said with the edit that caused it (#18, design §7.3).
            if (SayRecompute())
            {
                return;
            }

            UpdateMessageBar();
            UpdateAttention();
        };
        Editor.DesignOpened += (_, _) => ResetRecompute();
        Editor.DesignChanged += (_, _) => OnDesignChanged();
        Editor.SelectionChanged += (_, _) => OnSelectionChanged();
        Editor.History.Changed += (_, _) => UpdateMenuEnablement();

        BuildSamplesMenu();
        BuildKeyBindings();
        ApplySettings();

        // The window opens on the plan; View says so the way Samples marks the open sample.
        ShowViewChrome();

        DrawingCanvas.ViewChanged += (_, _) =>
        {
            UpdateZoomReadout();
            PlaceDimensionEditor();
        };
        // Only the view on screen speaks for the pointer: the plan, hidden as another view shows, says the
        // pointer left it, and must not overwrite that view's readout.
        DrawingCanvas.PointerWorldPositionChanged += (_, point) =>
        {
            if (!IsShowingModel)
            {
                UpdateCursorReadout(point);
            }
        };
        DrawingCanvas.ToolChanged += (_, _) => UpdateToolButtons();
        DrawingCanvas.HoveredPartChanged += (_, _) => UpdateRelationships();
        DrawingCanvas.DimensionEditRequested += (_, request) =>
            OpenDimensionEditor(request.Box, request.Axis);
        WireJoinery();
        WireRough();
        WireFirmUp();
        DrawingCanvas.CommandRequested += (_, request) => request.Handled = Run(request.Command);
        ModelDrawing.CommandRequested += (_, request) => request.Handled = Run(request.Command);
        DrawingCanvas.ViewRequested += (_, view) => ShowView(view);

        // The 3D view: the same editor, so the same drawing, selection and undo (assembly-model
        // §8.1). What it asks of the selection is done by the plan canvas's own commands, so there
        // is one Delete, one Pin, one Duplicate.
        ModelDrawing.Editor = Editor;
        ModelDrawing.ViewChanged += (_, _) => UpdateZoomReadout();
        ModelDrawing.HoveredPartChanged += (_, _) => UpdateRelationships();
        ModelDrawing.PointerModelPositionChanged += (_, point) =>
        {
            if (IsShowingModel)
            {
                UpdateCursorReadout(point);
            }
        };
        ModelDrawing.ViewRequested += (_, view) => ShowView(view);
        ModelDrawing.PlacementChanged += (_, _) => UpdateToolButtons();
        StockToolboxPanel.ItemPicked += (_, item) => PickStock(item);
        StockToolboxPanel.CategoryChanged += (_, _) => OnStockCategoryChanged();

        // The stock category icons sit on the main toolbar beside Select and Rectangle, always in
        // reach; the toolbox itself is only the drawer that drops down under the one picked.
        ToolRow.Children.Add(StockToolboxPanel.CategoryRow);
        BuildStockMenu();

        // A click on a category icon or an item gives the keyboard back to the drawing, so Escape
        // still reaches it; the toolbox has nothing to type into.
        ToolBar.AddHandler(Button.ClickEvent, (_, _) => FocusDrawing());
        StockToolboxPanel.AddHandler(Button.ClickEvent, (_, _) => FocusDrawing());

        WireWorkshop();

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
        Opened += (_, _) => FocusDrawing();

        // The cut list is a reading of this drawing, so it goes when the drawing does rather than
        // being left behind as a window with no design under it.
        Closed += (_, _) => _cutList?.Close();

        // Keys that reach the window with the menu focused still steer the view, so arrowing after
        // a menu click does what it looks like it should.
        AddHandler(KeyDownEvent, OnShellKeyDown, RoutingStrategies.Bubble);

        // The list takes up to a third of the side column — never less than about three rows — and
        // the Part panel, below it, the rest (#73, #78).
        SidePanels.SizeChanged += (_, e) =>
            RelationshipsPanel.MaxHeight = Math.Clamp(e.NewSize.Height / 3, 96, 320);

        // Enter in any of the Part panel's text fields applies the panel, the way the dimension
        // field applies on Enter: typing a number and reaching for the mouse is a step too many (#78).
        // The hardware box takes several lines, so Enter there is a new line and Ctrl+Enter applies.
        PropertiesPanel.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && e.Source is TextBox box
                && (!box.AcceptsReturn || e.KeyModifiers.HasFlag(CommandModifier)))
            {
                ApplyProperties();
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // The backdrop behind the save question takes every press, so nothing on the paper can be
        // edited while it waits for an answer.
        UnsavedBackdrop.PointerPressed += (_, e) => e.Handled = true;

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
        UpdateCursorReadout((Point2?)null);
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

    /// <summary>The wall tool's button (#18).</summary>
    public ToggleButton WallToolControl => WallToolButton;

    /// <summary>The select tool's button.</summary>
    public ToggleButton SelectToolControl => SelectToolButton;

    /// <summary>The toolbar's Shape button, which opens the selected part in the shape workshop.</summary>
    public Button ShapeToolControl => ShapeToolButton;

    /// <summary>The toolbar's Duplicate button.</summary>
    public Button DuplicateToolControl => DuplicateToolButton;

    /// <summary>The toolbar's Pin in place button.</summary>
    public Button PinToolControl => PinToolButton;

    /// <summary>The toolbar's Delete button.</summary>
    public Button DeleteToolControl => DeleteToolButton;

    /// <summary>
    /// Every icon button on the toolbar ahead of the stock categories, in the order it shows them:
    /// the tools, then the actions on the selection.
    /// </summary>
    public IReadOnlyList<Button> ToolButtons =>
    [
        SelectToolButton,
        RectangleToolButton,
        WallToolButton,
        ShapeToolButton,
        DuplicateToolButton,
        PinToolButton,
        DeleteToolButton,
    ];

    /// <summary>The <em>Draw</em> menu, which reaches everything the toolbar does.</summary>
    public MenuItem DrawMenuItem => DrawMenu;

    /// <summary>
    /// <em>Draw &#x2192; Stock</em>: one submenu per stock category, in the toolbar's order, each
    /// holding one item per size.
    /// </summary>
    public MenuItem StockMenuItem => StockMenu;

    /// <summary>The stock toolbox: its drawer, and through it the category icons on the toolbar.</summary>
    public StockToolbox Toolbox => StockToolboxPanel;

    /// <summary>Whether a stock category's drawer of sizes is open under its icon.</summary>
    public bool IsShowingStockSizes => StockToolboxPanel.IsVisible;

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

    /// <summary>Whether the last message offers a way out: of a conflict, or of a refused turn.</summary>
    public bool IsOfferingAWayOut => MessageOfferButton.IsVisible;

    /// <summary>The wording of that offer.</summary>
    public string OfferText => MessageOfferButton.Content as string ?? string.Empty;

    /// <summary>The button that takes the offer.</summary>
    public Button OfferButton => MessageOfferButton;

    /// <summary>The relationship list panel.</summary>
    public Border Relationships => RelationshipsPanel;

    /// <summary>
    /// What the relationship list is showing, one line each. Empty while it is collapsed to its
    /// badge, because then no sentence is on the screen.
    /// </summary>
    public IReadOnlyList<string> RelationshipsOnScreen =>
    [
        .. RelationshipsList.Children.OfType<Grid>().Select(row => row.Children.OfType<TextBlock>().First().Text ?? string.Empty),
    ];

    /// <summary>The row of the relationship list that says this, when the list is open.</summary>
    public Control? RelationshipRow(string text) =>
        RelationshipsList.Children.OfType<Grid>().FirstOrDefault(row => row.Children.OfType<TextBlock>().First().Text == text);

    /// <summary>The button on a row of the relationship list that removes what the row says.</summary>
    public Button? RemoveRelationshipButton(string text) =>
        RelationshipRow(text) is Grid row ? row.Children.OfType<Button>().FirstOrDefault() : null;

    /// <summary>Whether the relationship list is open to its sentences rather than showing only its count.</summary>
    public bool IsRelationshipListExpanded => RelationshipsPanel.IsVisible && RelationshipsList.IsVisible;

    /// <summary>What the relationship panel's headline says: the count badge, or the list's title.</summary>
    public string RelationshipHeadlineText =>
        RelationshipsPanel.IsVisible ? RelationshipsHeadline.Text ?? string.Empty : string.Empty;

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

        // A file a person opened is where Save writes back to. A sample is not: it ships beside
        // the executable, and a plain Save must not quietly overwrite it — the first Save of an
        // edited sample asks where to put the copy, as a new sheet's does.
        _documentPath = source is FileDesignSource file && !Samples.Contains(source) ? file.Path : null;
        UpdateTitle();
        DesignText.Text = $"{design.Name} — {source.Description}";

        foreach (MenuItem item in _sampleItems)
        {
            item.Icon = ReferenceEquals(item.Tag, source)
                ? new TextBlock { Text = "✓" }
                : null;
        }

        // The new design starts in the view the person chose for new designs (or was last in).
        ShowView(Settings.Current.ViewForNewDesign());

        return true;
    }

    /// <summary>Starts a blank sheet — after asking about unsaved changes, if there are any.</summary>
    public void NewSheetCommand() =>
        _ = WhenChangesAreSafe("starting a new sheet", () => Now(() => ShowDesign(new NewSheet())));

    /// <summary>
    /// Opens one of the samples — after asking about unsaved changes, if there are any.
    /// </summary>
    public void OpenSampleCommand(IDesignSource sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        _ = WhenChangesAreSafe($"opening {sample.Name}", () => Now(() => ShowDesign(sample)));
    }

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
            _cutList = new CutListWindow
            {
                ApplyRequest = (request, what) => Editor.Apply(request, what),
                Packs = Packs,
                KerfChanged = kerf => Settings.Update(settings => settings with { SawKerf = kerf }),
            };
            _cutList.SawKerf = Settings.Current.SawKerf;
            _cutList.Closed += (_, _) => _cutList = null;
        }

        _cutList.ShowDesign(CurrentDesign);
        _cutList.Show(this);
        _cutList.Activate();
        return _cutList;
    }

    /// <summary>
    /// Opens the cut-list window on its shopping-list tab (issue #9), or brings the open one forward
    /// and shows that tab.
    /// </summary>
    /// <returns>The window.</returns>
    public CutListWindow OpenShoppingList()
    {
        CutListWindow window = OpenCutList();
        window.ShowShoppingList();
        return window;
    }

    /// <summary>Opens the cut-list window on its cut-layout tab (issue #138), or shows that tab on the open one.</summary>
    /// <returns>The window.</returns>
    public CutListWindow OpenCutLayout()
    {
        CutListWindow window = OpenCutList();
        window.ShowCutLayout();
        return window;
    }

    /// <summary>Opens the cut layout with the saw kerf field ready to type into (Project &#x2192; Saw kerf…).</summary>
    /// <returns>The window.</returns>
    public CutListWindow OpenSawKerf()
    {
        CutListWindow window = OpenCutList();
        window.EditKerf();
        return window;
    }

    /// <summary>Opens the cut-list window on its fastener sizes and supplies tab.</summary>
    /// <returns>The window.</returns>
    public CutListWindow OpenFastenerSizes()
    {
        CutListWindow window = OpenCutList();
        window.ShowSizes();
        return window;
    }

    /// <summary>
    /// Asks for a file and opens it. Cancelling changes nothing at all.
    /// </summary>
    /// <remarks>
    /// The picker is awaited rather than blocked on, because the platform dialog is asynchronous.
    /// An I/O or container-format failure (<see cref="ProjectFile.IsFileException"/>) becomes a
    /// visible refusal; anything else — a programming error — is not caught here (#175), because
    /// hiding a real bug behind "file refused" is worse than a visible crash to fix.
    /// </remarks>
    public Task OpenFileAsync() => WhenChangesAreSafe("opening another file", PickAndOpenFileAsync);

    async Task PickAndOpenFileAsync()
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
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            ShowRefusal("the file you chose", [exception.Message]);
        }
        finally
        {
            _opening = false;
        }
    }
}

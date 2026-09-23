using System.Globalization;
using Avalonia;
using Avalonia.Automation;
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
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

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

    readonly List<MenuItem> _sampleItems = [];
    ISceneFilePicker _filePicker;
    CutListWindow? _cutList;
    bool _opening;
    bool _saving;
    bool _closeConfirmed;
    string? _documentPath;
    Func<Task>? _afterAnswer;
    bool _showingProperties;
    bool _showingCut;
    EntityId? _editingBox;
    EntityId? _shaping;
    SizeAxis _editingAxis = SizeAxis.Width;
    readonly List<CutSite> _cutsOnScreen = [];
    List<string> _cutLinesOnScreen = [];

    public MainWindow()
    {
        InitializeComponent();

        _filePicker = new StorageProviderScenePicker(this);

        DrawingCanvas.Editor = Editor;
        Editor.MessageChanged += (_, _) => UpdateMessageBar();
        Editor.DesignChanged += (_, _) => OnDesignChanged();
        Editor.SelectionChanged += (_, _) => OnSelectionChanged();
        Editor.History.Changed += (_, _) => UpdateMenuEnablement();

        BuildSamplesMenu();
        BuildKeyBindings();

        // The window opens on the plan; View says so the way Samples marks the open sample.
        PlanViewMenuItem.Icon = new TextBlock { Text = "✓" };

        DrawingCanvas.ViewChanged += (_, _) =>
        {
            UpdateZoomReadout();
            PlaceDimensionEditor();
        };
        DrawingCanvas.PointerWorldPositionChanged += (_, point) => UpdateCursorReadout(point);
        DrawingCanvas.ToolChanged += (_, _) => UpdateToolButtons();
        DrawingCanvas.HoveredPartChanged += (_, _) => UpdateRelationships();
        DrawingCanvas.DimensionEditRequested += (_, request) =>
            OpenDimensionEditor(request.Box, request.Axis);
        DrawingCanvas.ShapeRequested += (_, box) => OpenWorkshop(box);
        DrawingCanvas.ModelViewRequested += (_, _) => ShowModelView();

        // The 3D view: the same editor, so the same drawing, selection and undo (assembly-model
        // §8.1). What it asks of the selection is done by the plan canvas's own commands, so there
        // is one Delete, one Pin, one Duplicate.
        ModelDrawing.Editor = Editor;
        ModelDrawing.ViewChanged += (_, _) => UpdateZoomReadout();
        ModelDrawing.HoveredPartChanged += (_, _) => UpdateRelationships();
        ModelDrawing.PointerModelPositionChanged += (_, point) => UpdateCursorReadout(point);
        ModelDrawing.PlanRequested += (_, _) => ShowPlanView();
        ModelDrawing.SelectionCommandRequested += (_, command) => RunSelectionCommand(command);
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

        WorkshopDrawing.Editor = Editor;
        WorkshopDrawing.SelectedCutChanged += (_, _) => ShowCut();
        WorkshopDrawing.HintChanged += (_, _) => UpdateWorkshopHint();
        WorkshopCutsList.SelectionChanged += (_, _) => OnWorkshopCutPicked();
        WorkshopStockBox.TextChanged += (_, _) => UpdateWorkshopStockReadout();

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

    /// <summary>Whether the last message offers a way out of a conflict.</summary>
    public bool IsOfferingToRemoveRelationship => MessageOfferButton.IsVisible;

    /// <summary>The wording of that offer.</summary>
    public string RemoveOfferText => MessageOfferButton.Content as string ?? string.Empty;

    /// <summary>The button that takes the offer.</summary>
    public Button RemoveOfferButton => MessageOfferButton;

    /// <summary>The relationship list panel.</summary>
    public Border Relationships => RelationshipsPanel;

    /// <summary>
    /// What the relationship list is showing, one line each. Empty while it is collapsed to its
    /// badge, because then no sentence is on the screen.
    /// </summary>
    public IReadOnlyList<string> RelationshipsOnScreen =>
    [
        .. RelationshipsList.Children.OfType<TextBlock>().Select(line => line.Text ?? string.Empty),
    ];

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
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ShowRefusal("the file you chose", [exception.Message]);
        }
        finally
        {
            _opening = false;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Saving, and not losing what has not been saved
    // ---------------------------------------------------------------------------------------

    /// <summary>The <em>File</em> menu.</summary>
    public MenuItem FileMenuItem => FileMenu;

    /// <summary>The <em>File &#x2192; Exit</em> item.</summary>
    public MenuItem ExitMenuEntry => ExitMenuItem;

    /// <summary>The <em>File &#x2192; Save</em> item.</summary>
    public MenuItem SaveMenuEntry => SaveMenuItem;

    /// <summary>The <em>File &#x2192; Save As&#x2026;</em> item.</summary>
    public MenuItem SaveAsMenuEntry => SaveAsMenuItem;

    /// <summary>
    /// The scene file a plain Save writes to: the file that was opened, or the last one saved to.
    /// Null for a new sheet or a sample that has not been saved anywhere yet.
    /// </summary>
    public string? DocumentPath => _documentPath;

    /// <summary>Whether the drawing has changed since it was opened or last saved.</summary>
    public bool HasUnsavedChanges => Editor.HasUnsavedChanges;

    /// <summary>Whether the window is asking whether to save before it throws changes away.</summary>
    public bool IsAskingToSave => UnsavedBackdrop.IsVisible;

    /// <summary>What the save question says, or empty when it is not being asked.</summary>
    public string SaveQuestionText => IsAskingToSave ? UnsavedHeadline.Text ?? string.Empty : string.Empty;

    /// <summary>The question's Save button: save, then carry on.</summary>
    public Button SaveChangesButton => UnsavedSaveButton;

    /// <summary>The question's Don't save button: carry on, and lose the changes.</summary>
    public Button DiscardChangesButton => UnsavedDiscardButton;

    /// <summary>The question's Cancel button: do neither, and go back to the drawing.</summary>
    public Button KeepEditingButton => UnsavedCancelButton;

    /// <summary>
    /// Saves the drawing to the file it came from or was last saved to; a drawing with no such
    /// file asks where to put it, as <see cref="SaveAsAsync"/> does.
    /// </summary>
    /// <returns>Whether the drawing was written.</returns>
    public Task<bool> SaveAsync() =>
        _documentPath is { } path ? Task.FromResult(SaveTo(path)) : SaveAsAsync();

    /// <summary>
    /// Asks where to save the drawing, writes it there, and makes that the file a plain Save
    /// writes to from then on.
    /// </summary>
    /// <remarks>
    /// Cancelling writes nothing and says so. A picker that throws is reported like any other
    /// failure to save: the one thing this must never do is lose the drawing or the application.
    /// </remarks>
    /// <returns>Whether the drawing was written.</returns>
    public async Task<bool> SaveAsAsync()
    {
        if (_saving || IsAskingToSave || RefusedMidGesture())
        {
            return false;
        }

        _saving = true;
        try
        {
            string? path;
            try
            {
                path = await FilePicker.PickSaveDestinationAsync(SuggestedFileName()).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Editor.Say(EditSeverity.Problem, $"Not saved: {exception.Message}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                Editor.Say(EditSeverity.Hint, "Not saved — no file was chosen, so nothing was written.");
                return false;
            }

            return SaveTo(path);
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>Writes the drawing to a file, and says what happened.</summary>
    bool SaveTo(string path)
    {
        if (IsAskingToSave || RefusedMidGesture())
        {
            return false;
        }

        switch (SceneFileSaver.Save(path, Editor.Sketch))
        {
            case SceneSaved saved:
                Editor.MarkSaved();
                _documentPath = saved.Path;
                string fileName = System.IO.Path.GetFileName(saved.Path);
                DesignText.Text = $"{fileName} — {saved.Path}";
                UpdateTitle();
                Editor.Say(
                    EditSeverity.Done,
                    $"Saved {fileName} in {System.IO.Path.GetDirectoryName(saved.Path)}.");
                return true;

            case SceneNotSaved refused:
                Editor.Say(EditSeverity.Problem, "Not saved. " + string.Join(" ", refused.Problems));
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Whether a drag is still under way, and so there is no finished drawing to save yet — said
    /// in the message bar rather than silently ignored.
    /// </summary>
    bool RefusedMidGesture()
    {
        if (!Editor.InGesture)
        {
            return false;
        }

        Editor.Say(EditSeverity.Hint, "Not saved — finish the drag first, then save.");
        return true;
    }

    /// <summary>What Save As offers to call the file: the file it already has, or the design's name.</summary>
    string SuggestedFileName()
    {
        if (_documentPath is { } path)
        {
            return System.IO.Path.GetFileName(path);
        }

        string name = Editor.Design.Name;
        return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : name + ".scene.json";
    }

    /// <summary>
    /// Does something that would throw the drawing's changes away — at once when there are none,
    /// and otherwise only once the person has said whether to save them first.
    /// </summary>
    /// <param name="doing">What is about to happen, for the question: "starting a new sheet".</param>
    /// <param name="then">What to do once it is safe to.</param>
    /// <returns>
    /// The work itself when it ran at once; a finished task when the question is waiting, because
    /// the work then runs when the person answers.
    /// </returns>
    Task WhenChangesAreSafe(string doing, Func<Task> then)
    {
        if (IsAskingToSave)
        {
            return Task.CompletedTask;
        }

        if (!Editor.HasUnsavedChanges)
        {
            return then();
        }

        // A half-typed dimension would sit under the question, out of reach; the question is
        // about the drawing, so the drawing is all that is left showing.
        CloseDimensionEditor(focusCanvas: false);

        UnsavedHeadline.Text = $"Save the changes to {DocumentName} before {doing}?";
        _afterAnswer = then;
        UnsavedBackdrop.IsVisible = true;
        UnsavedSaveButton.Focus();
        return Task.CompletedTask;
    }

    /// <summary>The question is answered Save: save, and carry on only if the save worked.</summary>
    async Task SaveThenCarryOnAsync()
    {
        Func<Task>? then = TakeAnswer();
        if (await SaveAsync().ConfigureAwait(true) && then is not null)
        {
            await then().ConfigureAwait(true);
        }
    }

    /// <summary>The question is answered Don't save: carry on, and the changes go.</summary>
    async Task DiscardThenCarryOnAsync()
    {
        if (TakeAnswer() is { } then)
        {
            await then().ConfigureAwait(true);
        }
    }

    /// <summary>The question is answered Cancel: nothing happens, and the drawing is as it was.</summary>
    void KeepEditing()
    {
        TakeAnswer();
        Editor.Say(EditSeverity.Hint, "Nothing was saved and nothing was thrown away.");
    }

    /// <summary>Takes the question off the screen and hands back what it was holding.</summary>
    Func<Task>? TakeAnswer()
    {
        Func<Task>? then = _afterAnswer;
        _afterAnswer = null;
        UnsavedBackdrop.IsVisible = false;
        FocusDrawing();
        return then;
    }

    /// <summary>Closes the window without asking again: the person has already answered.</summary>
    Task CloseNow()
    {
        _closeConfirmed = true;
        Close();
        return Task.CompletedTask;
    }

    static Task Now(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Closing the window from its title bar, or quitting the application, asks first when there
    /// are unsaved changes. A close the application asks for itself (<see cref="Window.Close()"/>
    /// in code) does not: every such call has either asked already — <em>File &#x2192; Exit</em>
    /// goes through the same question — or is a test harness tearing the window down.
    /// </remarks>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        bool personAsked = !e.IsProgrammatic || e.CloseReason == WindowCloseReason.ApplicationShutdown;
        if (personAsked && !_closeConfirmed && Editor.HasUnsavedChanges)
        {
            e.Cancel = true;
            _ = WhenChangesAreSafe("closing napkin", CloseNow);
        }

        base.OnClosing(e);
    }

    /// <summary>What the drawing is called in the title and the save question: its file, or its name.</summary>
    string DocumentName =>
        _documentPath is { } path ? System.IO.Path.GetFileName(path) : Editor.Design.Name;

    /// <summary>The title: the file's name, marked with an asterisk while there are unsaved changes.</summary>
    void UpdateTitle() =>
        Title = Editor.HasUnsavedChanges ? $"napkin — {DocumentName}*" : $"napkin — {DocumentName}";

    // ---------------------------------------------------------------------------------------
    // The 3D view (docs/design/assembly-model.md §8)
    // ---------------------------------------------------------------------------------------

    /// <summary>The 3D view.</summary>
    public ModelView Model => ModelDrawing;

    /// <summary>Whether the window is showing the 3D view rather than the plan.</summary>
    public bool IsShowingModel => ModelDrawing.IsVisible;

    /// <summary>The <em>View &#x2192; Plan</em> item.</summary>
    public MenuItem PlanViewMenuEntry => PlanViewMenuItem;

    /// <summary>The <em>View &#x2192; 3D</em> item.</summary>
    public MenuItem ModelViewMenuEntry => ModelViewMenuItem;

    /// <summary>The three turn buttons, about X, Y and Z, shown on the toolbar in the 3D view.</summary>
    public IReadOnlyList<Button> TurnButtons => [TurnXToolButton, TurnYToolButton, TurnZToolButton];

    /// <summary>
    /// Shows the 3D view in the plan canvas's place: a mode the window is in, over the same
    /// document, selection, editor and undo stack (&#xA7;8.1, &#xA7;11 decision 13).
    /// </summary>
    /// <remarks>
    /// Nothing about the drawing changes, and nothing about the plan does either — its view, its
    /// tool and its snap settings are all where they were when it comes back. What the plan's tools
    /// hold is put down: drawing a rectangle or placing stock is the plan's (&#xA7;6), and asking for
    /// either from the 3D view brings the plan back.
    /// </remarks>
    public void ShowModelView()
    {
        if (IsShowingModel)
        {
            return;
        }

        CloseWorkshop();
        CloseDimensionEditor(focusCanvas: false);
        DrawingCanvas.ArmStock(null);
        DrawingCanvas.Tool = EditTool.Select;

        DrawingCanvas.IsVisible = false;
        ModelDrawing.IsVisible = true;
        ShowViewChrome();
        Editor.Say(
            EditSeverity.Hint,
            "3D view: drag to orbit; select a part, then drag an arrow to move it along that axis or "
            + "a square to resize it; X, Y and Z turn it. V goes back to the plan.");
    }

    /// <summary>Shows the plan canvas again, as it was left.</summary>
    public void ShowPlanView()
    {
        if (!IsShowingModel)
        {
            return;
        }

        ModelDrawing.IsVisible = false;
        DrawingCanvas.IsVisible = true;
        ShowViewChrome();
    }

    /// <summary>The chrome that differs between the two views: the turn buttons, the menu's tick, the readouts.</summary>
    void ShowViewChrome()
    {
        bool model = IsShowingModel;
        foreach (Button button in TurnButtons)
        {
            button.IsVisible = model;
        }

        PlanViewMenuItem.Icon = model ? null : new TextBlock { Text = "✓" };
        ModelViewMenuItem.Icon = model ? new TextBlock { Text = "✓" } : null;

        UpdateZoomReadout();
        if (model)
        {
            UpdateCursorReadout((Vector3d?)null);
        }
        else
        {
            UpdateCursorReadout((Point2?)null);
        }

        UpdateRelationships();
        UpdateMenuEnablement();
        FocusDrawing();
    }

    /// <summary>Gives the keyboard to whichever view of the drawing is showing.</summary>
    void FocusDrawing()
    {
        if (IsShowingModel)
        {
            ModelDrawing.Focus();
        }
        else
        {
            DrawingCanvas.Focus();
        }
    }

    /// <summary>What the 3D view asks of the selection, done by the plan canvas's own commands.</summary>
    void RunSelectionCommand(SelectionCommand command)
    {
        switch (command)
        {
            case SelectionCommand.Delete:
                DrawingCanvas.DeleteSelection();
                break;

            case SelectionCommand.Pin:
                DrawingCanvas.PinSelection();
                break;

            case SelectionCommand.Duplicate:
                DrawingCanvas.DuplicateSelection();
                break;

            case SelectionCommand.Shape:
                DrawingCanvas.ShapeSelection();
                break;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Undo and redo (#11)
    // ---------------------------------------------------------------------------------------

    /// <summary>The <em>Edit</em> menu.</summary>
    public MenuItem EditMenuItem => EditMenu;

    /// <summary>The <em>Edit &#x2192; Undo</em> item, which names what it would undo.</summary>
    public MenuItem UndoMenuEntry => UndoMenuItem;

    /// <summary>The <em>Edit &#x2192; Redo</em> item, which names what it would redo.</summary>
    public MenuItem RedoMenuEntry => RedoMenuItem;

    /// <summary>
    /// Takes back the last thing done to the drawing — or, while a text field has the keyboard,
    /// the last thing typed into it.
    /// </summary>
    /// <returns>Whether the drawing changed.</returns>
    public bool UndoCommand()
    {
        if (FocusManager?.GetFocusedElement() is TextBox field)
        {
            // The window's key binding sees the key before the field does, so the field's own
            // undo is asked for here: undo while typing a dimension takes back the typing, never
            // the part whose dimension is being typed.
            field.Undo();
            return false;
        }

        return !IsAskingToSave && Editor.Undo();
    }

    /// <summary>
    /// Puts back the last thing undone — or, while a text field has the keyboard, the last thing
    /// undone in it.
    /// </summary>
    /// <returns>Whether the drawing changed.</returns>
    public bool RedoCommand()
    {
        if (FocusManager?.GetFocusedElement() is TextBox field)
        {
            field.Redo();
            return false;
        }

        return !IsAskingToSave && Editor.Redo();
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
            FocusDrawing();
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
        UpdateTitle();
        UpdateRelationships();
        PlaceDimensionEditor();
        UpdateWorkshop();

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

        // The workshop's scope is one box (§7.1). Selecting something else — or nothing, because
        // the part was deleted or another design was opened — takes it off the screen rather than
        // leaving a sheet open over a part that is not there any more.
        if (_shaping is { } shaped && Editor.OnlySelected != shaped)
        {
            CloseWorkshop();
        }

        UpdateMenuEnablement();
        UpdateRelationships();
        ShowProperties();
    }

    // ---------------------------------------------------------------------------------------
    // The properties panel: what the selected part is, as opposed to where it is
    // ---------------------------------------------------------------------------------------

    /// <summary>The properties panel.</summary>
    public Border Properties => PropertiesPanel;

    /// <summary>Whether the properties panel is on screen.</summary>
    public bool IsShowingProperties => PropertiesPanel.IsVisible;

    /// <summary>The field the selected part's name is typed into.</summary>
    public TextBox PartNameField => PartNameBox;

    /// <summary>The box that says whether the selection is a piece to cut.</summary>
    public CheckBox IsPartField => IsPartCheck;

    /// <summary>
    /// The field the box's depth — for a part, its out-of-plane dimension — is typed into. Every box
    /// has one (docs/design/assembly-model.md §1.2), so it is shown for a plain box too.
    /// </summary>
    public TextBox OutOfPlaneField => OutOfPlaneBox;

    /// <summary>The field the quantity is typed into.</summary>
    public TextBox QuantityField => QuantityBox;

    /// <summary>The field a stock name is typed into.</summary>
    public TextBox StockField => StockBox;

    /// <summary>The field a species is typed into.</summary>
    public TextBox SpeciesField => SpeciesBox;

    /// <summary>Which of the three dimensions the box's width is.</summary>
    public ComboBox PlanAcross => PlanXBox;

    /// <summary>Which of the three dimensions the box's height is.</summary>
    public ComboBox PlanUp => PlanYBox;

    /// <summary>The button that puts the panel's contents on the part.</summary>
    public Button ApplyPart => ApplyPartButton;

    /// <summary>What the stock line says: the library's own hover text, or why it did not resolve.</summary>
    public string StockReadoutText => StockReadout.Text ?? string.Empty;

    /// <summary>What the panel is complaining about, or empty when it is not.</summary>
    public string PropertiesErrorText => PropertiesError.IsVisible ? PropertiesError.Text ?? string.Empty : string.Empty;

    /// <summary>
    /// Fills the properties panel from the selection, or hides it when there is nothing to fill it
    /// from.
    /// </summary>
    void ShowProperties()
    {
        // While the shape workshop is open the canvas's floating chrome is off the screen, this
        // panel with it: the workshop is a mode with its own panel, and two panels stacked on the
        // same corner of the window would have one of them taking the other's clicks.
        if (IsShapingPart || Editor.OnlySelectedBox is not { } box)
        {
            PropertiesPanel.IsVisible = false;
            ShowCut();
            return;
        }

        _showingProperties = true;
        try
        {
            PropertiesPanel.IsVisible = true;
            PropertiesError.IsVisible = false;

            PartNameBox.Text = box.Name;
            IsPartCheck.IsChecked = box.Part is not null;
            PartFields.IsVisible = box.Part is not null;

            FillDimensionChoices();

            Part part = box.Part ?? DefaultPart;
            PlanXBox.SelectedItem = SceneWords.Of(part.PlanAxes.X);
            PlanYBox.SelectedItem = SceneWords.Of(part.PlanAxes.Y);
            OutOfPlaneBox.Text = box.Depth.Format(Editor.LabelFormat).Text;
            QuantityBox.Text = part.Quantity.ToString(CultureInfo.InvariantCulture);
            StockBox.Text = part.Stock ?? string.Empty;
            SpeciesBox.Text = part.Species ?? string.Empty;

            UpdateOutOfPlaneCaption();
            UpdateStockReadout();
        }
        finally
        {
            _showingProperties = false;
        }

        ShowCut();
    }

    void FillDimensionChoices()
    {
        if (PlanXBox.ItemsSource is not null)
        {
            return;
        }

        PlanXBox.ItemsSource = SceneWords.Dimensions;
        PlanYBox.ItemsSource = SceneWords.Dimensions;
        PlanXBox.SelectionChanged += (_, _) => UpdateOutOfPlaneCaption();
        PlanYBox.SelectionChanged += (_, _) => UpdateOutOfPlaneCaption();
        StockBox.TextChanged += (_, _) => UpdateStockReadout();
    }

    /// <summary>
    /// The depth field is labelled with the dimension it actually is: for a part, the one the
    /// plan's two axes leave, and for a plain box simply its depth.
    /// </summary>
    void UpdateOutOfPlaneCaption()
        => OutOfPlaneCaption.Text = IsPartCheck.IsChecked != true
            ? "Depth"
            : ChosenAxes() is { } axes
                ? SceneWords.Of(axes.OutOfPlane)
                : "Third";

    /// <summary>
    /// What the library says about the stock that was typed — the same hover line the picker
    /// shows — or that it does not carry that name, which is not an error.
    /// </summary>
    void UpdateStockReadout() => StockReadout.Text = StockReadoutFor(StockBox.Text);

    /// <summary>
    /// What the library says about a stock name somebody typed &#x2014; the same line the
    /// properties panel and the shape workshop both show, because what a person read is what gets
    /// assigned.
    /// </summary>
    static string StockReadoutFor(string? typed)
    {
        string name = (typed ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return "No stock chosen. The cut list shows the size you typed.";
        }

        return MaterialsLibrary.Shipped.TryFind(name, out StockItem item)
            ? item.HoverText
            : $"\"{name}\" is not in this build's materials library. "
              + "The cut list will say so rather than guess.";
    }

    PlanAxes? ChosenAxes()
    {
        if (!SceneWords.TryDimension(PlanXBox.SelectedItem as string, out PartDimension x)
            || !SceneWords.TryDimension(PlanYBox.SelectedItem as string, out PartDimension y)
            || x == y)
        {
            return null;
        }

        return new PlanAxes(x, y);
    }

    void OnIsPartChanged(object? sender, RoutedEventArgs e)
    {
        if (_showingProperties)
        {
            return;
        }

        PartFields.IsVisible = IsPartCheck.IsChecked == true;
        UpdateOutOfPlaneCaption();
    }

    void OnApplyPartClicked(object? sender, RoutedEventArgs e) => ApplyProperties();

    /// <summary>
    /// Puts what the panel says onto the selected box: its name, and what kind of part it is.
    /// </summary>
    /// <remarks>
    /// Both go through the editor as requests, so the canvas, the relationship list and the cut
    /// list all hear about them the same way they hear about a drag. Nothing is half-applied: the
    /// panel checks every field before it sends anything.
    /// </remarks>
    /// <returns>Whether the panel's contents were applied.</returns>
    public bool ApplyProperties()
    {
        if (Editor.OnlySelectedBox is not { } box)
        {
            return false;
        }

        Part? part = null;
        PlanAxes? chosen = null;
        if (IsPartCheck.IsChecked == true)
        {
            if (ChosenAxes() is not { } axes)
            {
                return Complain("A part's two plan dimensions have to be different ones.");
            }

            chosen = axes;
        }

        if (!Length.TryParse(OutOfPlaneBox.Text, out Length depth, out _) || depth <= Length.Zero)
        {
            return Complain(
                $"{(chosen is { } named ? SceneWords.Of(named.OutOfPlane) : "Depth")} has to be a length greater than zero, "
                + "like 3/4\" or 1' 4 1/4\".");
        }

        if (chosen is { } planAxes)
        {
            if (!int.TryParse(
                    QuantityBox.Text,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int quantity)
                || quantity < 1)
            {
                return Complain("A part stands for at least one piece.");
            }

            part = new Part(
                Blank(StockBox.Text),
                Blank(SpeciesBox.Text),
                quantity,
                planAxes);
        }

        PropertiesError.IsVisible = false;

        string name = PartNameBox.Text ?? string.Empty;
        List<Request> requests = [new SetName(box.Id, name), Assignment(box, part)];
        if (DepthRequest(box, part, depth) is { } typedDepth)
        {
            requests.Add(typedDepth);
        }

        Editor.Apply(
            Batch.Of([.. requests]),
            part is null ? "make it a plain box" : "set what this part is");

        ShowProperties();
        return true;

        static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// What to put to the editor to make this box that part: not just the stock's <em>name</em>,
    /// but the dimensions the yard fixes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A 1x6 is 5 1/2&#x2033; wide whatever the box was drawn at, so choosing it here really
    /// resizes the blank — through the updater, so that relationships propagate and a conflict is
    /// reported like any other, and as one batch, so that a part pinned to something that cannot
    /// move keeps both its old size and its old stock rather than being half-assigned
    /// (<c>docs/design/parts-and-cut-list.md</c> §1.2).
    /// </para>
    /// <para>
    /// Which dimensions those are is <see cref="StockAssignment"/>'s to say, not this window's: the
    /// panel resolves the typed name through the library — the same lookup the readout above it
    /// uses, so what a person read is what gets assigned — and calls it. A name the library does
    /// not carry fixes nothing and is not an error; the name is still stored, and the cut list says
    /// it did not resolve.
    /// </para>
    /// </remarks>
    Request Assignment(Box box, Part? part)
    {
        if (part is null)
        {
            return new SetPart(box.Id, null);
        }

        return StockAssignment.RequestsFor(Editor.Sketch, box, part, StockFor(part));
    }

    static StockItem? StockFor(Part part)
        => MaterialsLibrary.Shipped.TryFind(part.Stock, out StockItem item) ? item : null;

    /// <summary>
    /// What a typed depth puts to the editor: a <see cref="ParamValue"/> on the box's depth, exactly
    /// as a typed width is one on its width (docs/design/assembly-model.md §1.2) — or nothing, when
    /// the depth is what it already was, or when the part's stock fixes it and so states it itself.
    /// </summary>
    Request? DepthRequest(Box box, Part? part, Length depth)
    {
        if (depth == box.Depth || (part is not null && StockAssignment.FixesDepth(part, StockFor(part))))
        {
            return null;
        }

        return StockAssignment.SizeRequest(Editor.Sketch, new BoxDepthRef(box.Id), depth);
    }

    // ---------------------------------------------------------------------------------------
    // The shape workshop: one blank, its stock, and its cuts
    // (docs/design/shaped-parts-model.md §7.1, §7.2)
    // ---------------------------------------------------------------------------------------

    /// <summary>The workshop sheet, hidden until a part is being shaped.</summary>
    public Border WorkshopPanel => WorkshopSheet;

    /// <summary>The workshop's drawing of the blank.</summary>
    public WorkshopView Workshop => WorkshopDrawing;

    /// <summary>Whether the window is in the shape workshop.</summary>
    public bool IsShapingPart => WorkshopSheet.IsVisible;

    /// <summary>The part being shaped, or null when the workshop is closed.</summary>
    public EntityId? ShapedPart => _shaping;

    /// <summary>The <em>Draw &#x2192; Shape&#x2026;</em> item.</summary>
    public MenuItem ShapeMenuEntry => ShapeMenuItem;

    /// <summary>The workshop's stock field.</summary>
    public TextBox WorkshopStockField => WorkshopStockBox;

    /// <summary>The button that makes the blank really be the stock that was typed.</summary>
    public Button WorkshopStockApply => WorkshopStockButton;

    /// <summary>What the workshop's stock line says.</summary>
    public string WorkshopStockReadoutText => WorkshopStockReadout.Text ?? string.Empty;

    /// <summary>What the workshop's cut list is showing, one line each, in site order.</summary>
    public IReadOnlyList<string> WorkshopCutsOnScreen => _cutLinesOnScreen;

    /// <summary>What the workshop says a press would do, or empty when the pointer is on nothing.</summary>
    public string WorkshopHintText => WorkshopHint.Text ?? string.Empty;

    /// <summary>The selected cut's first typed value.</summary>
    public TextBox CutFirstField => CutFirstBox;

    /// <summary>The selected cut's second typed value, when it has two.</summary>
    public TextBox CutSecondField => CutSecondBox;

    /// <summary>The angle a corner cut is cut at, as an entry mode (&#xA7;1.3).</summary>
    public TextBox CutAngleField => CutAngleBox;

    /// <summary>The button that applies what the cut fields say.</summary>
    public Button ApplyCut => ApplyCutButton;

    /// <summary>The button that sets both setbacks to the blank's width, once.</summary>
    public Button FullMitre => FullMitreButton;

    /// <summary>The button that takes the selected cut off the blank.</summary>
    public Button RemoveCut => RemoveCutButton;

    /// <summary>Whether the cut fields are on screen.</summary>
    public bool IsShowingCutFields => CutFields.IsVisible;

    /// <summary>What the cut fields say about the selected cut, in words.</summary>
    public string CutReadoutText => CutReadout.Text ?? string.Empty;

    /// <summary>What the cut fields are complaining about, or empty when they are not.</summary>
    public string CutErrorText => CutError.IsVisible ? CutError.Text ?? string.Empty : string.Empty;

    /// <summary>
    /// Opens the shape workshop on one part.
    /// </summary>
    /// <remarks>
    /// It is a mode, not a second document (&#xA7;7.1): the same editor, the same sketch, the same
    /// selection. Leaving it returns to the canvas with the part exactly where it was, because
    /// nothing about where it is was ever touched.
    /// </remarks>
    /// <returns>Whether the workshop opened.</returns>
    public bool OpenWorkshop(EntityId box)
    {
        if (Editor.Design.Sketch.Find<Box>(box) is not { } part)
        {
            return false;
        }

        CloseDimensionEditor(focusCanvas: false);
        Editor.Select(box);

        _shaping = box;
        WorkshopSheet.IsVisible = true;
        WorkshopDrawing.Blank = box;

        // The chrome that belongs to the canvas goes while the canvas is behind the sheet. The
        // workshop says what the part is in its own headline and leads with its own stock picker,
        // so nothing a person needs here is in the panels that just went.
        ToolBar.IsVisible = false;
        RelationshipsPanel.IsVisible = false;
        PropertiesPanel.IsVisible = false;
        DrawingCanvas.ArmStock(null);
        UpdateToolbox();

        _showingCut = true;
        try
        {
            WorkshopStockBox.Text = part.Part?.Stock ?? string.Empty;
        }
        finally
        {
            _showingCut = false;
        }

        UpdateWorkshop();

        // The hint line is where the modifiers are written down, and it is the first thing a
        // person needs. The view only announces it when it changes, and it opens saying nothing,
        // so the default is put up here rather than waiting for a hover.
        UpdateWorkshopHint();
        UpdateMenuEnablement();
        ShowProperties();
        WorkshopDrawing.Focus();
        return true;
    }

    /// <summary>Leaves the workshop, and gives the drawing back.</summary>
    public void CloseWorkshop()
    {
        if (!WorkshopSheet.IsVisible)
        {
            return;
        }

        _shaping = null;
        WorkshopSheet.IsVisible = false;
        WorkshopDrawing.Blank = null;
        ToolBar.IsVisible = true;
        CutFields.IsVisible = false;
        UpdateToolbox();

        UpdateRelationships();
        UpdateMenuEnablement();
        ShowProperties();
        FocusDrawing();
    }

    /// <summary>
    /// Makes the blank really be the stock the workshop's field names
    /// (<c>docs/design/parts-and-cut-list.md</c> &#xA7;1.2).
    /// </summary>
    /// <remarks>
    /// The same path the properties panel takes: <see cref="StockAssignment"/> says which of the
    /// three dimensions the yard fixes and the batch goes through the editor, so a 1x6 really is
    /// 5&#xBD;&#x2033; wide and a conflict is reported like any other. A box that was not a part
    /// yet becomes one on the way — the workshop leads with the stock picker, and picking a stock
    /// for something that is not a piece to cut would mean nothing.
    /// </remarks>
    /// <returns>Whether the drawing changed.</returns>
    public bool ApplyWorkshopStock()
    {
        if (_shaping is not { } id || Editor.Design.Sketch.Find<Box>(id) is not { } box)
        {
            return false;
        }

        string typed = (WorkshopStockBox.Text ?? string.Empty).Trim();
        Part part = (box.Part ?? DefaultPart) with { Stock = typed.Length == 0 ? null : typed };

        StockItem? stock = MaterialsLibrary.Shipped.TryFind(part.Stock, out StockItem item)
            ? item
            : null;

        string what = typed.Length == 0
            ? $"Took the stock off {Editor.NameOf(id)}"
            : $"Cut {Editor.NameOf(id)} from {typed}";

        Editor.BeginGesture(what);
        UpdateResult result = Editor.Apply(
            StockAssignment.RequestsFor(Editor.Sketch, box, part, stock),
            what);
        Editor.EndGesture();

        UpdateWorkshop();
        ShowProperties();
        return result is Succeeded;
    }

    /// <summary>
    /// Applies what the cut fields say to the selected cut: one <see cref="SetCut"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A corner cut has two descriptions and only one of them can be exact (&#xA7;1.3), so the
    /// panel offers both and one of them wins: when the angle has been edited it is the angle,
    /// converted to the setback it implies with one explicit rounding, keeping the longer setback
    /// as the edge that angle is measured against; otherwise it is the two lengths, taken as
    /// typed.
    /// </para>
    /// <para>
    /// Text that is not a length or an angle is explained in the panel and changes nothing at all
    /// — the same rule the dimension field has had from the start.
    /// </para>
    /// </remarks>
    /// <returns>Whether the drawing changed.</returns>
    public bool ApplyCutEntry()
    {
        if (WorkshopDrawing.LocalBlank is not { } blank || WorkshopDrawing.SelectedCut is not { } cut)
        {
            return false;
        }

        switch (cut)
        {
            case CornerCut clip:
                return ApplyCornerCutEntry(blank, clip);

            case RoundedCorner rounded:
                return Read(CutFirstBox, "A radius") is { } radius
                       && WorkshopDrawing.SetCutNow(
                           rounded with { Radius = radius },
                           $"Set {blank.Name}'s {cut.Site} to a {Show(radius)} radius");

            case CurvedEdge curve:
                return Read(CutFirstBox, "A depth") is { } depth
                       && WorkshopDrawing.SetCutNow(
                           curve with { Depth = depth },
                           $"Set {blank.Name}'s {cut.Site} to {Show(depth)} deep");

            default:
                return false;
        }
    }

    bool ApplyCornerCutEntry(Box blank, CornerCut clip)
    {
        if (AngleWasEdited(clip) && CutAngle.TryParseDegrees(CutAngleBox.Text, out double degrees))
        {
            Length longer = Length.Max(clip.AlongX, clip.AlongY);
            if (CutAngle.SetbackFor(longer, degrees) is not { } shorter)
            {
                return ComplainAboutCut("A cut is made at more than 0° and less than 90° off square.");
            }

            CornerCut angled = clip.AlongX >= clip.AlongY
                ? clip with { AlongY = shorter }
                : clip with { AlongX = shorter };

            return WorkshopDrawing.SetCutNow(
                angled,
                $"Set {blank.Name}'s {clip.Site} to {CutAngle.Text(shorter, longer)} off square");
        }

        if (Read(CutFirstBox, "A setback") is not { } alongX
            || Read(CutSecondBox, "A setback") is not { } alongY)
        {
            return false;
        }

        return WorkshopDrawing.SetCutNow(
            clip with { AlongX = alongX, AlongY = alongY },
            $"Set {blank.Name}'s {clip.Site} to {Show(alongX)} by {Show(alongY)}");
    }

    /// <summary>Whether the angle field holds something other than what the stored cut reads as.</summary>
    bool AngleWasEdited(CornerCut clip) => !string.Equals(
        (CutAngleBox.Text ?? string.Empty).Trim(),
        CutAngle.Text(Length.Min(clip.AlongX, clip.AlongY), Length.Max(clip.AlongX, clip.AlongY)),
        StringComparison.Ordinal);

    /// <summary>A length out of a cut field, or nothing at all when it does not read as one.</summary>
    Length? Read(TextBox field, string what)
    {
        if (DimensionEntry.Interpret(field.Text) is ReadableLength readable && readable.Value > Length.Zero)
        {
            CutError.IsVisible = false;
            return readable.Value;
        }

        ComplainAboutCut($"{what} has to be a length greater than zero, like 3/4\" or 1' 4 1/4\".");
        return null;
    }

    bool Complain(string why)
    {
        PropertiesError.Text = why;
        PropertiesError.IsVisible = true;
        return false;
    }

    bool ComplainAboutCut(string why)
    {
        CutError.Text = why;
        CutError.IsVisible = true;
        return false;
    }

    /// <summary>Refreshes everything the workshop shows from the sketch.</summary>
    void UpdateWorkshop()
    {
        if (!WorkshopSheet.IsVisible)
        {
            return;
        }

        if (_shaping is not { } id || Editor.Design.Sketch.Find<Box>(id) is not { } box)
        {
            CloseWorkshop();
            return;
        }

        WorkshopHeadline.Text = $"Shaping {Editor.NameOf(id)} — "
                                + $"{Show(box.Width)} × {Show(box.Height)} blank";

        UpdateWorkshopStockReadout();
        UpdateWorkshopCuts(box);
        ShowCut();
    }

    void UpdateWorkshopStockReadout() =>
        WorkshopStockReadout.Text = StockReadoutFor(WorkshopStockBox.Text);

    /// <summary>The blank's cuts, one line each, with the site selected in the drawing picked.</summary>
    void UpdateWorkshopCuts(Box box)
    {
        _showingCut = true;
        try
        {
            _cutsOnScreen.Clear();
            List<string> lines = [];
            foreach (Cut cut in box.Cuts)
            {
                _cutsOnScreen.Add(cut.Site);
                lines.Add(CutSummary(cut, Editor.LabelFormat));
            }

            _cutLinesOnScreen = lines;
            WorkshopCutsList.ItemsSource = lines;
            WorkshopCutsList.IsVisible = lines.Count > 0;
            WorkshopCutsEmpty.IsVisible = lines.Count == 0;
            WorkshopCutsHeadline.Text = lines.Count == 1 ? "Cuts — 1" : $"Cuts — {lines.Count}";

            WorkshopCutsList.SelectedIndex = WorkshopDrawing.SelectedSite is { } site
                ? _cutsOnScreen.IndexOf(site)
                : -1;
        }
        finally
        {
            _showingCut = false;
        }
    }

    void UpdateWorkshopHint()
    {
        string hint = WorkshopDrawing.Hint;
        WorkshopHint.Text = hint.Length > 0
            ? hint + "."
            : "Drag a corner to clip it, Shift for 45°, Alt to round it; drag an edge's middle to "
              + "curve it. Delete takes the selected cut off.";
    }

    void OnWorkshopCutPicked()
    {
        if (_showingCut)
        {
            return;
        }

        int index = WorkshopCutsList.SelectedIndex;
        WorkshopDrawing.SelectSite(index >= 0 && index < _cutsOnScreen.Count ? _cutsOnScreen[index] : null);
    }

    /// <summary>
    /// Fills the cut fields from the cut the workshop has selected, or hides them.
    /// </summary>
    void ShowCut()
    {
        if (!WorkshopSheet.IsVisible
            || WorkshopDrawing.LocalBlank is not { } blank
            || WorkshopDrawing.SelectedCut is not { } cut)
        {
            CutFields.IsVisible = false;
            return;
        }

        _showingCut = true;
        try
        {
            CutFields.IsVisible = true;
            CutError.IsVisible = false;
            CutHeadline.Text = $"Cut at the {BlankShape.Words(cut.Site)}";
            CutFirstCaption.IsVisible = true;
            CutFirstBox.IsVisible = true;

            bool corner = cut is CornerCut;
            CutSecondCaption.IsVisible = corner;
            CutSecondBox.IsVisible = corner;
            CutAngleCaption.IsVisible = corner;
            CutAngleBox.IsVisible = corner;
            FullMitreButton.IsVisible = corner;

            switch (cut)
            {
                case CornerCut clip:
                    CutFirstCaption.Text = $"{BlankShape.Compass(XEdge(clip.Corner))} edge";
                    CutFirstBox.Text = clip.AlongX.Format(Editor.LabelFormat).Text;
                    CutSecondCaption.Text = $"{BlankShape.Compass(YEdge(clip.Corner))} edge";
                    CutSecondBox.Text = clip.AlongY.Format(Editor.LabelFormat).Text;
                    CutAngleBox.Text = CutAngle.Text(
                        Length.Min(clip.AlongX, clip.AlongY),
                        Length.Max(clip.AlongX, clip.AlongY));
                    CutReadout.Text =
                        "Two marks, one on each edge. Typing an angle instead keeps the longer "
                        + "mark and works the shorter one out from it, rounding once.";
                    break;

                case RoundedCorner rounded:
                    CutFirstCaption.Text = "Radius";
                    CutFirstBox.Text = rounded.Radius.Format(Editor.LabelFormat).Text;
                    CutReadout.Text = "A quarter circle, tangent to both edges that meet here.";
                    break;

                case CurvedEdge curve:
                    CutFirstCaption.Text = "Depth";
                    CutFirstBox.Text = curve.Depth.Format(Editor.LabelFormat).Text;
                    CutReadout.Text = curve.Bow == Bow.Inward
                        ? "A scallop: the corners stay and the middle goes in by this much."
                        : "A bow: the middle stays and the corners come in by this much.";
                    break;
            }

            RemoveCutButton.IsEnabled = true;
            FullMitreButton.IsEnabled = corner;
            ApplyCutButton.IsEnabled = true;
        }
        finally
        {
            _showingCut = false;
        }

        UpdateWorkshopCutSelection();
    }

    /// <summary>Keeps the list's highlight on the cut the drawing has selected.</summary>
    void UpdateWorkshopCutSelection()
    {
        if (WorkshopDrawing.SelectedSite is not { } site)
        {
            return;
        }

        int index = _cutsOnScreen.IndexOf(site);
        if (WorkshopCutsList.SelectedIndex == index)
        {
            return;
        }

        _showingCut = true;
        try
        {
            WorkshopCutsList.SelectedIndex = index;
        }
        finally
        {
            _showingCut = false;
        }
    }

    void OnShapeClicked(object? sender, RoutedEventArgs e) => DrawingCanvas.ShapeSelection();

    void OnWorkshopDoneClicked(object? sender, RoutedEventArgs e) => CloseWorkshop();

    void OnWorkshopStockClicked(object? sender, RoutedEventArgs e) => ApplyWorkshopStock();

    void OnApplyCutClicked(object? sender, RoutedEventArgs e) => ApplyCutEntry();

    void OnRemoveCutClicked(object? sender, RoutedEventArgs e) => WorkshopDrawing.RemoveSelectedCut();

    void OnFullMitreClicked(object? sender, RoutedEventArgs e)
    {
        if (WorkshopDrawing.LocalBlank is not { } blank
            || WorkshopDrawing.SelectedSite?.AsCorner is not { } corner)
        {
            return;
        }

        CornerCut mitre = CutAngle.FullMitre(blank, corner);
        WorkshopDrawing.SetCutNow(
            mitre,
            $"Mitred {blank.Name}'s {mitre.Site} the full {Show(mitre.AlongX)}");
    }

    /// <summary>One cut as the workshop's list writes it: the site, the kind, and the numbers.</summary>
    static string CutSummary(Cut cut, LengthFormat format) => cut switch
    {
        CornerCut clip =>
            $"{BlankShape.Words(cut.Site)} — clip {Text(clip.AlongX, format)} × {Text(clip.AlongY, format)}",
        RoundedCorner rounded =>
            $"{BlankShape.Words(cut.Site)} — round, {Text(rounded.Radius, format)} radius",
        CurvedEdge { Bow: Bow.Inward } scallop =>
            $"{BlankShape.Words(cut.Site)} — scallop {Text(scallop.Depth, format)} deep",
        CurvedEdge curve =>
            $"{BlankShape.Words(cut.Site)} — curve {Text(curve.Depth, format)} deep",
        _ => BlankShape.Words(cut.Site),
    };


    static string Text(Length length, LengthFormat format)
    {
        FormattedLength formatted = length.Format(format);
        return formatted.IsExact ? formatted.Text : CutAngle.Approximately + formatted.Text;
    }

    /// <summary>The corner's edge that runs along the blank's local X.</summary>
    static BoxEdge XEdge(BoxCorner corner) =>
        corner is BoxCorner.SouthWest or BoxCorner.SouthEast ? BoxEdge.South : BoxEdge.North;

    /// <summary>The corner's edge that runs along the blank's local Y.</summary>
    static BoxEdge YEdge(BoxCorner corner) =>
        corner is BoxCorner.SouthWest or BoxCorner.NorthWest ? BoxEdge.West : BoxEdge.East;


    /// <summary>
    /// Greys out what has nothing to act on — the menu item and its toolbar button together, so
    /// the two never disagree about whether a function is available.
    /// </summary>
    void UpdateMenuEnablement()
    {
        bool anything = Editor.Selection.Count > 0;
        bool shapeable = Editor.OnlySelected is not null && !IsShapingPart;
        DeleteMenuItem.IsEnabled = DeleteToolButton.IsEnabled = anything;
        PinMenuItem.IsEnabled = PinToolButton.IsEnabled = anything;
        ShapeMenuItem.IsEnabled = ShapeToolButton.IsEnabled = shapeable;

        bool turnable = Editor.OnlySelectedBox is not null && !IsShapingPart;
        TurnXMenuItem.IsEnabled = TurnXToolButton.IsEnabled = turnable;
        TurnYMenuItem.IsEnabled = TurnYToolButton.IsEnabled = turnable;
        TurnZMenuItem.IsEnabled = TurnZToolButton.IsEnabled = turnable;

        // The shape workshop covers the paper and takes the toolbar's stock icons with it, so the
        // menu's way in to the same stock goes too: there is no paper to drag it onto.
        StockMenu.IsEnabled = !IsShapingPart;

        // Undo and redo name what they would do, and grey out when there is nothing to. An
        // underscore in a part's name is doubled so the menu shows it rather than taking it as
        // an access key.
        UndoHistory history = Editor.History;
        UndoMenuItem.IsEnabled = history.CanUndo;
        UndoMenuItem.Header = history.UndoWhat is { } undo ? $"_Undo {Escaped(undo)}" : "_Undo";
        RedoMenuItem.IsEnabled = history.CanRedo;
        RedoMenuItem.Header = history.RedoWhat is { } redo ? $"_Redo {Escaped(redo)}" : "_Redo";

        static string Escaped(string text) => text.Replace("_", "__", StringComparison.Ordinal);
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

    /// <summary>
    /// The relationship list, on demand (#62): a count badge while nothing it talks about is in
    /// play, and every sentence once a part it names is selected or under the pointer.
    /// </summary>
    /// <remarks>
    /// Expanded, it shows the whole list, not only the selected part's rows: the sentences are the
    /// same ones it has always shown, and which part is selected only decides whether the drawing
    /// has something to say that is worth the room. The panel stays off the screen while the shape
    /// workshop is open, whatever changes underneath it.
    /// </remarks>
    void UpdateRelationships()
    {
        IReadOnlyList<RelationshipEntry> entries = Editor.RelationshipEntries();
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        bool expanded = entries.Any(entry => entry.Entities.Any(IsInPlay));

        RelationshipsList.Children.Clear();
        if (expanded)
        {
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
        }

        RelationshipsList.IsVisible = expanded;
        RelationshipsHeadline.Text = expanded
            ? $"Relationships — {entries.Count}"
            : entries.Count == 1 ? "1 relationship" : $"{entries.Count} relationships";
        RelationshipsHeadline.FontSize = expanded ? 12 : 11;
        RelationshipsPanel.Padding = expanded ? new Thickness(10, 8) : new Thickness(8, 3);
        RelationshipsPanel.CornerRadius = new CornerRadius(expanded ? 4 : 10);
        RelationshipsPanel.IsVisible = entries.Count > 0 && !IsShapingPart;
    }

    /// <summary>
    /// Whether a part is one the relationship list should open for: selected, or resting under the
    /// pointer. Hovering is the quick look; selecting keeps it open while the pointer goes elsewhere.
    /// </summary>
    bool IsInPlay(EntityId id) =>
        Editor.Selection.Contains(id)
        || (IsShowingModel ? ModelDrawing.HoveredPart == id : DrawingCanvas.HoveredPart == id);

    void UpdateToolButtons()
    {
        SelectToolButton.IsChecked = DrawingCanvas.Tool == EditTool.Select;
        RectangleToolButton.IsChecked = DrawingCanvas.Tool == EditTool.Rectangle;
        StockToolboxPanel.ShowArmed(DrawingCanvas.ArmedStock);
    }

    // ---------------------------------------------------------------------------------------
    // The stock toolbox: pick real stock, then drag it onto the paper (issue #7, GUI-CUT-02)
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// A category icon was clicked: its drawer drops down under it, or — when that was the open
    /// one — the drawer closes and puts down whatever it had picked up.
    /// </summary>
    void OnStockCategoryChanged()
    {
        if (StockToolboxPanel.Category is null)
        {
            DrawingCanvas.ArmStock(null);
        }

        UpdateToolbox();
    }

    /// <summary>
    /// Shows the drawer under its category's icon while one is open and the canvas is what is on
    /// screen; the shape workshop covers the canvas and takes the toolbar and the drawer with it.
    /// </summary>
    void UpdateToolbox()
    {
        StockToolboxPanel.IsVisible = StockToolboxPanel.Category is not null && !IsShapingPart;
        if (!StockToolboxPanel.IsVisible
            || StockToolboxPanel.Category is not { } open
            || ToolBar.Parent is not Visual overlay)
        {
            return;
        }

        // Dropped down from the icon that opened it, pulled back left only as far as it takes to
        // stay inside the window.
        ToggleButton icon = StockToolboxPanel.CategoryButtons[open];
        double left = icon.TranslatePoint(new Point(0, 0), overlay)?.X ?? ToolBar.Bounds.Left;
        double room = overlay.Bounds.Width - StockToolboxPanel.Width - 10;
        left = Math.Max(10, Math.Min(left, room));
        StockToolboxPanel.Margin = new Thickness(left, ToolBar.Bounds.Bottom + 4, 10, 10);
    }

    /// <summary>
    /// An item was picked in the toolbox: the pointer now holds it, and the next drag on the paper
    /// places a part already cut from it. The toolbox stays open for the one after.
    /// </summary>
    void PickStock(StockItem item)
    {
        // Stock is placed on the plan (assembly-model §6), so picking some brings the plan back.
        if (StockTool.CanPlace(item))
        {
            ShowPlanView();
        }

        if (DrawingCanvas.ArmStock(item))
        {
            Editor.Say(
                EditSeverity.Hint,
                $"Holding {item.Name} — actual {item.ActualSizeText}. Drag on the paper to place it; Escape puts it down.");
        }
        else
        {
            Editor.Say(
                EditSeverity.Hint,
                $"{item.HoverText}. napkin does not place fasteners on the drawing yet, so there is nothing to drag.");
        }

        FocusDrawing();
    }

    /// <summary>
    /// Builds <em>Draw &#x2192; Stock</em>: the toolbox's two levels as two levels of menu — a
    /// submenu per category, named and explained as its toolbar icon is, and under it an item per
    /// size with the size's actual dimensions and citation on hover.
    /// </summary>
    /// <remarks>
    /// Picking an item does what the toolbar's two clicks do, through the same code: it opens that
    /// category's drawer, so the item held is marked in it, and then picks the item exactly as a
    /// click in the drawer does (<see cref="PickStock"/>). A fastener is listed and cannot be
    /// placed, by menu as by toolbar.
    /// </remarks>
    void BuildStockMenu()
    {
        List<MenuItem> categories = [];
        foreach (StockCategory category in StockToolboxPanel.Library.Categories)
        {
            List<MenuItem> sizes = [];
            foreach (StockItem item in StockToolboxPanel.Library.InCategory(category))
            {
                MenuItem size = new() { Header = item.Name, Tag = item };
                ToolTip.SetTip(size, item.HoverText);
                AutomationProperties.SetHelpText(size, item.HoverText);
                size.Click += (_, _) =>
                {
                    StockToolboxPanel.ShowCategory(category);
                    PickStock(item);
                };
                sizes.Add(size);
            }

            MenuItem submenu = new()
            {
                Header = StockToolbox.Words(category),
                Tag = category,
                ItemsSource = sizes,
            };
            ToolTip.SetTip(submenu, ToolTip.GetTip(StockToolboxPanel.CategoryButtons[category]));
            categories.Add(submenu);
        }

        StockMenu.ItemsSource = categories;
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
        UnsavedPanel.Background = paper;
        UnsavedPanel.BorderBrush = edge;
        UnsavedHeadline.Foreground = edge;
        UnsavedDetail.Foreground = new SolidColorBrush(palette.Label);
        RefusalHeadline.Foreground = edge;
        RefusalDismissHint.Foreground = new SolidColorBrush(palette.Label);

        ToolBar.Background = paper;
        ToolBar.BorderBrush = new SolidColorBrush(palette.GridMajor);
        ToolRowDivider.Background = new SolidColorBrush(palette.GridMajor);
        ToolRowActionsDivider.Background = new SolidColorBrush(palette.GridMajor);
        StockToolboxPanel.ApplyPalette(palette);

        // The tool icons are drawn the way the stock category icons are, so the row reads as one.
        foreach (Button button in ToolButtons.Concat(TurnButtons))
        {
            if (button.Content is Avalonia.Controls.Shapes.Path glyph)
            {
                glyph.Stroke = new SolidColorBrush(palette.Dimension);
                glyph.Fill = new SolidColorBrush(palette.PreviewFill);
            }
        }

        RelationshipsPanel.Background = paper;
        RelationshipsPanel.BorderBrush = new SolidColorBrush(palette.GridMajor);
        RelationshipsHeadline.Foreground = edge;

        PropertiesPanel.Background = paper;
        PropertiesPanel.BorderBrush = new SolidColorBrush(palette.GridMajor);
        PropertiesHeadline.Foreground = edge;
        PropertiesError.Foreground = new SolidColorBrush(palette.Snap);
        StockReadout.Foreground = new SolidColorBrush(palette.Label);

        WorkshopSheet.Background = paper;
        WorkshopHeadline.Foreground = edge;
        WorkshopHint.Foreground = new SolidColorBrush(palette.Label);
        WorkshopStockReadout.Foreground = new SolidColorBrush(palette.Label);
        WorkshopCutsPanel.Background = paper;
        WorkshopCutsPanel.BorderBrush = new SolidColorBrush(palette.GridMajor);
        WorkshopCutsHeadline.Foreground = edge;
        WorkshopCutsEmpty.Foreground = new SolidColorBrush(palette.Label);
        CutHeadline.Foreground = edge;
        CutReadout.Foreground = new SolidColorBrush(palette.Label);
        CutError.Foreground = new SolidColorBrush(palette.Snap);
        CutFieldsRule.BorderBrush = new SolidColorBrush(palette.GridMajor);

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

            item.Click += (_, _) => OpenSampleCommand(source);
            _sampleItems.Add(item);
        }

        SamplesMenu.ItemsSource = _sampleItems;
        NewMenuItem.InputGesture = new KeyGesture(Key.N, command);
        OpenMenuItem.InputGesture = new KeyGesture(Key.O, command);
        SaveMenuItem.InputGesture = new KeyGesture(Key.S, command);
        SaveAsMenuItem.InputGesture = new KeyGesture(Key.S, command | KeyModifiers.Shift);
        UndoMenuItem.InputGesture = UndoGestures[0];
        RedoMenuItem.InputGesture = RedoGestures[0];
        ZoomToFitMenuItem.InputGesture = new KeyGesture(Key.D0, command);
        ZoomInMenuItem.InputGesture = new KeyGesture(Key.OemPlus);
        ZoomOutMenuItem.InputGesture = new KeyGesture(Key.OemMinus);
        SelectToolMenuItem.InputGesture = new KeyGesture(Key.S);
        RectangleToolMenuItem.InputGesture = new KeyGesture(Key.R);
        ShapeMenuItem.InputGesture = new KeyGesture(Key.C);
        DuplicateMenuItem.InputGesture = new KeyGesture(Key.D);
        PinMenuItem.InputGesture = new KeyGesture(Key.P);
        DeleteMenuItem.InputGesture = new KeyGesture(Key.Delete);
        TurnXMenuItem.InputGesture = new KeyGesture(Key.X);
        TurnYMenuItem.InputGesture = new KeyGesture(Key.Y);
        TurnZMenuItem.InputGesture = new KeyGesture(Key.Z);
        PlanViewMenuItem.InputGesture = new KeyGesture(Key.V);
        ModelViewMenuItem.InputGesture = new KeyGesture(Key.V);
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
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.S, command),
            Command = new RelayCommand(() => _ = SaveAsync()),
        });
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.S, command | KeyModifiers.Shift),
            Command = new RelayCommand(() => _ = SaveAsAsync()),
        });

        foreach (KeyGesture gesture in UndoGestures)
        {
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = new RelayCommand(() => UndoCommand()) });
        }

        foreach (KeyGesture gesture in RedoGestures)
        {
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = new RelayCommand(() => RedoCommand()) });
        }

        for (int i = 0; i < Samples.Count && i < 9; i++)
        {
            IDesignSource source = Samples[i];
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.D1 + i, command),
                Command = new RelayCommand(() => OpenSampleCommand(source)),
            });
        }
    }

    /// <summary>
    /// The platform's own undo keys — Control+Z on Windows, Command+Z on macOS — read from the
    /// platform the way <see cref="CommandModifier"/> is, rather than hardcoded.
    /// </summary>
    static IReadOnlyList<KeyGesture> UndoGestures =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.Undo is { Count: > 0 } undo
            ? [.. undo]
            : [new KeyGesture(Key.Z, CommandModifier)];

    /// <summary>
    /// The platform's own redo keys — Avalonia lists command+Y and command+Shift+Z — every one of
    /// them bound. The menu shows the first, so the list is ordered by convention: Control+Y
    /// first on Windows, Command+Shift+Z first on macOS, where Command+Y is not redo.
    /// </summary>
    static IReadOnlyList<KeyGesture> RedoGestures
    {
        get
        {
            KeyModifiers command = CommandModifier;
            List<KeyGesture> redo = Application.Current?.PlatformSettings?.HotkeyConfiguration.Redo is { Count: > 0 } listed
                ? [.. listed]
                : [new KeyGesture(Key.Y, command), new KeyGesture(Key.Z, command | KeyModifiers.Shift)];

            bool mac = command.HasFlag(KeyModifiers.Meta);
            return
            [
                .. redo.OrderBy(gesture =>
                    gesture.Key == Key.Z && gesture.KeyModifiers.HasFlag(KeyModifiers.Shift) ? (mac ? 0 : 1) : (mac ? 1 : 0)),
            ];
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

        // While the save question is up, Escape answers it with Cancel, and no other key reaches
        // the drawing underneath.
        if (IsAskingToSave)
        {
            if (e.Key == Key.Escape)
            {
                KeepEditing();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Escape && IsShapingPart && !IsRefusalShowing)
        {
            CloseWorkshop();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && IsRefusalShowing)
        {
            DismissRefusal();
            e.Handled = true;
            return;
        }

        // A key typed into a field is text, not a view command: the properties panel gets the same
        // guard the dimension editor has had from the start, so that "-" and "+" in 1'-4 1/4"
        // cannot reach HandleViewKey and zoom the drawing. Whether Avalonia's own TextBox already
        // stops every one of those keys is not something to rely on, and the headless platform
        // cannot be used to find out — it routes them differently from a real backend.
        if (IsShowingModel)
        {
            if (!ModelDrawing.IsFocused && !IsShapingPart && !PropertiesPanel.IsKeyboardFocusWithin
                && ModelDrawing.HandleViewKey(e.Key, e.KeyModifiers))
            {
                e.Handled = true;
            }

            return;
        }

        if (!DrawingCanvas.IsFocused && !IsEditingDimension && !IsShapingPart
            && !PropertiesPanel.IsKeyboardFocusWithin
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
        FocusDrawing();
    }

    void OnRectangleToolClicked(object? sender, RoutedEventArgs e)
    {
        // Drawing is the plan's (assembly-model §6): asking for the rectangle brings the plan back.
        ShowPlanView();
        DrawingCanvas.Tool = EditTool.Rectangle;
        UpdateToolButtons();
        FocusDrawing();
    }

    void OnDuplicateClicked(object? sender, RoutedEventArgs e) => DrawingCanvas.DuplicateSelection();

    void OnPinClicked(object? sender, RoutedEventArgs e) => DrawingCanvas.PinSelection();

    void OnDeleteClicked(object? sender, RoutedEventArgs e) => DrawingCanvas.DeleteSelection();

    void OnMessageOfferClicked(object? sender, RoutedEventArgs e) => TakeRemoveOffer();

    void OnCutListClicked(object? sender, RoutedEventArgs e) => OpenCutList();

    void OnZoomToFitClicked(object? sender, RoutedEventArgs e)
    {
        if (IsShowingModel)
        {
            ModelDrawing.ZoomToFit();
        }
        else
        {
            DrawingCanvas.ZoomToFit();
        }
    }

    void OnZoomInClicked(object? sender, RoutedEventArgs e)
    {
        if (IsShowingModel)
        {
            ModelDrawing.ZoomIn();
        }
        else
        {
            DrawingCanvas.ZoomIn();
        }
    }

    void OnZoomOutClicked(object? sender, RoutedEventArgs e)
    {
        if (IsShowingModel)
        {
            ModelDrawing.ZoomOut();
        }
        else
        {
            DrawingCanvas.ZoomOut();
        }
    }

    void OnPlanViewClicked(object? sender, RoutedEventArgs e) => ShowPlanView();

    void OnModelViewClicked(object? sender, RoutedEventArgs e) => ShowModelView();

    void OnTurnXClicked(object? sender, RoutedEventArgs e) => SelectionTurn.Turn(Editor, Axis.X, 1);

    void OnTurnYClicked(object? sender, RoutedEventArgs e) => SelectionTurn.Turn(Editor, Axis.Y, 1);

    void OnTurnZClicked(object? sender, RoutedEventArgs e) => SelectionTurn.Turn(Editor, Axis.Z, 1);

    void OnExitClicked(object? sender, RoutedEventArgs e) => _ = WhenChangesAreSafe("quitting", CloseNow);

    void OnSaveClicked(object? sender, RoutedEventArgs e) => _ = SaveAsync();

    void OnSaveAsClicked(object? sender, RoutedEventArgs e) => _ = SaveAsAsync();

    void OnUndoClicked(object? sender, RoutedEventArgs e) => UndoCommand();

    void OnRedoClicked(object? sender, RoutedEventArgs e) => RedoCommand();

    void OnUnsavedSaveClicked(object? sender, RoutedEventArgs e) => _ = SaveThenCarryOnAsync();

    void OnUnsavedDiscardClicked(object? sender, RoutedEventArgs e) => _ = DiscardThenCarryOnAsync();

    void OnUnsavedCancelClicked(object? sender, RoutedEventArgs e) => KeepEditing();

    void UpdateZoomReadout() => ZoomText.Text = string.Create(
        CultureInfo.InvariantCulture,
        $"Zoom {(IsShowingModel ? ModelDrawing.Camera.ZoomPercent : DrawingCanvas.View.ZoomPercent):0.#}%");

    /// <summary>Where on a part the pointer is in the 3D view, in feet, inches and fractions.</summary>
    void UpdateCursorReadout(Vector3d? point) => CursorText.Text = point is { } at
        ? $"x {Show(Near(at.X))}   y {Show(Near(at.Y))}   z {Show(Near(at.Z))}"
        : "x —   y —   z —";

    /// <summary>A display value: the pointer's position rounded onto the grid, as the plan's readout is.</summary>
    static Length Near(double inches) => Length.FromInches(inches, Rounding.HalfAwayFromZero);

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

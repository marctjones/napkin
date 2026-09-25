using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Design = Napkin.Modules.Editing.Design;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Napkin.Modules.Building;
using Napkin.Modules.Furniture;
using Napkin.Modules.Editing;

namespace Napkin.App;

public partial class MainWindow
{
    /// <summary>
    /// Lights the panels that sit on the drawing from the same palette the drawing uses, so they
    /// read as notes on the paper in either theme.
    /// </summary>
    void ApplyEditingPalette()
    {
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant, new SketchLook(Settings.Current.SketchPaper, Settings.Current.SketchLine));
        SketchPaper sheet = Settings.Current.SketchPaper;
        SolidColorBrush paper = new(ChromeColours.Note(sheet, palette.Background));
        SolidColorBrush edge = new(palette.Dimension);
        if (ChromeColours.NoteRule(sheet) is Color rule)
        {
            palette = palette with { GridMajor = rule };
        }

        // A sheet is light whatever the theme, so the notes on it wear the light theme's controls too.
        ThemeVariant? noteTheme = sheet == SketchPaper.Screen ? null : ThemeVariant.Light;
        foreach (Control note in (Control[])
        [
            RefusalPanel, UnsavedPanel, ToolBar, ViewSnapBar, RelationshipsPanel, PropertiesPanel,
            StockToolboxPanel, DimensionEditor, .. WorkshopSheet.Notes,
        ])
        {
            note.SetValue(ThemeVariantScope.RequestedThemeVariantProperty, noteTheme);
            if (!note.Classes.Contains("note"))
            {
                note.Classes.Add("note");
            }
        }

        RefusalPanel.Background = paper;
        RefusalPanel.BorderBrush = edge;
        JoinPanel.Background = paper;
        JoinPanel.BorderBrush = edge;
        JoinMessage.Foreground = edge;
        UnsavedPanel.Background = paper;
        UnsavedPanel.BorderBrush = edge;
        UnsavedHeadline.Foreground = edge;
        UnsavedDetail.Foreground = new SolidColorBrush(palette.Label);
        RefusalHeadline.Foreground = edge;
        RefusalDismissHint.Foreground = new SolidColorBrush(palette.Label);

        ToolBar.Background = paper;
        ViewSnapBar.Background = paper;
        ViewSnapBar.BorderBrush = new SolidColorBrush(palette.GridMajor);
        ViewChips.Background = paper;
        ViewChips.BorderBrush = new SolidColorBrush(palette.GridMajor);
        ToolBar.BorderBrush = new SolidColorBrush(palette.GridMajor);
        ToolRowDivider.Background = new SolidColorBrush(palette.GridMajor);
        ToolRowActionsDivider.Background = new SolidColorBrush(palette.GridMajor);
        StockToolboxPanel.ApplyPalette(palette);

        // The tool icons are drawn the way the stock category icons are, so the row reads as one.
        foreach (Button button in ToolButtons.Concat(TurnButtons).Concat(ViewSnapButtons))
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

        WorkshopSheet.ApplyPalette(palette, paper, edge);

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

    /// <summary>Where the person's preferences live.</summary>
    public SettingsStore Settings { get; }

    /// <summary>
    /// Whether new windows carry the bench title bar: macOS only. The GUI test harness turns it off so
    /// layout and hit-test assertions mean the same on every platform (headless windows have no native
    /// title bar to extend into).
    /// </summary>
    public static bool BenchTitleBar { get; set; } = OperatingSystem.IsMacOS();

    /// <summary>
    /// On macOS the bench colour runs up into the title bar: the system's traffic-light buttons stay,
    /// drawn over our bar, which carries the title and drags the window. Other platforms keep their
    /// system title bar untouched. To undo: delete this method and its call.
    /// </summary>
    void ExtendTitleBarIntoBench()
    {
        if (!BenchTitleBar)
        {
            return;
        }

        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = -1;
        TitleBar.IsVisible = true;
        TitleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(TitleBar).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        };
    }

    /// <summary>Puts the remembered preferences on the window, and says so when the file could not be used.</summary>
    void ApplySettings()
    {
        // The projection first: the readout the others refresh notices a projection that differs from
        // the settings and would save it over them.
        ModelDrawing.Projection = Settings.Current.Projection;
        ApplyTheme(Settings.Current.Theme);
        ApplySketch();
        ApplyOpenIn(Settings.Current.OpenIn);
        ApplyGrid(Settings.Current.ShowGrid, Settings.Current.SnapToGrid);
        ApplyHiddenEdges(Settings.Current.ShowHiddenEdges);
        _showRulers = Settings.Current.ShowRulers;
        DrawingCanvas.ShowRulers = _showRulers;
        ModelDrawing.ShowScaleBar = ModelDrawing.ShowRulers = _showRulers;
        UpdateRulerLayout();

        if (Settings.Notice is { } notice)
        {
            MessageText.Text = notice;
            MessageBar.IsVisible = true;
        }
    }

    void OnGridClicked(object? sender, RoutedEventArgs e) => ToggleGrid();

    void ToggleGrid()
    {
        bool show = !Settings.Current.ShowGrid;
        Settings.Update(s => s with { ShowGrid = show });
        ApplyGrid(show, Settings.Current.SnapToGrid);
    }

    void OnHiddenEdgesClicked(object? sender, RoutedEventArgs e) => ToggleHiddenEdges();

    /// <summary>
    /// Hidden edges on or off (standard-views §2.3), remembered. Only a standard view other than Top
    /// draws them; anywhere else the key says where to look instead of changing what cannot be seen.
    /// </summary>
    void ToggleHiddenEdges()
    {
        if (!IsShowingStandardView)
        {
            Editor.Say(EditSeverity.Hint, StandardViewWords.HiddenEdgesElsewhere);
            return;
        }

        bool show = !Settings.Current.ShowHiddenEdges;
        Settings.Update(s => s with { ShowHiddenEdges = show });
        ApplyHiddenEdges(show);
        Editor.Say(EditSeverity.Done, show ? StandardViewWords.HiddenEdgesShown : StandardViewWords.HiddenEdgesNotShown);
    }

    void ApplyHiddenEdges(bool show)
    {
        ModelDrawing.ShowHiddenEdges = show;
        HiddenEdgesMenuItem.Icon = show ? new TextBlock { Text = "✓" } : null;
    }

    void OnSnapToGridClicked(object? sender, RoutedEventArgs e)
    {
        bool snap = !Settings.Current.SnapToGrid;
        Settings.Update(s => s with { SnapToGrid = snap });
        ApplyGrid(Settings.Current.ShowGrid, snap);
    }

    /// <summary>The grid's lines and its snapping, each on every view that has them.</summary>
    void ApplyGrid(bool show, bool snap)
    {
        DrawingCanvas.ShowGrid = ModelDrawing.ShowGrid = show;
        DrawingCanvas.SnapToGrid = ModelDrawing.SnapToGrid = WorkshopSheet.Drawing.SnapToGrid = snap;
        GridMenuItem.Icon = show ? new TextBlock { Text = "✓" } : null;
        SnapToGridMenuItem.Icon = snap ? new TextBlock { Text = "✓" } : null;
        UpdateZoomReadout();
    }

    void OnOpenInPlanClicked(object? sender, RoutedEventArgs e) => ChooseOpenIn(OpenDesignsIn.Plan);

    void OnOpenInModelClicked(object? sender, RoutedEventArgs e) => ChooseOpenIn(OpenDesignsIn.Model);

    void OnOpenInLastClicked(object? sender, RoutedEventArgs e) => ChooseOpenIn(OpenDesignsIn.LastUsed);

    /// <summary>Takes effect on the next design; the one on screen stays in the view it is in.</summary>
    void ChooseOpenIn(OpenDesignsIn choice)
    {
        Settings.Update(s => s with { OpenIn = choice });
        ApplyOpenIn(choice);
    }

    void ApplyOpenIn(OpenDesignsIn choice)
    {
        OpenInPlanMenuItem.Icon = choice == OpenDesignsIn.Plan ? new TextBlock { Text = "✓" } : null;
        OpenInModelMenuItem.Icon = choice == OpenDesignsIn.Model ? new TextBlock { Text = "✓" } : null;
        OpenInLastMenuItem.Icon = choice == OpenDesignsIn.LastUsed ? new TextBlock { Text = "✓" } : null;
    }

    void OnThemeLightClicked(object? sender, RoutedEventArgs e) => ChooseTheme(ThemeChoice.Light);

    void OnThemeDarkClicked(object? sender, RoutedEventArgs e) => ChooseTheme(ThemeChoice.Dark);

    void OnThemeSystemClicked(object? sender, RoutedEventArgs e) => ChooseTheme(ThemeChoice.FollowSystem);

    void ChooseTheme(ThemeChoice theme)
    {
        Settings.Update(s => s with { Theme = theme });
        ApplyTheme(theme);
    }

    /// <summary>Puts the theme on the whole application, so every window and panel changes at once.</summary>
    void ApplyTheme(ThemeChoice theme)
    {
        // The window itself is asked too: a window can be shown with no application theme to inherit.
        Avalonia.Styling.ThemeVariant variant = theme switch
        {
            ThemeChoice.Light => Avalonia.Styling.ThemeVariant.Light,
            ThemeChoice.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default,
        };
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = variant;
        }

        RequestedThemeVariant = variant;
        ThemeLightMenuItem.Icon = theme == ThemeChoice.Light ? new TextBlock { Text = "✓" } : null;
        ThemeDarkMenuItem.Icon = theme == ThemeChoice.Dark ? new TextBlock { Text = "✓" } : null;
        ThemeSystemMenuItem.Icon = theme == ThemeChoice.FollowSystem ? new TextBlock { Text = "✓" } : null;
    }

    void OnPaperScreenClicked(object? sender, RoutedEventArgs e) => ChoosePaper(SketchPaper.Screen);

    void OnPaperGraphClicked(object? sender, RoutedEventArgs e) => ChoosePaper(SketchPaper.Graph);

    void OnPaperPlainClicked(object? sender, RoutedEventArgs e) => ChoosePaper(SketchPaper.Plain);

    void OnPaperNapkinClicked(object? sender, RoutedEventArgs e) => ChoosePaper(SketchPaper.Napkin);

    void OnLineCleanClicked(object? sender, RoutedEventArgs e) => ChooseLine(SketchLine.Clean);

    void OnLinePencilClicked(object? sender, RoutedEventArgs e) => ChooseLine(SketchLine.Pencil);

    void OnLineCarpenterClicked(object? sender, RoutedEventArgs e) => ChooseLine(SketchLine.Carpenter);

    void ChoosePaper(SketchPaper paper)
    {
        Settings.Update(s => s with { SketchPaper = paper });
        ApplySketch();
    }

    void ChooseLine(SketchLine line)
    {
        Settings.Update(s => s with { SketchLine = line });
        ApplySketch();
    }

    /// <summary>Puts the remembered paper and pencil on both drawings and ticks them in the menu (#142). Drawing only.</summary>
    void ApplySketch()
    {
        SketchPaper paper = Settings.Current.SketchPaper;
        SketchLine line = Settings.Current.SketchLine;
        DrawingCanvas.Look = ModelDrawing.Look = new SketchLook(paper, line);
        TextBlock? Tick(bool on) => on ? new TextBlock { Text = "✓" } : null;
        PaperScreenMenuItem.Icon = Tick(paper == SketchPaper.Screen);
        PaperGraphMenuItem.Icon = Tick(paper == SketchPaper.Graph);
        PaperPlainMenuItem.Icon = Tick(paper == SketchPaper.Plain);
        PaperNapkinMenuItem.Icon = Tick(paper == SketchPaper.Napkin);
        LineCleanMenuItem.Icon = Tick(line == SketchLine.Clean);
        LinePencilMenuItem.Icon = Tick(line == SketchLine.Pencil);
        LineCarpenterMenuItem.Icon = Tick(line == SketchLine.Carpenter);
        ApplyEditingPalette();
    }

    void UpdateZoomReadout()
    {
        if (Settings.Current.Projection != ModelDrawing.Projection)
        {
            Settings.Update(s => s with { Projection = ModelDrawing.Projection });
        }

        // A standard view is orthographic by definition and nothing in it snaps: its zoom is all it says.
        ZoomText.Text = IsShowingStandardView
            ? string.Create(CultureInfo.InvariantCulture, $"Zoom {ModelDrawing.Camera.ZoomPercent:0.#}%")
            : IsShowingModel
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{(ModelDrawing.Projection == CameraProjection.Perspective ? "Perspective" : "Orthographic")} · Zoom {ModelDrawing.Camera.ZoomPercent:0.#}%")
            : string.Create(CultureInfo.InvariantCulture, $"Zoom {DrawingCanvas.View.ZoomPercent:0.#}%");

        // While snapping is on, the step a drag lands on: it changes with the zoom, as the grid does.
        if (Settings.Current.SnapToGrid && !IsShowingStandardView)
        {
            double step = IsShowingModel ? ModelDrawing.GridStepInches : DrawingCanvas.GridStepInches;
            ZoomText.Text += " · Snap " + Show(new Length(SnapGrid.UnitsPerStep(step)));
        }

        // The projection belongs to the 3D view: the plan has none to choose.
        bool perspective = ModelDrawing.Projection == CameraProjection.Perspective;
        OrthographicMenuItem.IsEnabled = PerspectiveMenuItem.IsEnabled = _view == DesignView.Model;
        ViewText.Text = _view == DesignView.Model && perspective ? "3D, perspective" : StandardViews.Name(_view);
        OrthographicMenuItem.Icon = perspective ? null : new TextBlock { Text = "✓" };
        PerspectiveMenuItem.Icon = perspective ? new TextBlock { Text = "✓" } : null;

        // O switches, so it shows on the one it would switch to.
        OrthographicMenuItem.InputGesture = perspective ? Gesture(ViewCommand.Projection) : null;
        PerspectiveMenuItem.InputGesture = perspective ? null : Gesture(ViewCommand.Projection);
    }

    bool _showRulers;

    /// <summary>The margins the floating panels sit at with no ruler under them.</summary>
    static readonly Thickness ToolBarMargin = new(10);
    static readonly Thickness ToolboxMargin = new(10, 56, 10, 10);
    static readonly Thickness SidePanelsMargin = new(10);

    void OnRulersClicked(object? sender, RoutedEventArgs e)
    {
        _showRulers = !_showRulers;
        DrawingCanvas.ShowRulers = _showRulers;
        ModelDrawing.ShowScaleBar = ModelDrawing.ShowRulers = _showRulers;
        Settings.Update(s => s with { ShowRulers = _showRulers });
        UpdateRulerLayout();
    }

    /// <summary>
    /// The rulers are drawn over the plan's top and left edges, so the floating panels that sit there
    /// move in by a ruler's thickness while they are showing, and only in the plan.
    /// </summary>
    void UpdateRulerLayout()
    {
        RulersMenuItem.Icon = _showRulers ? new TextBlock { Text = "✓" } : null;

        double t = _showRulers && (!IsShowingModel || IsShowingStandardView) ? CanvasView.RulerThickness : 0;
        ToolBar.Margin = ToolBarMargin + new Thickness(t, t, 0, 0);
        StockToolboxPanel.Margin = ToolboxMargin + new Thickness(t, t, 0, 0);
        SidePanels.Margin = SidePanelsMargin + new Thickness(0, t, 0, 0);
    }

    void OnOrthographicClicked(object? sender, RoutedEventArgs e) => ModelDrawing.Projection = CameraProjection.Orthographic;

    void OnPerspectiveClicked(object? sender, RoutedEventArgs e) => ModelDrawing.Projection = CameraProjection.Perspective;

    /// <summary>
    /// Where the pointer is, in feet, inches and fractions: on a part in the 3D view, all three
    /// coordinates; in a standard view, only the two it can show (standard-views §5.3) — never the
    /// third, which a flat view cannot know.
    /// </summary>
    void UpdateCursorReadout(Vector3d? point)
    {
        if (StandardViews.Of(_view) is { } view && IsShowingStandardView)
        {
            (Axis across, Axis upward) = StandardViewFrame.Readable(view);
            CursorText.Text = point is { } on
                ? $"{Label(across)} {Show(Near(Component(on, across)))}   {Label(upward)} {Show(Near(Component(on, upward)))}"
                : $"{Label(across)} —   {Label(upward)} —";
            return;
        }

        CursorText.Text = point is { } at
            ? $"x {Show(Near(at.X))}   y {Show(Near(at.Y))}   z {Show(Near(at.Z))}"
            : "x —   y —   z —";
    }

    static string Label(Axis axis) => axis.ToString().ToLowerInvariant();

    static double Component(Vector3d point, Axis axis) => axis switch
    {
        Axis.X => point.X,
        Axis.Y => point.Y,
        _ => point.Z,
    };

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

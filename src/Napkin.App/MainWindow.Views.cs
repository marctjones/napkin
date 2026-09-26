using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Napkin.App.Designs;
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
    // ---------------------------------------------------------------------------------------
    // The 3D view (docs/design/assembly-model.md §8)
    // ---------------------------------------------------------------------------------------

    /// <summary>The 3D view.</summary>
    public ModelView Model => ModelDrawing;

    /// <summary>The sheet (standard-views §11): Top, Front, Right and 3D at once.</summary>
    public SheetView Sheet => SheetDrawing;

    /// <summary>Whether the sheet is on screen, in place of the plan and the single views.</summary>
    public bool IsShowingSheet => SheetDrawing.IsVisible;

    /// <summary>The Parts view (parts-view.md).</summary>
    public PartsView Parts => PartsDrawing;

    /// <summary>Whether the Parts view is on screen.</summary>
    public bool IsShowingParts => PartsDrawing.IsVisible;

    /// <summary>Whether the plan canvas is on screen: the one view the plan's drawing tools work in.</summary>
    public bool IsShowingPlan => DrawingCanvas.IsVisible;

    /// <summary>
    /// Whether 3D controls are on screen rather than the plan: the free 3D view, a locked standard view
    /// (Bottom–Right, the same control with its camera locked), or the sheet's panes.
    /// </summary>
    public bool IsShowingModel => ModelDrawing.IsVisible || IsShowingSheet;

    /// <summary>
    /// The 3D control the window's view commands and readouts go to: on the sheet, the pane the pointer
    /// was last over, else Front (§11.5); otherwise the 3D view's control.
    /// </summary>
    ModelView ActiveModel => IsShowingSheet ? SheetDrawing.Active : ModelDrawing;

    /// <summary>Every 3D control, shown or not: what a setting that changes how they draw reaches.</summary>
    IEnumerable<ModelView> ModelViews => [ModelDrawing, .. SheetDrawing.Panes];

    DesignView _view = DesignView.Top;
    DesignView _last2DView = DesignView.Top;

    /// <summary>The view on screen (docs/design/standard-views.md §4.1): one of the six, or 3D.</summary>
    public DesignView CurrentView => _view;

    /// <summary>
    /// Whether a read-only standard view is what the window speaks for: Bottom, Front, Back, Left or
    /// Right on its own, or a locked pane of the sheet.
    /// </summary>
    public bool IsShowingStandardView => IsShowingModel && ActiveModel.Locked is not null;

    /// <summary>The <em>View</em> menu's item for a view.</summary>
    public MenuItem ViewMenuEntry(DesignView view) => view switch
    {
        DesignView.Top => TopViewMenuItem,
        DesignView.Bottom => BottomViewMenuItem,
        DesignView.Front => FrontViewMenuItem,
        DesignView.Back => BackViewMenuItem,
        DesignView.Left => LeftViewMenuItem,
        DesignView.Right => RightViewMenuItem,
        DesignView.Parts => PartsViewMenuItem,
        _ => ModelViewMenuItem,
    };

    /// <summary>The view switcher's chip for a view (automation name "View: Front").</summary>
    public ToggleButton ViewChip(DesignView view) => view switch
    {
        DesignView.Top => TopViewChip,
        DesignView.Bottom => BottomViewChip,
        DesignView.Front => FrontViewChip,
        DesignView.Back => BackViewChip,
        DesignView.Left => LeftViewChip,
        DesignView.Right => RightViewChip,
        DesignView.Parts => PartsViewChip,
        _ => ModelViewChip,
    };

    /// <summary>What the status bar says the view is: "Top", "Front", "3D" or "3D, perspective".</summary>
    public string ViewReadout => ViewText.Text ?? string.Empty;

    /// <summary>The three turn buttons, about X, Y and Z, shown on the toolbar in the 3D view.</summary>
    public IReadOnlyList<Button> TurnButtons => [TurnXToolButton, TurnYToolButton, TurnZToolButton];

    /// <summary>The 3D view's quick view-snap buttons (#132), shown only there.</summary>
    public IReadOnlyList<Button> ViewSnapButtons => [LookXToolButton, LookYToolButton, LookZToolButton, NextSurfaceToolButton];

    /// <summary>
    /// Shows a view of the drawing (standard-views §4.1): the one entry point for the View menu, the
    /// keys 1–7, the chips, <c>V</c> and the open-in setting, and the one place that brings the rest
    /// of the window along — the menu ticks, the pressed chip, the status text, the remembered view
    /// and the toolbar.
    /// </summary>
    /// <remarks>
    /// Top is the plan canvas; 3D and the other five are the 3D view's control, free or locked to a
    /// direction. Nothing about the drawing changes, and each view is where it was left. Between the
    /// plan and 3D what the tools hold comes along (#74, assembly-model §6 as amended); a read-only
    /// view puts it down, goes back to Select and closes the dimension editor (§5.4).
    /// </remarks>
    public void ShowView(DesignView view)
    {
        DesignView from = _view;
        bool leavingSheet = IsShowingSheet;
        bool showing = !IsShowingSheet && view switch
        {
            DesignView.Top => DrawingCanvas.IsVisible,
            DesignView.Parts => PartsDrawing.IsVisible,
            _ => ModelDrawing.IsVisible && ModelDrawing.Locked == StandardViews.Of(view),
        };
        if (view == from && showing)
        {
            FocusDrawing();
            return;
        }

        CloseDimensionEditor(focusCanvas: false);
        PartsDrawing.IsVisible = false;
        if (view == DesignView.Parts)
        {
            // The Parts view is for reading the pieces (parts-view §4): the tools go back to Select.
            CloseWorkshop();
            DrawingCanvas.ArmStock(null);
            DrawingCanvas.Tool = EditTool.Select;
            ModelDrawing.Disarm();
            DrawingCanvas.IsVisible = false;
            ModelDrawing.IsVisible = false;
            SheetDrawing.IsVisible = false;
            PartsDrawing.IsVisible = true;
        }
        else if (view == DesignView.Top)
        {
            CloseWorkshop();

            // What the 3D view was holding goes back to the plan's tools.
            if (ModelDrawing.Placement.Stock is { } held)
            {
                DrawingCanvas.ArmStock(held);
            }
            else if (ModelDrawing.Placement.PlainBoard)
            {
                DrawingCanvas.Tool = EditTool.Rectangle;
            }

            ModelDrawing.Disarm();
            ModelDrawing.IsVisible = false;
            SheetDrawing.IsVisible = false;
            DrawingCanvas.IsVisible = true;
        }
        else
        {
            CloseWorkshop();

            // What the plan's tools were holding comes along into 3D: the 3D view places it on the face
            // under the pointer (#74). A read-only view takes nothing (§5.4).
            if (view == DesignView.Model && DrawingCanvas.IsVisible)
            {
                if (DrawingCanvas.ArmedStock is { } held)
                {
                    ModelDrawing.Arm(held);
                }
                else if (DrawingCanvas.Tool == EditTool.Rectangle)
                {
                    ModelDrawing.ArmPlainBoard();
                }
            }

            DrawingCanvas.ArmStock(null);
            DrawingCanvas.Tool = EditTool.Select;
            ModelDrawing.Locked = StandardViews.Of(view);
            DrawingCanvas.IsVisible = false;
            SheetDrawing.IsVisible = false;
            ModelDrawing.IsVisible = true;
        }

        _view = view;

        // V goes back to the last standard view; the Parts view has its own switch (parts-view §10.5).
        if (StandardViews.Of(view) is not null)
        {
            _last2DView = view;
        }

        if (Settings.Current.LastView != view || leavingSheet)
        {
            Settings.Update(s => s with { LastView = view, ShowSheet = s.ShowSheet && !leavingSheet });
        }

        ShowViewChrome();
        UpdateToolButtons();
        if (view == DesignView.Model)
        {
            Editor.Say(
                EditSeverity.Hint,
                ModelDrawing.Placement.IsArmed
                    ? $"3D view: holding {ModelDrawing.Placement.Holding} — click or drag on a face, or the floor, to place it; Escape puts it down."
                    : "3D view: drag a part to slide it, or empty space to orbit; on a selected part drag an arrow "
                      + "to move it along that axis or a square to resize it; X, Y and Z turn it. V goes back to the plan.");
        }
        else if (view == DesignView.Parts)
        {
            Editor.Say(EditSeverity.Hint, StandardViewWords.PartsHint);
        }
        else if (view != DesignView.Top)
        {
            Editor.Say(EditSeverity.Hint, StandardViewWords.ReadOnlyHint(StandardViews.Of(view)!.Value));
        }
    }

    /// <summary>
    /// The sheet on or off (standard-views §11.1), remembered. On, it takes the drawing's place with
    /// Select as the tool; off, the single view the person was last in comes back — as any view key,
    /// chip or menu item also does.
    /// </summary>
    public void ShowSheet(bool on)
    {
        if (on == IsShowingSheet)
        {
            FocusDrawing();
            return;
        }

        if (!on)
        {
            ShowView(_view);
            return;
        }

        CloseDimensionEditor(focusCanvas: false);
        CloseWorkshop();
        DrawingCanvas.ArmStock(null);
        DrawingCanvas.Tool = EditTool.Select;
        ModelDrawing.Disarm();
        SheetDrawing.PaneFor(null).Projection = ModelDrawing.Projection;
        DrawingCanvas.IsVisible = false;
        ModelDrawing.IsVisible = false;
        PartsDrawing.IsVisible = false;
        SheetDrawing.IsVisible = true;
        if (!Settings.Current.ShowSheet)
        {
            Settings.Update(s => s with { ShowSheet = true });
        }

        ShowViewChrome();
        UpdateToolButtons();
        Editor.Say(EditSeverity.Hint, StandardViewWords.SheetHint);
    }

    void OnSheetClicked(object? sender, RoutedEventArgs e) => ShowSheet(!IsShowingSheet);

    /// <summary>The Parts view's 3D option (parts-view §3), from the menu; I does the same in the view.</summary>
    void OnPartsIn3DClicked(object? sender, RoutedEventArgs e)
    {
        PartsDrawing.Isometric = !PartsDrawing.Isometric;
        FocusDrawing();
    }

    static readonly DesignView[] AllViews =
        [DesignView.Top, DesignView.Bottom, DesignView.Front, DesignView.Back, DesignView.Left, DesignView.Right, DesignView.Model, DesignView.Parts];

    readonly Dictionary<Control, object?> _toolTipsOutsideViews = [];

    /// <summary>
    /// The chrome that differs between the views: the turn and snap buttons (free 3D only), the menu
    /// ticks and the pressed chip, the status text, the tools a read-only view cannot use (§5.4), the
    /// remembered view, the readouts.
    /// </summary>
    void ShowViewChrome()
    {
        bool free3D = _view == DesignView.Model && !IsShowingSheet;

        // The sheet is for reading (§11.3): its tools are a read-only view's, whichever pane is active;
        // so are the Parts view's.
        bool readOnly = IsShowingStandardView || IsShowingSheet || IsShowingParts;
        foreach (Button button in TurnButtons)
        {
            button.IsVisible = free3D;
        }

        ViewSnapBar.IsVisible = free3D;
        foreach (MenuItem item in (MenuItem[])[LookXMenuItem, LookYMenuItem, LookZMenuItem, NextSurfaceMenuItem])
        {
            item.IsEnabled = free3D;
        }

        HiddenEdgesMenuItem.IsEnabled = readOnly;

        foreach (DesignView view in AllViews)
        {
            ViewMenuEntry(view).Icon = view == _view && !IsShowingSheet ? new TextBlock { Text = "✓" } : null;
            ViewChip(view).IsChecked = view == _view && !IsShowingSheet;
        }

        // A read-only view cannot draw, place or shape: those tools say so rather than vanish, and
        // come back with their own tooltips in the plan and 3D.
        SheetMenuItem.Icon = IsShowingSheet ? new TextBlock { Text = "✓" } : null;
        PartsIn3DMenuItem.IsEnabled = IsShowingParts;
        string? refusal = IsShowingSheet ? StandardViewWords.NotOnSheet
            : IsShowingParts ? StandardViewWords.NotInPartsView
            : StandardViews.Of(_view) is { } shown ? StandardViewWords.NotInView(shown) : null;
        foreach (Control tool in (Control[])[RectangleToolButton, RectangleToolMenuItem, StockToolboxPanel.CategoryRow])
        {
            if (!_toolTipsOutsideViews.ContainsKey(tool))
            {
                _toolTipsOutsideViews[tool] = ToolTip.GetTip(tool);
            }

            tool.IsEnabled = !readOnly;
            ToolTip.SetTip(tool, readOnly ? refusal : _toolTipsOutsideViews[tool]);
            ToolTip.SetShowOnDisabled(tool, true);
        }

        if (readOnly && StockToolboxPanel.Category is not null)
        {
            StockToolboxPanel.ShowCategory(null);
            UpdateToolbox();
        }

        UpdateRulerLayout();
        UpdateZoomReadout();
        if (IsShowingModel)
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

    void OnViewMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && AllViews.Where(view => ReferenceEquals(ViewMenuEntry(view), item)).Cast<DesignView?>().FirstOrDefault() is { } view)
        {
            ShowView(view);
        }
    }

    void OnViewChipClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton chip && AllViews.Where(view => ReferenceEquals(ViewChip(view), chip)).Cast<DesignView?>().FirstOrDefault() is { } view)
        {
            ShowView(view);
        }

        // The chip shows the view on screen, whatever the click did to its own state.
        foreach (DesignView each in AllViews)
        {
            ViewChip(each).IsChecked = each == _view && !IsShowingSheet;
        }
    }

    /// <summary>Gives the keyboard to whichever view of the drawing is showing.</summary>
    void FocusDrawing()
    {
        if (IsShowingParts)
        {
            PartsDrawing.Focus();
        }
        else if (IsShowingModel)
        {
            ActiveModel.Focus();
        }
        else
        {
            DrawingCanvas.Focus();
        }
    }

}

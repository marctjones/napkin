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

    /// <summary>
    /// Whether the 3D view's control is on screen rather than the plan: in the free 3D view or in a
    /// locked standard view (Bottom–Right), which is the same control with its camera locked.
    /// </summary>
    public bool IsShowingModel => ModelDrawing.IsVisible;

    DesignView _view = DesignView.Top;
    DesignView _last2DView = DesignView.Top;

    /// <summary>The view on screen (docs/design/standard-views.md §4.1): one of the six, or 3D.</summary>
    public DesignView CurrentView => _view;

    /// <summary>Whether a read-only standard view is showing: Bottom, Front, Back, Left or Right.</summary>
    public bool IsShowingStandardView => ModelDrawing.Locked is not null && IsShowingModel;

    /// <summary>The <em>View</em> menu's item for a view.</summary>
    public MenuItem ViewMenuEntry(DesignView view) => view switch
    {
        DesignView.Top => TopViewMenuItem,
        DesignView.Bottom => BottomViewMenuItem,
        DesignView.Front => FrontViewMenuItem,
        DesignView.Back => BackViewMenuItem,
        DesignView.Left => LeftViewMenuItem,
        DesignView.Right => RightViewMenuItem,
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
        bool showing = view == DesignView.Top ? !IsShowingModel : IsShowingModel && ModelDrawing.Locked == StandardViews.Of(view);
        if (view == from && showing)
        {
            FocusDrawing();
            return;
        }

        CloseDimensionEditor(focusCanvas: false);
        if (view == DesignView.Top)
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
            DrawingCanvas.IsVisible = true;
        }
        else
        {
            CloseWorkshop();

            // What the plan's tools were holding comes along into 3D: the 3D view places it on the face
            // under the pointer (#74). A read-only view takes nothing (§5.4).
            if (view == DesignView.Model && !IsShowingModel)
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
            ModelDrawing.IsVisible = true;
        }

        _view = view;
        if (view != DesignView.Model)
        {
            _last2DView = view;
        }

        if (Settings.Current.LastView != view)
        {
            Settings.Update(s => s with { LastView = view });
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
        else if (view != DesignView.Top)
        {
            string name = StandardViews.Name(view);
            Editor.Say(EditSeverity.Hint, $"{name} view: read-only for now — pan, zoom and select; 1 for the plan or 7 for 3D to edit.");
        }
    }

    static readonly DesignView[] AllViews =
        [DesignView.Top, DesignView.Bottom, DesignView.Front, DesignView.Back, DesignView.Left, DesignView.Right, DesignView.Model];

    readonly Dictionary<Control, object?> _toolTipsOutsideViews = [];

    /// <summary>
    /// The chrome that differs between the views: the turn and snap buttons (free 3D only), the menu
    /// ticks and the pressed chip, the status text, the tools a read-only view cannot use (§5.4), the
    /// remembered view, the readouts.
    /// </summary>
    void ShowViewChrome()
    {
        bool free3D = _view == DesignView.Model;
        bool readOnly = IsShowingStandardView;
        foreach (Button button in TurnButtons)
        {
            button.IsVisible = free3D;
        }

        ViewSnapBar.IsVisible = free3D;
        foreach (MenuItem item in (MenuItem[])[LookXMenuItem, LookYMenuItem, LookZMenuItem, NextSurfaceMenuItem])
        {
            item.IsEnabled = free3D;
        }

        foreach (DesignView view in AllViews)
        {
            ViewMenuEntry(view).Icon = view == _view ? new TextBlock { Text = "✓" } : null;
            ViewChip(view).IsChecked = view == _view;
        }

        // A read-only view cannot draw, place or shape: those tools say so rather than vanish, and
        // come back with their own tooltips in the plan and 3D.
        string refusal = $"Not in a {StandardViews.Name(_view)} view yet — 1 for the plan or 7 for 3D";
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
            ViewChip(each).IsChecked = each == _view;
        }
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

}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App;

// ---------------------------------------------------------------------------------------
// The shape workshop: one blank, its stock, and its cuts
// (docs/design/shaped-parts-model.md §7.1, §7.2). The sheet itself is Shaping/ShapeWorkshop;
// the window decides when it is open and what of its own chrome hides meanwhile.
// ---------------------------------------------------------------------------------------
public partial class MainWindow
{
    /// <summary>The workshop sheet, hidden until a part is being shaped.</summary>
    public Control WorkshopPanel => WorkshopSheet;

    /// <summary>The blank drawn large in the workshop.</summary>
    public WorkshopView Workshop => WorkshopSheet.Drawing;

    /// <summary>Whether a part is being shaped.</summary>
    public bool IsShapingPart => WorkshopSheet.IsVisible;

    /// <summary>The part being shaped, if one is.</summary>
    public EntityId? ShapedPart => WorkshopSheet.Shaping;

    /// <summary>The <em>Shape…</em> menu item.</summary>
    public MenuItem ShapeMenuEntry => ShapeMenuItem;

    /// <summary>The workshop's stock field.</summary>
    public TextBox WorkshopStockField => WorkshopSheet.StockField;

    /// <summary>The workshop's stock button.</summary>
    public Button WorkshopStockApply => WorkshopSheet.StockApply;

    /// <summary>What the library says about the workshop's stock.</summary>
    public string WorkshopStockReadoutText => WorkshopSheet.StockReadoutText;

    /// <summary>The workshop's cuts, one line each.</summary>
    public IReadOnlyList<string> WorkshopCutsOnScreen => WorkshopSheet.CutsOnScreen;

    /// <summary>The workshop's hint line.</summary>
    public string WorkshopHintText => WorkshopSheet.HintText;

    /// <summary>The selected cut's first field.</summary>
    public TextBox CutFirstField => WorkshopSheet.CutFirstField;

    /// <summary>A corner cut's second setback.</summary>
    public TextBox CutSecondField => WorkshopSheet.CutSecondField;

    /// <summary>A corner cut's angle.</summary>
    public TextBox CutAngleField => WorkshopSheet.CutAngleField;

    /// <summary>Applies the cut fields.</summary>
    public Button ApplyCut => WorkshopSheet.ApplyCut;

    /// <summary>Mitres the selected corner the full width.</summary>
    public Button FullMitre => WorkshopSheet.FullMitre;

    /// <summary>Takes the selected cut off.</summary>
    public Button RemoveCut => WorkshopSheet.RemoveCut;

    /// <summary>Whether the selected cut's fields show.</summary>
    public bool IsShowingCutFields => WorkshopSheet.IsShowingCutFields;

    /// <summary>What the cut fields say the numbers mean.</summary>
    public string CutReadoutText => WorkshopSheet.CutReadoutText;

    /// <summary>Why the cut fields changed nothing, while that is shown.</summary>
    public string CutErrorText => WorkshopSheet.CutErrorText;

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
        WorkshopSheet.Open(part);

        // The chrome that belongs to the canvas goes while the canvas is behind the sheet. The
        // workshop says what the part is in its own headline and leads with its own stock picker,
        // so nothing a person needs here is in the panels that just went.
        ToolBar.IsVisible = false;
        RelationshipsPanel.IsVisible = false;
        PropertiesPanel.IsVisible = false;
        DrawingCanvas.ArmStock(null);
        UpdateToolbox();
        UpdateMenuEnablement();
        ShowProperties();
        WorkshopSheet.Drawing.Focus();
        return true;
    }

    /// <summary>Leaves the workshop, and gives the drawing back.</summary>
    public void CloseWorkshop()
    {
        if (!WorkshopSheet.IsVisible)
        {
            return;
        }

        WorkshopSheet.Close();
        ToolBar.IsVisible = true;
        UpdateToolbox();

        UpdateRelationships();
        UpdateMenuEnablement();
        ShowProperties();
        FocusDrawing();
    }

    /// <summary>Makes the blank really be the stock the workshop's field names.</summary>
    /// <returns>Whether the drawing changed.</returns>
    public bool ApplyWorkshopStock() => WorkshopSheet.ApplyStock();

    /// <summary>Applies what the cut fields say to the selected cut.</summary>
    /// <returns>Whether the drawing changed.</returns>
    public bool ApplyCutEntry() => WorkshopSheet.ApplyCutEntry();

    void WireWorkshop()
    {
        WorkshopSheet.Editor = Editor;
        WorkshopSheet.DefaultPart = DefaultPart;
        WorkshopSheet.DoneRequested += (_, _) => CloseWorkshop();
        WorkshopSheet.StockApplied += (_, _) =>
        {
            UpdateWorkshop();
            ShowProperties();
        };
    }

    /// <summary>Refreshes the workshop from the sketch, and leaves it when its part has gone.</summary>
    void UpdateWorkshop()
    {
        if (!WorkshopSheet.Refresh())
        {
            CloseWorkshop();
        }
    }

    void OnShapeClicked(object? sender, RoutedEventArgs e) => RunSelectionCommand(SelectionCommand.Shape);
}

using System.Globalization;
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
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.App;

public partial class MainWindow
{
    void OnNewClicked(object? sender, RoutedEventArgs e) => NewSheetCommand();

    void OnOpenClicked(object? sender, RoutedEventArgs e) => _ = OpenFileAsync();

    void OnSelectToolClicked(object? sender, RoutedEventArgs e)
    {
        DrawingCanvas.Tool = EditTool.Select;
        ModelDrawing.Disarm();
        UpdateToolButtons();
        FocusDrawing();
    }

    void OnWallToolClicked(object? sender, RoutedEventArgs e) => ArmWall(null);

    void OnWall2x4Clicked(object? sender, RoutedEventArgs e) => ArmWall("2x4");

    void OnWall2x6Clicked(object? sender, RoutedEventArgs e) => ArmWall("2x6");

    void OnWindowToolClicked(object? sender, RoutedEventArgs e) => ArmOpening(OpeningKind.Window);

    void OnDoorToolClicked(object? sender, RoutedEventArgs e) => ArmOpening(OpeningKind.Door);

    /// <summary>
    /// Picks up the wall tool with a member ("2x4", "2x6") or, with null, the one it last had. Walls
    /// are drawn in the plan, so the plan comes forward if the 3D view was showing.
    /// </summary>
    public void ArmWall(string? member)
    {
        if (IsShowingModel)
        {
            ShowView(DesignView.Top);
        }

        LumberStock? stock = member is not null
                             && MaterialsLibrary.Shipped.TryFind(StockCategory.DimensionalLumber, member, out StockItem found)
            ? found as LumberStock
            : null;
        DrawingCanvas.ArmWall(stock);
        UpdateToolButtons();
        FocusDrawing();
    }

    /// <summary>Picks up the opening tool: the next click on a wall in the plan puts a window or door in it.</summary>
    public void ArmOpening(OpeningKind kind)
    {
        if (IsShowingModel)
        {
            ShowView(DesignView.Top);
        }

        DrawingCanvas.ArmOpening(kind);
        UpdateToolButtons();
        FocusDrawing();
    }

    void OnRectangleToolClicked(object? sender, RoutedEventArgs e)
    {
        // In the 3D view the rectangle tool is a plain board to place on a face (#74); a read-only
        // view has none (§5.4).
        if (IsShowingStandardView)
        {
            Editor.Say(EditSeverity.Hint, $"Not in a {StandardViews.Name(_view)} view yet — 1 for the plan or 7 for 3D.");
        }
        else if (IsShowingModel)
        {
            ModelDrawing.ArmPlainBoard();
            Editor.Say(
                EditSeverity.Hint,
                "Holding a plain board, 24\" × 12\" × 3/4\". Click on a face, or the floor, to place it, or drag along "
                + "the face for its size; Escape puts it down.");
        }
        else
        {
            DrawingCanvas.Tool = EditTool.Rectangle;
        }

        UpdateToolButtons();
        FocusDrawing();
    }

    void OnDuplicateClicked(object? sender, RoutedEventArgs e) => RunSelectionCommand(SelectionCommand.Duplicate);

    void OnMirrorEastWestClicked(object? sender, RoutedEventArgs e) => RunSelectionCommand(SelectionCommand.MirrorEastWest);

    void OnMirrorNorthSouthClicked(object? sender, RoutedEventArgs e) => RunSelectionCommand(SelectionCommand.MirrorNorthSouth);

    void OnPinClicked(object? sender, RoutedEventArgs e) => RunSelectionCommand(SelectionCommand.Pin);

    void OnDeleteClicked(object? sender, RoutedEventArgs e) => RunSelectionCommand(SelectionCommand.Delete);

    void OnMessageOfferClicked(object? sender, RoutedEventArgs e) => TakeOffer();

    void OnCutListClicked(object? sender, RoutedEventArgs e) => OpenCutList();

    void OnShoppingListClicked(object? sender, RoutedEventArgs e) => OpenShoppingList();

    void OnCutLayoutClicked(object? sender, RoutedEventArgs e) => OpenCutLayout();

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

    void OnTurnXClicked(object? sender, RoutedEventArgs e) => SelectionTurn.Turn(Editor, Axis.X, 1);

    void OnTurnYClicked(object? sender, RoutedEventArgs e) => SelectionTurn.Turn(Editor, Axis.Y, 1);

    void OnTurnZClicked(object? sender, RoutedEventArgs e) => SelectionTurn.Turn(Editor, Axis.Z, 1);

    void OnLookXClicked(object? sender, RoutedEventArgs e)
    {
        ModelDrawing.LookAlong(Axis.X);
        FocusDrawing();
    }

    void OnLookYClicked(object? sender, RoutedEventArgs e)
    {
        ModelDrawing.LookAlong(Axis.Y);
        FocusDrawing();
    }

    void OnLookZClicked(object? sender, RoutedEventArgs e)
    {
        ModelDrawing.LookAlong(Axis.Z);
        FocusDrawing();
    }

    void OnNextSurfaceClicked(object? sender, RoutedEventArgs e)
    {
        ModelDrawing.NextSurface();
        FocusDrawing();
    }

    void OnExitClicked(object? sender, RoutedEventArgs e) => _ = WhenChangesAreSafe("quitting", CloseNow);

    void OnSaveClicked(object? sender, RoutedEventArgs e) => _ = SaveAsync();

    void OnSaveAsClicked(object? sender, RoutedEventArgs e) => _ = SaveAsAsync();

    void OnUndoClicked(object? sender, RoutedEventArgs e) => UndoCommand();

    void OnRedoClicked(object? sender, RoutedEventArgs e) => RedoCommand();

    void OnUnsavedSaveClicked(object? sender, RoutedEventArgs e) => _ = SaveThenCarryOnAsync();

    void OnUnsavedDiscardClicked(object? sender, RoutedEventArgs e) => _ = DiscardThenCarryOnAsync();

    void OnUnsavedCancelClicked(object? sender, RoutedEventArgs e) => KeepEditing();
}

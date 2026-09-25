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

    void OnSelectToolClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.SelectTool);

    void SelectTool()
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

    void OnRectangleToolClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.RectangleTool);

    void RectangleTool()
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
            Editor.Say(EditSeverity.Hint, "Rectangle tool: drag on the paper to draw a part.");
        }

        UpdateToolButtons();
        FocusDrawing();
    }

    void OnDuplicateClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.Duplicate);

    void OnMirrorEastWestClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.MirrorEastWest);

    void OnMirrorNorthSouthClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.MirrorNorthSouth);

    void OnPinClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.Pin);

    void OnDeleteClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.Delete);

    void OnMessageOfferClicked(object? sender, RoutedEventArgs e) => TakeOffer();

    void OnCutListClicked(object? sender, RoutedEventArgs e) => OpenCutList();

    void OnShoppingListClicked(object? sender, RoutedEventArgs e) => OpenShoppingList();

    void OnCutLayoutClicked(object? sender, RoutedEventArgs e) => OpenCutLayout();

    void OnFastenerSizesClicked(object? sender, RoutedEventArgs e) => OpenFastenerSizes();

    void OnSawKerfClicked(object? sender, RoutedEventArgs e) => OpenSawKerf();

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

    void OnResetViewClicked(object? sender, RoutedEventArgs e)
    {
        _ = IsShowingModel ? ModelDrawing.Apply(ViewCommand.ResetView) : DrawingCanvas.Apply(ViewCommand.ResetView);
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

    void OnTurnXClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.TurnX);

    void OnTurnYClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.TurnY);

    void OnTurnZClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.TurnZ);

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

    /// <summary>
    /// Runs an editing command — from a key in either view, or from a menu — the one way it is run.
    /// </summary>
    /// <returns>Whether it did anything; a key that did nothing goes on to the view keys.</returns>
    public bool Run(EditCommand command)
    {
        switch (command)
        {
            case EditCommand.SelectTool:
                SelectTool();
                return true;

            case EditCommand.RectangleTool:
                RectangleTool();
                return true;

            case EditCommand.WallTool:
                ArmWall(null);
                return true;

            case EditCommand.Shape when IsShowingStandardView:
                Editor.Say(EditSeverity.Hint, $"Not in a {StandardViews.Name(_view)} view yet — 1 for the plan or 7 for 3D.");
                return true;

            case EditCommand.Shape:
                if (SelectionCommands.PartToShape(Editor) is { } part)
                {
                    OpenWorkshop(part);
                }

                return true;

            case EditCommand.Delete or EditCommand.Confirm when Editor.SelectedJoint is not null:
                RunJointCommand(command == EditCommand.Delete ? JointCommand.Delete : JointCommand.Edit);
                return true;

            case EditCommand.Delete or EditCommand.Pin or EditCommand.Duplicate or EditCommand.MirrorEastWest or EditCommand.MirrorNorthSouth:
                if (Editor.Selection.Count == 0)
                {
                    return false;
                }

                RunOnSelection(command);
                return true;

            case EditCommand.Join or EditCommand.JoinAll:
                BeginJoin(command == EditCommand.JoinAll);
                return true;

            case >= EditCommand.TurnX and <= EditCommand.TurnZBack:
                SelectionTurn.Turn(
                    Editor,
                    command switch { EditCommand.TurnX or EditCommand.TurnXBack => Axis.X, EditCommand.TurnY or EditCommand.TurnYBack => Axis.Y, _ => Axis.Z },
                    command is EditCommand.TurnXBack or EditCommand.TurnYBack or EditCommand.TurnZBack ? -1 : 1);
                return true;

            case EditCommand.OtherView:
                ShowView(CurrentView == DesignView.Model ? _last2DView : DesignView.Model);
                return true;

            case EditCommand.ToggleGrid:
                ToggleGrid();
                return true;

            case EditCommand.ToggleHiddenEdges:
                ToggleHiddenEdges();
                return true;

            case EditCommand.Cancel when Editor.Selection.Count > 0 || Editor.SelectedJoint is not null:
                Editor.ClearSelection();
                return true;

            case EditCommand.EditWidth when !IsShowingModel && Editor.OnlySelectedBox is { } forWidth:
                OpenDimensionEditor(forWidth.Id, SizeAxis.Width);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Delete, pin, duplicate or mirror the selection, and bring a copy into view.</summary>
    void RunOnSelection(EditCommand command)
    {
        switch (command)
        {
            case EditCommand.Delete:
                SelectionCommands.Delete(Editor);
                break;

            case EditCommand.Pin:
                SelectionCommands.Pin(Editor);
                break;

            case EditCommand.Duplicate:
                BringIntoView(SelectionCommands.Duplicate(Editor, IsShowingModel ? ModelDrawing.GridStepInches : DrawingCanvas.GridStepInches));
                break;

            default:
                BringIntoView(SelectionCommands.Mirror(Editor, command == EditCommand.MirrorEastWest ? Axis.X : Axis.Y));
                break;
        }
    }

    void BringIntoView(EntityId? copy)
    {
        if (copy is not { } id)
        {
            return;
        }

        if (IsShowingModel)
        {
            ModelDrawing.BringIntoView(id);
        }
        else
        {
            DrawingCanvas.BringIntoView(id);
        }
    }
}

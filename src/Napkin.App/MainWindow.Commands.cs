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
        foreach (ModelView view in ModelViews)
        {
            view.Disarm();
        }

        UpdateToolButtons();
        FocusDrawing();
    }

    void OnWallToolClicked(object? sender, RoutedEventArgs e) => ArmWall(null);

    void OnWall2x4Clicked(object? sender, RoutedEventArgs e) => ArmWall("2x4");

    void OnWall2x6Clicked(object? sender, RoutedEventArgs e) => ArmWall("2x6");

    void OnWindowToolClicked(object? sender, RoutedEventArgs e) => ArmOpening(OpeningKind.Window);

    void OnDoorToolClicked(object? sender, RoutedEventArgs e) => ArmOpening(OpeningKind.Door);

    void OnScreenToolClicked(object? sender, RoutedEventArgs e) => ArmOpening(OpeningKind.Window, OpeningFill.Screen);

    void OnRoomToolClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.RoomTool);

    void OnDeckToolClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.DeckTool);

    void OnRoofToolClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.RoofTool);

    void OnNoteToolClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.NoteTool);

    void OnStrutToolClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.StrutTool);

    /// <summary>
    /// Picks up the angled-part tool (assembly-model §3a.7): in the 3D view where it is natural, in the
    /// plan otherwise; a standard view brings the plan forward, since it cannot see a click's depth.
    /// </summary>
    public void ArmStrut()
    {
        if (_view == DesignView.Model)
        {
            Model.ArmStrut();
        }
        else
        {
            if (!IsShowingPlan)
            {
                ShowView(DesignView.Top);
            }

            DrawingCanvas.ArmStrut();
        }

        UpdateToolButtons();
        FocusDrawing();
    }

    /// <summary>Picks up the note tool (renovation-sketches §8); notes are put in the plan, so the plan comes forward.</summary>
    public void ArmNote()
    {
        if (!IsShowingPlan)
        {
            ShowView(DesignView.Top);
        }

        DrawingCanvas.ArmNote();
        UpdateToolButtons();
        FocusDrawing();
    }

    /// <summary>Picks up the deck tool (deck-and-porch §8); decks are drawn in the plan, so the plan comes forward.</summary>
    public void ArmDeck()
    {
        if (!IsShowingPlan)
        {
            ShowView(DesignView.Top);
        }

        DrawingCanvas.ArmDeck();
        UpdateToolButtons();
        FocusDrawing();
    }

    /// <summary>Picks up the porch roof tool (deck-and-porch §8): a click on a deck in the plan roofs it.</summary>
    public void ArmRoof()
    {
        if (!IsShowingPlan)
        {
            ShowView(DesignView.Top);
        }

        DrawingCanvas.ArmRoof();
        UpdateToolButtons();
        FocusDrawing();
    }

    /// <summary>Picks up the room tool (renovation-sketches §8); rooms are drawn in the plan, so the plan comes forward.</summary>
    public void ArmRoom()
    {
        if (!IsShowingPlan)
        {
            ShowView(DesignView.Top);
        }

        DrawingCanvas.ArmRoom();
        UpdateToolButtons();
        FocusDrawing();
    }

    /// <summary>
    /// Picks up the wall tool with a member ("2x4", "2x6") or, with null, the one it last had. Walls
    /// are drawn in the plan, so the plan comes forward if the 3D view was showing.
    /// </summary>
    public void ArmWall(string? member)
    {
        if (!IsShowingPlan)
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
    public void ArmOpening(OpeningKind kind, OpeningFill? fill = null)
    {
        if (!IsShowingPlan)
        {
            ShowView(DesignView.Top);
        }

        DrawingCanvas.ArmOpening(kind, fill);
        UpdateToolButtons();
        FocusDrawing();
    }

    void OnRectangleToolClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.RectangleTool);

    void RectangleTool()
    {
        // In the 3D view the rectangle tool is a plain board to place on a face (#74); a read-only
        // view has none (§5.4).
        if (IsShowingSheet)
        {
            Editor.Say(EditSeverity.Hint, StandardViewWords.NotOnSheet);
        }
        else if (IsShowingParts)
        {
            Editor.Say(EditSeverity.Hint, StandardViewWords.NotInPartsView);
        }
        else if (IsShowingStandardView)
        {
            Editor.Say(EditSeverity.Hint, StandardViewWords.NotInView(StandardViews.Of(_view)!.Value));
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
        if (IsShowingParts)
        {
            PartsDrawing.ZoomToFit();
        }
        else if (IsShowingSheet)
        {
            // Fit on the sheet is the sheet's: the three drawings at their one scale again (§11.4).
            SheetDrawing.RequestFit();
        }
        else if (IsShowingModel)
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
        _ = IsShowingParts ? PartsDrawing.Apply(ViewCommand.ResetView)
            : IsShowingModel ? ActiveModel.Apply(ViewCommand.ResetView)
            : DrawingCanvas.Apply(ViewCommand.ResetView);
    }

    void OnZoomInClicked(object? sender, RoutedEventArgs e)
    {
        if (IsShowingParts)
        {
            PartsDrawing.Apply(ViewCommand.ZoomIn);
        }
        else if (IsShowingModel)
        {
            ActiveModel.ZoomIn();
        }
        else
        {
            DrawingCanvas.ZoomIn();
        }
    }

    void OnZoomOutClicked(object? sender, RoutedEventArgs e)
    {
        if (IsShowingParts)
        {
            PartsDrawing.Apply(ViewCommand.ZoomOut);
        }
        else if (IsShowingModel)
        {
            ActiveModel.ZoomOut();
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

            case EditCommand.RoomTool:
                ArmRoom();
                return true;

            case EditCommand.DeckTool:
                ArmDeck();
                return true;

            case EditCommand.RoofTool:
                ArmRoof();
                return true;

            case EditCommand.NoteTool:
                ArmNote();
                return true;

            case EditCommand.StrutTool:
                ArmStrut();
                return true;

            case EditCommand.Shape when IsShowingStandardView:
                Editor.Say(EditSeverity.Hint, StandardViewWords.NotInView(StandardViews.Of(_view)!.Value));
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

            case EditCommand.FirmUp:
                BeginFirmUp();
                return true;

            case EditCommand.ToggleRough:
                SetEntryMode(Editor.EntryMode == EntryMode.Rough ? EntryMode.Precise : EntryMode.Rough);
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

            case EditCommand.EditWidth when IsShowingPlan && Editor.OnlySelectedBox is { } forWidth:
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
                BringIntoView(SelectionCommands.Duplicate(Editor, IsShowingModel ? ActiveModel.GridStepInches : DrawingCanvas.GridStepInches));
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
            ActiveModel.BringIntoView(id);
        }
        else
        {
            DrawingCanvas.BringIntoView(id);
        }
    }
}

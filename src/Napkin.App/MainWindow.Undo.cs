using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Napkin.App.Designs;
using Design = Napkin.Modules.Editing.Design;
using Napkin.App.Editing;
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
        DrawingCanvas.KeepSelectionLabels = true;

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
        // Typing a size on a rough part firms it: the size and the clearing of the mark, one undo step (sketch-mode §3.1).
        UpdateResult result = Editor.Apply(
            RoughEntry.Typed(Editor.Design.Sketch, box, DimensionEntry.RequestFor(Editor.Design.Sketch, size, readable.Value)),
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
        DrawingCanvas.KeepSelectionLabels = false;
        _editingBox = null;

        if (focusCanvas)
        {
            FocusDrawing();
        }
    }

    /// <summary>
    /// Takes the last message's way out — a conflict's relationship removed, or a refused turn's
    /// relationships let go of and the turn made — as one undo step.
    /// </summary>
    public void TakeOffer()
    {
        if (Editor.LastMessage?.Offer is not { } offer)
        {
            return;
        }

        Editor.BeginGesture(offer.What);
        Editor.Apply(offer.Request, offer.What);
        Editor.EndGesture();
        FocusDrawing();
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
        FollowDesignInProperties();
        UpdatePartJoints();
        UpdateAttention();

        // The cut list follows the drawing: widen a part with the list open and the row changes,
        // because both are readings of one design rather than a drawing and a snapshot of it.
        _cutList?.ShowDesign(CurrentDesign);
        _codeWindow?.ShowDesign(CurrentDesign);
        if (PropertiesPanel.IsVisible && Editor.OnlySelectedBox is { } selected)
        {
            ShowFraming(selected);
        }
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
        if (WorkshopSheet.Shaping is { } shaped && Editor.OnlySelected != shaped)
        {
            CloseWorkshop();
        }

        UpdateMenuEnablement();
        UpdateRelationships();
        ShowProperties();
    }
}

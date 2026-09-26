using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Napkin.App.Viewing;

using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// Rough and Precise (docs/design/sketch-mode.md &#xA7;1, &#xA7;2.4): one switch the Draw menu, the
/// toolbar's two words and Q all reach, the mode named first on the status bar, and in Rough the
/// live size beside it.
/// </summary>
public partial class MainWindow
{
    /// <summary>The status bar's word for the mode: ROUGH or PRECISE.</summary>
    public string EntryModeWord => EntryModeText.Text ?? string.Empty;

    /// <summary>The status bar's size readout, empty in Precise.</summary>
    public string SizeReadout => SizeReadoutText.IsVisible ? SizeReadoutText.Text ?? string.Empty : string.Empty;

    /// <summary>The toolbar's Rough word.</summary>
    public ToggleButton RoughToggle => RoughModeButton;

    /// <summary>The toolbar's Precise word.</summary>
    public ToggleButton PreciseToggle => PreciseModeButton;

    /// <summary>Draw ▸ Rough sketching.</summary>
    public MenuItem RoughSketchingMenuItem => RoughMenuItem;

    void WireRough()
    {
        Editor.EntryModeChanged += (_, _) => ShowEntryMode();
        Editor.SelectionChanged += (_, _) => UpdateSizeReadout();
        Editor.DesignChanged += (_, _) => UpdateSizeReadout();
        DrawingCanvas.PointerWorldPositionChanged += (_, _) => UpdateSizeReadout();
        ShowEntryMode();
    }

    /// <summary>
    /// Switches how the next gesture enters the design. Not an undo step: like Snap to grid, it
    /// changes what the next gesture does, never the design (&#xA7;1.1).
    /// </summary>
    public void SetEntryMode(EntryMode mode)
    {
        Editor.EntryMode = mode;

        // A toggle button clicked on the mode already in force unchecks itself; the mode has not
        // changed, so put the two words back as the mode says.
        ShowEntryMode();
        FocusDrawing();
    }

    void OnRoughMenuClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.ToggleRough);

    void OnRoughModeClicked(object? sender, RoutedEventArgs e) => SetEntryMode(EntryMode.Rough);

    void OnPreciseModeClicked(object? sender, RoutedEventArgs e) => SetEntryMode(EntryMode.Precise);

    void ShowEntryMode()
    {
        bool rough = Editor.EntryMode == EntryMode.Rough;
        EntryModeText.Text = RoughEntry.Word(Editor.EntryMode);
        RoughModeButton.IsChecked = rough;
        PreciseModeButton.IsChecked = !rough;
        RoughMenuItem.IsChecked = rough;
        UpdateSizeReadout();
        UpdateZoomReadout();
        DrawingCanvas.InvalidateVisual();
        foreach (ModelView view in ModelViews)
        {
            view.InvalidateVisual();
        }
    }

    /// <summary>
    /// In Rough, the numbers the quiet labels leave off the paper: the size being dragged out, else
    /// the selected part's name and three finished sizes in cut-list order (&#xA7;2.4).
    /// </summary>
    void UpdateSizeReadout()
    {
        string? text = null;
        if (Editor.EntryMode == EntryMode.Rough)
        {
            if (!IsShowingModel && DrawingCanvas.LiveDrawSize is { } live)
            {
                text = RoughEntry.DrawingReadout(live.Width, live.Height, Editor.LabelFormat);
            }
            else if (Editor.OnlySelectedBox is { } box)
            {
                text = RoughEntry.SelectedReadout(box, Editor.NameOf(box.Id), Editor.LabelFormat);
            }
        }

        SizeReadoutText.Text = text ?? string.Empty;
        SizeReadoutText.IsVisible = text is not null;

        // The readout and the design's name share the start of the row; while there is a size to read,
        // it takes the room (the name is in the title bar), so neither runs under the view chips.
        DesignText.IsVisible = text is null;
        StatusLeft.ColumnDefinitions[1].Width = text is null ? GridLength.Auto : GridLength.Star;
        StatusLeft.ColumnDefinitions[2].Width = text is null ? GridLength.Star : GridLength.Auto;
    }
}

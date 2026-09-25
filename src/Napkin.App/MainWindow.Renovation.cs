using Avalonia.Controls;
using Avalonia.Interactivity;

using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// Renovation (docs/design/renovation-sketches.md): Edit → Phase and the panel's Phase row, and
/// the status bar's phase of the selection.
/// </summary>
public partial class MainWindow
{
    static readonly Phase[] PhaseChoices = [Phase.New, Phase.Existing, Phase.Demolish];

    bool _fillingPhase;

    /// <summary>The Phase submenu, for the GUI suite.</summary>
    public MenuItem PhaseMenuItem => PhaseMenu;

    /// <summary>The panel's Phase picker, for the GUI suite.</summary>
    public ComboBox PhaseField => PhaseBox;

    /// <summary>The status bar's phase of the one selected thing: "Wall 1, existing"; empty for a new one or none.</summary>
    public string PhaseReadout => PhaseReadoutText.IsVisible ? PhaseReadoutText.Text ?? string.Empty : string.Empty;

    void WireRenovation()
    {
        DrawingCanvas.Packs = () => Packs;
        Editor.SelectionChanged += (_, _) => ShowPhase();
        Editor.DesignChanged += (_, _) => ShowPhase();
    }

    void OnPhaseExistingClicked(object? sender, RoutedEventArgs e) => SetPhase(Phase.Existing);

    void OnPhaseNewClicked(object? sender, RoutedEventArgs e) => SetPhase(Phase.New);

    void OnPhaseDemolishClicked(object? sender, RoutedEventArgs e) => SetPhase(Phase.Demolish);

    void OnPhaseChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_fillingPhase || PhaseBox.SelectedIndex < 0 || PhaseBox.SelectedIndex >= PhaseChoices.Length)
        {
            return;
        }

        SetPhase(PhaseChoices[PhaseBox.SelectedIndex]);
    }

    /// <summary>Marks the selection existing, new or demolish: one undo step, a wall's openings untouched.</summary>
    public void SetPhase(Phase phase)
    {
        if (PhaseCommand.Plan(Editor.Sketch, Editor.Selection, phase) is not { } request)
        {
            ShowPhase();
            return;
        }

        List<string> names = [.. Editor.Selection.Order().Select(Editor.NameOf)];
        Editor.Apply(request, PhaseCommand.Message(names, phase));
    }

    /// <summary>The menu's radio mark, the panel's picker and the status bar, from the selection.</summary>
    void ShowPhase()
    {
        Phase? shared = PhaseCommand.Shared(Editor.Sketch, Editor.Selection);
        PhaseMenu.IsEnabled = Editor.Selection.Count > 0;
        PhaseExistingMenuItem.IsChecked = shared == Phase.Existing;
        PhaseNewMenuItem.IsChecked = shared == Phase.New;
        PhaseDemolishMenuItem.IsChecked = shared == Phase.Demolish;

        _fillingPhase = true;
        try
        {
            PhaseBox.ItemsSource ??= PhaseChoices.Select(PhaseCommand.Word).ToArray();
            PhaseBox.SelectedIndex = shared is { } phase ? Array.IndexOf(PhaseChoices, phase) : -1;
        }
        finally
        {
            _fillingPhase = false;
        }

        string words = Editor.OnlySelected is { } id && Editor.Sketch.Find(id) is { } entity
            ? PhaseCommand.StatusWords(Editor.NameOf(id), entity.Phase)
            : string.Empty;
        PhaseReadoutText.Text = words;
        PhaseReadoutText.IsVisible = words.Length > 0;
    }
}

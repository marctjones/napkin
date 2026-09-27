using Avalonia.Controls;

using Napkin.Modules.Furniture;

namespace Napkin.App;

/// <summary>
/// A part's species (docs/design/furniture-checks.md slice A): typed freely, or picked from the cited
/// table, with the table's values said beneath it. Picking applies at once, one undo step.
/// </summary>
public partial class MainWindow
{
    bool _fillingSpecies;

    /// <summary>The species picker, for the GUI suite.</summary>
    public ComboBox SpeciesPickerControl => SpeciesPicker;

    /// <summary>What the panel says under the species.</summary>
    public string SpeciesReadoutText => SpeciesReadout.Text ?? string.Empty;

    void WireSpecies()
    {
        SpeciesPicker.ItemsSource = WoodProperties.Shipped.Species.Select(row => row.Name).ToArray();
        SpeciesBox.TextChanged += (_, _) => SpeciesReadout.Text = WoodProperties.Shipped.Describe(SpeciesBox.Text);
    }

    /// <summary>Shows a part's species in the picker when it is a row of the table, and clears it otherwise.</summary>
    void ShowSpecies(string? species)
    {
        _fillingSpecies = true;
        try
        {
            SpeciesPicker.SelectedItem = WoodProperties.Shipped.Find(species)?.Name;
            SpeciesReadout.Text = WoodProperties.Shipped.Describe(species);
        }
        finally
        {
            _fillingSpecies = false;
        }
    }

    void OnSpeciesPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_fillingSpecies || SpeciesPicker.SelectedItem is not string name || SpeciesBox.Text == name)
        {
            return;
        }

        SpeciesBox.Text = name;
        ApplyProperties();
    }
}

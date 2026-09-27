using System.Globalization;

using Avalonia.Controls;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

namespace Napkin.App;

/// <summary>
/// A shelf's sag in the Part panel (docs/design/furniture-checks.md §3, slice B): shown only for a part
/// its joints make a shelf; the load and the limit are the person's, kept in their settings.
/// </summary>
public partial class MainWindow
{
    /// <summary>Whether the shelf block is showing.</summary>
    public bool IsShowingShelf => ShelfFields.IsVisible;

    /// <summary>The estimate, or why there is none.</summary>
    public string ShelfSagLine => ShelfFields.IsVisible ? ShelfSagText.Text ?? string.Empty : string.Empty;

    /// <summary>The load and limit boxes, for the GUI suite.</summary>
    public (TextBox Load, TextBox Limit) ShelfControls => (ShelfLoadBox, ShelfLimitBox);

    void WireShelf()
    {
        foreach (TextBox box in new[] { ShelfLoadBox, ShelfLimitBox })
        {
            box.LostFocus += (_, _) => ApplyShelf();
        }
    }

    /// <summary>Shows the shelf block for a shelf, or hides it for anything else.</summary>
    void ShowShelf(Box box)
    {
        ShelfSpan? shelf = ShelfSag.Of(Editor.Sketch, box);
        ShelfFields.IsVisible = shelf is not null;
        if (shelf is null)
        {
            return;
        }

        ShelfLoadBox.Text = Settings.Current.ShelfLoadPsf.ToString(CultureInfo.InvariantCulture);
        ShelfLimitBox.Text = Settings.Current.ShelfSagLimit is { } limit ? limit.Format(Editor.LabelFormat).Text : string.Empty;
        (ShelfSagEstimate? estimate, string? missing) = ShelfSag.Estimate(shelf, Settings.Current.ShelfLoadPsf, Settings.Current.ShelfSagLimit, MaterialsLibrary.Shipped);
        ShelfSagText.Text = estimate?.Text ?? missing;
    }

    /// <summary>Reads the load and limit into the person's settings and re-estimates; a value that does not read is said and kept out.</summary>
    void ApplyShelf()
    {
        if (!ShelfFields.IsVisible || Editor.OnlySelectedBox is not { } box)
        {
            return;
        }

        string loadText = ShelfLoadBox.Text?.Trim() ?? string.Empty;
        if (!decimal.TryParse(loadText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal load) || load <= 0)
        {
            Editor.Say(EditSeverity.Problem, "The shelf load is a number of pounds per square foot, like 15. Nothing was changed.");
            ShowShelf(box);
            return;
        }

        string limitText = ShelfLimitBox.Text?.Trim() ?? string.Empty;
        Length? limit = null;
        if (limitText.Length > 0)
        {
            if (!Length.TryParse(limitText, out Length typed, out _) || typed <= Length.Zero)
            {
                Editor.Say(EditSeverity.Problem, "The sag limit is a length, like 1/8\". Nothing was changed.");
                ShowShelf(box);
                return;
            }

            limit = typed;
        }

        Settings.Update(s => s with { ShelfLoadPsf = load, ShelfSagLimit = limit });
        ShowShelf(box);
    }
}

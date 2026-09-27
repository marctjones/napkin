using Avalonia.Controls;
using Avalonia.Interactivity;

using Napkin.Core.Geometry;

namespace Napkin.App;

/// <summary>
/// Project → This piece is (docs/design/furniture-checks.md §9.1): what the piece is and whether it is
/// anchored, said by the person — napkin never detects it. Each choice is one undo step.
/// </summary>
public partial class MainWindow
{
    /// <summary>The menu's items, for the GUI suite.</summary>
    public (MenuItem Menu, MenuItem None, MenuItem Clothing, MenuItem Bunk, MenuItem Anchored) FurnitureMenu
        => (FurnitureKindMenuItem, FurnitureNoneMenuItem, FurnitureClothingMenuItem, FurnitureBunkMenuItem, FurnitureAnchoredMenuItem);

    void OnFurnitureMenuOpened(object? sender, RoutedEventArgs e)
    {
        FurnitureMarks marks = Editor.Sketch.Furniture;
        foreach ((MenuItem item, FurnitureKind kind) in new[] { (FurnitureNoneMenuItem, FurnitureKind.None), (FurnitureClothingMenuItem, FurnitureKind.ClothingStorage), (FurnitureBunkMenuItem, FurnitureKind.BunkBed) })
        {
            item.Icon = marks.Kind == kind ? new TextBlock { Text = "✓" } : null;
        }

        FurnitureAnchoredMenuItem.Icon = marks.Anchored ? new TextBlock { Text = "✓" } : null;
    }

    void OnFurnitureKindClicked(object? sender, RoutedEventArgs e)
    {
        FurnitureKind kind = ReferenceEquals(sender, FurnitureClothingMenuItem) ? FurnitureKind.ClothingStorage
            : ReferenceEquals(sender, FurnitureBunkMenuItem) ? FurnitureKind.BunkBed
            : FurnitureKind.None;
        if (Editor.Sketch.Furniture.Kind != kind)
        {
            string what = kind switch
            {
                FurnitureKind.ClothingStorage => "a clothing storage unit",
                FurnitureKind.BunkBed => "a bunk bed",
                _ => "nothing in particular",
            };
            Editor.Apply(new SetFurnitureMarks(Editor.Sketch.Furniture with { Kind = kind }), $"Said this piece is {what}");
        }
    }

    void OnFurnitureAnchoredClicked(object? sender, RoutedEventArgs e)
    {
        bool anchored = !Editor.Sketch.Furniture.Anchored;
        Editor.Apply(new SetFurnitureMarks(Editor.Sketch.Furniture with { Anchored = anchored }), anchored ? "Said it is anchored to the wall" : "Said it is not anchored to the wall");
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Napkin.App.Designs;
using Design = Napkin.Modules.Editing.Design;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Napkin.Modules.Furniture;
using Napkin.Modules.Editing;

namespace Napkin.App;

public partial class MainWindow
{
    // ---------------------------------------------------------------------------------------
    // The stock toolbox: pick real stock, then drag it onto the paper (issue #7, GUI-CUT-02)
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// A category icon was clicked: its drawer drops down under it, or — when that was the open
    /// one — the drawer closes and puts down whatever it had picked up.
    /// </summary>
    void OnStockCategoryChanged()
    {
        if (StockToolboxPanel.Category is null)
        {
            DrawingCanvas.ArmStock(null);
            if (ModelDrawing.Placement.Stock is not null)
            {
                ModelDrawing.Disarm();
            }
        }

        UpdateToolbox();
    }

    /// <summary>
    /// Shows the drawer under its category's icon while one is open and the canvas is what is on
    /// screen; the shape workshop covers the canvas and takes the toolbar and the drawer with it.
    /// </summary>
    void UpdateToolbox()
    {
        StockToolboxPanel.IsVisible = StockToolboxPanel.Category is not null && !IsShapingPart;
        if (!StockToolboxPanel.IsVisible
            || StockToolboxPanel.Category is not { } open
            || ToolBar.Parent is not Visual overlay)
        {
            return;
        }

        // Dropped down from the icon that opened it, pulled back left only as far as it takes to
        // stay inside the window.
        ToggleButton icon = StockToolboxPanel.CategoryButtons[open];
        double left = icon.TranslatePoint(new Point(0, 0), overlay)?.X ?? ToolBar.Bounds.Left;
        double room = overlay.Bounds.Width - StockToolboxPanel.Width - 10;
        left = Math.Max(10, Math.Min(left, room));
        StockToolboxPanel.Margin = new Thickness(left, ToolBar.Bounds.Bottom + 4, 10, 10);
    }

    /// <summary>
    /// An item was picked in the toolbox: the pointer now holds it, and the next drag on the paper
    /// places a part already cut from it. The toolbox stays open for the one after.
    /// </summary>
    void PickStock(StockItem item)
    {
        // In the 3D view stock is placed on the face under the pointer (#74); in the plan, dragged
        // out on the paper.
        if (IsShowingModel)
        {
            if (ModelDrawing.Arm(item))
            {
                Editor.Say(
                    EditSeverity.Hint,
                    $"Holding {item.Name} — actual {item.ActualSizeText}. Click on a face, or the floor, to place 24\" of it, "
                    + "or drag along the face for its length; Escape puts it down.");
            }
            else
            {
                Editor.Say(
                    EditSeverity.Hint,
                    $"{item.HoverText}. {FastenerList.NotPlacedOnDrawing}, so there is nothing to place.");
            }

            UpdateToolButtons();
            FocusDrawing();
            return;
        }

        if (DrawingCanvas.ArmStock(item))
        {
            Editor.Say(
                EditSeverity.Hint,
                $"Holding {item.Name} — actual {item.ActualSizeText}. Drag on the paper to place it; Escape puts it down.");
        }
        else
        {
            Editor.Say(
                EditSeverity.Hint,
                $"{item.HoverText}. {FastenerList.NotPlacedOnDrawing}, so there is nothing to drag.");
        }

        FocusDrawing();
    }

    /// <summary>
    /// Builds <em>Draw &#x2192; Stock</em>: the toolbox's two levels as two levels of menu — a
    /// submenu per category, named and explained as its toolbar icon is, and under it an item per
    /// size with the size's actual dimensions and citation on hover.
    /// </summary>
    /// <remarks>
    /// Picking an item does what the toolbar's two clicks do, through the same code: it opens that
    /// category's drawer, so the item held is marked in it, and then picks the item exactly as a
    /// click in the drawer does (<see cref="PickStock"/>). A fastener is listed and cannot be
    /// placed, by menu as by toolbar.
    /// </remarks>
    void BuildStockMenu()
    {
        List<MenuItem> categories = [];
        foreach (StockCategory category in StockToolboxPanel.Library.Categories)
        {
            List<MenuItem> sizes = [];
            foreach (StockItem item in StockToolboxPanel.Library.InCategory(category))
            {
                MenuItem size = new() { Header = item.Name, Tag = item };
                ToolTip.SetTip(size, item.HoverText);
                AutomationProperties.SetHelpText(size, item.HoverText);
                size.Click += (_, _) =>
                {
                    StockToolboxPanel.ShowCategory(category);
                    PickStock(item);
                };
                sizes.Add(size);
            }

            MenuItem submenu = new()
            {
                Header = StockToolbox.Words(category),
                Tag = category,
                ItemsSource = sizes,
            };
            ToolTip.SetTip(submenu, ToolTip.GetTip(StockToolboxPanel.CategoryButtons[category]));
            categories.Add(submenu);
        }

        StockMenu.ItemsSource = categories;
    }
}

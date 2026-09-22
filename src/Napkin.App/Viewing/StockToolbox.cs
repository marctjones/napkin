using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;
using Napkin.Core.Materials;

namespace Napkin.App.Viewing;

/// <summary>
/// The stock toolbox: one small icon per category, always on the main toolbar, and under the one
/// that is picked a plain text list of what is in it, with each item's actual size on hover before
/// it is placed.
/// </summary>
/// <remarks>
/// <para>
/// Issue #7's picker, as decided and not redesigned here: "organized the way a lumber yard is,
/// not as a flat list of every stock size". The top row is
/// <see cref="MaterialsLibrary.Categories"/> — so a category with no table yet shows no icon that
/// opens an empty list — each drawn as a cross-section glyph, which is "enough to scan by eye".
/// The second level is text, because "2x4" already is the icon. Hovering an item shows
/// <see cref="StockItem.HoverText"/> — "2x4 — actual 1 1/2" x 3 1/2", PS 20-25" — as a tooltip,
/// the shell's hover convention for its tool buttons (#62), and in the readout line under the list,
/// which is the same sentence the properties panel's stock line shows once the part is placed.
/// </para>
/// <para>
/// The two levels live in two places. The icons are <see cref="CategoryRow"/>, which the window
/// puts on its main toolbar beside Select and Rectangle so they are always in reach, with nothing
/// to open first (Marc, 2026-09-22: "Shouldnt the stock items all be visible on a main toolbar").
/// This control itself is only the drawer — the list, its caption and the readout — and the window
/// shows it under the icon that opened it while <see cref="Category"/> is not null.
/// </para>
/// <para>
/// The drawer is not modal and it does not close when an item is placed: a toolbox, not a dialog.
/// That is why it is a panel on the drawing rather than a light-dismiss flyout, which would close
/// on the very press on the paper that places the part. Clicking the open category's icon again
/// closes it. Picking an item raises <see cref="ItemPicked"/>; what that arms is the window's
/// business.
/// </para>
/// <para>
/// <strong>Fasteners are listed and cannot be placed.</strong> The library carries them and the
/// decided picker names the category, so the drawer is there and every nail's size is on hover;
/// but napkin has no fastener-as-a-part — a nail is not a box on a cut list — so the drawer says
/// so in its caption rather than offering a gesture that would make one.
/// </para>
/// </remarks>
public sealed class StockToolbox : Border
{
    /// <summary>What the readout says when nothing is hovered and nothing is picked.</summary>
    public const string IdleText = "Hover an item for its actual size. Click one, then drag on the paper to place it.";

    readonly MaterialsLibrary _library;
    readonly StackPanel _categoryRow = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    readonly TextBlock _caption = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    readonly StackPanel _itemList = new() { Spacing = 1 };
    readonly ScrollViewer _itemScroller;
    readonly TextBlock _readout = new() { FontSize = 10, TextWrapping = TextWrapping.Wrap, Text = IdleText };
    readonly Dictionary<StockCategory, ToggleButton> _categoryButtons = [];
    readonly List<Button> _itemButtons = [];
    readonly List<Path> _glyphs = [];
    StockItem? _armed;
    StockItem? _hovered;
    CanvasPalette _palette = CanvasPalette.Light;

    /// <summary>Builds the toolbox over the shipped materials library.</summary>
    public StockToolbox()
        : this(MaterialsLibrary.Shipped)
    {
    }

    /// <summary>Builds the toolbox over a library.</summary>
    public StockToolbox(MaterialsLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));

        Padding = new Thickness(8, 7);
        CornerRadius = new CornerRadius(4);
        BorderThickness = new Thickness(1);
        Width = 208;

        foreach (StockCategory category in _library.Categories)
        {
            Path glyph = new()
            {
                Data = Geometry.Parse(GlyphFor(category)),
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                StrokeThickness = 1.2,
            };
            _glyphs.Add(glyph);

            ToggleButton button = new()
            {
                Content = glyph,
                Padding = new Thickness(5),
                Tag = category,
            };
            ToolTip.SetTip(button, category == StockCategory.Fastener
                ? "Fasteners: listed for their sizes; napkin does not place them yet"
                : $"{Words(category)}: pick a size, then drag it onto the paper");
            AutomationProperties.SetName(button, Words(category));
            // A second click on the open drawer's icon closes it again.
            button.Click += (_, _) => ShowCategory(Category == category ? null : category);

            _categoryButtons.Add(category, button);
            _categoryRow.Children.Add(button);
        }

        // Twenty lumber sizes do not fit a laptop window, so the drawer scrolls rather than
        // pushing the readout off the bottom of the screen.
        _itemScroller = new ScrollViewer
        {
            MaxHeight = 230,
            Content = _itemList,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        Child = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                _caption,
                _itemScroller,
                _readout,
            },
        };

        // No drawer is open until a category is picked (issue #7: the second level "only opens
        // once a category is picked, so the toolbox never shows more than a handful of things at
        // once").
        ShowCategory(null);
        ApplyPalette(_palette);
    }

    /// <summary>Raised when a person clicks an item in the list.</summary>
    public event EventHandler<StockItem>? ItemPicked;

    /// <summary>Raised when a drawer opens, closes, or is swapped for another.</summary>
    public event EventHandler? CategoryChanged;

    /// <summary>
    /// The category icons, one toggle each, for the window to put on its main toolbar. They are
    /// not inside this control: the drawer comes and goes, the icons stay.
    /// </summary>
    public Control CategoryRow => _categoryRow;

    /// <summary>
    /// The library the toolbox lists, so another way in to the same stock — the Draw menu's —
    /// offers exactly the categories and sizes the icons do.
    /// </summary>
    public MaterialsLibrary Library => _library;

    /// <summary>The drawer that is open.</summary>
    public StockCategory? Category { get; private set; }

    /// <summary>The icon button of each category, in the order the top row shows them.</summary>
    public IReadOnlyDictionary<StockCategory, ToggleButton> CategoryButtons => _categoryButtons;

    /// <summary>The open drawer's items, one text button each, in the order the tables list them.</summary>
    public IReadOnlyList<Button> ItemButtons => _itemButtons;

    /// <summary>The scrolling area the open drawer's items are in.</summary>
    public ScrollViewer ItemScroller => _itemScroller;

    /// <summary>What the line under the list says now.</summary>
    public string ReadoutText => _readout.Text ?? string.Empty;

    /// <summary>What the line above the list says about the open drawer.</summary>
    public string CaptionText => _caption.Text ?? string.Empty;

    /// <summary>The item button for a stock item in the open drawer, or null when it is not showing.</summary>
    public Button? ButtonFor(string name) =>
        _itemButtons.FirstOrDefault(button => button.Tag is StockItem item && item.Name == name);

    /// <summary>A category as the toolbox names it.</summary>
    public static string Words(StockCategory category) => category switch
    {
        StockCategory.DimensionalLumber => "Dimensional lumber",
        StockCategory.SheetGood => "Sheet goods",
        StockCategory.HardwoodBoard => "Hardwood boards",
        StockCategory.Decking => "Decking",
        StockCategory.Fastener => "Fasteners",
        _ => category.ToString(),
    };

    /// <summary>
    /// Opens one drawer — its items, as text, replace the last drawer's — or closes the open one
    /// with null.
    /// </summary>
    public void ShowCategory(StockCategory? category)
    {
        Category = category;
        foreach ((StockCategory each, ToggleButton button) in _categoryButtons)
        {
            button.IsChecked = each == category;
        }

        _itemList.Children.Clear();
        _itemButtons.Clear();
        _itemScroller.Offset = default;
        IEnumerable<StockItem> items = category is { } open ? _library.InCategory(open) : [];
        foreach (StockItem item in items)
        {
            Button button = new()
            {
                Content = item.Name,
                Tag = item,
                FontSize = 12,
                Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            ToolTip.SetTip(button, item.HoverText);
            ToolTip.SetShowDelay(button, 0);
            AutomationProperties.SetName(button, item.Name);
            AutomationProperties.SetHelpText(button, item.HoverText);

            button.PointerEntered += (_, _) =>
            {
                _hovered = item;
                UpdateReadout();
            };
            button.PointerExited += (_, _) =>
            {
                if (ReferenceEquals(_hovered, item))
                {
                    _hovered = null;
                    UpdateReadout();
                }
            };
            button.Click += (_, _) => ItemPicked?.Invoke(this, item);

            _itemButtons.Add(button);
            _itemList.Children.Add(button);
        }

        _caption.Text = category switch
        {
            null => "Pick a category.",
            StockCategory.Fastener => "Listed for their sizes. napkin does not place fasteners on the drawing yet.",
            { } drawer => $"{Words(drawer)} — {_itemButtons.Count} sizes",
        };

        _hovered = null;
        UpdateItemHighlight();
        UpdateReadout();
        CategoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Marks the item the pointer is holding, or none: the same item stays marked while it is
    /// armed, so a person can see what the next drag will place.
    /// </summary>
    public void ShowArmed(StockItem? armed)
    {
        _armed = armed;
        UpdateItemHighlight();
        UpdateReadout();
    }

    /// <summary>Lights the toolbox from the canvas palette, the way every panel on the drawing is.</summary>
    public void ApplyPalette(CanvasPalette palette)
    {
        _palette = palette ?? throw new ArgumentNullException(nameof(palette));

        Background = new SolidColorBrush(palette.Background);
        BorderBrush = new SolidColorBrush(palette.GridMajor);
        _caption.Foreground = new SolidColorBrush(palette.Label);
        _readout.Foreground = new SolidColorBrush(palette.Label);

        foreach (Path glyph in _glyphs)
        {
            glyph.Stroke = new SolidColorBrush(palette.Dimension);
            glyph.Fill = new SolidColorBrush(palette.PreviewFill);
        }

        UpdateItemHighlight();
    }

    void UpdateItemHighlight()
    {
        foreach (Button button in _itemButtons)
        {
            bool armed = _armed is not null && ReferenceEquals(button.Tag, _armed);
            button.FontWeight = armed ? FontWeight.SemiBold : FontWeight.Normal;
            button.Foreground = new SolidColorBrush(armed ? _palette.Selection : _palette.Dimension);
        }
    }

    void UpdateReadout() => _readout.Text = _hovered is { } hovered
        ? hovered.HoverText
        : _armed is { } armed
            ? $"Holding {armed.Name}: drag on the paper to place it. Escape puts it down."
            : IdleText;

    /// <summary>
    /// A cross-section glyph per category, in an 18-unit box: "a cross-section glyph per category
    /// is enough to scan by eye" (issue #7). Deliberately plain.
    /// </summary>
    static string GlyphFor(StockCategory category) => category switch
    {
        // A board on end: its rectangular section, taller than wide, with a ring of grain.
        StockCategory.DimensionalLumber => "M5,1 L13,1 L13,17 L5,17 Z M7,17 A6,6 0 0 1 11,11",

        // Plies, edge-on.
        StockCategory.SheetGood => "M1,5 L17,5 L17,7.5 L1,7.5 Z M1,8 L17,8 L17,10.5 L1,10.5 Z M1,11 L17,11 L17,13.5 L1,13.5 Z",

        // A wide, thick slab with a growth ring: random width, sold by thickness.
        StockCategory.HardwoodBoard => "M1,5 L17,5 L17,13 L1,13 Z M4,13 A5,5 0 0 1 14,13",

        // Two deck boards side by side, with the gap between them.
        StockCategory.Decking => "M1,7 L8,7 L8,11 L1,11 Z M10,7 L17,7 L17,11 L10,11 Z",

        // A nail: head, shank, point.
        StockCategory.Fastener => "M5,2 L13,2 L13,4 L5,4 Z M8,4 L10,4 L10,14 L9,17 L8,14 Z",

        _ => "M2,2 L16,2 L16,16 L2,16 Z",
    };
}

using System.Collections.Immutable;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

namespace Napkin.App.Viewing;

/// <summary>Which column a cut list is sorted by.</summary>
public enum CutListColumn
{
    /// <summary>What the pieces are called.</summary>
    Label,

    /// <summary>How many to cut.</summary>
    Quantity,

    /// <summary>The finished length — the order the list comes in.</summary>
    Length,

    /// <summary>The finished width.</summary>
    Width,

    /// <summary>The finished thickness.</summary>
    Thickness,

    /// <summary>What it is cut from.</summary>
    Material,
}

/// <summary>
/// The cut list on screen: the rows a person reads at the bench, in a table they can sort.
/// </summary>
/// <remarks>
/// <para>
/// <strong>It renders rows; it does not compute them.</strong> <see cref="CutList.Of"/> produces
/// the list, this draws it, and <see cref="CutListCsv.ToCsv"/> writes the same list to a file. The
/// table and the export cannot disagree about what is being built because there is one list
/// (issue #8, "Output format").
/// </para>
/// <para>
/// Sorting reorders what is shown and never changes a number. <see cref="Sorted"/> is the order on
/// screen, so an export taken while a column is sorted is the table as it is being read.
/// </para>
/// </remarks>
public sealed class CutListTable : Grid
{
    private ImmutableArray<CutListRow> _rows = [];
    private CutListColumn _sortBy = CutListColumn.Length;
    private bool _descending = true;

    /// <summary>A table with nothing in it yet.</summary>
    /// <remarks>
    /// The last column carries the shape thumbnails (<c>docs/design/shaped-parts-model.md</c>
    /// &#xA7;4.4). It has no header, because it is a picture of the row rather than something to
    /// sort by, and it is empty for a plain rectangle.
    /// </remarks>
    public CutListTable()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto,Auto,Auto");

        // Transparent rather than none, so a press anywhere along a row — not only on its text — is the
        // table's to read (#205).
        Background = Brushes.Transparent;
        Rebuild();
    }

    /// <summary>Raised when a column header is clicked and the order changes.</summary>
    public event EventHandler? SortChanged;

    /// <summary>
    /// Raised when a row is pressed (#205, parts-view §5.5): its parts are to be selected in the model —
    /// toggled with Ctrl or Cmd, added with Shift, as a Parts view cell does.
    /// </summary>
    public event EventHandler<CutListRowPick>? RowPicked;

    /// <summary>Which row each grid line belongs to: a row's own line and the lines of its sentences.</summary>
    readonly Dictionary<int, CutListRow> _lineRows = [];

    /// <summary>The first grid line of a row, for the GUI suite to press it.</summary>
    public int LineOf(CutListRow row) => _lineRows.Where(pair => pair.Value.Equals(row)).Min(pair => pair.Key);

    /// <summary>The rows this table shows, in the order <see cref="CutList.Of"/> produced them.</summary>
    public ImmutableArray<CutListRow> Rows
    {
        get => _rows;
        set
        {
            _rows = value;
            Rebuild();
        }
    }

    /// <summary>The column the table is sorted by.</summary>
    public CutListColumn SortBy => _sortBy;

    /// <summary>Whether the sort runs largest or last first.</summary>
    public bool IsDescending => _descending;

    /// <summary>The rows in the order they are on screen, which is what an export writes.</summary>
    public ImmutableArray<CutListRow> Sorted => [.. Order(_rows)];

    /// <summary>
    /// What the table reads, header line first and then one line per row, tab separated.
    /// </summary>
    /// <remarks>
    /// For the GUI suite, which asserts what a person can see rather than what the model holds.
    /// </remarks>
    public ImmutableArray<string> LinesOnScreen =>
    [
        .. Enumerable.Range(0, RowDefinitions.Count)
            .Select(row => string.Join(
                "\t",
                Children
                    .Where(cell => GetRow(cell) == row && cell is not CutThumbnail)
                    .OrderBy(GetColumn)
                    .Select(TextOf))),
    ];

    /// <summary>
    /// The labels of the rows that drew a shape thumbnail, in the order they are on screen.
    /// </summary>
    /// <remarks>
    /// The pictures cannot be read as text, so this is how the GUI suite asks which rows got one.
    /// </remarks>
    public ImmutableArray<string> RowsWithThumbnails =>
    [
        .. Children
            .OfType<CutThumbnail>()
            .OrderBy(GetRow)
            .Select(thumbnail => LabelOn(GetRow(thumbnail))),
    ];

    /// <summary>What the part column of one line reads.</summary>
    string LabelOn(int line) => Children
        .Where(cell => GetRow(cell) == line && GetColumn(cell) == 0)
        .Select(TextOf)
        .FirstOrDefault(string.Empty);

    /// <summary>What one cell reads, whether it is a header button or a plain cell.</summary>
    private static string TextOf(Control cell) => cell switch
    {
        TextBlock text => text.Text ?? string.Empty,
        ContentControl { Content: TextBlock caption } => caption.Text ?? string.Empty,

        // A rough row's label and its tag: "Leg rough".
        StackPanel words => string.Join(" ", words.Children.OfType<TextBlock>().Select(word => word.Text)),
        _ => string.Empty,
    };

    /// <summary>
    /// Sorts by a column, turning the direction around when it is the column already sorted by.
    /// </summary>
    /// <param name="column">The column to sort by.</param>
    public void SortByColumn(CutListColumn column)
    {
        if (_sortBy == column)
        {
            _descending = !_descending;
        }
        else
        {
            _sortBy = column;

            // Sizes and counts read largest-first, which is the order a person cuts in; words read
            // A to Z.
            _descending = column is not (CutListColumn.Label or CutListColumn.Material);
        }

        Rebuild();
        SortChanged?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<CutListRow> Order(IEnumerable<CutListRow> rows)
    {
        IOrderedEnumerable<CutListRow> ordered = _sortBy switch
        {
            CutListColumn.Label => By(row => row.Label, StringComparer.Ordinal),
            CutListColumn.Quantity => By(row => row.Quantity),
            CutListColumn.Width => By(row => row.Width),
            CutListColumn.Thickness => By(row => row.Thickness),
            CutListColumn.Material => By(row => row.MaterialText, StringComparer.Ordinal),
            _ => By(row => row.Length),
        };

        // Whatever the column, the rest of the design's own order breaks a tie, so that sorting by
        // quantity does not shuffle rows that have the same quantity.
        return ordered
            .ThenByDescending(row => row.Length)
            .ThenByDescending(row => row.Width)
            .ThenByDescending(row => row.Thickness)
            .ThenBy(row => row.Label, StringComparer.Ordinal);

        IOrderedEnumerable<CutListRow> By<TKey>(Func<CutListRow, TKey> key, IComparer<TKey>? comparer = null)
            => _descending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer);
    }

    private void Rebuild()
    {
        Children.Clear();
        RowDefinitions.Clear();
        _lineRows.Clear();

        AddHeader();

        int line = 1;
        foreach (CutListRow row in Order(_rows))
        {
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _lineRows[line] = row;

            Add(row.Rough ? RoughLabel(row.Label) : Cell(row.Label), line, 0);
            Add(Cell(row.Quantity.ToString(CultureInfo.InvariantCulture), right: true), line, 1);
            Add(Sized(CutListCsv.Text(row.Length), row.Rough), line, 2);
            Add(Sized(CutListCsv.Text(row.Width), row.Rough), line, 3);
            Add(Sized(CutListCsv.Text(row.Thickness), row.Rough), line, 4);

            TextBlock material = Cell(row.MaterialText);
            if (row.Unresolved)
            {
                // A name this build's library does not carry is shown saying so rather than being
                // dropped or guessed at, and it is worth looking different from a name that
                // resolved.
                material.FontStyle = FontStyle.Italic;
                material.Opacity = 0.85;
            }

            Add(material, line, 5);

            if (Thumbnail(row) is { } shape)
            {
                Add(shape, line, 6);
            }

            line++;

            // What to do to the blank, under the row it belongs to: the sentences §4.4 wrote for a
            // person at a bench, beside the picture of the shape they describe. A plain rectangle
            // has none and gets no line.
            foreach (string sentence in row.CutText.AddRange(row.JointText).AddRange(row.Flags))
            {
                RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _lineRows[line] = row;
                Add(Sentence(sentence), line, 0, span: 7);
                line++;
            }
        }
    }

    /// <summary>One bench sentence, set under its row and indented to read as part of it.</summary>
    private static TextBlock Sentence(string text)
    {
        TextBlock block = new()
        {
            Text = text,
            FontSize = 11,
            Opacity = 0.9,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(18, 0, 8, 5),
        };

        // The ink follows the theme the sentence is actually shown in, which is not known until it is
        // on screen, and changes when the person picks another theme.
        void Ink() => block.Foreground = new SolidColorBrush(CanvasPalette.For(block.ActualThemeVariant).Label);
        block.AttachedToVisualTree += (_, _) => Ink();
        block.ActualThemeVariantChanged += (_, _) => Ink();
        Ink();
        return block;
    }

    /// <summary>
    /// A picture of what this row describes, or nothing when it describes a plain rectangle.
    /// </summary>
    /// <remarks>
    /// The blank is rebuilt from what the row already carries: <see cref="CutListRow.PlanAxes"/>
    /// says which of the three finished dimensions the box's stored width and height were, and
    /// the cuts come across by value. So the row needs nothing added to it to be drawable — which
    /// is right, because a row is a value with no drawing decisions in it.
    /// </remarks>
    private static CutThumbnail? Thumbnail(CutListRow row)
    {
        if (row.Cuts.IsEmpty)
        {
            return null;
        }

        // The one blank the Parts view draws too (parts-view §0): built there, read here.
        Box blank = PartsCell.BlankFor(row);

        return new CutThumbnail(blank);
    }

    private void AddHeader()
    {
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Add(Header("Part", CutListColumn.Label), 0, 0);
        Add(Header("Qty", CutListColumn.Quantity, right: true), 0, 1);
        Add(Header("Length", CutListColumn.Length, right: true), 0, 2);
        Add(Header("Width", CutListColumn.Width, right: true), 0, 3);
        Add(Header("Thickness", CutListColumn.Thickness, right: true), 0, 4);
        Add(Header("Material", CutListColumn.Material), 0, 5);
    }

    private Button Header(string text, CutListColumn column, bool right = false)
    {
        // The arrow is part of the header's text so that "what is it sorted by?" is answerable
        // from what is on screen, by a person and by the GUI suite alike.
        string caption = _sortBy == column ? $"{text} {(_descending ? "▼" : "▲")}" : text;

        Button header = new()
        {
            Content = new TextBlock
            {
                Text = caption,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
            },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

        header.Click += (_, _) => SortByColumn(column);
        return header;
    }

    /// <summary>
    /// A rough row's label: the label, then a <c>rough</c> tag in the pencil colour
    /// (<c>docs/design/sketch-mode.md</c> &#xA7;5).
    /// </summary>
    private static StackPanel RoughLabel(string label)
    {
        TextBlock tag = new()
        {
            Text = RoughTag,
            FontSize = 10,
            FontStyle = FontStyle.Italic,
            VerticalAlignment = VerticalAlignment.Center,
        };
        void Ink() => tag.Foreground = new SolidColorBrush(CanvasPalette.For(tag.ActualThemeVariant).Dimension);
        tag.AttachedToVisualTree += (_, _) => Ink();
        tag.ActualThemeVariantChanged += (_, _) => Ink();
        Ink();

        StackPanel cell = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 0 };
        cell.Children.Add(Cell(label));
        cell.Children.Add(tag);
        return cell;
    }

    /// <summary>The word a rough row is tagged with.</summary>
    public const string RoughTag = "rough";

    /// <summary>A size cell; a rough row's in the lighter ink its part is drawn in (§6.3): sizes as drawn.</summary>
    private static TextBlock Sized(string text, bool rough)
    {
        TextBlock cell = Cell(text, right: true);
        if (rough)
        {
            cell.Opacity = 0.6;
        }

        return cell;
    }

    private static TextBlock Cell(string text, bool right = false) => new()
    {
        Text = text,
        FontSize = 12,
        Margin = new Thickness(8, 3),
        TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled)
        {
            return;
        }

        // The line under the pointer, from the lines' heights, says the row — wherever along it the press
        // landed. The header's buttons handle their own presses and never get here.
        double y = e.GetPosition(this).Y, top = 0;
        for (int line = 0; line < RowDefinitions.Count; line++)
        {
            top += RowDefinitions[line].ActualHeight;
            if (y < top)
            {
                if (_lineRows.TryGetValue(line, out CutListRow? row))
                {
                    KeyModifiers held = e.KeyModifiers;
                    RowPicked?.Invoke(this, new CutListRowPick(row, held.HasFlag(KeyModifiers.Control) || held.HasFlag(KeyModifiers.Meta), held.HasFlag(KeyModifiers.Shift)));
                    e.Handled = true;
                }

                return;
            }
        }
    }

    private void Add(Control control, int row, int column, int span = 1)
    {
        SetRow(control, row);
        SetColumn(control, column);
        SetColumnSpan(control, span);
        Children.Add(control);
    }
}

/// <summary>A cut-list row pressed, to select its parts (#205).</summary>
/// <param name="Row">The row.</param>
/// <param name="Toggle">Ctrl or Cmd was held: toggle each part.</param>
/// <param name="Add">Shift was held: add the parts to the selection.</param>
public sealed record CutListRowPick(CutListRow Row, bool Toggle, bool Add);

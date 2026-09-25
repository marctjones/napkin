using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

namespace Napkin.App.Shaping;

/// <summary>
/// The shape workshop's sheet: one blank, its stock, and its cuts
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;7.1, &#xA7;7.2).
/// </summary>
/// <remarks>
/// It is a mode of the window, not a second document: the same editor, the same sketch, the same
/// selection. The window opens and closes it and hides its own chrome meanwhile; this control
/// shows the blank and reads what is typed into its fields. The words and the reading of the fields
/// are <see cref="CutEntry"/>'s, so they are tested without a window.
/// </remarks>
public partial class ShapeWorkshop : UserControl
{
    readonly List<CutSite> _cutsOnScreen = [];
    List<string> _cutLinesOnScreen = [];
    DesignEditor? _editor;
    bool _filling;

    /// <summary>Builds the sheet, closed.</summary>
    public ShapeWorkshop()
    {
        InitializeComponent();
        WorkshopDrawing.SelectedCutChanged += (_, _) => ShowCut();
        WorkshopDrawing.HintChanged += (_, _) => UpdateHint();
        WorkshopCutsList.SelectionChanged += (_, _) => OnCutPicked();
        WorkshopStockBox.TextChanged += (_, _) => UpdateStockReadout();
    }

    /// <summary>The person asked to leave: the Done button.</summary>
    public event EventHandler? DoneRequested;

    /// <summary>The stock field was applied to the blank, whether or not the drawing changed.</summary>
    public event EventHandler? StockApplied;

    /// <summary>The editor the workshop shapes parts in: the window's.</summary>
    public DesignEditor? Editor
    {
        get => _editor;
        set
        {
            _editor = value;
            WorkshopDrawing.Editor = value;
        }
    }

    /// <summary>The part being shaped, or nothing while the workshop is closed.</summary>
    public EntityId? Shaping { get; private set; }

    /// <summary>The blank drawn large.</summary>
    public WorkshopView Drawing => WorkshopDrawing;

    /// <summary>The surfaces that are notes on the paper, for the window to theme.</summary>
    public IReadOnlyList<Control> Notes => [Sheet, WorkshopCutsPanel];

    /// <summary>The stock field.</summary>
    public TextBox StockField => WorkshopStockBox;

    /// <summary>The stock field's button.</summary>
    public Button StockApply => WorkshopStockButton;

    /// <summary>What the library says about the stock typed.</summary>
    public string StockReadoutText => WorkshopStockReadout.Text ?? string.Empty;

    /// <summary>The cuts, one line each, as the list shows them.</summary>
    public IReadOnlyList<string> CutsOnScreen => _cutLinesOnScreen;

    /// <summary>The hint line.</summary>
    public string HintText => WorkshopHint.Text ?? string.Empty;

    /// <summary>The selected cut's first field: a setback, a radius or a depth.</summary>
    public TextBox CutFirstField => CutFirstBox;

    /// <summary>A corner cut's second setback.</summary>
    public TextBox CutSecondField => CutSecondBox;

    /// <summary>A corner cut's angle.</summary>
    public TextBox CutAngleField => CutAngleBox;

    /// <summary>Applies the cut fields.</summary>
    public Button ApplyCut => ApplyCutButton;

    /// <summary>Mitres the selected corner the full width.</summary>
    public Button FullMitre => FullMitreButton;

    /// <summary>Takes the selected cut off.</summary>
    public Button RemoveCut => RemoveCutButton;

    /// <summary>Whether the selected cut's fields show.</summary>
    public bool IsShowingCutFields => CutFields.IsVisible;

    /// <summary>What the cut fields say the numbers mean.</summary>
    public string CutReadoutText => CutReadout.Text ?? string.Empty;

    /// <summary>Why the cut fields changed nothing, while that is shown.</summary>
    public string CutErrorText => CutError.IsVisible ? CutError.Text ?? string.Empty : string.Empty;

    /// <summary>Opens the sheet on <paramref name="part"/>, its stock in the field.</summary>
    public void Open(Box part)
    {
        Shaping = part.Id;
        IsVisible = true;
        WorkshopDrawing.Blank = part.Id;

        _filling = true;
        try
        {
            WorkshopStockBox.Text = part.Part?.Stock ?? string.Empty;
        }
        finally
        {
            _filling = false;
        }

        Refresh();

        // The hint line is where the modifiers are written down, and it is the first thing a
        // person needs. The view only announces it when it changes, and it opens saying nothing,
        // so the default is put up here rather than waiting for a hover.
        UpdateHint();
    }

    /// <summary>Closes the sheet.</summary>
    public void Close()
    {
        Shaping = null;
        IsVisible = false;
        WorkshopDrawing.Blank = null;
        CutFields.IsVisible = false;
    }

    /// <summary>
    /// Refreshes everything the sheet shows from the sketch.
    /// </summary>
    /// <returns>False when the part being shaped is no longer in the drawing.</returns>
    public bool Refresh()
    {
        if (!IsVisible)
        {
            return true;
        }

        if (_editor is null || Shaping is not { } id || _editor.Design.Sketch.Find<Box>(id) is not { } box)
        {
            return false;
        }

        WorkshopHeadline.Text = CutEntry.Headline(_editor.NameOf(id), box, _editor.LabelFormat);
        UpdateStockReadout();
        UpdateCuts(box);
        ShowCut();
        return true;
    }

    /// <summary>
    /// Makes the blank really be the stock the field names
    /// (<c>docs/design/parts-and-cut-list.md</c> &#xA7;1.2).
    /// </summary>
    /// <remarks>
    /// The same path the properties panel takes: <see cref="StockAssignment"/> says which of the
    /// three dimensions the yard fixes and the batch goes through the editor, so a 1x6 really is
    /// 5&#xBD;&#x2033; wide and a conflict is reported like any other. A box that was not a part
    /// yet becomes one on the way — the workshop leads with the stock picker, and picking a stock
    /// for something that is not a piece to cut would mean nothing.
    /// </remarks>
    /// <returns>Whether the drawing changed.</returns>
    public bool ApplyStock()
    {
        if (_editor is not { } editor || Shaping is not { } id || editor.Design.Sketch.Find<Box>(id) is not { } box
            || (box.Part ?? DefaultPart) is not { } basis)
        {
            return false;
        }

        string typed = (WorkshopStockBox.Text ?? string.Empty).Trim();
        Part part = basis with { Stock = typed.Length == 0 ? null : typed };

        StockItem? stock = MaterialsLibrary.Shipped.TryFind(part.Stock, out StockItem item)
            ? item
            : null;

        string what = typed.Length == 0
            ? $"Took the stock off {editor.NameOf(id)}"
            : $"Cut {editor.NameOf(id)} from {typed}";

        editor.BeginGesture(what);
        UpdateResult result = editor.Apply(
            StockAssignment.RequestsFor(editor.Sketch, box, part, stock),
            what);
        editor.EndGesture();

        StockApplied?.Invoke(this, EventArgs.Empty);
        return result is Succeeded;
    }

    /// <summary>
    /// Applies what the cut fields say to the selected cut: one <see cref="SetCut"/>, or, when the
    /// text is not a length or an angle, the reason in the panel and no change at all.
    /// </summary>
    /// <returns>Whether the drawing changed.</returns>
    public bool ApplyCutEntry()
    {
        if (_editor is not { } editor
            || WorkshopDrawing.LocalBlank is not { } blank
            || WorkshopDrawing.SelectedCut is not { } cut)
        {
            return false;
        }

        TypedCut typed = new(CutFirstBox.Text, CutSecondBox.Text, CutAngleBox.Text);
        switch (CutEntry.Read(blank, cut, typed, editor.LabelFormat))
        {
            case CutEdit edit:
                CutError.IsVisible = false;
                return WorkshopDrawing.SetCutNow(edit.Cut, edit.What);

            case CutEntryProblem problem:
                CutError.Text = problem.Why;
                CutError.IsVisible = true;
                return false;

            default:
                return false;
        }
    }

    /// <summary>Lights the sheet from the drawing's palette.</summary>
    public void ApplyPalette(CanvasPalette palette, IBrush paper, IBrush edge)
    {
        Sheet.Background = paper;
        WorkshopHeadline.Foreground = edge;
        WorkshopHint.Foreground = new SolidColorBrush(palette.Label);
        WorkshopStockReadout.Foreground = new SolidColorBrush(palette.Label);
        WorkshopCutsPanel.Background = paper;
        WorkshopCutsPanel.BorderBrush = new SolidColorBrush(palette.GridMajor);
        WorkshopCutsHeadline.Foreground = edge;
        WorkshopCutsEmpty.Foreground = new SolidColorBrush(palette.Label);
        CutHeadline.Foreground = edge;
        CutReadout.Foreground = new SolidColorBrush(palette.Label);
        CutError.Foreground = new SolidColorBrush(palette.Snap);
        CutFieldsRule.BorderBrush = new SolidColorBrush(palette.GridMajor);
    }

    void UpdateStockReadout() =>
        WorkshopStockReadout.Text = StockAssignment.Readout(WorkshopStockBox.Text, MaterialsLibrary.Shipped);

    /// <summary>The blank's cuts, one line each, with the site selected in the drawing picked.</summary>
    void UpdateCuts(Box box)
    {
        _filling = true;
        try
        {
            _cutsOnScreen.Clear();
            List<string> lines = [];
            foreach (Cut cut in box.Cuts)
            {
                _cutsOnScreen.Add(cut.Site);
                lines.Add(CutEntry.Summary(cut, _editor!.LabelFormat));
            }

            _cutLinesOnScreen = lines;
            WorkshopCutsList.ItemsSource = lines;
            WorkshopCutsList.IsVisible = lines.Count > 0;
            WorkshopCutsEmpty.IsVisible = lines.Count == 0;
            WorkshopCutsHeadline.Text = CutEntry.CutsHeadline(lines.Count);

            WorkshopCutsList.SelectedIndex = WorkshopDrawing.SelectedSite is { } site
                ? _cutsOnScreen.IndexOf(site)
                : -1;
        }
        finally
        {
            _filling = false;
        }
    }

    void UpdateHint() => WorkshopHint.Text = CutEntry.Hint(WorkshopDrawing.Hint);

    void OnCutPicked()
    {
        if (_filling)
        {
            return;
        }

        int index = WorkshopCutsList.SelectedIndex;
        WorkshopDrawing.SelectSite(index >= 0 && index < _cutsOnScreen.Count ? _cutsOnScreen[index] : null);
    }

    /// <summary>Fills the cut fields from the cut the workshop has selected, or hides them.</summary>
    public void ShowCut()
    {
        if (!IsVisible
            || _editor is null
            || WorkshopDrawing.SelectedCut is not { } cut)
        {
            CutFields.IsVisible = false;
            return;
        }

        CutFieldsText fields = CutEntry.Fields(cut, _editor.LabelFormat);
        _filling = true;
        try
        {
            CutFields.IsVisible = true;
            CutError.IsVisible = false;
            CutHeadline.Text = fields.Headline;
            CutFirstCaption.IsVisible = true;
            CutFirstBox.IsVisible = true;
            CutFirstCaption.Text = fields.FirstCaption;
            CutFirstBox.Text = fields.First;

            bool corner = fields.IsCorner;
            CutSecondCaption.IsVisible = corner;
            CutSecondBox.IsVisible = corner;
            CutAngleCaption.IsVisible = corner;
            CutAngleBox.IsVisible = corner;
            FullMitreButton.IsVisible = corner;
            if (corner)
            {
                CutSecondCaption.Text = fields.SecondCaption;
                CutSecondBox.Text = fields.Second;
                CutAngleBox.Text = fields.Angle;
            }

            CutReadout.Text = fields.Readout;
            RemoveCutButton.IsEnabled = true;
            FullMitreButton.IsEnabled = corner;
            ApplyCutButton.IsEnabled = true;
        }
        finally
        {
            _filling = false;
        }

        UpdateCutSelection();
    }

    /// <summary>Keeps the list's highlight on the cut the drawing has selected.</summary>
    void UpdateCutSelection()
    {
        if (WorkshopDrawing.SelectedSite is not { } site)
        {
            return;
        }

        int index = _cutsOnScreen.IndexOf(site);
        if (WorkshopCutsList.SelectedIndex == index)
        {
            return;
        }

        _filling = true;
        try
        {
            WorkshopCutsList.SelectedIndex = index;
        }
        finally
        {
            _filling = false;
        }
    }

    void OnWorkshopDoneClicked(object? sender, RoutedEventArgs e) => DoneRequested?.Invoke(this, EventArgs.Empty);

    void OnWorkshopStockClicked(object? sender, RoutedEventArgs e) => ApplyStock();

    void OnApplyCutClicked(object? sender, RoutedEventArgs e) => ApplyCutEntry();

    void OnRemoveCutClicked(object? sender, RoutedEventArgs e) => WorkshopDrawing.RemoveSelectedCut();

    void OnFullMitreClicked(object? sender, RoutedEventArgs e)
    {
        if (_editor is null
            || WorkshopDrawing.LocalBlank is not { } blank
            || WorkshopDrawing.SelectedSite?.AsCorner is not { } corner)
        {
            return;
        }

        CutEdit mitre = CutEntry.FullMitre(blank, corner, _editor.LabelFormat);
        WorkshopDrawing.SetCutNow(mitre.Cut, mitre.What);
    }

    /// <summary>What a plain box becomes when the workshop's stock field makes it a part: the window's.</summary>
    public Part? DefaultPart { get; set; }
}

using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Napkin.App.Designs;
using Design = Napkin.Modules.Editing.Design;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Napkin.Modules.Furniture;
using Napkin.Modules.Editing;

namespace Napkin.App;

public partial class MainWindow
{
    // ---------------------------------------------------------------------------------------
    // The shape workshop: one blank, its stock, and its cuts
    // (docs/design/shaped-parts-model.md §7.1, §7.2)
    // ---------------------------------------------------------------------------------------

    /// <summary>The workshop sheet, hidden until a part is being shaped.</summary>
    public Border WorkshopPanel => WorkshopSheet;

    /// <summary>The workshop's drawing of the blank.</summary>
    public WorkshopView Workshop => WorkshopDrawing;

    /// <summary>Whether the window is in the shape workshop.</summary>
    public bool IsShapingPart => WorkshopSheet.IsVisible;

    /// <summary>The part being shaped, or null when the workshop is closed.</summary>
    public EntityId? ShapedPart => _shaping;

    /// <summary>The <em>Draw &#x2192; Shape&#x2026;</em> item.</summary>
    public MenuItem ShapeMenuEntry => ShapeMenuItem;

    /// <summary>The workshop's stock field.</summary>
    public TextBox WorkshopStockField => WorkshopStockBox;

    /// <summary>The button that makes the blank really be the stock that was typed.</summary>
    public Button WorkshopStockApply => WorkshopStockButton;

    /// <summary>What the workshop's stock line says.</summary>
    public string WorkshopStockReadoutText => WorkshopStockReadout.Text ?? string.Empty;

    /// <summary>What the workshop's cut list is showing, one line each, in site order.</summary>
    public IReadOnlyList<string> WorkshopCutsOnScreen => _cutLinesOnScreen;

    /// <summary>What the workshop says a press would do, or empty when the pointer is on nothing.</summary>
    public string WorkshopHintText => WorkshopHint.Text ?? string.Empty;

    /// <summary>The selected cut's first typed value.</summary>
    public TextBox CutFirstField => CutFirstBox;

    /// <summary>The selected cut's second typed value, when it has two.</summary>
    public TextBox CutSecondField => CutSecondBox;

    /// <summary>The angle a corner cut is cut at, as an entry mode (&#xA7;1.3).</summary>
    public TextBox CutAngleField => CutAngleBox;

    /// <summary>The button that applies what the cut fields say.</summary>
    public Button ApplyCut => ApplyCutButton;

    /// <summary>The button that sets both setbacks to the blank's width, once.</summary>
    public Button FullMitre => FullMitreButton;

    /// <summary>The button that takes the selected cut off the blank.</summary>
    public Button RemoveCut => RemoveCutButton;

    /// <summary>Whether the cut fields are on screen.</summary>
    public bool IsShowingCutFields => CutFields.IsVisible;

    /// <summary>What the cut fields say about the selected cut, in words.</summary>
    public string CutReadoutText => CutReadout.Text ?? string.Empty;

    /// <summary>What the cut fields are complaining about, or empty when they are not.</summary>
    public string CutErrorText => CutError.IsVisible ? CutError.Text ?? string.Empty : string.Empty;

    /// <summary>
    /// Opens the shape workshop on one part.
    /// </summary>
    /// <remarks>
    /// It is a mode, not a second document (&#xA7;7.1): the same editor, the same sketch, the same
    /// selection. Leaving it returns to the canvas with the part exactly where it was, because
    /// nothing about where it is was ever touched.
    /// </remarks>
    /// <returns>Whether the workshop opened.</returns>
    public bool OpenWorkshop(EntityId box)
    {
        if (Editor.Design.Sketch.Find<Box>(box) is not { } part)
        {
            return false;
        }

        CloseDimensionEditor(focusCanvas: false);
        Editor.Select(box);

        _shaping = box;
        WorkshopSheet.IsVisible = true;
        WorkshopDrawing.Blank = box;

        // The chrome that belongs to the canvas goes while the canvas is behind the sheet. The
        // workshop says what the part is in its own headline and leads with its own stock picker,
        // so nothing a person needs here is in the panels that just went.
        ToolBar.IsVisible = false;
        RelationshipsPanel.IsVisible = false;
        PropertiesPanel.IsVisible = false;
        DrawingCanvas.ArmStock(null);
        UpdateToolbox();

        _showingCut = true;
        try
        {
            WorkshopStockBox.Text = part.Part?.Stock ?? string.Empty;
        }
        finally
        {
            _showingCut = false;
        }

        UpdateWorkshop();

        // The hint line is where the modifiers are written down, and it is the first thing a
        // person needs. The view only announces it when it changes, and it opens saying nothing,
        // so the default is put up here rather than waiting for a hover.
        UpdateWorkshopHint();
        UpdateMenuEnablement();
        ShowProperties();
        WorkshopDrawing.Focus();
        return true;
    }

    /// <summary>Leaves the workshop, and gives the drawing back.</summary>
    public void CloseWorkshop()
    {
        if (!WorkshopSheet.IsVisible)
        {
            return;
        }

        _shaping = null;
        WorkshopSheet.IsVisible = false;
        WorkshopDrawing.Blank = null;
        ToolBar.IsVisible = true;
        CutFields.IsVisible = false;
        UpdateToolbox();

        UpdateRelationships();
        UpdateMenuEnablement();
        ShowProperties();
        FocusDrawing();
    }

    /// <summary>
    /// Makes the blank really be the stock the workshop's field names
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
    public bool ApplyWorkshopStock()
    {
        if (_shaping is not { } id || Editor.Design.Sketch.Find<Box>(id) is not { } box)
        {
            return false;
        }

        string typed = (WorkshopStockBox.Text ?? string.Empty).Trim();
        Part part = (box.Part ?? DefaultPart) with { Stock = typed.Length == 0 ? null : typed };

        StockItem? stock = MaterialsLibrary.Shipped.TryFind(part.Stock, out StockItem item)
            ? item
            : null;

        string what = typed.Length == 0
            ? $"Took the stock off {Editor.NameOf(id)}"
            : $"Cut {Editor.NameOf(id)} from {typed}";

        Editor.BeginGesture(what);
        UpdateResult result = Editor.Apply(
            StockAssignment.RequestsFor(Editor.Sketch, box, part, stock),
            what);
        Editor.EndGesture();

        UpdateWorkshop();
        ShowProperties();
        return result is Succeeded;
    }

    /// <summary>
    /// Applies what the cut fields say to the selected cut: one <see cref="SetCut"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A corner cut has two descriptions and only one of them can be exact (&#xA7;1.3), so the
    /// panel offers both and one of them wins: when the angle has been edited it is the angle,
    /// converted to the setback it implies with one explicit rounding, keeping the longer setback
    /// as the edge that angle is measured against; otherwise it is the two lengths, taken as
    /// typed.
    /// </para>
    /// <para>
    /// Text that is not a length or an angle is explained in the panel and changes nothing at all
    /// — the same rule the dimension field has had from the start.
    /// </para>
    /// </remarks>
    /// <returns>Whether the drawing changed.</returns>
    public bool ApplyCutEntry()
    {
        if (WorkshopDrawing.LocalBlank is not { } blank || WorkshopDrawing.SelectedCut is not { } cut)
        {
            return false;
        }

        switch (cut)
        {
            case CornerCut clip:
                return ApplyCornerCutEntry(blank, clip);

            case RoundedCorner rounded:
                return Read(CutFirstBox, "A radius") is { } radius
                       && WorkshopDrawing.SetCutNow(
                           rounded with { Radius = radius },
                           $"Set {blank.Name}'s {cut.Site} to a {Show(radius)} radius");

            case CurvedEdge curve:
                return Read(CutFirstBox, "A depth") is { } depth
                       && WorkshopDrawing.SetCutNow(
                           curve with { Depth = depth },
                           $"Set {blank.Name}'s {cut.Site} to {Show(depth)} deep");

            default:
                return false;
        }
    }

    bool ApplyCornerCutEntry(Box blank, CornerCut clip)
    {
        if (AngleWasEdited(clip) && CutAngle.TryParseDegrees(CutAngleBox.Text, out double degrees))
        {
            Length longer = Length.Max(clip.AlongX, clip.AlongY);
            if (CutAngle.SetbackFor(longer, degrees) is not { } shorter)
            {
                return ComplainAboutCut("A cut is made at more than 0° and less than 90° off square.");
            }

            CornerCut angled = clip.AlongX >= clip.AlongY
                ? clip with { AlongY = shorter }
                : clip with { AlongX = shorter };

            return WorkshopDrawing.SetCutNow(
                angled,
                $"Set {blank.Name}'s {clip.Site} to {CutAngle.Text(shorter, longer)} off square");
        }

        if (Read(CutFirstBox, "A setback") is not { } alongX
            || Read(CutSecondBox, "A setback") is not { } alongY)
        {
            return false;
        }

        return WorkshopDrawing.SetCutNow(
            clip with { AlongX = alongX, AlongY = alongY },
            $"Set {blank.Name}'s {clip.Site} to {Show(alongX)} by {Show(alongY)}");
    }

    /// <summary>Whether the angle field holds something other than what the stored cut reads as.</summary>
    bool AngleWasEdited(CornerCut clip) => !string.Equals(
        (CutAngleBox.Text ?? string.Empty).Trim(),
        CutAngle.Text(Length.Min(clip.AlongX, clip.AlongY), Length.Max(clip.AlongX, clip.AlongY)),
        StringComparison.Ordinal);

    /// <summary>A length out of a cut field, or nothing at all when it does not read as one.</summary>
    Length? Read(TextBox field, string what)
    {
        if (DimensionEntry.Interpret(field.Text) is ReadableLength readable && readable.Value > Length.Zero)
        {
            CutError.IsVisible = false;
            return readable.Value;
        }

        ComplainAboutCut($"{what} has to be a length greater than zero, like 3/4\" or 1' 4 1/4\".");
        return null;
    }

    bool Complain(string why)
    {
        PropertiesError.Text = why;
        PropertiesError.IsVisible = true;
        return false;
    }

    bool ComplainAboutCut(string why)
    {
        CutError.Text = why;
        CutError.IsVisible = true;
        return false;
    }

    /// <summary>Refreshes everything the workshop shows from the sketch.</summary>
    void UpdateWorkshop()
    {
        if (!WorkshopSheet.IsVisible)
        {
            return;
        }

        if (_shaping is not { } id || Editor.Design.Sketch.Find<Box>(id) is not { } box)
        {
            CloseWorkshop();
            return;
        }

        WorkshopHeadline.Text = $"Shaping {Editor.NameOf(id)} — "
                                + $"{Show(box.Width)} × {Show(box.Height)} blank";

        UpdateWorkshopStockReadout();
        UpdateWorkshopCuts(box);
        ShowCut();
    }

    void UpdateWorkshopStockReadout() =>
        WorkshopStockReadout.Text = StockReadoutFor(WorkshopStockBox.Text);

    /// <summary>The blank's cuts, one line each, with the site selected in the drawing picked.</summary>
    void UpdateWorkshopCuts(Box box)
    {
        _showingCut = true;
        try
        {
            _cutsOnScreen.Clear();
            List<string> lines = [];
            foreach (Cut cut in box.Cuts)
            {
                _cutsOnScreen.Add(cut.Site);
                lines.Add(CutSummary(cut, Editor.LabelFormat));
            }

            _cutLinesOnScreen = lines;
            WorkshopCutsList.ItemsSource = lines;
            WorkshopCutsList.IsVisible = lines.Count > 0;
            WorkshopCutsEmpty.IsVisible = lines.Count == 0;
            WorkshopCutsHeadline.Text = lines.Count == 1 ? "Cuts — 1" : $"Cuts — {lines.Count}";

            WorkshopCutsList.SelectedIndex = WorkshopDrawing.SelectedSite is { } site
                ? _cutsOnScreen.IndexOf(site)
                : -1;
        }
        finally
        {
            _showingCut = false;
        }
    }

    void UpdateWorkshopHint()
    {
        string hint = WorkshopDrawing.Hint;
        WorkshopHint.Text = hint.Length > 0
            ? hint + "."
            : "Drag a corner to clip it, Shift for 45°, Alt to round it; drag an edge's middle to "
              + "curve it. Delete takes the selected cut off.";
    }

    void OnWorkshopCutPicked()
    {
        if (_showingCut)
        {
            return;
        }

        int index = WorkshopCutsList.SelectedIndex;
        WorkshopDrawing.SelectSite(index >= 0 && index < _cutsOnScreen.Count ? _cutsOnScreen[index] : null);
    }

    /// <summary>
    /// Fills the cut fields from the cut the workshop has selected, or hides them.
    /// </summary>
    void ShowCut()
    {
        if (!WorkshopSheet.IsVisible
            || WorkshopDrawing.LocalBlank is not { } blank
            || WorkshopDrawing.SelectedCut is not { } cut)
        {
            CutFields.IsVisible = false;
            return;
        }

        _showingCut = true;
        try
        {
            CutFields.IsVisible = true;
            CutError.IsVisible = false;
            CutHeadline.Text = $"Cut at the {BlankShape.Words(cut.Site)}";
            CutFirstCaption.IsVisible = true;
            CutFirstBox.IsVisible = true;

            bool corner = cut is CornerCut;
            CutSecondCaption.IsVisible = corner;
            CutSecondBox.IsVisible = corner;
            CutAngleCaption.IsVisible = corner;
            CutAngleBox.IsVisible = corner;
            FullMitreButton.IsVisible = corner;

            switch (cut)
            {
                case CornerCut clip:
                    CutFirstCaption.Text = $"{BlankShape.Compass(XEdge(clip.Corner))} edge";
                    CutFirstBox.Text = clip.AlongX.Format(Editor.LabelFormat).Text;
                    CutSecondCaption.Text = $"{BlankShape.Compass(YEdge(clip.Corner))} edge";
                    CutSecondBox.Text = clip.AlongY.Format(Editor.LabelFormat).Text;
                    CutAngleBox.Text = CutAngle.Text(
                        Length.Min(clip.AlongX, clip.AlongY),
                        Length.Max(clip.AlongX, clip.AlongY));
                    CutReadout.Text =
                        "Two marks, one on each edge. Typing an angle instead keeps the longer "
                        + "mark and works the shorter one out from it, rounding once.";
                    break;

                case RoundedCorner rounded:
                    CutFirstCaption.Text = "Radius";
                    CutFirstBox.Text = rounded.Radius.Format(Editor.LabelFormat).Text;
                    CutReadout.Text = "A quarter circle, tangent to both edges that meet here.";
                    break;

                case CurvedEdge curve:
                    CutFirstCaption.Text = "Depth";
                    CutFirstBox.Text = curve.Depth.Format(Editor.LabelFormat).Text;
                    CutReadout.Text = curve.Bow == Bow.Inward
                        ? "A scallop: the corners stay and the middle goes in by this much."
                        : "A bow: the middle stays and the corners come in by this much.";
                    break;
            }

            RemoveCutButton.IsEnabled = true;
            FullMitreButton.IsEnabled = corner;
            ApplyCutButton.IsEnabled = true;
        }
        finally
        {
            _showingCut = false;
        }

        UpdateWorkshopCutSelection();
    }

    /// <summary>Keeps the list's highlight on the cut the drawing has selected.</summary>
    void UpdateWorkshopCutSelection()
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

        _showingCut = true;
        try
        {
            WorkshopCutsList.SelectedIndex = index;
        }
        finally
        {
            _showingCut = false;
        }
    }

    void OnShapeClicked(object? sender, RoutedEventArgs e) => RunSelectionCommand(SelectionCommand.Shape);

    void OnWorkshopDoneClicked(object? sender, RoutedEventArgs e) => CloseWorkshop();

    void OnWorkshopStockClicked(object? sender, RoutedEventArgs e) => ApplyWorkshopStock();

    void OnApplyCutClicked(object? sender, RoutedEventArgs e) => ApplyCutEntry();

    void OnRemoveCutClicked(object? sender, RoutedEventArgs e) => WorkshopDrawing.RemoveSelectedCut();

    void OnFullMitreClicked(object? sender, RoutedEventArgs e)
    {
        if (WorkshopDrawing.LocalBlank is not { } blank
            || WorkshopDrawing.SelectedSite?.AsCorner is not { } corner)
        {
            return;
        }

        CornerCut mitre = CutAngle.FullMitre(blank, corner);
        WorkshopDrawing.SetCutNow(
            mitre,
            $"Mitred {blank.Name}'s {mitre.Site} the full {Show(mitre.AlongX)}");
    }

    /// <summary>One cut as the workshop's list writes it: the site, the kind, and the numbers.</summary>
    static string CutSummary(Cut cut, LengthFormat format) => cut switch
    {
        CornerCut clip =>
            $"{BlankShape.Words(cut.Site)} — clip {Text(clip.AlongX, format)} × {Text(clip.AlongY, format)}",
        RoundedCorner rounded =>
            $"{BlankShape.Words(cut.Site)} — round, {Text(rounded.Radius, format)} radius",
        CurvedEdge { Bow: Bow.Inward } scallop =>
            $"{BlankShape.Words(cut.Site)} — scallop {Text(scallop.Depth, format)} deep",
        CurvedEdge curve =>
            $"{BlankShape.Words(cut.Site)} — curve {Text(curve.Depth, format)} deep",
        _ => BlankShape.Words(cut.Site),
    };


    static string Text(Length length, LengthFormat format)
    {
        FormattedLength formatted = length.Format(format);
        return formatted.IsExact ? formatted.Text : CutAngle.Approximately + formatted.Text;
    }

    /// <summary>The corner's edge that runs along the blank's local X.</summary>
    static BoxEdge XEdge(BoxCorner corner) =>
        corner is BoxCorner.SouthWest or BoxCorner.SouthEast ? BoxEdge.South : BoxEdge.North;

    /// <summary>The corner's edge that runs along the blank's local Y.</summary>
    static BoxEdge YEdge(BoxCorner corner) =>
        corner is BoxCorner.SouthWest or BoxCorner.NorthWest ? BoxEdge.West : BoxEdge.East;
}

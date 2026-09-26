using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

namespace Napkin.App;

/// <summary>
/// An angled part's panel (docs/design/assembly-model.md §3a.7, angled-parts.md §1.2, §2.4): its two
/// ends, each typed; the plane each end is cut to; which way its wide face keeps, in plain words; its
/// stock and cross-section; and three derived readouts — length, tilt and azimuth — each marked ≈
/// unless exact. Apply is one undo step: the ends first, then the cuts, then the sizes, so that a flat
/// brace can be raised and cut to the floor in one go.
/// </summary>
public partial class MainWindow
{
    static readonly EndCut[] EndCutChoices = [EndCut.Z, EndCut.X, EndCut.Y, EndCut.Square];
    static readonly Axis[] ReferenceChoices = [Axis.Z, Axis.X, Axis.Y];
    bool _fillingStrut;

    /// <summary>Whether the angled-part panel is showing (one strut is selected).</summary>
    public bool IsShowingStrut => StrutPanel.IsVisible;

    /// <summary>The panel's fields, for the GUI suite.</summary>
    public (TextBox FromX, TextBox FromY, TextBox FromZ, TextBox ToX, TextBox ToY, TextBox ToZ, TextBox Stock, TextBox Height, TextBox Depth) StrutFields
        => (StrutFromXBox, StrutFromYBox, StrutFromZBox, StrutToXBox, StrutToYBox, StrutToZBox, StrutStockBox, StrutHeightBox, StrutDepthBox);

    /// <summary>The panel's pickers, for the GUI suite: the two cuts and the wide face.</summary>
    public (ComboBox FromCut, ComboBox ToCut, ComboBox Reference) StrutPickers => (StrutFromCutBox, StrutToCutBox, StrutReferenceBox);

    /// <summary>The lean fields, for the GUI suite: tilt, azimuth and rise.</summary>
    public (TextBox Tilt, TextBox Azimuth, TextBox Rise, Button Place) StrutLeanFields => (StrutTiltBox, StrutAzimuthBox, StrutRiseBox, StrutPlaceTopButton);

    /// <summary>The picker for whether the run is the part's length or its width.</summary>
    public ComboBox StrutRunIsPicker => StrutRunIsBox;

    /// <summary>What the panel's readout line says.</summary>
    public string StrutReadoutText => StrutPanel.IsVisible ? StrutReadout.Text ?? string.Empty : string.Empty;

    // Short, to fit beside each other: level (a floor or an underside), a face square to X or Y, or square.
    static string CutWord(EndCut cut) => cut switch
    {
        EndCut.Z => "level",
        EndCut.X => "X face",
        EndCut.Y => "Y face",
        _ => "square",
    };

    void WireStruts()
    {
        StrutFromCutBox.ItemsSource = EndCutChoices.Select(CutWord).ToArray();
        StrutToCutBox.ItemsSource = EndCutChoices.Select(CutWord).ToArray();
        StrutReferenceBox.ItemsSource = ReferenceChoices.Select(StrutTool.ReferenceWords).ToArray();
        StrutRunIsBox.ItemsSource = new[] { "length", "width" };

        foreach (TextBox field in new[] { StrutFromXBox, StrutFromYBox, StrutFromZBox, StrutToXBox, StrutToYBox, StrutToZBox, StrutStockBox, StrutHeightBox, StrutDepthBox })
        {
            field.AddHandler(KeyDownEvent, (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ApplyStrut();
                    FocusDrawing();
                    e.Handled = true;
                }
            }, RoutingStrategies.Tunnel);
        }

        Editor.SelectionChanged += (_, _) => ShowStrutPanel();
        Editor.DesignChanged += (_, _) => ShowStrutPanel();
    }

    Strut? SelectedStrut() => Editor.OnlySelected is { } id ? Editor.Sketch.Find<Strut>(id) : null;

    /// <summary>Shows the panel for one selected strut, or hides it.</summary>
    void ShowStrutPanel()
    {
        Strut? strut = SelectedStrut();
        StrutPanel.IsVisible = strut is not null && !IsShapingPart;
        if (strut is null)
        {
            return;
        }

        _fillingStrut = true;
        try
        {
            string Text(Length value) => value.Format(Editor.LabelFormat).Text;
            StrutHeadline.Text = Editor.NameOf(strut.Id);
            StrutFromXBox.Text = Text(strut.From.X);
            StrutFromYBox.Text = Text(strut.From.Y);
            StrutFromZBox.Text = Text(strut.From.Z);
            StrutToXBox.Text = Text(strut.To.X);
            StrutToYBox.Text = Text(strut.To.Y);
            StrutToZBox.Text = Text(strut.To.Z);
            StrutFromCutBox.SelectedIndex = Array.IndexOf(EndCutChoices, strut.FromCut);
            StrutToCutBox.SelectedIndex = Array.IndexOf(EndCutChoices, strut.ToCut);
            StrutReferenceBox.SelectedIndex = Array.IndexOf(ReferenceChoices, strut.Reference);
            StrutRunIsBox.SelectedIndex = strut.Part?.PlanAxes.X == PartDimension.Width ? 1 : 0;
            StrutStockBox.Text = strut.Part?.Stock ?? string.Empty;
            StrutHeightBox.Text = Text(strut.Height);
            StrutDepthBox.Text = Text(strut.Depth);
            (string length, string tilt, string azimuth) = StrutTool.Readouts(strut, Editor.LabelFormat);
            StrutReadout.Text = $"Length {length} · tilt {tilt} · azimuth {azimuth}";
        }
        finally
        {
            _fillingStrut = false;
        }
    }

    void OnStrutApplyClicked(object? sender, RoutedEventArgs e) => ApplyStrut();

    /// <summary>Applies what the panel says to the selected strut, as one undo step.</summary>
    public void ApplyStrut()
    {
        if (_fillingStrut || SelectedStrut() is not { } strut)
        {
            return;
        }

        if (!TryPoint(StrutFromXBox, StrutFromYBox, StrutFromZBox, out Point3 from)
            || !TryPoint(StrutToXBox, StrutToYBox, StrutToZBox, out Point3 to)
            || !Length.TryParse(StrutHeightBox.Text, out Length height, out _) || height <= Length.Zero
            || !Length.TryParse(StrutDepthBox.Text, out Length depth, out _) || depth <= Length.Zero)
        {
            Editor.Say(EditSeverity.Problem, "Each end is three lengths, and the width and thickness are lengths greater than zero.");
            return;
        }

        EndCut fromCut = EndCutChoices[Math.Max(StrutFromCutBox.SelectedIndex, 0)];
        EndCut toCut = EndCutChoices[Math.Max(StrutToCutBox.SelectedIndex, 0)];
        Axis reference = ReferenceChoices[Math.Max(StrutReferenceBox.SelectedIndex, 0)];

        List<Request> requests = [];
        if (from != strut.From)
        {
            requests.Add(new SetStrutEnd(strut.Id, StrutEnd.From, from));
        }

        if (to != strut.To)
        {
            requests.Add(new SetStrutEnd(strut.Id, StrutEnd.To, to));
        }

        if (fromCut != strut.FromCut || toCut != strut.ToCut || reference != strut.Reference)
        {
            requests.Add(new SetStrutCuts(strut.Id, fromCut, toCut, reference));
        }

        // The run is its length (a leg) or its width (an angled shelf), which names the other two
        // (angled-parts §1.5, invariant 16).
        PlanAxes axes = StrutRunIsBox.SelectedIndex == 1
            ? new PlanAxes(PartDimension.Width, PartDimension.Thickness)
            : new PlanAxes(PartDimension.Length, PartDimension.Width);

        // A stock named fixes the cross-section through the yard's sizes; otherwise the typed sizes do.
        string stock = StrutStockBox.Text?.Trim() ?? string.Empty;
        if (!string.Equals(stock, strut.Part?.Stock ?? string.Empty, StringComparison.Ordinal) || (strut.Part is { } had && had.PlanAxes != axes))
        {
            Part part = (strut.Part ?? new Part(null, null, 1, axes)) with { Stock = stock.Length == 0 ? null : stock, PlanAxes = axes };
            StockItem? item = MaterialsLibrary.Shipped.TryFind(part.Stock, out StockItem found) ? found : null;
            requests.AddRange(StockAssignment.RequestsFor(Editor.Sketch, strut, part, item).Requests);
        }
        else
        {
            if (height != strut.Height)
            {
                requests.Add(StockAssignment.SizeRequest(Editor.Sketch, new StrutHeightRef(strut.Id), height));
            }

            if (depth != strut.Depth)
            {
                requests.Add(StockAssignment.SizeRequest(Editor.Sketch, new StrutDepthRef(strut.Id), depth));
            }
        }

        if (requests.Count == 0)
        {
            return;
        }

        Editor.Apply(new Batch([.. requests]), $"Changed {Editor.NameOf(strut.Id)}");
    }

    void OnStrutPlaceTopClicked(object? sender, RoutedEventArgs e) => PlaceStrutTop();

    /// <summary>
    /// Places the selected strut's top by a typed lean (angled-parts §1.1): the tilt from vertical, the
    /// direction in the plan and the rise, worked out and rounded once to the grid; says so when the
    /// rounding moved it. One undo step; the angle is not kept.
    /// </summary>
    public void PlaceStrutTop()
    {
        if (SelectedStrut() is not { } strut)
        {
            return;
        }

        System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;
        if (!double.TryParse(StrutTiltBox.Text, System.Globalization.NumberStyles.Float, invariant, out double tilt) || tilt < 0 || tilt >= 90
            || !double.TryParse(string.IsNullOrWhiteSpace(StrutAzimuthBox.Text) ? "0" : StrutAzimuthBox.Text, System.Globalization.NumberStyles.Float, invariant, out double azimuth)
            || !Length.TryParse(StrutRiseBox.Text, out Length rise, out _) || rise <= Length.Zero)
        {
            Editor.Say(EditSeverity.Problem, "A lean is a tilt from vertical under 90°, a direction in degrees (0 east, 90 north) and a rise greater than zero.");
            return;
        }

        (Point3 top, bool rounded) = StrutTool.ByAngle(strut.From, tilt, azimuth, rise, null);
        string said = rounded ? " — to the nearest 1/1024″" : string.Empty;
        if (Editor.Apply(new SetStrutEnd(strut.Id, StrutEnd.To, top), $"Placed {Editor.NameOf(strut.Id)}'s top{said}") is Succeeded && rounded)
        {
            Editor.Say(EditSeverity.Done, $"Placed {Editor.NameOf(strut.Id)}'s top, rounded to the nearest 1/1024″; the panel shows the angle it now has.");
        }
    }

    static bool TryPoint(TextBox x, TextBox y, TextBox z, out Point3 point)
    {
        point = default;
        if (!Length.TryParse(x.Text, out Length px, out _) || !Length.TryParse(y.Text, out Length py, out _) || !Length.TryParse(z.Text, out Length pz, out _))
        {
            return false;
        }

        point = new Point3(px, py, pz);
        return true;
    }
}

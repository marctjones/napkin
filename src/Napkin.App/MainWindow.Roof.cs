using System.Globalization;

using Avalonia.Controls;
using Avalonia.Interactivity;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

namespace Napkin.App;

/// <summary>
/// A porch roof's block in the Part panel (docs/design/deck-and-porch.md §5.3, §8): the pitch typed as
/// "5 in 12" (which sets the box's rise), the inputs as typed boxes, and the rafters, cuts, coverings,
/// rafter check and sunroom test, derived every time. Every change is one undo step.
/// </summary>
public partial class MainWindow
{
    bool _fillingRoof;

    string _shownPitch = string.Empty;

    /// <summary>Whether the roof block is showing (a roof is selected).</summary>
    public bool IsShowingRoof => RoofFields.IsVisible;

    /// <summary>The roof's headline: its pitch and its run.</summary>
    public string RoofHeadlineText => RoofFields.IsVisible ? RoofHeadline.Text ?? string.Empty : string.Empty;

    /// <summary>The rafters, cuts, coverings and notes, or why there is no frame.</summary>
    public string RoofFrameLines => RoofFields.IsVisible ? RoofFrameText.Text ?? string.Empty : string.Empty;

    /// <summary>The rafter check.</summary>
    public string RoofCheckLine => RoofFields.IsVisible ? RoofCheckText.Text ?? string.Empty : string.Empty;

    /// <summary>The sunroom test's line.</summary>
    public string RoofGlazingLine => RoofFields.IsVisible ? RoofGlazingText.Text ?? string.Empty : string.Empty;

    /// <summary>The roof block's text boxes, for the GUI suite.</summary>
    public (TextBox Pitch, TextBox Overhang, TextBox Rafter, TextBox Spacing, TextBox Ledger, TextBox Sheathing, TextBox Roofing, TextBox Coverage, TextBox Waste) RoofControls
        => (RoofPitchBox, RoofOverhangBox, RoofRafterBox, RoofSpacingBox, RoofLedgerBox, RoofSheathingBox, RoofRoofingBox, RoofCoverageBox, RoofWasteBox);

    TextBox[] RoofTextBoxes =>
        [RoofPitchBox, RoofOverhangBox, RoofRafterBox, RoofSpacingBox, RoofLedgerBox, RoofSheathingBox, RoofRoofingBox, RoofCoverageBox, RoofWasteBox];

    bool IsRoofField(TextBox box) => Array.IndexOf(RoofTextBoxes, box) >= 0;

    void WireRoof()
    {
        foreach (TextBox box in RoofTextBoxes)
        {
            box.LostFocus += (_, _) =>
            {
                if (!_fillingRoof && RoofFields.IsVisible)
                {
                    ApplyRoof();
                }
            };
        }
    }

    Roof? SelectedRoof() => Editor.OnlySelectedBox is { } box && Roof.Is(Editor.Sketch, box) ? new Roof(box) : null;

    /// <summary>Fills the roof block for a roof, or hides it for anything else.</summary>
    void ShowRoof(Box box)
    {
        bool isRoof = Roof.Is(Editor.Sketch, box) && box.Roof is not null;
        RoofFields.IsVisible = isRoof;
        if (!isRoof)
        {
            return;
        }

        Roof roof = new(box);
        RoofInputs inputs = box.Roof!;
        string Text(Length length) => length.Format(Editor.LabelFormat).Text;
        RoofHeadline.Text = $"{roof.Name}: {roof.Pitch}, {Text(box.Width)} along the house, a run of {Text(box.Height)}.";
        RoofLowEndText.Text = inputs.LowEnd switch
        {
            WallLowEnd wall => $"Low end: {Editor.NameOf(wall.Wall)}'s top plates.",
            BeamLowEnd beam => $"Low end: a ({beam.Beam.Plies}) {beam.Beam.Lumber} beam on {beam.PostCount} {beam.Post} posts standing on the deck.",
            _ => string.Empty,
        };

        (RoofFraming? framing, string? problem) = RoofFrame.Of(Editor.Sketch, roof, MaterialsLibrary.Shipped);
        if (framing is null)
        {
            RoofFrameText.Text = problem;
            RoofCheckText.Text = string.Empty;
            RoofGlazingText.Text = Glazing.NoRoof;
        }
        else
        {
            RoofFrameText.Text = string.Join("\n", new[] { framing.Line + ".", framing.Cuts }.Concat(framing.Coverings).Concat(framing.Notes));
            RoofCheckText.Text = RoofCheck.Rafters(Editor.Sketch.After(), framing, Packs.Resolve(Editor.Sketch.Code).Pack).Text;
            RoofGlazingText.Text = Glazing.Of(Editor.Sketch.After(), framing.Deck, box.Height, box.Depth, framing.SlopedArea)?.Text
                                   ?? "Glazing: no wall stands on the deck yet, so there is nothing to take a ratio of.";
        }

        _fillingRoof = true;
        try
        {
            _shownPitch = roof.Pitch;
            RoofPitchBox.Text = roof.Pitch;
            RoofOverhangBox.Text = Text(inputs.Overhang);
            RoofRafterBox.Text = inputs.Rafter;
            RoofSpacingBox.Text = Text(inputs.RafterSpacing);
            RoofLedgerBox.Text = inputs.Ledger;
            RoofSheathingBox.Text = inputs.Sheathing ?? string.Empty;
            RoofRoofingBox.Text = inputs.Roofing.Name;
            RoofCoverageBox.Text = inputs.Roofing.Coverage?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            RoofWasteBox.Text = inputs.Roofing.Waste.ToString(CultureInfo.InvariantCulture);
            RoofBlockingCheck.IsChecked = inputs.Blocking;
        }
        finally
        {
            _fillingRoof = false;
        }
    }

    void OnRoofTicked(object? sender, RoutedEventArgs e)
    {
        if (!_fillingRoof && RoofFields.IsVisible)
        {
            ApplyRoof();
        }
    }

    /// <summary>
    /// Reads the roof block and sets the selected roof's inputs and, when the pitch changed, its rise:
    /// one undo step. A value that does not read is said and nothing changes.
    /// </summary>
    public bool ApplyRoof()
    {
        if (SelectedRoof() is not { Box.Roof: { } now } roof)
        {
            return false;
        }

        List<string> problems = [];

        Length Measure(TextBox box, string what, bool zeroAllowed, Length fallback)
        {
            if (Length.TryParse(box.Text?.Trim() ?? string.Empty, out Length value, out _) && (value > Length.Zero || (zeroAllowed && value == Length.Zero)))
            {
                return value;
            }

            problems.Add($"{what} is a length, like 16\"");
            return fallback;
        }

        string Named(TextBox box, string what, string fallback)
        {
            string text = box.Text?.Trim() ?? string.Empty;
            if (text.Length > 0)
            {
                return text;
            }

            problems.Add($"{what} is named, like {fallback}");
            return fallback;
        }

        int? Whole(TextBox box, string what, int least, bool emptyAllowed, int? fallback)
        {
            string text = box.Text?.Trim() ?? string.Empty;
            if (text.Length == 0 && emptyAllowed)
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= least)
            {
                return value;
            }

            problems.Add($"{what} is a whole number, {least} or more");
            return fallback;
        }

        // The pitch: left alone when it still says what it was shown as (an inexact one reads "≈ …").
        Length rise = roof.Box.Depth;
        string pitch = RoofPitchBox.Text?.Trim() ?? string.Empty;
        if (pitch != _shownPitch)
        {
            if (RoofTool.RiseFor(pitch, roof.Box.Height) is { } typed)
            {
                rise = typed;
            }
            else
            {
                problems.Add("the pitch is a rise in 12, like 5 in 12");
            }
        }

        RoofInputs next = now with
        {
            Overhang = Measure(RoofOverhangBox, "the overhang", true, now.Overhang),
            Rafter = Named(RoofRafterBox, "the rafter", now.Rafter),
            RafterSpacing = Measure(RoofSpacingBox, "the rafter spacing", false, now.RafterSpacing),
            Ledger = Named(RoofLedgerBox, "the ledger", now.Ledger),
            Sheathing = string.IsNullOrWhiteSpace(RoofSheathingBox.Text) ? null : RoofSheathingBox.Text.Trim(),
            Roofing = new Roofing(
                Named(RoofRoofingBox, "the roofing", now.Roofing.Name),
                Whole(RoofCoverageBox, "the roofing's coverage", 1, true, now.Roofing.Coverage),
                Whole(RoofWasteBox, "the waste", 0, false, now.Roofing.Waste) ?? 0),
            Blocking = RoofBlockingCheck.IsChecked == true,
        };

        if (problems.Count > 0)
        {
            Editor.Say(EditSeverity.Problem, $"{roof.Name}: {string.Join("; ", problems)}. Nothing was changed.");
            return false;
        }

        List<Request> requests = [];
        if (next != now)
        {
            requests.Add(new SetRoofInputs(roof.Id, next));
        }

        if (rise != roof.Box.Depth)
        {
            requests.Add(StockAssignment.SizeRequest(Editor.Sketch, new BoxDepthRef(roof.Id), rise));
        }

        if (requests.Count == 0)
        {
            return false;
        }

        string what = rise != roof.Box.Depth ? $"Set {roof.Name}'s pitch" : $"Set {roof.Name}'s frame";
        Editor.Apply(requests.Count == 1 ? requests[0] : Batch.Of([.. requests]), what);
        return true;
    }
}

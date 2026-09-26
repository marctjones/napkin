using System.Globalization;

using Avalonia.Controls;
using Avalonia.Interactivity;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// A deck's block in the Part panel (docs/design/deck-and-porch.md §2.2, §8): the inputs as typed
/// boxes, and the frame in one line, derived every time. Every change is one undo step; what the deck
/// supports, the species and the footing depth start empty and are never defaulted.
/// </summary>
public partial class MainWindow
{
    bool _fillingDeck;

    FrostSuggestion? _frostOffered;

    /// <summary>The deck's code check lines as the panel shows them.</summary>
    public string DeckCheckLines => DeckFields.IsVisible ? DeckCheckText.Text ?? string.Empty : string.Empty;

    /// <summary>The adopted code's frost depth offered, or empty.</summary>
    public string DeckFrostOfferLine => DeckFrostOffer.IsVisible ? DeckFrostOfferText.Text ?? string.Empty : string.Empty;

    /// <summary>The button that accepts the frost depth offered.</summary>
    public Button DeckUseFrost => DeckUseFrostButton;

    /// <summary>Accepts the offered frost depth into the site values, saying where it came from: one undo step (§3.4).</summary>
    void OnUseFrostClicked(object? sender, RoutedEventArgs e)
    {
        if (_frostOffered is not { } offer || Packs.Resolve(Editor.Sketch.Code).Pack is not { } pack)
        {
            return;
        }

        SiteValues site = Editor.Sketch.Site with
        {
            FrostDepth = offer.Depth,
            Source = new SiteSource(
                string.Join("; ", new[] { Editor.Sketch.Site.Source?.Text, $"frost depth: {pack.Manifest.Adoption.ShortName}, {pack.Frost!.Source.Location}" }.OfType<string>()),
                DateOnly.FromDateTime(DateTime.Today)),
        };
        Editor.Apply(new SetSite(site), $"Used {pack.Manifest.Adoption.ShortName}'s frost depth");
    }

    /// <summary>Whether the deck block is showing (a deck is selected).</summary>
    public bool IsShowingDeck => DeckFields.IsVisible;

    /// <summary>The deck's headline: its size and height.</summary>
    public string DeckHeadlineText => DeckFields.IsVisible ? DeckHeadline.Text ?? string.Empty : string.Empty;

    /// <summary>The frame in one line, or why there is none.</summary>
    public string DeckFrameLine => DeckFields.IsVisible ? DeckFrameText.Text ?? string.Empty : string.Empty;

    /// <summary>The deck block's text boxes, for the GUI suite.</summary>
    public (TextBox Joist, TextBox Spacing, TextBox Plies, TextBox Beam, TextBox Post, TextBox PostCount, TextBox Cantilever, TextBox Footing, TextBox Decking, TextBox Gap, TextBox Supports, TextBox Species) DeckControls
        => (DeckJoistBox, DeckSpacingBox, DeckPliesBox, DeckBeamBox, DeckPostBox, DeckPostCountBox, DeckCantileverBox, DeckFootingBox, DeckDeckingBox, DeckGapBox, DeckSupportsBox, DeckSpeciesBox);

    TextBox[] DeckTextBoxes =>
    [
        DeckJoistBox, DeckSpacingBox, DeckPliesBox, DeckBeamBox, DeckPostBox, DeckPostCountBox, DeckCantileverBox, DeckFootingBox,
        DeckDeckingBox, DeckGapBox, DeckSupportsBox, DeckSpeciesBox,
    ];

    bool IsDeckField(TextBox box) => Array.IndexOf(DeckTextBoxes, box) >= 0;

    void WireDeck()
    {
        foreach (TextBox box in DeckTextBoxes)
        {
            box.LostFocus += (_, _) =>
            {
                if (!_fillingDeck && DeckFields.IsVisible)
                {
                    ApplyDeck();
                }
            };
        }
    }

    Deck? SelectedDeck() => Editor.OnlySelectedBox is { } box && Deck.Is(Editor.Sketch, box) ? new Deck(box) : null;

    /// <summary>Fills the deck block for a deck, or hides it for anything else.</summary>
    void ShowDeck(Box box)
    {
        bool isDeck = Deck.Is(Editor.Sketch, box);
        DeckFields.IsVisible = isDeck;
        if (!isDeck)
        {
            return;
        }

        Deck deck = new(box);
        string Text(Length length) => length.Format(Editor.LabelFormat).Text;
        DeckHeadline.Text = $"{deck.Name}: {Text(box.Width)} × {Text(box.Height)}, {Text(deck.Height)} above grade.";
        (DeckFraming? framing, DeckRefusal? refusal) = DeckFrame.Of(Editor.Sketch, deck, MaterialsLibrary.Shipped);
        DeckFrameText.Text = framing is not null
            ? $"Frame: {DeckTool.FrameLine(framing)}."
            : refusal!.Text;

        DeckChecks checks = DeckCheck.For(Editor.Sketch.After(), deck, Packs.Resolve(Editor.Sketch.Code).Pack, MaterialsLibrary.Shipped);
        DeckCheckText.Text = string.Join("\n", checks.Lines.Select(line => line.Text));
        DeckSupportsNote.Text = checks.SupportsNote ?? string.Empty;
        DeckSupportsNote.IsVisible = checks.SupportsNote is not null;
        _frostOffered = checks.Frost;
        DeckFrostOfferText.Text = checks.Frost?.Text ?? string.Empty;
        DeckFrostOffer.IsVisible = checks.Frost is not null;

        DeckInputs inputs = box.Deck ?? DeckTool.StartingInputs;
        _fillingDeck = true;
        try
        {
            DeckJoistBox.Text = inputs.Joist;
            DeckSpacingBox.Text = Text(inputs.JoistSpacing);
            DeckPliesBox.Text = inputs.Beam.Plies.ToString(CultureInfo.InvariantCulture);
            DeckBeamBox.Text = inputs.Beam.Lumber;
            DeckPostBox.Text = inputs.Post;
            DeckPostCountBox.Text = inputs.PostCount.ToString(CultureInfo.InvariantCulture);
            DeckCantileverBox.Text = Text(inputs.Cantilever);
            DeckFootingBox.Text = inputs.FootingDepth is { } footing ? Text(footing) : string.Empty;
            DeckDeckingBox.Text = inputs.Decking;
            DeckGapBox.Text = Text(inputs.DeckingGap);
            DeckSupportsBox.Text = inputs.Supports ?? string.Empty;
            DeckSpeciesBox.Text = inputs.Species ?? string.Empty;
            DeckBlockingCheck.IsChecked = inputs.Blocking;
        }
        finally
        {
            _fillingDeck = false;
        }
    }

    void OnDeckTicked(object? sender, RoutedEventArgs e)
    {
        if (!_fillingDeck && DeckFields.IsVisible)
        {
            ApplyDeck();
        }
    }

    /// <summary>
    /// Reads the deck block and sets the selected deck's inputs: one undo step. A value that does not
    /// read is said and nothing changes.
    /// </summary>
    public bool ApplyDeck()
    {
        if (SelectedDeck() is not { } deck)
        {
            return false;
        }

        List<string> problems = [];
        DeckInputs now = deck.Box.Deck ?? DeckTool.StartingInputs;

        Length? Measure(TextBox box, string what, bool zeroAllowed, bool emptyAllowed)
        {
            string text = box.Text?.Trim() ?? string.Empty;
            if (text.Length == 0 && emptyAllowed)
            {
                return null;
            }

            if (Length.TryParse(text, out Length value, out _) && (value > Length.Zero || (zeroAllowed && value == Length.Zero)))
            {
                return value;
            }

            problems.Add($"{what} is a length, like 16\"");
            return null;
        }

        int Whole(TextBox box, string what, int least, int most, int fallback)
        {
            if (int.TryParse(box.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= least && value <= most)
            {
                return value;
            }

            problems.Add($"{what} is a whole number from {least} to {most}");
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

        string? Optional(TextBox box) => string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim();

        DeckInputs next = now with
        {
            Joist = Named(DeckJoistBox, "the joist", now.Joist),
            JoistSpacing = Measure(DeckSpacingBox, "the joist spacing", false, false) ?? now.JoistSpacing,
            Beam = new BeamSpec(Whole(DeckPliesBox, "the beam's plies", 1, 3, now.Beam.Plies), Named(DeckBeamBox, "the beam lumber", now.Beam.Lumber)),
            Post = Named(DeckPostBox, "the post", now.Post),
            PostCount = Whole(DeckPostCountBox, "the post count", 2, 99, now.PostCount),
            Cantilever = Measure(DeckCantileverBox, "the cantilever", true, false) ?? now.Cantilever,
            FootingDepth = Measure(DeckFootingBox, "the footing depth", true, true),
            Decking = Named(DeckDeckingBox, "the decking", now.Decking),
            DeckingGap = Measure(DeckGapBox, "the decking gap", true, false) ?? now.DeckingGap,
            Supports = Optional(DeckSupportsBox),
            Species = Optional(DeckSpeciesBox),
            Blocking = DeckBlockingCheck.IsChecked == true,
        };

        if (problems.Count > 0)
        {
            Editor.Say(EditSeverity.Problem, $"{deck.Name}: {string.Join("; ", problems)}. Nothing was changed.");
            return false;
        }

        if (next == now && deck.Box.Deck is not null)
        {
            return false;
        }

        Editor.Apply(new SetDeckInputs(deck.Id, next), $"Set {deck.Name}'s frame");
        return true;
    }
}

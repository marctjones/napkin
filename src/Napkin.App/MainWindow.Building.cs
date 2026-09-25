using System.Collections.Immutable;

using Avalonia.Controls;
using Avalonia.Interactivity;

using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;
using Napkin.Modules.Furniture;

namespace Napkin.App;

// ---------------------------------------------------------------------------------------
// Walls and openings (#18): drawn in the plan, framed by FramingList, checked by CodeCheck
// ---------------------------------------------------------------------------------------
public partial class MainWindow
{
    IReadOnlyList<string> _packRoots = PackLocations.All();
    CodePacks? _packs;
    ImmutableArray<OpeningCheck> _checksShown = [];
    bool _sayingRecompute;
    bool _fillingSpacing;
    bool _fillingSupports;
    CodeWindow? _codeWindow;

    /// <summary>
    /// Where napkin looks for code packs: the shipped folder beside the executable and the per-user
    /// one (<see cref="PackLocations.All()"/>). A test points it at its own synthetic packs.
    /// </summary>
    public IReadOnlyList<string> PackRoots
    {
        get => _packRoots;
        set
        {
            _packRoots = value ?? [];
            _packs = null;
            ResetRecompute();
            if (_cutList is not null)
            {
                _cutList.Packs = Packs;
            }

            if (_codeWindow is not null)
            {
                _codeWindow.Packs = Packs;
                _codeWindow.PackRoots = _packRoots;
            }
        }
    }

    /// <summary>The code packs found under <see cref="PackRoots"/>, read once.</summary>
    public CodePacks Packs => _packs ??= CodePacks.Discover(_packRoots);

    /// <summary>Every opening's header result for the design on screen, computed now.</summary>
    public ImmutableArray<OpeningCheck> Checks => CodeCheck.Of(Editor.Sketch, Packs);

    /// <summary>How walls are framed: the stud spacing default with the code check's sizes plugged in.</summary>
    public FramingOptions Framing => CodeCheck.Framing(Checks, MaterialsLibrary.Shipped);

    /// <summary>The panel's line naming the selected wall or opening, empty when neither is selected.</summary>
    public string FramingHeadlineText => FramingFields.IsVisible ? FramingHeadline.Text ?? string.Empty : string.Empty;

    /// <summary>The panel's framing summary for the selected wall, or the wall the selected opening is in.</summary>
    public string FramingText => FramingFields.IsVisible ? FramingReadout.Text ?? string.Empty : string.Empty;

    /// <summary>What the panel says could not be framed, empty when nothing.</summary>
    public string FramingProblemsText => FramingProblems.IsVisible ? FramingProblems.Text ?? string.Empty : string.Empty;

    /// <summary>The panel's notes on the framing: the spacing and the placeholder jacks.</summary>
    public string FramingNotesText => FramingFields.IsVisible ? FramingNotes.Text ?? string.Empty : string.Empty;

    /// <summary>The stud spacing picker.</summary>
    public ComboBox StudSpacingControl => StudSpacingBox;

    /// <summary>The picker for what the selected wall supports.</summary>
    public ComboBox SupportsControl => SupportsBox;

    /// <summary>Whether the supports picker is showing (a wall is selected).</summary>
    public bool IsShowingSupports => FramingFields.IsVisible && SupportsRow.IsVisible;

    /// <summary>The code check's headline for the selected opening, empty when no opening is selected.</summary>
    public string CodeCheckText => FramingFields.IsVisible && CodeCheckFields.IsVisible ? CodeCheckHeadline.Text ?? string.Empty : string.Empty;

    /// <summary>The code check's citation line for the selected opening.</summary>
    public string CodeCheckCitationText => FramingFields.IsVisible && CodeCheckFields.IsVisible ? CodeCheckCitation.Text ?? string.Empty : string.Empty;

    /// <summary>The code check's working: band trace, footnotes, source.</summary>
    public string CodeCheckWorkingText => CodeCheckDetails.Text ?? string.Empty;

    /// <summary>The Edit menu's entry for the code and site.</summary>
    public MenuItem CodeMenuEntry => CodeMenuItem;

    /// <summary>The code and site window, when open.</summary>
    public CodeWindow? CodeSite => _codeWindow;

    static readonly Length[] SpacingChoices =
    [
        .. MaterialsLibrary.Shipped.SpacingsFor("Wall").Select(spacing => spacing.Spacing)
            .Append(FramingOptions.DefaultSpacing)
            .Distinct()
            .Order(),
    ];

    static readonly string SpacingTip =
        "Spacings the library carries for walls: "
        + string.Join(", ", MaterialsLibrary.Shipped.SpacingsFor("Wall").Select(spacing => $"{spacing.Name} ({spacing.Source.ShortForm})"))
        + ". Saved with the wall; 16\" is napkin's design default, not a code requirement.";

    static string SpacingText(Length spacing) => $"{spacing.Format(new InchesOnlyFormat(16)).Text} on centre";

    /// <summary>What a table's value for "supports" reads as in the picker: its words, dashes as spaces.</summary>
    public static string SupportsText(string value) => value.Replace('-', ' ');

    /// <summary>Opens the code and site window, or brings the open one forward.</summary>
    public CodeWindow OpenCode()
    {
        if (_codeWindow is null)
        {
            _codeWindow = new CodeWindow
            {
                ApplyRequest = (request, what) => Editor.Apply(request, what),
                Packs = Packs,
                PackRoots = _packRoots,
            };
            _codeWindow.Closed += (_, _) => _codeWindow = null;
        }

        _codeWindow.ShowDesign(CurrentDesign);
        _codeWindow.Show(this);
        _codeWindow.Activate();
        return _codeWindow;
    }

    void OnCodeClicked(object? sender, RoutedEventArgs e) => OpenCode();

    /// <summary>Takes the results now on screen as the ones later changes are measured from.</summary>
    void ResetRecompute() => _checksShown = CodeCheck.Of(Editor.Sketch, Packs);

    /// <summary>
    /// Recomputes every header (total, design §7.3) and, when any result changed since the last
    /// message, says so with that message: "Header for Window 1 changed: (1) 2x8 → (2) 2x10 (Table …)".
    /// </summary>
    /// <returns>Whether it put a new message up (which has then been shown).</returns>
    bool SayRecompute()
    {
        if (_sayingRecompute)
        {
            return false;
        }

        ImmutableArray<OpeningCheck> now = CodeCheck.Of(Editor.Sketch, Packs);
        ImmutableArray<string> changes = CodeCheck.Changes(_checksShown, now);
        _checksShown = now;
        if (changes.IsEmpty)
        {
            return false;
        }

        EditMessage? last = Editor.LastMessage;
        string text = string.Join(" ", (last is null ? [] : new[] { last.Text }).Concat(changes));
        _sayingRecompute = true;
        try
        {
            Editor.Show(last is null
                ? EditMessage.Plain(EditSeverity.Hint, text)
                : last with { Text = text, Severity = last.Severity == EditSeverity.Problem ? EditSeverity.Problem : EditSeverity.Hint });
        }
        finally
        {
            _sayingRecompute = false;
        }

        return true;
    }

    /// <summary>Fills the panel's framing part for a wall or an opening, or hides it for anything else.</summary>
    void ShowFraming(Box box)
    {
        ImmutableArray<OpeningCheck> checks = Checks;
        WallFraming? framing = FramingList.For(Editor.Sketch, box.Id, MaterialsLibrary.Shipped, CodeCheck.Framing(checks, MaterialsLibrary.Shipped));
        FramingFields.IsVisible = framing is not null;
        if (framing is null)
        {
            return;
        }

        string Text(Length length) => length.Format(Editor.LabelFormat).Text;
        Wall wall = framing.Wall;
        OpeningFraming? opening = framing.Openings.FirstOrDefault(candidate => candidate.Opening.Id == box.Id);
        OpeningCheck? check = opening is null ? null : checks.FirstOrDefault(candidate => candidate.Opening.Id == box.Id);

        FramingHeadline.Text = opening is { } o
            ? $"{o.Opening.Name}: a {(o.Opening.Kind == OpeningKind.Door ? "door" : "window")} in {wall.Name}, "
              + $"{Text(o.Opening.Width)} × {Text(o.Opening.Height)} rough opening, sill {Text(o.Opening.Sill)}, "
              + $"{Text(o.Opening.Offset)} along. Header {Text(o.HeaderLength)} long in {Text(o.HeaderRoom)} of room"
              + (check?.Result is HeaderResult.Sized ? "." : ": not yet sized.")
            : $"{wall.Name}: a wall of {framing.Stock?.Name ?? "no library"} studs, {Text(wall.Length)} long, "
              + $"{Text(wall.Thickness)} thick, {Text(wall.Height)} tall.";

        List<string> lines = [$"Framing of {wall.Name}: {framing.Summary}."];
        foreach (OpeningFraming each in framing.Openings)
        {
            lines.Add($"{each.Opening.Name}: {(each.Opening.Kind == OpeningKind.Door ? "door" : "window")}, "
                      + $"{Text(each.Opening.Width)} × {Text(each.Opening.Height)}, sill {Text(each.Opening.Sill)}, {Text(each.Opening.Offset)} along");
        }

        FramingReadout.Text = string.Join("\n", lines);
        FramingProblems.IsVisible = !framing.Problems.IsEmpty && !framing.Pieces.IsEmpty;
        FramingProblems.Text = string.Join("\n", framing.Problems);
        FramingNotes.Text = string.Join("\n", framing.Notes);

        ShowCodeCheck(check);
        ShowSupports(opening is null ? wall : null);

        _fillingSpacing = true;
        try
        {
            StudSpacingBox.ItemsSource = SpacingChoices.Select(SpacingText).ToArray();
            StudSpacingBox.SelectedIndex = Array.IndexOf(SpacingChoices, framing.Spacing);
            ToolTip.SetTip(StudSpacingBox, SpacingTip);
        }
        finally
        {
            _fillingSpacing = false;
        }
    }

    void ShowCodeCheck(OpeningCheck? check)
    {
        CodeCheckFields.IsVisible = check is not null;
        if (check is null)
        {
            return;
        }

        CheckWords words = CodeCheck.Words(check.Result, MaterialsLibrary.Shipped);
        CodeCheckHeadline.Text = words.Headline;
        CodeCheckCitation.Text = words.Citation;
        CodeCheckCitation.IsVisible = words.Citation.Length > 0;
        CodeCheckDetails.Text = words.Details;
        CodeCheckWorking.IsVisible = words.Details.Length > 0;
        ToolTip.SetTip(CodeCheckCitation, words.Details.Length > 0 ? words.Details : null);
    }

    /// <summary>The supports picker for a selected wall: the adopted code's table values, or disabled with why.</summary>
    void ShowSupports(Wall? wall)
    {
        SupportsRow.IsVisible = wall is not null;
        if (wall is null)
        {
            return;
        }

        CodeResolution code = Packs.Resolve(Editor.Sketch.Code);
        ImmutableArray<string> values = CodeCheck.SupportsChoices(code.Pack);
        string? chosen = wall.Box.WallInputs?.Supports;
        List<string> items = ["not chosen", .. values.Select(SupportsText)];
        if (chosen is not null && !values.Contains(chosen))
        {
            // A value the current code's table does not declare (another pack's) is shown as it is, not dropped.
            items.Add($"{SupportsText(chosen)} (not in this code's table)");
        }

        _fillingSupports = true;
        try
        {
            SupportsBox.ItemsSource = items.ToArray();
            SupportsBox.SelectedIndex = chosen is null ? 0 : values.Contains(chosen) ? values.IndexOf(chosen) + 1 : items.Count - 1;
            SupportsBox.IsEnabled = !values.IsEmpty;
            ToolTip.SetTip(
                SupportsBox,
                values.IsEmpty
                    ? code.Pack is null
                        ? $"No code selected: choose one under {CodeCheck.WhereToChoose} first."
                        : $"{code.Pack.Code.ShortName} has no header table loaded, so there is nothing to choose from yet (docs/rules-engine.md)."
                    : "What this wall carries, as the adopted code's header table names it. napkin never assumes it.");
        }
        finally
        {
            _fillingSupports = false;
        }
    }

    void OnSupportsChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_fillingSupports || SupportsBox.SelectedIndex < 0)
        {
            return;
        }

        ImmutableArray<string> values = CodeCheck.SupportsChoices(Packs.Resolve(Editor.Sketch.Code).Pack);
        int index = SupportsBox.SelectedIndex;
        if (index == 0)
        {
            SetWallSupports(null);
        }
        else if (index - 1 < values.Length)
        {
            SetWallSupports(values[index - 1]);
        }
    }

    /// <summary>The wall selected, or the wall the selected opening is in.</summary>
    Wall? SelectedWall()
        => Editor.OnlySelectedBox is not { } box ? null
            : Wall.Is(Editor.Sketch, box) ? new Wall(box)
            : Opening.Find(Editor.Sketch, box.Id)?.Wall;

    /// <summary>Says what the selected wall supports (null for "not chosen"); one undo step.</summary>
    public void SetWallSupports(string? supports)
    {
        if (SelectedWall() is not { } wall || wall.Box.WallInputs?.Supports == supports)
        {
            return;
        }

        WallInputs inputs = (wall.Box.WallInputs ?? new WallInputs(null, null)) with { Supports = supports };
        Editor.Apply(
            new SetWallInputs(wall.Id, inputs),
            supports is null ? $"Cleared what {wall.Name} supports" : $"Set {wall.Name} to support {SupportsText(supports)}");
    }

    void OnStudSpacingChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_fillingSpacing || StudSpacingBox.SelectedIndex < 0 || StudSpacingBox.SelectedIndex >= SpacingChoices.Length)
        {
            return;
        }

        SetStudSpacing(SpacingChoices[StudSpacingBox.SelectedIndex]);
    }

    /// <summary>Frames the selected wall (or the selected opening's) at this spacing; saved with it, one undo step.</summary>
    public void SetStudSpacing(Length spacing)
    {
        if (SelectedWall() is not { } wall)
        {
            return;
        }

        Length now = wall.Box.WallInputs?.StudSpacing ?? FramingOptions.DefaultSpacing;
        if (now == spacing && wall.Box.WallInputs?.StudSpacing is not null)
        {
            return;
        }

        WallInputs inputs = (wall.Box.WallInputs ?? new WallInputs(null, null)) with { StudSpacing = spacing };
        Editor.Apply(new SetWallInputs(wall.Id, inputs), $"Set {wall.Name}'s studs at {SpacingText(spacing)}");
    }
}

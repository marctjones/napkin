using System.Collections.Immutable;

using Avalonia.Controls;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;

namespace Napkin.App;

/// <summary>
/// What a wall is (docs/design/renovation-sketches.md §4.3): its side, whether it bears, and — for a
/// wall that does not — the header the person chose for its openings. Entered, never defaulted.
/// </summary>
public partial class MainWindow
{
    static readonly string[] SideWords = ["not said", "exterior", "interior"];
    static readonly string[] BearingWords = ["not said", "bearing", "not bearing"];

    bool _fillingWallType;

    /// <summary>The Side picker, for the GUI suite.</summary>
    public ComboBox SideControl => SideBox;

    /// <summary>The Bearing picker, for the GUI suite.</summary>
    public ComboBox BearingControl => BearingBox;

    /// <summary>The Header picker, shown for a wall that is not bearing, for the GUI suite.</summary>
    public ComboBox HeaderControl => HeaderBox;

    /// <summary>Whether the Header picker is showing.</summary>
    public bool IsShowingHeaderChoice => FramingFields.IsVisible && HeaderRow.IsVisible;

    /// <summary>The wall's note: a demolished bearing wall's warning, or what the typed header is.</summary>
    public string WallNoteText => WallNote.IsVisible ? WallNote.Text ?? string.Empty : string.Empty;

    /// <summary>The headers a person may choose: one to three plies of each dimension 2x lumber in the library.</summary>
    static ImmutableArray<TypedHeader> HeaderChoices { get; } =
    [
        .. MaterialsLibrary.Shipped.Items
            .OfType<LumberStock>()
            .Where(lumber => lumber.SizeClass == SizeClass.Dimension && lumber.NominalThickness == Length.Inches(2) && !lumber.StandardLengths.IsEmpty)
            .OrderBy(lumber => lumber.Width)
            .SelectMany(lumber => Enumerable.Range(1, 3).Select(plies => new TypedHeader(plies, lumber.Name))),
    ];

    /// <summary>The Side and Bearing pickers for a selected wall, the Header picker when it is not bearing, and its note.</summary>
    void ShowWallType(Wall? wall)
    {
        WallTypeRow.IsVisible = wall is not null;
        WallInputs? inputs = wall?.Box.WallInputs;
        HeaderRow.IsVisible = wall is not null && inputs?.Bearing == false;
        string? note = wall is null ? null
            : wall.Box.Phase == Phase.Demolish && inputs?.Bearing == true ? $"{wall.Name} is bearing and marked demolish. {CodeCheck.BearingDemolishedText}"
            : inputs?.Bearing == false ? CodeCheck.NotBearingText(wall) + (inputs.Header is null ? string.Empty : " Not a code result.")
            : null;
        WallNote.Text = note ?? string.Empty;
        WallNote.IsVisible = note is not null;
        if (wall is null)
        {
            return;
        }

        _fillingWallType = true;
        try
        {
            SideBox.ItemsSource ??= SideWords;
            BearingBox.ItemsSource ??= BearingWords;
            SideBox.SelectedIndex = inputs?.Side switch { WallSide.Exterior => 1, WallSide.Interior => 2, _ => 0 };
            BearingBox.SelectedIndex = inputs?.Bearing switch { true => 1, false => 2, _ => 0 };
            HeaderBox.ItemsSource ??= new[] { "none chosen" }.Concat(HeaderChoices.Select(header => header.ToString())).ToArray();
            HeaderBox.SelectedIndex = inputs?.Header is { } typed && HeaderChoices.IndexOf(typed) is >= 0 and var at ? at + 1 : 0;
        }
        finally
        {
            _fillingWallType = false;
        }
    }

    void OnSideChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_fillingWallType && SideBox.SelectedIndex >= 0)
        {
            SetWallSide(SideBox.SelectedIndex switch { 1 => WallSide.Exterior, 2 => WallSide.Interior, _ => null });
        }
    }

    void OnBearingChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_fillingWallType && BearingBox.SelectedIndex >= 0)
        {
            SetWallBearing(BearingBox.SelectedIndex switch { 1 => true, 2 => false, _ => null });
        }
    }

    void OnHeaderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_fillingWallType && HeaderBox.SelectedIndex >= 0)
        {
            SetWallHeader(HeaderBox.SelectedIndex == 0 ? null : HeaderChoices[HeaderBox.SelectedIndex - 1]);
        }
    }

    /// <summary>Says which side the selected wall is on (null: not said); one undo step.</summary>
    public void SetWallSide(WallSide? side)
    {
        if (SelectedWall() is not { } wall || wall.Box.WallInputs?.Side == side)
        {
            return;
        }

        WallInputs inputs = (wall.Box.WallInputs ?? new WallInputs(null, null)) with { Side = side };
        Editor.Apply(new SetWallInputs(wall.Id, inputs), side is null ? $"Cleared which side {wall.Name} is on" : $"Set {wall.Name} {(side == WallSide.Exterior ? "exterior" : "interior")}");
    }

    /// <summary>Says whether the selected wall is bearing (null: not said); one undo step.</summary>
    public void SetWallBearing(bool? bearing)
    {
        if (SelectedWall() is not { } wall || wall.Box.WallInputs?.Bearing == bearing)
        {
            return;
        }

        WallInputs inputs = (wall.Box.WallInputs ?? new WallInputs(null, null)) with { Bearing = bearing };
        Editor.Apply(
            new SetWallInputs(wall.Id, inputs),
            bearing switch { true => $"Marked {wall.Name} bearing", false => $"Marked {wall.Name} not bearing", _ => $"Cleared whether {wall.Name} is bearing" });
    }

    /// <summary>Chooses the header over every opening in the selected wall (null: none); one undo step.</summary>
    public void SetWallHeader(TypedHeader? header)
    {
        if (SelectedWall() is not { } wall || wall.Box.WallInputs?.Header == header)
        {
            return;
        }

        WallInputs inputs = (wall.Box.WallInputs ?? new WallInputs(null, null)) with { Header = header };
        Editor.Apply(
            new SetWallInputs(wall.Id, inputs),
            header is null ? $"Cleared the header chosen for {wall.Name}" : $"Chose a {header} header for {wall.Name}'s openings: your choice, not a code result");
    }
}

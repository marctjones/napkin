using System.Collections.Immutable;
using System.Globalization;

using Avalonia.Controls;
using Avalonia.Interactivity;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// A room's block in the Part panel (docs/design/renovation-sketches.md §5, §8): the finishes
/// ticked, the values typed from the packages, what was measured, and the takeoff it gives. Every
/// change is one undo step. Nothing is defaulted but napkin's own flooring allowance.
/// </summary>
public partial class MainWindow
{
    static readonly (RoomSurfaces Value, string Word)[] SurfaceWords = [(RoomSurfaces.None, "none"), (RoomSurfaces.Walls, "walls"), (RoomSurfaces.WallsAndCeiling, "walls and ceiling")];
    static readonly (InsulatedWalls Value, string Word)[] InsulatedWords = [(InsulatedWalls.None, "none"), (InsulatedWalls.Exterior, "exterior walls"), (InsulatedWalls.All, "all walls")];
    static readonly (InsulationBy Value, string Word)[] ByWords = [(InsulationBy.Area, "by area"), (InsulationBy.Bays, "by stud bays")];

    bool _fillingRoom;

    /// <summary>Whether the room block is showing (a room is selected).</summary>
    public bool IsShowingRoom => RoomFields.IsVisible;

    /// <summary>The room's headline: its inside size and ceiling.</summary>
    public string RoomHeadlineText => RoomFields.IsVisible ? RoomHeadline.Text ?? string.Empty : string.Empty;

    /// <summary>Which walls bound the room, or why none do.</summary>
    public string RoomBoundsLine => RoomFields.IsVisible ? RoomBoundsText.Text ?? string.Empty : string.Empty;

    /// <summary>What the measurements say, and what napkin cannot read.</summary>
    public string RoomNotesLine => RoomFields.IsVisible && RoomNotesText.IsVisible ? RoomNotesText.Text ?? string.Empty : string.Empty;

    /// <summary>The room's takeoff lines as the panel shows them.</summary>
    public string RoomTakeoffLines => RoomFields.IsVisible ? RoomTakeoffText.Text ?? string.Empty : string.Empty;

    /// <summary>The room block's controls, for the GUI suite.</summary>
    public (ComboBox Drywall, TextBox Sheet, ComboBox Insulation, ComboBox InsulationBy, TextBox InsulationCoverage, ComboBox Paint, TextBox Coats, TextBox PaintCoverage, CheckBox Flooring, TextBox Waste, TextBox Box, CheckBox Baseboard, TextBox Stick) RoomControls
        => (DrywallBox, SheetBox, InsulationBox, InsulationByBox, InsulationCoverageBox, PaintBox, PaintCoatsBox, PaintCoverageBox, FlooringCheck, FlooringWasteBox, FlooringBoxBox, BaseboardCheck, BaseboardStickBox);

    TextBox[] RoomTextBoxes =>
    [
        SheetBox, InsulationCoverageBox, PaintCoatsBox, PaintCoverageBox, FlooringWasteBox, FlooringBoxBox, BaseboardStickBox,
        MeasuredSouthBox, MeasuredNorthBox, MeasuredWestBox, MeasuredEastBox, MeasuredDiagonal1Box, MeasuredDiagonal2Box,
    ];

    bool IsRoomField(TextBox box) => Array.IndexOf(RoomTextBoxes, box) >= 0;

    void WireRoom()
    {
        DrywallBox.ItemsSource = SurfaceWords.Select(pair => pair.Word).ToArray();
        PaintBox.ItemsSource = SurfaceWords.Select(pair => pair.Word).ToArray();
        InsulationBox.ItemsSource = InsulatedWords.Select(pair => pair.Word).ToArray();
        InsulationByBox.ItemsSource = ByWords.Select(pair => pair.Word).ToArray();
        foreach (TextBox box in RoomTextBoxes)
        {
            box.LostFocus += (_, _) =>
            {
                if (!_fillingRoom && RoomFields.IsVisible)
                {
                    ApplyRoom();
                }
            };
        }
    }

    Room? SelectedRoom() => Editor.OnlySelectedBox is { } box && Room.Is(Editor.Sketch, box) ? new Room(box) : null;

    /// <summary>Fills the room block for a room, or hides it for anything else.</summary>
    void ShowRoom(Box box)
    {
        bool isRoom = Room.Is(Editor.Sketch, box);
        RoomFields.IsVisible = isRoom;
        if (!isRoom)
        {
            return;
        }

        Room room = new(box);
        RoomInputs inputs = box.Room ?? RoomInputs.None;
        string Text(Length length) => length.Format(Editor.LabelFormat).Text;
        string Optional(Length? length) => length is { } value ? Text(value) : string.Empty;

        RoomHeadline.Text = $"{room.Name}: {Text(room.Length)} × {Text(room.Width)} inside, ceiling {Text(room.Height)}"
                            + (box.Phase == Phase.New ? "." : $" ({PhaseCommand.Word(box.Phase)}: not taken off).");
        Sketch after = Editor.Sketch.After();
        ImmutableArray<BoundingWall> bounding = RoomBounds.Of(after, room);
        RoomBoundsText.Text = bounding.IsEmpty
            ? "No walls bound this room; openings are not subtracted."
            : $"Bounded by {string.Join(", ", bounding.Select(b => b.Wall.Name).Distinct())}; openings: "
              + (RoomBounds.Openings(after, room) is { IsEmpty: false } openings ? string.Join(", ", openings.Select(o => o.Name)) : "none") + ".";

        List<string> notes = [.. OutOfSquare.Of(room), .. RoomBounds.NearMisses(after, room)];
        RoomNotesText.Text = string.Join("\n", notes.Select(note => char.ToUpperInvariant(note[0]) + note[1..] + "."));
        RoomNotesText.IsVisible = notes.Count > 0;
        RoomTakeoffText.Text = box.Phase == Phase.New
            ? string.Join("\n", AreaTakeoff.Of(Editor.Sketch, room, MaterialsLibrary.Shipped, Framing).Select(line => line.Text))
            : string.Empty;

        _fillingRoom = true;
        try
        {
            DrywallBox.SelectedIndex = Array.FindIndex(SurfaceWords, pair => pair.Value == inputs.Drywall);
            PaintBox.SelectedIndex = Array.FindIndex(SurfaceWords, pair => pair.Value == inputs.Paint);
            InsulationBox.SelectedIndex = Array.FindIndex(InsulatedWords, pair => pair.Value == inputs.Insulation);
            InsulationByBox.SelectedIndex = Array.FindIndex(ByWords, pair => pair.Value == inputs.InsulationBy);
            SheetBox.Text = inputs.Sheet is { } sheet ? $"{Text(sheet.Width)} x {Text(sheet.Length)}" : string.Empty;
            InsulationCoverageBox.Text = inputs.InsulationCoverage?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            PaintCoatsBox.Text = inputs.PaintCoats?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            PaintCoverageBox.Text = inputs.PaintCoverage?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            FlooringCheck.IsChecked = inputs.Flooring;
            FlooringWasteBox.Text = inputs.FlooringWaste.ToString(CultureInfo.InvariantCulture);
            FlooringBoxBox.Text = inputs.FlooringBox?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            BaseboardCheck.IsChecked = inputs.Baseboard;
            BaseboardStickBox.Text = Optional(inputs.BaseboardStick);
            MeasuredSouthBox.Text = Optional(inputs.Measured.South);
            MeasuredNorthBox.Text = Optional(inputs.Measured.North);
            MeasuredWestBox.Text = Optional(inputs.Measured.West);
            MeasuredEastBox.Text = Optional(inputs.Measured.East);
            MeasuredDiagonal1Box.Text = Optional(inputs.Measured.Diagonal1);
            MeasuredDiagonal2Box.Text = Optional(inputs.Measured.Diagonal2);
        }
        finally
        {
            _fillingRoom = false;
        }
    }

    void OnRoomChoiceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_fillingRoom && RoomFields.IsVisible)
        {
            ApplyRoom();
        }
    }

    void OnRoomTicked(object? sender, RoutedEventArgs e)
    {
        if (!_fillingRoom && RoomFields.IsVisible)
        {
            ApplyRoom();
        }
    }

    /// <summary>
    /// Reads the room block and sets the selected room's inputs: one undo step. A value that does
    /// not read is said and nothing changes.
    /// </summary>
    public bool ApplyRoom()
    {
        if (SelectedRoom() is not { } room)
        {
            return false;
        }

        List<string> problems = [];
        SheetSize? sheet = null;
        string sheetText = SheetBox.Text?.Trim() ?? string.Empty;
        if (sheetText.Length > 0)
        {
            string[] sides = sheetText.Split(['x', 'X', '×'], 2, StringSplitOptions.TrimEntries);
            if (sides.Length == 2 && Length.TryParse(sides[0], out Length w, out _) && Length.TryParse(sides[1], out Length l, out _) && w > Length.Zero && l > Length.Zero)
            {
                sheet = new SheetSize(w, l);
            }
            else
            {
                problems.Add("the sheet size is width x length, like 4' x 8'");
            }
        }

        int? Whole(TextBox box, string what, int least)
        {
            string text = box.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= least)
            {
                return value;
            }

            problems.Add($"{what} is a whole number{(least > 0 ? $" of at least {least}" : string.Empty)}");
            return null;
        }

        Length? Measure(TextBox box, string what)
        {
            string text = box.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                return null;
            }

            if (Length.TryParse(text, out Length value, out _) && value > Length.Zero)
            {
                return value;
            }

            problems.Add($"{what} is a length, like 8'");
            return null;
        }

        RoomInputs now = room.Box.Room ?? RoomInputs.None;
        RoomInputs next = now with
        {
            Drywall = DrywallBox.SelectedIndex >= 0 ? SurfaceWords[DrywallBox.SelectedIndex].Value : now.Drywall,
            Sheet = sheet,
            Insulation = InsulationBox.SelectedIndex >= 0 ? InsulatedWords[InsulationBox.SelectedIndex].Value : now.Insulation,
            InsulationBy = InsulationByBox.SelectedIndex >= 0 ? ByWords[InsulationByBox.SelectedIndex].Value : now.InsulationBy,
            InsulationCoverage = Whole(InsulationCoverageBox, "a bag's coverage", 1),
            Paint = PaintBox.SelectedIndex >= 0 ? SurfaceWords[PaintBox.SelectedIndex].Value : now.Paint,
            PaintCoats = Whole(PaintCoatsBox, "the number of coats", 1),
            PaintCoverage = Whole(PaintCoverageBox, "a gallon's coverage", 1),
            Flooring = FlooringCheck.IsChecked == true,
            FlooringWaste = Whole(FlooringWasteBox, "the waste allowance", 0) ?? now.FlooringWaste,
            FlooringBox = Whole(FlooringBoxBox, "a box's coverage", 1),
            Baseboard = BaseboardCheck.IsChecked == true,
            BaseboardStick = Measure(BaseboardStickBox, "the stick"),
            Measured = new MeasuredRoom(
                Measure(MeasuredSouthBox, "the south wall"),
                Measure(MeasuredNorthBox, "the north wall"),
                Measure(MeasuredEastBox, "the east wall"),
                Measure(MeasuredWestBox, "the west wall"),
                Measure(MeasuredDiagonal1Box, "a diagonal"),
                Measure(MeasuredDiagonal2Box, "a diagonal")),
        };

        if (problems.Count > 0)
        {
            Editor.Say(EditSeverity.Problem, $"{room.Name}: {string.Join("; ", problems)}. Nothing was changed.");
            return false;
        }

        if (next == now && room.Box.Room is not null)
        {
            return false;
        }

        Editor.Apply(new SetRoomInputs(room.Id, next), $"Set {room.Name}'s finishes");
        return true;
    }
}

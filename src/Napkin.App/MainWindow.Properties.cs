using System.Collections.Immutable;
using System.Globalization;
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
    // The properties panel: what the selected part is, as opposed to where it is
    // ---------------------------------------------------------------------------------------

    /// <summary>The properties panel.</summary>
    public Border Properties => PropertiesPanel;

    /// <summary>The part of the side column the Part panel is shown in, scrolling when it is taller.</summary>
    public ScrollViewer PropertiesArea => PropertiesScroller;

    /// <summary>Whether the properties panel is on screen.</summary>
    public bool IsShowingProperties => PropertiesPanel.IsVisible;

    /// <summary>The field the selected part's name is typed into.</summary>
    public TextBox PartNameField => PartNameBox;

    /// <summary>The box that says whether the selection is a piece to cut.</summary>
    public CheckBox IsPartField => IsPartCheck;

    /// <summary>
    /// The field the box's depth — for a part, its out-of-plane dimension — is typed into. Every box
    /// has one (docs/design/assembly-model.md §1.2), so it is shown for a plain box too.
    /// </summary>
    public TextBox OutOfPlaneField => OutOfPlaneBox;

    /// <summary>The Part panel's three position fields: east, north and up (#78).</summary>
    public (TextBox East, TextBox North, TextBox Up) PositionFields => (PositionXBox, PositionYBox, PositionZBox);

    /// <summary>What the Part panel calls its three sizes now: the two part rows, then the depth field.</summary>
    public (string First, string Second, string Depth) PartRowCaptions =>
        (PlanXCaption.Text ?? string.Empty, PlanYCaption.Text ?? string.Empty, OutOfPlaneCaption.Text ?? string.Empty);

    /// <summary>The field the quantity is typed into.</summary>
    public TextBox QuantityField => QuantityBox;

    /// <summary>The field a stock name is typed into.</summary>
    public TextBox StockField => StockBox;

    /// <summary>The field a species is typed into.</summary>
    public TextBox SpeciesField => SpeciesBox;

    /// <summary>The Rough tick-box under the stock field (docs/design/sketch-mode.md &#xA7;4.2).</summary>
    public CheckBox RoughField => RoughCheck;

    /// <summary>Which of the three dimensions the box's width is.</summary>
    public ComboBox PlanAcross => PlanXBox;

    /// <summary>Which of the three dimensions the box's height is.</summary>
    public ComboBox PlanUp => PlanYBox;

    /// <summary>The button that puts the panel's contents on the part.</summary>
    public Button ApplyPart => ApplyPartButton;

    /// <summary>What the stock line says: the library's own hover text, or why it did not resolve.</summary>
    public string StockReadoutText => StockReadout.Text ?? string.Empty;

    /// <summary>What the panel is complaining about, or empty when it is not.</summary>
    public string PropertiesErrorText => PropertiesError.IsVisible ? PropertiesError.Text ?? string.Empty : string.Empty;

    /// <summary>
    /// Fills the properties panel from the selection, or hides it when there is nothing to fill it
    /// from.
    /// </summary>
    void ShowProperties()
    {
        // While the shape workshop is open the canvas's floating chrome is off the screen, this
        // panel with it: the workshop is a mode with its own panel, and two panels stacked on the
        // same corner of the window would have one of them taking the other's clicks.
        if (IsShapingPart || Editor.OnlySelectedBox is not { } box)
        {
            PropertiesPanel.IsVisible = false;
            _propertiesShown = null;
            WorkshopSheet.ShowCut();
            return;
        }

        _showingProperties = true;
        try
        {
            PropertiesPanel.IsVisible = true;
            PropertiesError.IsVisible = false;

            PartNameBox.Text = box.Name;
            FillPartFields(box);
            OutOfPlaneBox.Text = box.Depth.Format(Editor.LabelFormat).Text;
            FillPosition(box);

            UpdateOutOfPlaneCaption();
            UpdateStockReadout();
            ShowFraming(box);
            ShowRoom(box);
        }
        finally
        {
            _showingProperties = false;
        }

        _propertiesShown = box;
        WorkshopSheet.ShowCut();
    }

    /// <summary>
    /// Keeps the panel in step with the design while the same part stays selected (#69).
    /// </summary>
    /// <remarks>
    /// A drag in the 3D view, an undo or a grip on a turned part can change what the panel shows
    /// without changing the selection, and a panel still showing the old depth would put it back
    /// — pinned by a typed size — the next time Apply is pressed. Only the fields whose value in
    /// the design changed are refilled, so a name half-typed in the panel survives a move that did
    /// not touch the name.
    /// </remarks>
    void FollowDesignInProperties()
    {
        if (IsShapingPart
            || _propertiesShown is not { } shown
            || Editor.OnlySelectedBox is not { } box
            || box.Id != shown.Id
            || box == shown)
        {
            return;
        }

        _showingProperties = true;
        try
        {
            if (box.Name != shown.Name)
            {
                PartNameBox.Text = box.Name;
            }

            if (box.Part != shown.Part)
            {
                FillPartFields(box);
            }

            if (box.Depth != shown.Depth)
            {
                OutOfPlaneBox.Text = box.Depth.Format(Editor.LabelFormat).Text;
            }

            if (SpaceSnapResolver.Extent(box).Low != SpaceSnapResolver.Extent(shown).Low)
            {
                FillPosition(box);
            }

            UpdateOutOfPlaneCaption();
            UpdateStockReadout();
            ShowRoom(box);
        }
        finally
        {
            _showingProperties = false;
        }

        _propertiesShown = box;
    }

    /// <summary>Where the box is: the low corner of its extent, in the world's terms (#78).</summary>
    void FillPosition(Box box)
    {
        Point3 low = SpaceSnapResolver.Extent(box).Low;
        PositionXBox.Text = low.X.Format(Editor.LabelFormat).Text;
        PositionYBox.Text = low.Y.Format(Editor.LabelFormat).Text;
        PositionZBox.Text = low.Z.Format(Editor.LabelFormat).Text;
    }

    /// <summary>What kind of part the box is: the check box and everything under it.</summary>
    void FillPartFields(Box box)
    {
        IsPartCheck.IsChecked = box.Part is not null;
        PartFields.IsVisible = box.Part is not null;

        FillDimensionChoices();

        Part part = box.Part ?? DefaultPart;
        PlanXBox.SelectedItem = SceneWords.Of(part.PlanAxes.X);
        PlanYBox.SelectedItem = SceneWords.Of(part.PlanAxes.Y);
        QuantityBox.Text = part.Quantity.ToString(CultureInfo.InvariantCulture);
        StockBox.Text = part.Stock ?? string.Empty;
        SpeciesBox.Text = part.Species ?? string.Empty;
        RoughCheck.IsChecked = part.Rough;
        GrainBox.SelectedIndex = part.Grain is { } grain ? 1 + (int)grain : 0;
        ShowFaceBox.SelectedIndex = part.ShowFace is { } shows ? 1 + Array.IndexOf(ShowFaceChoices, shows) : 0;
        HardwareBox.Text = string.Join("\n", part.Hardware.Select(item => $"{item.Name} \u00d7 {item.Quantity}"));
    }

    /// <summary>The hardware field, for the GUI suite to type into.</summary>
    public TextBox HardwareField => HardwareBox;

    static readonly BoxFace[] ShowFaceChoices = [BoxFace.Top, BoxFace.Bottom, BoxFace.North, BoxFace.South, BoxFace.East, BoxFace.West];

    /// <summary>The grain and show-face pickers, for the GUI suite.</summary>
    public (ComboBox Grain, ComboBox ShowFace) GrainFields => (GrainBox, ShowFaceBox);

    void FillDimensionChoices()
    {
        if (PlanXBox.ItemsSource is not null)
        {
            return;
        }

        GrainBox.ItemsSource = new[] { "unsaid", "length", "width", "thickness" };
        ShowFaceBox.ItemsSource = new[] { "unsaid" }.Concat(ShowFaceChoices.Select(face => face.ToString().ToLowerInvariant())).ToArray();

        PlanXBox.ItemsSource = SceneWords.Dimensions;
        PlanYBox.ItemsSource = SceneWords.Dimensions;
        PlanXBox.SelectionChanged += (_, _) => UpdateOutOfPlaneCaption();
        PlanYBox.SelectionChanged += (_, _) => UpdateOutOfPlaneCaption();
        StockBox.TextChanged += (_, _) => UpdateStockReadout();
    }

    /// <summary>
    /// The depth field is labelled with the dimension it actually is — for a part, the one the
    /// plan's two axes leave, and for a plain box simply its depth — and which way it runs now; and
    /// each of the two part rows with the way its dimension runs now (#81). "Across" and "Up" used
    /// to name the plan's screen directions, so a standing leg read "Up: Thickness" while its
    /// length was what pointed up.
    /// </summary>
    void UpdateOutOfPlaneCaption()
    {
        Box? box = Editor.OnlySelectedBox;
        string name = IsPartCheck.IsChecked != true
            ? "Depth"
            : ChosenAxes() is { } axes
                ? SceneWords.Of(axes.OutOfPlane)
                : "Third";

        if (box is not { Orientation.IsExact: true })
        {
            OutOfPlaneCaption.Text = name;
            PlanXCaption.Text = "Across";
            PlanYCaption.Text = "Up";
            return;
        }

        OutOfPlaneCaption.Text = $"{name} ({WorldWords.Along(box.Orientation.Image(Axis.Z).Axis).ToLowerInvariant()})";
        PlanXCaption.Text = WorldWords.Along(box.Orientation.Image(Axis.X).Axis);
        PlanYCaption.Text = WorldWords.Along(box.Orientation.Image(Axis.Y).Axis);
    }

    /// <summary>
    /// What the library says about the stock that was typed — the same hover line the picker
    /// shows — or that it does not carry that name, which is not an error.
    /// </summary>
    void UpdateStockReadout() => StockReadout.Text = StockAssignment.Readout(StockBox.Text, MaterialsLibrary.Shipped);

    PlanAxes? ChosenAxes()
    {
        if (!SceneWords.TryDimension(PlanXBox.SelectedItem as string, out PartDimension x)
            || !SceneWords.TryDimension(PlanYBox.SelectedItem as string, out PartDimension y)
            || x == y)
        {
            return null;
        }

        return new PlanAxes(x, y);
    }

    void OnIsPartChanged(object? sender, RoutedEventArgs e)
    {
        if (_showingProperties)
        {
            return;
        }

        PartFields.IsVisible = IsPartCheck.IsChecked == true;
        UpdateOutOfPlaneCaption();
    }

    void OnApplyPartClicked(object? sender, RoutedEventArgs e) => ApplyProperties();

    /// <summary>
    /// Puts what the panel says onto the selected box: its name, and what kind of part it is.
    /// </summary>
    /// <remarks>
    /// Both go through the editor as requests, so the canvas, the relationship list and the cut
    /// list all hear about them the same way they hear about a drag. Nothing is half-applied: the
    /// panel checks every field before it sends anything.
    /// </remarks>
    /// <returns>Whether the panel's contents were applied.</returns>
    public bool ApplyProperties()
    {
        if (Editor.OnlySelectedBox is not { } box)
        {
            return false;
        }

        Part? part = null;
        PlanAxes? chosen = null;
        if (IsPartCheck.IsChecked == true)
        {
            if (ChosenAxes() is not { } axes)
            {
                return Complain("A part's two plan dimensions have to be different ones.");
            }

            chosen = axes;
        }

        if (!Length.TryParse(OutOfPlaneBox.Text, out Length depth, out _) || depth <= Length.Zero)
        {
            return Complain(
                $"{(chosen is { } named ? SceneWords.Of(named.OutOfPlane) : "Depth")} has to be a length greater than zero, "
                + "like 3/4\" or 1' 4 1/4\".");
        }

        if (chosen is { } planAxes)
        {
            if (!int.TryParse(
                    QuantityBox.Text,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int quantity)
                || quantity < 1)
            {
                return Complain("A part stands for at least one piece.");
            }

            if (!HardwareEntry.TryRead(HardwareBox.Text, out ImmutableList<HardwareItem> hardware, out string problem))
            {
                return Complain(problem);
            }

            part = new Part(
                Blank(StockBox.Text),
                Blank(SpeciesBox.Text),
                quantity,
                planAxes)
            {
                Hardware = hardware,
                Rough = RoughCheck.IsChecked == true,
                Grain = GrainBox.SelectedIndex > 0 ? (PartDimension)(GrainBox.SelectedIndex - 1) : null,
                ShowFace = ShowFaceBox.SelectedIndex > 0 ? ShowFaceChoices[ShowFaceBox.SelectedIndex - 1] : null,
            };
        }

        Point3 low = SpaceSnapResolver.Extent(box).Low;
        if (!TryPosition(PositionXBox, low.X, out Length x)
            || !TryPosition(PositionYBox, low.Y, out Length y)
            || !TryPosition(PositionZBox, low.Z, out Length z))
        {
            return Complain(
                "Where it is has to be three lengths, like 1' 4\" — negative for west of or south of the "
                + "origin, or below the floor.");
        }

        PropertiesError.IsVisible = false;

        string name = PartNameBox.Text ?? string.Empty;
        Request? typedDepth = DepthRequest(box, part, depth);

        // Typing the depth of a rough part firms it, as typing either plan size does (sketch-mode §3.1).
        if (typedDepth is not null && part is { Rough: true } && box.Part is { Rough: true })
        {
            part = part with { Rough = false };
        }

        List<Request> requests = [new SetName(box.Id, name), Assignment(box, part)];
        if (typedDepth is not null)
        {
            requests.Add(typedDepth);
        }

        // A typed place is exact: the anchor moves by what the corner was asked to, through the
        // updater, so what is held to the part follows and a conflict is explained (#78).
        Point3 typedLow = new(x, y, z);
        if (typedLow != low)
        {
            requests.Add(new SetPosition(box.Id, box.Anchor + (typedLow - low)));
        }

        Editor.Apply(
            Batch.Of([.. requests]),
            UndoName(box, part, name, requests.Count));

        ShowProperties();
        return true;

        static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// What the undo step is called: "mark rough" or "mark firm" when the Rough tick is the only
    /// thing the panel changed (docs/design/sketch-mode.md &#xA7;4.2), otherwise what it always was.
    /// </summary>
    static string UndoName(Box box, Part? part, string name, int requestCount)
    {
        if (part is null)
        {
            return "make it a plain box";
        }

        bool onlyRoughChanged = requestCount == 2
            && name == box.Name
            && box.Part is { } before
            && before.Rough != part.Rough
            && before with { Rough = part.Rough } == part;
        return onlyRoughChanged ? (part.Rough ? "mark rough" : "mark firm") : "set what this part is";
    }

    /// <summary>
    /// A position field's length, or where the part already is when the field is empty: an empty
    /// field is "leave it", not zero.
    /// </summary>
    static bool TryPosition(TextBox field, Length current, out Length value)
    {
        if (string.IsNullOrWhiteSpace(field.Text))
        {
            value = current;
            return true;
        }

        return Length.TryParse(field.Text, out value, out _);
    }

    /// <summary>
    /// What to put to the editor to make this box that part: not just the stock's <em>name</em>,
    /// but the dimensions the yard fixes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A 1x6 is 5 1/2&#x2033; wide whatever the box was drawn at, so choosing it here really
    /// resizes the blank — through the updater, so that relationships propagate and a conflict is
    /// reported like any other, and as one batch, so that a part pinned to something that cannot
    /// move keeps both its old size and its old stock rather than being half-assigned
    /// (<c>docs/design/parts-and-cut-list.md</c> §1.2).
    /// </para>
    /// <para>
    /// Which dimensions those are is <see cref="StockAssignment"/>'s to say, not this window's: the
    /// panel resolves the typed name through the library — the same lookup the readout above it
    /// uses, so what a person read is what gets assigned — and calls it. A name the library does
    /// not carry fixes nothing and is not an error; the name is still stored, and the cut list says
    /// it did not resolve.
    /// </para>
    /// </remarks>
    Request Assignment(Box box, Part? part)
    {
        if (part is null)
        {
            return new SetPart(box.Id, null);
        }

        return StockAssignment.RequestsFor(Editor.Sketch, box, part, StockFor(part));
    }

    static StockItem? StockFor(Part part)
        => MaterialsLibrary.Shipped.TryFind(part.Stock, out StockItem item) ? item : null;

    /// <summary>
    /// What a typed depth puts to the editor: a <see cref="ParamValue"/> on the box's depth, exactly
    /// as a typed width is one on its width (docs/design/assembly-model.md §1.2) — or nothing, when
    /// the depth is what it already was, or when the part's stock fixes it and so states it itself.
    /// </summary>
    Request? DepthRequest(Box box, Part? part, Length depth)
    {
        if (depth == box.Depth || (part is not null && StockAssignment.FixesDepth(part, StockFor(part))))
        {
            return null;
        }

        return StockAssignment.SizeRequest(Editor.Sketch, new BoxDepthRef(box.Id), depth);
    }

    bool Complain(string why)
    {
        PropertiesError.Text = why;
        PropertiesError.IsVisible = true;
        return false;
    }
}

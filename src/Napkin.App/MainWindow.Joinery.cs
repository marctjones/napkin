using System.Collections.Immutable;
using System.Globalization;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// The join tool (docs/design/joinery-and-fasteners.md &#xA7;5.1): select two parts, press J, choose, Enter. A sheet
/// over the paper at the contact, not a window; every joint it makes is one undo step.
/// </summary>
public partial class MainWindow
{
    /// <summary>One pair of parts the popover is about: the two selected parts, what napkin proposes and what the person changed.</summary>
    sealed class PairDraft(EntityId first, EntityId second, JointProposal proposal, TouchingFaces? faces = null)
    {
        /// <summary>The faces the joint is between: the proposal's, or an existing joint's own.</summary>
        public TouchingFaces Faces { get; } = faces ?? proposal.Faces;

        public EntityId First { get; } = first;

        public EntityId Second { get; } = second;

        public JointProposal Proposal { get; } = proposal;

        /// <summary>The person swapped which part receives.</summary>
        public bool Swapped { get; set; }

        /// <summary>The pocket face picked for a tied pair; null for the proposal's own.</summary>
        public BoxFace? PocketFace { get; set; }

        /// <summary>Whether the pair is ticked, in a join-all.</summary>
        public bool Ticked { get; set; } = true;

        public ComboBox? PocketBox { get; set; }

        public CheckBox? TickBox { get; set; }
    }

    /// <summary>What the last joint was, for Shift+J on two parts: the same settings without the popover.</summary>
    sealed record LastJoint(JointType Type, Length? Depth, FasteningKind Fastening, int? Count, bool Glue);

    static readonly FasteningKind[] FasteningOrder =
    [
        FasteningKind.None, FasteningKind.PocketScrews, FasteningKind.Screws, FasteningKind.Brads,
        FasteningKind.Nails, FasteningKind.Dowels, FasteningKind.Biscuits, FasteningKind.Clips,
    ];

    readonly List<PairDraft> _joinPairs = [];
    readonly Dictionary<JointType, RadioButton> _joinTypeButtons = [];
    ImmutableArray<FasteningKind> _joinFastenings = [];
    Joint? _joinEditing;
    bool _joinAll;
    bool _joinBuilt;
    bool _joinRefreshing;
    LastJoint? _lastJoint;

    /// <summary>The join popover, for the GUI suite.</summary>
    public Border JoinSheet => JoinPanel;

    /// <summary>The Part panel's list of the selected part's joints, empty when it has none.</summary>
    public string PartJointsText => PartJoints.IsVisible ? PartJoints.Text ?? string.Empty : string.Empty;

    /// <summary>Whether the join popover is open.</summary>
    public bool IsJoining => JoinPanel.IsVisible;

    /// <summary>The join popover's title line.</summary>
    public string JoinHeadline => JoinTitle.Text ?? string.Empty;

    /// <summary>The join popover's refusal line, empty when there is none.</summary>
    public string JoinRefusal => JoinMessage.IsVisible ? JoinMessage.Text ?? string.Empty : string.Empty;

    /// <summary>The type button for a joint type.</summary>
    /// <param name="type">The type.</param>
    public RadioButton JoinTypeControl(JointType type) => _joinTypeButtons[type];

    /// <summary>The fastening list.</summary>
    public ComboBox JoinFasteningControl => JoinFasteningBox;

    /// <summary>The count box, blank for the recipe.</summary>
    public TextBox JoinCountControl => JoinCountBox;

    /// <summary>The pocket-face list.</summary>
    public ComboBox JoinPocketControl => JoinPocketBox;

    /// <summary>The depth box.</summary>
    public TextBox JoinDepthControl => JoinDepthBox;

    /// <summary>The glue check box.</summary>
    public CheckBox JoinGlueControl => JoinGlueCheck;

    /// <summary>The button that makes the joint.</summary>
    public Button JoinOkControl => JoinOkButton;

    /// <summary>The button that closes the popover.</summary>
    public Button JoinCancelControl => JoinCancelButton;

    /// <summary>The swap button.</summary>
    public Button JoinSwapControl => JoinSwapButton;

    /// <summary>The pairs a join-all lists, one row each.</summary>
    public StackPanel JoinPairRows => JoinPairs;

    /// <summary>The check box of a join-all's n-th pair.</summary>
    /// <param name="index">The row, from zero.</param>
    public CheckBox JoinPairTick(int index) => _joinPairs[index].TickBox!;

    /// <summary>The pocket-face list of a join-all's n-th pair, or null when it is not a tie.</summary>
    /// <param name="index">The row, from zero.</param>
    public ComboBox? JoinPairPocket(int index) => _joinPairs[index].PocketBox;

    void OnJoinClicked(object? sender, RoutedEventArgs e) => BeginJoin(all: false);

    void OnJoinAllClicked(object? sender, RoutedEventArgs e) => BeginJoin(all: true);

    void WireJoinery()
    {
        JoinSwapButton.Click += (_, _) => SwapJoin();
        JoinOkButton.Click += (_, _) => ConfirmJoin();
        JoinCancelButton.Click += (_, _) => CancelJoin();
        JoinFasteningBox.SelectionChanged += (_, _) => RefreshJoinFields(fromType: false);
        JoinPocketBox.SelectionChanged += (_, _) =>
        {
            if (!_joinRefreshing && _joinPairs is [var only] && JoinPocketBox.SelectedItem is string face)
            {
                only.PocketFace = FaceOf(face);
            }
        };

        Editor.SelectionChanged += (_, _) => UpdatePartJoints();

        // Enter joins and Escape cancels wherever the keyboard is while the sheet is up, ahead of the
        // panels and the drawing; an open drop-down list takes its own Enter and Escape first.
        AddHandler(KeyDownEvent, OnJoinKeyDown, RoutingStrategies.Tunnel);

        DrawingCanvas.JointActivated += (_, id) => EditJoint(id);
        ModelDrawing.JointActivated += (_, id) => EditJoint(id);
    }

    void OnJoinKeyDown(object? sender, KeyEventArgs e)
    {
        if (!JoinPanel.IsVisible || e.Handled)
        {
            return;
        }

        bool listOpen = JoinFasteningBox.IsDropDownOpen || JoinPocketBox.IsDropDownOpen
            || _joinPairs.Any(pair => pair.PocketBox?.IsDropDownOpen == true);
        if (e.Key == Key.Escape && !listOpen)
        {
            CancelJoin();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !listOpen && e.Source is not Button { Name: "JoinCancelButton" })
        {
            ConfirmJoin();
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Starting: which parts, what does not touch, and Shift+J's repeat
    // ------------------------------------------------------------------------------------------------

    void BeginJoin(bool all)
    {
        if (IsShapingPart || IsAskingToSave)
        {
            return;
        }

        Box[] boxes = SelectionCommands.SelectedBoxes(Editor);
        if (boxes.Length < 2)
        {
            Editor.Say(EditSeverity.Problem, JointDescription.SelectTwoPartsPrompt);
            return;
        }

        if (!all && boxes.Length > 2)
        {
            Editor.Say(EditSeverity.Problem, $"J joins two parts at a time and {boxes.Length} are selected. Select two, or press Shift+J to join every touching pair among them.");
            return;
        }

        Sketch sketch = Editor.Sketch;
        List<PairDraft> pairs = [];
        for (int i = 0; i < boxes.Length; i++)
        {
            for (int j = i + 1; j < boxes.Length; j++)
            {
                if (JointProposals.For(sketch, boxes[i].Id, boxes[j].Id) is not { } proposal)
                {
                    continue;
                }

                // A pair that already has a joint is left alone when joining many; asked for by itself it is an edit.
                if (boxes.Length > 2 && sketch.RelationshipsInOrder.OfType<Joint>().Any(joint => Holds(joint, boxes[i].Id, boxes[j].Id)))
                {
                    continue;
                }

                pairs.Add(new PairDraft(boxes[i].Id, boxes[j].Id, proposal));
            }
        }

        if (pairs.Count == 0)
        {
            Editor.Say(
                EditSeverity.Problem,
                boxes.Length == 2
                    ? $"{Editor.NameOf(boxes[0].Id)} and {Editor.NameOf(boxes[1].Id)} don't touch."
                    : "None of the selected parts touch, or the ones that do are already joined.");
            return;
        }

        if (all && boxes.Length == 2 && _lastJoint is { } last)
        {
            RepeatJoint(pairs[0], last);
            return;
        }

        if (boxes.Length == 2 && sketch.RelationshipsInOrder.OfType<Joint>().FirstOrDefault(joint => Holds(joint, boxes[0].Id, boxes[1].Id)) is { } existing)
        {
            EditJoint(existing.Id);
            return;
        }

        OpenJoin(pairs, all: boxes.Length > 2, editing: null);
    }

    static bool Holds(Joint joint, EntityId a, EntityId b)
        => (joint.Receiving.Box == a && joint.Inserted.Box == b) || (joint.Receiving.Box == b && joint.Inserted.Box == a);

    void EditJoint(RelationshipId id)
    {
        if (Editor.Sketch.Relationships.GetValueOrDefault(id) is not Joint joint
            || JointGeometry.Contact(Editor.Sketch, joint) is not { } contact
            || JointProposals.For(Editor.Sketch, joint.Receiving.Box, joint.Inserted.Box) is not { } proposal)
        {
            Editor.Say(EditSeverity.Problem, "Those parts no longer touch, so the joint cannot be edited; remove it or move them back.");
            return;
        }

        Editor.SelectJoint(id);
        TouchingFaces own = new(joint.Receiving.Feature.Faces[0], joint.Inserted.Feature.Faces[0], contact);
        OpenJoin([new PairDraft(joint.Receiving.Box, joint.Inserted.Box, proposal, own)], all: false, editing: joint);
    }

    void RunJointCommand(JointCommand command)
    {
        if (Editor.SelectedJoint is not { } id)
        {
            return;
        }

        if (command == JointCommand.Edit)
        {
            EditJoint(id);
            return;
        }

        string what = "Removed the joint: " + RelationshipText.Describe(Editor.Sketch, Editor.Sketch.Relationships[id], Editor.NameOf, Editor.LabelFormat);
        Editor.SelectJoint(null);
        Editor.Apply(new RemoveRelationship(id), what);
        FocusDrawing();
    }

    // ------------------------------------------------------------------------------------------------
    // The sheet
    // ------------------------------------------------------------------------------------------------

    void BuildJoinControls()
    {
        if (_joinBuilt)
        {
            return;
        }

        _joinBuilt = true;
        foreach (JointType type in new[] { JointType.Butt, JointType.Groove, JointType.Rabbet, JointType.HalfLap, JointType.Tabletop })
        {
            RadioButton button = new()
            {
                Content = JointTooltip.TypeName(type),
                GroupName = "JoinType",
                FontSize = 12,
                Margin = new Thickness(0, 0, 8, 0),
            };
            AutomationProperties.SetName(button, JointTooltip.TypeName(type));
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true)
                {
                    _joinType = type;
                    if (!_joinRefreshing)
                    {
                        RefreshJoinFields(fromType: true);
                    }
                }
            };
            _joinTypeButtons[type] = button;
            JoinTypes.Children.Add(button);
        }
    }

    // Set as a button is checked: the group unchecks the old one after the new one reports, so asking the buttons would say the old.
    JointType _joinType;

    JointType ChosenType => _joinType;

    FasteningKind ChosenFastening => JoinFasteningBox.SelectedIndex is >= 0 and var i && i < _joinFastenings.Length ? _joinFastenings[i] : FasteningKind.None;

    void OpenJoin(List<PairDraft> pairs, bool all, Joint? editing)
    {
        BuildJoinControls();
        _joinPairs.Clear();
        _joinPairs.AddRange(pairs);
        _joinAll = all;
        _joinEditing = editing;
        JoinMessage.IsVisible = false;

        _joinRefreshing = true;
        try
        {
            PairDraft first = pairs[0];
            if (editing is not null)
            {
                first.Swapped = false;
                _joinTypeButtons[editing.Type].IsChecked = true;
                JoinGlueCheck.IsChecked = editing.Glue;
                JoinDepthBox.Text = editing.Depth is { } d ? CutListCsv.Text(d) : string.Empty;
                JoinCountBox.Text = editing.Fastening.Count?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                first.PocketFace = editing.Fastening.PocketFace;
            }
            else
            {
                _joinTypeButtons[first.Proposal.Type].IsChecked = true;
                JoinGlueCheck.IsChecked = _lastJoint?.Glue ?? first.Proposal.Type != JointType.Tabletop;
                JoinDepthBox.Text = string.Empty;
                JoinCountBox.Text = string.Empty;
            }

            // Which part receives: the editing joint's own, else the tool's rule.
            if (editing is not null)
            {
                first.Swapped = Roles(first, editing.Type).Receiving.Box != editing.Receiving.Box;
            }
        }
        finally
        {
            _joinRefreshing = false;
        }

        BuildPairRows();
        RefreshJoinFields(fromType: true, preferred: editing?.Fastening.Kind);

        JoinOkButton.Content = editing is not null ? "Save" : "Join";
        JoinSwapButton.IsVisible = !all;
        JoinPanel.IsVisible = true;
        PlaceJoinPanel(pairs[0]);
        JoinFasteningBox.Focus();
    }

    void BuildPairRows()
    {
        JoinPairs.Children.Clear();
        JoinPairs.IsVisible = _joinAll;
        if (!_joinAll)
        {
            return;
        }

        foreach (PairDraft pair in _joinPairs)
        {
            (FeatureRef receiving, FeatureRef inserted) = Roles(pair, pair.Proposal.Type);
            CheckBox tick = new()
            {
                IsChecked = true,
                FontSize = 11,
                Content = $"{Editor.NameOf(inserted.Box)} → {Editor.NameOf(receiving.Box)} ({JointTooltip.TypeName(pair.Proposal.Type).ToLowerInvariant()})",
            };
            AutomationProperties.SetName(tick, $"Join {Editor.NameOf(inserted.Box)} to {Editor.NameOf(receiving.Box)}");
            tick.IsCheckedChanged += (_, _) =>
            {
                pair.Ticked = tick.IsChecked == true;
                UpdateJoinTitle();
            };
            pair.TickBox = tick;

            StackPanel row = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(tick);
            if (pair.Proposal.PocketFaceTied)
            {
                ComboBox faces = new() { FontSize = 11, MinWidth = 96 };
                foreach (BoxFace face in pair.Proposal.PocketFaces)
                {
                    faces.Items.Add(FaceWords(face));
                }

                faces.SelectedIndex = 0;
                faces.SelectionChanged += (_, _) => pair.PocketFace = faces.SelectedItem is string face ? FaceOf(face) : null;
                AutomationProperties.SetName(faces, $"Pocket holes from, for {Editor.NameOf(inserted.Box)}");
                ToolTip.SetTip(faces, "Both faces are equally near the middle: pick the one the holes go in from.");
                pair.PocketBox = faces;
                pair.PocketFace = pair.Proposal.PocketFaces[0];
                row.Children.Add(faces);
            }

            JoinPairs.Children.Add(row);
        }
    }

    (FeatureRef Receiving, FeatureRef Inserted) Roles(PairDraft pair, JointType type)
    {
        FeatureRef first = new(pair.First, BoxFeature.Face(pair.Faces.OfFirst));
        FeatureRef second = new(pair.Second, BoxFeature.Face(pair.Faces.OfSecond));
        JointRoles roles = JointGeometry.ProposeRoles(Editor.Sketch, type, first, second)!;
        return pair.Swapped ? (roles.Inserted, roles.Receiving) : (roles.Receiving, roles.Inserted);
    }

    static string FaceWords(BoxFace face) => face.ToString().ToLowerInvariant() + " face";

    static BoxFace? FaceOf(string words) => Enum.TryParse(words.Replace(" face", string.Empty, StringComparison.Ordinal), ignoreCase: true, out BoxFace face) ? face : null;

    /// <summary>Refills what depends on the type and the fastening: the fastenings allowed, the recipe count, the depth and pocket boxes.</summary>
    void RefreshJoinFields(bool fromType, FasteningKind? preferred = null)
    {
        if (_joinRefreshing || _joinPairs.Count == 0 || !_joinBuilt)
        {
            return;
        }

        _joinRefreshing = true;
        try
        {
            JointType type = ChosenType;
            PairDraft first = _joinPairs[0];

            if (fromType)
            {
                ImmutableArray<FasteningKind> allowed = [.. FasteningOrder.Where(JointRules.AllowedFastenings(type).Contains)];
                FasteningKind keep = preferred ?? (JoinFasteningBox.SelectedIndex is >= 0 and var at && at < _joinFastenings.Length ? _joinFastenings[at] : FasteningKind.None);
                _joinFastenings = allowed;
                JoinFasteningBox.Items.Clear();
                foreach (FasteningKind kind in allowed)
                {
                    JoinFasteningBox.Items.Add(FasteningName(kind));
                }

                JoinFasteningBox.SelectedIndex = Math.Max(0, allowed.IndexOf(allowed.Contains(keep) ? keep : allowed.Contains(FasteningKind.Clips) ? FasteningKind.Clips : FasteningKind.None));
            }

            FasteningKind fastening = ChosenFastening;
            JoinDepthBox.IsEnabled = JointRules.NeedsDepth(type);
            JoinDepthCaption.Opacity = JoinDepthBox.IsEnabled ? 1 : 0.4;
            JoinCountBox.IsEnabled = fastening != FasteningKind.None;
            JoinCountBox.PlaceholderText = fastening == FasteningKind.None
                ? string.Empty
                : $"recipe: {Recipes.Recipe(fastening, first.Faces.Contact.JointLength)}";

            bool pocket = fastening == FasteningKind.PocketScrews;
            JoinPocketBox.IsVisible = pocket && !_joinAll;
            JoinPocketCaption.IsVisible = pocket && !_joinAll;
            foreach (PairDraft pair in _joinPairs)
            {
                pair.PocketBox?.SetCurrentValue(IsVisibleProperty, pocket);
            }

            if (pocket && !_joinAll)
            {
                (_, FeatureRef inserted) = Roles(first, type);
                Box insertedBox = Editor.Sketch.Find<Box>(inserted.Box)!;
                BoxFace contact = inserted.Feature.Faces[0];
                BoxFace[] options =
                [
                    .. new[] { BoxFace.West, BoxFace.South, BoxFace.Bottom, BoxFace.East, BoxFace.North, BoxFace.Top }
                        .Where(face => face != contact && face != JointRules.Opposite(contact)),
                ];
                ImmutableArray<BoxFace> best = JointProposals.PocketFacesFor(Editor.Sketch, insertedBox, contact);
                BoxFace chosen = first.PocketFace is { } picked && options.Contains(picked) && !fromType ? picked
                    : _joinEditing?.Fastening.PocketFace is { } stored && options.Contains(stored) ? stored
                    : best[0];
                JoinPocketBox.Items.Clear();
                foreach (BoxFace face in options)
                {
                    JoinPocketBox.Items.Add(FaceWords(face));
                }

                JoinPocketBox.SelectedIndex = Array.IndexOf(options, chosen);
                first.PocketFace = chosen;
            }

            foreach (PairDraft pair in _joinAll ? _joinPairs : [])
            {
                if (pair.TickBox is { } tick)
                {
                    // Pairs whose suggested type differs from the chosen one are listed and unticked (§5.1).
                    bool agrees = pair.Proposal.Type == type;
                    tick.IsChecked = agrees;
                    pair.Ticked = agrees;
                }
            }

            UpdateJoinTitle();
        }
        finally
        {
            _joinRefreshing = false;
        }
    }

    static string FasteningName(FasteningKind kind) => kind switch
    {
        FasteningKind.None => "None",
        FasteningKind.PocketScrews => "Pocket screws",
        FasteningKind.Screws => "Screws",
        FasteningKind.Brads => "Brads",
        FasteningKind.Nails => "Nails",
        FasteningKind.Dowels => "Dowels",
        FasteningKind.Biscuits => "Biscuits",
        _ => "Tabletop clips",
    };

    void UpdateJoinTitle()
    {
        if (_joinPairs.Count == 0)
        {
            return;
        }

        if (_joinAll)
        {
            int ticked = _joinPairs.Count(pair => pair.Ticked);
            JoinTitle.Text = $"Join {ticked} of {_joinPairs.Count} touching pairs";
            return;
        }

        (FeatureRef receiving, FeatureRef inserted) = Roles(_joinPairs[0], ChosenType);
        JoinTitle.Text = $"{(_joinEditing is null ? "Join" : "Joint")}  {Editor.NameOf(inserted.Box)}  →  {Editor.NameOf(receiving.Box)}";
    }

    void SwapJoin()
    {
        if (_joinPairs.Count != 1)
        {
            return;
        }

        _joinPairs[0].Swapped = !_joinPairs[0].Swapped;
        _joinPairs[0].PocketFace = null;
        RefreshJoinFields(fromType: false);
    }

    void PlaceJoinPanel(PairDraft pair)
    {
        Sketch sketch = Editor.Sketch;
        JointContact contact = pair.Faces.Contact;
        Point3 middle = contact.Centre;
        Point at = IsShowingModel
            ? ModelDrawing.Camera.Project(middle)
            : DrawingCanvas.View.ToScreen(new Point2(middle.X, middle.Y));
        Visual host = IsShowingModel ? ModelDrawing : DrawingCanvas;
        Point inWindow = host.TranslatePoint(at, this) ?? new Point(200, 200);
        Point inParent = this.TranslatePoint(inWindow, (Visual)JoinPanel.Parent!) ?? inWindow;
        Size room = ((Control)JoinPanel.Parent!).Bounds.Size;

        JoinPanel.Measure(new Size(430, double.PositiveInfinity));
        double height = JoinPanel.DesiredSize.Height;
        // Beside the contact, never over it: to its right when there is room, else to its left.
        double x = inParent.X + 24 + 430 <= room.Width ? inParent.X + 24 : inParent.X - 24 - 430;
        JoinPanel.Margin = new Thickness(
            Math.Clamp(x, 8, Math.Max(8, room.Width - 438)),
            Math.Clamp(inParent.Y - (height / 2), 8, Math.Max(8, room.Height - height - 8)),
            0,
            0);
        _ = sketch;
    }

    void CancelJoin()
    {
        JoinPanel.IsVisible = false;
        _joinPairs.Clear();
        _joinEditing = null;
        FocusDrawing();
    }

    // ------------------------------------------------------------------------------------------------
    // Making the joint
    // ------------------------------------------------------------------------------------------------

    void ConfirmJoin()
    {
        if (_joinPairs.Count == 0)
        {
            return;
        }

        JointType type = ChosenType;
        FasteningKind fastening = ChosenFastening;

        Length? depth = null;
        if (JointRules.NeedsDepth(type))
        {
            if (!Length.TryParse(JoinDepthBox.Text, out Length parsed, out _) || parsed <= Length.Zero)
            {
                RefuseJoin($"A {JointTooltip.TypeName(type).ToLowerInvariant()} needs a depth greater than zero, like 1/4\".");
                return;
            }

            depth = parsed;
        }

        int? count = null;
        string countText = (JoinCountBox.Text ?? string.Empty).Trim();
        if (fastening != FasteningKind.None && countText.Length > 0)
        {
            if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out int typed) || typed < 1)
            {
                RefuseJoin("A count is a whole number of at least 1, or blank for the recipe's.");
                return;
            }

            count = typed;
        }

        bool glue = JoinGlueCheck.IsChecked == true;
        List<Request> requests = [];
        List<Joint> made = [];
        foreach (PairDraft pair in _joinPairs.Where(pair => !_joinAll || pair.Ticked))
        {
            (FeatureRef receiving, FeatureRef inserted) = Roles(pair, type);
            BoxFace? pocket = fastening == FasteningKind.PocketScrews
                ? pair.PocketFace ?? pair.Proposal.PocketFace
                : null;
            RelationshipId id = _joinEditing?.Id ?? new RelationshipId(Guid.NewGuid());
            Joint joint = new(id, receiving, inserted, type, depth, new Fastening(fastening, count, pocket), glue);
            if (JointRules.Errors(joint).FirstOrDefault() is { } refusal)
            {
                RefuseJoin(refusal);
                return;
            }

            made.Add(joint);
        }

        if (made.Count == 0)
        {
            RefuseJoin("No pair is ticked.");
            return;
        }

        string what = _joinEditing is not null
            ? $"Changed the joint between {Editor.NameOf(made[0].Inserted.Box)} and {Editor.NameOf(made[0].Receiving.Box)}"
            : made.Count == 1
                ? $"Joined {Editor.NameOf(made[0].Inserted.Box)} to {Editor.NameOf(made[0].Receiving.Box)}"
                : $"Made {made.Count} joints";
        if (_joinEditing is { } old)
        {
            requests.Add(new RemoveRelationship(old.Id));
        }

        requests.AddRange(made.Select(joint => new AddRelationship(joint)));

        UpdateResult result = Editor.Apply(Batch.Of([.. requests]), what);
        if (result is Rejected)
        {
            RefuseJoin(Editor.LastMessage?.Text ?? "That joint could not be made.");
            return;
        }

        _lastJoint = new LastJoint(type, depth, fastening, count, glue);
        JoinPanel.IsVisible = false;
        _joinPairs.Clear();
        _joinEditing = null;
        if (made.Count == 1)
        {
            Editor.SelectJoint(made[0].Id);
        }

        FocusDrawing();
    }

    void RefuseJoin(string words)
    {
        JoinMessage.Text = words;
        JoinMessage.IsVisible = true;
    }

    /// <summary>Shift+J on two parts: the last joint's settings, without the popover.</summary>
    void RepeatJoint(PairDraft pair, LastJoint last)
    {
        FeatureRef first = new(pair.First, BoxFeature.Face(pair.Faces.OfFirst));
        FeatureRef second = new(pair.Second, BoxFeature.Face(pair.Faces.OfSecond));
        JointRoles roles = JointGeometry.ProposeRoles(Editor.Sketch, last.Type, first, second)!;
        Box inserted = Editor.Sketch.Find<Box>(roles.Inserted.Box)!;
        BoxFace? pocket = last.Fastening == FasteningKind.PocketScrews
            ? JointProposals.PocketFacesFor(Editor.Sketch, inserted, roles.Inserted.Feature.Faces[0])[0]
            : null;
        Joint joint = new(
            new RelationshipId(Guid.NewGuid()),
            roles.Receiving,
            roles.Inserted,
            last.Type,
            last.Depth,
            new Fastening(last.Fastening, last.Count, pocket),
            last.Glue);

        if (JointRules.Errors(joint).FirstOrDefault() is { } refusal)
        {
            Editor.Say(EditSeverity.Problem, refusal);
            return;
        }

        if (Editor.Apply(new AddRelationship(joint), $"Joined {Editor.NameOf(joint.Inserted.Box)} to {Editor.NameOf(joint.Receiving.Box)} like the last joint") is not Rejected)
        {
            Editor.SelectJoint(joint.Id);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // The Part panel's joint list
    // ------------------------------------------------------------------------------------------------

    void UpdatePartJoints()
    {
        if (Editor.OnlySelectedBox is not { } box)
        {
            PartJoints.Text = string.Empty;
            PartJoints.IsVisible = false;
            return;
        }

        Sketch sketch = Editor.Sketch;
        string[] lines =
        [
            .. sketch.RelationshipsInOrder.OfType<Joint>()
                .Where(joint => joint.Receiving.Box == box.Id || joint.Inserted.Box == box.Id)
                .Select(joint => "• " + JointTooltip.Of(sketch, joint, Editor.NameOf)),
        ];
        PartJoints.Text = lines.Length == 0 ? string.Empty : "Joints\n" + string.Join("\n", lines);
        PartJoints.IsVisible = lines.Length > 0;
    }
}

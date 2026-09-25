using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Joinery on screen (docs/design/joinery-and-fasteners.md &#xA7;10.4): the fastener sizes, the
/// hardware and the supplies, typed and read back the way a person reads them, on the DIY coffee table.
/// </summary>
public class JoineryWorkflows
{
    /// <summary>Two parts drawn, J, Enter: a joint with a marker, a tooltip, and a Delete that takes it away in one undo step.</summary>
    [GuiWorkflow("GUI-JOIN-01")]
    public void Join_two_parts_with_J_read_the_marker_and_delete_it() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        app.Chord(Key.N);

        // Two rectangles that meet along x = 18: a 12 x 3 rail and a 3 x 12 post.
        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(6, 6)), At(window, Point2.Inches(12, 8)), At(window, Point2.Inches(18, 9)));
        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(18, 6)), At(window, Point2.Inches(20, 12)), At(window, Point2.Inches(21, 18)));

        app.Expect("two touching parts are drawn and no joint is", () =>
        {
            Assert.Equal(2, window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Count());
            Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>());
        });

        Box rail = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Anchor.X.Units).First();
        Box post = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Anchor.X.Units).Last();
        app.Click(At(window, Point2.Inches(9, 7)));
        app.Click(At(window, Point2.Inches(19, 15)), modifiers: KeyModifiers.Shift);

        app.Expect("both parts are selected", () => Assert.Equal([rail.Id, post.Id], window.Editor.Selection.Order()));

        // Refusals first, in napkin's voice: one part is not two, and a third part far away touches nothing.
        app.Click(new Point(120, 320));
        app.Click(At(window, Point2.Inches(9, 7)));
        app.Press(Key.J);
        app.Expect("one part selected: J says what it needs and opens nothing", () =>
        {
            Assert.False(window.IsJoining);
            Assert.Equal("Select the two parts to join first, then press J.", window.Editor.LastMessage!.Text);
        });

        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(-9, 6)), At(window, Point2.Inches(-8, 8)), At(window, Point2.Inches(-6, 9)));
        app.Click(new Point(120, 320));
        app.Click(At(window, Point2.Inches(9, 7)));
        app.Click(At(window, Point2.Inches(-8, 7)), modifiers: KeyModifiers.Shift);
        app.Press(Key.J);
        app.Expect("two parts that do not touch: the status line says so, by name, and nothing opens", () =>
        {
            Assert.False(window.IsJoining);
            Assert.Equal("Part 1 and Part 3 don't touch.", window.Editor.LastMessage!.Text);
        });

        app.Click(new Point(120, 320));
        app.Click(At(window, Point2.Inches(9, 7)));
        app.Click(At(window, Point2.Inches(19, 15)), modifiers: KeyModifiers.Shift);
        app.Press(Key.J);

        app.Expect("the popover opens on the contact with a butt chosen, the parts named, and no joint made yet", () =>
        {
            Assert.True(window.IsJoining);
            Assert.True(window.JoinTypeControl(JointType.Butt).IsChecked);
            Assert.Contains("\u2192", window.JoinHeadline, StringComparison.Ordinal);
            Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>());
        });
        app.SaveFrame("popover");

        // Which part receives can be swapped, and swapped back.
        string headline = window.JoinHeadline;
        ClickControl(app, window, window.JoinSwapControl);
        app.Expect("swap reverses the two parts in the title", () => Assert.NotEqual(headline, window.JoinHeadline));
        ClickControl(app, window, window.JoinSwapControl);
        app.Expect("and swapping again puts them back", () => Assert.Equal(headline, window.JoinHeadline));

        app.Press(Key.Enter);

        app.Expect("Enter made one butt joint, closed the popover, and its marker is on the drawing, solid", () =>
        {
            Assert.False(window.IsJoining);
            Joint joint = Assert.Single(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>());
            Assert.Equal(JointType.Butt, joint.Type);
            PlacedJointMarker marker = Assert.Single(window.Canvas.JointMarkerLayer.Placed);
            Assert.Equal(('B', true), (marker.Marker.Letter, marker.Marker.Satisfied));
            Assert.Equal(joint.Id, window.Editor.SelectedJoint);
        });

        window.Editor.ClearSelection();
        PlacedJointMarker placed = Assert.Single(window.Canvas.JointMarkerLayer.Placed);
        Point onMarker = InWindow(window, placed.At);
        app.MoveTo(onMarker);
        app.MoveTo(onMarker + new Point(1, 1));
        app.SaveFrame("marker");

        app.Expect("the tooltip on the marker is the one sentence the joint reads as", () =>
        {
            string? tip = ToolTip.GetTip(window.Canvas) as string;
            Assert.NotNull(tip);
            Assert.StartsWith("Butt \u2014 ", tip, StringComparison.Ordinal);
            Assert.Contains(" \u2190 ", tip, StringComparison.Ordinal);
        });

        app.Click(onMarker);
        app.Expect("a click on the marker selects the joint and no part", () =>
        {
            Assert.NotNull(window.Editor.SelectedJoint);
            Assert.Empty(window.Editor.Selection);
        });

        app.Press(Key.Delete);
        app.Expect("Delete removes the joint and leaves both parts", () =>
        {
            Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>());
            Assert.Equal(3, window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Count());
            Assert.Empty(window.Canvas.JointMarkerLayer.Placed);
        });

        app.Chord(Key.Z);
        app.Expect("one undo brings the joint back, with its marker", () =>
        {
            Assert.Single(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>());
            Assert.Single(window.Canvas.JointMarkerLayer.Placed);
        });

        // Reopen it: double-click its marker; Escape closes it with the joint still selected; Enter opens it again.
        app.DoubleClick(InWindow(window, Assert.Single(window.Canvas.JointMarkerLayer.Placed).At));
        app.Expect("a double-click on the marker opens the popover on that joint, saying Save", () =>
        {
            Assert.True(window.IsJoining);
            Assert.Equal("Save", window.JoinOkControl.Content);
            Assert.True(window.JoinGlueControl.IsChecked);
        });

        app.Press(Key.Escape);
        app.Expect("Escape closes it and changes nothing; the joint stays selected", () =>
        {
            Assert.False(window.IsJoining);
            Assert.NotNull(window.Editor.SelectedJoint);
        });

        window.Canvas.Focus();
        app.Press(Key.Enter);
        app.Expect("Enter on a selected joint opens it again", () => Assert.True(window.IsJoining));

        ClickControl(app, window, window.JoinGlueControl);
        app.Press(Key.Enter);
        app.Expect("the joint has the same id, is now unglued, and is still the only joint", () =>
        {
            Joint joint = Assert.Single(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>());
            Assert.False(joint.Glue);
            Assert.False(window.IsJoining);
        });

        app.Chord(Key.Z);
        app.Expect("one undo puts the glue back", () =>
            Assert.True(Assert.Single(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>()).Glue));

        Select(window, "Part 1");
        app.Expect("the Part panel lists the part's joint in the same words as the tooltip", () =>
            Assert.StartsWith("Joints\n\u2022 Butt \u2014 ", window.PartJointsText, StringComparison.Ordinal));

        window.Canvas.Focus();
        app.Press(Key.V);
        app.Expect("the 3D view draws the same marker, one, solid, with the same letter", () =>
        {
            Assert.True(window.IsShowingModel);
            PlacedJointMarker marker = Assert.Single(window.Model.JointMarkerLayer.Placed);
            Assert.Equal(('B', true), (marker.Marker.Letter, marker.Marker.Satisfied));
        });
        app.SaveFrame("marker-3d");
    });

    static readonly string[] Frame =
    [
        "Leg, south-west", "Leg, north-west", "Leg, south-east", "Leg, north-east",
        "Apron, back", "Apron, side, west", "Apron, side, east", "Front rail", "Web",
    ];

    /// <summary>The leg frame in one gesture: Shift+J on nine parts, ten pocket-screw joints, the web's pocket face picked with the keys.</summary>
    [GuiWorkflow("GUI-JOIN-02")]
    public void Join_the_whole_frame_with_Shift_J_and_pick_the_webs_pocket_face() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "DIY coffee table with drawers");
        Strip(window, joint => joint.Fastening.Kind == FasteningKind.PocketScrews);
        Select(window, Frame);

        app.Expect("the frame has no pocket-screw joints and its nine parts are selected", () =>
        {
            Assert.Equal(24, Sketch(window).RelationshipsInOrder.OfType<Joint>().Count());
            Assert.Equal(9, window.Editor.Selection.Count);
        });

        window.Canvas.Focus();
        app.Press(Key.J, KeyModifiers.Shift);

        app.Expect("the popover lists the ten touching pairs, all suggesting a butt, two with a pocket face to choose", () =>
        {
            Assert.True(window.IsJoining);
            Assert.Equal(10, window.JoinPairRows.Children.Count);
            Assert.Contains("10 of 10", window.JoinHeadline, StringComparison.Ordinal);
            Assert.Equal(2, Enumerable.Range(0, 10).Count(i => window.JoinPairPocket(i) is not null));
        });

        // Pairs whose suggested type is not the type chosen are unticked: choose a groove and none of the ten is ticked, a butt and all are.
        ClickControl(app, window, window.JoinTypeControl(JointType.Groove));
        app.Expect("a groove suits none of the ten pairs, so none is ticked", () =>
        {
            Assert.Contains("0 of 10", window.JoinHeadline, StringComparison.Ordinal);
            Assert.All(Enumerable.Range(0, 10), i => Assert.False(window.JoinPairTick(i).IsChecked));
        });

        ClickControl(app, window, window.JoinTypeControl(JointType.Butt));
        app.Expect("choosing a butt again ticks all ten", () => Assert.Contains("10 of 10", window.JoinHeadline, StringComparison.Ordinal));

        // Pocket screws by keyboard: the fastening list has focus, and one step down from None is Pocket screws.
        window.JoinFasteningControl.Focus();
        app.WaitForIdle();
        app.Press(Key.Down);

        app.Expect("the fastening is pocket screws and the count box says the recipe's number for a joint", () =>
        {
            Assert.Equal("Pocket screws", window.JoinFasteningControl.SelectedItem);
            Assert.StartsWith("recipe: ", window.JoinCountControl.PlaceholderText, StringComparison.Ordinal);
        });

        // The web sits at the middle of everything, so both its broad faces are equally near: pick east on the first, by keyboard.
        int firstTie = Enumerable.Range(0, 10).First(i => window.JoinPairPocket(i) is not null);
        window.JoinPairPocket(firstTie)!.Focus();
        app.WaitForIdle();
        app.Press(Key.Down);
        app.SaveFrame("frame-popover");

        app.Expect("the first web pair's pocket face is east, the other still west, the low face first", () =>
        {
            Assert.Equal("east face", window.JoinPairPocket(firstTie)!.SelectedItem);
            Assert.Equal("west face", Enumerable.Range(0, 10).Where(i => i != firstTie).Select(window.JoinPairPocket).Single(box => box is not null)!.SelectedItem);
        });

        app.Press(Key.Enter);

        app.Expect("ten butt joints with pocket screws, glued, one gesture, and 27 pocket screws all told (6 + 12 + 4 + 3 + 2)", () =>
        {
            Joint[] pocket = [.. Sketch(window).RelationshipsInOrder.OfType<Joint>().Where(joint => joint.Fastening.Kind == FasteningKind.PocketScrews)];
            Assert.Equal(10, pocket.Length);
            Assert.All(pocket, joint => Assert.Equal((JointType.Butt, true), (joint.Type, joint.Glue)));
            Assert.Equal(27, FastenerList.Of(Sketch(window)).Single(row => row.Kind == FastenerKind.PocketScrew).Count);
            Assert.Equal(
                [BoxFace.East, BoxFace.West],
                pocket.Where(joint => Name(window, joint.Inserted.Box) == "Web").OrderBy(joint => joint.Id).Select(joint => joint.Fastening.PocketFace!.Value).Order());
            Assert.Equal(34, Sketch(window).RelationshipsInOrder.OfType<Joint>().Count());
        });

        app.Chord(Key.Z);
        app.Expect("one undo takes all ten joints away, and the markers with them", () =>
        {
            Assert.True(24 == Sketch(window).RelationshipsInOrder.OfType<Joint>().Count(), string.Join("\n", Sketch(window).RelationshipsInOrder.OfType<Joint>().Select(joint => Describe(window, joint))));
        });
    });

    static Sketch Sketch(MainWindow window) => window.CurrentDesign!.Sketch;

    static string Name(MainWindow window, EntityId id) => Sketch(window).Find(id)!.Name;

    static Box Part(MainWindow window, string name) => Sketch(window).Entities.Values.OfType<Box>().Single(box => box.Name == name);

    static void Select(MainWindow window, params string[] names) => window.Editor.SelectAll(names.Select(name => Part(window, name).Id));

    /// <summary>Removes joints the workflow is about to make again, in one setup step (the drawing is not what is under test).</summary>
    static void Strip(MainWindow window, Func<Joint, bool> which) => window.Editor.Apply(
        Batch.Of([.. Sketch(window).RelationshipsInOrder.OfType<Joint>().Where(which).Select(joint => (Request)new RemoveRelationship(joint.Id))]),
        "strip joints");

    static void ChooseMenu(AppDriver app, MainWindow window, string menu, string item)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>(menu)!));
        app.WaitForIdle();
        MenuItem entry = window.GetVisualDescendants().OfType<MenuItem>().SingleOrDefault(candidate => (candidate.Header as string) == item)
            ?? throw new InvalidOperationException("No menu item " + item + "; have: " + string.Join(" | ", window.GetVisualDescendants().OfType<MenuItem>().Select(candidate => candidate.Header)));
        app.Click(CentreOf(window, entry));
    }

    /// <summary>What a joint is, by the names of its parts, for comparing the tool's joints with the sample's.</summary>
    static string Describe(MainWindow window, Joint joint) =>
        $"{Name(window, joint.Inserted.Box)} > {Name(window, joint.Receiving.Box)}: {joint.Type} {joint.Depth?.Units} {joint.Fastening.Kind} {joint.Fastening.Count} {joint.Glue}";

    /// <summary>The drawer, by hand: each joint chosen in the popover, the rabbet's depth typed, the bottom's groove pre-selected, a count typed over the recipe's.</summary>
    [GuiWorkflow("GUI-JOIN-03")]
    public void Join_a_drawer_with_a_rabbet_a_groove_and_a_typed_count() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "DIY coffee table with drawers");
        Func<Joint, bool> drawerA = joint => Name(window, joint.Inserted.Box).EndsWith(", A", StringComparison.Ordinal);
        Strip(window, drawerA);

        // The box front's end sits in a rabbet cut in the side: rabbet, 1/4 deep, brads.
        Select(window, "Drawer side, left, A", "Drawer box front, A");
        window.Canvas.Focus();
        app.Press(Key.J);
        app.Expect("the popover proposes a butt for two equal sheets meeting at an end", () =>
        {
            Assert.True(window.IsJoining);
            Assert.True(window.JoinTypeControl(JointType.Butt).IsChecked);
            Assert.False(window.JoinDepthControl.IsEnabled);
        });

        ClickControl(app, window, window.JoinTypeControl(JointType.Rabbet));
        app.Press(Key.Enter);
        app.Expect("a rabbet with no depth is refused in the sheet, in words, and nothing is joined", () =>
        {
            Assert.True(window.IsJoining);
            Assert.Equal("A rabbet needs a depth greater than zero, like 1/4\".", window.JoinRefusal);
            Assert.Empty(Sketch(window).RelationshipsInOrder.OfType<Joint>().Where(drawerA));
        });

        ClickControl(app, window, window.JoinDepthControl);
        app.Type("0");
        app.Press(Key.Enter);
        Assert.Contains("greater than zero", window.JoinRefusal, StringComparison.Ordinal);
        app.Chord(Key.A);
        app.Type("1/4");
        Choose(app, window, "Brads");
        app.SaveFrame("rabbet");
        app.Press(Key.Enter);

        app.Expect("a rabbet 1/4 deep held with brads, glued: one joint, and the recipe's 3 brads for its 3 1/2 in", () =>
        {
            Assert.False(window.IsJoining);
            Joint joint = Sketch(window).RelationshipsInOrder.OfType<Joint>().Single(drawerA);
            Assert.Equal((JointType.Rabbet, 256L, FasteningKind.Brads, true), (joint.Type, joint.Depth!.Value.Units, joint.Fastening.Kind, joint.Glue));
            Assert.Equal(3, Recipes.Count(joint, JointGeometry.Contact(Sketch(window), joint)!.JointLength));
        });

        // The bottom rides in a groove: the tool sees a thin panel's edge in a thicker face and pre-selects it.
        Select(window, "Drawer side, left, A", "Drawer bottom, A");
        window.Canvas.Focus();
        app.Press(Key.J);
        app.Expect("the popover has the groove chosen already, and asks for its depth", () =>
        {
            Assert.True(window.JoinTypeControl(JointType.Groove).IsChecked);
            Assert.True(window.JoinDepthControl.IsEnabled);
        });

        ClickControl(app, window, window.JoinDepthControl);
        app.Type("1/4");
        Choose(app, window, "None");
        ClickControl(app, window, window.JoinGlueControl);
        app.Press(Key.Enter);

        app.Expect("a groove 1/4 deep, nothing fastening it, not glued", () =>
        {
            Joint groove = Sketch(window).RelationshipsInOrder.OfType<Joint>().Single(joint => joint.Type == JointType.Groove && drawerA(joint));
            Assert.Equal((256L, FasteningKind.None, false), (groove.Depth!.Value.Units, groove.Fastening.Kind, groove.Glue));
        });

        // The drawer front goes on the box front with screws: the recipe says 3, and the builder types 4 over it.
        Select(window, "Drawer front, A", "Drawer box front, A");
        window.Canvas.Focus();
        app.Press(Key.J);
        Choose(app, window, "Screws");

        app.Expect("the count box shows the recipe's number as a suggestion, not a value", () =>
        {
            Assert.Equal("recipe: 3", window.JoinCountControl.PlaceholderText);
            Assert.True(string.IsNullOrEmpty(window.JoinCountControl.Text));
        });

        ClickControl(app, window, window.JoinCountControl);
        app.Type("0");
        app.Press(Key.Enter);
        Assert.Contains("whole number of at least 1", window.JoinRefusal, StringComparison.Ordinal);
        app.Chord(Key.A);
        app.Type("4");
        Assert.False(window.JoinGlueControl.IsChecked, "glue starts as the last joint had it: unglued");
        app.SaveFrame("count");
        app.Press(Key.Enter);

        app.Expect("the joint keeps the typed 4, unglued, and the wood-screw line is 4 through the 1/2 in box front", () =>
        {
            Joint screws = Sketch(window).RelationshipsInOrder.OfType<Joint>().Single(joint => joint.Fastening.Kind == FasteningKind.Screws && drawerA(joint));
            Assert.Equal((4, false), (screws.Fastening.Count, screws.Glue));
            FastenerRow row = FastenerList.Of(Sketch(window)).Single(line => line.Kind == FastenerKind.WoodScrew && line.Sources.Any(source => source.Joint == screws.Id) && line.Thickness == new Length(512));
            Assert.Equal(8, row.Count);   // this drawer's 4 and the other drawer's typed 4
        });

        // Shift+J on two parts repeats the last joint without asking: the same type, screws and typed count, unglued.
        Select(window, "Drawer side, right, A", "Drawer box front, A");
        window.Canvas.Focus();
        HashSet<RelationshipId> known = [.. Sketch(window).RelationshipsInOrder.OfType<Joint>().Select(joint => joint.Id)];
        int before = known.Count;
        app.Press(Key.J, KeyModifiers.Shift);

        app.Expect("one more joint like the last, made with no popover", () =>
        {
            Assert.False(window.IsJoining);
            Joint repeated = Sketch(window).RelationshipsInOrder.OfType<Joint>().Single(joint => !known.Contains(joint.Id));
            Assert.Equal(before + 1, Sketch(window).RelationshipsInOrder.OfType<Joint>().Count());
            Assert.Equal((JointType.Butt, FasteningKind.Screws, 4, false), (repeated.Type, repeated.Fastening.Kind, repeated.Fastening.Count, repeated.Glue));
        });

        app.Chord(Key.Z);
        app.Expect("and undo takes just that one away", () =>
            Assert.Equal(before, Sketch(window).RelationshipsInOrder.OfType<Joint>().Count()));
    });

    /// <summary>Picks a fastening in the popover with the keyboard: the list has focus and the arrow keys step through it.</summary>
    static void Choose(AppDriver app, MainWindow window, string fastening)
    {
        window.JoinFasteningControl.Focus();
        app.WaitForIdle();
        int target = window.JoinFasteningControl.Items.IndexOf(fastening);
        while (window.JoinFasteningControl.SelectedIndex != target)
        {
            app.Press(window.JoinFasteningControl.SelectedIndex < target ? Key.Down : Key.Up);
        }

        Assert.Equal(fastening, window.JoinFasteningControl.SelectedItem);
    }

    /// <summary>The web moves an inch north: its two markers go hollow, the cut list flags its row, and one undo puts it all back.</summary>
    [GuiWorkflow("GUI-JOIN-04")]
    public void Move_a_joined_part_and_see_its_joints_go_hollow_and_come_back_with_undo() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "DIY coffee table with drawers");
        app.Chord(Key.L);
        CutListWindow list = window.CutList!;

        app.Expect("every joint of the sample touches: 34 markers are drawn when they fit, all solid, and no cut-list row is flagged", () =>
        {
            Assert.All(window.Canvas.JointMarkerLayer.Placed, marker => Assert.True(marker.Marker.Satisfied));
            Assert.DoesNotContain(list.Rows.LinesOnScreen, line => line.Contains("joint not satisfied", StringComparison.Ordinal));
        });

        Select(window, "Web");
        window.Canvas.Focus();
        Box web = Part(window, "Web");
        window.PartNameField.Focus();
        app.Click(CentreOf(window, window.PartNameField));
        app.Chord(Key.A);
        app.Type("Web");
        app.Tab(2);
        app.Type("20 5/8");
        app.Tab();
        app.Type("3 1/4");
        app.Press(Key.Enter);

        app.Expect("the web is 1 in further north, and neither of its joints was refused or moved with it", () =>
        {
            Assert.Equal(web.Anchor.Y + new Length(1024), Part(window, "Web").Anchor.Y);
            Assert.Equal(34, Sketch(window).RelationshipsInOrder.OfType<Joint>().Count());
        });

        app.Expect("its two joints are hollow, the others solid, and the web's cut-list row says the joint is not satisfied", () =>
        {
            PlacedJointMarker[] hollow = [.. window.Canvas.JointMarkerLayer.Placed.Where(marker => !marker.Marker.Satisfied)];
            Assert.Equal(2, hollow.Length);
            Assert.Contains(list.Rows.LinesOnScreen, line => line.Contains("joint not satisfied", StringComparison.Ordinal));
            Joint apron = Sketch(window).RelationshipsInOrder.OfType<Joint>().Single(joint => Name(window, joint.Inserted.Box) == "Web" && Name(window, joint.Receiving.Box) == "Apron, back");
            Assert.StartsWith("Butt (parts no longer touch)", JointTooltip.Of(Sketch(window), apron), StringComparison.Ordinal);
        });

        AppDriver lists = AppDriver.Attach(list, "flagged");
        lists.SaveFrame("row-flagged");
        window.Activate();
        app.Click(new Point(100, 520));
        app.Chord(Key.Z);

        app.Expect("undo moves the web back and every marker is solid again, the row unflagged", () =>
        {
            Assert.Equal(web.Anchor.Y, Part(window, "Web").Anchor.Y);
            Assert.All(window.Canvas.JointMarkerLayer.Placed, marker => Assert.True(marker.Marker.Satisfied));
            Assert.DoesNotContain(list.Rows.LinesOnScreen, line => line.Contains("joint not satisfied", StringComparison.Ordinal));
        });
    });

    /// <summary>
    /// The joinery storyboard end to end (note &#xA7;9, steps 5 to 14) on the DIY table's parts: the frame in one gesture, the
    /// cleats, both drawers joint by joint, the tabletop, then the sizes and supplies typed and the lists read against
    /// the hand-worked expectations: 34 joints, 13 cut-list rows, 27 / 6 / 8 / 24 / 10 fasteners.
    /// </summary>
    [GuiWorkflow("GUI-JOIN-07")]
    public void Join_the_whole_table_and_read_the_cut_list_and_the_fasteners() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        System.Text.Json.JsonElement expected;
        using (FileStream stream = File.OpenRead(Path.Combine(RepositoryLayout.SamplesDirectory, "diy-coffee-table-drawers.expected.json")))
        {
            expected = System.Text.Json.JsonDocument.Parse(stream).RootElement.Clone();
        }

        Sketch sample = Assert.IsType<Napkin.Core.Project.Loaded>(Napkin.Core.Project.SceneReader.ReadFile(Path.Combine(RepositoryLayout.SamplesDirectory, "diy-coffee-table-drawers.scene.json"))).Sketch;
        OpenSample(app, window, "DIY coffee table with drawers");
        Strip(window, _ => true);
        window.Editor.Apply(new SetFastenerChoices([]), "clear sizes");
        window.Editor.Apply(new SetSupplies([]), "clear supplies");

        app.Expect("the parts are drawn and nothing is joined yet: 24 parts, no joints, every fastener size and supply still to type", () =>
        {
            Assert.Equal(24, Sketch(window).Entities.Values.OfType<Box>().Count());
            Assert.Empty(Sketch(window).RelationshipsInOrder.OfType<Joint>());
        });

        // 5. The leg frame in one gesture.
        Select(window, Frame);
        window.Canvas.Focus();
        app.Press(Key.J, KeyModifiers.Shift);
        Choose(app, window, "Pocket screws");
        app.Press(Key.Enter);
        app.Expect("ten pocket-screw joints from one gesture", () =>
            Assert.Equal(10, Sketch(window).RelationshipsInOrder.OfType<Joint>().Count(joint => joint.Fastening.Kind == FasteningKind.PocketScrews)));

        // 6. The slide cleats, screwed through the aprons.
        foreach (string side in new[] { "west", "east" })
        {
            JoinTwo(app, window, $"Apron, side, {side}", $"Slide cleat, {side}", fastening: "Screws", glue: true);
        }

        // 7 and 8. Both drawers, joint by joint: rabbet the front, butt the back, groove the bottom all round, screw the front on.
        foreach (string drawer in new[] { "A", "B" })
        {
            foreach (string side in new[] { "left", "right" })
            {
                JoinTwo(app, window, $"Drawer side, {side}, {drawer}", $"Drawer box front, {drawer}", JointType.Rabbet, "1/4", "Brads", glue: true);
                JoinTwo(app, window, $"Drawer side, {side}, {drawer}", $"Drawer box back, {drawer}", fastening: "Brads", glue: true);
                JoinTwo(app, window, $"Drawer side, {side}, {drawer}", $"Drawer bottom, {drawer}", JointType.Groove, "1/4", "None", glue: false);
            }

            JoinTwo(app, window, $"Drawer box front, {drawer}", $"Drawer bottom, {drawer}", JointType.Groove, "1/4", "None", glue: false);
            JoinTwo(app, window, $"Drawer box back, {drawer}", $"Drawer bottom, {drawer}", JointType.Groove, "1/4", "None", glue: false);
            JoinTwo(app, window, $"Drawer front, {drawer}", $"Drawer box front, {drawer}", fastening: "Screws", glue: false, count: "4");
        }

        // 9. The top is held with clips: Shift+J on the top and what it sits on.
        Select(window, "Top", "Apron, back", "Apron, side, west", "Apron, side, east", "Front rail");
        window.Canvas.Focus();
        app.Press(Key.J, KeyModifiers.Shift);
        SetGlue(app, window, false);
        app.Press(Key.Enter);

        app.Expect("the tool made the note's 34 joints, each as the sample records it: same parts, type, depth, fastening, count, glue and pocket face", () =>
        {
            string[] made = [.. Sketch(window).RelationshipsInOrder.OfType<Joint>().Select(joint => Describe(window, joint) + " " + joint.Fastening.PocketFace).Order()];
            string[] want = [.. sample.RelationshipsInOrder.OfType<Joint>().Select(joint => $"{sample.Find(joint.Inserted.Box)!.Name} > {sample.Find(joint.Receiving.Box)!.Name}: {joint.Type} {joint.Depth?.Units} {joint.Fastening.Kind} {joint.Fastening.Count} {joint.Glue} {joint.Fastening.PocketFace}").Order()];
            Assert.Equal(34, made.Length);
            Assert.Equal(want, made);
        });

        // 13. The cut list: thirteen rows, 24 pieces, the sentences and the CSV the note works out.
        app.Chord(Key.L);
        CutListWindow list = window.CutList!;
        app.Expect("the cut list is thirteen rows and its CSV is the hand-worked one, joinery column and all", () =>
        {
            Assert.Contains("13 rows, 24 pieces to cut", list.Headline, StringComparison.Ordinal);
            string[] want = [.. expected.GetProperty("cutListCsv").EnumerateArray().Select(line => line.GetString()!)];
            string[] lines = list.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(want.Skip(2).Order(StringComparer.Ordinal), lines.Skip(2).Order(StringComparer.Ordinal));
        });

        // 12 and 14. Type the sizes, the packs and the supplies; read the fasteners.
        AppDriver lists = AppDriver.Attach(list, "storyboard");
        lists.Click(CentreOf(list, list.ShoppingListTabItem));
        app.Expect("with no size chosen every fastener line says so, and the counts are already the note's: 27, 6, 8, 24, 10", () =>
        {
            string[][] rows = [.. list.Extras.LinesOnScreen.Skip(1).Where(line => line.StartsWith("Fasteners", StringComparison.Ordinal)).Select(line => line.Split('\t'))];
            Assert.Equal(["27", "6", "8", "24", "10"], rows.Select(row => row[3]));
            Assert.All(rows, row => Assert.Equal("size not chosen", row[2]));
        });
        lists.SaveFrame("unsized");

        lists.Click(CentreOf(list, list.SizesTabItem));
        string[] sizes = ["1-1/4 in coarse", "#8 x 1-1/4", "#8 x 1", "18 ga x 1", "figure-8, with screws"];
        string[] packs = ["100", "100", "100", "1000", "8"];
        for (int row = 0; row < sizes.Length; row++)
        {
            lists.Click(CentreOf(list, list.SizeBox(0)));
            lists.Chord(Key.A);
            lists.Type(sizes[row]);
            lists.Press(Key.Tab);
            lists.Type(packs[row]);
            lists.Press(Key.Enter);
        }

        lists.Wheel(CentreOf(list, list.SaveSizesControl), new Vector(0, -20));
        lists.WaitForIdle();
        ClickList(lists, list, list.SuppliesField);
        lists.Type("Wood glue");
        lists.Press(Key.Enter);
        lists.Type("Sandpaper, 120 and 220");
        lists.Press(Key.Enter);
        lists.Type("Finish");
        ClickList(lists, list, list.SaveSuppliesControl);
        lists.Click(CentreOf(list, list.ShoppingListTabItem));
        lists.SaveFrame("sized");

        app.Expect("the fastener list is the note's five rows (27, 6, 8, 24, 10 in packs of 1, 1, 1, 1, 2) and the supplies end with the glue line", () =>
        {
            string[][] wantCsv = [.. expected.GetProperty("suppliesCsv").EnumerateArray().Skip(2).Select(line => Napkin.Modules.Furniture.CutListCsv.Parse(line.GetString()! + "\n").Single().ToArray())];
            string[][] got = [.. list.Extras.LinesOnScreen.Skip(1).Select(line => line.Split('\t'))];

            // Section, item, size, count, pack, packs: the For column depends on the order the joints were made in.
            Assert.Equal(wantCsv.Select(row => string.Join("|", row.Take(6))), got.Select(row => string.Join("|", row.Take(6))));
            Assert.Equal("Glue: 20 of 34 joints", got[^1][1]);
        });
    });

    static void ClickList(AppDriver lists, CutListWindow list, Visual control)
    {
        lists.MoveTo(CentreOf(list, control));
        lists.WaitForIdle();
        lists.Click(CentreOf(list, control));
    }

    static void SetGlue(AppDriver app, MainWindow window, bool wanted)
    {
        if (window.JoinGlueControl.IsChecked != wanted)
        {
            ClickControl(app, window, window.JoinGlueControl);
        }

        Assert.Equal(wanted, window.JoinGlueControl.IsChecked);
    }

    /// <summary>J on two parts, then what the person chooses in the popover, then Enter.</summary>
    static void JoinTwo(AppDriver app, MainWindow window, string receiving, string inserted, JointType? type = null, string? depth = null, string fastening = "None", bool glue = true, string? count = null)
    {
        Select(window, receiving, inserted);
        window.Canvas.Focus();
        app.Press(Key.J);
        Assert.True(window.IsJoining, $"J on {inserted} and {receiving} opened nothing: {window.Editor.LastMessage?.Text}");

        if (type is { } chosen && !Equals(window.JoinTypeControl(chosen).IsChecked, true))
        {
            ClickControl(app, window, window.JoinTypeControl(chosen));
        }

        if (depth is not null)
        {
            ClickControl(app, window, window.JoinDepthControl);
            app.Type(depth);
        }

        Choose(app, window, fastening);
        if (count is not null)
        {
            ClickControl(app, window, window.JoinCountControl);
            app.Type(count);
        }

        SetGlue(app, window, glue);
        app.Press(Key.Enter);
        Assert.False(window.IsJoining, $"the popover stayed open: {window.JoinRefusal}");
    }

    /// <summary>The fasteners the sample needs, from the counts worked out by hand in its expectations (27, 6, 8, 24, 10).</summary>
    [GuiWorkflow("GUI-JOIN-05")]
    public void Type_a_fastener_size_and_watch_the_shopping_list_change() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "DIY coffee table with drawers");

        // A size typed earlier for a fastener no joint needs now (nails): saving the panel must not lose it.
        FastenerChoice stale = new(FastenerKind.Nail, new Length(768), "8d", 50);
        window.Editor.Apply(new SetFastenerChoices([.. window.CurrentDesign!.Sketch.FastenerChoices, stale]), "set fastener sizes");

        app.Chord(Key.L);
        CutListWindow list = window.CutList!;
        AppDriver lists = AppDriver.Attach(list, "fasteners");
        lists.Click(CentreOf(list, list.ShoppingListTabItem));

        app.Expect("the shopping list has the five fastener lines, sized as the sample's builder typed them", () =>
        {
            string[] lines = [.. list.Extras.LinesOnScreen];
            Assert.Equal("Section\tItem\tSize\tCount\tPack\tPacks\tFor", lines[0]);
            Assert.StartsWith("Fasteners\tPocket screw, 3/4\" stock\t1-1/4 in coarse\t27\t100\t1\t", lines[1], StringComparison.Ordinal);
            Assert.StartsWith("Fasteners\tTabletop clip\tfigure-8, with screws\t10\t8\t2\t", lines[5], StringComparison.Ordinal);
        });

        lists.Click(CentreOf(list, list.SizesTabItem));
        Assert.Equal(5, list.SizeEditorRows.Children.Count);

        // Empty the tabletop clip's size (a clip is the last row); a size not chosen goes to the top of the editor.
        lists.Click(CentreOf(list, list.SizeBox(4)));
        lists.Chord(Key.A);
        lists.Press(Key.Delete);
        lists.Click(CentreOf(list, list.SaveSizesControl));

        app.Expect("with no size chosen the clip line says so and the blank row moves to the top of the editor", () =>
        {
            string[] lines = [.. list.Extras.LinesOnScreen];
            Assert.Contains("Fasteners\tTabletop clip\tsize not chosen\t10\t8\t2\t", lines.Single(line => line.Contains("Tabletop clip", StringComparison.Ordinal)), StringComparison.Ordinal);
            Assert.Equal(string.Empty, list.SizeBox(0).Text);
            Assert.Equal("Pocket screw, 3/4\" stock", ((TextBlock)((StackPanel)list.SizeEditorRows.Children[1]).Children[0]).Text!.Split(" (")[0]);
            Assert.Equal("Tabletop clip (10 needed)", ((TextBlock)((StackPanel)list.SizeEditorRows.Children[0]).Children[0]).Text);
        });

        // Type a size and a pack size, and confirm with the keyboard.
        lists.Click(CentreOf(list, list.SizeBox(0)));
        lists.Type("Z-clip, 1 in");
        lists.Press(Key.Tab);
        lists.Type("20");
        lists.Press(Key.Enter);
        lists.SaveFrame("sizes-typed");

        app.Expect("the size and the pack arithmetic follow: 10 clips in packs of 20 is one pack, and it is one undo", () =>
        {
            string clip = list.Extras.LinesOnScreen.Single(line => line.Contains("Tabletop clip", StringComparison.Ordinal));
            Assert.StartsWith("Fasteners\tTabletop clip\tZ-clip, 1 in\t10\t20\t1\t", clip, StringComparison.Ordinal);
            Assert.Equal("Z-clip, 1 in", window.CurrentDesign!.Sketch.FastenerChoices.Single(choice => choice.Kind == FastenerKind.TabletopClip).Size);
        });

        app.Expect("saving left the nail size nobody needs alone", () =>
            Assert.Contains(stale, window.CurrentDesign!.Sketch.FastenerChoices));

        // A pack size of nothing, or that is not a number, is refused in words, and nothing changes.
        lists.Click(CentreOf(list, list.PackBox(0)));
        lists.Chord(Key.A);
        lists.Type("0");
        lists.Click(CentreOf(list, list.SaveSizesControl));
        Assert.Contains("at least 1", list.SizesMessage, StringComparison.Ordinal);
        lists.Click(CentreOf(list, list.PackBox(0)));
        lists.Chord(Key.A);
        lists.Type("many");
        lists.Click(CentreOf(list, list.SaveSizesControl));

        app.Expect("a pack size that is not a number is refused where it was typed, and the design keeps its 20", () =>
        {
            Assert.Contains("whole number", list.SizesMessage, StringComparison.Ordinal);
            Assert.Equal(20, window.CurrentDesign!.Sketch.FastenerChoices.Single(choice => choice.Kind == FastenerKind.TabletopClip).PackSize);
        });

        // Undo from the drawing takes the typed clip size back to what the file had.
        FocusPaper(app, window);
        app.Chord(Key.Z);
        app.Expect("undo puts the clip size back to none chosen", () =>
            Assert.Equal(string.Empty, window.CurrentDesign!.Sketch.FastenerChoices.Single(choice => choice.Kind == FastenerKind.TabletopClip).Size));
    });

    /// <summary>Hardware typed onto a part, supplies typed onto the list, and the export saying what the screen says.</summary>
    [GuiWorkflow("GUI-JOIN-06")]
    public void Type_hardware_on_a_part_and_supplies_on_the_list_and_export_them() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "DIY coffee table with drawers");
        Box front = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Drawer front, A");
        window.Editor.Select(front.Id);

        app.Expect("the part panel shows the drawer front's pull, one line", () =>
            Assert.Equal("Drawer pull × 1", window.HardwareField.Text));

        // The Part panel scrolls; a person wheels down to the hardware box, and so does the test.
        app.Wheel(CentreOf(window, window.StockField), new Vector(0, -20));
        app.WaitForIdle();
        app.SaveFrame("hardware-field");
        app.Click(CentreOf(window, window.HardwareField));
        app.Chord(Key.A);
        app.Type("Drawer pull x 2");
        app.Press(Key.Enter);
        app.Type("Soft-close bumper × 4");
        app.Chord(Key.Enter);

        app.Expect("the part carries two hardware items, counted per copy of the part", () =>
        {
            Box now = window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Id == front.Id);
            Assert.Equal([new HardwareItem("Drawer pull", 2), new HardwareItem("Soft-close bumper", 4)], now.Part!.Hardware);
            Assert.Equal("stock kept", now.Part.Stock is null ? "stock lost" : "stock kept");
        });

        app.Chord(Key.L);
        CutListWindow list = window.CutList!;
        AppDriver lists = AppDriver.Attach(list, "hardware");
        lists.Click(CentreOf(list, list.ShoppingListTabItem));

        app.Expect("the shopping list's hardware section sums it: the pull is 2 here and 1 on drawer B, and the slides stay 2", () =>
        {
            string[] lines = [.. list.Extras.LinesOnScreen];
            Assert.Contains("Hardware\t16 in side-mount drawer slide, pair\t\t2\t\t\tDrawer box front, A, Drawer box front, B", lines);
            Assert.Contains("Hardware\tDrawer pull\t\t3\t\t\tDrawer front, A, Drawer front, B", lines);
            Assert.Contains("Hardware\tSoft-close bumper\t\t4\t\t\tDrawer front, A", lines);
        });

        lists.Click(CentreOf(list, list.SizesTabItem));
        lists.Wheel(CentreOf(list, list.SaveSizesControl), new Vector(0, -20));
        lists.WaitForIdle();
        lists.Click(CentreOf(list, list.SuppliesField));
        lists.Chord(Key.A);
        lists.Type("Wood glue");
        lists.Press(Key.Enter);
        lists.Type("Finish | one quart");
        lists.Click(CentreOf(list, list.SaveSuppliesControl));
        lists.Click(CentreOf(list, list.ShoppingListTabItem));
        lists.SaveFrame("hardware-supplies");

        app.Expect("the supplies are the two typed lines, the note in the For column, and the one line napkin adds", () =>
        {
            string[] supplies = [.. list.Extras.LinesOnScreen.Where(line => line.StartsWith("Supplies", StringComparison.Ordinal))];
            Assert.Equal(
                ["Supplies\tWood glue\t\t\t\t\t", "Supplies\tFinish\t\t\t\t\tone quart", "Supplies\tGlue: 20 of 34 joints\t\t\t\t\t"],
                supplies);
        });

        app.Expect("the export is the rows on screen, field for field, under the header line", () =>
        {
            string[][] parsed = [.. CutListCsv.Parse(list.ExtrasCsv).Select(line => line.ToArray())];

            Assert.Equal(SuppliesList.Statement, parsed[0][0]);
            Assert.Equal(list.Extras.LinesOnScreen.Select(line => line.Split('\t')), parsed.Skip(1));
        });

        // Undo takes the supplies back out, one step; the list follows the drawing.
        FocusPaper(app, window);
        app.Chord(Key.Z);
        app.Expect("one undo removes the typed supplies and leaves the hardware", () =>
        {
            Assert.Equal(3, window.CurrentDesign!.Sketch.Supplies.Count);
            Assert.Contains("Hardware\tSoft-close bumper\t\t4\t\t\tDrawer front, A", list.Extras.LinesOnScreen);
        });
    });

    /// <summary>Clicks empty paper in the drawing's window, so the keyboard is the drawing's and not a text field's.</summary>
    static void FocusPaper(AppDriver app, MainWindow window)
    {
        window.Activate();
        app.Click(new Point(100, 520));
    }

    /// <summary>Points at a control, lets the layout settle, and clicks it: a sheet that has just opened lays itself out first.</summary>
    static void ClickControl(AppDriver app, MainWindow window, Visual control)
    {
        app.MoveTo(CentreOf(window, control));
        app.WaitForIdle();
        app.Click(CentreOf(window, control));
    }

    static Point InWindow(MainWindow window, Point onCanvas)
    {
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    static Point At(MainWindow window, Point2 world) => InWindow(window, window.Canvas.View.ToScreen(world));

    static void OpenSample(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.SamplesMenuItem));

        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        Size size = control.Bounds.Size;
        return topLeft + new Point(size.Width / 2, size.Height / 2);
    }
}

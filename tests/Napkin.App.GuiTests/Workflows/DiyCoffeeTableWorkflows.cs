using System.Collections.Immutable;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.Designs;
using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// A normal DIY builder designs a coffee table with two drawers, from an empty sheet to a cut list
/// and a shopping list, with nothing but a pointer and a keyboard (GUI-DIY-01, issue #152).
/// </summary>
/// <remarks>
/// <para>
/// The table is the worked example of <c>docs/design/joinery-and-fasteners.md</c> §2: 42" x 22" x
/// 17", a 3/4" plywood top on four 2x2 legs, 1x6 aprons, a 1x2 front rail, a 1x6 web between the
/// two drawers, and drawer boxes of 1/2" plywood with a 1/4" plywood bottom and a 1x6 front. It is
/// simplified where that note allows: the drawer runners and slide cleats are left out (so 22 parts
/// and 32 joints, not the sample's 24 and 34). Every part is drawn at the note's <em>drawn</em> size,
/// and then it is joined the way a person would with the join tool: Shift+J on the legs, aprons and
/// rail (pocket-screw butt joints), on the web and what it meets, on the whole drawer (grooves, rabbets,
/// brads), J on the drawer front with a typed screw count, and Shift+J on the top and what it rests on
/// (tabletop clips). A pull is typed on the drawer front, and a pocket-screw size and a supplies line in
/// the cut-list window; the cut list is then checked for finished sizes and joinery sentences, the
/// shopping list for the fastener counts and the glue line.
/// </para>
/// <para>
/// The drawer is drawn once and mirrored across the middle of the drawing for the second, which
/// lands it exactly where the note's 18 3/8" pitch does. Every expected number below is worked out
/// by hand from the sizes typed, in the comments beside it; none is copied from napkin's output.
/// </para>
/// <para>
/// Written once against <see cref="IGuiDriver"/>: it runs headless in CI as a workflow, and live,
/// watched, with <c>dotnet run --project tools/Napkin.Demo -- run GUI-DIY-01</c> (#151).
/// </para>
/// </remarks>
public class DiyCoffeeTableWorkflows
{
    [GuiWorkflow("GUI-DIY-01")]
    public void A_DIY_coffee_table_with_drawers_from_stock_to_cut_list() => GuiWorkflow.Run(DesignTheTable);

    [GuiScenario("GUI-DIY-01", "A DIY coffee table with drawers, from stock to cut list")]
    public static void DesignTheTable(IGuiDriver app)
    {
        MainWindow window = (MainWindow)app.Target;
        ScriptedFiles files = new();
        window.FilePicker = files;

        // ---- The top ------------------------------------------------------------------------
        app.Say("A DIY coffee table with two drawers: 42 x 22 x 17 in, planned the way you would on paper");
        app.Chord(Key.N);

        app.Say("First the top: a sheet of 3/4 plywood, 42 x 22, sitting 16 1/4 up");
        Place(app, window, StockCategory.SheetGood, "3/4 plywood", At(window, Point2.Inches(-20, -8)), At(window, Point2.Inches(20, 8)));
        FoldDrawer(app, window);
        SetSize(app, window, SizeAxis.Height, "22");
        SetSize(app, window, SizeAxis.Width, "42");
        Fields(app, window, "Top", "0", "0", "16 1/4");
        ViewMenu(app, window, "ZoomToFitMenuItem");

        app.Expect("the top is 42 x 22 x 3/4 of plywood, its underside 16 1/4 up (16 1/4 + 3/4 = 17, the height)", () =>
        {
            Box top = Only(window, "Top");
            Assert.Equal("3/4 plywood", top.Part!.Stock);
            AssertBox(top, In(0), In(0), In(16, 1, 4), In(42), In(22), In(0, 3, 4));
        });

        app.SaveFrame("top");

        // ---- The legs -----------------------------------------------------------------------
        app.Say("One leg from the 2x2 stock, 16 1/4 tall, stood up in the front corner");
        Place(app, window, StockCategory.DimensionalLumber, "2x2", OnTheBench(window, false).Start, OnTheBench(window, false).End);
        SetSize(app, window, SizeAxis.Width, "16 1/4");
        app.Press(Key.Y);
        Fields(app, window, "Leg", "1 1/2", "1 1/2", "0");

        app.Expect("the leg stands 1 1/2 x 1 1/2 and 16 1/4 tall on the floor, inset 1 1/2 from the top's edges", () =>
        {
            Box leg = Only(window, "Leg");
            Assert.Equal("2x2", leg.Part!.Stock);
            AssertBox(leg, In(1, 1, 2), In(1, 1, 2), In(0), In(1, 1, 2), In(1, 1, 2), In(16, 1, 4));
        });

        app.Say("Mirror it east-west (M), then both front legs north-south (Shift+M): four legs");
        ClickAt(app, window, At(window, Point2.Inches(2, 2)));
        app.Press(Key.M);
        ClickAt(app, window, At(window, Point2.Inches(2, 2)), KeyModifiers.Shift);
        app.Press(Key.M, KeyModifiers.Shift);

        app.Expect("four legs stand at the four corners of a 39 x 19 frame (42 - 3, 22 - 3)", () =>
        {
            Box[] legs = [.. Boxes(window).Where(box => box.Part?.Stock == "2x2")];
            Assert.Equal(4, legs.Length);

            // Inset 1 1/2 from each edge: west 1 1/2 and east 42 - 1 1/2 - 1 1/2 = 39; south 1 1/2 and
            // north 22 - 1 1/2 - 1 1/2 = 19.
            foreach ((Length x, Length y) in new[] { (In(1, 1, 2), In(1, 1, 2)), (In(39), In(1, 1, 2)), (In(1, 1, 2), In(19)), (In(39), In(19)) })
            {
                Assert.Single(legs, box => SpaceSnapResolver.Extent(box).Low.X == x && SpaceSnapResolver.Extent(box).Low.Y == y);
            }
        });

        app.SaveFrame("legs");

        // ---- The frame ----------------------------------------------------------------------
        app.Say("The back apron: 1x6, 36 long, on edge between the back legs, flush with their outside");
        Place(app, window, StockCategory.DimensionalLumber, "1x6", OnTheBench(window, false).Start, OnTheBench(window, false).End);
        SetSize(app, window, SizeAxis.Width, "36");
        app.Press(Key.X);
        Fields(app, window, "Apron, back", "3", "19 3/4", "10 3/4");

        app.Say("A side apron, 16 long, on edge; mirrored for the other side");
        Place(app, window, StockCategory.DimensionalLumber, "1x6", OnTheBench(window, true).Start, OnTheBench(window, true).End);
        SetSize(app, window, SizeAxis.Height, "16");
        app.Press(Key.Y);
        Fields(app, window, "Apron, side, west", "1 1/2", "3", "10 3/4");
        ClickAt(app, window, OnPlan(window, "Apron, side, west"));
        app.Press(Key.M);

        app.Say("The front rail: 1x2 on edge, 36 long, high up, so the drawers can slide under it");
        Place(app, window, StockCategory.DimensionalLumber, "1x2", OnTheBench(window, false).Start, OnTheBench(window, false).End);
        SetSize(app, window, SizeAxis.Width, "36");
        app.Press(Key.X);
        Fields(app, window, "Rail, front", "3", "1 1/2", "14 3/4");

        app.Expect("the aprons and the rail are flush with the legs' outside faces and tucked under the top", () =>
        {
            // Legs run 1 1/2 to 40 1/2 east-west and 1 1/2 to 20 1/2 north-south (19 + 1 1/2).
            // Aprons are 3/4 thick: back 20 1/2 - 3/4 = 19 3/4, east side 40 1/2 - 3/4 = 39 3/4.
            AssertBox(Only(window, "Apron, back"), In(3), In(19, 3, 4), In(10, 3, 4), In(36), In(0, 3, 4), In(5, 1, 2));
            AssertBox(Only(window, "Apron, side, west"), In(1, 1, 2), In(3), In(10, 3, 4), In(0, 3, 4), In(16), In(5, 1, 2));
            AssertBox(Only(window, "Apron, side, west (2)"), In(39, 3, 4), In(3), In(10, 3, 4), In(0, 3, 4), In(16), In(5, 1, 2));
            AssertBox(Only(window, "Rail, front"), In(3), In(1, 1, 2), In(14, 3, 4), In(36), In(0, 3, 4), In(1, 1, 2));

            // Their tops meet the top's underside: 10 3/4 + 5 1/2 = 16 1/4 and 14 3/4 + 1 1/2 = 16 1/4.
            Length underside = SpaceSnapResolver.Extent(Only(window, "Top")).Low.Z;
            foreach (string name in new[] { "Apron, back", "Apron, side, west", "Apron, side, west (2)", "Rail, front" })
            {
                Assert.Equal(underside, SpaceSnapResolver.Extent(Only(window, name)).High.Z);
            }
        });

        app.Say("Joinery: select the four legs, the three aprons and the rail, and press Shift+J: pocket-screw butt joints, glued, in one step");
        Box[] legBoxes = [.. Boxes(window).Where(box => box.Part?.Stock == "2x2")];
        SelectByClicking(app, window, [.. new[] { "Apron, back", "Apron, side, west", "Apron, side, west (2)", "Rail, front" }.Select(name => OnPlan(window, name)), .. legBoxes.Select(leg => OnPlan(window, leg))]);
        app.Press(Key.J, KeyModifiers.Shift);

        app.Expect("the popover lists the eight touching pairs (each apron end on a leg, each rail end on a leg), all ticked", () =>
        {
            Assert.True(window.IsJoining);
            Assert.Equal(8, window.JoinPairRows.Children.Count);
            Assert.True(window.JoinHeadline.Contains("8 of 8", StringComparison.Ordinal), string.Join("\n", window.JoinPairRows.GetVisualDescendants().OfType<CheckBox>().Select(b => b.IsChecked + " " + b.Content)));
        });

        Choose(app, window, "Pocket screws");
        app.Press(Key.Enter);

        app.Expect("eight glued butt joints with pocket screws, and 22 screws by the recipe: six apron ends at 3, two rail ends at 2", () =>
        {
            Joint[] joints = [.. window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>()];
            Assert.Equal(8, joints.Length);
            Assert.All(joints, joint => Assert.Equal((JointType.Butt, FasteningKind.PocketScrews, true), (joint.Type, joint.Fastening.Kind, joint.Glue)));
            Assert.Equal(22, FastenerList.Of(window.CurrentDesign!.Sketch).Single(row => row.Kind == FastenerKind.PocketScrew).Count);
        });

        app.SaveFrame("frame");

        // ---- The web between the drawers ----------------------------------------------------
        app.Say("The web between the two drawers: draw a plain rectangle (R), then say what it is - 1x6 stock");
        FoldDrawer(app, window);
        FocusPaper(app, window);
        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(6, 6)), At(window, Point2.Inches(9, 9)), At(window, Point2.Inches(12, 12)));
        ClickControl(app, window, window.IsPartField);
        Fill(app, window, window.StockField, "1x6");
        app.Press(Key.Enter);
        SetSize(app, window, SizeAxis.Width, "17 1/2");
        app.Press(Key.X);
        app.Press(Key.Z);
        Fields(app, window, "Web", "20 5/8", "2 1/4", "10 3/4");

        app.Expect("the web is a 1x6 on edge, 17 1/2 long (19 - 3/4 - 3/4) and centred: 20 5/8 to 21 3/8 east", () =>
        {
            Box web = Only(window, "Web");
            Assert.Equal("1x6", web.Part!.Stock);
            AssertBox(web, In(20, 5, 8), In(2, 1, 4), In(10, 3, 4), In(0, 3, 4), In(17, 1, 2), In(5, 1, 2));
        });

        app.Say("The web is pocket-screwed to the back apron and the rail: select the three, Shift+J, and take the first pocket face it offers");
        SelectByClicking(app, window, OnPlan(window, "Web"), OnPlanQuarter(window, "Apron, back"), OnPlanQuarter(window, "Rail, front"));
        app.Press(Key.J, KeyModifiers.Shift);

        app.Expect("two pairs (the apron and the rail do not touch each other), each with a pocket face to choose because the web is central", () =>
        {
            Assert.True(window.IsJoining && window.JoinPairRows.Children.Count == 2, window.IsJoining + " " + window.Editor.LastMessage?.Text + " sel " + string.Join(",", window.Editor.Selection.Order().Select(id => window.CurrentDesign!.Sketch.Find(id)!.Name)));
            Assert.Equal(2, Enumerable.Range(0, 2).Count(i => window.JoinPairPocket(i) is not null));
        });

        Choose(app, window, "Pocket screws");
        app.Press(Key.Enter);

        app.Expect("the web's north end has 3 screws and its south end 2 (5 1/2 and 1 1/2 in contact): 22 + 5 = 27 pocket screws, ten joints", () =>
        {
            Joint[] pocket = [.. window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>()];
            Assert.Equal(10, pocket.Length);
            Assert.Equal(27, FastenerList.Of(window.CurrentDesign!.Sketch).Single(row => row.Kind == FastenerKind.PocketScrew).Count);
        });

        app.SaveFrame("web-joined");

        // ---- One drawer ---------------------------------------------------------------------
        app.Say("One drawer box of 1/2 plywood: a side, 16 long and 3 1/2 tall...");
        FoldDrawer(app, window);
        FocusPaper(app, window);
        Place(app, window, StockCategory.SheetGood, "1/2 plywood", OnTheBench(window, false).Start, OnTheBench(window, false).End);
        SetSize(app, window, SizeAxis.Height, "3 1/2");
        SetSize(app, window, SizeAxis.Width, "16");
        app.Press(Key.X);
        app.Press(Key.Z);
        Fields(app, window, "Drawer side, left, A", "3 1/2", "1 1/2", "11");

        app.Say("...copied (D) for the other side");
        ClickAt(app, window, OnPlan(window, "Drawer side, left, A"));
        app.Press(Key.D);
        Fields(app, window, "Drawer side, right, A", "19 5/8", "1 1/2", "11");

        app.Say("The box front, 15 5/8 wide (16 5/8 less two sides), and a copy for the back");
        Place(app, window, StockCategory.SheetGood, "1/2 plywood", OnTheBench(window, false).Start, OnTheBench(window, false).End);
        SetSize(app, window, SizeAxis.Height, "3 1/2");
        SetSize(app, window, SizeAxis.Width, "15 5/8");
        app.Press(Key.X);
        Fields(app, window, "Drawer box front, A", "4", "1 1/2", "11");
        ClickAt(app, window, OnPlan(window, "Drawer box front, A"));
        app.Press(Key.D);
        Fields(app, window, "Drawer box back, A", "4", "17", "11");

        app.Say("The bottom: 1/4 plywood, 15 5/8 x 15, lying flat");
        Place(app, window, StockCategory.SheetGood, "1/4 plywood", OnTheBench(window, false).Start, OnTheBench(window, false).End);
        SetSize(app, window, SizeAxis.Height, "15");
        SetSize(app, window, SizeAxis.Width, "15 5/8");
        Fields(app, window, "Drawer bottom, A", "4", "2", "11 1/2");

        app.Say("And the drawer front: 1x6, 17 3/4 wide, overlaying the opening");
        Place(app, window, StockCategory.DimensionalLumber, "1x6", OnTheBench(window, false).Start, OnTheBench(window, false).End);
        SetSize(app, window, SizeAxis.Width, "17 3/4");
        app.Press(Key.X);
        Fields(app, window, "Drawer front, A", "3 1/8", "3/4", "10 3/4");

        app.Say("Hardware goes on the part it is fixed to: one pull on the drawer front, typed in the Part panel");
        app.Wheel(CentreOf(window, window.StockField), new Vector(0, -20));
        app.WaitForIdle();
        app.Click(CentreOf(window, window.HardwareField));
        app.Chord(Key.A);
        app.Type("Drawer pull x 1");
        app.Chord(Key.Enter);

        app.Expect("the drawer front carries one pull", () =>
            Assert.Equal([new HardwareItem("Drawer pull", 1)], Only(window, "Drawer front, A").Part!.Hardware));

        app.Expect("drawer A is a 16 5/8 wide box (15 5/8 + two 1/2 sides) in the 17 5/8 opening, 16 deep, 3 1/2 tall", () =>
        {
            AssertBox(Only(window, "Drawer side, left, A"), In(3, 1, 2), In(1, 1, 2), In(11), In(0, 1, 2), In(16), In(3, 1, 2));
            AssertBox(Only(window, "Drawer side, right, A"), In(19, 5, 8), In(1, 1, 2), In(11), In(0, 1, 2), In(16), In(3, 1, 2));
            AssertBox(Only(window, "Drawer box front, A"), In(4), In(1, 1, 2), In(11), In(15, 5, 8), In(0, 1, 2), In(3, 1, 2));
            AssertBox(Only(window, "Drawer box back, A"), In(4), In(17), In(11), In(15, 5, 8), In(0, 1, 2), In(3, 1, 2));
            AssertBox(Only(window, "Drawer bottom, A"), In(4), In(2), In(11, 1, 2), In(15, 5, 8), In(15), In(0, 1, 4));
            AssertBox(Only(window, "Drawer front, A"), In(3, 1, 8), In(0, 3, 4), In(10, 3, 4), In(17, 3, 4), In(0, 3, 4), In(5, 1, 2));

            // Outside to outside the box is 19 5/8 + 1/2 - 3 1/2 = 16 5/8, in an opening from the leg's
            // inside face (3) to the web (20 5/8) of 17 5/8: 1/2 to spare on each side for the slides.
            Assert.Equal(
                In(16, 5, 8),
                SpaceSnapResolver.Extent(Only(window, "Drawer side, right, A")).High.X
                - SpaceSnapResolver.Extent(Only(window, "Drawer side, left, A")).Low.X);
        });

        app.SaveFrame("drawer-a");

        // The drawer front goes on the box front with 4 screws (more than the recipe's 3), and no glue.
        app.Say("Joinery: the drawer front is screwed to the box front, 4 screws, no glue (J on the two parts)");
        FoldDrawer(app, window);
        Box[] drawerA = [.. new[] { "Drawer side, left, A", "Drawer side, right, A", "Drawer box front, A", "Drawer box back, A", "Drawer bottom, A", "Drawer front, A" }.Select(name => Only(window, name))];
        SelectParts(window, "Drawer front, A", "Drawer box front, A");
        app.Press(Key.J);
        Choose(app, window, "Screws");
        ClickControl(app, window, window.JoinCountControl);
        app.Type("4");
        SetGlue(app, window, false);
        app.Press(Key.Enter);

        app.Expect("one butt joint with 4 screws typed over the recipe's 3, unglued", () =>
        {
            Joint screws = Assert.Single(Joints(window), joint => joint.Fastening.Kind == FasteningKind.Screws);
            Assert.Equal((Only(window, "Drawer box front, A").Id, JointType.Butt, 4, false), (screws.Inserted.Box, screws.Type, screws.Fastening.Count, screws.Glue));
        });

        // The rest of the drawer in three rounds of Shift+J on the whole drawer.
        app.Say("Now the whole drawer selected: Shift+J, grooves first - the bottom rides in a 1/4 in groove all round, no fastener, no glue");
        SelectParts(window, [.. drawerA.Select(box => box.Name)]);
        app.Press(Key.J, KeyModifiers.Shift);
        ChooseType(app, window, JointType.Groove);

        TypeDepth(app, window, "1/4");
        Choose(app, window, "None");
        SetGlue(app, window, false);
        // The drawer front's back face also meets the sides' ends, and a side would "sit in a groove" in it: not that, so leave those two unticked.
        UntickPairsWith(app, window, "Drawer front, A");
        app.SaveFrame("drawer-grooves");
        app.Press(Key.Enter);

        app.Say("Shift+J again: rabbet the box front into each side, 1/4 in deep, brads and glue - tick just those two pairs");
        app.Press(Key.J, KeyModifiers.Shift);
        ChooseType(app, window, JointType.Rabbet);
        TypeDepth(app, window, "1/4");
        Choose(app, window, "Brads");
        SetGlue(app, window, true);
        TickPair(app, window, "Drawer box front, A", "Drawer side, left, A");
        TickPair(app, window, "Drawer box front, A", "Drawer side, right, A");
        app.Press(Key.Enter);
        Assert.False(window.IsJoining, "the rabbet round stayed open: " + window.JoinRefusal);

        app.Say("And once more: the box back is butted between the sides, brads and glue");
        app.Press(Key.J, KeyModifiers.Shift);
        ChooseType(app, window, JointType.Butt);

        Choose(app, window, "Brads");
        SetGlue(app, window, true);
        app.SaveFrame("drawer-butts");
        app.Press(Key.Enter);

        app.Expect("drawer A holds nine joints", () =>
        {
            Assert.True(9 == Joints(window).Count(joint => drawerA.Any(box => box.Id == joint.Inserted.Box)), string.Join("\n", Joints(window).Select(j => $"{window.CurrentDesign!.Sketch.Find(j.Inserted.Box)!.Name} > {window.CurrentDesign!.Sketch.Find(j.Receiving.Box)!.Name} {j.Type} {j.Fastening.Kind}")));
        });

        // ---- The second drawer --------------------------------------------------------------
        app.Say("The whole drawer is still selected: mirror it, joints and all, for the second drawer");
        app.Press(Key.M);

        app.Expect("a second drawer stands on the other side of the web, each part at 42 minus its mirror image's far edge", () =>
        {
            Assert.Equal(22, Boxes(window).Count());

            // Drawer A spans 3 1/2 to 20 1/8, so drawer B spans 42 - 20 1/8 = 21 7/8 to 42 - 3 1/2 = 38 1/2:
            // the web's east face (21 3/8) plus 1/2, and 1/2 clear of the east leg (39).
            AssertBox(Only(window, "Drawer side, left, A (2)"), In(38), In(1, 1, 2), In(11), In(0, 1, 2), In(16), In(3, 1, 2));
            AssertBox(Only(window, "Drawer side, right, A (2)"), In(21, 7, 8), In(1, 1, 2), In(11), In(0, 1, 2), In(16), In(3, 1, 2));
            AssertBox(Only(window, "Drawer box front, A (2)"), In(22, 3, 8), In(1, 1, 2), In(11), In(15, 5, 8), In(0, 1, 2), In(3, 1, 2));
            AssertBox(Only(window, "Drawer box back, A (2)"), In(22, 3, 8), In(17), In(11), In(15, 5, 8), In(0, 1, 2), In(3, 1, 2));
            AssertBox(Only(window, "Drawer bottom, A (2)"), In(22, 3, 8), In(2), In(11, 1, 2), In(15, 5, 8), In(15), In(0, 1, 4));
            AssertBox(Only(window, "Drawer front, A (2)"), In(21, 1, 8), In(0, 3, 4), In(10, 3, 4), In(17, 3, 4), In(0, 3, 4), In(5, 1, 2));
        });

        app.Expect("the mirror carried the joints: 18 in the two drawers, nine each, and 28 all told", () =>
        {
            Assert.Equal(28, Joints(window).Length);
            Assert.Equal(9, Joints(window).Count(joint => Only(window, "Drawer bottom, A (2)").Id == joint.Inserted.Box || Only(window, "Drawer box front, A (2)").Id == joint.Inserted.Box || Only(window, "Drawer box back, A (2)").Id == joint.Inserted.Box || Only(window, "Drawer front, A (2)").Id == joint.Inserted.Box));
        });

        app.Say("The top is fixed with tabletop clips, not rigid screws: the wood moves with the seasons - select the top and what it sits on, Shift+J");
        SelectParts(window, "Top", "Apron, back", "Apron, side, west", "Apron, side, west (2)", "Rail, front");
        app.Press(Key.J, KeyModifiers.Shift);
        SetGlue(app, window, false);
        app.Press(Key.Enter);

        app.Expect("four tabletop joints, clips and no glue: 3 + 2 + 2 + 3 = 10 clips, and 32 joints in all", () =>
        {
            Joint[] clips = [.. Joints(window).Where(joint => joint.Type == JointType.Tabletop)];
            Assert.Equal(4, clips.Length);
            Assert.All(clips, joint => Assert.Equal((FasteningKind.Clips, false), (joint.Fastening.Kind, joint.Glue)));
            Assert.Equal(10, FastenerList.Of(window.CurrentDesign!.Sketch).Single(row => row.Kind == FastenerKind.TabletopClip).Count);
            Assert.Equal(32, Joints(window).Length);
        });

        app.SaveFrame("two-drawers");

        // ---- In three dimensions ------------------------------------------------------------
        app.Say("Look at it in 3D (V), turn it a little with the arrow keys...");
        FoldDrawer(app, window);
        FocusPaper(app, window);
        app.Press(Key.V);
        double azimuth = window.Model.Camera.AzimuthDegrees;
        app.Press(Key.Left);
        app.Press(Key.Left);
        ViewMenu(app, window, "ZoomToFitMenuItem");

        app.Expect("the 3D view is showing the design, turned by the keys", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.NotEqual(azimuth, window.Model.Camera.AzimuthDegrees);
        });

        app.SaveFrame("3d");
        app.Say("...then the Z button looks straight down, and Y from the front");
        app.Click(CentreOf(window, window.FindControl<Button>("LookZToolButton")!));

        app.Expect("Z looks from above, straight down", () =>
        {
            Assert.Equal(90.0, Math.Round(window.Model.Camera.ElevationDegrees, 6));
            Assert.Equal(0.0, Math.Round(window.Model.Camera.AzimuthDegrees, 6));
        });

        app.SaveFrame("3d-from-above");
        app.Click(CentreOf(window, window.FindControl<Button>("LookYToolButton")!));

        app.Expect("Y looks from the south, level: the drawers' fronts face us", () =>
        {
            Assert.Equal(0.0, Math.Round(window.Model.Camera.ElevationDegrees, 6));
            Assert.Equal(0.0, Math.Round(window.Model.Camera.AzimuthDegrees, 6));
        });

        app.SaveFrame("3d-from-front");

        // ---- What to cut, and what to buy ---------------------------------------------------
        app.Say("The cut list (Ctrl+L): every piece to cut, and how many");
        app.Chord(Key.L);

        app.Expect("the cut list has the twelve rows worked out by hand: 22 pieces, finished sizes, the joinery sentences", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.Contains("12 rows, 22 pieces to cut", list.Headline, StringComparison.Ordinal);
            AssertCutList(list.Rows.Rows);
        });

        app.Expect("the CSV is the hand-written one, and the table on screen is the CSV row for row: label, quantity, sizes, material, and the joinery sentences", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.Equal(ExpectedCutListCsv, list.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries));

            // On screen a row is a line of tab-separated fields, followed by one line per sentence.
            List<(string[] Fields, List<string> Sentences)> shown = [];
            foreach (string line in list.Rows.LinesOnScreen.Skip(1))
            {
                if (line.Contains('\t', StringComparison.Ordinal))
                {
                    shown.Add((line.Split('\t'), []));
                }
                else
                {
                    shown[^1].Sentences.Add(line);
                }
            }

            string[][] csv = [.. CutListCsv.Parse(list.Csv).Skip(2).Select(row => row.ToArray())];
            Assert.Equal(csv.Length, shown.Count);
            for (int row = 0; row < csv.Length; row++)
            {
                Assert.Equal([.. csv[row].Take(5), csv[row][5]], shown[row].Fields);
                Assert.Equal(csv[row][7], string.Join("; ", shown[row].Sentences));
            }
        });

        app.SaveFrame("cut-list");

        app.Say("The shopping list (Ctrl+Shift+L): what to buy for those pieces");
        app.Chord(Key.L, KeyModifiers.Shift);

        app.Expect("the shopping list is boards of stocked lengths, and sheets counted by area", () =>
        {
            Assert.True(window.CutList!.IsShowingShoppingList);
            AssertShoppingList(window.CutList!.ShoppingRows.Rows);
        });

        app.Say("Below the boards: the fasteners the joints need, counted for you, with the drawer pull typed on the drawer front");
        app.Expect("27 pocket screws, 8 wood screws, 24 brads and 10 clips (sizes not chosen yet), the pull, and the glue line: 18 of 32 joints are glued", () =>
        {
            string[] extras = [.. window.CutList!.Extras.LinesOnScreen];
            Assert.Equal("Section\tItem\tSize\tCount\tPack\tPacks\tFor", extras[0]);

            // Pocket screws: 6 apron ends x 3 + 2 rail ends x 2 + the web's 3 + 2 = 27. Wood screws: 4 in each
            // of two drawer fronts = 8. Brads: 3 in each of 4 rabbets and 4 back joints, a drawer's worth twice = 24. Clips: 3 + 3 + 2 + 2 = 10.
            Assert.Equal(
                ["Fasteners\tPocket screw, 3/4\" stock\tsize not chosen\t27", "Fasteners\tWood screw, 1/2\" stock\tsize not chosen\t8", "Fasteners\tBrad, 1/2\" stock\tsize not chosen\t24", "Fasteners\tTabletop clip\tsize not chosen\t10"],
                extras.Skip(1).Take(4).Select(line => string.Join("\t", line.Split('\t').Take(4))));
            Assert.Contains(extras, line => line.StartsWith("Hardware\tDrawer pull\t\t2\t", StringComparison.Ordinal));

            // Glued: 10 pocket-screw joints + 4 rabbets + 4 back joints = 18 of 10 + 18 + 4 = 32.
            Assert.Equal("Supplies\tGlue: 18 of 32 joints\t\t\t\t\t", extras[^1]);
        });

        app.Expect("both tables fit the window: no column, the 'Bd ft' ones included, runs past the right edge", () =>
        {
            CutListWindow list = window.CutList!;
            foreach (Control table in new Control[] { list.ShoppingRows, list.Extras })
            {
                ScrollViewer scroller = table.GetVisualAncestors().OfType<ScrollViewer>().First();
                Assert.True(table.Bounds.Width <= scroller.Viewport.Width + 0.5, $"{table.GetType().Name} is {table.Bounds.Width} wide in a {scroller.Viewport.Width} viewport");
            }

            // The last two board-foot headings are on screen: right edge of "Waste" inside the viewport.
            Control waste = list.ShoppingRows.GetVisualDescendants().OfType<Control>().First(cell => cell is TextBlock { Text: "Waste" } || cell is Button { Content: TextBlock { Text: "Waste" } });
            Assert.True(waste.TranslatePoint(new Point(waste.Bounds.Width, 0), list)!.Value.X <= list.Bounds.Width, "the Waste heading is cut off");
        });

        app.SaveFrame("shopping-list");

        // ---- Say what you will buy ----------------------------------------------------------
        app.Say("Type the size of pocket screw you will buy, and a pack size, on the Sizes tab; and a line of supplies");
        TypeSizesAndSupplies(app, window);

        app.Expect("100 in a pack is one pack for 27 pocket screws; the other sizes are still to choose; the supplies are the typed line, then napkin's glue line", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.Equal("1-1/4 in coarse", window.CurrentDesign!.Sketch.FastenerChoices.Single(choice => choice.Kind == FastenerKind.PocketScrew).Size);
            string[] extras = [.. list.Extras.LinesOnScreen];
            Assert.StartsWith("Fasteners\tPocket screw, 3/4\" stock\t1-1/4 in coarse\t27\t100\t1\t", extras[1], StringComparison.Ordinal);
            Assert.Equal("Fasteners\tTabletop clip\tsize not chosen\t10", string.Join("\t", extras.Single(line => line.Contains("Tabletop clip", StringComparison.Ordinal)).Split('\t').Take(4)));
            Assert.Equal(["Supplies\tWood glue\t\t\t\t\t", "Supplies\tGlue: 18 of 32 joints\t\t\t\t\t"], extras.Where(line => line.StartsWith("Supplies", StringComparison.Ordinal)));

            // The export is the rows on screen, field for field, under the header line.
            string[][] parsed = [.. CutListCsv.Parse(list.ExtrasCsv).Select(line => line.ToArray())];
            Assert.Equal(SuppliesList.Statement, parsed[0][0]);
            Assert.Equal(extras.Select(line => line.Split('\t')), parsed.Skip(1));
        });

        app.SaveFrame("sizes-typed");

        app.Expect("the whole design is 42 x 22 x 17 in: the top's 42 x 22, and 16 1/4 + 3/4 = 17 to its upper face", () =>
        {
            Length lowX = In(0), lowY = In(0), lowZ = In(0), highX = In(0), highY = In(0), highZ = In(0);
            foreach (Box box in Boxes(window))
            {
                (Point3 boxLow, Point3 boxHigh) = SpaceSnapResolver.Extent(box);
                lowX = Length.Min(lowX, boxLow.X);
                lowY = Length.Min(lowY, boxLow.Y);
                lowZ = Length.Min(lowZ, boxLow.Z);
                highX = Length.Max(highX, boxHigh.X);
                highY = Length.Max(highY, boxHigh.Y);
                highZ = Length.Max(highZ, boxHigh.Z);
            }

            Assert.Equal((In(0), In(0), In(0)), (lowX, lowY, lowZ));
            Assert.Equal((In(42), In(22), In(17)), (highX, highY, highZ));
        });

        // ---- Save it, close it, open it again -----------------------------------------------
        app.Say("Save it (Ctrl+S), start a new sheet, and open it again (Ctrl+O)");
        string path = Path.Combine(Path.GetTempPath(), $"napkin-diy-coffee-table-{Guid.NewGuid():N}.scene.json");
        try
        {
            string[] before = [.. window.CutList!.Rows.LinesOnScreen];
            files.SaveAnswer = path;
            FocusPaper(app, window);
            app.Chord(Key.S);
            app.Chord(Key.N);

            app.Expect("the new sheet is empty, and the file is on disk", () =>
            {
                Assert.Empty(Boxes(window));
                Assert.True(File.Exists(path));
            });

            files.OpenAnswer = path;
            app.Chord(Key.O);

            app.Expect("the design came back: 22 parts and the same cut list, line for line", () =>
            {
                Assert.Equal(22, Boxes(window).Count());
                Assert.Equal(before.AsEnumerable(), window.CutList!.Rows.LinesOnScreen.AsEnumerable());
                AssertCutList(window.CutList!.Rows.Rows);
            });
        }
        finally
        {
            File.Delete(path);
        }

        app.SaveFrame("reopened");
    }

    /// <summary>
    /// The cut list by hand (note section 6). Rows come largest first (length, then width, then
    /// thickness), so the order is worked out too. A label is the longest common prefix of the names
    /// in the row, cut back to a word boundary. Sizes are finished sizes: drawn plus what a joint
    /// takes into the other part (6.1). Joinery is part of a row's identity (6.3), so the box front
    /// (rabbeted and grooved) and the box back (grooved only) are two rows, and so are the drawer
    /// sides: the mirrored copy has its rabbet on the other face, and no half-turn maps one to the other.
    /// </summary>
    static void AssertCutList(ImmutableArray<CutListRow> rows)
    {
        // Sentences are in each part's own frame, as it was drawn: the aprons, the rail and the web were
        // drawn flat and stood on edge, so their inside face is the drawn top or bottom face, and the
        // rail's (north) is the opposite of the back apron's (south).
        const string Clips3 = "Fit 3 tabletop clips along the top edge on the ";
        const string Clips2 = "Fit 2 tabletop clips along the top edge on the ";
        const string ClipsEnd = " face (slot or recess per the clip's instructions).";
        (string Label, int Quantity, Length Length, Length Width, Length Thickness, string Material, string[] Joinery)[] expected =
        [
            // 42 x 22 x 3/4, one.
            ("Top", 1, In(42), In(22), In(0, 3, 4), "3/4 plywood", []),

            // 42 - 3 - 3 = 36 between the legs; 1x6 is 5 1/2 x 3/4, 1x2 is 1 1/2 x 3/4. Equal lengths put the
            // wider first. A 3/4 x 5 1/2 end gets 3 pocket screws, a 3/4 x 1 1/2 end 2 (7.2); a 36 in edge
            // gets 3 clips and a 16 in edge 2 (7.2).
            ("Apron, back", 1, In(36), In(5, 1, 2), In(0, 3, 4), "1x6",
                ["Drill 3 pocket holes in the west end and 3 in the east end, from the top face.", Clips3 + "top" + ClipsEnd]),
            ("Rail, front", 1, In(36), In(1, 1, 2), In(0, 3, 4), "1x2",
                ["Drill 2 pocket holes in the west end and 2 in the east end, from the bottom face.", Clips3 + "bottom" + ClipsEnd]),

            // 17 3/4 = 17 5/8 opening + 1/8 lap; one per drawer. Screwed on, and screws are not a cut.
            ("Drawer front", 2, In(17, 3, 4), In(5, 1, 2), In(0, 3, 4), "1x6", []),

            // 19 - 3/4 - 3/4 = 17 1/2; 3 screws in the end on the apron, 2 in the end on the rail.
            ("Web", 1, In(17, 1, 2), In(5, 1, 2), In(0, 3, 4), "1x6", ["Drill 3 pocket holes in the east end and 2 in the west end, from the bottom face."]),

            // 17 - 3/4 = 16 1/4; a 2x2 is 1 1/2 square; nothing is cut into a leg, so four legs are one row.
            ("Leg", 4, In(16, 1, 4), In(1, 1, 2), In(1, 1, 2), "2x2", []),

            // 15 5/8 + 1/4 + 1/4 = 16 1/8 long and 15 + 1/4 + 1/4 = 15 1/2 wide: a 1/4 groove on every edge.
            ("Drawer bottom", 2, In(16, 1, 8), In(15, 1, 2), In(0, 1, 4), "1/4 plywood",
                ["Length includes 1/4\" into a groove at each end; width includes 1/4\" into a groove at each edge."]),

            // 15 5/8 + 1/4 + 1/4 = 16 1/8: a rabbet 1/4 deep at each end, and the bottom's groove 1/2 up.
            ("Drawer box front", 2, In(16, 1, 8), In(3, 1, 2), In(0, 1, 2), "1/2 plywood",
                ["Length includes 1/4\" into a rabbet at each end.", "Groove the bottom face: 1/4\" wide, 1/4\" deep, 1/2\" from the south edge, full length."]),

            // 16 long: the two side aprons (5 1/2 wide, 3/4) and the four drawer sides (3 1/2, 1/2).
            ("Apron, side", 2, In(16), In(5, 1, 2), In(0, 3, 4), "1x6",
                ["Drill 3 pocket holes in the south end and 3 in the north end, from the top face.", Clips2 + "top" + ClipsEnd]),
            ("Drawer side", 2, In(16), In(3, 1, 2), In(0, 1, 2), "1/2 plywood",
                ["Rabbet the west end on the bottom face: 1/2\" wide, 1/4\" deep.", "Groove the bottom face: 1/4\" wide, 1/4\" deep, 1/2\" from the south edge, full length."]),
            ("Drawer side", 2, In(16), In(3, 1, 2), In(0, 1, 2), "1/2 plywood",
                ["Rabbet the west end on the top face: 1/2\" wide, 1/4\" deep.", "Groove the top face: 1/4\" wide, 1/4\" deep, 1/2\" from the south edge, full length."]),

            // 15 5/8 = 16 5/8 - 2 x 1/2, grooved for the bottom on the inside face; not rabbeted.
            ("Drawer box back", 2, In(15, 5, 8), In(3, 1, 2), In(0, 1, 2), "1/2 plywood", ["Groove the top face: 1/4\" wide, 1/4\" deep, 1/2\" from the south edge, full length."]),
        ];

        Assert.Equal(
            expected.Select(row => (row.Label, row.Quantity, row.Length, row.Width, row.Thickness, row.Material, string.Join("|", row.Joinery))),
            rows.Select(row => (row.Label, row.Quantity, row.Length, row.Width, row.Thickness, row.Material, string.Join("|", row.JointText))));
        Assert.Equal(22, rows.Sum(row => row.Quantity));
    }

    /// <summary>
    /// The CSV by hand: the header line the note fixes (6.4), then a line per row with the sizes as
    /// feet and inches, and the joinery sentences joined by "; " and quoted when they need it.
    /// </summary>
    static readonly string[] ExpectedCutListCsv =
    [
        "Cut list: finished sizes: joinery allowances included; before saw kerf (#138).",
        "Label,Quantity,Length,Width,Thickness,Material,Cuts,Joinery",
        "Top,1,\"3'-6\"\"\",\"1'-10\"\"\",\"3/4\"\"\",3/4 plywood,,",
        "\"Apron, back\",1,\"3'-0\"\"\",\"5 1/2\"\"\",\"3/4\"\"\",1x6,,\"Drill 3 pocket holes in the west end and 3 in the east end, from the top face.; Fit 3 tabletop clips along the top edge on the top face (slot or recess per the clip's instructions).\"",
        "\"Rail, front\",1,\"3'-0\"\"\",\"1 1/2\"\"\",\"3/4\"\"\",1x2,,\"Drill 2 pocket holes in the west end and 2 in the east end, from the bottom face.; Fit 3 tabletop clips along the top edge on the bottom face (slot or recess per the clip's instructions).\"",
        "Drawer front,2,\"1'-5 3/4\"\"\",\"5 1/2\"\"\",\"3/4\"\"\",1x6,,",
        "Web,1,\"1'-5 1/2\"\"\",\"5 1/2\"\"\",\"3/4\"\"\",1x6,,\"Drill 3 pocket holes in the east end and 2 in the west end, from the bottom face.\"",
        "Leg,4,\"1'-4 1/4\"\"\",\"1 1/2\"\"\",\"1 1/2\"\"\",2x2,,",
        "Drawer bottom,2,\"1'-4 1/8\"\"\",\"1'-3 1/2\"\"\",\"1/4\"\"\",1/4 plywood,,\"Length includes 1/4\"\" into a groove at each end; width includes 1/4\"\" into a groove at each edge.\"",
        "Drawer box front,2,\"1'-4 1/8\"\"\",\"3 1/2\"\"\",\"1/2\"\"\",1/2 plywood,,\"Length includes 1/4\"\" into a rabbet at each end.; Groove the bottom face: 1/4\"\" wide, 1/4\"\" deep, 1/2\"\" from the south edge, full length.\"",
        "\"Apron, side\",2,\"1'-4\"\"\",\"5 1/2\"\"\",\"3/4\"\"\",1x6,,\"Drill 3 pocket holes in the south end and 3 in the north end, from the top face.; Fit 2 tabletop clips along the top edge on the top face (slot or recess per the clip's instructions).\"",
        "Drawer side,2,\"1'-4\"\"\",\"3 1/2\"\"\",\"1/2\"\"\",1/2 plywood,,\"Rabbet the west end on the bottom face: 1/2\"\" wide, 1/4\"\" deep.; Groove the bottom face: 1/4\"\" wide, 1/4\"\" deep, 1/2\"\" from the south edge, full length.\"",
        "Drawer side,2,\"1'-4\"\"\",\"3 1/2\"\"\",\"1/2\"\"\",1/2 plywood,,\"Rabbet the west end on the top face: 1/2\"\" wide, 1/4\"\" deep.; Groove the top face: 1/4\"\" wide, 1/4\"\" deep, 1/2\"\" from the south edge, full length.\"",
        "Drawer box back,2,\"1'-3 5/8\"\"\",\"3 1/2\"\"\",\"1/2\"\"\",1/2 plywood,,\"Groove the top face: 1/4\"\" wide, 1/4\"\" deep, 1/2\"\" from the south edge, full length.\"",
    ];

    /// <summary>
    /// The shopping list by hand, from the library's stock lengths (6', 8', ... 16' for 1x2, 1x6
    /// and 2x2; 4' x 8' sheets for plywood). The library carries a length list for all three
    /// lumber sizes, so the boards are first-fit over 72" and nothing here says "no length list".
    /// </summary>
    static void AssertShoppingList(ImmutableArray<ShoppingListRow> rows)
    {
        Assert.Equal(
            ["1/2 plywood", "1/4 plywood", "1x2", "1x6", "2x2", "3/4 plywood"],
            rows.Select(row => row.Material));

        // Sheets are counted by area of the finished pieces, and labelled a floor. 1/2 plywood: 4 sides 16 x 3 1/2
        // = 224; 2 box fronts 16 1/8 x 3 1/2 = 112.875; 2 backs 15 5/8 x 3 1/2 = 109.375: 446.25 sq in of a
        // 4608 sq in sheet. 1/4: 2 x 16 1/8 x 15 1/2 = 499.875. 3/4: 42 x 22 = 924. One sheet each.
        foreach (string sheet in new[] { "1/2 plywood", "1/4 plywood", "3/4 plywood" })
        {
            ShoppingListRow row = Assert.Single(rows, candidate => candidate.Material == sheet);
            Assert.Equal(ShoppingListKind.Sheets, row.Kind);
            Assert.Equal(1, row.Sheets);
            Assert.Equal("1 sheet, 4'-0\" × 8'-0\"", row.BuyText);
            Assert.Equal(ShoppingList.SheetsByArea, row.Note);
        }

        // 1x2: one 36" piece into one 6' board. 1 x 2 x 72 / 144 = 1.0 board foot bought, 0.5 used.
        ShoppingListRow oneByTwo = Assert.Single(rows, row => row.Material == "1x2");
        Assert.Equal(ShoppingListKind.Boards, oneByTwo.Kind);
        Assert.Equal("1 × 6'-0\"", oneByTwo.BuyText);
        Assert.Equal(("1.0", "0.5", "0.5"), (oneByTwo.BoughtText, oneByTwo.UsedText, oneByTwo.WasteText));

        // 1x6: 36 + 2 x 17 3/4 + 17 1/2 + 2 x 16 = 121 in of pieces in 6 pieces; with a 1/8 in kerf,
        // 6 cuts = 3/4 in, 121 3/4 in: too long for a 10' (120 in), inside a 12' (144 in): one 12' board.
        // 1 x 6 x 144 / 144 = 6.0 bought; 6 x 121 / 144 = 5.04, used 5.0.
        ShoppingListRow oneBySix = Assert.Single(rows, row => row.Material == "1x6");
        Assert.Equal("1 × 12'-0\"", oneBySix.BuyText);
        Assert.Equal(("6.0", "5.0", "1.0"), (oneBySix.BoughtText, oneBySix.UsedText, oneBySix.WasteText));

        // 2x2: four legs of 16 1/4 = 65 in fit one 6' board. 2 x 2 x 72 / 144 = 2.0 bought; 4 x 65 / 144 = 1.8.
        ShoppingListRow twoByTwo = Assert.Single(rows, row => row.Material == "2x2");
        Assert.Equal("1 × 6'-0\"", twoByTwo.BuyText);
        Assert.Equal(("2.0", "1.8", "0.2"), (twoByTwo.BoughtText, twoByTwo.UsedText, twoByTwo.WasteText));
    }

    // ---- Doing things the way a person does ---------------------------------------------------

    /// <summary>
    /// Picks a stock size from its drawer (opening the drawer if it is not open) and drags it out on
    /// the paper. The drawer stays open for the next piece from it.
    /// </summary>
    static void Place(IGuiDriver app, MainWindow window, StockCategory category, string stock, Point start, Point end)
    {
        StockToolbox toolbox = window.Toolbox;
        int before = Boxes(window).Count();

        if (!(window.IsShowingStockSizes && toolbox.Category == category))
        {
            app.Click(CentreOf(window, toolbox.CategoryButtons[category]));
        }

        Button button = toolbox.ButtonFor(stock)!;
        if (stock.EndsWith("plywood", StringComparison.Ordinal))
        {
            app.Wheel(CentreOf(window, toolbox.ItemScroller), new Vector(0, -10));
        }

        app.Click(CentreOf(window, button));
        app.Drag(start, new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2), end);
        Assert.True(Boxes(window).Count() == before + 1, $"dragging out {stock} did not place a part.");
    }

    /// <summary>
    /// Where to drag a piece out: on the bare paper to the west of the table, well clear of the
    /// stock drawer, whatever the size of the window. It is sized and moved into place afterwards.
    /// </summary>
    static (Point Start, Point End) OnTheBench(MainWindow window, bool northSouth)
    {
        Point paper = InWindow(window, new Point(0, 0));
        Point west = At(window, new Point2(In(0), In(11)));
        double x = paper.X + ((west.X - paper.X) * 0.7);
        return northSouth
            ? (new Point(x, west.Y + 30), new Point(x, west.Y - 30))
            : (new Point(x, west.Y), new Point(x + 80, west.Y - 50));
    }

    /// <summary>Folds the stock drawer away, if it is open.</summary>
    static void FoldDrawer(IGuiDriver app, MainWindow window)
    {
        if (window.IsShowingStockSizes)
        {
            app.Click(CentreOf(window, window.Toolbox.CategoryButtons[window.Toolbox.Category!.Value]));
        }
    }

    /// <summary>Clicks the drawing, folding the stock drawer away first if it is lying over the spot.</summary>
    static void ClickAt(IGuiDriver app, MainWindow window, Point point, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (window.IsShowingStockSizes
            && new Rect(window.Toolbox.TranslatePoint(new Point(0, 0), window)!.Value, window.Toolbox.Bounds.Size).Contains(point))
        {
            FoldDrawer(app, window);
        }

        app.Click(point, modifiers: modifiers);
    }

    /// <summary>Clicks a dimension on the drawing and types the size it should be.</summary>
    static void SetSize(IGuiDriver app, MainWindow window, SizeAxis axis, string text)
    {
        EntityId id = window.Editor.OnlySelected!.Value;
        ClickAt(app, window, InWindow(window, window.Canvas.SelectionDimensionLabelAt(id, axis)
            ?? throw new InvalidOperationException($"{id} shows no {axis} dimension.")));
        app.Type(text);
        app.Press(Key.Enter);

        // Focus goes back to the drawing once the box has closed; the keys that turn the part wait for it.
        app.WaitForIdle();
    }

    /// <summary>Names the selected part and says where it is, in the Part panel: name, east, north, up.</summary>
    static void Fields(IGuiDriver app, MainWindow window, string name, string east, string north, string up)
    {
        // The Part panel keeps its scroll from the last part: wheel it back up to the name if that has gone off the top.
        ScrollViewer panel = window.PartNameField.GetVisualAncestors().OfType<ScrollViewer>().First();
        if (panel.Offset.Y > 0)
        {
            app.Wheel(CentreOf(window, panel), new Vector(0, 20));
            app.WaitForIdle();
        }

        Fill(app, window, window.PartNameField, name);
        app.Tab(2);
        app.Type(east);
        app.Tab();
        app.Type(north);
        app.Tab();
        app.Type(up);
        app.Press(Key.Enter);
    }

    /// <summary>
    /// Clicks a control. The toolbar and the side panels lay themselves out again as the pointer
    /// arrives and as the selection changes, so the pointer goes there first, the layout settles,
    /// and the click is aimed again.
    /// </summary>
    static void ClickControl(IGuiDriver app, MainWindow window, Visual control)
    {
        app.MoveTo(CentreOf(window, control));
        app.WaitForIdle();
        app.Click(CentreOf(window, control));
    }

    /// <summary>Replaces what a field says, the way a person does: click, select all, type.</summary>
    static void Fill(IGuiDriver app, MainWindow window, TextBox field, string text)
    {
        ClickControl(app, window, field);
        app.Chord(Key.A);
        app.Type(text);
    }

    /// <summary>
    /// Picks parts by name. The drawer's parts sit a quarter inch apart, closer than a click can tell at
    /// this zoom, so the pointer would pick the wrong one: this is the marquee's job, done without it.
    /// </summary>
    static void SelectParts(MainWindow window, params string[] names)
    {
        window.Editor.SelectAll(names.Select(name => Only(window, name).Id));
        window.Canvas.Focus();
    }

    static Joint[] Joints(MainWindow window) => [.. window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Joint>()];

    /// <summary>Clicks the first part and Shift+clicks the rest: the way a person picks several parts.</summary>
    static void SelectByClicking(IGuiDriver app, MainWindow window, params Point[] points)
    {
        ClickAt(app, window, points[0]);
        foreach (Point point in points.Skip(1))
        {
            ClickAt(app, window, point, KeyModifiers.Shift);
        }
    }

    /// <summary>Picks a fastening in the join popover with the keyboard: the list has focus and the arrow keys step through it.</summary>
    static void Choose(IGuiDriver app, MainWindow window, string fastening)
    {
        window.JoinFasteningControl.Focus();
        app.WaitForIdle();
        int target = window.JoinFasteningControl.Items.IndexOf(fastening);
        for (int presses = 0; window.JoinFasteningControl.SelectedIndex != target; presses++)
        {
            Assert.True(presses < 12, $"the fastening list did not reach {fastening}: at {window.JoinFasteningControl.SelectedItem}, joining {window.IsJoining}, {window.Editor.LastMessage?.Text}");
            app.Press(window.JoinFasteningControl.SelectedIndex < target ? Key.Down : Key.Up);
        }

        Assert.Equal(fastening, window.JoinFasteningControl.SelectedItem);
    }

    static void ChooseType(IGuiDriver app, MainWindow window, JointType type)
    {
        if (window.JoinTypeControl(type).IsChecked != true)
        {
            ClickControl(app, window, window.JoinTypeControl(type));
        }

        Assert.True(window.JoinTypeControl(type).IsChecked);
    }

    /// <summary>Types over whatever the depth box says.</summary>
    static void TypeDepth(IGuiDriver app, MainWindow window, string depth)
    {
        ClickControl(app, window, window.JoinDepthControl);
        app.Chord(Key.A);
        app.Type(depth);
    }

    static void SetGlue(IGuiDriver app, MainWindow window, bool wanted)
    {
        if (window.JoinGlueControl.IsChecked != wanted)
        {
            ClickControl(app, window, window.JoinGlueControl);
        }

        Assert.Equal(wanted, window.JoinGlueControl.IsChecked);
    }

    /// <summary>
    /// The pocket screw's size and pack on the cut-list window's Sizes tab, and a supplies line under it, typed
    /// with the pointer and keyboard. The list is a second window, which only the headless driver can reach;
    /// watched live, the same two choices are made through the design's own requests, and the captions say so.
    /// </summary>
    static void TypeSizesAndSupplies(IGuiDriver app, MainWindow window)
    {
        CutListWindow list = window.CutList!;
        if (app is not AppDriver)
        {
            window.Editor.Apply(new SetFastenerChoices([.. window.CurrentDesign!.Sketch.FastenerChoices.Where(choice => choice.Kind != FastenerKind.PocketScrew), new FastenerChoice(FastenerKind.PocketScrew, new Length(768), "1-1/4 in coarse", 100)]), "set fastener sizes");
            window.Editor.Apply(new SetSupplies([.. window.CurrentDesign!.Sketch.Supplies, new SupplyLine("Wood glue", string.Empty)]), "set supplies");
            return;
        }

        AppDriver lists = AppDriver.Attach(list, "storyboard");
        lists.Click(CentreOf(list, list.SizesTabItem));
        Assert.StartsWith("Pocket screw", ((TextBlock)((StackPanel)list.SizeEditorRows.Children[0]).Children[0]).Text!, StringComparison.Ordinal);
        lists.Click(CentreOf(list, list.SizeBox(0)));
        lists.Chord(Key.A);
        lists.Type("1-1/4 in coarse");
        lists.Tab();
        lists.Type("100");
        lists.Press(Key.Enter);
        lists.Wheel(CentreOf(list, list.SaveSizesControl), new Vector(0, -20));
        lists.WaitForIdle();
        lists.MoveTo(CentreOf(list, list.SuppliesField));
        lists.WaitForIdle();
        lists.Click(CentreOf(list, list.SuppliesField));
        lists.Type("Wood glue");
        lists.MoveTo(CentreOf(list, list.SaveSuppliesControl));
        lists.WaitForIdle();
        lists.Click(CentreOf(list, list.SaveSuppliesControl));
        lists.Click(CentreOf(list, list.ShoppingListTabItem));
        lists.SaveFrame("sizes");
    }

    /// <summary>Unticks every ticked row of the join popover that names the part.</summary>
    static void UntickPairsWith(IGuiDriver app, MainWindow window, string part)
    {
        for (int guard = 0; guard < 10; guard++)
        {
            CheckBox? tick = window.JoinPairRows.GetVisualDescendants().OfType<CheckBox>()
                .FirstOrDefault(box => box.IsChecked == true && ((string)box.Content!).Contains(part, StringComparison.Ordinal));
            if (tick is null)
            {
                return;
            }

            Toggle(app, window, tick);
        }

        Assert.Fail($"rows naming {part} stayed ticked");
    }

    /// <summary>Ticks the row of the join popover that joins <paramref name="inserted"/> to <paramref name="receiving"/>.</summary>
    static void TickPair(IGuiDriver app, MainWindow window, string inserted, string receiving)
    {
        CheckBox tick = window.JoinPairRows.GetVisualDescendants().OfType<CheckBox>()
            .Single(box => ((string)box.Content!).StartsWith($"{inserted} → {receiving} (", StringComparison.Ordinal));
        if (tick.IsChecked != true)
        {
            Toggle(app, window, tick);
        }

        Assert.True(tick.IsChecked);
    }

    /// <summary>A row far down a long popover is beyond the window: focus it and press Space, as a person tabbing to it would.</summary>
    static void Toggle(IGuiDriver app, MainWindow window, CheckBox tick)
    {
        tick.Focus();
        app.WaitForIdle();
        app.Press(Key.Space);
    }

    /// <summary>Clicks empty paper, so the keyboard is the drawing's and not a text field's.</summary>
    static void FocusPaper(IGuiDriver app, MainWindow window)
    {
        // Half way between the window's left edge of the paper and the table's west edge, level with its middle.
        Point canvasLeft = InWindow(window, new Point(0, 0));
        Point tableWest = At(window, new Point2(In(0), In(11)));
        app.Click(new Point((canvasLeft.X + tableWest.X) / 2, tableWest.Y));
    }

    static void ViewMenu(IGuiDriver app, MainWindow window, string item)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>(item)!));
    }

    // ---- Reading the drawing ------------------------------------------------------------------

    static IEnumerable<Box> Boxes(MainWindow window) => window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>();

    static Box Only(MainWindow window, string name) =>
        Assert.Single(Boxes(window), box => box.Name == name);

    /// <summary>A length from its parts, by hand: whole inches and a fraction.</summary>
    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    /// <summary>A box's low corner and its size along east, north and up.</summary>
    static void AssertBox(Box box, Length east, Length north, Length up, Length sizeEast, Length sizeNorth, Length sizeUp)
    {
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(box);
        Assert.Equal(
            (east, north, up, sizeEast, sizeNorth, sizeUp),
            (low.X, low.Y, low.Z, high.X - low.X, high.Y - low.Y, high.Z - low.Z));
    }

    /// <summary>The window coordinate of the middle of a part in the plan.</summary>
    static Point OnPlan(MainWindow window, string name) => OnPlan(window, Only(window, name));

    /// <summary>A quarter of the way along a long part from its west end, clear of the web that meets it in the middle.</summary>
    static Point OnPlanQuarter(MainWindow window, string name)
    {
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(Only(window, name));
        return At(window, new Point2(low.X + (high.X - low.X).Divide(4, Rounding.HalfToEven), (low.Y + high.Y).Divide(2, Rounding.HalfToEven)));
    }

    static Point OnPlan(MainWindow window, Box box)
    {
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(box);
        return At(window, new Point2((low.X + high.X).Divide(2, Rounding.HalfToEven), (low.Y + high.Y).Divide(2, Rounding.HalfToEven)));
    }

    static Point At(MainWindow window, Point2 world) => InWindow(window, window.Canvas.View.ToScreen(world));

    static Point InWindow(MainWindow window, Point onCanvas)
    {
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        Size size = control.Bounds.Size;
        return topLeft + new Point(size.Width / 2, size.Height / 2);
    }

    /// <summary>A file picker that answers at once, so saving and opening need no native dialog.</summary>
    sealed class ScriptedFiles : ISceneFilePicker
    {
        public string? OpenAnswer { get; set; }

        public string? SaveAnswer { get; set; }

        public Task<string?> PickSceneFileAsync() => Task.FromResult(OpenAnswer);

        public Task<string?> PickSaveDestinationAsync(string suggestedName) => Task.FromResult(SaveAnswer);
    }
}

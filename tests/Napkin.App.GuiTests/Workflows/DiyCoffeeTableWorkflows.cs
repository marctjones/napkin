using System.Collections.Immutable;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

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
/// simplified where that note allows: the drawer runners are left out, and every part is drawn at
/// the note's <em>drawn</em> size — joint allowances belong to joinery, which is designed (#144 to
/// #150) and not built, so it appears here only as captions marked "design only".
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
        Place(app, window, StockCategory.SheetGood, "3/4 plywood", Point2.Inches(-20, -8), Point2.Inches(20, 8));
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
        Place(app, window, StockCategory.DimensionalLumber, "2x2", Point2.Inches(6, 6), Point2.Inches(12, 6));
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
        app.Click(At(window, Point2.Inches(2, 2)));
        app.Press(Key.M);
        app.Click(At(window, Point2.Inches(2, 2)), modifiers: KeyModifiers.Shift);
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
        Place(app, window, StockCategory.DimensionalLumber, "1x6", Point2.Inches(6, 6), Point2.Inches(12, 6));
        SetSize(app, window, SizeAxis.Width, "36");
        app.Press(Key.X);
        Fields(app, window, "Apron, back", "3", "19 3/4", "10 3/4");

        app.Say("A side apron, 16 long, on edge; mirrored for the other side");
        Place(app, window, StockCategory.DimensionalLumber, "1x6", Point2.Inches(6, 6), Point2.Inches(6, 12));
        SetSize(app, window, SizeAxis.Height, "16");
        app.Press(Key.Y);
        Fields(app, window, "Apron, side, west", "1 1/2", "3", "10 3/4");
        app.Click(OnPlan(window, "Apron, side, west"));
        app.Press(Key.M);
        Rename(app, window, "Apron, side, east");

        app.Say("The front rail: 1x2 on edge, 36 long, high up, so the drawers can slide under it");
        Place(app, window, StockCategory.DimensionalLumber, "1x2", Point2.Inches(6, 6), Point2.Inches(12, 6));
        SetSize(app, window, SizeAxis.Width, "36");
        app.Press(Key.X);
        Fields(app, window, "Rail, front", "3", "1 1/2", "14 3/4");

        app.Expect("the aprons and the rail are flush with the legs' outside faces and tucked under the top", () =>
        {
            // Legs run 1 1/2 to 40 1/2 east-west and 1 1/2 to 20 1/2 north-south (19 + 1 1/2).
            // Aprons are 3/4 thick: back 20 1/2 - 3/4 = 19 3/4, east side 40 1/2 - 3/4 = 39 3/4.
            AssertBox(Only(window, "Apron, back"), In(3), In(19, 3, 4), In(10, 3, 4), In(36), In(0, 3, 4), In(5, 1, 2));
            AssertBox(Only(window, "Apron, side, west"), In(1, 1, 2), In(3), In(10, 3, 4), In(0, 3, 4), In(16), In(5, 1, 2));
            AssertBox(Only(window, "Apron, side, east"), In(39, 3, 4), In(3), In(10, 3, 4), In(0, 3, 4), In(16), In(5, 1, 2));
            AssertBox(Only(window, "Rail, front"), In(3), In(1, 1, 2), In(14, 3, 4), In(36), In(0, 3, 4), In(1, 1, 2));

            // Their tops meet the top's underside: 10 3/4 + 5 1/2 = 16 1/4 and 14 3/4 + 1 1/2 = 16 1/4.
            Length underside = SpaceSnapResolver.Extent(Only(window, "Top")).Low.Z;
            foreach (string name in new[] { "Apron, back", "Apron, side, west", "Apron, side, east", "Rail, front" })
            {
                Assert.Equal(underside, SpaceSnapResolver.Extent(Only(window, name)).High.Z);
            }
        });

        app.Say("Joinery (design only, #145): pocket-screw butt joints, glued, join each apron to its legs - 3 screws a 5 1/2 in end, 2 for the rail");
        app.SaveFrame("frame");

        // ---- The web between the drawers ----------------------------------------------------
        app.Say("The web between the two drawers: draw a plain rectangle (R), then say what it is - 1x6 stock");
        FocusPaper(app, window);
        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(6, 6)), At(window, Point2.Inches(9, 9)), At(window, Point2.Inches(12, 12)));
        app.Click(CentreOf(window, window.IsPartField));
        Fill(app, window, window.OutOfPlaneField, "3/4\"");
        Fill(app, window, window.StockField, "1x6");
        Fill(app, window, window.QuantityField, "1");
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

        // ---- One drawer ---------------------------------------------------------------------
        app.Say("One drawer box of 1/2 plywood: a side, 16 long and 3 1/2 tall...");
        Place(app, window, StockCategory.SheetGood, "1/2 plywood", Point2.Inches(6, 6), Point2.Inches(9, 9));
        SetSize(app, window, SizeAxis.Height, "3 1/2");
        SetSize(app, window, SizeAxis.Width, "16");
        app.Press(Key.X);
        app.Press(Key.Z);
        Fields(app, window, "Drawer side, left, A", "3 1/2", "2 1/4", "11");

        app.Say("...copied (D) for the other side");
        app.Click(OnPlan(window, "Drawer side, left, A"));
        app.Press(Key.D);
        Fields(app, window, "Drawer side, right, A", "19 5/8", "2 1/4", "11");

        app.Say("The box front, 15 5/8 wide (16 5/8 less two sides), and a copy for the back");
        Place(app, window, StockCategory.SheetGood, "1/2 plywood", Point2.Inches(6, 6), Point2.Inches(9, 9));
        SetSize(app, window, SizeAxis.Height, "3 1/2");
        SetSize(app, window, SizeAxis.Width, "15 5/8");
        app.Press(Key.X);
        Fields(app, window, "Drawer box front, A", "4", "2 1/4", "11");
        app.Click(OnPlan(window, "Drawer box front, A"));
        app.Press(Key.D);
        Fields(app, window, "Drawer box back, A", "4", "17 3/4", "11");

        app.Say("The bottom: 1/4 plywood, 15 5/8 x 15, lying flat");
        Place(app, window, StockCategory.SheetGood, "1/4 plywood", Point2.Inches(6, 6), Point2.Inches(9, 9));
        SetSize(app, window, SizeAxis.Height, "15");
        SetSize(app, window, SizeAxis.Width, "15 5/8");
        Fields(app, window, "Drawer bottom, A", "4", "2 3/4", "11 1/2");

        app.Say("And the drawer front: 1x6, 17 3/4 wide, overlaying the opening");
        Place(app, window, StockCategory.DimensionalLumber, "1x6", Point2.Inches(6, 6), Point2.Inches(12, 6));
        SetSize(app, window, SizeAxis.Width, "17 3/4");
        app.Press(Key.X);
        Fields(app, window, "Drawer front, A", "3 1/8", "3/4", "10 3/4");

        app.Expect("drawer A is a 16 5/8 wide box (15 5/8 + two 1/2 sides) in the 17 5/8 opening, 16 deep, 3 1/2 tall", () =>
        {
            AssertBox(Only(window, "Drawer side, left, A"), In(3, 1, 2), In(2, 1, 4), In(11), In(0, 1, 2), In(16), In(3, 1, 2));
            AssertBox(Only(window, "Drawer side, right, A"), In(19, 5, 8), In(2, 1, 4), In(11), In(0, 1, 2), In(16), In(3, 1, 2));
            AssertBox(Only(window, "Drawer box front, A"), In(4), In(2, 1, 4), In(11), In(15, 5, 8), In(0, 1, 2), In(3, 1, 2));
            AssertBox(Only(window, "Drawer box back, A"), In(4), In(17, 3, 4), In(11), In(15, 5, 8), In(0, 1, 2), In(3, 1, 2));
            AssertBox(Only(window, "Drawer bottom, A"), In(4), In(2, 3, 4), In(11, 1, 2), In(15, 5, 8), In(15), In(0, 1, 4));
            AssertBox(Only(window, "Drawer front, A"), In(3, 1, 8), In(0, 3, 4), In(10, 3, 4), In(17, 3, 4), In(0, 3, 4), In(5, 1, 2));

            // Outside to outside the box is 19 5/8 + 1/2 - 3 1/2 = 16 5/8, in an opening from the leg's
            // inside face (3) to the web (20 5/8) of 17 5/8: 1/2 to spare on each side for the slides.
            Assert.Equal(
                In(16, 5, 8),
                SpaceSnapResolver.Extent(Only(window, "Drawer side, right, A")).High.X
                - SpaceSnapResolver.Extent(Only(window, "Drawer side, left, A")).Low.X);
        });

        app.Say("Joinery (design only): rabbet the box front, butt the back, brads and glue; the bottom rides in a 1/4 in groove");
        app.SaveFrame("drawer-a");

        // ---- The second drawer --------------------------------------------------------------
        app.Say("Select the whole drawer with Shift+click, and mirror it: the second drawer");
        string[] drawer = ["Drawer side, left, A", "Drawer side, right, A", "Drawer box front, A", "Drawer box back, A", "Drawer bottom, A", "Drawer front, A"];
        app.Click(OnPlan(window, drawer[0]));
        foreach (string name in drawer.Skip(1))
        {
            app.Click(OnPlan(window, name), modifiers: KeyModifiers.Shift);
        }

        app.Press(Key.M);

        app.Expect("a second drawer stands on the other side of the web, each part at 42 minus its mirror image's far edge", () =>
        {
            Assert.Equal(22, Boxes(window).Count());

            // Drawer A spans 3 1/2 to 20 1/8, so drawer B spans 42 - 20 1/8 = 21 7/8 to 42 - 3 1/2 = 38 1/2:
            // the web's east face (21 3/8) plus 1/2, and 1/2 clear of the east leg (39).
            AssertBox(Only(window, "Drawer side, left, A (2)"), In(38), In(2, 1, 4), In(11), In(0, 1, 2), In(16), In(3, 1, 2));
            AssertBox(Only(window, "Drawer side, right, A (2)"), In(21, 7, 8), In(2, 1, 4), In(11), In(0, 1, 2), In(16), In(3, 1, 2));
            AssertBox(Only(window, "Drawer box front, A (2)"), In(22, 3, 8), In(2, 1, 4), In(11), In(15, 5, 8), In(0, 1, 2), In(3, 1, 2));
            AssertBox(Only(window, "Drawer box back, A (2)"), In(22, 3, 8), In(17, 3, 4), In(11), In(15, 5, 8), In(0, 1, 2), In(3, 1, 2));
            AssertBox(Only(window, "Drawer bottom, A (2)"), In(22, 3, 8), In(2, 3, 4), In(11, 1, 2), In(15, 5, 8), In(15), In(0, 1, 4));
            AssertBox(Only(window, "Drawer front, A (2)"), In(21, 1, 8), In(0, 3, 4), In(10, 3, 4), In(17, 3, 4), In(0, 3, 4), In(5, 1, 2));
        });

        app.Say("The top is fixed with tabletop clips, not rigid screws: the wood moves with the seasons (design only)");
        app.SaveFrame("two-drawers");

        // ---- In three dimensions ------------------------------------------------------------
        app.Say("Look at it in 3D (V), turn it a little with the arrow keys...");
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

        app.Expect("the cut list has the ten rows worked out by hand: 22 pieces from six kinds of stock", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.Contains("22 pieces to cut", list.Headline, StringComparison.Ordinal);
            AssertCutList(list.Rows.Rows);
        });

        app.Say("The shopping list (Ctrl+Shift+L): what to buy for those pieces");
        app.Chord(Key.L, KeyModifiers.Shift);

        app.Expect("the shopping list is boards of stocked lengths, and sheets counted by area", () =>
        {
            Assert.True(window.CutList!.IsShowingShoppingList);
            AssertShoppingList(window.CutList!.ShoppingRows.Rows);
        });

        app.Say("Fasteners (design only, #146): counts come from the joints - 27 pocket screws, 14 wood screws, 24 brads, 10 clips; you type the sizes you buy");

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
    /// The cut list by hand. Rows come largest first (length, then width, then thickness), so the
    /// order is worked out too. A label is the longest common prefix of the names in the row, cut
    /// back to a word boundary, and the sizes are the drawn sizes: joinery allowances are not built.
    /// </summary>
    static void AssertCutList(ImmutableArray<CutListRow> rows)
    {
        (string Label, int Quantity, Length Length, Length Width, Length Thickness, string Material)[] expected =
        [
            // 42 x 22 x 3/4, one.
            ("Top", 1, In(42), In(22), In(0, 3, 4), "3/4 plywood"),

            // 42 - 3 - 3 = 36 between the legs; 1x6 is 5 1/2 x 3/4, 1x2 is 1 1/2 x 3/4. Equal lengths
            // put the wider first.
            ("Apron, back", 1, In(36), In(5, 1, 2), In(0, 3, 4), "1x6"),
            ("Rail, front", 1, In(36), In(1, 1, 2), In(0, 3, 4), "1x2"),

            // 17 3/4 = 17 5/8 opening + 1/8 lap; one per drawer.
            ("Drawer front", 2, In(17, 3, 4), In(5, 1, 2), In(0, 3, 4), "1x6"),

            // 19 - 3/4 - 3/4 = 17 1/2.
            ("Web", 1, In(17, 1, 2), In(5, 1, 2), In(0, 3, 4), "1x6"),

            // 17 - 3/4 = 16 1/4; a 2x2 is 1 1/2 square; four corners.
            ("Leg", 4, In(16, 1, 4), In(1, 1, 2), In(1, 1, 2), "2x2"),

            // 16 long: the two side aprons (5 1/2 wide, 3/4) and the four drawer sides (3 1/2, 1/2).
            ("Apron, side", 2, In(16), In(5, 1, 2), In(0, 3, 4), "1x6"),
            ("Drawer side", 4, In(16), In(3, 1, 2), In(0, 1, 2), "1/2 plywood"),

            // 15 5/8 = 16 5/8 - 2 x 1/2: the bottom (15 wide, 1/4 thick), and the four box fronts
            // and backs (3 1/2, 1/2 thick) which share a row because their sizes are equal.
            ("Drawer bottom", 2, In(15, 5, 8), In(15), In(0, 1, 4), "1/4 plywood"),
            ("Drawer box", 4, In(15, 5, 8), In(3, 1, 2), In(0, 1, 2), "1/2 plywood"),
        ];

        Assert.Equal(
            expected.AsEnumerable(),
            rows.Select(row => (row.Label, row.Quantity, row.Length, row.Width, row.Thickness, row.Material)));
        Assert.Equal(22, rows.Sum(row => row.Quantity));
    }

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

        // Sheets are counted by area, and labelled a floor. 1/2 plywood: 4 sides 16 x 3 1/2 = 224 and
        // 4 fronts and backs 15 5/8 x 3 1/2 = 218.75, together 442.75 sq in of a 4608 sq in sheet.
        // 1/4: 2 x 15 5/8 x 15 = 468.75. 3/4: 42 x 22 = 924. One sheet each.
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

        // 1x6: 36 + 2 x 17 3/4 + 17 1/2 + 2 x 16 = 121 in of pieces, longest first: 36 + 17 3/4 +
        // 17 3/4 = 71 1/2 fills one 6' board, and 17 1/2 + 16 + 16 = 49 1/2 needs a second: two 6'
        // boards. 1 x 6 x 72 / 144 = 3.0 each, 6.0 bought; 6 x 121 / 144 = 5.04, used 5.0.
        ShoppingListRow oneBySix = Assert.Single(rows, row => row.Material == "1x6");
        Assert.Equal("2 × 6'-0\"", oneBySix.BuyText);
        Assert.Equal(("6.0", "5.0", "1.0"), (oneBySix.BoughtText, oneBySix.UsedText, oneBySix.WasteText));

        // 2x2: four legs of 16 1/4 = 65 in fit one 6' board. 2 x 2 x 72 / 144 = 2.0 bought; 4 x 65 / 144 = 1.8.
        ShoppingListRow twoByTwo = Assert.Single(rows, row => row.Material == "2x2");
        Assert.Equal("1 × 6'-0\"", twoByTwo.BuyText);
        Assert.Equal(("2.0", "1.8", "0.2"), (twoByTwo.BoughtText, twoByTwo.UsedText, twoByTwo.WasteText));
    }

    // ---- Doing things the way a person does ---------------------------------------------------

    /// <summary>Picks a stock size from its drawer, drags it out on the paper, and folds the drawer away.</summary>
    static void Place(IGuiDriver app, MainWindow window, StockCategory category, string stock, Point2 from, Point2 to)
    {
        StockToolbox toolbox = window.Toolbox;
        app.Click(CentreOf(window, toolbox.CategoryButtons[category]));
        Button button = toolbox.ButtonFor(stock)!;
        if (stock.EndsWith("plywood", StringComparison.Ordinal))
        {
            app.Wheel(CentreOf(window, toolbox.ItemScroller), new Vector(0, -10));
        }

        app.Click(CentreOf(window, button));
        Point start = At(window, from);
        Point end = At(window, to);
        app.Drag(start, new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2), end);

        // Fold the drawer away so it is not in the way of the size labels.
        app.Click(CentreOf(window, toolbox.CategoryButtons[category]));
    }

    /// <summary>Clicks a dimension on the drawing and types the size it should be.</summary>
    static void SetSize(IGuiDriver app, MainWindow window, SizeAxis axis, string text)
    {
        EntityId id = window.Editor.OnlySelected!.Value;
        app.Click(InWindow(window, window.Canvas.SelectionDimensionLabelAt(id, axis)
            ?? throw new InvalidOperationException($"{id} shows no {axis} dimension.")));
        app.Chord(Key.A);
        app.Type(text);
        app.Press(Key.Enter);
    }

    /// <summary>Names the selected part and says where it is, in the Part panel: name, east, north, up.</summary>
    static void Fields(IGuiDriver app, MainWindow window, string name, string east, string north, string up)
    {
        Fill(app, window, window.PartNameField, name);
        app.Tab(2);
        app.Type(east);
        app.Tab();
        app.Type(north);
        app.Tab();
        app.Type(up);
        app.Press(Key.Enter);
    }

    static void Rename(IGuiDriver app, MainWindow window, string name)
    {
        Fill(app, window, window.PartNameField, name);
        app.Press(Key.Enter);
    }

    /// <summary>Replaces what a field says, the way a person does: click, select all, type.</summary>
    static void Fill(IGuiDriver app, MainWindow window, TextBox field, string text)
    {
        // The side panels lay themselves out again as the pointer arrives; aim again once they have.
        app.MoveTo(CentreOf(window, field));
        app.WaitForIdle();
        app.Click(CentreOf(window, field));
        app.Chord(Key.A);
        app.Type(text);
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
    static Point OnPlan(MainWindow window, string name)
    {
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(Only(window, name));
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

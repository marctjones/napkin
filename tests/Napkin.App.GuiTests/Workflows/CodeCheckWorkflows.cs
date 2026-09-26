using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The code check on a wall's window (#18, #19), driven as a person drives it: draw a wall, choose
/// the adopted code and type the site's snow load, say what the wall supports, place a window, and
/// read its header with the citation; resize it, push it past the table, clear the snow, switch the
/// code, and undo.
/// </summary>
/// <remarks>
/// The packs are the SYNTHETIC ones in <c>tests/Napkin.Modules.Building.Tests/CodePacks</c> (their
/// numbers are made up, NOT CODE VALUES); every expected row is worked out by hand in
/// <c>CodeCheckTests</c>'s comments from that fixture. GUI-CHECK-04 uses the shipped Connecticut
/// pack, whose model-code tables are not loaded.
/// </remarks>
public class CodeCheckWorkflows
{
    static readonly string One = Path.Combine(AppContext.BaseDirectory, "CodePacks", "one");
    static readonly string Two = Path.Combine(AppContext.BaseDirectory, "CodePacks", "two");
    static readonly string Three = Path.Combine(AppContext.BaseDirectory, "CodePacks", "three");
    static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "packs");

    [GuiWorkflow("GUI-CHECK-01")]
    public void Choose_the_code_enter_the_snow_say_what_the_wall_carries_and_place_a_window() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        DrawWall(app, window);

        CodeWindow code = OpenCode(app, window);
        AppDriver site = AppDriver.Attach(code, "check-01-code");
        app.Expect("the picker lists the packs found, and the wall cannot be checked before a code is chosen", () =>
        {
            Assert.Equal(CodeWindow.NoCodeRow, code.PackRows[0]);
            Assert.Contains(code.PackRows, row => row.StartsWith("ZZ FRAME — IRC 2099, in force Jan 1, 2099 (pack us-zz-frame rev 1)", StringComparison.Ordinal));
            Assert.Contains(code.PackRows, row => row.StartsWith("ZZ OTHER — IRC 2099", StringComparison.Ordinal));
            Assert.All(code.PackRows.Skip(1), row => Assert.EndsWith("(UNREVIEWED)", row, StringComparison.Ordinal));
            Assert.Equal(CodeCheck.NoCodeSelectedText, code.StatusText);
        });

        PickPack(site, code, "ZZ FRAME");
        TypeSnow(site, code, "30");
        site.SaveFrame("code-and-site");

        app.Expect("the code is locked to revision 1 today and the snow load is 30 psf; nothing else was filled in", () =>
        {
            Sketch sketch = window.CurrentDesign!.Sketch;
            Assert.Equal(new CodeChoice("us-zz-frame", 1, CodeMode.Locked, DateOnly.FromDateTime(DateTime.Today)), sketch.Code);
            Assert.Equal(SiteValues.NotEntered with { GroundSnowLoadPsf = 30 }, sketch.Site);
            Assert.Equal(CodeCheck.LockedNote(DateOnly.FromDateTime(DateTime.Today), "us-zz-frame", 1), code.LockText);
            Assert.StartsWith("Checking against ZZ FRAME (IRC 2099), pack us-zz-frame rev 1", code.StatusText, StringComparison.Ordinal);
        });

        window.Activate();
        ChooseSupports(app, window);
        app.Expect("the wall now supports the table's zz-roof", () =>
            Assert.Equal("zz-roof", Assert.Single(Wall.All(window.CurrentDesign!.Sketch)).Box.WallInputs?.Supports));

        PlaceWindow(app, window);
        app.Expect("the 3 ft window is sized from row r.s30.a, with its citation", () =>
        {
            // 36 in ≤ 4'-1" at snow ≤ 30, zz-roof: (1) 2x8, 1 jack, 1 king (CodeCheckTests).
            Assert.Equal(CodeCheck.HeaderText("(1) 2x8", 1, 1), window.CodeCheckText);
            Assert.StartsWith("IRC 2099 Table ZZ-HEADER, as adopted by ZZ FRAME row r.s30.a", window.CodeCheckCitationText, StringComparison.Ordinal);
            Assert.Contains("How it was found: headerSpan 3'-0\" → ≤ 4'-1\"", window.CodeCheckWorkingText, StringComparison.Ordinal);
            Assert.DoesNotContain("not yet sized", window.FramingHeadlineText, StringComparison.Ordinal);
            Assert.Contains(FramingList.HeaderPieces(1, "2x8"), window.FramingText, StringComparison.Ordinal);
        });

        app.SaveFrame("window-sized");
    }, packRoots: [One]);

    [GuiWorkflow("GUI-CHECK-02")]
    public void Resize_the_window_and_the_header_follows_then_undo_and_buy_it() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        EntityId opening = SetUp(app, window, "ZZ FRAME", "30");

        TypeWidth(app, window, opening, "5'");
        app.Expect("at 5 ft the header is (2) 2x10 from row r.s30.b, and the message bar says what changed", () =>
        {
            // 60 in: past 4'-1", ≤ 6'-1": r.s30.b, (2) 2x10, 1 jack and 2 kings each side.
            Assert.Equal(CodeCheck.HeaderText("(2) 2x10", 1, 2), window.CodeCheckText);
            Assert.Contains("row r.s30.b", window.CodeCheckCitationText, StringComparison.Ordinal);
            Assert.Contains(
                "Header for Window 1 changed: (1) 2x8 → (2) 2x10, 1 jack and 2 king each side (Table ZZ-HEADER row r.s30.b).",
                window.MessageOnScreen,
                StringComparison.Ordinal);
            Assert.Contains("4 king studs", window.FramingText, StringComparison.Ordinal);
        });

        app.SaveFrame("window-widened");

        // Exactly the table's edge: 6'-1" is still r.s30.b.
        TypeWidth(app, window, opening, "6' 1\"");
        app.Expect("6'-1\" is the band's own edge: still (2) 2x10, and no change is announced", () =>
        {
            Assert.Equal(CodeCheck.HeaderText("(2) 2x10", 1, 2), window.CodeCheckText);
            Assert.DoesNotContain("Header for Window 1", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.Chord(Key.Z);
        app.Chord(Key.Z);
        app.Expect("two undos put the 3 ft window and its (1) 2x8 back, and say so", () =>
        {
            Assert.Equal(CodeCheck.HeaderText("(1) 2x8", 1, 1), window.CodeCheckText);
            Assert.Contains("Header for Window 1 changed: (2) 2x10 → (1) 2x8", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.Chord(Key.L, KeyModifiers.Shift);
        app.Expect("the shopping list buys the 2x8 header and says what it was checked against", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.Contains(list.FramingRows.Sorted, row => row.Material == "2x8");
            Assert.Contains($"{CodeCheck.UnderHeading(new AdoptedCodeRef("us-zz-frame", 1, "ZZ FRAME", "IRC 2099", ReviewStatus.Unreviewed))}: Window 1: (1) 2x8", list.FramingNoteText, StringComparison.Ordinal);
            Assert.DoesNotContain("not yet sized", list.FramingNoteText, StringComparison.Ordinal);
        });

        AppDriver.Attach(window.CutList!, "check-02-shopping").SaveFrame("framing-with-header");
    }, packRoots: [One]);

    [GuiWorkflow("GUI-CHECK-03")]
    public void Widen_the_window_past_the_table_then_clear_the_snow() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        EntityId opening = SetUp(app, window, "ZZ FRAME", "30");

        TypeWidth(app, window, opening, "8' 2\"");
        app.Expect("past 8'-1\" the window is beyond the table, citing the limit, with no size at all", () =>
        {
            // 98 in > 97 in, r.s30.c's span, the last for zz-roof at snow ≤ 30.
            Assert.Equal(
                "This opening is beyond what Table ZZ-HEADER covers: The header span 8'-2\" is longer than the longest span table ZZ-HEADER gives for these conditions (8'-1\", row r.s30.c). napkin stops here: get this header engineered.",
                window.CodeCheckText);
            Assert.StartsWith("Limit: IRC 2099 Table ZZ-HEADER, as adopted by ZZ FRAME row r.s30.c", window.CodeCheckCitationText, StringComparison.Ordinal);
            Assert.DoesNotContain("2x", window.CodeCheckText, StringComparison.Ordinal);
            Assert.Contains(FramingList.HeaderPieces(1, null), window.FramingText, StringComparison.Ordinal);
            Assert.Contains(CodeCheck.NowBeyondText("Window 1", "ZZ-HEADER"), window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.SaveFrame("beyond-the-table");

        CodeWindow code = OpenCode(app, window);
        AppDriver site = AppDriver.Attach(code, "check-03-code");
        site.Click(CentreOf(code, code.SnowField));
        site.Chord(Key.A);
        site.Press(Key.Back);
        site.Press(Key.Enter);
        app.Expect("with the snow load cleared nothing is assumed: the window says which input is missing and where", () =>
        {
            Assert.Null(window.CurrentDesign!.Sketch.Site.GroundSnowLoadPsf);
            // The exact wording is the thing under test here (CodeCheck.cs composes it from the
            // missing input's own name), not just duplicated from a constant.
            Assert.Equal(
                "Not checked: the ground snow load is not entered, and napkin never assumes a value. Enter the site values under Project → Adopted code and site.",
                window.CodeCheckText);
            Assert.Contains("Header for Window 1 can no longer be checked", window.MessageOnScreen, StringComparison.Ordinal);
        });

        window.Activate();
        app.SaveFrame("snow-missing");
    }, packRoots: [One]);

    [GuiWorkflow("GUI-BRACE-02")]
    public void Follow_a_newer_revision_switch_the_code_and_undo() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        EntityId opening = SetUp(app, window, "ZZ FRAME — IRC 2099, in force Jan 1, 2099 (pack us-zz-frame rev 1)", "30");
        TypeWidth(app, window, opening, "5'");
        app.Expect("locked to revision 1, the 5 ft window is (2) 2x10", () =>
            Assert.Equal(CodeCheck.HeaderText("(2) 2x10", 1, 2), window.CodeCheckText));

        CodeWindow code = OpenCode(app, window);
        AppDriver picker = AppDriver.Attach(code, "brace-02-code");
        picker.Click(CentreOf(code, code.LockToggle));
        app.Expect("following, the project takes revision 2, whose row r.s30.b is (2) 2x12, and says so", () =>
        {
            Assert.Equal(CodeMode.Following, window.CurrentDesign!.Sketch.Code!.Mode);
            Assert.Equal(CodeCheck.FollowingNote("us-zz-frame"), code.LockText);
            Assert.StartsWith("Checking against ZZ FRAME (IRC 2099), pack us-zz-frame rev 2", code.StatusText, StringComparison.Ordinal);
            Assert.Contains("Header for Window 1 changed: (2) 2x10 → (2) 2x12", window.MessageOnScreen, StringComparison.Ordinal);
            Assert.Equal(CodeCheck.HeaderText("(2) 2x12", 1, 2), window.CodeCheckText);
        });

        PickPack(picker, code, "ZZ OTHER");
        app.Expect("switching to the other pack recomputes every header and names the change", () =>
        {
            // us-zz-other's o.a: zz-roof, snow ≤ 60, ≤ 5'-1": (3) 2x10.
            Assert.Equal("us-zz-other", window.CurrentDesign!.Sketch.Code!.PackId);
            Assert.Contains(
                "Header for Window 1 changed: (2) 2x12 → (3) 2x10, 1 jack and 1 king each side (Table ZZ-OTHER-HEADER row o.a).",
                window.MessageOnScreen,
                StringComparison.Ordinal);
            Assert.Contains("Table ZZ-OTHER-HEADER", window.CodeCheckCitationText, StringComparison.Ordinal);
        });

        picker.SaveFrame("switched");
        window.Activate();
        app.Chord(Key.Z);
        app.Expect("undo puts the followed ZZ FRAME back, and its (2) 2x12", () =>
        {
            Assert.Equal("us-zz-frame", window.CurrentDesign!.Sketch.Code!.PackId);
            Assert.Equal(CodeCheck.HeaderText("(2) 2x12", 1, 2), window.CodeCheckText);
            Assert.Contains("(3) 2x10 → (2) 2x12", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.SaveFrame("undone");
    }, packRoots: [One, Two]);

    [GuiWorkflow("GUI-CHECK-04")]
    public void With_the_shipped_pack_the_check_says_there_is_no_data() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        DrawWall(app, window);
        CodeWindow code = OpenCode(app, window);
        AppDriver site = AppDriver.Attach(code, "check-04-code");
        app.Expect("the shipped Connecticut pack is listed as having no base tables", () =>
            Assert.Contains(code.PackRows, row => row.StartsWith("CT 2022 — IRC 2021, in force Oct 1, 2022 (pack us-ct-2022 rev 1): base tables not loaded", StringComparison.Ordinal)));

        PickPack(site, code, "CT 2022");
        TypeSnow(site, code, "30");
        site.SaveFrame("shipped-pack");
        app.Expect("the code is chosen, and the window says it cannot size a header until tables are loaded", () =>
        {
            Assert.Equal("us-ct-2022", window.CurrentDesign!.Sketch.Code!.PackId);
            Assert.Contains(CodeCheck.NoBaseTablesNote, code.StatusText, StringComparison.Ordinal);
        });

        window.Activate();
        app.Click(At(window, Point2.Inches(-60, 2)));
        app.Expect("with no table, there is nothing to choose for what the wall supports, and the picker says why", () =>
        {
            Assert.True(window.IsShowingSupports);
            Assert.False(window.SupportsControl.IsEnabled);
            Assert.Equal(CodeCheck.NoHeaderTableTip("CT 2022"), ToolTip.GetTip(window.SupportsControl) as string);
        });

        PlaceWindow(app, window);
        app.Expect("the window's check is the engine's honest no-data text and where to add tables, never a size", () =>
        {
            Assert.StartsWith(HeaderResult.NoData.NoTableExplanation("CT 2022", WallKind.ExteriorBearing), window.CodeCheckText, StringComparison.Ordinal);
            Assert.EndsWith(CodeCheck.WhereToAddTables, window.CodeCheckText, StringComparison.Ordinal);
            Assert.DoesNotContain("2x", window.CodeCheckText, StringComparison.Ordinal);
            Assert.Contains(FramingList.HeaderPieces(1, null), window.FramingText, StringComparison.Ordinal);
        });

        app.SaveFrame("no-data");
    }, packRoots: [Shipped]);

    [GuiWorkflow("GUI-CHECK-06")]
    public void Pick_the_town_and_use_the_values_the_adopted_code_prints_for_it() => GuiWorkflow.Run(app =>
    {
        // Connecticut's Appendix AY, p. 157, prints Bloomfield at 120 mph and 30 psf; Table R301.2,
        // p. 131, prints seismic design category B for the whole state (read from the pages 2026-09-26).
        MainWindow window = (MainWindow)app.Target;
        DrawWall(app, window);
        CodeWindow code = OpenCode(app, window);
        AppDriver site = AppDriver.Attach(code, "check-06-code");
        PickPack(site, code, "CT 2022");
        app.Expect("the Connecticut pack offers a town picker, with nothing chosen and nothing to use yet", () =>
        {
            Assert.True(code.TownPicker.IsVisible);
            Assert.Equal(169, code.TownPicker.ItemCount);
            Assert.False(code.UseTownValues.IsEnabled);
            Assert.StartsWith("CT 2022 publishes these values town by town", code.TownOfferLine, StringComparison.Ordinal);
        });

        Replace(site, code, code.WidthField, "24'");

        // Bloomfield is the eleventh town printed: open the picker with the mouse, walk down with the keys.
        site.Click(CentreOf(code, code.TownPicker));
        for (int i = 0; i < 11; i++)
        {
            site.Press(Key.Down);
        }

        site.Press(Key.Enter);
        app.Expect("the offer names Bloomfield's values and where they are printed, and nothing is set yet", () =>
        {
            Assert.StartsWith("Bloomfield, as CT 2022 prints it: ground snow load 30 psf and ultimate wind speed 120 mph (Appendix AY, p. 157", code.TownOfferLine, StringComparison.Ordinal);
            Assert.True(code.UseTownValues.IsEnabled);
            Assert.Null(window.CurrentDesign!.Sketch.Site.GroundSnowLoadPsf);
        });

        site.Click(CentreOf(code, code.UseTownValues));
        site.SaveFrame("town-values");
        app.Expect("accepted: snow, wind and seismic are Connecticut's for Bloomfield, the typed width stays, and the source says where", () =>
        {
            SiteValues values = window.CurrentDesign!.Sketch.Site;
            Assert.Equal((30, 120, "B"), (values.GroundSnowLoadPsf, values.UltimateWindSpeedMph, values.SeismicDesignCategory));
            Assert.Equal(Length.Inches(288), values.BuildingWidth);
            Assert.StartsWith("CT 2022, Bloomfield: Appendix AY, p. 157", values.Source!.Text, StringComparison.Ordinal);
            Assert.Equal("30", code.SnowField.Text);
            Assert.Equal("120", code.WindField.Text);
        });
    }, packRoots: [Shipped]);

    [GuiWorkflow("GUI-CHECK-05")]
    public void A_snow_load_between_two_columns_interpolates_and_below_30_the_roof_live_load_decides() => GuiWorkflow.Run(app =>
    {
        // CodePacks/three (SYNTHETIC, NOT CODE VALUES): ZZ-INTERP-HEADER, zz-roof, (1) 2x8 spans 6'-0" at 30 psf,
        // 4'-0" at 50 psf; its overlay's footnote e interpolates between 30 and 50 and takes snow below 30 as 30
        // when the roof live load is at most 20. At 40 psf: (6'-0" + 4'-0") / 2 = 5'-0".
        MainWindow window = (MainWindow)app.Target;
        DrawWall(app, window);
        CodeWindow code = OpenCode(app, window);
        AppDriver site = AppDriver.Attach(code, "check-05-code");
        PickPack(site, code, "ZZ INTERP");
        TypeSnow(site, code, "40");
        Replace(site, code, code.RoofLiveLoadField, "20");
        site.SaveFrame("snow-40-roof-live-20");
        app.Expect("the snow load is 40 psf and the roof live load 20 psf, typed in the dialog", () =>
            Assert.Equal(SiteValues.NotEntered with { GroundSnowLoadPsf = 40, RoofLiveLoadPsf = 20 }, window.CurrentDesign!.Sketch.Site));

        window.Activate();
        ChooseSupports(app, window);
        EntityId opening = PlaceWindow(app, window);
        TypeWidth(app, window, opening, "4' 6\"");
        app.Expect("a 4'-6\" opening at 40 psf takes the (1) 2x8, whose interpolated span is 5'-0\", and the check says it was interpolated", () =>
        {
            // The plain 50 psf column would allow only 4'-0" for the 2x8; interpolation between 6'-0" and 4'-0" gives 5'-0".
            Assert.Equal(CodeCheck.HeaderText("(1) 2x8", 1, 1), window.CodeCheckText);
            Assert.Equal(
                "Interpolated between the 30 psf row (i.s30.a) and the 50 psf row (i.s50.a) (ZZ INTERP footnote e, p. 9)",
                window.CodeCheckInterpolationText);
            Assert.Contains("row i.s50.a", window.CodeCheckCitationText, StringComparison.Ordinal);
            Assert.Contains("weight (40 − 30) / (50 − 30) = 1/2", window.CodeCheckWorkingText, StringComparison.Ordinal);
        });
        app.SaveFrame("interpolated");

        code = OpenCode(app, window);
        site = AppDriver.Attach(code, "check-05-code-30");
        Replace(site, code, code.SnowField, "30");
        app.Expect("at exactly 30 psf the plain 30 psf row is used and the interpolation line is gone", () =>
        {
            Assert.Equal(CodeCheck.HeaderText("(1) 2x8", 1, 1), window.CodeCheckText);
            Assert.Equal(string.Empty, window.CodeCheckInterpolationText);
            Assert.Contains("row i.s30.a", window.CodeCheckCitationText, StringComparison.Ordinal);
        });

        Replace(site, code, code.SnowField, "25");
        Replace(site, code, code.RoofLiveLoadField, "25");
        site.SaveFrame("snow-25-roof-live-25");
        app.Expect("below 30 psf with a 25 psf roof live load the footnote does not apply: out of scope, citing footnote e", () =>
        {
            Assert.StartsWith("This opening is beyond what Table ZZ-INTERP-HEADER covers: Footnote e of table ZZ-INTERP-HEADER lets groundSnowLoad below 30 psf be taken as 30 psf only when roofLiveLoad is at most 20 psf", window.CodeCheckText, StringComparison.Ordinal);
            Assert.DoesNotContain("2x", window.CodeCheckText, StringComparison.Ordinal);
            Assert.StartsWith("Limit: IRC 2099 Table ZZ-INTERP-HEADER, as adopted by ZZ INTERP (footnote e)", window.CodeCheckCitationText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, window.CodeCheckInterpolationText);
        });

        window.Activate();
        app.SaveFrame("out-of-scope-by-footnote");
    }, packRoots: [Three]);

    // ---- Steps ---------------------------------------------------------------------------

    /// <summary>A new sheet, zoomed out, and a 16 ft 2x4 wall dragged west to east with the W tool.</summary>
    static void DrawWall(AppDriver app, MainWindow window)
    {
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        if (window.Editor.Selection.Count > 0)
        {
            app.Press(Key.Escape);
        }

        app.Wheel(At(window, Point2.Inches(0, 0)), new Vector(0, -8));
        app.Wheel(At(window, Point2.Inches(0, 0)), new Vector(0, -4));
        app.Press(Key.W);
        app.Drag(At(window, Point2.Inches(-96, 0)), At(window, Point2.Inches(0, 2)), At(window, Point2.Inches(96, 2)));
        app.Expect("a 16 ft wall is drawn and selected", () =>
        {
            Box wall = Assert.Single(Wall.All(window.CurrentDesign!.Sketch)).Box;
            Assert.Equal(Length.Inches(192), wall.Width);
            Assert.Equal(wall.Id, window.Editor.OnlySelected);
        });

        // napkin never assumes what a wall is (renovation-sketches §4.3): say it is exterior and
        // bearing, in the panel, with the pointer and the keyboard.
        Pick(app, window, window.SideControl, downs: 1);
        Pick(app, window, window.BearingControl, downs: 1);
        app.Expect("the wall is said to be exterior and bearing", () =>
        {
            WallInputs inputs = Assert.Single(Wall.All(window.CurrentDesign!.Sketch)).Box.WallInputs!;
            Assert.Equal(WallSide.Exterior, inputs.Side);
            Assert.True(inputs.Bearing);
        });
    }

    /// <summary>A picker in the Part panel, scrolled into sight: a click, then Down so many times, then Enter.</summary>
    static void Pick(AppDriver app, MainWindow window, Control picker, int downs)
    {
        Reveal(app, window, picker);
        app.Click(CentreOf(window, picker));
        for (int i = 0; i < downs; i++)
        {
            app.Press(Key.Down);
        }

        app.Press(Key.Enter);
    }

    /// <summary>Scrolls the Part panel with the mouse wheel, either way, until <paramref name="control"/> is inside it.</summary>
    static void Reveal(AppDriver app, MainWindow window, Control control)
    {
        ScrollViewer scroller = window.FindControl<ScrollViewer>("PropertiesScroller")!;
        for (int i = 0; i < 30; i++)
        {
            if (Edge(window, control, bottom: true) > Edge(window, scroller, bottom: true))
            {
                app.Wheel(CentreOf(window, scroller), new Vector(0, -1));
            }
            else if (Edge(window, control, bottom: false) < Edge(window, scroller, bottom: false))
            {
                app.Wheel(CentreOf(window, scroller), new Vector(0, 1));
            }
            else
            {
                return;
            }
        }

        static double Edge(MainWindow window, Control c, bool bottom) => c.TranslatePoint(new Point(0, bottom ? c.Bounds.Height : 0), window)!.Value.Y;
    }

    /// <summary>Project → Adopted code and site…, with the pointer.</summary>
    static CodeWindow OpenCode(AppDriver app, MainWindow window)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ProjectMenu")!));
        app.Click(CentreOf(window, window.CodeMenuEntry));
        return window.CodeSite ?? throw new InvalidOperationException("The code window did not open.");
    }

    static void PickPack(AppDriver driver, CodeWindow code, string startsWith)
    {
        int index = code.PackRows.ToList().FindIndex(row => row.StartsWith(startsWith, StringComparison.Ordinal));
        Assert.True(index > 0, $"No pack row starts with \"{startsWith}\".");
        code.PackPicker.ScrollIntoView(index);
        driver.WaitForIdle();
        Control row = code.PackPicker.ContainerFromIndex(index) ?? throw new InvalidOperationException("The row is not realised.");
        driver.Click(CentreOf(code, row));
    }

    static void TypeSnow(AppDriver driver, CodeWindow code, string psf)
    {
        driver.Click(CentreOf(code, code.SnowField));
        driver.Type(psf);
        driver.Press(Key.Enter);
    }

    /// <summary>Clicks a site field, selects what is there, types the new value and presses Enter.</summary>
    static void Replace(AppDriver driver, CodeWindow code, TextBox field, string text)
    {
        driver.Click(CentreOf(code, field));
        driver.Chord(Key.A);
        driver.Type(text);
        driver.Press(Key.Enter);
    }

    /// <summary>The supports picker, with the pointer and the keyboard: the table's first value.</summary>
    static void ChooseSupports(AppDriver app, MainWindow window) => Pick(app, window, window.SupportsControl, downs: 1);

    /// <summary>Draw → Window, then a click on the middle of the wall: a 3 ft window centred there.</summary>
    static EntityId PlaceWindow(AppDriver app, MainWindow window)
    {
        app.Click(CentreOf(window, window.DrawMenuItem));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("WindowToolMenuItem")!));
        app.Click(At(window, Point2.Inches(0, 1)));
        Sketch sketch = window.CurrentDesign!.Sketch;
        return Assert.Single(Opening.In(sketch, Assert.Single(Wall.All(sketch)))).Id;
    }

    /// <summary>A wall, a chosen code, a typed snow load, what the wall supports, and a window: every step by input.</summary>
    static EntityId SetUp(AppDriver app, MainWindow window, string pack, string snow)
    {
        DrawWall(app, window);
        CodeWindow code = OpenCode(app, window);
        AppDriver site = AppDriver.Attach(code, "setup");
        PickPack(site, code, pack);
        TypeSnow(site, code, snow);
        window.Activate();
        ChooseSupports(app, window);
        EntityId opening = PlaceWindow(app, window);
        app.Expect("the window is placed and sized: (1) 2x8 from row r.s30.a", () =>
            Assert.Equal(CodeCheck.HeaderText("(1) 2x8", 1, 1), window.CodeCheckText));
        return opening;
    }

    /// <summary>Types a width on the opening's width label.</summary>
    static void TypeWidth(AppDriver app, MainWindow window, EntityId opening, string width)
    {
        app.Click(InWindow(window, window.Canvas.SelectionDimensionLabelAt(opening, Napkin.App.Editing.SizeAxis.Width)
            ?? throw new InvalidOperationException("The opening shows no width label.")));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type(width);
        app.Press(Key.Enter);
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
}

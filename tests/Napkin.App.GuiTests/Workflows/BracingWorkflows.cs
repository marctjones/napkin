using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Modules.Building;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The wall-bracing check (#39) and the pack-switch recompute (#19), driven as a person drives them:
/// draw a wall, choose the code and type the wind speed, put two windows in it, assign the bracing
/// already on each solid segment, widen a window step by step until the wall line runs short, undo,
/// then switch the code and read what changed.
/// </summary>
/// <remarks>
/// The packs are the SYNTHETIC ones in <c>tests/Napkin.Modules.Building.Tests/CodePacks/brace</c> (made
/// up, NOT CODE VALUES); every expected length is worked by hand in <c>BracingCheckTests</c>'s comments:
/// a 16 ft wall (192"), 8 ft tall; windows 3 ft wide at 30" and 126" along it, so segments of 30", 60"
/// and 30". Pack A at 90 mph: 76.8" → 78" (6'-6") required; zz-panel counts from 24", capped at 72";
/// zz-board from 48". Pack B: 72" (6'-0") required; only zz-panel, from 36".
/// </remarks>
public class BracingWorkflows
{
    static readonly string Brace = Path.Combine(AppContext.BaseDirectory, "CodePacks", "brace");
    static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "packs");

    [GuiWorkflow("GUI-BRACE-01")]
    public void Assign_the_bracing_widen_a_window_until_the_wall_line_is_short_and_undo() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        (EntityId one, _) = SetUp(app, window);

        SelectWall(app, window);
        app.Expect("the wall's three solid segments are listed with their lengths, none braced, and the line is short by all of it", () =>
        {
            Assert.Equal(["1. wall start to Window 1, 2'-6\"", "2. Window 1 to Window 2, 5'-0\"", "3. Window 2 to wall end, 2'-6\""], window.BracingSegmentTexts);
            Assert.All(window.BracingPickers, picker => Assert.Equal("not braced", picker.SelectedItem));
            Assert.Equal("Braced length 0\" of 6'-6\" required: SHORT by 6'-6\" (ZZ-BRACE.1).", window.BracingText);
        });

        for (int segment = 0; segment < 3; segment++)
        {
            ChooseMethod(app, window, segment, downs: 1);
        }

        app.Expect("with ZZ panel on every segment, 10'-0\" is braced of 6'-6\" required: it passes, citing the section", () =>
        {
            // 30 + 60 + 30 = 120" ≥ 78".
            Assert.Equal("Braced length 10'-0\" of 6'-6\" required: passes (ZZ-BRACE.1).", window.BracingText);
            Assert.StartsWith("IRC 2099 Section ZZ-BRACE.1, as adopted by ZZ BRACE A row q.w99", window.BracingCitationText, StringComparison.Ordinal);
            Assert.Equal(3, Assert.Single(Wall.All(window.CurrentDesign!.Sketch)).Box.WallInputs!.Bracing.Length);
            Assert.Contains("Braced Wall 1's segment Window 2 to wall end with ZZ panel (synthetic)", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.SaveFrame("braced");

        TypeWidth(app, window, one, "5'");
        app.Expect("Window 1 widened to 5'-0\": the middle segment is 36\", the line still passes, and the message bar says so", () =>
        {
            // 126 − (30 + 60) = 36": 30 + 36 + 30 = 96" (8'-0").
            Assert.Equal("Braced length 8'-0\" of 6'-6\" required: passes (ZZ-BRACE.1).", window.BracingText);
            Assert.Contains("Wall 1's braced line now passes, braced 8'-0\" of 6'-6\" required (ZZ-BRACE.1).", window.MessageOnScreen, StringComparison.Ordinal);
        });

        TypeWidth(app, window, one, "6'");
        app.Expect("at 6'-0\" the middle segment is 24\", exactly the minimum panel: still passes", () =>
            Assert.Equal("Braced length 7'-0\" of 6'-6\" required: passes (ZZ-BRACE.1).", window.BracingText));

        TypeWidth(app, window, one, "6' 1\"");
        app.Expect("one inch wider the middle segment is under the minimum and counts for nothing: SHORT by 1'-6\", said with the edit", () =>
        {
            // 23" < 24": 30 + 0 + 30 = 60" (5'-0") of 78": short 18".
            Assert.Equal("Braced length 5'-0\" of 6'-6\" required: SHORT by 1'-6\" (ZZ-BRACE.1).", window.BracingText);
            Assert.Contains("Wall 1's braced line is now SHORT by 1'-6\", braced 5'-0\" of 6'-6\" required (ZZ-BRACE.1).", window.MessageOnScreen, StringComparison.Ordinal);
            Assert.Contains("shorter than the 2'-0\" minimum panel", window.BracingWorkingText, StringComparison.Ordinal);
        });

        app.SaveFrame("short");

        // The wall's own panel: its segments and the flag, scrolled into view with the wheel.
        app.Click(At(window, Point2.Inches(0, 60)));
        SelectWall(app, window);
        Reveal(app, window, window.FindControl<TextBlock>("BracingCitation")!);
        app.Expect("the wall's Bracing block lists the 23\" middle segment and flags the line", () =>
        {
            Assert.Equal("2. Window 1 to Window 2, 1'-11\"", window.BracingSegmentTexts[1]);
            Assert.Equal("Braced length 5'-0\" of 6'-6\" required: SHORT by 1'-6\" (ZZ-BRACE.1).", window.BracingText);
        });

        app.SaveFrame("short-panel");

        app.Chord(Key.Z);
        app.Expect("undo puts the 6'-0\" window back, and the line passes again", () =>
        {
            Assert.Equal("Braced length 7'-0\" of 6'-6\" required: passes (ZZ-BRACE.1).", window.BracingText);
            Assert.Contains("Wall 1's braced line now passes", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.SaveFrame("undone");
    }, packRoots: [Brace]);

    [GuiWorkflow("GUI-BRACE-03")]
    public void Switch_to_the_other_code_and_the_wall_is_re_flagged_not_carried_over() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        SetUp(app, window);
        SelectWall(app, window);
        ChooseMethod(app, window, 0, downs: 1);
        ChooseMethod(app, window, 1, downs: 2);
        ChooseMethod(app, window, 2, downs: 1);
        app.Expect("under ZZ BRACE A, panel, board and panel brace 10'-0\" of 6'-6\": it passes", () =>
        {
            // 30 (panel) + 60 (board, ≥ 48") + 30 (panel) = 120".
            Assert.Equal("Braced length 10'-0\" of 6'-6\" required: passes (ZZ-BRACE.1).", window.BracingText);
            Assert.Equal("ZZ board (synthetic)", window.BracingPickers[1].SelectedItem);
        });

        CodeWindow code = OpenCode(app, window);
        AppDriver picker = AppDriver.Attach(code, "brace-03-code");
        PickPack(picker, code, "ZZ BRACE B");
        app.Expect("switching the code recomputes everything and says what changed, what became flagged and what can no longer be computed", () =>
        {
            Assert.Equal("us-zz-brace-b", window.CurrentDesign!.Sketch.Code!.PackId);
            Assert.Contains(
                "Now checking against ZZ BRACE B (IRC 2099, pack us-zz-brace-b rev 1): every result recomputed; 1 changed, 1 newly flagged, none can no longer be computed.",
                window.MessageOnScreen,
                StringComparison.Ordinal);

            // B: the 30" panels are under its 36" minimum and zz-board is not one of its methods: 0" of 72".
            Assert.Contains("Wall 1's braced line is now SHORT by 6'-0\", braced 0\" of 6'-0\" required (ZZ-BRACE-B.7).", window.MessageOnScreen, StringComparison.Ordinal);
        });

        picker.SaveFrame("switched");
        window.Activate();
        app.SaveFrame("summary");
        window.Activate();
        SelectWall(app, window);
        app.Expect("the wall is re-flagged under B, nothing carried over: the board segment says it is not in this code", () =>
        {
            Assert.Equal("Braced length 0\" of 6'-0\" required: SHORT by 6'-0\" (ZZ-BRACE-B.7).", window.BracingText);
            Assert.Equal("zz-board (not in this code)", window.BracingPickers[1].SelectedItem);
            Assert.Equal("ZZ panel B (synthetic)", window.BracingPickers[0].SelectedItem);
            Assert.Contains("method 'zz-board' is not one of ZZ BRACE B's methods", window.BracingWorkingText, StringComparison.Ordinal);
        });

        app.SaveFrame("re-flagged");
        app.Chord(Key.Z);
        app.Expect("undo puts ZZ BRACE A back, and its passing result", () =>
        {
            Assert.Equal("us-zz-brace-a", window.CurrentDesign!.Sketch.Code!.PackId);
            Assert.Equal("Braced length 10'-0\" of 6'-6\" required: passes (ZZ-BRACE.1).", window.BracingText);
            Assert.Contains("Now checking against ZZ BRACE A", window.MessageOnScreen, StringComparison.Ordinal);
        });
    }, packRoots: [Brace]);

    [GuiWorkflow("GUI-BRACE-04")]
    public void With_the_shipped_pack_the_bracing_check_says_there_is_no_data() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        DrawWall(app, window);
        CodeWindow code = OpenCode(app, window);
        AppDriver site = AppDriver.Attach(code, "brace-04-code");
        PickPack(site, code, "CT 2022");
        TypeWind(site, code, "115");
        window.Activate();
        PlaceWindow(app, window, 0);
        SelectWall(app, window);
        app.Expect("under the shipped Connecticut pack the wall's segments are listed, the pickers say why there is nothing to choose, and the check is the honest no-data text", () =>
        {
            Assert.Equal(2, window.BracingPickers.Count);
            Assert.All(window.BracingPickers, picker => Assert.False(picker.IsEnabled));
            Assert.Contains("CT 2022 has no wall-bracing provisions loaded", ToolTip.GetTip(window.BracingPickers[0]) as string ?? string.Empty, StringComparison.Ordinal);
            Assert.StartsWith("The loaded pack CT 2022 has no wall-bracing provisions, so napkin cannot check this wall line's bracing.", window.BracingText, StringComparison.Ordinal);
            Assert.EndsWith(CodeCheck.WhereToAddTables, window.BracingText, StringComparison.Ordinal);
            Assert.DoesNotContain("SHORT", window.BracingText, StringComparison.Ordinal);
            Assert.DoesNotContain("passes", window.BracingText, StringComparison.Ordinal);
        });

        app.SaveFrame("no-data");
    }, packRoots: [Shipped]);

    // ---- Steps ---------------------------------------------------------------------------

    /// <summary>A 16 ft wall, the code ZZ BRACE A with 90 mph typed, and two windows at 30" and 126" along it.</summary>
    static (EntityId One, EntityId Two) SetUp(AppDriver app, MainWindow window)
    {
        DrawWall(app, window);
        CodeWindow code = OpenCode(app, window);
        AppDriver site = AppDriver.Attach(code, "setup");
        PickPack(site, code, "ZZ BRACE A");
        TypeWind(site, code, "90");
        window.Activate();
        EntityId one = PlaceWindow(app, window, -48);
        EntityId two = PlaceWindow(app, window, 48);
        app.Expect("two 3 ft windows sit 30\" and 126\" along the wall", () =>
        {
            Sketch sketch = window.CurrentDesign!.Sketch;
            Assert.Equal([Length.Inches(30), Length.Inches(126)], Opening.In(sketch, Assert.Single(Wall.All(sketch))).Select(o => o.Offset));
        });
        return (one, two);
    }

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
        app.Expect("a 16 ft wall, 8 ft tall, is drawn", () =>
        {
            Box wall = Assert.Single(Wall.All(window.CurrentDesign!.Sketch)).Box;
            Assert.Equal(Length.Inches(192), wall.Width);
            Assert.Equal(Length.Inches(96), wall.Depth);
        });
    }

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

    static void TypeWind(AppDriver driver, CodeWindow code, string mph)
    {
        driver.Click(CentreOf(code, code.WindField));
        driver.Type(mph);
        driver.Press(Key.Enter);
    }

    /// <summary>Draw → Window, then a click on the wall at <paramref name="x"/> inches: a 3 ft window centred there.</summary>
    static EntityId PlaceWindow(AppDriver app, MainWindow window, long x)
    {
        app.Click(CentreOf(window, window.DrawMenuItem));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("WindowToolMenuItem")!));
        app.Click(At(window, Point2.Inches(x, 1)));
        return window.Editor.OnlySelected ?? throw new InvalidOperationException("The window was not placed.");
    }

    /// <summary>A click on the wall's solid start, west of both windows.</summary>
    static void SelectWall(AppDriver app, MainWindow window)
    {
        app.Click(At(window, Point2.Inches(-90, 2)));
        app.Expect("the wall is selected and its Bracing block is showing", () =>
        {
            Assert.Equal(Assert.Single(Wall.All(window.CurrentDesign!.Sketch)).Id, window.Editor.OnlySelected);
            Assert.NotEmpty(window.BracingPickers);
        });
    }

    /// <summary>A segment's method picker, with the pointer, then Down <paramref name="downs"/> times and Enter.</summary>
    static void ChooseMethod(AppDriver app, MainWindow window, int segment, int downs)
    {
        Reveal(app, window, window.BracingPickers[segment]);
        app.Click(CentreOf(window, window.BracingPickers[segment]));
        for (int i = 0; i < downs; i++)
        {
            app.Press(Key.Down);
        }

        app.Press(Key.Enter);
    }

    /// <summary>Scrolls the Part panel with the mouse wheel until <paramref name="control"/> is inside it.</summary>
    static void Reveal(AppDriver app, MainWindow window, Control control)
    {
        ScrollViewer scroller = window.FindControl<ScrollViewer>("PropertiesScroller")!;
        for (int i = 0; i < 20 && Bottom(window, control) > Bottom(window, scroller); i++)
        {
            app.Wheel(CentreOf(window, scroller), new Vector(0, -1));
        }

        static double Bottom(MainWindow window, Control c) => c.TranslatePoint(new Point(0, c.Bounds.Height), window)!.Value.Y;
    }

    static void TypeWidth(AppDriver app, MainWindow window, EntityId opening, string width)
    {
        if (window.Editor.OnlySelected != opening)
        {
            // A click on empty paper lets go of the wall, so the next click picks the window rather than the wall.
            app.Click(At(window, Point2.Inches(0, 60)));
            app.Click(At(window, Point2.Inches(-50, 1)));
        }

        Assert.True(opening == window.Editor.OnlySelected, $"selected {window.Editor.OnlySelected?.Value}, expected {opening.Value}; selection count {window.Editor.Selection.Count}");
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The cut layout (issue #138), driven the way a person drives it: open a stocked design, open the
/// cut layout from the View menu, read which piece is cut from which board, change the saw kerf and
/// watch the boards change, and export it.
/// </summary>
/// <remarks>
/// The expected boards are worked out by hand (<c>samples/stocked-bench.expected.json</c>): with a 1/8 in
/// kerf the 1x4 pieces (46 + 46 + 14 + 14 = 120 in) need 4 cuts, 120 1/2 in, a 12' board; with no kerf
/// they exactly fill a 10' board (120 in, 3 cuts).
/// </remarks>
public class CutLayoutWorkflows
{
    [GuiWorkflow("GUI-CUT-08")]
    public void Open_the_cut_layout_change_the_saw_kerf_and_export_it() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Stocked bench");
        app.Chord(Key.L);

        // Turn to the cut layout from the View menu, with the mouse. The menu is long, and a headless
        // popup cannot leave the window, so the window is made tall enough to hold it.
        app.ResizeWindow(900, 900);
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ListsMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("CutLayoutMenuItem")!));

        app.Expect("the cut layout is showing: one 12 ft 1x4 and one 14 ft 2x4 with the default kerf", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.True(list.IsShowingCutLayout);
            Assert.Equal(CutLayout.DefaultKerf, list.SawKerf);
            Assert.Equal("1/8 in", list.KerfField.Text);
            Assert.Equal(
                [
                    "Board 1: 1x4 x 12 ft: Apron 46 in + Apron 46 in + End rail 14 in + End rail 14 in | 4 cuts, kerf 1/2 in | offcut 23 1/2 in",
                    "Board 1: 2x4 x 14 ft: Stretcher 43 in + Stretcher 43 in + Leg 16 1/2 in + Leg 16 1/2 in + Leg 16 1/2 in + Leg 16 1/2 in | 6 cuts, kerf 3/4 in | offcut 15 1/4 in",
                ],
                list.LayoutRows.LinesOnScreen);
            Assert.Equal(2, list.LayoutRows.Bars.Length);
            Assert.Contains(CutLayout.SheetGoodsNote, list.LayoutSummaryText, StringComparison.Ordinal);
        });

        AppDriver lists = AppDriver.Attach(window.CutList!, "cut-layout");
        lists.SaveFrame("cut-layout-light");

        // No kerf: type 0 into the kerf box and press Enter. The 1x4's four pieces are 120 in, which
        // exactly fill a 10' (120 in): 3 cuts, no offcut. The 2x4 pieces (152 in) still need the 14'.
        lists.Click(CentreOf(window.CutList!, window.CutList!.KerfField));
        lists.Chord(Key.A);
        lists.Type("0");
        lists.Press(Key.Enter);

        app.Expect("with no kerf the 1x4 exactly fills a 10 ft board and the shopping list agrees", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.Equal(Length.Zero, list.SawKerf);
            Assert.Equal(
                "Board 1: 1x4 x 10 ft: Apron 46 in + Apron 46 in + End rail 14 in + End rail 14 in | 3 cuts, kerf 0 in | offcut 0 in",
                list.LayoutRows.LinesOnScreen[0]);
            Assert.Contains("1 × 10'-0\"", list.ShoppingRows.Sorted.Select(row => row.BuyText));
            Assert.Contains("includes a 0 in saw kerf per cut", list.ShoppingCsv.Split('\n')[0], StringComparison.Ordinal);
        });

        // Text that is not a length is refused, in words, and changes nothing.
        lists.Click(CentreOf(window.CutList!, window.CutList!.KerfField));
        lists.Chord(Key.A);
        lists.Type("thick");
        lists.Click(CentreOf(window.CutList!, window.CutList!.SetKerfControl));

        app.Expect("a kerf that is not a length is refused and the boards stay as they were", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.Contains("\"thick\" is not", list.KerfMessage, StringComparison.Ordinal);
            Assert.Equal(Length.Zero, list.SawKerf);
        });

        // A 1/4 in kerf, typed and entered from the keyboard: 1x4 is 120 + 4 x 1/4 = 121 in, a 12'.
        lists.Click(CentreOf(window.CutList!, window.CutList!.KerfField));
        lists.Chord(Key.A);
        lists.Type("1/4");
        lists.Press(Key.Enter);

        app.Expect("a 1/4 in kerf is kept, shown, and plans the boards again", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.Equal(Length.Inches(0, 1, 4), list.SawKerf);
            Assert.Equal(string.Empty, list.KerfMessage);
            Assert.Equal("1/4 in", list.KerfField.Text);
            Assert.Equal(
                "Board 1: 1x4 x 12 ft: Apron 46 in + Apron 46 in + End rail 14 in + End rail 14 in | 4 cuts, kerf 1 in | offcut 23 in",
                list.LayoutRows.LinesOnScreen[0]);

            // The setting is the person's, so it is kept in their settings, not the design.
            Assert.Equal(Length.Inches(0, 1, 4), window.Settings.Current.SawKerf);
        });

        app.Expect("the export is the lines on screen, row for row", () =>
        {
            CutListWindow list = window.CutList!;
            var exported = CutListCsv.Parse(list.LayoutCsv);

            Assert.Equal([CutLayout.Statement(Length.Inches(0, 1, 4))], exported[0]);
            Assert.Equal(CutLayout.Header.Split(','), exported[1]);
            Assert.Equal(list.LayoutRows.LinesOnScreen.Length, exported.Length - 2);
            Assert.Equal(
                list.LayoutRows.LinesOnScreen,
                exported.Skip(2).Select(fields => CutLayout.Line(fields)));
        });

        lists.SaveFrame("cut-layout-1-4-kerf");

        // The dark theme, for the frame the bars' legibility is judged on.
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("AppearanceMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeDarkMenuItem")!));
        AppDriver.Attach(window.CutList!, "cut-layout-dark").SaveFrame("cut-layout-dark");
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("AppearanceMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeSystemMenuItem")!));

        // Close it and open it again: the kerf comes back from the person's settings.
        window.CutList!.Close();
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ListsMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("CutLayoutMenuItem")!));

        app.Expect("the kerf the person set is still there when the window comes back", () =>
        {
            Assert.True(window.CutList!.IsShowingCutLayout);
            Assert.Equal("1/4 in", window.CutList!.KerfField.Text);
        });
    });

    static void OpenSample(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.FileMenuItem));
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

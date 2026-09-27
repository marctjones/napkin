using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Modules.Building;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The site plan (docs/design/permit-set.md §5, #223): a wall drawn, the lot typed as the survey prints
/// it in Project → Site plan, each line's distance read against its setback, and one undo.
/// </summary>
public class SiteWorkflows
{
    [GuiWorkflow("GUI-SITE-01")]
    public void Type_a_lot_as_the_survey_prints_it_and_read_each_lines_distance_from_the_house() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        if (window.Editor.Selection.Count > 0)
        {
            app.Press(Key.Escape);
        }

        // A 16 ft wall from x −8' to 8' along y = 0, 3 1/2" thick.
        app.Wheel(OnPlan(window, Point2.Inches(0, 0)), new Vector(0, -8));
        app.Wheel(OnPlan(window, Point2.Inches(0, 0)), new Vector(0, -4));
        app.Press(Key.W);
        app.Drag(OnPlan(window, Point2.Inches(-96, 0)), OnPlan(window, Point2.Inches(0, 2)), OnPlan(window, Point2.Inches(96, 2)));
        app.Expect("a 16 ft wall is drawn", () => Assert.Equal(Length.Inches(192), Assert.Single(Wall.All(window.CurrentDesign!.Sketch)).Length));

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ProjectMenu")!));
        app.Click(CentreOf(window, window.SitePlanMenuEntry));
        SitePlanWindow plan = window.SitePlanSite ?? throw new InvalidOperationException("The site plan did not open.");
        AppDriver site = AppDriver.Attach(plan, "site-01-plan");
        app.Expect("the window is open with nothing typed", () => Assert.Equal("No property lines yet.", plan.Readout));

        // The point of beginning 10' west and 25' south of the origin; 50' north, 40' east, 50' south, 40' west.
        Type(site, plan, plan.Fields.StartX, "-10'");
        Type(site, plan, plan.Fields.StartY, "-25'");
        site.Click(CentreOf(plan, plan.Fields.Courses));
        foreach (string line in new[] { "N 0 E 50' side 10'", "N 90 E 40' rear 20'", "S 0 E 50'", "N 90 W 40' front 25'" })
        {
            site.Type(line);
            site.Press(Key.Enter);
        }

        site.Click(CentreOf(plan, plan.Fields.Apply));
        app.Expect("the lot closes, and each line says the wall's distance against its setback", () =>
        {
            Boundary lot = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Boundary>());
            Assert.Equal((Point2.Inches(-120, -300), 4), (lot.Start, lot.Courses.Length));
            Assert.Equal(
                "The courses close on the point of beginning.\n"
                + "Wall 1 to side line 2'-0\" (setback 10'-0\": short by 8'-0\")\n"
                + "Wall 1 to rear line 24'-8 1/2\" (setback 20'-0\": clear)\n"
                + "Wall 1 to line 3 22'-0\"\n"
                + "Wall 1 to front line 25'-0\" (setback 25'-0\": clear)",
                plan.Readout);
            Assert.Equal(string.Empty, plan.Problem);
        });
        site.SaveFrame("lot-typed");
        app.SaveFrame("lot-on-the-plan");

        // A course that does not read is said, with its line, and nothing changes.
        Type(site, plan, plan.Fields.Courses, "N 91 E 50'");
        site.Click(CentreOf(plan, plan.Fields.Apply));
        app.Expect("a 91° bearing is refused by line, and the lot is unchanged", () =>
        {
            Assert.StartsWith("Line 1: a bearing is at most 90°", plan.Problem, StringComparison.Ordinal);
            Assert.Equal(4, Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Boundary>()).Courses.Length);
        });

        // Edit → Undo takes the lot back off.
        window.Activate();
        app.Click(CentreOf(window, window.EditMenuItem));
        app.Click(CentreOf(window, window.UndoMenuEntry));
        app.Expect("undo removes the lot, and the window says there is none", () =>
        {
            Assert.Empty(window.CurrentDesign!.Sketch.Entities.Values.OfType<Boundary>());
            Assert.Equal("No property lines yet.", plan.Readout);
        });
    });

    /// <summary>A field of the site plan window: a click, the text over what was there.</summary>
    static void Type(AppDriver site, SitePlanWindow plan, TextBox box, string text)
    {
        site.Click(CentreOf(plan, box));
        site.Press(Key.A, AppDriver.CommandModifier);
        site.Type(text);
    }
}

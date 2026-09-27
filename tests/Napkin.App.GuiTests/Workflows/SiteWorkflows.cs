using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.Designs;
using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

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

    /// <summary>A 40 × 30 light-blue PNG, made for this test: a real image the app can decode.</summary>
    static readonly byte[] Survey = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAACgAAAAeCAIAAADRv8uKAAAAKklEQVR42u3NMQ0AAAgDsPlXNlcgA44m/Zt2TkQsFovFYrFYLBaLxX/jBRFC/vbagGPXAAAAAElFTkSuQmCC");

    [GuiWorkflow("GUI-SITE-02")]
    public void Put_a_survey_image_under_the_plan_calibrate_it_by_two_clicks_save_and_reopen() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        ScriptedFiles files = new();
        window.FilePicker = files;
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        if (window.Editor.Selection.Count > 0)
        {
            app.Press(Key.Escape);
        }

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ProjectMenu")!));
        app.Click(CentreOf(window, window.SitePlanMenuEntry));
        SitePlanWindow plan = window.SitePlanSite ?? throw new InvalidOperationException("The site plan did not open.");
        AppDriver site = AppDriver.Attach(plan, "site-02-plan");

        // The platform's file picker cannot be driven headless: the bytes go through the same seam its handler calls.
        Assert.True(plan.SetUnderlay(Survey, "survey.png"));
        string hash = Napkin.Core.Project.ProjectAssets.Hash(Survey);
        app.Expect("the image is under the plan an inch a pixel from the origin, its bytes kept by their SHA-256", () =>
        {
            SurveyUnderlay underlay = window.CurrentDesign!.Sketch.Site.Underlay!;
            Assert.Equal((hash, new Pixel(0, 0), new Pixel(40, 0), Length.Inches(40)), (underlay.Asset, underlay.ImageA, underlay.ImageB, underlay.Distance));
            Assert.Equal(Survey, window.CurrentDesign.Assets[hash]);
            Assert.Equal("Survey underlay: survey.png, calibrated to 3'-4\" between two points. This site plan is not a survey.", plan.UnderlayLine);
        });

        app.SaveFrame("uncalibrated");

        // Type the real distance, then click two points on the plan 20" apart: they are 20'-0" apart on the ground.
        site.Click(CentreOf(plan, plan.UnderlayFields.Distance));
        site.Type("20'");
        site.Click(CentreOf(plan, plan.UnderlayFields.Calibrate));
        window.Activate();
        app.Click(OnPlan(window, Point2.Inches(-10, 0)));
        app.Click(OnPlan(window, Point2.Inches(10, 0)));
        app.Expect("the two clicks are pixels −10 and 10, and the image now measures 20'-0\" between them", () =>
        {
            SurveyUnderlay underlay = window.CurrentDesign!.Sketch.Site.Underlay!;
            Assert.Equal((new Pixel(-10, 0), new Pixel(10, 0), Length.Feet(20)), (underlay.ImageA, underlay.ImageB, underlay.Distance));
            UnderlayPlacement placement = new(underlay);
            (double ax, double ay) = placement.ToWorld(-10, 0);
            (double bx, double by) = placement.ToWorld(10, 0);
            Assert.InRange(Math.Sqrt(((bx - ax) * (bx - ax)) + ((by - ay) * (by - ay))) - Length.Feet(20).Units, -1, 1);
            Assert.Equal("Survey underlay: survey.png, calibrated to 20'-0\" between two points. This site plan is not a survey.", plan.UnderlayLine);
        });
        app.SaveFrame("calibrated");

        // Save: a design with a survey image is offered as a .napkin project, which carries the image.
        string path = BadScenes.MissingFile($"site-{Guid.NewGuid():N}.napkin");
        files.SaveAnswer = path;
        app.Chord(Key.S);
        app.Expect("it saved as a project, with the image beside the drawing", () =>
        {
            Assert.Equal(["Untitled.napkin"], files.SuggestedNames);
            Assert.True(File.Exists(path));
            Assert.StartsWith($"Saved {Path.GetFileName(path)}", window.MessageOnScreen, StringComparison.Ordinal);
        });

        // A new sheet, then open the project again: the calibration and the image are both back.
        SurveyUnderlay saved = window.CurrentDesign!.Sketch.Site.Underlay!;
        app.Chord(Key.N);
        files.OpenAnswer = path;
        app.Chord(Key.O);
        app.Expect("the reopened project has the same calibration and the same image", () =>
        {
            Assert.Equal(saved, window.CurrentDesign!.Sketch.Site.Underlay);
            Assert.Equal(Survey, window.CurrentDesign.Assets[hash]);
        });
    });

    /// <summary>A picker that answers at once with whatever the workflow put in it.</summary>
    sealed class ScriptedFiles : ISceneFilePicker
    {
        public string? OpenAnswer { get; set; }

        public string? SaveAnswer { get; set; }

        public List<string> SuggestedNames { get; } = [];

        public Task<string?> PickSceneFileAsync() => Task.FromResult(OpenAnswer);

        public Task<string?> PickSaveDestinationAsync(string suggestedName)
        {
            SuggestedNames.Add(suggestedName);
            return Task.FromResult(SaveAnswer);
        }
    }

    /// <summary>A field of the site plan window: a click, the text over what was there.</summary>
    static void Type(AppDriver site, SitePlanWindow plan, TextBox box, string text)
    {
        site.Click(CentreOf(plan, box));
        site.Press(Key.A, AppDriver.CommandModifier);
        site.Type(text);
    }
}

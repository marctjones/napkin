using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;
using Napkin.App.Viewing;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>View > Sketch: paper and pencil for the plan and the 3D view, cosmetic only (#142).</summary>
public class SketchLookWorkflows
{
    [GuiWorkflow("GUI-SET-05")]
    public void Choosing_paper_and_pencil_from_the_View_menu_redraws_both_views_and_changes_nothing_else() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        Control plan = window.Canvas;

        OpenSample(app, window, "Coffee table");
        Color screen = PaperAt(app, window, plan);
        int entities = window.CurrentDesign!.Sketch.Entities.Count;

        app.Expect("the clean look is the default and the menu says so", () =>
        {
            Assert.Equal(new SketchLook(), window.Canvas.Look);
            Assert.NotNull(window.FindControl<MenuItem>("PaperScreenMenuItem")!.Icon);
            Assert.NotNull(window.FindControl<MenuItem>("LineCleanMenuItem")!.Icon);
            Assert.Equal(CanvasPalette.Light.Background, screen);
        });

        Pick(app, window, "PaperGraphMenuItem");
        Pick(app, window, "LinePencilMenuItem");

        app.Expect("graph paper and pencil: both drawings wear it, it is remembered, and the ground is the sheet's colour", () =>
        {
            Assert.Equal(new SketchLook(SketchPaper.Graph, SketchLine.Pencil), window.Canvas.Look);
            Assert.Equal(window.Canvas.Look, window.Model.Look);
            Assert.Equal(SketchPaper.Graph, window.Settings.Current.SketchPaper);
            Assert.Equal(SketchLine.Pencil, window.Settings.Current.SketchLine);
            Assert.NotNull(window.FindControl<MenuItem>("PaperGraphMenuItem")!.Icon);
            Assert.Null(window.FindControl<MenuItem>("PaperScreenMenuItem")!.Icon);
            Assert.NotNull(window.FindControl<MenuItem>("LinePencilMenuItem")!.Icon);
            Assert.Equal(SketchColours.Graph, PaperAt(app, window, plan));
        });

        app.SaveFrame("plan-graph-pencil");
        app.Press(Key.V);

        app.Expect("the 3D view is on the same sheet", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.Equal(SketchColours.Graph, PaperAt(app, window, window.Model));
        });

        app.SaveFrame("3d-graph-pencil");
        Pick(app, window, "PaperPlainMenuItem");
        app.SaveFrame("3d-plain-pencil");
        app.Press(Key.V);
        app.SaveFrame("plan-plain-pencil");
        Pick(app, window, "PaperNapkinMenuItem");
        Pick(app, window, "LineCarpenterMenuItem");

        app.Expect("napkin and carpenter's pencil", () =>
        {
            Assert.Equal(new SketchLook(SketchPaper.Napkin, SketchLine.Carpenter), window.Canvas.Look);
            Assert.Equal(SketchColours.Napkin, PaperAt(app, window, plan));
        });

        app.SaveFrame("plan-napkin-carpenter");
        app.Press(Key.V);
        app.SaveFrame("3d-napkin-carpenter");
        app.Press(Key.V);

        // The look is drawing only: a part can still be drawn on it.
        app.Press(Key.R);
        app.Drag([new(620, 300), new(700, 340), new(803, 391)]);

        app.Expect("a part drawn under the look is a real part", () =>
            Assert.Equal(entities + 1, window.CurrentDesign!.Sketch.Entities.Count));

        Pick(app, window, "PaperScreenMenuItem");
        Pick(app, window, "LineCleanMenuItem");

        app.Expect("screen and clean bring the original paper back, and the choice is remembered", () =>
        {
            Assert.Equal(screen, PaperAt(app, window, plan));
            Assert.Equal(new SketchLook(), window.Model.Look);
            Assert.Equal(SketchPaper.Screen, window.Settings.Current.SketchPaper);
            Assert.Equal(SketchLine.Clean, window.Settings.Current.SketchLine);
        });
    });

    static void Pick(AppDriver app, MainWindow window, string itemName)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("SketchMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>(itemName)!));
    }

    static Color PaperAt(AppDriver app, MainWindow window, Control canvas)
    {
        Point corner = canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        int x = (int)corner.X + (int)canvas.Bounds.Width - 260;
        int y = (int)corner.Y + (int)canvas.Bounds.Height - 60;
        return FrameSampling.Patch(app, x, y, 24, 24)
            .GroupBy(color => color)
            .OrderByDescending(group => group.Count())
            .First().Key;
    }
}

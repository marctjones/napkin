using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// One key map for both views (#168): the tool keys do in the 3D view what their menu items do
/// there, and in a standard view they say why not rather than doing nothing.
/// </summary>
public class KeyMapWorkflows
{
    [GuiWorkflow("GUI-VIEW-11")]
    public void The_tool_keys_work_in_3D_and_say_why_not_in_a_standard_view() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Coffee table");
        Box apron = BoxNamed(window, "Apron, long, south");
        app.Click(OnPlan(window, apron.Center.XY));
        app.Press(Key.D7);
        app.Expect("7 shows 3D with the apron selected", () =>
        {
            Assert.Equal(DesignView.Model, window.CurrentView);
            Assert.Equal([apron.Id], window.Editor.Selection.Order());
        });

        app.Press(Key.R);
        app.Expect("R in 3D picks up a plain board to place, as Draw → Rectangle does there", () =>
        {
            Assert.True(window.Model.Placement.IsArmed);
            Assert.StartsWith("Holding a plain board", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.Press(Key.S);
        app.Expect("S puts it down and is the select tool, still in 3D", () =>
        {
            Assert.False(window.Model.Placement.IsArmed);
            Assert.Equal(EditTool.Select, window.Canvas.Tool);
            Assert.Equal(DesignView.Model, window.CurrentView);
        });

        app.Press(Key.D3);
        app.Press(Key.C);
        app.Expect("C in Front says the workshop is not there yet, and opens nothing", () =>
        {
            Assert.Equal(DesignView.Front, window.CurrentView);
            Assert.False(window.IsShapingPart);
            Assert.StartsWith("Not in a Front view yet", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.Press(Key.W);
        app.Expect("W brings the plan forward with the wall tool, as Draw → Wall does", () =>
        {
            Assert.Equal(DesignView.Top, window.CurrentView);
            Assert.Equal(EditTool.Wall, window.Canvas.Tool);
        });
    });
}

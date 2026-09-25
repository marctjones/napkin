using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;

using Xunit;
using Napkin.Modules.Editing;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>Which view a design opens in is the person's choice, and it is kept (#109).</summary>
public class OpenInWorkflows
{
    [GuiWorkflow("GUI-SET-04")]
    public void Designs_open_in_the_view_chosen_from_the_View_menu() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        app.Expect("by default a design opens in the last-used view, and the menu says so", () =>
        {
            Assert.Equal(OpenDesignsIn.LastUsed, window.Settings.Current.OpenIn);
            Assert.NotNull(window.FindControl<MenuItem>("OpenInLastMenuItem")!.Icon);
            Assert.False(window.IsShowingModel);
        });

        ChooseOpenIn(app, window, "OpenInModelMenuItem");

        app.Expect("choosing 3D does not move the view that is on screen", () =>
        {
            Assert.False(window.IsShowingModel);
            Assert.Equal(OpenDesignsIn.Model, window.Settings.Current.OpenIn);
            Assert.NotNull(window.FindControl<MenuItem>("OpenInModelMenuItem")!.Icon);
        });

        OpenSample(app, window, "Coffee table");

        app.Expect("the next design opens in 3D", () => Assert.True(window.IsShowingModel));

        // Back to the plan by hand; the next design still opens in 3D, whatever was last used.
        app.Press(Key.V);
        OpenSample(app, window, "Wall with window");

        app.Expect("with 3D chosen, a design opens in 3D even after the plan was last used", () => Assert.True(window.IsShowingModel));

        ChooseOpenIn(app, window, "OpenInPlanMenuItem");

        app.Expect("choosing the plan does not move the 3D view that is on screen", () => Assert.True(window.IsShowingModel));

        OpenSample(app, window, "Coffee table");

        app.Expect("the next design opens on the plan", () =>
        {
            Assert.False(window.IsShowingModel);
            Assert.Equal(OpenDesignsIn.Plan, window.Settings.Current.OpenIn);
        });

        // Last used follows the person: leave in 3D, and the next design is 3D.
        ChooseOpenIn(app, window, "OpenInLastMenuItem");
        app.Press(Key.V);
        OpenSample(app, window, "Wall with window");

        app.Expect("last used: the design opens in the view that was left in, 3D", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.Equal(DesignView.Model, window.Settings.Current.LastView);
        });

        ChooseOpenIn(app, window, "OpenInModelMenuItem");
        app.Press(Key.V);

        app.Expect("a new window on the same settings remembers the choice: a new sheet opens in 3D", () =>
        {
            var next = new MainWindow(new SettingsStore(window.Settings.Location));
            try
            {
                next.Show();
                Assert.NotNull(next.FindControl<MenuItem>("OpenInModelMenuItem")!.Icon);
                Assert.True(next.ShowDesign(new NewSheet()));
                Assert.True(next.IsShowingModel);
            }
            finally
            {
                next.Close();
            }
        });
    });

    static void ChooseOpenIn(AppDriver app, MainWindow window, string itemName)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("OpenInMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>(itemName)!));
    }
}

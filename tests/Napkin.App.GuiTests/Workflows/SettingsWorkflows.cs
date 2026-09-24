using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>Preferences the person sets from the View menu are still set in the next run (#102).</summary>
public class SettingsWorkflows
{
    [GuiWorkflow("GUI-SET-01")]
    public void A_setting_changed_from_the_View_menu_is_kept_by_a_new_window() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");

        app.Expect("a fresh run has the defaults: rulers off, perspective", () =>
        {
            Assert.False(window.Settings.Current.ShowRulers);
            Assert.Equal(CameraProjection.Perspective, window.Settings.Current.Projection);
        });

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("RulersMenuItem")!));
        app.Press(Key.V);
        app.Press(Key.O);

        app.Expect("the choices are written to the settings file as they are made", () =>
        {
            Assert.True(window.Settings.Current.ShowRulers);
            Assert.Equal(CameraProjection.Orthographic, window.Settings.Current.Projection);
            Assert.True(File.Exists(window.Settings.Location));
        });

        app.Expect("a new window on the same settings file starts with rulers on and orthographic", () =>
        {
            var next = new MainWindow(new Napkin.App.Settings.SettingsStore(window.Settings.Location));
            try
            {
                next.Show();
                Assert.True(next.Canvas.ShowRulers);
                Assert.NotNull(next.FindControl<MenuItem>("RulersMenuItem")!.Icon);
                Assert.Equal(CameraProjection.Orthographic, next.Model.Projection);
            }
            finally
            {
                next.Close();
            }
        });
    });
}

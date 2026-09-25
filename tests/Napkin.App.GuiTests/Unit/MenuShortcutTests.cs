using Avalonia.Controls;
using Avalonia.Input;
using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Modules.Editing;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The shortcuts the menus show are the ones <see cref="KeyMaps"/> binds: no two items show the
/// same keys, every shown key is a real binding, and the ones the review found missing or doubled
/// (<c>O</c>, <c>Home</c>, the list shortcuts, the shape item's mnemonic) are right.
/// </summary>
public class MenuShortcutTests
{
    [Fact]
    public void No_two_menu_items_show_the_same_keys_and_every_shown_key_is_bound()
    {
        HeadlessWindow.Run(window =>
        {
            List<(string Name, KeyGesture Gesture)> shown =
            [
                .. AllItems(window.MenuBar.Items)
                    .Where(item => item.InputGesture is not null)
                    .Select(item => (item.Name ?? item.Header?.ToString() ?? "?", item.InputGesture!)),
            ];

            Assert.NotEmpty(shown);
            foreach (IGrouping<string, (string Name, KeyGesture Gesture)> same in shown.GroupBy(pair => pair.Gesture.ToString()))
            {
                Assert.True(same.Count() == 1, $"{same.Key} is shown on {string.Join(", ", same.Select(pair => pair.Name))}");
            }

            foreach ((string name, KeyGesture gesture) in shown)
            {
                Keystroke? keystroke = KeyInput.From(gesture.Key, gesture.KeyModifiers);
                Assert.True(
                    keystroke is { } k && (KeyMaps.Shell.Find(k) is not null || KeyMaps.Edit.Find(k) is not null || KeyMaps.View.Find(k) is not null),
                    $"{name} shows {gesture}, which no map binds");
            }
        });
    }

    [Fact]
    public void The_lists_reset_view_and_projection_show_their_keys()
    {
        HeadlessWindow.Run(window =>
        {
            Assert.Equal(new Keystroke(KeyName.L, KeyMods.Command), Shown(window, "CutListMenuItem"));
            Assert.Equal(new Keystroke(KeyName.L, KeyMods.Command | KeyMods.Shift), Shown(window, "ShoppingListMenuItem"));
            Assert.Equal(new Keystroke(KeyName.Home), Shown(window, "ResetViewMenuItem"));
            Assert.Equal(new Keystroke(KeyName.D1), Shown(window, "TopViewMenuItem"));
            Assert.Equal(new Keystroke(KeyName.D7), Shown(window, "ModelViewMenuItem"));

            // O switches, so it is shown on the projection it would switch to, and only there.
            window.ShowView(Napkin.App.Settings.DesignView.Model);
            window.Model.Projection = CameraProjection.Orthographic;
            HeadlessWindow.Settle();

            Assert.Null(Shown(window, "OrthographicMenuItem"));
            Assert.Equal(new Keystroke(KeyName.O), Shown(window, "PerspectiveMenuItem"));

            window.Model.Projection = CameraProjection.Perspective;
            HeadlessWindow.Settle();

            Assert.Equal(new Keystroke(KeyName.O), Shown(window, "OrthographicMenuItem"));
            Assert.Null(Shown(window, "PerspectiveMenuItem"));
        });
    }

    [Fact]
    public void The_shape_item_is_reached_by_the_same_letter_its_key_is()
    {
        HeadlessWindow.Run(window =>
        {
            MenuItem shape = AllItems(window.MenuBar.Items).Single(item => item.Name == "ShapeMenuItem");
            string header = (string)shape.Header!;
            char mnemonic = char.ToUpperInvariant(header[header.IndexOf('_', StringComparison.Ordinal) + 1]);

            Assert.Equal(new Keystroke(KeyName.C), Shown(window, "ShapeMenuItem"));
            Assert.Equal('C', mnemonic);
        });
    }

    static Keystroke? Shown(MainWindow window, string name) =>
        AllItems(window.MenuBar.Items).Single(item => item.Name == name).InputGesture is { } gesture
            ? KeyInput.From(gesture.Key, gesture.KeyModifiers)
            : null;

    static IEnumerable<MenuItem> AllItems(IEnumerable<object?> items)
    {
        foreach (MenuItem item in items.OfType<MenuItem>())
        {
            yield return item;
            foreach (MenuItem child in AllItems(item.Items))
            {
                yield return child;
            }
        }
    }
}

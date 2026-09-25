using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Every keyboard shortcut, checked binding by binding against a table written out here by hand —
/// not read back from <see cref="KeyMaps"/> — plus the rules the table keeps: one meaning per
/// keystroke, every command reachable, the command key only where it belongs, and
/// <c>docs/shortcuts.md</c> saying exactly what the table says.
/// </summary>
public class KeyMapTests
{
    const KeyMods None = KeyMods.None;
    const KeyMods Shift = KeyMods.Shift;
    const KeyMods Cmd = KeyMods.Command;

    public static TheoryData<KeyName, KeyMods, ShellCommand> ShellBindings => new()
    {
        { KeyName.N, Cmd, ShellCommand.New },
        { KeyName.O, Cmd, ShellCommand.Open },
        { KeyName.S, Cmd, ShellCommand.Save },
        { KeyName.S, Cmd | Shift, ShellCommand.SaveAs },
        { KeyName.Z, Cmd, ShellCommand.Undo },
        { KeyName.Z, Cmd | Shift, ShellCommand.Redo },
        { KeyName.Y, Cmd, ShellCommand.Redo },
        { KeyName.L, Cmd, ShellCommand.CutList },
        { KeyName.L, Cmd | Shift, ShellCommand.ShoppingList },
        { KeyName.D1, Cmd, ShellCommand.Sample1 },
        { KeyName.D2, Cmd, ShellCommand.Sample2 },
        { KeyName.D3, Cmd, ShellCommand.Sample3 },
        { KeyName.D4, Cmd, ShellCommand.Sample4 },
        { KeyName.D5, Cmd, ShellCommand.Sample5 },
        { KeyName.D6, Cmd, ShellCommand.Sample6 },
        { KeyName.D7, Cmd, ShellCommand.Sample7 },
        { KeyName.D8, Cmd, ShellCommand.Sample8 },
        { KeyName.D9, Cmd, ShellCommand.Sample9 },
    };

    public static TheoryData<KeyName, KeyMods, EditCommand> EditBindings => new()
    {
        { KeyName.S, None, EditCommand.SelectTool },
        { KeyName.R, None, EditCommand.RectangleTool },
        { KeyName.W, None, EditCommand.WallTool },
        { KeyName.C, None, EditCommand.Shape },
        { KeyName.D, None, EditCommand.Duplicate },
        { KeyName.M, None, EditCommand.MirrorEastWest },
        { KeyName.M, Shift, EditCommand.MirrorNorthSouth },
        { KeyName.P, None, EditCommand.Pin },
        { KeyName.Delete, None, EditCommand.Delete },
        { KeyName.Backspace, None, EditCommand.Delete },
        { KeyName.J, None, EditCommand.Join },
        { KeyName.J, Shift, EditCommand.JoinAll },
        { KeyName.Q, None, EditCommand.ToggleRough },
        { KeyName.X, None, EditCommand.TurnX },
        { KeyName.X, Shift, EditCommand.TurnXBack },
        { KeyName.Y, None, EditCommand.TurnY },
        { KeyName.Y, Shift, EditCommand.TurnYBack },
        { KeyName.Z, None, EditCommand.TurnZ },
        { KeyName.Z, Shift, EditCommand.TurnZBack },
        { KeyName.V, None, EditCommand.OtherView },
        { KeyName.G, None, EditCommand.ToggleGrid },
        { KeyName.H, None, EditCommand.ToggleHiddenEdges },
        { KeyName.Escape, None, EditCommand.Cancel },
        { KeyName.Enter, None, EditCommand.Confirm },
        { KeyName.Tab, None, EditCommand.EditWidth },
        { KeyName.Left, None, EditCommand.NudgeLeft },
        { KeyName.Right, None, EditCommand.NudgeRight },
        { KeyName.Up, None, EditCommand.NudgeUp },
        { KeyName.Down, None, EditCommand.NudgeDown },
        { KeyName.Left, Shift, EditCommand.NudgeFarLeft },
        { KeyName.Right, Shift, EditCommand.NudgeFarRight },
        { KeyName.Up, Shift, EditCommand.NudgeFarUp },
        { KeyName.Down, Shift, EditCommand.NudgeFarDown },
    };

    public static TheoryData<KeyName, KeyMods, ViewCommand> ViewBindings => new()
    {
        { KeyName.D0, Cmd, ViewCommand.ZoomToFit },
        { KeyName.Home, None, ViewCommand.ResetView },
        { KeyName.Plus, None, ViewCommand.ZoomIn },
        { KeyName.Plus, Shift, ViewCommand.ZoomIn },
        { KeyName.Plus, Cmd, ViewCommand.ZoomIn },
        { KeyName.Minus, None, ViewCommand.ZoomOut },
        { KeyName.Minus, Cmd, ViewCommand.ZoomOut },
        { KeyName.Left, None, ViewCommand.Left },
        { KeyName.Right, None, ViewCommand.Right },
        { KeyName.Up, None, ViewCommand.Up },
        { KeyName.Down, None, ViewCommand.Down },
        { KeyName.Left, Shift, ViewCommand.FarLeft },
        { KeyName.Right, Shift, ViewCommand.FarRight },
        { KeyName.Up, Shift, ViewCommand.FarUp },
        { KeyName.Down, Shift, ViewCommand.FarDown },
        { KeyName.O, None, ViewCommand.Projection },
        { KeyName.D1, None, ViewCommand.ShowTop },
        { KeyName.D2, None, ViewCommand.ShowBottom },
        { KeyName.D3, None, ViewCommand.ShowFront },
        { KeyName.D4, None, ViewCommand.ShowBack },
        { KeyName.D5, None, ViewCommand.ShowLeft },
        { KeyName.D6, None, ViewCommand.ShowRight },
        { KeyName.D7, None, ViewCommand.ShowModel },
    };

    [Theory]
    [MemberData(nameof(ShellBindings))]
    public void Every_window_shortcut_is_bound_as_listed(KeyName key, KeyMods mods, ShellCommand command) =>
        Assert.Equal(command, KeyMaps.Shell.Find(new Keystroke(key, mods)));

    [Theory]
    [MemberData(nameof(EditBindings))]
    public void Every_editing_key_is_bound_as_listed(KeyName key, KeyMods mods, EditCommand command) =>
        Assert.Equal(command, KeyMaps.Edit.Find(new Keystroke(key, mods)));

    [Theory]
    [MemberData(nameof(ViewBindings))]
    public void Every_view_key_is_bound_as_listed(KeyName key, KeyMods mods, ViewCommand command) =>
        Assert.Equal(command, KeyMaps.View.Find(new Keystroke(key, mods)));

    [Fact]
    public void Each_map_holds_exactly_the_listed_bindings_and_nothing_else()
    {
        Assert.True(Listed(ShellBindings).SetEquals(Bound(KeyMaps.Shell)));
        Assert.True(Listed(EditBindings).SetEquals(Bound(KeyMaps.Edit)));
        Assert.True(Listed(ViewBindings).SetEquals(Bound(KeyMaps.View)));
        Assert.Equal(ShellBindings.Count(), KeyMaps.Shell.Shortcuts.Count);
        Assert.Equal(EditBindings.Count(), KeyMaps.Edit.Shortcuts.Count);
        Assert.Equal(ViewBindings.Count(), KeyMaps.View.Shortcuts.Count);
    }

    [Fact]
    public void No_keystroke_means_two_things_within_a_map()
    {
        AssertDistinct(KeyMaps.Shell);
        AssertDistinct(KeyMaps.Edit);
        AssertDistinct(KeyMaps.View);
    }

    [Fact]
    public void Across_maps_only_the_arrows_are_shared_edit_first_then_view()
    {
        HashSet<Keystroke> shell = [.. KeyMaps.Shell.Shortcuts.Select(s => s.Keystroke)];
        HashSet<Keystroke> edit = [.. KeyMaps.Edit.Shortcuts.Select(s => s.Keystroke)];
        HashSet<Keystroke> view = [.. KeyMaps.View.Shortcuts.Select(s => s.Keystroke)];

        Assert.Empty(shell.Intersect(edit));
        Assert.Empty(shell.Intersect(view));
        HashSet<Keystroke> arrows = [.. new[] { KeyName.Left, KeyName.Right, KeyName.Up, KeyName.Down }
            .SelectMany(k => new[] { new Keystroke(k), new Keystroke(k, Shift) })];
        Assert.True(arrows.SetEquals(edit.Intersect(view)), string.Join(", ", edit.Intersect(view)));
    }

    [Fact]
    public void Every_command_has_a_key_and_words()
    {
        foreach (ShellCommand command in Enum.GetValues<ShellCommand>())
        {
            Assert.NotEmpty(KeyMaps.Shell.KeysFor(command));
            Assert.False(string.IsNullOrWhiteSpace(KeyMaps.Shell.Words(command)));
        }

        foreach (EditCommand command in Enum.GetValues<EditCommand>())
        {
            Assert.NotEmpty(KeyMaps.Edit.KeysFor(command));
            Assert.False(string.IsNullOrWhiteSpace(KeyMaps.Edit.Words(command)));
        }

        foreach (ViewCommand command in Enum.GetValues<ViewCommand>())
        {
            Assert.NotEmpty(KeyMaps.View.KeysFor(command));
            Assert.False(string.IsNullOrWhiteSpace(KeyMaps.View.Words(command)));
        }
    }

    [Fact]
    public void The_window_shortcuts_all_hold_the_command_key_and_the_editing_keys_never_do()
    {
        Assert.All(KeyMaps.Shell.Shortcuts, s => Assert.True(s.Keystroke.Mods.HasFlag(Cmd), s.Keystroke.ToString()));
        Assert.All(KeyMaps.Edit.Shortcuts, s => Assert.False(s.Keystroke.Mods.HasFlag(Cmd), s.Keystroke.ToString()));
        Assert.All(
            KeyMaps.View.Shortcuts.Where(s => s.Keystroke.Mods.HasFlag(Cmd)),
            s => Assert.Contains(s.Keystroke.Key, new[] { KeyName.D0, KeyName.Plus, KeyName.Minus }));
        Assert.All(
            [.. KeyMaps.Shell.Shortcuts.Select(s => s.Keystroke), .. KeyMaps.Edit.Shortcuts.Select(s => s.Keystroke), .. KeyMaps.View.Shortcuts.Select(s => s.Keystroke)],
            k => Assert.False(k.Mods.HasFlag(KeyMods.Alt), k.ToString()));
    }

    [Theory]
    [InlineData(KeyName.R, KeyMods.Alt)]
    [InlineData(KeyName.R, KeyMods.Shift)]
    [InlineData(KeyName.R, KeyMods.Command)]
    [InlineData(KeyName.D, KeyMods.Shift)]
    [InlineData(KeyName.P, KeyMods.Shift)]
    [InlineData(KeyName.Q, KeyMods.Shift)]
    [InlineData(KeyName.O, KeyMods.Shift)]
    public void A_modifier_a_shortcut_does_not_hold_makes_it_a_different_key(KeyName key, KeyMods mods)
    {
        Keystroke pressed = new(key, mods);
        Assert.Null(KeyMaps.Edit.Find(pressed));
        Assert.Null(KeyMaps.View.Find(pressed));
    }

    [Theory]
    [InlineData(KeyName.D1, KeyMods.Alt)]
    [InlineData(KeyName.D1, KeyMods.Shift)]
    [InlineData(KeyName.D8, KeyMods.None)]
    [InlineData(KeyName.D0, KeyMods.None)]
    [InlineData(KeyName.Home, KeyMods.Command)]
    public void A_digit_or_home_with_the_wrong_modifiers_is_no_view_key(KeyName key, KeyMods mods) =>
        Assert.Null(KeyMaps.View.Find(new Keystroke(key, mods)));

    [Fact]
    public void The_first_key_for_a_command_is_the_one_a_menu_shows()
    {
        Assert.Equal(new Keystroke(KeyName.Delete), KeyMaps.Edit.KeysFor(EditCommand.Delete)[0]);
        Assert.Equal(new Keystroke(KeyName.Backspace), KeyMaps.Edit.KeysFor(EditCommand.Delete)[1]);
        Assert.Equal(new Keystroke(KeyName.Z, Cmd | Shift), KeyMaps.Shell.KeysFor(ShellCommand.Redo)[0]);
        Assert.Equal(new Keystroke(KeyName.Plus), KeyMaps.View.KeysFor(ViewCommand.ZoomIn)[0]);
        Assert.Equal(3, KeyMaps.View.KeysFor(ViewCommand.ZoomIn).Count);
    }

    [Theory]
    [InlineData(KeyName.L, KeyMods.Command | KeyMods.Shift, "Ctrl/Cmd+Shift+L")]
    [InlineData(KeyName.M, KeyMods.Shift, "Shift+M")]
    [InlineData(KeyName.D7, KeyMods.None, "7")]
    [InlineData(KeyName.D0, KeyMods.Command, "Ctrl/Cmd+0")]
    [InlineData(KeyName.Plus, KeyMods.None, "+")]
    [InlineData(KeyName.Minus, KeyMods.Command, "Ctrl/Cmd+-")]
    [InlineData(KeyName.Backspace, KeyMods.None, "Backspace")]
    [InlineData(KeyName.Q, KeyMods.Alt | KeyMods.Shift, "Alt+Shift+Q")]
    public void A_keystroke_is_written_the_same_on_every_platform(KeyName key, KeyMods mods, string expected) =>
        Assert.Equal(expected, new Keystroke(key, mods).ToString());

    [Fact]
    public void The_shortcut_list_lists_every_command_with_all_its_keys_once()
    {
        string doc = KeyMaps.Markdown();

        Assert.Contains("| `Delete`, `Backspace` | Delete the selected joint, or the selection |", doc, StringComparison.Ordinal);
        Assert.Contains("| `Ctrl/Cmd+Shift+Z`, `Ctrl/Cmd+Y` | Redo", doc, StringComparison.Ordinal);
        Assert.Contains("| `7` | 3D |", doc, StringComparison.Ordinal);
        Assert.Contains("## Anywhere in the window", doc, StringComparison.Ordinal);
        Assert.Contains("## Editing", doc, StringComparison.Ordinal);
        Assert.Contains("## Moving about the view", doc, StringComparison.Ordinal);
        int rows = doc.Split('\n').Count(line => line.StartsWith("| `", StringComparison.Ordinal));
        Assert.Equal(Enum.GetValues<ShellCommand>().Length + Enum.GetValues<EditCommand>().Length + Enum.GetValues<ViewCommand>().Length, rows);
    }

    [Fact]
    public void Docs_shortcuts_md_is_the_table()
    {
        string path = Path.Combine(RepositoryRoot(), "docs", "shortcuts.md");
        string expected = KeyMaps.Markdown();
        if (Environment.GetEnvironmentVariable("NAPKIN_WRITE_SHORTCUTS") == "1")
        {
            File.WriteAllText(path, expected);
        }

        Assert.True(File.Exists(path), "docs/shortcuts.md is missing: run this test with NAPKIN_WRITE_SHORTCUTS=1.");
        Assert.Equal(expected, File.ReadAllText(path).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_table_with_a_keystroke_twice_is_refused() =>
        Assert.Throws<ArgumentException>(() => new KeyMap<EditCommand>(
            [new(new Keystroke(KeyName.D), EditCommand.Duplicate), new(new Keystroke(KeyName.D), EditCommand.Pin)],
            new Dictionary<EditCommand, string>()));

    static string RepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "napkin.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException($"napkin.sln not found above {AppContext.BaseDirectory}.");
    }

    static void AssertDistinct<T>(KeyMap<T> map)
        where T : struct, Enum
    {
        List<Keystroke> keys = [.. map.Shortcuts.Select(s => s.Keystroke)];
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    static HashSet<(KeyName, KeyMods, T)> Listed<T>(TheoryData<KeyName, KeyMods, T> data) =>
        [.. data.Select(row => ((KeyName)row[0], (KeyMods)row[1], (T)row[2]))];

    static HashSet<(KeyName, KeyMods, T)> Bound<T>(KeyMap<T> map)
        where T : struct, Enum =>
        [.. map.Shortcuts.Select(s => (s.Keystroke.Key, s.Keystroke.Mods, s.Command))];
}

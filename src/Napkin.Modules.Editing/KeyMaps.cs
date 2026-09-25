using System.Collections.Frozen;
using System.Text;

namespace Napkin.Modules.Editing;

/// <summary>What an editing key asks for: the same command whichever view has the keyboard.</summary>
/// <remarks>
/// The plan and the 3D view both raise one of these and the window runs it, once, the same way its
/// menu item does. What a view does first on its own — an Escape that ends a drag, X while holding
/// a part to place, an arrow that nudges the selection — is the view's, because it is the view's
/// state; everything else is the window's.
/// </remarks>
public enum EditCommand
{
    /// <summary>The select tool.</summary>
    SelectTool,

    /// <summary>The rectangle tool; in the 3D view, a plain board to place.</summary>
    RectangleTool,

    /// <summary>The wall tool, with the member it last had; walls are drawn in the plan.</summary>
    WallTool,

    /// <summary>Open the selected part in the shape workshop.</summary>
    Shape,

    /// <summary>Copy the selection, beside it.</summary>
    Duplicate,

    /// <summary>Copy the selection across the drawing's middle, east to west.</summary>
    MirrorEastWest,

    /// <summary>Copy the selection across the drawing's middle, north to south.</summary>
    MirrorNorthSouth,

    /// <summary>Pin the selection where it is.</summary>
    Pin,

    /// <summary>Remove the selected joint, or the selection, relationships and all.</summary>
    Delete,

    /// <summary>Join the two selected parts.</summary>
    Join,

    /// <summary>Join every pair of parts that touch.</summary>
    JoinAll,

    /// <summary>Switch between Rough and Precise entry (docs/design/sketch-mode.md &#xA7;1.1).</summary>
    ToggleRough,

    /// <summary>A quarter turn of the selection about X.</summary>
    TurnX,

    /// <summary>A quarter turn of the selection about X, the other way.</summary>
    TurnXBack,

    /// <summary>A quarter turn of the selection about Y.</summary>
    TurnY,

    /// <summary>A quarter turn of the selection about Y, the other way.</summary>
    TurnYBack,

    /// <summary>A quarter turn of the selection about Z.</summary>
    TurnZ,

    /// <summary>A quarter turn of the selection about Z, the other way.</summary>
    TurnZBack,

    /// <summary>The 3D view from a flat one; the last flat one from the 3D view.</summary>
    OtherView,

    /// <summary>Show or hide the grid.</summary>
    ToggleGrid,

    /// <summary>Show or hide the hidden edges' dashes in a standard view.</summary>
    ToggleHiddenEdges,

    /// <summary>Stop what is under way, or let go of the selection.</summary>
    Cancel,

    /// <summary>Open the selected joint to edit it.</summary>
    Confirm,

    /// <summary>Type the selected part's width.</summary>
    EditWidth,

    /// <summary>Move the selection one grid step west.</summary>
    NudgeLeft,

    /// <summary>Move the selection one grid step east.</summary>
    NudgeRight,

    /// <summary>Move the selection one grid step north.</summary>
    NudgeUp,

    /// <summary>Move the selection one grid step south.</summary>
    NudgeDown,

    /// <summary>Move the selection four grid steps west.</summary>
    NudgeFarLeft,

    /// <summary>Move the selection four grid steps east.</summary>
    NudgeFarRight,

    /// <summary>Move the selection four grid steps north.</summary>
    NudgeFarUp,

    /// <summary>Move the selection four grid steps south.</summary>
    NudgeFarDown,
}

/// <summary>What a view key asks for. Each view does it its own way: an arrow pans the plan and orbits the 3D view.</summary>
public enum ViewCommand
{
    /// <summary>Fit the drawing in the view.</summary>
    ZoomToFit,

    /// <summary>Back to the view's starting point: the plan fits, the 3D view returns to its first angle.</summary>
    ResetView,

    /// <summary>Zoom in a step.</summary>
    ZoomIn,

    /// <summary>Zoom out a step.</summary>
    ZoomOut,

    /// <summary>Pan left (the plan, a standard view), or orbit (3D).</summary>
    Left,

    /// <summary>Pan right, or orbit.</summary>
    Right,

    /// <summary>Pan up, or orbit.</summary>
    Up,

    /// <summary>Pan down, or orbit.</summary>
    Down,

    /// <summary>Pan left further, or orbit further.</summary>
    FarLeft,

    /// <summary>Pan right further, or orbit further.</summary>
    FarRight,

    /// <summary>Pan up further, or orbit further.</summary>
    FarUp,

    /// <summary>Pan down further, or orbit further.</summary>
    FarDown,

    /// <summary>Switch the 3D view between orthographic and perspective.</summary>
    Projection,

    /// <summary>The plan, from the top.</summary>
    ShowTop,

    /// <summary>From the bottom.</summary>
    ShowBottom,

    /// <summary>From the front.</summary>
    ShowFront,

    /// <summary>From the back.</summary>
    ShowBack,

    /// <summary>From the left.</summary>
    ShowLeft,

    /// <summary>From the right.</summary>
    ShowRight,

    /// <summary>The 3D view.</summary>
    ShowModel,
}

/// <summary>What a window-wide shortcut (always with the command key) asks for.</summary>
public enum ShellCommand
{
    /// <summary>A new, empty design.</summary>
    New,

    /// <summary>Open a design file.</summary>
    Open,

    /// <summary>Save.</summary>
    Save,

    /// <summary>Save under a new name.</summary>
    SaveAs,

    /// <summary>Undo.</summary>
    Undo,

    /// <summary>Redo.</summary>
    Redo,

    /// <summary>The cut list.</summary>
    CutList,

    /// <summary>The shopping list.</summary>
    ShoppingList,

    /// <summary>The first sample.</summary>
    Sample1,

    /// <summary>The second sample.</summary>
    Sample2,

    /// <summary>The third sample.</summary>
    Sample3,

    /// <summary>The fourth sample.</summary>
    Sample4,

    /// <summary>The fifth sample.</summary>
    Sample5,

    /// <summary>The sixth sample.</summary>
    Sample6,

    /// <summary>The seventh sample.</summary>
    Sample7,

    /// <summary>The eighth sample.</summary>
    Sample8,

    /// <summary>The ninth sample.</summary>
    Sample9,
}

/// <summary>One key bound to one command, with what the shortcut list says it does.</summary>
/// <typeparam name="TCommand">The kind of command.</typeparam>
/// <param name="Keystroke">The key and modifiers.</param>
/// <param name="Command">What it asks for.</param>
public sealed record Shortcut<TCommand>(Keystroke Keystroke, TCommand Command)
    where TCommand : struct, Enum;

/// <summary>A table of shortcuts: each keystroke at most once, looked up both ways.</summary>
/// <typeparam name="TCommand">The kind of command.</typeparam>
public sealed class KeyMap<TCommand>
    where TCommand : struct, Enum
{
    readonly FrozenDictionary<Keystroke, TCommand> _byKey;
    readonly IReadOnlyDictionary<TCommand, string> _words;

    /// <summary>A table from its shortcuts, in order, and what each command does in words.</summary>
    /// <exception cref="ArgumentException">A keystroke appears twice.</exception>
    public KeyMap(IReadOnlyList<Shortcut<TCommand>> shortcuts, IReadOnlyDictionary<TCommand, string> words)
    {
        // ToFrozenDictionary throws on a repeated keystroke: a key that means two things is a bug
        // in this table, found the first time anything asks it for anything.
        _byKey = shortcuts.ToDictionary(shortcut => shortcut.Keystroke, shortcut => shortcut.Command).ToFrozenDictionary();
        Shortcuts = shortcuts;
        _words = words;
    }

    /// <summary>Every shortcut, in the order the shortcut list shows them.</summary>
    public IReadOnlyList<Shortcut<TCommand>> Shortcuts { get; }

    /// <summary>What <paramref name="keystroke"/> asks for, or nothing.</summary>
    public TCommand? Find(Keystroke keystroke) =>
        _byKey.TryGetValue(keystroke, out TCommand command) ? command : null;

    /// <summary>The keys bound to <paramref name="command"/>, the one a menu shows first.</summary>
    public IReadOnlyList<Keystroke> KeysFor(TCommand command) =>
        [.. Shortcuts.Where(shortcut => EqualityComparer<TCommand>.Default.Equals(shortcut.Command, command)).Select(shortcut => shortcut.Keystroke)];

    /// <summary>What the shortcut list says <paramref name="command"/> does.</summary>
    public string Words(TCommand command) => _words[command];
}

/// <summary>
/// Every keyboard shortcut napkin has, in three tables: the window's (with the command key), the
/// editing keys and the view keys (with the drawing focused). The views, the window's key bindings,
/// the menus' shown gestures and <c>docs/shortcuts.md</c> all read them from here.
/// </summary>
public static class KeyMaps
{
    static Keystroke K(KeyName key, KeyMods mods = KeyMods.None) => new(key, mods);

    static Keystroke Shift(KeyName key) => new(key, KeyMods.Shift);

    static Keystroke Command(KeyName key, KeyMods also = KeyMods.None) => new(key, KeyMods.Command | also);

    /// <summary>The window's shortcuts: always with Control (Windows, Linux) or Command (macOS).</summary>
    public static KeyMap<ShellCommand> Shell { get; } = new(
        [
            new(Command(KeyName.N), ShellCommand.New),
            new(Command(KeyName.O), ShellCommand.Open),
            new(Command(KeyName.S), ShellCommand.Save),
            new(Command(KeyName.S, KeyMods.Shift), ShellCommand.SaveAs),
            new(Command(KeyName.Z), ShellCommand.Undo),
            new(Command(KeyName.Z, KeyMods.Shift), ShellCommand.Redo),
            new(Command(KeyName.Y), ShellCommand.Redo),
            new(Command(KeyName.L), ShellCommand.CutList),
            new(Command(KeyName.L, KeyMods.Shift), ShellCommand.ShoppingList),
            .. Enumerable.Range(0, 9).Select(i => new Shortcut<ShellCommand>(Command(KeyName.D1 + i), ShellCommand.Sample1 + i)),
        ],
        new Dictionary<ShellCommand, string>
        {
            [ShellCommand.New] = "New design",
            [ShellCommand.Open] = "Open a design",
            [ShellCommand.Save] = "Save",
            [ShellCommand.SaveAs] = "Save as",
            [ShellCommand.Undo] = "Undo",
            [ShellCommand.Redo] = "Redo (the platform's own keys: Ctrl+Y first on Windows, Cmd+Shift+Z on macOS)",
            [ShellCommand.CutList] = "Cut list",
            [ShellCommand.ShoppingList] = "Shopping list",
            [ShellCommand.Sample1] = "Open the first sample",
            [ShellCommand.Sample2] = "Open the second sample",
            [ShellCommand.Sample3] = "Open the third sample",
            [ShellCommand.Sample4] = "Open the fourth sample",
            [ShellCommand.Sample5] = "Open the fifth sample",
            [ShellCommand.Sample6] = "Open the sixth sample",
            [ShellCommand.Sample7] = "Open the seventh sample",
            [ShellCommand.Sample8] = "Open the eighth sample",
            [ShellCommand.Sample9] = "Open the ninth sample",
        });

    /// <summary>The editing keys: no command key, the drawing focused, the same in the plan and the 3D view.</summary>
    public static KeyMap<EditCommand> Edit { get; } = new(
        [
            new(K(KeyName.S), EditCommand.SelectTool),
            new(K(KeyName.R), EditCommand.RectangleTool),
            new(K(KeyName.W), EditCommand.WallTool),
            new(K(KeyName.C), EditCommand.Shape),
            new(K(KeyName.D), EditCommand.Duplicate),
            new(K(KeyName.M), EditCommand.MirrorEastWest),
            new(Shift(KeyName.M), EditCommand.MirrorNorthSouth),
            new(K(KeyName.P), EditCommand.Pin),
            new(K(KeyName.Delete), EditCommand.Delete),
            new(K(KeyName.Backspace), EditCommand.Delete),
            new(K(KeyName.J), EditCommand.Join),
            new(Shift(KeyName.J), EditCommand.JoinAll),
            new(K(KeyName.Q), EditCommand.ToggleRough),
            new(K(KeyName.X), EditCommand.TurnX),
            new(Shift(KeyName.X), EditCommand.TurnXBack),
            new(K(KeyName.Y), EditCommand.TurnY),
            new(Shift(KeyName.Y), EditCommand.TurnYBack),
            new(K(KeyName.Z), EditCommand.TurnZ),
            new(Shift(KeyName.Z), EditCommand.TurnZBack),
            new(K(KeyName.V), EditCommand.OtherView),
            new(K(KeyName.G), EditCommand.ToggleGrid),
            new(K(KeyName.H), EditCommand.ToggleHiddenEdges),
            new(K(KeyName.Escape), EditCommand.Cancel),
            new(K(KeyName.Enter), EditCommand.Confirm),
            new(K(KeyName.Tab), EditCommand.EditWidth),
            new(K(KeyName.Left), EditCommand.NudgeLeft),
            new(K(KeyName.Right), EditCommand.NudgeRight),
            new(K(KeyName.Up), EditCommand.NudgeUp),
            new(K(KeyName.Down), EditCommand.NudgeDown),
            new(Shift(KeyName.Left), EditCommand.NudgeFarLeft),
            new(Shift(KeyName.Right), EditCommand.NudgeFarRight),
            new(Shift(KeyName.Up), EditCommand.NudgeFarUp),
            new(Shift(KeyName.Down), EditCommand.NudgeFarDown),
        ],
        new Dictionary<EditCommand, string>
        {
            [EditCommand.SelectTool] = "Select tool",
            [EditCommand.RectangleTool] = "Rectangle tool; in 3D, a plain board to place; in a standard view, says why not",
            [EditCommand.WallTool] = "Wall tool, with the member it last had; brings the plan forward",
            [EditCommand.Shape] = "Cut the selected part to shape (the shape workshop); not in a standard view",
            [EditCommand.Duplicate] = "Duplicate the selection",
            [EditCommand.MirrorEastWest] = "Mirror copy east–west",
            [EditCommand.MirrorNorthSouth] = "Mirror copy north–south",
            [EditCommand.Pin] = "Pin the selection in place",
            [EditCommand.Delete] = "Delete the selected joint, or the selection",
            [EditCommand.Join] = "Join the two selected parts",
            [EditCommand.JoinAll] = "Join every pair of touching parts",
            [EditCommand.ToggleRough] = "Rough sketching on or off: big round steps, nothing stated, rectangles drawn as planks",
            [EditCommand.TurnX] = "Turn the selection about X (while placing stock in 3D: turn what is held)",
            [EditCommand.TurnXBack] = "Turn about X the other way",
            [EditCommand.TurnY] = "Turn the selection about Y (while placing: turn what is held)",
            [EditCommand.TurnYBack] = "Turn about Y the other way",
            [EditCommand.TurnZ] = "Turn the selection about Z (while placing: turn what is held)",
            [EditCommand.TurnZBack] = "Turn about Z the other way",
            [EditCommand.OtherView] = "3D from a flat view; back to the last flat view from 3D",
            [EditCommand.ToggleGrid] = "Show or hide the grid",
            [EditCommand.ToggleHiddenEdges] = "Show or hide hidden edges, as light dashes (Bottom, Front, Back, Left, Right)",
            [EditCommand.Cancel] = "Stop what is under way, put down the tool, or let go of the selection",
            [EditCommand.Confirm] = "Edit the selected joint",
            [EditCommand.EditWidth] = "Type the selected part's width (plan)",
            [EditCommand.NudgeLeft] = "Nudge the selection one grid step west (plan; with nothing selected the arrow pans)",
            [EditCommand.NudgeRight] = "Nudge one grid step east",
            [EditCommand.NudgeUp] = "Nudge one grid step north",
            [EditCommand.NudgeDown] = "Nudge one grid step south",
            [EditCommand.NudgeFarLeft] = "Nudge four grid steps west",
            [EditCommand.NudgeFarRight] = "Nudge four grid steps east",
            [EditCommand.NudgeFarUp] = "Nudge four grid steps north",
            [EditCommand.NudgeFarDown] = "Nudge four grid steps south",
        });

    /// <summary>The view keys: the drawing focused, or nothing that takes typing.</summary>
    public static KeyMap<ViewCommand> View { get; } = new(
        [
            new(Command(KeyName.D0), ViewCommand.ZoomToFit),
            new(K(KeyName.Home), ViewCommand.ResetView),
            new(K(KeyName.Plus), ViewCommand.ZoomIn),
            new(Shift(KeyName.Plus), ViewCommand.ZoomIn),
            new(Command(KeyName.Plus), ViewCommand.ZoomIn),
            new(K(KeyName.Minus), ViewCommand.ZoomOut),
            new(Command(KeyName.Minus), ViewCommand.ZoomOut),
            new(K(KeyName.Left), ViewCommand.Left),
            new(K(KeyName.Right), ViewCommand.Right),
            new(K(KeyName.Up), ViewCommand.Up),
            new(K(KeyName.Down), ViewCommand.Down),
            new(Shift(KeyName.Left), ViewCommand.FarLeft),
            new(Shift(KeyName.Right), ViewCommand.FarRight),
            new(Shift(KeyName.Up), ViewCommand.FarUp),
            new(Shift(KeyName.Down), ViewCommand.FarDown),
            new(K(KeyName.O), ViewCommand.Projection),
            new(K(KeyName.D1), ViewCommand.ShowTop),
            new(K(KeyName.D2), ViewCommand.ShowBottom),
            new(K(KeyName.D3), ViewCommand.ShowFront),
            new(K(KeyName.D4), ViewCommand.ShowBack),
            new(K(KeyName.D5), ViewCommand.ShowLeft),
            new(K(KeyName.D6), ViewCommand.ShowRight),
            new(K(KeyName.D7), ViewCommand.ShowModel),
        ],
        new Dictionary<ViewCommand, string>
        {
            [ViewCommand.ZoomToFit] = "Zoom to fit",
            [ViewCommand.ResetView] = "Reset the view: the plan fits; 3D goes back to its first angle",
            [ViewCommand.ZoomIn] = "Zoom in",
            [ViewCommand.ZoomOut] = "Zoom out",
            [ViewCommand.Left] = "Pan left (plan, standard views); orbit (3D)",
            [ViewCommand.Right] = "Pan right; orbit",
            [ViewCommand.Up] = "Pan up; orbit",
            [ViewCommand.Down] = "Pan down; orbit",
            [ViewCommand.FarLeft] = "Pan or orbit further",
            [ViewCommand.FarRight] = "Pan or orbit further",
            [ViewCommand.FarUp] = "Pan or orbit further",
            [ViewCommand.FarDown] = "Pan or orbit further",
            [ViewCommand.Projection] = "Orthographic or perspective (3D; a standard view is always orthographic)",
            [ViewCommand.ShowTop] = "Top (the plan)",
            [ViewCommand.ShowBottom] = "Bottom",
            [ViewCommand.ShowFront] = "Front",
            [ViewCommand.ShowBack] = "Back",
            [ViewCommand.ShowLeft] = "Left",
            [ViewCommand.ShowRight] = "Right",
            [ViewCommand.ShowModel] = "3D",
        });

    /// <summary>The shortcut list, as <c>docs/shortcuts.md</c> holds it.</summary>
    public static string Markdown()
    {
        StringBuilder text = new();
        text.Append("# Keyboard shortcuts\n\n")
            .Append("Generated from `src/Napkin.Modules.Editing/KeyMaps.cs` — the one table the views, the window's key\n")
            .Append("bindings and the menus read. `KeyMapTests` fails when this file and the table differ; to regenerate it,\n")
            .Append("run that test with `NAPKIN_WRITE_SHORTCUTS=1` set.\n\n")
            .Append("\"Ctrl/Cmd\" is Control on Windows and Linux and Command on macOS. A key typed into a text field is\n")
            .Append("text: no shortcut below except the window's fires while a field has the keyboard.\n");

        Section(text, "Anywhere in the window", Shell);
        Section(text, "Editing (the plan or the 3D view has the keyboard)", Edit);
        Section(text, "Moving about the view", View);
        return text.ToString();
    }

    static void Section<TCommand>(StringBuilder text, string title, KeyMap<TCommand> map)
        where TCommand : struct, Enum
    {
        text.Append("\n## ").Append(title).Append("\n\n| Keys | Does |\n| --- | --- |\n");
        foreach (TCommand command in map.Shortcuts.Select(shortcut => shortcut.Command).Distinct())
        {
            string keys = string.Join(", ", map.KeysFor(command).Select(key => $"`{key}`"));
            text.Append("| ").Append(keys).Append(" | ").Append(map.Words(command)).Append(" |\n");
        }
    }
}

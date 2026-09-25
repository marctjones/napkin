namespace Napkin.Modules.Editing;

/// <summary>
/// A key, named without a UI toolkit: the keys napkin's shortcuts use. The app turns its toolkit's
/// key into one of these (a number-pad digit is the digit, the number pad's plus is plus), so the
/// maps below are the one statement of what every key does.
/// </summary>
public enum KeyName
{
    /// <summary>A.</summary>
    A,

    /// <summary>B.</summary>
    B,

    /// <summary>C.</summary>
    C,

    /// <summary>D.</summary>
    D,

    /// <summary>E.</summary>
    E,

    /// <summary>F.</summary>
    F,

    /// <summary>G.</summary>
    G,

    /// <summary>H.</summary>
    H,

    /// <summary>I.</summary>
    I,

    /// <summary>J.</summary>
    J,

    /// <summary>K.</summary>
    K,

    /// <summary>L.</summary>
    L,

    /// <summary>M.</summary>
    M,

    /// <summary>N.</summary>
    N,

    /// <summary>O.</summary>
    O,

    /// <summary>P.</summary>
    P,

    /// <summary>Q.</summary>
    Q,

    /// <summary>R.</summary>
    R,

    /// <summary>S.</summary>
    S,

    /// <summary>T.</summary>
    T,

    /// <summary>U.</summary>
    U,

    /// <summary>V.</summary>
    V,

    /// <summary>W.</summary>
    W,

    /// <summary>X.</summary>
    X,

    /// <summary>Y.</summary>
    Y,

    /// <summary>Z.</summary>
    Z,

    /// <summary>The digit 0, on the top row or the number pad.</summary>
    D0,

    /// <summary>The digit 1.</summary>
    D1,

    /// <summary>The digit 2.</summary>
    D2,

    /// <summary>The digit 3.</summary>
    D3,

    /// <summary>The digit 4.</summary>
    D4,

    /// <summary>The digit 5.</summary>
    D5,

    /// <summary>The digit 6.</summary>
    D6,

    /// <summary>The digit 7.</summary>
    D7,

    /// <summary>The digit 8.</summary>
    D8,

    /// <summary>The digit 9.</summary>
    D9,

    /// <summary>The left arrow.</summary>
    Left,

    /// <summary>The right arrow.</summary>
    Right,

    /// <summary>The up arrow.</summary>
    Up,

    /// <summary>The down arrow.</summary>
    Down,

    /// <summary>Home.</summary>
    Home,

    /// <summary>Escape.</summary>
    Escape,

    /// <summary>Enter or Return.</summary>
    Enter,

    /// <summary>Tab.</summary>
    Tab,

    /// <summary>Delete (forward delete).</summary>
    Delete,

    /// <summary>Backspace (the Mac's Delete).</summary>
    Backspace,

    /// <summary>Plus, on the top row (with or without Shift) or the number pad.</summary>
    Plus,

    /// <summary>Minus, on the top row or the number pad.</summary>
    Minus,
}

/// <summary>The modifiers a shortcut can hold.</summary>
[Flags]
public enum KeyMods
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>Shift.</summary>
    Shift = 1,

    /// <summary>The platform's command key: Control on Windows and Linux, Command on macOS.</summary>
    Command = 2,

    /// <summary>Alt, or Option on macOS. No shortcut uses it; it is here so a key held with it matches nothing.</summary>
    Alt = 4,
}

/// <summary>One key with its modifiers: what a person presses.</summary>
/// <param name="Key">The key.</param>
/// <param name="Mods">The modifiers held with it.</param>
public readonly record struct Keystroke(KeyName Key, KeyMods Mods = KeyMods.None)
{
    /// <summary>
    /// The keystroke as the shortcut list writes it, the same on every platform: <c>Ctrl/Cmd+Shift+L</c>,
    /// <c>Shift+M</c>, <c>Delete</c>, <c>1</c>.
    /// </summary>
    public override string ToString()
    {
        string key = Key switch
        {
            >= KeyName.D0 and <= KeyName.D9 => ((int)(Key - KeyName.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            KeyName.Plus => "+",
            KeyName.Minus => "-",
            _ => Key.ToString(),
        };

        return (Mods.HasFlag(KeyMods.Command) ? "Ctrl/Cmd+" : string.Empty)
               + (Mods.HasFlag(KeyMods.Alt) ? "Alt+" : string.Empty)
               + (Mods.HasFlag(KeyMods.Shift) ? "Shift+" : string.Empty)
               + key;
    }
}

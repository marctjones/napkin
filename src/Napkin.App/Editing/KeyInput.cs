using Avalonia.Input;
using Napkin.App.Settings;
using Napkin.Modules.Editing;

namespace Napkin.App.Editing;

/// <summary>An editing key a view could not settle on its own, for the window to run.</summary>
/// <param name="command">What the key asks for.</param>
public sealed class EditCommandRequest(EditCommand command) : EventArgs
{
    /// <summary>What the key asks for.</summary>
    public EditCommand Command { get; } = command;

    /// <summary>Whether the window did something with it; if not, the key goes on to the view keys.</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Avalonia's keys to and from <see cref="KeyMaps"/>' neutral ones: the only place a toolkit key
/// meets a shortcut.
/// </summary>
public static class KeyInput
{
    /// <summary>
    /// The keystroke a key press is, or nothing when no shortcut could use that key. Control and
    /// Command are both the command key — Control under the headless platform, which reports no
    /// macOS backend — so there is one path for both. A number-pad digit is the digit, and either
    /// plus or minus key is plus or minus.
    /// </summary>
    public static Keystroke? From(Key key, KeyModifiers modifiers)
    {
        KeyName? name = key switch
        {
            >= Key.A and <= Key.Z => KeyName.A + (key - Key.A),
            >= Key.D0 and <= Key.D9 => KeyName.D0 + (key - Key.D0),
            >= Key.NumPad0 and <= Key.NumPad9 => KeyName.D0 + (key - Key.NumPad0),
            Key.Left => KeyName.Left,
            Key.Right => KeyName.Right,
            Key.Up => KeyName.Up,
            Key.Down => KeyName.Down,
            Key.Home => KeyName.Home,
            Key.Escape => KeyName.Escape,
            Key.Enter => KeyName.Enter,
            Key.Tab => KeyName.Tab,
            Key.Delete => KeyName.Delete,
            Key.Back => KeyName.Backspace,
            Key.OemPlus or Key.Add => KeyName.Plus,
            Key.OemMinus or Key.Subtract => KeyName.Minus,
            _ => null,
        };

        if (name is not { } found)
        {
            return null;
        }

        KeyMods mods = KeyMods.None;
        if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta))
        {
            mods |= KeyMods.Command;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            mods |= KeyMods.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            mods |= KeyMods.Alt;
        }

        return new Keystroke(found, mods);
    }

    /// <summary>The gesture a menu item shows for <paramref name="keystroke"/>, with the platform's command key.</summary>
    public static KeyGesture Gesture(Keystroke keystroke, KeyModifiers command)
    {
        Key key = keystroke.Key switch
        {
            >= KeyName.A and <= KeyName.Z => Key.A + (keystroke.Key - KeyName.A),
            >= KeyName.D0 and <= KeyName.D9 => Key.D0 + (keystroke.Key - KeyName.D0),
            KeyName.Left => Key.Left,
            KeyName.Right => Key.Right,
            KeyName.Up => Key.Up,
            KeyName.Down => Key.Down,
            KeyName.Home => Key.Home,
            KeyName.Escape => Key.Escape,
            KeyName.Enter => Key.Enter,
            KeyName.Tab => Key.Tab,
            KeyName.Delete => Key.Delete,
            KeyName.Backspace => Key.Back,
            KeyName.Plus => Key.OemPlus,
            _ => Key.OemMinus,
        };

        KeyModifiers modifiers = KeyModifiers.None;
        if (keystroke.Mods.HasFlag(KeyMods.Command))
        {
            modifiers |= command;
        }

        if (keystroke.Mods.HasFlag(KeyMods.Shift))
        {
            modifiers |= KeyModifiers.Shift;
        }

        if (keystroke.Mods.HasFlag(KeyMods.Alt))
        {
            modifiers |= KeyModifiers.Alt;
        }

        return new KeyGesture(key, modifiers);
    }

    /// <summary>The view a <c>Show…</c> command asks for, or nothing for any other command.</summary>
    public static DesignView? ViewFor(ViewCommand command) => command switch
    {
        ViewCommand.ShowTop => DesignView.Top,
        ViewCommand.ShowBottom => DesignView.Bottom,
        ViewCommand.ShowFront => DesignView.Front,
        ViewCommand.ShowBack => DesignView.Back,
        ViewCommand.ShowLeft => DesignView.Left,
        ViewCommand.ShowRight => DesignView.Right,
        ViewCommand.ShowModel => DesignView.Model,
        _ => null,
    };

    /// <summary>The command that shows <paramref name="view"/>.</summary>
    public static ViewCommand CommandFor(DesignView view) => view switch
    {
        DesignView.Top => ViewCommand.ShowTop,
        DesignView.Bottom => ViewCommand.ShowBottom,
        DesignView.Front => ViewCommand.ShowFront,
        DesignView.Back => ViewCommand.ShowBack,
        DesignView.Left => ViewCommand.ShowLeft,
        DesignView.Right => ViewCommand.ShowRight,
        _ => ViewCommand.ShowModel,
    };
}

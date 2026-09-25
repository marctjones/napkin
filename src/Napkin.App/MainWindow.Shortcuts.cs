using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Napkin.App.Designs;
using Design = Napkin.Modules.Editing.Design;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Modules.Building;
using Napkin.Modules.Furniture;
using Napkin.Modules.Editing;

namespace Napkin.App;

public partial class MainWindow
{
    void BuildSamplesMenu()
    {
        for (int i = 0; i < Samples.Count; i++)
        {
            IDesignSource source = Samples[i];
            MenuItem item = new()
            {
                Header = source.Name,
                Tag = source,
            };

            // The first nine samples get a command-digit shortcut; past that the menu is the way
            // in, because there is no tenth digit to give.
            if (i < 9)
            {
                item.InputGesture = Gesture(ShellCommand.Sample1 + i);
            }

            item.Click += (_, _) => OpenSampleCommand(source);
            _sampleItems.Add(item);
        }

        SamplesMenu.ItemsSource = _sampleItems;
        ShowGestures();
        UpdateMenuEnablement();
    }

    /// <summary>
    /// Puts every menu item's shortcut on it from <see cref="KeyMaps"/>, the one table the keys are
    /// handled from. Display only, as every gesture here is: the window's shortcuts are key
    /// bindings, and the drawing's keys are handled in the views, where they yield to typing.
    /// </summary>
    void ShowGestures()
    {
        NewMenuItem.InputGesture = Gesture(ShellCommand.New);
        OpenMenuItem.InputGesture = Gesture(ShellCommand.Open);
        SaveMenuItem.InputGesture = Gesture(ShellCommand.Save);
        SaveAsMenuItem.InputGesture = Gesture(ShellCommand.SaveAs);
        UndoMenuItem.InputGesture = UndoGestures[0];
        RedoMenuItem.InputGesture = RedoGestures[0];
        CutListMenuItem.InputGesture = Gesture(ShellCommand.CutList);
        ShoppingListMenuItem.InputGesture = Gesture(ShellCommand.ShoppingList);

        foreach ((MenuItem item, EditCommand command) in (ReadOnlySpan<(MenuItem, EditCommand)>)
        [
            (SelectToolMenuItem, EditCommand.SelectTool),
            (RectangleToolMenuItem, EditCommand.RectangleTool),
            (WallToolMenuItem, EditCommand.WallTool),
            (ShapeMenuItem, EditCommand.Shape),
            (DuplicateMenuItem, EditCommand.Duplicate),
            (JoinMenuItem, EditCommand.Join),
            (JoinAllMenuItem, EditCommand.JoinAll),
            (MirrorEastWestMenuItem, EditCommand.MirrorEastWest),
            (MirrorNorthSouthMenuItem, EditCommand.MirrorNorthSouth),
            (PinMenuItem, EditCommand.Pin),
            (DeleteMenuItem, EditCommand.Delete),
            (TurnXMenuItem, EditCommand.TurnX),
            (TurnYMenuItem, EditCommand.TurnY),
            (TurnZMenuItem, EditCommand.TurnZ),
            (GridMenuItem, EditCommand.ToggleGrid),
        ])
        {
            item.InputGesture = Gesture(command);
        }

        ZoomToFitMenuItem.InputGesture = Gesture(ViewCommand.ZoomToFit);
        ResetViewMenuItem.InputGesture = Gesture(ViewCommand.ResetView);
        ZoomInMenuItem.InputGesture = Gesture(ViewCommand.ZoomIn);
        ZoomOutMenuItem.InputGesture = Gesture(ViewCommand.ZoomOut);

        // The views on 1-7 (standard-views §4.2); they yield to a length being typed.
        foreach (DesignView view in AllViews)
        {
            ViewMenuEntry(view).InputGesture = Gesture(KeyInput.CommandFor(view));
        }
    }

    /// <summary>The gesture a menu shows for a command: its first key in the map, with the platform's command key.</summary>
    static KeyGesture Gesture(ShellCommand command) => KeyInput.Gesture(KeyMaps.Shell.KeysFor(command)[0], CommandModifier);

    static KeyGesture Gesture(EditCommand command) => KeyInput.Gesture(KeyMaps.Edit.KeysFor(command)[0], CommandModifier);

    static KeyGesture Gesture(ViewCommand command) => KeyInput.Gesture(KeyMaps.View.KeysFor(command)[0], CommandModifier);

    /// <summary>
    /// The window's own shortcuts (<see cref="KeyMaps.Shell"/>), bound wherever the keyboard is.
    /// Undo and redo are bound to the platform's own keys, which the map lists the usual ones of.
    /// </summary>
    void BuildKeyBindings()
    {
        foreach (Shortcut<ShellCommand> shortcut in KeyMaps.Shell.Shortcuts)
        {
            if (shortcut.Command is ShellCommand.Undo or ShellCommand.Redo
                || (shortcut.Command >= ShellCommand.Sample1 && shortcut.Command - ShellCommand.Sample1 >= Samples.Count))
            {
                continue;
            }

            ShellCommand command = shortcut.Command;
            KeyBindings.Add(new KeyBinding
            {
                Gesture = KeyInput.Gesture(shortcut.Keystroke, CommandModifier),
                Command = new RelayCommand(() => Run(command)),
            });
        }

        foreach (KeyGesture gesture in UndoGestures)
        {
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = new RelayCommand(() => UndoCommand()) });
        }

        foreach (KeyGesture gesture in RedoGestures)
        {
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = new RelayCommand(() => RedoCommand()) });
        }
    }

    /// <summary>Runs a window shortcut.</summary>
    void Run(ShellCommand command)
    {
        switch (command)
        {
            case ShellCommand.New:
                NewSheetCommand();
                break;

            case ShellCommand.Open:
                _ = OpenFileAsync();
                break;

            case ShellCommand.Save:
                _ = SaveAsync();
                break;

            case ShellCommand.SaveAs:
                _ = SaveAsAsync();
                break;

            case ShellCommand.Undo:
                UndoCommand();
                break;

            case ShellCommand.Redo:
                RedoCommand();
                break;

            case ShellCommand.CutList:
                OpenCutList();
                break;

            case ShellCommand.ShoppingList:
                OpenShoppingList();
                break;

            default:
                OpenSampleCommand(Samples[command - ShellCommand.Sample1]);
                break;
        }
    }

    /// <summary>
    /// The platform's own undo keys — Control+Z on Windows, Command+Z on macOS — read from the
    /// platform the way <see cref="CommandModifier"/> is, rather than hardcoded.
    /// </summary>
    static IReadOnlyList<KeyGesture> UndoGestures =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.Undo is { Count: > 0 } undo
            ? [.. undo]
            : [new KeyGesture(Key.Z, CommandModifier)];

    /// <summary>
    /// The platform's own redo keys — Avalonia lists command+Y and command+Shift+Z — every one of
    /// them bound. The menu shows the first, so the list is ordered by convention: Control+Y
    /// first on Windows, Command+Shift+Z first on macOS, where Command+Y is not redo.
    /// </summary>
    static IReadOnlyList<KeyGesture> RedoGestures
    {
        get
        {
            KeyModifiers command = CommandModifier;
            List<KeyGesture> redo = Application.Current?.PlatformSettings?.HotkeyConfiguration.Redo is { Count: > 0 } listed
                ? [.. listed]
                : [new KeyGesture(Key.Y, command), new KeyGesture(Key.Z, command | KeyModifiers.Shift)];

            bool mac = command.HasFlag(KeyModifiers.Meta);
            return
            [
                .. redo.OrderBy(gesture =>
                    gesture.Key == Key.Z && gesture.KeyModifiers.HasFlag(KeyModifiers.Shift) ? (mac ? 0 : 1) : (mac ? 1 : 0)),
            ];
        }
    }

    /// <summary>
    /// Control on Windows, Command on macOS, read from the platform rather than hardcoded.
    /// </summary>
    static KeyModifiers CommandModifier =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers
        ?? KeyModifiers.Control;

    void OnShellKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        // While the save question is up, Escape answers it with Cancel, and no other key reaches
        // the drawing underneath.
        if (IsAskingToSave)
        {
            if (e.Key == Key.Escape)
            {
                KeepEditing();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Escape && IsShapingPart && !IsRefusalShowing)
        {
            CloseWorkshop();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && IsRefusalShowing)
        {
            DismissRefusal();
            e.Handled = true;
            return;
        }

        // One rule for typing: while a field has the keyboard, a key is text, never a view command —
        // a digit is part of a length (standard-views §4.2), and "-" and "+" in 1'-4 1/4" must not
        // zoom. Whether Avalonia's own TextBox already stops every one of those keys is not
        // something to rely on, and the headless platform routes them differently from a real
        // backend. The shape workshop is its own surface with its own keys.
        if (IsShapingPart || KeyboardIsOnAField()
            || KeyInput.From(e.Key, e.KeyModifiers) is not { } key
            || KeyMaps.View.Find(key) is not { } command)
        {
            return;
        }

        if (IsShowingModel
                ? !ModelDrawing.IsFocused && ModelDrawing.Apply(command)
                : !DrawingCanvas.IsFocused && DrawingCanvas.Apply(command))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Whether the keyboard is on something a person types or picks in — a text field anywhere, or
    /// any control of the properties panel — rather than on the drawing or on nothing in particular.
    /// </summary>
    bool KeyboardIsOnAField() =>
        FocusManager?.GetFocusedElement() is TextBox || PropertiesPanel.IsKeyboardFocusWithin;
}

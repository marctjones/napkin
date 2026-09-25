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
        KeyModifiers command = CommandModifier;
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
                item.InputGesture = new KeyGesture(Key.D1 + i, command);
            }

            item.Click += (_, _) => OpenSampleCommand(source);
            _sampleItems.Add(item);
        }

        SamplesMenu.ItemsSource = _sampleItems;
        NewMenuItem.InputGesture = new KeyGesture(Key.N, command);
        OpenMenuItem.InputGesture = new KeyGesture(Key.O, command);
        SaveMenuItem.InputGesture = new KeyGesture(Key.S, command);
        SaveAsMenuItem.InputGesture = new KeyGesture(Key.S, command | KeyModifiers.Shift);
        UndoMenuItem.InputGesture = UndoGestures[0];
        RedoMenuItem.InputGesture = RedoGestures[0];
        ZoomToFitMenuItem.InputGesture = new KeyGesture(Key.D0, command);
        ZoomInMenuItem.InputGesture = new KeyGesture(Key.OemPlus);
        ZoomOutMenuItem.InputGesture = new KeyGesture(Key.OemMinus);
        SelectToolMenuItem.InputGesture = new KeyGesture(Key.S);
        RectangleToolMenuItem.InputGesture = new KeyGesture(Key.R);
        WallToolMenuItem.InputGesture = new KeyGesture(Key.W);
        ShapeMenuItem.InputGesture = new KeyGesture(Key.C);
        DuplicateMenuItem.InputGesture = new KeyGesture(Key.D);
        JoinMenuItem.InputGesture = new KeyGesture(Key.J);
        JoinAllMenuItem.InputGesture = new KeyGesture(Key.J, KeyModifiers.Shift);
        MirrorEastWestMenuItem.InputGesture = new KeyGesture(Key.M);
        MirrorNorthSouthMenuItem.InputGesture = new KeyGesture(Key.M, KeyModifiers.Shift);
        PinMenuItem.InputGesture = new KeyGesture(Key.P);
        DeleteMenuItem.InputGesture = new KeyGesture(Key.Delete);
        TurnXMenuItem.InputGesture = new KeyGesture(Key.X);
        TurnYMenuItem.InputGesture = new KeyGesture(Key.Y);
        TurnZMenuItem.InputGesture = new KeyGesture(Key.Z);
        GridMenuItem.InputGesture = new KeyGesture(Key.G);

        // The views on 1-7 (standard-views §4.2). Display only, as every gesture here is: the keys are
        // handled in the drawing's own key path, where they yield to a length being typed.
        foreach (DesignView view in AllViews)
        {
            ViewMenuEntry(view).InputGesture = new KeyGesture(Key.D0 + (int)view);
        }

        UpdateMenuEnablement();
    }

    void BuildKeyBindings()
    {
        KeyModifiers command = CommandModifier;
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.N, command),
            Command = new RelayCommand(NewSheetCommand),
        });
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.O, command),
            Command = new RelayCommand(() => _ = OpenFileAsync()),
        });
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.L, command),
            Command = new RelayCommand(() => OpenCutList()),
        });
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.L, command | KeyModifiers.Shift),
            Command = new RelayCommand(() => OpenShoppingList()),
        });
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.S, command),
            Command = new RelayCommand(() => _ = SaveAsync()),
        });
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.S, command | KeyModifiers.Shift),
            Command = new RelayCommand(() => _ = SaveAsAsync()),
        });

        foreach (KeyGesture gesture in UndoGestures)
        {
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = new RelayCommand(() => UndoCommand()) });
        }

        foreach (KeyGesture gesture in RedoGestures)
        {
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = new RelayCommand(() => RedoCommand()) });
        }

        for (int i = 0; i < Samples.Count && i < 9; i++)
        {
            IDesignSource source = Samples[i];
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.D1 + i, command),
                Command = new RelayCommand(() => OpenSampleCommand(source)),
            });
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

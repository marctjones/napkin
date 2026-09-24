using System.Reflection;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace Napkin.Demo;

/// <summary>
/// Avalonia's raw-input API, bound at run time.
/// </summary>
/// <remarks>
/// <para>
/// The raw input events, the platform's input callback and a window's input root are public in
/// Avalonia 12's implementation assemblies — they are how every platform backend, and
/// <c>Avalonia.Headless</c>, deliver input — but marked <c>[PrivateApi]</c>, so the reference
/// assemblies a project compiles against leave their constructors and accessors out. The live
/// host needs exactly those members to feed a real window the way its OS backend does, so it binds
/// them by reflection here, in one place, and fails loudly at start-up if a later Avalonia renames
/// one. Nothing else in napkin does this.
/// </para>
/// </remarks>
static class RawInput
{
    const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    static readonly ConstructorInfo PointerEvent = Constructor(typeof(RawPointerEventArgs),
        typeof(IInputDevice), typeof(ulong), typeof(IInputRoot), typeof(RawPointerEventType), typeof(Point), typeof(RawInputModifiers));

    static readonly ConstructorInfo WheelEvent = Constructor(typeof(RawMouseWheelEventArgs),
        typeof(IInputDevice), typeof(ulong), typeof(IInputRoot), typeof(Point), typeof(Vector), typeof(RawInputModifiers));

    static readonly ConstructorInfo KeyEvent = Constructor(typeof(RawKeyEventArgs),
        typeof(IInputDevice), typeof(ulong), typeof(IInputRoot), typeof(RawKeyEventType), typeof(Key), typeof(RawInputModifiers),
        typeof(PhysicalKey), typeof(string), typeof(KeyDeviceType));

    static readonly ConstructorInfo TextEvent = Constructor(typeof(RawTextInputEventArgs),
        typeof(IKeyboardDevice), typeof(ulong), typeof(IInputRoot), typeof(string));

    /// <summary>The callback a window's platform implementation raises its input through.</summary>
    public static Action<RawInputEventArgs> InputOf(TopLevel topLevel)
    {
        ITopLevelImpl impl = topLevel.PlatformImpl
            ?? throw new InvalidOperationException("The window has no platform implementation.");
        PropertyInfo input = typeof(ITopLevelImpl).GetProperty("Input", Instance)
            ?? throw Missing("ITopLevelImpl.Input");
        return raw => ((Action<RawInputEventArgs>?)input.GetValue(impl)
            ?? throw new InvalidOperationException("The window is not listening for input."))(raw);
    }

    /// <summary>The input root a window's events are raised against: its presentation source's.</summary>
    public static IInputRoot RootOf(TopLevel topLevel)
    {
        IPresentationSource source = topLevel.GetPresentationSource()
            ?? throw new InvalidOperationException("The window is not shown: it has no presentation source.");
        PropertyInfo root = typeof(IPresentationSource).GetProperty("InputRoot", Instance)
            ?? throw Missing("IPresentationSource.InputRoot");
        return (IInputRoot?)root.GetValue(source)
            ?? throw new InvalidOperationException("The window has no input root.");
    }

    /// <summary>A mouse of its own, so the person's real mouse and the scenario's never share a capture.</summary>
    public static IMouseDevice NewMouse() =>
        (IMouseDevice)Constructor(typeof(MouseDevice), typeof(Avalonia.Input.Pointer))
            .Invoke([new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true)]);

    /// <summary>The platform's keyboard device, which keyboard focus is tracked on.</summary>
    public static IKeyboardDevice Keyboard()
    {
        PropertyInfo instance = typeof(KeyboardDevice).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw Missing("KeyboardDevice.Instance");
        return (IKeyboardDevice?)instance.GetValue(null)
            ?? throw new InvalidOperationException("The platform has no keyboard device.");
    }

    public static RawInputEventArgs Pointer(IInputDevice device, IInputRoot root, RawPointerEventType type, Point at, RawInputModifiers modifiers) =>
        (RawInputEventArgs)PointerEvent.Invoke([device, Timestamp, root, type, at, modifiers]);

    public static RawInputEventArgs Wheel(IInputDevice device, IInputRoot root, Point at, Vector delta, RawInputModifiers modifiers) =>
        (RawInputEventArgs)WheelEvent.Invoke([device, Timestamp, root, at, delta, modifiers]);

    public static RawInputEventArgs Key(IKeyboardDevice device, IInputRoot root, RawKeyEventType type, Key key, RawInputModifiers modifiers) =>
        (RawInputEventArgs)KeyEvent.Invoke([device, Timestamp, root, type, key, modifiers, PhysicalKey.None, null, KeyDeviceType.Keyboard]);

    public static RawInputEventArgs Text(IKeyboardDevice device, IInputRoot root, string text) =>
        (RawInputEventArgs)TextEvent.Invoke([device, Timestamp, root, text]);

    static ulong Timestamp => (ulong)Environment.TickCount64;

    static ConstructorInfo Constructor(Type type, params Type[] parameters) =>
        type.GetConstructor(Instance, parameters) ?? throw Missing($"{type.Name}({string.Join(", ", parameters.Select(p => p.Name))})");

    static MissingMemberException Missing(string what) =>
        new($"The live host needs Avalonia's {what}, which this Avalonia does not have (docs/testing/live-scenarios.md).");
}

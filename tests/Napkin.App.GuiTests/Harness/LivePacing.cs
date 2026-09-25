using System.Globalization;
using Avalonia;
using Avalonia.Input;

namespace Napkin.App.GuiTests.Harness;

/// <summary>
/// The pure arithmetic behind the live host's pacing (#151): where the visible pointer is on each
/// frame of a glide, when each typed character lands, and what the caption bar and the exit code
/// say. Kept here, away from any window, so it is unit tested headless.
/// </summary>
public static class LivePacing
{
    /// <summary>The default pointer speed, in device-independent pixels per second.</summary>
    public const double DefaultPixelsPerSecond = 750;

    /// <summary>About one frame at 60 Hz: one injected move per frame while gliding.</summary>
    public static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    /// <summary>The delay between typed characters at speed 1.</summary>
    public static readonly TimeSpan PerCharacter = TimeSpan.FromMilliseconds(90);

    /// <summary>
    /// Ease-in-out (cubic): starts and ends slowly, like a hand. Maps 0 to 0 and 1 to 1, and is
    /// monotonic between; inputs outside [0, 1] are clamped.
    /// </summary>
    public static double EaseInOut(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t < 0.5 ? 4 * t * t * t : 1 - (Math.Pow(-2 * t + 2, 3) / 2);
    }

    /// <summary>
    /// The points a glide from <paramref name="from"/> to <paramref name="to"/> passes through, one
    /// per frame, excluding the start and ending exactly on the destination.
    /// </summary>
    /// <param name="from">Where the pointer is now.</param>
    /// <param name="to">Where it is going.</param>
    /// <param name="pixelsPerSecond">How fast it travels; must be positive.</param>
    /// <param name="frame">How long one step takes; must be positive.</param>
    /// <returns>At least one point; the last is <paramref name="to"/>.</returns>
    public static IReadOnlyList<Point> Glide(Point from, Point to, double pixelsPerSecond, TimeSpan frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerSecond);
        if (frame <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), "A frame must take some time.");
        }

        Vector travel = to - from;
        double seconds = travel.Length / pixelsPerSecond;
        int steps = Math.Max(1, (int)Math.Ceiling(seconds / frame.TotalSeconds));

        var points = new Point[steps];
        for (int i = 1; i < steps; i++)
        {
            points[i - 1] = from + (travel * EaseInOut((double)i / steps));
        }

        points[steps - 1] = to;
        return points;
    }

    /// <summary>
    /// Splits text into what is typed at each keystroke — one text element each, so a surrogate
    /// pair or a combining sequence goes in whole — with the pause before it. A pause after a space
    /// is a little longer, the way a person types.
    /// </summary>
    public static IReadOnlyList<(string Text, TimeSpan Before)> TypingSchedule(string text, TimeSpan perCharacter)
    {
        ArgumentNullException.ThrowIfNull(text);
        var schedule = new List<(string, TimeSpan)>();
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
        string? previous = null;
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            TimeSpan before = previous is null ? TimeSpan.Zero
                : previous == " " ? perCharacter * 1.5
                : perCharacter;
            schedule.Add((element, before));
            previous = element;
        }

        return schedule;
    }

    /// <summary>
    /// The runner's exit code: 0 when every expectation passed, 1 when the scenario failed (an
    /// expectation, or the scenario itself threw), 2 when the host could not run it at all.
    /// </summary>
    public static int ExitCode(int failures, bool infrastructureError) =>
        infrastructureError ? 2 : failures > 0 ? 1 : 0;

    /// <summary>The caption line for an expectation: a tick or a cross, and on failure why.</summary>
    public static string ExpectCaption(string what, Exception? failure)
    {
        if (failure is null)
        {
            return "✓ " + what;
        }

        string reason = (failure.Message ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? failure.GetType().Name;
        return $"✗ {what}: {reason}";
    }

    /// <summary>
    /// A key as a person reads it on a keycap: modifiers first, in the platform's own names
    /// (Cmd on macOS, Ctrl elsewhere), then the key.
    /// </summary>
    public static string KeyCaption(Key key, KeyModifiers modifiers, bool macOS)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add(macOS ? "Option" : "Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add(macOS ? "Cmd" : "Win");
        }

        parts.Add(key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture),
            _ => key.ToString(),
        });
        return string.Join('+', parts);
    }
}

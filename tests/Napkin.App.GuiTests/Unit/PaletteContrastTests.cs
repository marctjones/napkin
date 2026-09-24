using Avalonia.Media;
using Napkin.App.Viewing;

using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>Text on the drawing has to be readable in both themes: WCAG contrast of at least 4.5:1 (#112).</summary>
public class PaletteContrastTests
{
    public static TheoryData<string, CanvasPalette> Palettes => new()
    {
        { "light", CanvasPalette.Light },
        { "dark", CanvasPalette.Dark },
    };

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Part_names_are_readable_on_the_paper(string theme, CanvasPalette palette) =>
        Assert.True(
            Contrast(palette.Label, palette.Background) >= 4.5,
            $"{theme}: label {palette.Label} on {palette.Background} is {Contrast(palette.Label, palette.Background):0.00}:1.");

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Dimension_text_is_readable_on_the_paper(string theme, CanvasPalette palette) =>
        Assert.True(
            Contrast(palette.Dimension, palette.Background) >= 4.5,
            $"{theme}: dimension {palette.Dimension} on {palette.Background} is {Contrast(palette.Dimension, palette.Background):0.00}:1.");

    [Fact]
    public void The_ratio_matches_the_published_extremes()
    {
        Assert.Equal(21.0, Contrast(Colors.Black, Colors.White), 3);
        Assert.Equal(1.0, Contrast(Colors.White, Colors.White), 3);
    }

    /// <summary>The WCAG 2 contrast ratio of two opaque colours.</summary>
    static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    static double Luminance(Color c)
    {
        static double Linear(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
    }
}

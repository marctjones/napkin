using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The bundled IBM Plex families resolve from <c>App.axaml</c>'s <c>avares://</c> links. Those links
/// name the assembly, which has been <c>napkin</c> since #56; a link naming the wrong assembly does
/// not throw, the text just falls back to another font, so this is what notices.
/// </summary>
public class BundledFontTests
{
    [AvaloniaTheory]
    [InlineData("SeFontSans", "IBM Plex Sans")]
    [InlineData("SeFontSerif", "IBM Plex Serif")]
    [InlineData("SeFontMono", "IBM Plex Mono")]
    public void Each_bundled_family_resolves_to_its_own_font(string key, string family)
    {
        Assert.True(Application.Current!.TryGetResource(key, null, out object? resource), $"No {key} resource.");
        FontFamily font = Assert.IsType<FontFamily>(resource);

        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(font), out var glyphs), $"{key} does not resolve.");
        Assert.Equal(family, glyphs!.FamilyName);
    }
}

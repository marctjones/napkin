using Excise.Core.Graphics;

namespace Napkin.Interop.Pdf;

/// <summary>
/// The sheet's typeface: IBM Plex Sans, Regular and Medium — the app's own fonts (OFL-1.1,
/// docs/third-party-notices.md), embedded in the assembly and embedded again, subset, in every PDF,
/// so the page shows and extracts any character a label or a citation holds. One font program per
/// weight per document: every size is <see cref="PdfFont.WithSize"/> of it, so each weight embeds once.
/// </summary>
internal sealed class SheetFonts
{
    readonly PdfFont _regular, _medium;
    readonly Dictionary<(bool Medium, double Size), PdfFont> _sized = [];

    SheetFonts(PdfFont regular, PdfFont medium)
    {
        _regular = regular;
        _medium = medium;
    }

    /// <summary>A fresh pair, for one document: a font program collects the glyphs its document uses.</summary>
    public static SheetFonts Load() => new(Read("IBMPlexSans-Regular.ttf"), Read("IBMPlexSans-Medium.ttf"));

    /// <summary>The regular weight at a size.</summary>
    public PdfFont Regular(double size) => Sized(false, size);

    /// <summary>The medium weight at a size.</summary>
    public PdfFont Medium(double size) => Sized(true, size);

    PdfFont Sized(bool medium, double size)
    {
        if (!_sized.TryGetValue((medium, size), out PdfFont? font))
        {
            font = (medium ? _medium : _regular).WithSize(size);
            _sized[(medium, size)] = font;
        }

        return font;
    }

    static PdfFont Read(string file)
    {
        using Stream stream = typeof(SheetFonts).Assembly.GetManifestResourceStream("Napkin.Interop.Pdf.Fonts." + file)
            ?? throw new InvalidOperationException($"The sheet's font {file} is not embedded in the assembly.");
        return PdfFont.FromTrueType(stream, 10);
    }
}

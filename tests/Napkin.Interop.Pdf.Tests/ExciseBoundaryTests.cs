using System.Text;

using Excise.Core.Document;
using Excise.Core.Graphics;
using Excise.Core.Text;

namespace Napkin.Interop.Pdf.Tests;

/// <summary>
/// napkin writes PDFs with Excise.Core and never decodes a JPEG 2000 image, so CSJ2K — the codec
/// Excise.Core reaches only from its JPX decoder — is pruned from napkin's restore graph
/// (Directory.Build.props; docs/third-party-notices.md says why). These hold that it stays out:
/// not restored beside the build, and never loaded by writing a page or reading one back.
/// </summary>
public class ExciseBoundaryTests
{
    [Fact]
    public void CSJ2K_is_not_beside_the_build()
    {
        string here = AppContext.BaseDirectory;
        Assert.True(File.Exists(Path.Combine(here, "Excise.Core.dll")));
        Assert.Empty(Directory.EnumerateFiles(here, "CSJ2K*.dll"));
    }

    [Fact]
    public void Writing_a_page_and_reading_it_back_never_loads_CSJ2K()
    {
        PdfDocument document = PdfDocument.CreateNew();
        PdfPage page = document.Pages.AddBlank(792, 612);
        using (PdfGraphics graphics = page.GetGraphics())
        {
            graphics.DrawLine(72, 72, 144, 72, new PdfPen(PdfColor.Black, 1.4));
            graphics.DrawString("napkin", PdfFont.Helvetica(12), PdfBrush.Black, 72, 90);
        }

        byte[] bytes = document.SaveToBytes();
        PdfDocument read = PdfDocument.Open(bytes);
        string operators = Encoding.Latin1.GetString(read.Pages[0].GetContentStreamBytes());
        string text = new TextExtractor(read.Pages[0]).ExtractText();

        Assert.Contains("72 72 m", operators, StringComparison.Ordinal);
        Assert.Contains("napkin", text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            AppDomain.CurrentDomain.GetAssemblies(),
            assembly => assembly.GetName().Name?.StartsWith("CSJ2K", StringComparison.OrdinalIgnoreCase) == true);
    }
}

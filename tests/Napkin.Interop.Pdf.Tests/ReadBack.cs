using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Excise.Core.Document;
using Excise.Core.Text;

namespace Napkin.Interop.Pdf.Tests;

/// <summary>
/// A written sheet read back the way a PDF reader reads it: each page's content stream as its
/// operators, and each page's text as a reader extracts it, whitespace collapsed to single spaces.
/// </summary>
internal sealed class ReadBack
{
    ReadBack(PdfDocument document)
    {
        Document = document;
        Operators = [.. Enumerable.Range(0, document.PageCount).Select(i => Encoding.Latin1.GetString(document.Pages[i].GetContentStreamBytes()))];
        Text = [.. Enumerable.Range(0, document.PageCount).Select(i => Regex.Replace(new TextExtractor(document.Pages[i]).ExtractText(), @"\s+", " "))];
    }

    public PdfDocument Document { get; }

    /// <summary>Each page's text with every space taken out: what survives a reader running wrapped lines together.</summary>
    public IReadOnlyList<string> Letters => [.. Text.Select(Squash)];

    /// <summary>Text with its whitespace taken out.</summary>
    public static string Squash(string text) => Regex.Replace(text, @"\s+", string.Empty);

    /// <summary>Each page's content stream.</summary>
    public IReadOnlyList<string> Operators { get; }

    /// <summary>Each page's text.</summary>
    public IReadOnlyList<string> Text { get; }

    public static ReadBack Of(PlanSheet sheet)
    {
        using MemoryStream stream = new();
        SheetPdf.Write(sheet, stream);
        return new ReadBack(PdfDocument.Open(stream.ToArray()));
    }

    /// <summary>A number as a PDF content stream prints it: at most six decimals, trailing zeros trimmed.</summary>
    public static string N(double value)
    {
        string text = value.ToString("0.######", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    /// <summary>How a straight stroke from one point to another reads in the stream.</summary>
    public static string Stroke(double x1, double y1, double x2, double y2) => $"{N(x1)} {N(y1)} m\n{N(x2)} {N(y2)} l\nS";

    /// <summary>How many times a piece of text occurs.</summary>
    public static int Count(string haystack, string needle)
    {
        int count = 0;
        for (int at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}

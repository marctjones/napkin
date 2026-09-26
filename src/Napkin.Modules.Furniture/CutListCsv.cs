using System.Collections.Immutable;
using System.Globalization;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// The cut list as a file a spreadsheet opens: exactly the rows on screen, in the same order, with
/// the same numbers (<c>docs/design/parts-and-cut-list.md</c> §3.1).
/// </summary>
/// <remarks>
/// <para>
/// A header line says what the list is before — saw kerf and joinery allowance — then the column
/// names, then one line per row. Lines end in <c>\n</c> on every platform, for the same reason the
/// scene writer's do.
/// </para>
/// <para>
/// <strong>Lengths are always quoted</strong>, because <c>4'-0"</c> carries a quote of its own, and
/// a length that is not exact at 1/16&#x2033; carries the same &#x2248; marker the canvas uses, so
/// the file never claims more precision than the screen does.
/// </para>
/// <para>
/// <strong>A <c>Cuts</c> column</strong> holds what to do to the blank, one sentence per
/// cut joined by <see cref="BetweenCuts"/>, and is empty for a plain rectangle
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;4.5). The header line's statement — finished
/// sizes, joinery allowances included, before saw kerf — is as true of a shaped part as of a
/// rectangle: the sizes are the blank's.
/// </para>
/// <para>
/// <strong>A trailing <c>Joinery</c> column</strong> holds what to do because of the part's joints,
/// sentences joined the same way (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;6.4), followed by
/// "joint not satisfied" when a joint on the row no longer holds; empty for a part nothing is done to.
/// </para>
/// </remarks>
public static class CutListCsv
{
    /// <summary>The column names, in the order they are written.</summary>
    public const string Header = "Label,Quantity,Length,Width,Thickness,Material,Rough,Cuts,Joinery";

    /// <summary>What the first line gains when any row is rough (<c>docs/design/sketch-mode.md</c> &#xA7;5).</summary>
    public const string RoughClause = ", rough rows are as drawn";

    /// <summary>What separates one cut's sentence from the next in the <c>Cuts</c> column.</summary>
    public const string BetweenCuts = "; ";

    /// <summary>The marker on a length whose text is not the stored value (geometry model §1.4).</summary>
    public const string Approximately = "≈";

    /// <summary>The cut list as CSV text.</summary>
    /// <param name="rows">The rows, in the order they are on screen. Written in that order.</param>
    public static string ToCsv(IEnumerable<CutListRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        List<CutListRow> all = [.. rows];
        string statement = all.Any(row => row.Rough)
            ? CutList.BeforeKerfAndJoinery[..^1] + RoughClause + "."
            : CutList.BeforeKerfAndJoinery;

        StringBuilder csv = new();
        csv.Append(Field(statement)).Append('\n');
        csv.Append(Header).Append('\n');

        foreach (CutListRow row in all)
        {
            csv.Append(Field(row.Label)).Append(',')
               .Append(row.Quantity.ToString(CultureInfo.InvariantCulture)).Append(',')
               .Append(Quoted(row.LengthText)).Append(',')
               .Append(Quoted(row.WidthText)).Append(',')
               .Append(Quoted(row.ThicknessText)).Append(',')
               .Append(Field(row.MaterialText)).Append(',')
               .Append(row.Rough ? "yes" : string.Empty).Append(',')
               .Append(Field(string.Join(BetweenCuts, row.CutText))).Append(',')
               .Append(Field(string.Join(BetweenCuts, row.JointText.AddRange(row.Flags))))
               .Append('\n');
        }

        return csv.ToString();
    }

    /// <summary>
    /// How a length reads in the file: feet, inches and sixteenths, marked when that text is not
    /// the whole truth.
    /// </summary>
    /// <param name="length">The length.</param>
    public static string Text(Length length)
    {
        FormattedLength formatted = length.Format(LengthFormat.Default);
        return formatted.IsExact ? formatted.Text : Approximately + formatted.Text;
    }

    /// <summary>
    /// Reads back what <see cref="ToCsv"/> wrote, as the text of each field.
    /// </summary>
    /// <remarks>
    /// Here so that a test can compare an export against the rows it came from without a CSV
    /// library, which is what "the export is exactly the table" needs to be checkable. It parses
    /// RFC 4180 quoting — a quoted field may hold commas, newlines and doubled quotes — and
    /// nothing else.
    /// </remarks>
    /// <param name="csv">The text <see cref="ToCsv"/> produced.</param>
    public static ImmutableArray<ImmutableArray<string>> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        List<ImmutableArray<string>> lines = [];
        List<string> fields = [];
        StringBuilder field = new();
        bool quoted = false;
        bool anything = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (quoted)
            {
                if (c != '"')
                {
                    field.Append(c);
                }
                else if (i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    anything = true;
                    break;

                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    anything = true;
                    break;

                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    lines.Add([.. fields]);
                    fields.Clear();
                    anything = false;
                    break;

                default:
                    field.Append(c);
                    anything = true;
                    break;
            }
        }

        if (anything || field.Length > 0)
        {
            fields.Add(field.ToString());
            lines.Add([.. fields]);
        }

        return [.. lines];
    }

    /// <summary>A field, quoted when its text would otherwise change the shape of the line.</summary>
    public static string Field(string text)
        => text.AsSpan().IndexOfAny(',', '"', '\n') >= 0 || text.Contains('\r', StringComparison.Ordinal)
            ? Quoted(text)
            : text;

    private static string Quoted(string text)
        => $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

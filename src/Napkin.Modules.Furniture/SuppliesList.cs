using System.Collections.Immutable;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>The three lists below the boards and sheets on the shopping list (&#xA7;7, &#xA7;8), in the order they are shown.</summary>
public enum ExtraSection
{
    /// <summary>Fasteners, derived from the joints.</summary>
    Fasteners,

    /// <summary>Hardware typed onto parts.</summary>
    Hardware,

    /// <summary>The typed supplies checklist and the derived glue line.</summary>
    Supplies,
}

/// <summary>One line below the boards and sheets: what to buy beyond wood, as text the table shows and the file carries.</summary>
/// <param name="Section">Which list.</param>
/// <param name="Item">What it is: a fastener kind and stock, a hardware name or a supply.</param>
/// <param name="Size">The builder's typed size, "size not chosen" for a fastener with none, else empty.</param>
/// <param name="Count">How many, or null for a line that has no count.</param>
/// <param name="PackSize">Fasteners in a pack, or null.</param>
/// <param name="Packs">Packs to buy, or null.</param>
/// <param name="For">Which joints or parts, or a supply's typed note.</param>
public sealed record ExtraRow(ExtraSection Section, string Item, string Size, int? Count, int? PackSize, int? Packs, string For)
{
    /// <summary>The section's name as shown.</summary>
    public string SectionText => Section.ToString();

    /// <summary>The count as shown, empty when there is none.</summary>
    public string CountText => Count?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>The pack size as shown.</summary>
    public string PackText => PackSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>The packs as shown.</summary>
    public string PacksText => Packs?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
}

/// <summary>
/// Fasteners, hardware and supplies as rows below the shopping list's boards (&#xA7;7.4, &#xA7;7.5, &#xA7;8):
/// the same rows on screen and in the CSV. <see cref="ShoppingList"/> itself is not touched by this.
/// </summary>
public static class SuppliesList
{
    /// <summary>The columns, in the order they are shown and written.</summary>
    public const string Header = "Section,Item,Size,Count,Pack,Packs,For";

    /// <summary>What the file says it is before, as the shopping list does.</summary>
    public const string Statement = "Fasteners, hardware and supplies: counted from the joints and parts; sizes and packs are the builder's own typed text.";

    /// <summary>What a fastener with no typed choice shows in the Size column.</summary>
    public const string SizeNotChosen = "size not chosen";

    /// <summary>Why a typed pack size is refused: "A pack size is a whole number of at least 1, or blank; "0" is not."</summary>
    /// <param name="typed">What was typed, trimmed.</param>
    public static string PackSizeRefusal(string typed) => $"A pack size is a whole number of at least 1, or blank; \"{typed}\" is not.";

    /// <summary>The fasteners, hardware and supplies, in that order.</summary>
    /// <param name="sketch">The design.</param>
    public static ImmutableArray<ExtraRow> Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        List<ExtraRow> rows = [];
        foreach (FastenerRow fastener in FastenerList.Of(sketch))
        {
            string item = fastener.Thickness is { } thickness
                ? $"{KindName(fastener.Kind)}, {CutListCsv.Text(thickness)} stock"
                : KindName(fastener.Kind);
            rows.Add(new ExtraRow(
                ExtraSection.Fasteners,
                item,
                fastener.SizeText.Length == 0 ? SizeNotChosen : fastener.SizeText,
                fastener.Count,
                fastener.PackSize,
                fastener.Packs,
                fastener.For));
        }

        foreach (HardwareRow hardware in HardwareList.Of(sketch))
        {
            rows.Add(new ExtraRow(ExtraSection.Hardware, hardware.Name, string.Empty, hardware.Count, null, null, hardware.For));
        }

        foreach (SupplyLine line in sketch.Supplies)
        {
            rows.Add(new ExtraRow(ExtraSection.Supplies, line.Item, string.Empty, null, null, null, line.Note));
        }

        // Every joint that buys anything, a box's or a strut's end's: whether it is glued.
        bool[] glued =
        [
            .. sketch.RelationshipsInOrder.OfType<Joint>().Where(joint =>
                sketch.Find<Box>(joint.Inserted.Box) is not { } inserted
                || sketch.Find<Box>(joint.Receiving.Box) is not { } receiving
                || FastenerList.Buys(inserted, receiving)).Select(joint => joint.Glue),
            .. sketch.RelationshipsInOrder.OfType<StrutJoint>().Where(joint =>
                sketch.Find(joint.Inserted.Strut) is not { } inserted
                || sketch.Find(joint.Receiving.Owner) is not { } receiving
                || FastenerList.Buys(inserted, receiving)).Select(joint => joint.Glue),
        ];
        if (glued.Length > 0)
        {
            rows.Add(new ExtraRow(ExtraSection.Supplies, GlueLine(glued.Count(glue => glue), glued.Length), string.Empty, null, null, null, string.Empty));
        }

        return [.. rows];
    }

    /// <summary>"Glue: 20 of 34 joints": the one supply napkin can stand behind (&#xA7;8).</summary>
    /// <param name="glued">How many joints are glued.</param>
    /// <param name="joints">How many joints there are.</param>
    public static string GlueLine(int glued, int joints)
        => $"Glue: {glued} of {joints} joint{(joints == 1 ? string.Empty : "s")}";

    /// <summary>A fastener kind as the list names it.</summary>
    /// <param name="kind">The kind.</param>
    public static string KindName(FastenerKind kind) => kind switch
    {
        FastenerKind.PocketScrew => "Pocket screw",
        FastenerKind.WoodScrew => "Wood screw",
        FastenerKind.Brad => "Brad",
        FastenerKind.Nail => "Nail",
        FastenerKind.Dowel => "Dowel",
        FastenerKind.Biscuit => "Biscuit",
        _ => "Tabletop clip",
    };

    /// <summary>The text of one row, field by field.</summary>
    /// <param name="row">The row.</param>
    public static ImmutableArray<string> Fields(ExtraRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return [row.SectionText, row.Item, row.Size, row.CountText, row.PackText, row.PacksText, row.For];
    }

    /// <summary>The rows as CSV text: the statement, the column names, then a line per row.</summary>
    /// <param name="rows">The rows, in the order they are on screen.</param>
    public static string ToCsv(IEnumerable<ExtraRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        StringBuilder csv = new();
        csv.Append(CutListCsv.Field(Statement)).Append('\n').Append(Header).Append('\n');
        foreach (ExtraRow row in rows)
        {
            csv.AppendJoin(',', Fields(row).Select(CutListCsv.Field)).Append('\n');
        }

        return csv.ToString();
    }
}

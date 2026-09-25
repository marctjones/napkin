using System.Text.Json;

namespace Napkin.Core.RulesEngine;

/// <summary>Reads one overlay file (design §1.3): the operations a layer applies to one table.</summary>
internal static class OverlayReader
{
    public static RawOverlay? Read(
        IPackSource source, string file, IReadOnlyDictionary<string, SourceDocument> sources, CitationLayer layer, ProblemList problems)
    {
        JsonObj? root = ManifestReader.OpenVersioned(source, file, problems, out JsonDocument? document);
        using (document)
        {
            if (root is null)
            {
                return null;
            }

            string? table = root.String("table");
            if (table is null)
            {
                return null;
            }

            root.Where = new Where(file, table);
            int before = problems.Count;
            TableReader.ResolveSource(root.String("source"), sources, "source", root.Where, problems);
            root.String("location");
            List<RawOperation> operations = [];
            IReadOnlyList<JsonElement>? items = root.Array("operations");
            for (int i = 0; items is not null && i < items.Count; i++)
            {
                RawOperation? op = ReadOperation(items[i], i, file, root.Where, sources, layer, problems);
                if (op is not null)
                {
                    operations.Add(op);
                }
            }

            root.Done();
            return problems.Count > before ? null : new RawOverlay(file, table, operations);
        }
    }

    private static RawOperation? ReadOperation(
        JsonElement element, int index, string file, Where tableWhere, IReadOnlyDictionary<string, SourceDocument> sources,
        CitationLayer layer, ProblemList problems)
    {
        string path = $"operations[{index}]";
        Where where = tableWhere with { Row = $"operation {index}" };
        JsonObj? o = JsonObj.Create(element, path, where, problems);
        if (o is null)
        {
            return null;
        }

        int before = problems.Count;
        OpKind? op = o.Enum("op", Vocabulary.Operations);
        SourceDocument? doc = TableReader.ResolveSource(o.String("source"), sources, $"{path}.source", where, problems);
        o.String("location");
        RawOperation? result = null;
        switch (op)
        {
            case OpKind.Add or OpKind.Amend:
                JsonElement? row = o.Get("row");
                RawRow? raw = row is null ? null : TableReader.ReadRawRow(row.Value, $"{path}.row", file, where, layer, doc, problems);
                result = raw is null ? null : new RawOperation(op.Value, index, raw.Id, raw, null, null);
                break;
            case OpKind.Delete:
                string? rowId = o.String("rowId");
                result = rowId is null ? null : new RawOperation(op.Value, index, rowId, null, null, null);
                break;
            case OpKind.AddTable or OpKind.AmendTable:
                JsonObj? t = o.Obj("table");
                if (t is not null)
                {
                    TableMeta? meta = TableReader.ReadMeta(t, doc, layer, file, problems);
                    List<RawRow>? rows = op == OpKind.AddTable ? TableReader.ReadRows(t, file, layer, doc, problems) : null;
                    t.Done();
                    result = meta is null || (op == OpKind.AddTable && rows is null) ? null : new RawOperation(op.Value, index, null, null, meta, rows);
                }

                break;
            case OpKind.DeleteTable:
                result = new RawOperation(op.Value, index, null, null, null, null);
                break;
        }

        o.Done();
        return problems.Count > before ? null : result;
    }
}

/// <summary>
/// Applies an overlay to the tables composed so far: the three operations over dictionaries
/// keyed by id that design §1.3 describes. Conflicts are problems, never last-one-wins (§6.3).
/// </summary>
internal static class Composer
{
    public static void Apply(RawOverlay overlay, Dictionary<string, RawTable> tables, ProblemList problems)
    {
        Where where = new(overlay.File, overlay.Table);
        List<RawOperation> tableOps = [.. overlay.Operations.Where(o => o.Op is OpKind.AddTable or OpKind.AmendTable or OpKind.DeleteTable)];
        List<RawOperation> rowOps = [.. overlay.Operations.Where(o => o.Op is OpKind.Add or OpKind.Amend or OpKind.Delete)];

        foreach (RawOperation extra in tableOps.Skip(1))
        {
            problems.Add(where with { Row = $"operation {extra.Index}" }, "a second table operation on the same table in one overlay; one per table per layer.");
        }

        if (tableOps.Count > 0)
        {
            RawOperation op = tableOps[0];
            Where opWhere = where with { Row = $"operation {op.Index}" };
            tables.TryGetValue(overlay.Table, out RawTable? existing);
            switch (op.Op)
            {
                case OpKind.AddTable when existing is not null:
                    problems.Add(opWhere, $"add-table: table '{overlay.Table}' already exists in the layer below; use amend-table.");
                    break;
                case OpKind.AddTable:
                    tables[overlay.Table] = new RawTable(overlay.Table, op.TableMeta!, op.TableRows!);
                    break;
                case OpKind.AmendTable when existing is null:
                case OpKind.DeleteTable when existing is null:
                    problems.Add(opWhere, $"{Vocabulary.OpName(op.Op)}: table '{overlay.Table}' does not exist in the layer below.");
                    break;
                case OpKind.AmendTable:
                    existing!.Meta = op.TableMeta!;
                    break;
                case OpKind.DeleteTable when rowOps.Count > 0:
                    problems.Add(opWhere, "delete-table: cannot be combined with row operations on the same table.");
                    return;
                default:
                    tables.Remove(overlay.Table);
                    break;
            }
        }

        if (rowOps.Count == 0)
        {
            return;
        }

        if (!tables.TryGetValue(overlay.Table, out RawTable? table))
        {
            foreach (RawOperation op in rowOps)
            {
                problems.Add(
                    where with { Row = $"operation {op.Index}" },
                    $"{Vocabulary.OpName(op.Op)}: table '{overlay.Table}' is not in the layer below; a new table is added with add-table.");
            }

            return;
        }

        HashSet<string> targeted = new(StringComparer.Ordinal);
        foreach (RawOperation op in rowOps)
        {
            Where opWhere = where with { Row = $"operation {op.Index}: row {op.RowId}" };
            if (!targeted.Add(op.RowId!))
            {
                problems.Add(opWhere, $"two operations in one overlay target row '{op.RowId}'.");
                continue;
            }

            int index = table.Rows.FindIndex(r => r.Id == op.RowId);
            switch (op.Op)
            {
                case OpKind.Add when index >= 0:
                    problems.Add(opWhere, $"add: row '{op.RowId}' already exists in the layer below; use amend.");
                    break;
                case OpKind.Add:
                    table.Rows.Add(op.Row!);
                    break;
                case OpKind.Amend or OpKind.Delete when index < 0:
                    problems.Add(opWhere, $"{Vocabulary.OpName(op.Op)}: row '{op.RowId}' does not exist in the layer below.");
                    break;
                case OpKind.Amend:
                    table.Rows[index] = op.Row!;
                    break;
                default:
                    table.Rows.RemoveAt(index);
                    break;
            }
        }
    }
}

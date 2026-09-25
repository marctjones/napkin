using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// Sizes a header against a loaded pack's table for the wall kind. Pure: same pack, same request,
/// same result. Comparisons are exact on integers; there is no epsilon, no rounding of the input
/// and no value between rows (design §3.5, §4.2).
/// </summary>
internal static class HeaderEvaluator
{
    public static HeaderResult Size(LoadedPack pack, HeaderRequest request)
    {
        AdoptedCodeRef code = pack.Code;
        HeaderSizingTable? table = pack.Tables.FirstOrDefault(t => t.WallKind == request.Kind);
        if (table is null)
        {
            string kind = Vocabulary.WallKindName(request.Kind);
            return new HeaderResult.NoData(
                NoDataReason.NoTableForWallKind,
                code,
                request.Kind,
                $"The loaded pack {code.ShortName} has no header table for {kind} walls, so napkin cannot size this header. "
                + "Nothing is guessed: add the table to the pack directory from your copy of the code (docs/rules-engine.md).");
        }

        Dictionary<string, CellValue?> values = new(StringComparer.Ordinal)
        {
            ["supports"] = CellValue.Category(request.Supports),
            ["seismicDesignCategory"] = request.Site.SeismicDesignCategory is { } sdc ? CellValue.Category(sdc) : null,
            ["groundSnowLoad"] = request.Site.GroundSnowLoadPsf is { } snow ? CellValue.Whole(ColumnType.Psf, snow) : null,
            ["ultimateWindSpeed"] = request.Site.UltimateWindSpeedMph is { } wind ? CellValue.Whole(ColumnType.Mph, wind) : null,
            ["buildingWidth"] = request.Site.BuildingWidth is { } width ? CellValue.Of(width) : null,
            ["frostDepth"] = request.Site.FrostDepth is { } frost ? CellValue.Of(frost) : null,
            [Vocabulary.HeaderSpan] = CellValue.Of(request.HeaderSpan),
        };

        List<string> missing = [.. table.RequiredInputs.Where(name => values[name] is null)];
        if (missing.Count > 0)
        {
            return new HeaderResult.InputMissing(
                missing.ToValueList(),
                table.Designation,
                code,
                $"Table {table.Designation} needs {string.Join(", ", missing)}, which has not been entered. "
                + "Enter it for the site; napkin never assumes a value.");
        }

        Selection selection = new(pack, table, values);
        return selection.Run();
    }

    /// <summary>One lookup: narrows the rows column by column, recording the trace as it goes.</summary>
    private sealed class Selection(LoadedPack pack, HeaderSizingTable table, Dictionary<string, CellValue?> values)
    {
        private readonly List<BandMatch> trace = [];

        public HeaderResult Run()
        {
            foreach (Footnote footnote in table.Footnotes.Where(f => f.AppliesTo == FootnoteScope.Table && Excludes(f)))
            {
                return Narrowed(footnote, row: null);
            }

            IEnumerable<HeaderRow> candidates = table.Rows;
            foreach (InputColumn column in table.Inputs.Where(c => c.Band == BandKind.Exact))
            {
                CellValue input = values[column.Name]!.Value;
                List<HeaderRow> matched = [.. candidates.Where(r => r.Inputs[column.Name].Symbol == input.Symbol)];
                if (matched.Count == 0)
                {
                    trace.Add(new BandMatch(column.Name, input.ToString(), "no rows"));
                    return new HeaderResult.OutOfScope(
                        OutOfScopeReason.ConditionNotCovered,
                        TableCitation($"{column.Name} is one of: {string.Join(", ", column.Values)}"),
                        $"Table {table.Designation} has no rows for {column.Name} '{input}'; it covers only {string.Join(", ", column.Values)}. "
                        + "This case is outside the prescriptive table: get an engineer.");
                }

                trace.Add(new BandMatch(column.Name, input.ToString(), $"= {input}"));
                candidates = matched;
            }

            // Upper-bound columns first, in declared order (they nest), then the capacity column,
            // which turns "which row" into "which member" (design §4.1).
            foreach (InputColumn column in table.Inputs.Where(c => c.Band != BandKind.Exact).OrderBy(c => c.Band == BandKind.Capacity ? 1 : 0))
            {
                CellValue input = values[column.Name]!.Value;
                List<HeaderRow> rows = [.. candidates];
                ColumnDomain domain = column.Domain!;
                if (input.Magnitude < domain.Min.Magnitude)
                {
                    HeaderRow lowest = rows.OrderBy(r => r.Inputs[column.Name].Magnitude).ThenBy(r => r.Id, StringComparer.Ordinal).First();
                    trace.Add(new BandMatch(column.Name, input.ToString(), $"below the table's smallest {domain.Min}"));
                    return new HeaderResult.OutOfScope(
                        OutOfScopeReason.InputBelowTableBands,
                        RowCitation(lowest),
                        $"{column.Name} {input} is below the smallest value table {table.Designation} covers ({domain.Min}). "
                        + "This case is outside the prescriptive table: get an engineer.");
                }

                // The smallest bound or capacity that is still at least the input: the
                // conservative direction for both band kinds (design §4.1). Exact integer comparison.
                long? chosen = rows.Select(r => r.Inputs[column.Name].Magnitude)
                    .Where(bound => input.Magnitude <= bound)
                    .Select(bound => (long?)bound)
                    .Min();
                if (chosen is null)
                {
                    HeaderRow limit = rows
                        .OrderByDescending(r => r.Inputs[column.Name].Magnitude)
                        .ThenByDescending(r => r.Inputs[Vocabulary.HeaderSpan].Magnitude)
                        .ThenBy(r => r.Id, StringComparer.Ordinal)
                        .First();
                    CellValue largest = limit.Inputs[column.Name];
                    trace.Add(new BandMatch(column.Name, input.ToString(), $"above the largest ≤ {largest}"));
                    bool span = column.Band == BandKind.Capacity;
                    return new HeaderResult.OutOfScope(
                        span ? OutOfScopeReason.SpanExceedsTable : OutOfScopeReason.InputAboveTableBands,
                        RowCitation(limit),
                        span
                            ? $"The header span {input} is longer than the longest span table {table.Designation} gives for these conditions ({largest}, row {limit.Id}). "
                              + "This opening is outside the prescriptive table: get an engineer."
                            : $"{column.Name} {input} is above the largest band table {table.Designation} covers ({largest}, row {limit.Id}). "
                              + "This case is outside the prescriptive table: get an engineer.");
                }

                CellValue bound = new(input.Type, null, chosen.Value);
                trace.Add(new BandMatch(column.Name, input.ToString(), $"≤ {bound}"));
                candidates = rows.Where(r => r.Inputs[column.Name].Magnitude == chosen.Value);
            }

            // Band validation at load guarantees exactly one row remains (design §4.3).
            HeaderRow row = candidates.Single();
            foreach (string id in row.Footnotes)
            {
                Footnote footnote = table.Footnotes.First(f => f.Id == id);
                if (Excludes(footnote))
                {
                    return Narrowed(footnote, row);
                }
            }

            return new HeaderResult.Sized(row.Header, row.JackStuds, row.KingStuds, RowCitation(row));
        }

        private bool Excludes(Footnote footnote)
        {
            if (footnote.EncodedAs != FootnoteEncoding.AsLimit)
            {
                return false;
            }

            FootnoteLimit limit = footnote.Limit!;
            CellValue input = values[limit.Input]!.Value;
            return limit.Above is { } above ? input.Magnitude > above.Magnitude : input.Symbol == limit.EqualTo;
        }

        private HeaderResult.OutOfScope Narrowed(Footnote footnote, HeaderRow? row)
        {
            FootnoteLimit limit = footnote.Limit!;
            CellValue input = values[limit.Input]!.Value;
            string rule = limit.Above is { } above ? $"above {above}" : $"= {limit.EqualTo}";
            trace.Add(new BandMatch(limit.Input, input.ToString(), $"excluded by footnote {footnote.Id} ({rule})"));
            Citation citation = row is null ? TableCitation($"footnote {footnote.Id}") : RowCitation(row);
            return new HeaderResult.OutOfScope(
                OutOfScopeReason.NarrowedByFootnote,
                citation,
                $"Footnote {footnote.Id} of table {table.Designation} excludes {limit.Input} {rule}: \"{footnote.Text}\". "
                + "This case is outside the prescriptive table: get an engineer.");
        }

        private Citation RowCitation(HeaderRow row)
            => new(
                pack.Code,
                table.Designation,
                row.Id,
                Label(row),
                row.Layer,
                row.Source,
                Footnotes(row).ToValueList(),
                trace.ToValueList());

        private Citation TableCitation(string label)
            => new(
                pack.Code,
                table.Designation,
                null,
                label,
                table.Layer,
                table.Source,
                Footnotes(null).ToValueList(),
                trace.ToValueList());

        private IEnumerable<FootnoteRef> Footnotes(HeaderRow? row)
            => table.Footnotes
                .Where(f => f.AppliesTo == FootnoteScope.Table || (row is not null && row.Footnotes.Contains(f.Id)))
                .Select(f => new FootnoteRef(f.Id, f.Text, f.EncodedAs));

        private string Label(HeaderRow row)
            => string.Join("; ", table.Inputs.Select(c => c.Band == BandKind.Exact
                ? $"{c.Name} = {row.Inputs[c.Name]}"
                : $"{c.Name} ≤ {row.Inputs[c.Name]}"))
               + $" → {row.Header}, {row.JackStuds} jack, {row.KingStuds} king";
    }
}

/// <summary>Answers rules questions for one adopted code. One engine per pack; no global "current code" (design §3.4).</summary>
public interface IRulesEngine
{
    /// <summary>The pack this engine was built from.</summary>
    AdoptedCodeRef Code { get; }

    /// <summary>Sizes a header. Never throws for a request no row covers: that is a result.</summary>
    HeaderResult SizeHeader(HeaderRequest request);
}

/// <summary>Builds engines, and answers for a project that may have no pack at all.</summary>
public static class RulesEngine
{
    /// <summary>
    /// An engine over a loaded, validated, composed pack. Pure: same pack, same request, same
    /// result; never touches the file system.
    /// </summary>
    public static IRulesEngine For(LoadedPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return new Engine(pack);
    }

    /// <summary>
    /// Sizes a header for a project whose pack may be missing: with no pack the answer is
    /// <see cref="HeaderResult.NoData"/>, never a guess.
    /// </summary>
    public static HeaderResult SizeHeader(LoadedPack? pack, HeaderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return pack is null
            ? new HeaderResult.NoData(
                NoDataReason.NoPackSelected,
                null,
                request.Kind,
                "No adopted code is selected for this project (or its pack is unavailable), so napkin cannot size this header. Choose a code pack.")
            : HeaderEvaluator.Size(pack, request);
    }

    private sealed class Engine(LoadedPack pack) : IRulesEngine
    {
        public AdoptedCodeRef Code => pack.Code;

        public HeaderResult SizeHeader(HeaderRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return HeaderEvaluator.Size(pack, request);
        }
    }
}

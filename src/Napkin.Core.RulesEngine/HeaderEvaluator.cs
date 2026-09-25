using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// Sizes a header against a loaded pack's table for the wall kind. Pure: same pack, same request,
/// same result. Comparisons are exact on integers and exact fractions; there is no epsilon and no
/// rounding of the input. A value between rows exists only where a pack footnote declares an
/// interpolation, and only strictly between the two columns it names (design §3.5, §4.2, §4.4).
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
            [Vocabulary.RoofLiveLoad] = request.Site.RoofLiveLoadPsf is { } live ? CellValue.Whole(ColumnType.Psf, live) : null,
            [Vocabulary.HeaderSpan] = CellValue.Of(request.HeaderSpan),
        };

        List<string> missing = [.. table.RequiredInputs.Where(name => values[name] is null)];
        if (missing.Count > 0)
        {
            return Missing(table, code, missing, null);
        }

        Selection selection = new(pack, table, values);
        return selection.Run();
    }

    private static HeaderResult.InputMissing Missing(HeaderSizingTable table, AdoptedCodeRef code, List<string> missing, Footnote? because)
        => new(
            missing.ToValueList(),
            table.Designation,
            code,
            $"Table {table.Designation} needs {string.Join(", ", missing)}, which has not been entered"
            + (because is null ? "." : $" (footnote {because.Id}: \"{because.Text}\").")
            + " Enter it for the site; napkin never assumes a value.");

    /// <summary>One lookup: narrows the rows column by column, recording the trace as it goes.</summary>
    private sealed class Selection(LoadedPack pack, HeaderSizingTable table, Dictionary<string, CellValue?> values)
    {
        private readonly List<BandMatch> trace = [];

        /// <summary>The interpolation in effect for this request, once its column is reached; null for a plain table lookup.</summary>
        private (Footnote Footnote, FootnoteOperation.Interpolate Op)? interpolating;

        public HeaderResult Run()
        {
            HeaderResult? substituted = Substitute();
            if (substituted is not null)
            {
                return substituted;
            }

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
                if (column.Band == BandKind.Capacity && interpolating is { } active)
                {
                    return Interpolated(input, rows, active.Footnote, active.Op);
                }

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

                // Strictly between the two columns a footnote declares: keep the rows of both
                // columns; the capacity column interpolates between them (design §4.4). At a
                // column exactly, or outside the pair, the plain lookup below runs unchanged.
                if (InterpolationFor(column.Name, input) is { } found)
                {
                    interpolating = found;
                    CellValue lower = found.Op.Lower;
                    CellValue upper = found.Op.Upper;
                    trace.Add(new BandMatch(column.Name, input.ToString(), $"between ≤ {lower} and ≤ {upper}: interpolated by footnote {found.Footnote.Id}"));
                    candidates = rows.Where(r => r.Inputs[column.Name].Magnitude == lower.Magnitude || r.Inputs[column.Name].Magnitude == upper.Magnitude);
                    continue;
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
            if (RowExclusion(row) is { } excluded)
            {
                return excluded;
            }

            return new HeaderResult.Sized(row.Header, row.JackStuds, row.KingStuds, RowCitation(row));
        }

        /// <summary>
        /// A footnote's substitute-input operation: an input below its threshold is replaced by the
        /// footnote's value when its condition holds; the request is missing the condition's input
        /// when it is not entered, and out of scope, citing the footnote, when the condition fails.
        /// </summary>
        private HeaderResult? Substitute()
        {
            foreach (Footnote footnote in table.Footnotes)
            {
                foreach (FootnoteOperation.SubstituteInput op in footnote.Operations.OfType<FootnoteOperation.SubstituteInput>())
                {
                    CellValue input = values[op.Input]!.Value;
                    if (input.Magnitude >= op.Below.Magnitude)
                    {
                        continue;
                    }

                    if (values[op.WhenInput] is not { } condition)
                    {
                        return Missing(table, pack.Code, [op.WhenInput], footnote);
                    }

                    if (condition.Magnitude > op.AtMost.Magnitude)
                    {
                        trace.Add(new BandMatch(op.Input, input.ToString(), $"below {op.Below}, but {op.WhenInput} {condition} is above {op.AtMost}: excluded by footnote {footnote.Id}"));
                        return new HeaderResult.OutOfScope(
                            OutOfScopeReason.NarrowedByFootnote,
                            TableCitation($"footnote {footnote.Id}"),
                            $"Footnote {footnote.Id} of table {table.Designation} lets {op.Input} below {op.Below} be taken as {op.Use} only when {op.WhenInput} is at most {op.AtMost}; "
                            + $"here {op.Input} is {input} and {op.WhenInput} is {condition}: \"{footnote.Text}\". "
                            + "This case is outside the prescriptive table: get an engineer.");
                    }

                    values[op.Input] = op.Use;
                    trace.Add(new BandMatch(op.Input, input.ToString(), $"taken as {op.Use} by footnote {footnote.Id} ({op.WhenInput} {condition} ≤ {op.AtMost})"));
                }
            }

            return null;
        }

        private (Footnote Footnote, FootnoteOperation.Interpolate Op)? InterpolationFor(string column, CellValue input)
            => table.Footnotes
                .SelectMany(f => f.Operations.OfType<FootnoteOperation.Interpolate>().Select(op => (Footnote: f, Op: op)))
                .Where(x => x.Op.Input == column && input.Magnitude > x.Op.Lower.Magnitude && input.Magnitude < x.Op.Upper.Magnitude)
                .Select(x => ((Footnote, FootnoteOperation.Interpolate)?)x)
                .FirstOrDefault();

        /// <summary>
        /// The interpolated capacity lookup: pairs the rows of the two columns by member, gives each
        /// member the exact linear interpolation of its two spans, and chooses the smallest member
        /// whose exact span is at least the opening. Stud counts are the larger of the pair.
        /// </summary>
        private HeaderResult Interpolated(CellValue opening, List<HeaderRow> rows, Footnote footnote, FootnoteOperation.Interpolate op)
        {
            string column = op.Input;
            CellValue input = values[column]!.Value;
            ExactFraction weight = SpanInterpolation.Weight(ExactFraction.Whole(input.Magnitude), op.Lower.Magnitude, op.Upper.Magnitude);
            List<HeaderRow> atLower = [.. rows.Where(r => r.Inputs[column].Magnitude == op.Lower.Magnitude)];
            List<HeaderRow> atUpper = [.. rows.Where(r => r.Inputs[column].Magnitude == op.Upper.Magnitude)];

            // A member with a row at only one of the two columns has nothing to interpolate with:
            // it is not offered (never given the lower column's span for a heavier load).
            List<(HeaderRow Lower, HeaderRow Upper, ExactFraction Span)> pairs = [.. atLower
                .Join(atUpper, r => r.Header, r => r.Header, (l, u) => (Lower: l, Upper: u))
                .Select(p => (p.Lower, p.Upper, Span: SpanInterpolation.Span(Span(p.Lower), Span(p.Upper), weight)))
                .OrderBy(p => p.Span)
                .ThenBy(p => Span(p.Upper))
                .ThenBy(p => p.Upper.Id, StringComparer.Ordinal)];
            List<string> unpaired = [.. atLower.Concat(atUpper).Where(r => !pairs.Any(p => p.Lower == r || p.Upper == r)).Select(r => r.Id).Order(StringComparer.Ordinal)];
            if (unpaired.Count > 0)
            {
                trace.Add(new BandMatch(Vocabulary.HeaderSpan, opening.ToString(), $"not interpolated (no row for the same member at the other column): {string.Join(", ", unpaired)}"));
            }

            (HeaderRow Lower, HeaderRow Upper, ExactFraction Span)? chosen = pairs
                .Where(p => SpanInterpolation.Covers(p.Span, new Length(opening.Magnitude)))
                .Select(p => ((HeaderRow, HeaderRow, ExactFraction)?)p)
                .FirstOrDefault();
            if (chosen is not { } pick)
            {
                if (pairs.Count == 0)
                {
                    HeaderRow any = atUpper.Concat(atLower).OrderBy(r => r.Id, StringComparer.Ordinal).First();
                    trace.Add(new BandMatch(Vocabulary.HeaderSpan, opening.ToString(), "no member has rows at both columns"));
                    return new HeaderResult.OutOfScope(
                        OutOfScopeReason.SpanExceedsTable,
                        RowCitation(any),
                        $"Table {table.Designation} has no member with rows at both {op.Lower} and {op.Upper}, so footnote {footnote.Id}'s interpolation gives no span. "
                        + "This opening is outside the prescriptive table: get an engineer.");
                }

                (HeaderRow Lower, HeaderRow Upper, ExactFraction Span) longest = pairs[^1];
                Length shown = SpanInterpolation.Shown(longest.Span);
                trace.Add(new BandMatch(Vocabulary.HeaderSpan, opening.ToString(), $"above the longest interpolated span {CellValue.Of(shown)} (exact {longest.Span} × 1/1024\")"));
                InterpolationTrace limit = Working(footnote, op, input, weight, longest.Lower, longest.Upper, longest.Span);
                return new HeaderResult.OutOfScope(
                    OutOfScopeReason.SpanExceedsTable,
                    RowCitation(longest.Upper, longest.Lower, limit),
                    $"The header span {opening} is longer than the longest span table {table.Designation} gives for these conditions, "
                    + $"interpolated by footnote {footnote.Id} ({CellValue.Of(shown)}, between rows {longest.Lower.Id} and {longest.Upper.Id}). "
                    + "This opening is outside the prescriptive table: get an engineer.");
            }

            Length span = SpanInterpolation.Shown(pick.Span);
            trace.Add(new BandMatch(Vocabulary.HeaderSpan, opening.ToString(), $"≤ {CellValue.Of(span)} interpolated (exact {pick.Span} × 1/1024\", shown rounded down to 1/16\")"));
            int jacks = Math.Max(pick.Lower.JackStuds, pick.Upper.JackStuds);
            int kings = Math.Max(pick.Lower.KingStuds, pick.Upper.KingStuds);
            trace.Add(new BandMatch("jackStuds", $"{pick.Lower.JackStuds} and {pick.Upper.JackStuds}", $"{jacks}: counts are not interpolated; the larger of the two rows is used"));
            trace.Add(new BandMatch("kingStuds", $"{pick.Lower.KingStuds} and {pick.Upper.KingStuds}", $"{kings}: counts are not interpolated; the larger of the two rows is used"));
            if ((RowExclusion(pick.Lower) ?? RowExclusion(pick.Upper)) is { } excluded)
            {
                return excluded;
            }

            InterpolationTrace working = Working(footnote, op, input, weight, pick.Lower, pick.Upper, pick.Span);
            return new HeaderResult.Sized(pick.Upper.Header, jacks, kings, RowCitation(pick.Upper, pick.Lower, working));
        }

        private InterpolationTrace Working(
            Footnote footnote, FootnoteOperation.Interpolate op, CellValue input, ExactFraction weight, HeaderRow lower, HeaderRow upper, ExactFraction span)
            => new(
                op.Input,
                input,
                op.Lower,
                op.Upper,
                lower.Id,
                upper.Id,
                new Length(Span(lower)),
                new Length(Span(upper)),
                weight,
                span,
                SpanInterpolation.Shown(span),
                Ref(footnote));

        private static long Span(HeaderRow row) => row.Inputs[Vocabulary.HeaderSpan].Magnitude;

        private HeaderResult.OutOfScope? RowExclusion(HeaderRow row)
        {
            foreach (string id in row.Footnotes)
            {
                Footnote footnote = table.Footnotes.First(f => f.Id == id);
                if (Excludes(footnote))
                {
                    return Narrowed(footnote, row);
                }
            }

            return null;
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

        /// <summary>An interpolated result's citation: the upper column's row (the one a plain lookup would give), both rows' footnotes, and the working.</summary>
        private Citation RowCitation(HeaderRow upper, HeaderRow lower, InterpolationTrace working)
            => new(
                pack.Code,
                table.Designation,
                upper.Id,
                $"{working.Column} {working.Input} interpolated between rows {lower.Id} and {upper.Id} → {upper.Header}, span {CellValue.Of(working.SpanShown)}",
                upper.Layer,
                upper.Source,
                Footnotes(upper).Concat(Footnotes(lower)).Distinct().ToValueList(),
                trace.ToValueList(),
                working);

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
                .Select(Ref);

        private FootnoteRef Ref(Footnote f) => new(f.Id, f.Text, f.EncodedAs, f.Source ?? table.Source);

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

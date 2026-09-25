using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// Checks a wall line's bracing against a pack's provisions (docs/rules-engine.md "Wall bracing").
/// Pure and exact: required and provided lengths are exact fractions of 1/1024″ until each is
/// rounded once, required UP and each contribution DOWN, to the pack's step. A separate evaluator
/// from the header lookup: it reads a whole wall line, not one opening (design §3.3).
/// </summary>
internal static class BracingEvaluator
{
    public static BracingResult Check(LoadedPack pack, BracingRequest request)
    {
        AdoptedCodeRef code = pack.Code;
        if (pack.Bracing is not { } provisions)
        {
            return new BracingResult.NoData(
                BracingNoDataReason.NoBracingProvisions,
                code,
                BracingResult.NoData.NoProvisionsExplanation(code.ShortName));
        }

        SiteInputs site = request.Site;
        Dictionary<string, CellValue?> values = new(StringComparer.Ordinal)
        {
            ["seismicDesignCategory"] = site.SeismicDesignCategory is { } sdc ? CellValue.Category(sdc) : null,
            ["groundSnowLoad"] = site.GroundSnowLoadPsf is { } snow ? CellValue.Whole(ColumnType.Psf, snow) : null,
            ["ultimateWindSpeed"] = site.UltimateWindSpeedMph is { } wind ? CellValue.Whole(ColumnType.Mph, wind) : null,
            ["buildingWidth"] = site.BuildingWidth is { } width ? CellValue.Of(width) : null,
            [Vocabulary.WallHeight] = CellValue.Of(request.Line.WallHeight),
        };

        List<string> missing = [.. provisions.RequiredInputs.Where(name => values[name] is null)];
        if (missing.Count > 0)
        {
            return new BracingResult.InputMissing(
                missing.ToValueList(),
                provisions.Section,
                code,
                $"Section {provisions.Section} needs {string.Join(", ", missing)}, which has not been entered. Enter it for the site; napkin never assumes a value.");
        }

        return new LineCheck(code, provisions, request.Line, values).Run();
    }

    private sealed class LineCheck(AdoptedCodeRef code, BracingProvisions provisions, BracedWallLine line, Dictionary<string, CellValue?> values)
    {
        private readonly List<BandMatch> trace = [];

        public BracingResult Run()
        {
            foreach (BracingLimit limit in provisions.Limits)
            {
                CellValue input = values[limit.When.Input]!.Value;
                if (limit.When.Holds(input))
                {
                    trace.Add(new BandMatch(limit.When.Input, input.ToString(), $"excluded by limit {limit.Id} ({limit.When})"));
                    return new BracingResult.OutOfScope(
                        OutOfScopeReason.NotPrescriptive,
                        Cite(limit.Section, limit.Id, $"limit {limit.Id}: {limit.When}", limit.Source),
                        $"Section {limit.Section} limit {limit.Id} excludes {limit.When.Input} {input}: \"{limit.Text}\". "
                        + "This wall line is outside the prescriptive method: get an engineer.");
                }
            }

            if (Base() is not BracingRequiredRow row)
            {
                return scopeResult!;
            }

            // Required, exact: base × line / unit × each multiplier + each added length (in units).
            ExactFraction required = new((Int128)row.Length.Units * line.Length.Units, provisions.UnitLength.Units);
            List<FactorApplied> applied = [];
            foreach (BracingFactor factor in provisions.Factors.Where(f => f.When.Holds(values[f.When.Input]!.Value)))
            {
                CellValue input = values[factor.When.Input]!.Value;
                required = factor.Multiply is { } m
                    ? new ExactFraction(required.Numerator * m.Numerator, required.Denominator * m.Denominator)
                    : new ExactFraction(required.Numerator + (factor.Add!.Value.Units * required.Denominator), required.Denominator);
                applied.Add(new FactorApplied(factor.Id, factor.Section, factor.Effect, $"{factor.When.Input} {input} ({factor.When})", factor.Source.Location));
            }

            Length step = provisions.Step;
            Length requiredRounded = new(CeilingSteps(required, step.Units) * step.Units);

            List<SegmentContribution> segments = [];
            foreach (BracedSegment segment in line.Segments)
            {
                SegmentContribution? contribution = Contribution(segment, step);
                if (contribution is null)
                {
                    return scopeResult!;
                }

                segments.Add(contribution);
            }

            Length provided = segments.Aggregate(Length.Zero, (sum, s) => sum + s.Contribution);
            BracingWorking working = new(
                row.Id,
                row.Length,
                provisions.UnitLength,
                line.Length,
                applied.ToValueList(),
                required,
                step,
                requiredRounded,
                segments.ToValueList());
            Citation citation = Cite(provisions.Section, row.Id, Label(row), row.Source);
            return provided >= requiredRounded
                ? new BracingResult.Passes(requiredRounded, provided, citation, working)
                : new BracingResult.Fails(requiredRounded, provided, requiredRounded - provided, citation, working);
        }

        /// <summary>The out-of-scope result a lookup stopped at, when <see cref="Base"/> or <see cref="Contribution"/> returned null.</summary>
        private BracingResult.OutOfScope? scopeResult;

        /// <summary>The base row for the inputs: exact columns by equality, upper-bound columns by the smallest bound still at least the input.</summary>
        private BracingRequiredRow? Base()
        {
            IEnumerable<BracingRequiredRow> candidates = provisions.Required;
            foreach (InputColumn column in provisions.Inputs.Where(c => c.Band == BandKind.Exact))
            {
                CellValue input = values[column.Name]!.Value;
                List<BracingRequiredRow> matched = [.. candidates.Where(r => r.Inputs[column.Name].Symbol == input.Symbol)];
                if (matched.Count == 0)
                {
                    trace.Add(new BandMatch(column.Name, input.ToString(), "no rows"));
                    scopeResult = new BracingResult.OutOfScope(
                        OutOfScopeReason.ConditionNotCovered,
                        Cite(provisions.Section, null, $"{column.Name} is one of: {string.Join(", ", column.Values)}", provisions.Source),
                        $"Section {provisions.Section} has no rows for {column.Name} '{input}'; it covers only {string.Join(", ", column.Values)}. "
                        + "This wall line is outside the prescriptive method: get an engineer.");
                    return null;
                }

                trace.Add(new BandMatch(column.Name, input.ToString(), $"= {input}"));
                candidates = matched;
            }

            foreach (InputColumn column in provisions.Inputs.Where(c => c.Band == BandKind.UpperBound))
            {
                CellValue input = values[column.Name]!.Value;
                List<BracingRequiredRow> rows = [.. candidates];
                if (input.Magnitude < column.Domain!.Min.Magnitude)
                {
                    trace.Add(new BandMatch(column.Name, input.ToString(), $"below the smallest {column.Domain.Min}"));
                    BracingRequiredRow lowest = rows.OrderBy(r => r.Inputs[column.Name].Magnitude).ThenBy(r => r.Id, StringComparer.Ordinal).First();
                    scopeResult = new BracingResult.OutOfScope(
                        OutOfScopeReason.InputBelowTableBands,
                        Cite(provisions.Section, lowest.Id, Label(lowest), lowest.Source),
                        $"{column.Name} {input} is below the smallest value section {provisions.Section} covers ({column.Domain.Min}). "
                        + "This wall line is outside the prescriptive method: get an engineer.");
                    return null;
                }

                long? chosen = rows.Select(r => r.Inputs[column.Name].Magnitude).Where(bound => input.Magnitude <= bound).Select(b => (long?)b).Min();
                if (chosen is null)
                {
                    BracingRequiredRow top = rows.OrderByDescending(r => r.Inputs[column.Name].Magnitude).ThenBy(r => r.Id, StringComparer.Ordinal).First();
                    trace.Add(new BandMatch(column.Name, input.ToString(), $"above the largest ≤ {top.Inputs[column.Name]}"));
                    scopeResult = new BracingResult.OutOfScope(
                        OutOfScopeReason.InputAboveTableBands,
                        Cite(provisions.Section, top.Id, Label(top), top.Source),
                        $"{column.Name} {input} is above the largest band section {provisions.Section} covers ({top.Inputs[column.Name]}, row {top.Id}). "
                        + "This wall line is outside the prescriptive method: get an engineer.");
                    return null;
                }

                CellValue bound = new(input.Type, null, chosen.Value);
                trace.Add(new BandMatch(column.Name, input.ToString(), $"≤ {bound}"));
                candidates = rows.Where(r => r.Inputs[column.Name].Magnitude == chosen.Value);
            }

            // Band validation at load guarantees exactly one row remains.
            return candidates.Single();
        }

        /// <summary>One segment's contribution, or null (with <see cref="scopeResult"/> set) when its method does not cover the wall height.</summary>
        private SegmentContribution? Contribution(BracedSegment segment, Length step)
        {
            if (segment.Method is not { } id)
            {
                return new SegmentContribution(segment.Label, segment.Length, null, Length.Zero, "not braced: no method assigned");
            }

            if (provisions.Method(id) is not { } method)
            {
                return new SegmentContribution(
                    segment.Label,
                    segment.Length,
                    id,
                    Length.Zero,
                    SegmentContribution.UnknownMethodWhy(id, code.ShortName));
            }

            Length height = line.WallHeight;
            if (height.Units < method.HeightDomain.Min.Magnitude || height.Units > method.HeightDomain.Max.Magnitude)
            {
                bool above = height.Units > method.HeightDomain.Max.Magnitude;
                MinimumPanelRow edge = above ? method.MinimumPanel[^1] : method.MinimumPanel[0];
                trace.Add(new BandMatch(Vocabulary.WallHeight, CellValue.Of(height).ToString(), above ? $"above the largest ≤ {CellValue.Of(edge.WallHeight)} for {method.Name}" : $"below {method.HeightDomain.Min} for {method.Name}"));
                scopeResult = new BracingResult.OutOfScope(
                    above ? OutOfScopeReason.InputAboveTableBands : OutOfScopeReason.InputBelowTableBands,
                    Cite(method.Section, edge.Id, $"{method.Name}: minimum panel for walls ≤ {CellValue.Of(edge.WallHeight)}", edge.Source),
                    $"The wall height {CellValue.Of(height)} is {(above ? "above the tallest" : "below the shortest")} wall section {method.Section} gives a minimum panel length for "
                    + $"{method.Name} ({(above ? CellValue.Of(edge.WallHeight) : method.HeightDomain.Min)}). This wall line is outside the prescriptive method: get an engineer.");
                return null;
            }

            MinimumPanelRow row = method.MinimumPanel.First(r => height <= r.WallHeight);
            if (segment.Length < row.Length)
            {
                return new SegmentContribution(
                    segment.Label,
                    segment.Length,
                    id,
                    Length.Zero,
                    $"{method.Name}: shorter than the {CellValue.Of(row.Length)} minimum panel for walls ≤ {CellValue.Of(row.WallHeight)} ({method.Section} row {row.Id}), so it counts for nothing");
            }

            Length counted = method.Cap is { } cap && cap < segment.Length ? cap : segment.Length;
            Length contribution = new(counted.Units / step.Units * step.Units);
            string capped = method.Cap is { } c && c < segment.Length ? $"capped at {CellValue.Of(c)}" : "its full length";
            return new SegmentContribution(
                segment.Label,
                segment.Length,
                id,
                contribution,
                $"{method.Name}: {capped}, rounded down to the {CellValue.Of(step)} step ({method.Section}, row {row.Id})");
        }

        private Citation Cite(string section, string? rowId, string label, SourceRef source)
            => new(
                code,
                section,
                rowId,
                label,
                provisions.Layer,
                source,
                provisions.Footnotes.Select(f => new FootnoteRef(f.Id, f.Text, f.EncodedAs, f.Source ?? provisions.Source)).ToValueList(),
                trace.ToValueList(),
                IsSection: true);

        private string Label(BracingRequiredRow row)
            => string.Join("; ", provisions.Inputs.Select(c => c.Band == BandKind.Exact ? $"{c.Name} = {row.Inputs[c.Name]}" : $"{c.Name} ≤ {row.Inputs[c.Name]}"))
               + $" → {CellValue.Of(row.Length)} per {CellValue.Of(provisions.UnitLength)}";
    }

    /// <summary>The number of whole steps that reach at least <paramref name="units"/>: rounding UP, exactly.</summary>
    internal static long CeilingSteps(ExactFraction units, long step)
    {
        // ceil(p / (q·step)) for p ≥ 0.
        Int128 denominator = units.Denominator * step;
        Int128 q = units.Numerator / denominator;
        return (long)(q * denominator == units.Numerator ? q : q + 1);
    }
}

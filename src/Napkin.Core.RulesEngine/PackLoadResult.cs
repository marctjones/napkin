namespace Napkin.Core.RulesEngine;

/// <summary>
/// One thing wrong with a pack: the file, and where applicable the table and the row or operation
/// (design §9.2).
/// </summary>
public sealed record PackProblem(string File, string? Table, string? RowOrOperation, string Message)
{
    /// <summary>"packs/x/pack.json [R602.7(1)] (row y): message".</summary>
    public override string ToString()
        => File
           + (Table is null ? string.Empty : $" [{Table}]")
           + (RowOrOperation is null ? string.Empty : $" ({RowOrOperation})")
           + ": " + Message;
}

/// <summary>
/// The result of loading a pack: loaded entirely, or invalid with every problem found. Loading
/// never throws for a bad pack (design §9.2).
/// </summary>
public abstract record PackLoadResult
{
    private PackLoadResult()
    {
    }

    /// <summary>The pack loaded, validated and composed.</summary>
    public sealed record Loaded(LoadedPack Pack) : PackLoadResult;

    /// <summary>The pack was refused. Every problem found is listed, not just the first.</summary>
    public sealed record Invalid(string PackId, ValueList<PackProblem> Problems) : PackLoadResult
    {
        /// <summary>One problem per line, for a diagnostics panel or a test failure message.</summary>
        public override string ToString()
            => $"pack '{PackId}' is invalid:" + Environment.NewLine
               + string.Join(Environment.NewLine, Problems.Select(p => "  " + p));
    }
}

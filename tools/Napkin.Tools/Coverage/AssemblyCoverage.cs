namespace Napkin.Tools.Coverage;

/// <summary>
/// One assembly's measured coverage, as counts rather than rates: counts are what can be merged
/// across several coverage reports without double-counting (see <see cref="CoverageMerger"/>).
/// </summary>
/// <param name="Assembly">The assembly name, taken from the Cobertura package name.</param>
/// <param name="LinesCovered">Distinct source lines hit at least once.</param>
/// <param name="LinesCoverable">Distinct source lines the compiler emitted sequence points for.</param>
/// <param name="BranchesCovered">Branch outcomes taken at least once.</param>
/// <param name="BranchesTotal">Branch outcomes the compiler emitted.</param>
public sealed record AssemblyCoverage(
    string Assembly,
    int LinesCovered,
    int LinesCoverable,
    int BranchesCovered,
    int BranchesTotal)
{
    /// <summary>
    /// Line coverage in percentage points, or null when the assembly has no coverable lines —
    /// today's placeholder assemblies, which are reported N/A rather than 0% or 100%.
    /// </summary>
    public double? LinePercent => Percent(LinesCovered, LinesCoverable);

    /// <summary>
    /// Branch coverage in percentage points, or null when the assembly has no branches at all.
    /// Straight-line code is not 0% branch-covered; it has nothing to cover.
    /// </summary>
    public double? BranchPercent => Percent(BranchesCovered, BranchesTotal);

    /// <summary>True when there is nothing to measure — the assembly can never fail the ratchet.</summary>
    public bool IsNotApplicable => LinesCoverable == 0;

    private static double? Percent(int covered, int total) =>
        total == 0 ? null : 100.0 * covered / total;
}

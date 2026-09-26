using Napkin.Tools.Licenses;

namespace Napkin.Tools.Commands;

/// <summary>
/// <c>licenses check</c> (#2): fails when any restored package, direct or transitive, declares no
/// license, or one outside <c>licenses/policy.json</c>'s allowlist, unless that exact package version
/// has a written exception.
/// </summary>
/// <remarks>
/// A package that points at a license file or URL rather than an SPDX expression cannot be judged
/// by a machine: it fails until someone reads the text and records what it says as an exception.
/// An exception for a version no longer restored is reported, so the file does not rot, but does not
/// fail the check.
/// </remarks>
public static class LicensesCommand
{
    private static readonly HashSet<string> Valued = ["--root", "--policy"];
    private static readonly HashSet<string> Flags = ["--list"];

    public static int Check(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        CommandLine parsed = CommandLine.Parse(args, Valued, Flags);
        if (parsed.WantsHelp)
        {
            output.WriteLine(Cli.Usage);
            return ExitCode.Ok;
        }

        RepoLayout layout = Cli.ResolveRoot(parsed);
        LicensePolicy policy = LicensePolicy.Load(parsed.Value("--policy") ?? Path.Combine(layout.Root, LicensePolicy.RelativePath));
        IReadOnlyList<PackageLicense> packages = LicenseScan.Of(layout.Root);

        List<string> failures = [];
        int byException = 0;
        foreach (PackageLicense package in packages)
        {
            bool allowedByExpression = package.Kind == DeclaredKind.Expression && SpdxExpression.IsAllowed(package.Value, policy.Allowed);
            LicenseException? exception = allowedByExpression ? null : policy.ExceptionFor(package.Id, package.Version);
            if (exception is not null)
            {
                byException++;
            }

            if (parsed.Has("--list"))
            {
                string verdict = allowedByExpression ? "ok" : exception is not null ? $"exception ({exception.License})" : "REFUSED";
                output.WriteLine($"  {package.Id} {package.Version}: {package.Declared} — {verdict}");
            }

            if (!allowedByExpression && exception is null)
            {
                string why = package.Kind switch
                {
                    DeclaredKind.None => "declares no license",
                    DeclaredKind.Expression => $"declares {package.Value}, outside the allowlist",
                    _ => $"declares only {package.Declared}, which a person must read and record as an exception",
                };
                failures.Add($"{package.Id} {package.Version} {why} (used by {string.Join(", ", package.Projects)})");
            }
        }

        foreach (LicenseException stale in policy.Exceptions.Where(exception =>
                     !packages.Any(package => string.Equals(package.Id, exception.Package, StringComparison.OrdinalIgnoreCase) && package.Version == exception.Version)))
        {
            output.WriteLine($"licenses check: note — the exception for {stale.Package} {stale.Version} matches no restored package; remove it.");
        }

        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                error.WriteLine($"licenses check: {failure}");
            }

            error.WriteLine($"licenses check: FAILED — {failures.Count} of {packages.Count} package(s) outside the policy ({LicensePolicy.RelativePath}).");
            return ExitCode.GateFailed;
        }

        output.WriteLine($"licenses check: passed — {packages.Count} package(s), {byException} by a written exception.");
        return ExitCode.Ok;
    }
}

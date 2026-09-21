namespace Napkin.Tools;

/// <summary>
/// The process exit codes every command shares, so CI can tell "the gate failed" from
/// "the tool could not read its input" without parsing messages.
/// </summary>
public static class ExitCode
{
    /// <summary>The command did what it was asked to do.</summary>
    public const int Ok = 0;

    /// <summary>A gate failed: coverage fell below a floor, or a required workflow is missing.</summary>
    public const int GateFailed = 1;

    /// <summary>The command line was wrong — unknown command, missing or conflicting options.</summary>
    public const int UsageError = 2;

    /// <summary>An input file was missing, unreadable or malformed.</summary>
    public const int InputError = 3;
}

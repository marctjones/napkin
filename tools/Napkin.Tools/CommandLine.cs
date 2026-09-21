namespace Napkin.Tools;

/// <summary>
/// A very small option parser: `--name value` and `--flag`. Anything it does not recognise is an
/// error rather than a shrug, because a silently ignored `--reason` would be a bug that lowers a
/// coverage floor without recording why.
/// </summary>
public sealed class CommandLine
{
    private readonly Dictionary<string, string?> options = new(StringComparer.Ordinal);

    private CommandLine(IReadOnlyList<string> positional)
    {
        Positional = positional;
    }

    /// <summary>The arguments that are not options, in order.</summary>
    public IReadOnlyList<string> Positional { get; }

    /// <summary>True when the caller asked for help.</summary>
    public bool WantsHelp =>
        options.ContainsKey("--help") || options.ContainsKey("-h") || options.ContainsKey("--h");

    /// <summary>
    /// Parses <paramref name="arguments"/>. <paramref name="valued"/> names the options that take
    /// a value; every other `--option` is a flag.
    /// </summary>
    /// <exception cref="UsageException">An unknown option, or a value option with no value.</exception>
    public static CommandLine Parse(
        IEnumerable<string> arguments,
        IReadOnlySet<string> valued,
        IReadOnlySet<string> flags)
    {
        var positional = new List<string>();
        var parsed = new CommandLine(positional);
        using var walker = arguments.GetEnumerator();

        while (walker.MoveNext())
        {
            var argument = walker.Current;
            if (!argument.StartsWith('-'))
            {
                positional.Add(argument);
                continue;
            }

            var name = argument;
            string? value = null;
            var equals = argument.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
            {
                name = argument[..equals];
                value = argument[(equals + 1)..];
            }

            if (name is "--help" or "-h")
            {
                parsed.options[name] = null;
                continue;
            }

            if (valued.Contains(name))
            {
                if (value is null)
                {
                    if (!walker.MoveNext())
                    {
                        throw new UsageException($"{name} needs a value.");
                    }

                    value = walker.Current;
                }

                parsed.options[name] = value;
                continue;
            }

            if (flags.Contains(name))
            {
                if (value is not null)
                {
                    throw new UsageException($"{name} is a flag and takes no value.");
                }

                parsed.options[name] = null;
                continue;
            }

            throw new UsageException($"unknown option {name}.");
        }

        return parsed;
    }

    public bool Has(string name) => options.ContainsKey(name);

    public string? Value(string name) => options.TryGetValue(name, out var value) ? value : null;
}

/// <summary>The command line was wrong.</summary>
public sealed class UsageException(string message) : Exception(message);

using System.Text;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// Where pack files come from. Paths are relative to the packs root and always use '/', on every
/// platform; a source maps them to its own storage. The root holds <c>layers/&lt;id&gt;/</c> (model-code
/// base layers) and <c>packs/&lt;id&gt;/</c> (adopted codes), per design §1.2.
/// </summary>
public interface IPackSource
{
    /// <summary>The file's size in bytes, or null when it does not exist.</summary>
    long? FileLength(string path);

    /// <summary>The file's bytes. Only called for a file whose length was checked.</summary>
    byte[] ReadFile(string path);

    /// <summary>The names of the files directly in a directory, sorted ordinally; empty when it does not exist.</summary>
    IReadOnlyList<string> ListFiles(string directory);

    /// <summary>The names of the directories directly in a directory, sorted ordinally; empty when it does not exist.</summary>
    IReadOnlyList<string> ListDirectories(string directory);

    /// <summary>Whether the directory exists.</summary>
    bool DirectoryExists(string directory);
}

/// <summary>Pack files in a folder on disk: the path by which a person's own pack directory is loaded.</summary>
public sealed class DirectoryPackSource(string root) : IPackSource
{
    /// <summary>The packs root on disk.</summary>
    public string Root { get; } = root;

    private string Full(string path)
        => path.Length == 0 ? Root : Path.Combine([Root, .. path.Split('/')]);

    /// <inheritdoc/>
    public long? FileLength(string path)
    {
        FileInfo info = new(Full(path));
        return info.Exists ? info.Length : null;
    }

    /// <inheritdoc/>
    public byte[] ReadFile(string path) => File.ReadAllBytes(Full(path));

    /// <inheritdoc/>
    public IReadOnlyList<string> ListFiles(string directory)
    {
        string full = Full(directory);
        return Directory.Exists(full)
            ? [.. Directory.GetFiles(full).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)]
            : [];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ListDirectories(string directory)
    {
        string full = Full(directory);
        return Directory.Exists(full)
            ? [.. Directory.GetDirectories(full).Select(d => Path.GetFileName(d)).Order(StringComparer.Ordinal)]
            : [];
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string directory) => Directory.Exists(Full(directory));
}

/// <summary>Pack files held in memory, keyed by '/'-separated path. For tests and tools.</summary>
public sealed class InMemoryPackSource : IPackSource
{
    private readonly SortedDictionary<string, byte[]> files = new(StringComparer.Ordinal);

    /// <summary>Copies every file under a folder on disk.</summary>
    public static InMemoryPackSource FromDirectory(string root)
    {
        InMemoryPackSource source = new();
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            source.files[relative] = File.ReadAllBytes(file);
        }

        return source;
    }

    /// <summary>Adds or replaces a file.</summary>
    public InMemoryPackSource With(string path, string text)
    {
        files[path] = Encoding.UTF8.GetBytes(text);
        return this;
    }

    /// <summary>Adds or replaces a file.</summary>
    public InMemoryPackSource With(string path, byte[] bytes)
    {
        files[path] = bytes;
        return this;
    }

    /// <summary>Removes a file.</summary>
    public InMemoryPackSource Without(string path)
    {
        files.Remove(path);
        return this;
    }

    /// <summary>A file's text, for tests that edit a fixture.</summary>
    public string Text(string path) => Encoding.UTF8.GetString(files[path]);

    /// <inheritdoc/>
    public long? FileLength(string path) => files.TryGetValue(path, out byte[]? bytes) ? bytes.Length : null;

    /// <inheritdoc/>
    public byte[] ReadFile(string path) => files[path];

    /// <inheritdoc/>
    public IReadOnlyList<string> ListFiles(string directory)
    {
        string prefix = directory.Length == 0 ? string.Empty : directory + "/";
        return [.. files.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && !k[prefix.Length..].Contains('/'))
            .Select(k => k[prefix.Length..])];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ListDirectories(string directory)
    {
        string prefix = directory.Length == 0 ? string.Empty : directory + "/";
        return [.. files.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && k[prefix.Length..].Contains('/'))
            .Select(k => k[prefix.Length..].Split('/')[0])
            .Distinct(StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string directory)
        => directory.Length == 0 ? files.Count > 0 : files.Keys.Any(k => k.StartsWith(directory + "/", StringComparison.Ordinal));
}

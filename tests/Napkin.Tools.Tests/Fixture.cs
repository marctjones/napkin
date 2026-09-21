namespace Napkin.Tools.Tests;

/// <summary>
/// The captured toolchain output the parsers are tested against, plus a scratch directory for
/// the tests that write files.
/// </summary>
internal static class Fixture
{
    public static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static string Text(string name) => File.ReadAllText(Path(name));

    /// <summary>A directory that deletes itself at the end of the test.</summary>
    public static Scratch NewDirectory() => new();

    internal sealed class Scratch : IDisposable
    {
        public Scratch()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "napkin-tools-tests",
                Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string File(string relative)
        {
            var full = System.IO.Path.Combine(Path, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            return full;
        }

        public string Write(string relative, string content)
        {
            var full = File(relative);
            System.IO.File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A locked file on a scratch directory is not worth failing a test over.
            }
        }
    }
}

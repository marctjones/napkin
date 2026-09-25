using System.Text.RegularExpressions;
using Napkin.App.GuiTests.Harness;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// CVS-005's other half: no UI code path constructs a <see cref="Sketch"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every other test in this suite shows that an edit <em>does</em> go through the updater. This
/// one shows there is no way round it, by reading the application's own source: nothing under
/// <c>src/Napkin.App</c> or <c>src/Napkin.Modules.Editing</c> (the editing model it drives, #166) may build a sketch or write an entity or a relationship into one, except
/// the blank sheet <em>File &#x2192; New</em> starts from, which is the empty sketch and a layer
/// to draw on.
/// </para>
/// <para>
/// A source scan rather than a reflection check, because what is being ruled out is a line of
/// code somebody might write, and the failure message has to name the file it is in.
/// </para>
/// </remarks>
public class NoSketchConstructionTests
{
    /// <summary>
    /// The ways a sketch can be built or written to. <c>Sketch.Empty</c> is not one of them: it is
    /// a value, and it is what a blank sheet starts from.
    /// </summary>
    static readonly string[] Forbidden =
    [
        "new Sketch(",
        ".WithEntity(",
        ".WithRelationship(",
        ".WithoutEntity(",
        ".WithoutRelationship(",
        ".WithLayer(",
    ];

    /// <summary>
    /// The one file allowed to build one: <em>File &#x2192; New</em>'s blank sheet. There is no
    /// <c>AddLayer</c> request in <c>Core.Geometry</c>, so a layer a new part can go on can only
    /// come from the sketch a design source produced.
    /// </summary>
    const string TheBlankSheet = "NewSheet.cs";

    [Fact]
    [Trait("Feature", "CVS-005")]
    public void No_code_in_the_application_builds_a_sketch()
    {
        string[] sources =
        [
            Path.Combine(RepositoryLayout.RepositoryRoot, "src", "Napkin.App"),
            Path.Combine(RepositoryLayout.RepositoryRoot, "src", "Napkin.Modules.Editing"),
        ];
        foreach (string source in sources)
        {
            Assert.True(Directory.Exists(source), $"{source} is not there.");
        }

        List<string> offences = [];
        foreach (string file in sources.SelectMany(source => Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)))
        {
            if (Path.GetFileName(file) == TheBlankSheet || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (IsCommentOrDoc(line))
                {
                    continue;
                }

                foreach (string forbidden in Forbidden)
                {
                    if (line.Contains(forbidden, StringComparison.Ordinal))
                    {
                        offences.Add(
                            $"{Path.GetRelativePath(RepositoryLayout.RepositoryRoot, file)}:{i + 1}: {line.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            offences.Count == 0,
            "The application must change geometry only through IGeometryUpdater (CVS-005), but:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offences));
    }

    [Fact]
    public void The_scan_would_notice_a_line_that_broke_the_rule()
    {
        // A guard on the guard: a scan that matched nothing would pass for ever.
        string[] lines =
        [
            "        Sketch worse = sketch.WithEntity(box with { Width = wider });",
            "        // sketch.WithEntity(...) would be the wrong way to do this",
        ];

        List<string> caught =
        [
            .. lines.Where(line => !IsCommentOrDoc(line))
                .Where(line => Forbidden.Any(forbidden => line.Contains(forbidden, StringComparison.Ordinal))),
        ];

        Assert.Single(caught);
    }

    static bool IsCommentOrDoc(string line)
    {
        string trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
               || trimmed.StartsWith("///", StringComparison.Ordinal)
               || trimmed.StartsWith("*", StringComparison.Ordinal)
               || trimmed.StartsWith("/*", StringComparison.Ordinal)
               || Regex.IsMatch(trimmed, @"^<.*>");
    }
}

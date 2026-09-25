using System.Collections.Immutable;

namespace Napkin.App.Designs;

/// <summary>
/// The sample designs that ship beside the executable, as things the Samples menu can open.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The files in <c>samples/</c> are the samples.</strong> They are copied into the build
/// output (and into a <c>dotnet publish</c> layout) by a <c>Content</c> item in
/// <c>Napkin.App.csproj</c>, and they are opened through <see cref="FileDesignSource"/> and
/// <see cref="Napkin.Core.Project.SceneReader"/> like any other file. There is deliberately no
/// second, in-code copy of a sample: the viewer used to build them through the geometry API while
/// the reader and the files were being written in parallel (#6, #37), and two hand-computed copies
/// of the same drawing is one more than can be kept honest.
/// </para>
/// <para>
/// The only thing here that is not in a file is a sample's <em>title and one-line blurb</em>, and
/// that is because the M1 scene format stores no name — for an entity or for the drawing. Naming is
/// a format decision the cut list (#8) will have to make; until it does, the menu's words live in
/// <see cref="Catalogue"/> and nothing reads them back.
/// </para>
/// <para>
/// Nothing here throws when the directory is missing. A build that somehow shipped without its
/// samples gives an empty menu and an empty sheet, which the window can say plainly; it does not
/// fail to start.
/// </para>
/// </remarks>
public static class SampleFiles
{
    /// <summary>The pattern the samples are found by, and the one the open dialog filters on.</summary>
    public const string ScenePattern = "*.scene.json";

    /// <summary>
    /// What each shipped sample is called and what it is, keyed by file name, in menu order.
    /// </summary>
    /// <remarks>
    /// A file found in <see cref="SampleDirectory"/> that is not listed here is still offered,
    /// under a title made from its file name: shipping a sample is dropping a file in, not editing
    /// this list as well.
    /// </remarks>
    /// <devdoc>
    /// Declared before <see cref="All"/> on purpose: static initialisers run in source order, and
    /// <see cref="Discover"/> reads this one.
    /// </devdoc>
    public static ImmutableArray<SampleDescription> Catalogue { get; } =
    [
        new(
            "coffee-table.scene.json",
            "Coffee table",
            "A 4'-0\" × 2'-0\" top on four 2½\" legs, with aprons."),
        new(
            "rounded-corner-table.scene.json",
            "Rounded-corner table",
            "The same table with its top's four corners rounded to a 1\" radius."),
        new(
            "wall-with-window.scene.json",
            "Wall with window",
            "12'-0\" of 5½\" wall with a 3'-0\" opening centred in it."),
        new(
            "bookcase.scene.json",
            "Bookcase",
            "A 30\" × 36\" plywood carcass: sides, caps, two shelves and a 1/4\" back — sheet goods and pairs."),
        new(
            "bench.scene.json",
            "Bench",
            "A 3'-6\" bench on four legs, with one box standing for two rails — quantity, not box count."),
        new(
            "lying-beam.scene.json",
            "Lying beam",
            "One 36\" beam placed four ways — along X, along Y, upright and on its edge: same piece, same cut."),
        new(
            "chain-of-five.scene.json",
            "Chain of five",
            "Five slats each flush to the one before — resize any and the rest follow."),
        new(
            "fraction-stress.scene.json",
            "Fraction stress",
            "Parts of 1/16\", 3/32\", 5/64\" and about 1/3\" — sizes off the 1/16\" grid read as approximate."),
        new(
            "scale-extremes.scene.json",
            "Scale extremes",
            "A 40'-0\" plate and wall with a 1/32\" shim between — precision and zoom."),
        new(
            "framing-16-oc.scene.json",
            "Framing at 16\" o.c.",
            "Twenty-five studs and two plates — quantity counting."),
        new(
            "l-bracket.scene.json",
            "L-bracket",
            "An L-bracket with a distinct feature on every side — for telling the standard views apart."),
        new(
            "overlap.scene.json",
            "Overlap",
            "Two boards in the same place and a peg through both — napkin does not detect overlap."),
        new(
            "picture-frame.scene.json",
            "Picture frame",
            "An 8\" × 10\" opening in 1½\" moulding, mitred at all four corners — cut to the long point."),
        new(
            "stocked-bench.scene.json",
            "Stocked bench",
            "A 4'-0\" bench of 2x4, 1x4 and 3/4 plywood — every part names its stock, for the shopping list."),
        new(
            "diy-coffee-table-drawers.scene.json",
            "DIY coffee table with drawers",
            "A 42\" × 22\" table with two drawers — 34 joints: pocket screws, rabbets, grooves and tabletop clips."),
        new(
            "window-in-existing-wall.scene.json",
            "Window in an existing wall",
            "A new 3'-0\" window in an existing 12'-0\" exterior wall — what is new material and which studs come out."),
        new(
            "basement-room.scene.json",
            "Basement room",
            "A 14'-0\" × 12'-0\" room inside four new walls, a door and a window — drywall, insulation, paint, flooring and baseboard by area."),
    ];

    private static readonly string[] SceneSuffixes = [".scene.json", ".json"];

    /// <summary>The samples, in menu order, that were actually found beside the executable.</summary>
    public static IReadOnlyList<FileDesignSource> All { get; } = Discover();

    /// <summary>Where the samples live: <c>samples/</c> beside the application.</summary>
    public static string SampleDirectory => Path.Combine(AppContext.BaseDirectory, "samples");

    private static IReadOnlyList<FileDesignSource> Discover()
    {
        string directory = SampleDirectory;
        string[] found;
        try
        {
            found = Directory.Exists(directory) ? Directory.GetFiles(directory, ScenePattern) : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        HashSet<string> names = new(
            found.Select(path => Path.GetFileName(path)),
            StringComparer.OrdinalIgnoreCase);

        List<FileDesignSource> samples = [];

        // Catalogued samples first, in the order the menu wants them; then anything else found,
        // alphabetically, so the menu is stable whatever order the file system lists files in.
        foreach (SampleDescription description in Catalogue)
        {
            if (names.Remove(description.FileName))
            {
                samples.Add(new FileDesignSource(
                    Path.Combine(directory, description.FileName),
                    description.Title,
                    description.Blurb));
            }
        }

        foreach (string name in names.OrderBy(name => name, StringComparer.Ordinal))
        {
            samples.Add(new FileDesignSource(
                Path.Combine(directory, name),
                TitleFrom(name),
                "A sample design shipped with this build."));
        }

        return samples;
    }

    /// <summary>
    /// A title for an uncatalogued sample: <c>garden-gate.scene.json</c> becomes "Garden gate".
    /// </summary>
    public static string TitleFrom(string fileName)
    {
        string stem = fileName;
        foreach (string suffix in SceneSuffixes)
        {
            if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                stem = stem[..^suffix.Length];
                break;
            }
        }

        stem = stem.Replace('-', ' ').Replace('_', ' ').Trim();
        return stem.Length == 0
            ? fileName
            : char.ToUpperInvariant(stem[0]) + stem[1..];
    }
}

/// <summary>What a shipped sample is called in the menu, and the line the status bar shows.</summary>
/// <param name="FileName">The file's name in <see cref="SampleFiles.Directory"/>.</param>
/// <param name="Title">What the menu calls it.</param>
/// <param name="Blurb">One line about what it is.</param>
public sealed record SampleDescription(string FileName, string Title, string Blurb);

using System.IO.Compression;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// The container: what a saved project is, what it promises about its bytes, and every shape of
/// file it refuses.
/// </summary>
/// <remarks>
/// <strong>PRJ-006 is deliberately not claimed here.</strong> Its acceptance is "a saved project
/// opens with a standard zip tool and contains manifest.json, scene.json, <em>a thumbnail and an
/// assets directory</em>". The first two are tested below; a thumbnail means rendering, which
/// lives in the app, and neither it nor assets is written by this build. Tagging the trait anyway
/// would make the scorecard say something is proven that is not, which is the one thing the
/// scorecard exists to prevent (docs/testing/scorecard.md). It stays a planned stub until the
/// container grows those entries, which is a container-version bump.
/// </remarks>
public sealed class ProjectFileTests
{
    [Theory]
    [MemberData(nameof(SceneWriterTests.Seeds), MemberType = typeof(SceneWriterTests))]
    [Trait("Feature", "PRJ-007")]
    public void Load_of_save_is_the_project_it_started_from(int seed)
    {
        Sketch sketch = SketchGenerator.Generate(seed);

        byte[] saved = ProjectFile.SaveToBytes(sketch);
        LoadedProject opened = Open(saved, seed);

        Assert.Equal(sketch, opened.Sketch);
        Assert.Equal(FormatStamp.Current, opened.Stamp);
        Assert.Equal(ProjectManifest.CurrentContainerVersion, opened.Manifest.ContainerVersion);

        // And the bytes are stable across the round trip, so that opening and saving a project
        // without touching it does not churn the file.
        Assert.Equal(saved, ProjectFile.SaveToBytes(opened.Sketch, opened.Manifest.AdoptedCode));
    }

    [Theory]
    [MemberData(nameof(SceneWriterTests.Seeds), MemberType = typeof(SceneWriterTests))]
    public void Two_saves_of_the_same_project_are_the_same_bytes(int seed)
    {
        // This catches anything that varies between two calls — but note what it cannot catch: two
        // saves a moment apart would share a wall-clock timestamp too, so a writer that stamped
        // the clock instead of the epoch would still pass here. The assertion that the entries
        // carry ProjectFile.Timestamp, below, is what actually holds that down.
        Sketch sketch = SketchGenerator.Generate(seed);

        Assert.Equal(ProjectFile.SaveToBytes(sketch), ProjectFile.SaveToBytes(sketch));
    }

    [Fact]
    public void A_saved_project_is_a_zip_a_standard_tool_opens()
    {
        byte[] saved = ProjectFile.SaveToBytes(Containers.OneBox);

        using MemoryStream buffer = new(saved, writable: false);
        using ZipArchive archive = new(buffer, ZipArchiveMode.Read);

        Assert.Equal(
            ["manifest.json", "scene.json"],
            archive.Entries.Select(entry => entry.FullName));

        // The scene inside the container is the same document SceneWriter produces on its own:
        // the container wraps the scene body, it does not change it.
        using Stream scene = archive.GetEntry("scene.json")!.Open();
        using MemoryStream copy = new();
        scene.CopyTo(copy);
        Assert.Equal(SceneWriter.WriteToBytes(Containers.OneBox), copy.ToArray());

        // Nothing in the container is dated, because a save has to be a function of the drawing.
        // A zip's timestamps are DOS times, which carry no time zone: what is written is the
        // fixed wall-clock 1980-01-01 00:00 on every machine, and it is only the reading back
        // that puts the local offset on it.
        Assert.All(
            archive.Entries,
            entry => Assert.Equal(ProjectFile.Timestamp.DateTime, entry.LastWriteTime.DateTime));
    }

    [Fact]
    [Trait("Feature", "PRJ-005")]
    public void The_manifest_records_what_the_file_means()
    {
        LoadedProject opened = Open(ProjectFile.SaveToBytes(Containers.OneBox), seed: 0);

        Assert.Equal(ProjectManifest.CurrentContainerVersion, opened.Manifest.ContainerVersion);
        Assert.Equal(ProjectManifest.BuildVersion, opened.Manifest.AppVersion);
        Assert.NotEmpty(opened.Manifest.AppVersion);
        Assert.Null(opened.Manifest.AdoptedCode);

        // The storage units are the scene stamp's, not the manifest's: two stamps, two layers.
        Assert.Equal(FormatStamp.InchGrid, opened.Stamp.LengthUnit);
        Assert.Equal(FormatStamp.Arcsecond, opened.Stamp.AngleUnit);
        Assert.Equal(FormatStamp.CurrentVersion, opened.Stamp.FormatVersion);
    }

    [Fact]
    [Trait("Feature", "PRJ-005")]
    public void The_app_version_is_the_build_that_wrote_the_file()
    {
        // Not a constant in a test: whatever the build says about itself is what the file has to
        // carry, so that a bug report names the build.
        Assert.Contains(
            $"\"appVersion\": \"{ProjectManifest.BuildVersion}\"",
            ManifestOf(ProjectFile.SaveToBytes(Containers.OneBox)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("us-ct-2026", 1)]
    [InlineData("us-ma-780cmr-10", 0)]
    [InlineData("us-pa-ucc", 2147483647)]
    [Trait("Feature", "PRJ-005")]
    public void The_adopted_code_survives_a_round_trip_without_being_interpreted(string pack, int revision)
    {
        AdoptedCode code = new(pack, revision);

        LoadedProject opened = Open(ProjectFile.SaveToBytes(Containers.OneBox, code), seed: 0);

        // Nothing in this build knows what a pack id means, and nothing here asks it to: the
        // project says which code it was drawn against and that is carried through untouched.
        Assert.Equal(code, opened.Manifest.AdoptedCode);
    }

    [Fact]
    [Trait("Feature", "PRJ-005")]
    public void A_project_with_no_adopted_code_says_so_rather_than_leaving_the_field_out()
    {
        Assert.Contains(
            "\"adoptedCode\": null",
            ManifestOf(ProjectFile.SaveToBytes(Containers.OneBox)),
            StringComparison.Ordinal);
    }

    public static TheoryData<string, string, string, LoadProblemKind> BadAdoptedCodes => new()
    {
        { "not an object", "\"adoptedCode\": null", "\"adoptedCode\": \"us-ct-2026\"", LoadProblemKind.Malformed },
        { "no pack", "\"adoptedCode\": null", "\"adoptedCode\": { \"revision\": 1 }", LoadProblemKind.MissingField },
        { "no revision", "\"adoptedCode\": null", "\"adoptedCode\": { \"pack\": \"us-ct-2026\" }", LoadProblemKind.MissingField },
        { "a fractional revision", "\"adoptedCode\": null", "\"adoptedCode\": { \"pack\": \"us-ct-2026\", \"revision\": 1.5 }", LoadProblemKind.NotAnInteger },
        { "a negative revision", "\"adoptedCode\": null", "\"adoptedCode\": { \"pack\": \"us-ct-2026\", \"revision\": -1 }", LoadProblemKind.InvalidValue },
        { "a pack named nothing", "\"adoptedCode\": null", "\"adoptedCode\": { \"pack\": \"\", \"revision\": 1 }", LoadProblemKind.InvalidValue },
        { "a field nobody defined", "\"adoptedCode\": null", "\"adoptedCode\": { \"pack\": \"us-ct-2026\", \"revision\": 1, \"year\": 2026 }", LoadProblemKind.UnknownField },
        { "a pack that is a number", "\"adoptedCode\": null", "\"adoptedCode\": { \"pack\": 2026, \"revision\": 1 }", LoadProblemKind.Malformed },
    };

    [Theory]
    [MemberData(nameof(BadAdoptedCodes))]
    [Trait("Feature", "PRJ-005")]
    public void A_malformed_adopted_code_is_refused(string why, string original, string replacement, LoadProblemKind kind)
    {
        Assert.NotEmpty(why);

        Containers.Reject(
            Containers.With(manifest: Containers.GoodManifest.Swap(original, replacement)),
            kind,
            "adoptedCode");
    }

    // ---------------------------------------------------------------------------------------
    // The two stamps
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    [Trait("Feature", "PRJ-004")]
    public void A_container_from_another_version_is_refused_naming_both_versions(int version)
    {
        // Older or newer, it makes no difference: napkin is a beta and writes no migration code.
        LoadProblem problem = Containers.Reject(
            Containers.With(
                manifest: Containers.GoodManifest.Swap(
                    $"\"containerVersion\": {ProjectManifest.CurrentContainerVersion}",
                    $"\"containerVersion\": {version}")),
            LoadProblemKind.UnsupportedContainerVersion,
            version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ProjectManifest.CurrentContainerVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Contains("migration", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(11)]
    [Trait("Feature", "PRJ-004")]
    public void A_scene_from_another_format_version_is_refused_naming_both_versions(int version)
    {
        // The scene body keeps its own stamp inside the container, and it is judged too.
        string scene = Encoding.UTF8.GetString(Containers.GoodScene)
            .Swap($"\"formatVersion\": {FormatStamp.CurrentVersion}", $"\"formatVersion\": {version}");

        Containers.Reject(
            Containers.With(scene: Containers.Utf8(scene)),
            LoadProblemKind.UnsupportedFormatVersion,
            version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FormatStamp.CurrentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_problem_inside_the_scene_says_it_is_inside_the_scene()
    {
        string scene = Encoding.UTF8.GetString(Containers.GoodScene).Swap("\"width\": 30720", "\"width\": 30720.5");

        LoadProblem problem = Containers.Reject(
            Containers.With(scene: Containers.Utf8(scene)), LoadProblemKind.NotAnInteger, "width");

        Assert.StartsWith("scene.json/", problem.Location, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // What a container is not
    // ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_container_with_no_manifest_is_refused()
        => Containers.Reject(
            Containers.Zip(("scene.json", Containers.GoodScene)),
            LoadProblemKind.MissingEntry,
            "manifest.json");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_container_with_no_scene_is_refused()
        => Containers.Reject(
            Containers.Zip(("manifest.json", Containers.Utf8(Containers.GoodManifest))),
            LoadProblemKind.MissingEntry,
            "scene.json");

    [Theory]
    [InlineData("thumbnail.png")]
    [InlineData("assets/survey.pdf")]
    [InlineData("notes.txt")]
    [InlineData("Manifest.json")]
    [Trait("Feature", "PRJ-002")]
    public void An_entry_this_format_does_not_define_is_refused_naming_it(string name)
    {
        // Including the two DESIGN.md §6.4 reserves for later: they are not written by this build,
        // so a container holding one did not come from this build and is not opened as if it had.
        Containers.Reject(
            Containers.Zip(
                ("manifest.json", Containers.Utf8(Containers.GoodManifest)),
                ("scene.json", Containers.GoodScene),
                (name, Containers.Utf8("anything"))),
            LoadProblemKind.UnknownEntry,
            name);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void The_same_entry_twice_is_refused_rather_than_guessed_between()
        => Containers.Reject(
            Containers.Zip(
                ("manifest.json", Containers.Utf8(Containers.GoodManifest)),
                ("scene.json", Containers.GoodScene),
                ("scene.json", Containers.GoodScene)),
            LoadProblemKind.DuplicateEntry,
            "scene.json");

    [Theory]
    [InlineData("../escaped.json")]
    [InlineData("../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("..\\escaped.json")]
    [InlineData("subdir/../../escaped.json")]
    [InlineData("C:\\Windows\\System32\\evil.dll")]
    [InlineData("scene.json/")]
    [InlineData("./scene.json")]
    [Trait("Feature", "PRJ-002")]
    public void An_entry_whose_name_could_leave_the_container_is_refused(string name)
    {
        // Nothing here is ever written to disk, so none of these could overwrite anything today.
        // They are refused because a container carrying one is not a container napkin wrote, and
        // because the day something does extract an entry, the check is already in place.
        Containers.Reject(
            Containers.Zip(
                ("manifest.json", Containers.Utf8(Containers.GoodManifest)),
                ("scene.json", Containers.GoodScene),
                (name, Containers.Utf8("anything"))),
            LoadProblemKind.UnsafeEntryName);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_container_with_more_entries_than_the_format_has_is_refused_before_any_are_read()
    {
        (string, byte[])[] entries =
        [
            .. Enumerable.Range(0, ContainerLimits.MaxEntries + 1)
                .Select(index => ($"entry-{index}.json", Containers.Utf8("{}"))),
        ];

        Containers.Reject(Containers.Zip(entries), LoadProblemKind.TooLarge, "entries");
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void An_entry_that_expands_far_past_its_limit_is_refused()
    {
        // A zip bomb's shape: a few hundred bytes in the container, megabytes out of it. The
        // limit is judged from the entry's stated size and enforced again as the bytes arrive, so
        // a container that lies about the size stops at the limit rather than filling memory.
        byte[] enormous = Containers.Utf8(new string(' ', (int)ContainerLimits.MaxManifestBytes + 1));
        byte[] container = Containers.Zip(
            ("manifest.json", enormous),
            ("scene.json", Containers.Utf8("{}")));

        // The scene is a stand-in: the manifest is judged, and refused, first. A real scene would
        // be most of the container's bytes and hide how little the bomb itself takes.
        Assert.True(
            container.Length < enormous.Length / 100,
            "the test's own bomb did not compress, so it is not testing what it means to.");

        Containers.Reject(container, LoadProblemKind.TooLarge, "manifest.json");
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_project_larger_than_napkin_opens_is_refused_before_it_is_read()
    {
        using Stream enormous = new HugeStream(ContainerLimits.MaxContainerBytes + 1);

        Refused refused = Assert.IsType<Refused>(ProjectFile.Load(enormous));

        Assert.Equal(LoadProblemKind.TooLarge, Assert.Single(refused.Problems).Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ \"formatVersion\": 1 }")]
    [InlineData("not a file at all")]
    [Trait("Feature", "PRJ-002")]
    public void Something_that_is_not_a_zip_is_refused_and_the_message_points_at_the_scene_reader(string text)
    {
        // The plain scene reader is not gone and a napkin file is not sniffed at: a caller that
        // hands a bare scene document to the project loader is told which door to use.
        LoadProblem problem = Containers.Reject(
            Containers.Utf8(text), LoadProblemKind.NotAContainer, "SceneReader");

        Assert.Contains("zip", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_truncated_container_is_refused()
    {
        byte[] whole = ProjectFile.SaveToBytes(Containers.OneBox);

        Containers.Reject(whole[..(whole.Length - 40)], LoadProblemKind.NotAContainer);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_container_whose_compressed_data_is_damaged_is_refused()
    {
        // The damage is in the compressed middle, not in the directory at the end, so this gets
        // as far as decompressing and fails there — the other half of "not a readable zip". The
        // range is a proportion of the file rather than fixed offsets, because where the entries
        // land depends on how well the compressor did, which is the runtime's business.
        byte[] damaged = ProjectFile.SaveToBytes(SketchGenerator.Generate(5));
        for (int i = damaged.Length / 5; i < damaged.Length * 3 / 5; i++)
        {
            damaged[i] ^= 0xFF;
        }

        Refused refused = Assert.IsType<Refused>(Containers.Open(damaged));
        Assert.NotEmpty(refused.Problems);
    }

    // ---------------------------------------------------------------------------------------
    // Saving to a real file, atomically
    // ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void A_project_saved_to_a_path_opens_from_that_path()
    {
        using Workspace workspace = new();
        string path = workspace.Path("table" + ProjectFile.Extension);

        Saved saved = Assert.IsType<Saved>(ProjectFile.Save(path, Containers.OneBox));

        Assert.True(File.Exists(path));
        Assert.Equal(Path.GetFullPath(path), saved.Path);

        LoadedProject opened = Assert.IsType<LoadedProject>(ProjectFile.Load(path));
        Assert.Equal(Containers.OneBox, opened.Sketch);
    }

    [Fact]
    public void A_save_that_dies_part_way_through_leaves_the_previous_project_untouched()
    {
        using Workspace workspace = new();
        string path = workspace.Path("table" + ProjectFile.Extension);

        Assert.IsType<Saved>(ProjectFile.Save(path, Containers.OneBox));
        byte[] before = File.ReadAllBytes(path);

        // The second save is of a different drawing, and it fails after some of the bytes are
        // down. The whole promise of writing to a temporary and moving it is that this cannot
        // damage what is already there.
        Sketch other = SketchGenerator.Generate(11);
        NotSaved refused = Assert.IsType<NotSaved>(
            ProjectFile.Save(path, other, null, temporary => new FailingStream(temporary, after: 64)));

        Assert.Equal(SaveProblemKind.Unwritable, Assert.Single(refused.Problems).Kind);
        Assert.Contains("could not be saved", refused.Summary, StringComparison.Ordinal);

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(Containers.OneBox, Assert.IsType<LoadedProject>(ProjectFile.Load(path)).Sketch);

        // And nothing is left lying beside it.
        Assert.Equal([Path.GetFileName(path)], workspace.Contents());
    }

    [Fact]
    public void A_save_into_a_folder_that_cannot_be_written_fails_without_touching_the_old_file()
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows does not stop a file being created in a directory marked read-only, so
            // there is nothing to test here; the injected-failure test above covers the promise
            // on every platform.
            return;
        }

        using Workspace workspace = new();
        string path = workspace.Path("table" + ProjectFile.Extension);
        Assert.IsType<Saved>(ProjectFile.Save(path, Containers.OneBox));
        byte[] before = File.ReadAllBytes(path);

        File.SetUnixFileMode(
            workspace.Directory,
            UnixFileMode.UserRead | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        try
        {
            if (CanStillWrite(workspace.Directory))
            {
                // Running as a user the permissions do not apply to — root in a container, say.
                return;
            }

            NotSaved refused = Assert.IsType<NotSaved>(ProjectFile.Save(path, SketchGenerator.Generate(12)));

            Assert.Equal(SaveProblemKind.Unwritable, Assert.Single(refused.Problems).Kind);
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally
        {
            File.SetUnixFileMode(
                workspace.Directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void A_drawing_napkin_could_not_open_again_is_not_saved_at_all()
    {
        using Workspace workspace = new();
        string path = workspace.Path("table" + ProjectFile.Extension);

        // A segment whose ends are not in the sketch: written out, this would be a file the
        // reader refuses, so the save refuses first and the drawing stays where it can be fixed.
        Sketch broken = Sketch.Empty.WithEntity(new Segment(
            EntityId.New(), LayerId.Default, EntityId.New(), EntityId.New()));

        NotSaved refused = Assert.IsType<NotSaved>(ProjectFile.Save(path, broken));

        Assert.All(refused.Problems, problem => Assert.Equal(SaveProblemKind.InvalidSketch, problem.Kind));
        Assert.False(File.Exists(path));
        Assert.Empty(workspace.Contents());
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void Saving_over_an_existing_project_replaces_it()
    {
        using Workspace workspace = new();
        string path = workspace.Path("table" + ProjectFile.Extension);

        Assert.IsType<Saved>(ProjectFile.Save(path, Containers.OneBox));
        Sketch other = SketchGenerator.Generate(13);
        Assert.IsType<Saved>(ProjectFile.Save(path, other));

        Assert.Equal(other, Assert.IsType<LoadedProject>(
            ProjectFile.Load(path, new SceneWriterTests.EveryKindUpdater())).Sketch);
        Assert.Equal([Path.GetFileName(path)], workspace.Contents());
    }

    [Fact]
    [Trait("Feature", "PRJ-001")]
    public void The_plain_scene_reader_still_opens_a_hand_written_sample()
    {
        // The container did not replace the plain file. The samples, and anything a person writes
        // in a text editor, keep working exactly as they did in M1.
        Assert.IsType<Loaded>(SceneReader.ReadFile(
            Path.Combine(SampleFixtureTests.SampleDirectory, "wall-with-window.scene.json")));
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void A_sample_saved_as_a_project_comes_back_the_same_drawing()
    {
        Sketch sample = SceneWriterTests.ReadSample("coffee-table");

        Assert.Equal(sample, Open(ProjectFile.SaveToBytes(sample), seed: 0).Sketch);
    }

    /// <summary>
    /// Review §2.2 (#175): the shell (<c>MainWindow</c>, <c>FileDesignSource</c>) narrows its
    /// catch-almost-everything filters to this predicate, so that a real programming error is not
    /// reported as a refused file. Prove the predicate itself draws that line correctly: true for
    /// every I/O or container-format exception this type catches, false for a programming error.
    /// </summary>
    public static TheoryData<Func<Exception>, bool> FileExceptionCases => new()
    {
        { () => new IOException(), true },
        { () => new UnauthorizedAccessException(), true },
        { () => new NotSupportedException(), true },
        { () => new System.Security.SecurityException(), true },
        { () => new ObjectDisposedException(nameof(ProjectFileTests)), true },
        { () => new InvalidDataException(), true },
        { () => new NullReferenceException(), false },
        { () => new InvalidOperationException(), false },
        { () => new IndexOutOfRangeException(), false },
        { () => new ArgumentException(), false },
    };

    [Theory]
    [MemberData(nameof(FileExceptionCases))]
    public void IsFileException_narrows_to_IO_and_format_failures_only(Func<Exception> makeException, bool expected)
    {
        Assert.Equal(expected, ProjectFile.IsFileException(makeException()));
    }

    // ---------------------------------------------------------------------------------------
    // Plumbing
    // ---------------------------------------------------------------------------------------

    private static LoadedProject Open(byte[] saved, int seed)
    {
        LoadResult result = Containers.Open(saved);

        if (result is Refused refused)
        {
            Assert.Fail(
                $"seed {SketchGenerator.Describe(seed)}: the saved project was refused."
                + Environment.NewLine + refused.Summary);
        }

        return Assert.IsType<LoadedProject>(result);
    }

    private static string ManifestOf(byte[] saved)
    {
        using MemoryStream buffer = new(saved, writable: false);
        using ZipArchive archive = new(buffer, ZipArchiveMode.Read);
        using Stream manifest = archive.GetEntry("manifest.json")!.Open();
        using StreamReader reader = new(manifest, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static bool CanStillWrite(string directory)
    {
        string probe = Path.Combine(directory, "probe.tmp");
        try
        {
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A directory of its own for one test, removed afterwards whatever happens.</summary>
    private sealed class Workspace : IDisposable
    {
        internal Workspace()
        {
            Directory = System.IO.Directory.CreateTempSubdirectory("napkin-project-").FullName;
        }

        internal string Directory { get; }

        internal string Path(string name) => System.IO.Path.Combine(Directory, name);

        /// <summary>Everything in the directory, so that a test can say a temporary was cleaned up.</summary>
        internal string[] Contents()
            => [.. System.IO.Directory.EnumerateFileSystemEntries(Directory)
                .Select(System.IO.Path.GetFileName)
                .OfType<string>()
                .Order(StringComparer.Ordinal)];

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A test that could not clean up after itself is not a failing test.
            }
        }
    }

    /// <summary>A file that accepts a few bytes and then stops, the way a full disk does.</summary>
    private sealed class FailingStream(string path, int after) : Stream
    {
        private readonly FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        private long written;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => written;

        public override long Position
        {
            get => written;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int room = (int)Math.Max(0, Math.Min(count, after - written));
            if (room > 0)
            {
                file.Write(buffer, offset, room);
                written += room;
            }

            if (room < count)
            {
                throw new IOException("There is no space left on the device.");
            }
        }

        public override void Flush() => file.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                file.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A stream that claims to be enormous without allocating anything, so that the size check can
    /// be tested without building a 32 MB file.
    /// </summary>
    private sealed class HugeStream(long length) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("Nothing should read a stream this size.");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }
    }
}

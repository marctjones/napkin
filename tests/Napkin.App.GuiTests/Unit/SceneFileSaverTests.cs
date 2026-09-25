using Napkin.App.Designs;
using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// What <em>File &#x2192; Save</em> writes: a scene file the reader opens again, or a refusal that
/// leaves whatever was there before.
/// </summary>
public class SceneFileSaverTests
{
    [Fact]
    public void A_saved_sketch_reads_back_as_the_same_sketch()
    {
        Sketch sketch = EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12), EditingBuilder.At(30, 0, 6, 6)).Sketch;
        string path = BadScenes.MissingFile($"saved-{Guid.NewGuid():N}.scene.json");

        SceneSaved saved = Assert.IsType<SceneSaved>(SceneFileSaver.Save(path, sketch));

        Assert.Equal(Path.GetFullPath(path), saved.Path);
        Loaded loaded = Assert.IsType<Loaded>(SceneReader.ReadFile(path));
        Assert.Equal(sketch, loaded.Sketch);
    }

    [Fact]
    public void Saving_over_a_file_replaces_it_and_leaves_no_temporary_behind()
    {
        string path = BadScenes.Write($"over-{Guid.NewGuid():N}.scene.json", BadScenes.Good);
        Sketch sketch = EditingBuilder.Design(EditingBuilder.At(0, 0, 10, 10)).Sketch;

        Assert.IsType<SceneSaved>(SceneFileSaver.Save(path, sketch));

        Assert.Equal(sketch, Assert.IsType<Loaded>(SceneReader.ReadFile(path)).Sketch);
        Assert.DoesNotContain(
            Directory.GetFiles(Path.GetDirectoryName(path)!),
            file => file.EndsWith(".tmp", StringComparison.Ordinal)
                    && file.Contains(Path.GetFileName(path), StringComparison.Ordinal));
    }

    [Fact]
    public void A_folder_that_is_not_there_is_refused_in_words_and_nothing_throws()
    {
        string path = Path.Combine(BadScenes.MissingFile($"no-such-folder-{Guid.NewGuid():N}"), "design.scene.json");

        SceneNotSaved refused = Assert.IsType<SceneNotSaved>(
            SceneFileSaver.Save(path, EditingBuilder.Design(EditingBuilder.At(0, 0, 10, 10)).Sketch));

        Assert.Contains("design.scene.json could not be saved", Assert.Single(refused.Problems), StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void A_target_that_cannot_be_replaced_keeps_what_was_there_and_cleans_up()
    {
        // A directory where the file should go: the write to the temporary works and the move
        // over the target fails, which is the order a full disk or a locked file fails in too.
        string path = BadScenes.MissingFile($"a-folder-{Guid.NewGuid():N}.scene.json");
        Directory.CreateDirectory(path);

        SceneSaveResult result = SceneFileSaver.Save(path, EditingBuilder.Design(EditingBuilder.At(0, 0, 10, 10)).Sketch);

        Assert.IsType<SceneNotSaved>(result);
        Assert.True(Directory.Exists(path));
        Assert.DoesNotContain(
            Directory.GetFiles(Path.GetDirectoryName(path)!),
            file => file.Contains(Path.GetFileName(path), StringComparison.Ordinal));
    }

    [Fact]
    public void A_sketch_the_reader_would_refuse_is_not_written()
    {
        Sketch dangling = Sketch.Empty.WithRelationship(new Anchored(RelationshipId.New(), EntityId.New()));
        string path = BadScenes.MissingFile($"dangling-{Guid.NewGuid():N}.scene.json");

        SceneNotSaved refused = Assert.IsType<SceneNotSaved>(SceneFileSaver.Save(path, dangling));

        Assert.All(refused.Problems, problem =>
            Assert.StartsWith("The drawing is not one napkin could open again", problem, StringComparison.Ordinal));
        Assert.False(File.Exists(path));
    }
}

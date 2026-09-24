using Napkin.App.Settings;
using Napkin.App.Viewing;

using Xunit;

namespace Napkin.App.GuiTests.Unit;

public sealed class SettingsStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "napkin-settings-test-" + Guid.NewGuid().ToString("N"));

    string FilePath => Path.Combine(_dir, SettingsStore.FileName);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void A_missing_file_gives_the_defaults_and_no_notice()
    {
        var store = new SettingsStore(FilePath);
        Assert.Equal(new UserSettings(), store.Current);
        Assert.Null(store.Notice);
        Assert.Equal(CameraProjection.Perspective, store.Current.Projection);
        Assert.False(store.Current.ShowRulers);
    }

    [Fact]
    public void A_change_is_written_at_once_and_read_back_by_a_new_store()
    {
        new SettingsStore(FilePath).Update(s => s with { Projection = CameraProjection.Orthographic, ShowRulers = true });

        var again = new SettingsStore(FilePath);
        Assert.Equal(CameraProjection.Orthographic, again.Current.Projection);
        Assert.True(again.Current.ShowRulers);
        Assert.Null(again.Notice);
    }

    [Fact]
    public void A_write_leaves_no_temp_file_and_replaces_an_existing_one()
    {
        var store = new SettingsStore(FilePath);
        store.Update(s => s with { ShowRulers = true });
        store.Update(s => s with { ShowRulers = false });

        Assert.Equal([FilePath], Directory.GetFiles(_dir));
        Assert.False(new SettingsStore(FilePath).Current.ShowRulers);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"Version\":1,\"Projection\":\"Sideways\"}")]
    [InlineData("{\"Version\":99,\"ShowRulers\":true}")]
    public void A_corrupt_or_unknown_version_file_gives_the_defaults_and_a_notice(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, content);

        var store = new SettingsStore(FilePath);

        Assert.Equal(new UserSettings(), store.Current);
        Assert.NotNull(store.Notice);
        Assert.DoesNotContain('\n', store.Notice);
    }

    [Fact]
    public void An_unreadable_location_gives_the_defaults_and_a_failed_write_is_not_fatal()
    {
        // The settings "file" is a directory: reading it fails, and so does writing over it.
        Directory.CreateDirectory(FilePath);

        var store = new SettingsStore(FilePath);
        Assert.NotNull(store.Notice);

        store.Update(s => s with { ShowRulers = true });
        Assert.True(store.Current.ShowRulers);
    }

    [Fact]
    public void A_store_with_no_path_keeps_changes_for_the_run_only()
    {
        var store = new SettingsStore(null);
        store.Update(s => s with { ShowRulers = true });
        Assert.True(store.Current.ShowRulers);
        Assert.Null(store.Notice);
    }

    [Fact]
    public void The_config_directory_follows_each_platforms_convention()
    {
        string? None(string _) => null;
        string P(params string[] parts) => Path.Combine(parts);

        Assert.Equal(P(@"C:\Roam", "napkin"), SettingsStore.ConfigDirectory(SettingsStore.Platform.Windows, k => k == "APPDATA" ? @"C:\Roam" : null, "/h"));
        Assert.Equal(P("/h", "AppData", "Roaming", "napkin"), SettingsStore.ConfigDirectory(SettingsStore.Platform.Windows, None, "/h"));
        Assert.Equal(P("/h", "Library", "Application Support", "napkin"), SettingsStore.ConfigDirectory(SettingsStore.Platform.MacOS, None, "/h"));
        Assert.Equal(P("/x", "napkin"), SettingsStore.ConfigDirectory(SettingsStore.Platform.Other, k => k == "XDG_CONFIG_HOME" ? "/x" : null, "/h"));
        Assert.Equal(P("/h", ".config", "napkin"), SettingsStore.ConfigDirectory(SettingsStore.Platform.Other, None, "/h"));
    }
}

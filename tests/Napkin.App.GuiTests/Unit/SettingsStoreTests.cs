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
    public void A_fresh_store_draws_on_the_napkin_in_carpenters_pencil()
    {
        var store = new SettingsStore(FilePath);
        Assert.Equal(Napkin.App.Viewing.SketchPaper.Napkin, store.Current.SketchPaper);
        Assert.Equal(Napkin.App.Viewing.SketchLine.Carpenter, store.Current.SketchLine);
    }

    [Fact]
    public void The_clean_screen_look_stays_selectable_and_is_remembered()
    {
        new SettingsStore(FilePath).Update(s => s with { SketchPaper = Napkin.App.Viewing.SketchPaper.Screen, SketchLine = Napkin.App.Viewing.SketchLine.Clean });

        var again = new SettingsStore(FilePath);
        Assert.Equal(Napkin.App.Viewing.SketchPaper.Screen, again.Current.SketchPaper);
        Assert.Equal(Napkin.App.Viewing.SketchLine.Clean, again.Current.SketchLine);
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

    [Theory]
    [InlineData(OpenDesignsIn.Plan, DesignView.Model, DesignView.Plan)]
    [InlineData(OpenDesignsIn.Plan, DesignView.Plan, DesignView.Plan)]
    [InlineData(OpenDesignsIn.Model, DesignView.Plan, DesignView.Model)]
    [InlineData(OpenDesignsIn.Model, DesignView.Model, DesignView.Model)]
    [InlineData(OpenDesignsIn.LastUsed, DesignView.Plan, DesignView.Plan)]
    [InlineData(OpenDesignsIn.LastUsed, DesignView.Model, DesignView.Model)]
    public void A_new_design_opens_in_the_chosen_view_or_the_last_one_used(OpenDesignsIn choice, DesignView last, DesignView expected)
    {
        var settings = new UserSettings { OpenIn = choice, LastView = last };
        Assert.Equal(expected, settings.ViewForNewDesign());
    }

    [Fact]
    public void The_default_is_to_open_designs_in_the_last_used_view_on_the_plan()
    {
        var defaults = new UserSettings();
        Assert.Equal(OpenDesignsIn.LastUsed, defaults.OpenIn);
        Assert.Equal(DesignView.Plan, defaults.ViewForNewDesign());
    }

    [Fact]
    public void The_view_choices_and_the_last_view_are_kept()
    {
        new SettingsStore(FilePath).Update(s => s with { OpenIn = OpenDesignsIn.Model, LastView = DesignView.Model });

        var again = new SettingsStore(FilePath);
        Assert.Equal(OpenDesignsIn.Model, again.Current.OpenIn);
        Assert.Equal(DesignView.Model, again.Current.LastView);
    }
}

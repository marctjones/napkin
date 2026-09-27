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
    [Trait("Feature", "VIEW-009")]
    public void Hidden_edges_are_on_by_default_and_turning_them_off_is_remembered()
    {
        Assert.True(new SettingsStore(FilePath).Current.ShowHiddenEdges);
        new SettingsStore(FilePath).Update(s => s with { ShowHiddenEdges = false });
        Assert.False(new SettingsStore(FilePath).Current.ShowHiddenEdges);
    }

    [Fact]
    public void The_saw_kerf_defaults_to_an_eighth_and_is_kept_exactly_including_zero()
    {
        Assert.Equal(Napkin.Core.Geometry.Length.Inches(0, 1, 8), new SettingsStore(FilePath).Current.SawKerf);

        new SettingsStore(FilePath).Update(s => s with { SawKerf = Napkin.Core.Geometry.Length.Inches(0, 3, 32) });
        Assert.Equal(Napkin.Core.Geometry.Length.Inches(0, 3, 32), new SettingsStore(FilePath).Current.SawKerf);

        new SettingsStore(FilePath).Update(s => s with { SawKerf = Napkin.Core.Geometry.Length.Zero });
        Assert.Equal(Napkin.Core.Geometry.Length.Zero, new SettingsStore(FilePath).Current.SawKerf);
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
    [InlineData("{\"Version\":2,\"Projection\":\"Sideways\"}")]
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
    [InlineData(OpenDesignsIn.Plan, DesignView.Model, DesignView.Top)]
    [InlineData(OpenDesignsIn.Plan, DesignView.Front, DesignView.Top)]
    [InlineData(OpenDesignsIn.Model, DesignView.Top, DesignView.Model)]
    [InlineData(OpenDesignsIn.Model, DesignView.Left, DesignView.Model)]
    [InlineData(OpenDesignsIn.LastUsed, DesignView.Top, DesignView.Top)]
    [InlineData(OpenDesignsIn.LastUsed, DesignView.Front, DesignView.Front)]
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
        Assert.Equal(DesignView.Top, defaults.ViewForNewDesign());
    }

    [Theory]
    [Trait("Feature", "VIEW-004")]
    [InlineData(DesignView.Top)]
    [InlineData(DesignView.Bottom)]
    [InlineData(DesignView.Front)]
    [InlineData(DesignView.Back)]
    [InlineData(DesignView.Left)]
    [InlineData(DesignView.Right)]
    [InlineData(DesignView.Model)]
    public void Every_view_is_kept_as_the_last_view(DesignView view)
    {
        new SettingsStore(FilePath).Update(s => s with { OpenIn = OpenDesignsIn.Model, LastView = view });

        var again = new SettingsStore(FilePath);
        Assert.Null(again.Notice);
        Assert.Equal(UserSettings.CurrentVersion, again.Current.Version);
        Assert.Equal(OpenDesignsIn.Model, again.Current.OpenIn);
        Assert.Equal(view, again.Current.LastView);
        Assert.Contains($"\"LastView\": \"{view}\"", File.ReadAllText(FilePath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public void Version_3_keeps_where_the_assistants_model_runs_and_starts_with_none()
    {
        // docs/design/llm-assistant.md §7, §11.2: settings version 3 round-trips AssistantSettings.
        Assert.Equal(3, UserSettings.CurrentVersion);
        AssistantSettings fresh = new SettingsStore(FilePath).Current.Assistant;
        Assert.Equal(new AssistantSettings(AssistantProvider.None, "http://127.0.0.1:11434", null, 0.2), fresh);

        AssistantSettings local = new(AssistantProvider.LocalServer, "http://127.0.0.1:11434", "qwen3:4b-q4_K_M", 0.2);
        new SettingsStore(FilePath).Update(s => s with { Assistant = local });

        var again = new SettingsStore(FilePath);
        Assert.Null(again.Notice);
        Assert.Equal(local, again.Current.Assistant);
        string written = File.ReadAllText(FilePath);
        Assert.Contains("\"Version\": 3", written, StringComparison.Ordinal);
        Assert.Contains("\"Provider\": \"LocalServer\"", written, StringComparison.Ordinal);
        Assert.Contains("\"Model\": \"qwen3:4b-q4_K_M\"", written, StringComparison.Ordinal);
        Assert.DoesNotContain("key", written, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Feature", "AST-006")]
    public void A_version_2_file_from_before_the_assistant_gives_the_defaults_and_a_notice()
    {
        // Exactly what a version-2 napkin wrote; beta policy: no converter, the defaults and one line.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"Version\":2,\"ShowRulers\":true,\"Theme\":\"Dark\",\"LastView\":\"Front\"}");

        var store = new SettingsStore(FilePath);

        Assert.Equal(new UserSettings(), store.Current);
        Assert.Equal(AssistantSettings.None, store.Current.Assistant);
        Assert.Equal("Settings file is version 2, which this napkin does not read; using defaults.", store.Notice);
    }

    [Theory]
    [Trait("Feature", "AST-006")]
    [InlineData(AssistantProvider.None, "http://127.0.0.1:11434", "qwen3:4b-q4_K_M", 0.2, null)]
    [InlineData(AssistantProvider.LocalServer, "http://127.0.0.1:11434", null, 0.2, null)]
    [InlineData(AssistantProvider.LocalServer, "http://127.0.0.1:11434", "  ", 0.2, null)]
    [InlineData(AssistantProvider.LocalServer, "http://192.168.1.5:11434", "qwen3:4b-q4_K_M", 0.2, null)]
    [InlineData(AssistantProvider.LocalServer, "http://127.0.0.1:11434", "qwen3:4b-q4_K_M", -1, null)]
    [InlineData(AssistantProvider.LocalServer, "http://127.0.0.1:11434", "qwen3:4b-q4_K_M", 0.2, "Local: qwen3:4b-q4_K_M at 127.0.0.1:11434 — nothing leaves this machine.")]
    [InlineData(AssistantProvider.LocalServer, "localhost:8080", "qwen3-4b", 0.7, "Local: qwen3-4b at localhost:8080 — nothing leaves this machine.")]
    public void The_window_builds_a_local_model_only_from_a_loopback_address_and_a_name(
        AssistantProvider provider, string endpoint, string? model, double temperature, string? whereabouts)
    {
        UserSettings settings = new() { Assistant = new AssistantSettings(provider, endpoint, model, temperature) };

        Napkin.Modules.Assistant.IAssistantModel built = AssistantModels.FromSettings(settings);

        Assert.Equal(whereabouts ?? Napkin.Modules.Assistant.ScriptedModel.NoModelWhereabouts, built.Whereabouts);
        if (whereabouts is null)
        {
            Assert.True(Assert.IsType<Napkin.Modules.Assistant.ScriptedModel>(built).IsNone);
        }
        else
        {
            using var local = Assert.IsType<Napkin.Assistant.LocalServer.LocalServerModel>(built);
            Assert.Equal(temperature, local.Temperature);
        }
    }

    [Fact]
    [Trait("Feature", "VIEW-004")]
    public void A_version_1_file_from_before_the_standard_views_is_refused_and_the_defaults_apply()
    {
        // Exactly what a version-1 napkin wrote: the two-valued view, "Plan".
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"Version\":1,\"ShowRulers\":true,\"OpenIn\":\"Model\",\"LastView\":\"Plan\"}");

        var store = new SettingsStore(FilePath);

        Assert.Equal(new UserSettings(), store.Current);
        Assert.Equal(DesignView.Top, store.Current.LastView);
        Assert.Contains("version 1", store.Notice, StringComparison.Ordinal);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Napkin.App.Settings;

/// <summary>
/// Reads and writes <see cref="UserSettings"/> as one small JSON file in the platform's config
/// directory. A missing, unreadable, corrupt or unknown-version file gives the defaults and a
/// one-line <see cref="Notice"/>; it never throws and never converts. Every change is written at
/// once, through a temp file so a crash cannot leave half a file.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    readonly string? _path;

    /// <param name="path">The settings file, or null for a store that keeps nothing on disk.</param>
    public SettingsStore(string? path)
    {
        _path = path;
        Current = Load(out string? notice);
        Notice = notice;
    }

    /// <summary>The store on the person's real settings file.</summary>
    public static SettingsStore ForUser() => new(Path.Combine(ConfigDirectory(), FileName));

    public UserSettings Current { get; private set; }

    /// <summary>A one-line reason the settings on disk were not used, or null.</summary>
    public string? Notice { get; }

    public string? Location => _path;

    /// <summary>Changes the settings and writes them. A failed write is not fatal: the change still holds for this run.</summary>
    public void Update(Func<UserSettings, UserSettings> change)
    {
        Current = change(Current);
        Save();
    }

    /// <summary>The per-user config directory for napkin on the running platform.</summary>
    public static string ConfigDirectory() => ConfigDirectory(
        OperatingSystem.IsWindows() ? Platform.Windows : OperatingSystem.IsMacOS() ? Platform.MacOS : Platform.Other,
        Environment.GetEnvironmentVariable,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public enum Platform { Windows, MacOS, Other }

    /// <summary>The config directory for a platform: <c>%APPDATA%\napkin</c>, <c>~/Library/Application Support/napkin</c>, or <c>$XDG_CONFIG_HOME/napkin</c> (else <c>~/.config/napkin</c>).</summary>
    public static string ConfigDirectory(Platform platform, Func<string, string?> environment, string home)
    {
        switch (platform)
        {
            case Platform.Windows:
                string appData = environment("APPDATA") is { Length: > 0 } a ? a : System.IO.Path.Combine(home, "AppData", "Roaming");
                return System.IO.Path.Combine(appData, "napkin");
            case Platform.MacOS:
                return System.IO.Path.Combine(home, "Library", "Application Support", "napkin");
            default:
                string xdg = environment("XDG_CONFIG_HOME") is { Length: > 0 } x ? x : System.IO.Path.Combine(home, ".config");
                return System.IO.Path.Combine(xdg, "napkin");
        }
    }

    UserSettings Load(out string? notice)
    {
        notice = null;
        if (_path is null || (!File.Exists(_path) && !Directory.Exists(_path)))
        {
            return new UserSettings();
        }

        try
        {
            string text = File.ReadAllText(_path);

            // The version is looked at before anything else: an older file may hold values this
            // napkin no longer has (version 1's "Plan" view), and it should be told it is old, not broken.
            using (JsonDocument document = JsonDocument.Parse(text))
            {
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty(nameof(UserSettings.Version), out JsonElement version)
                    && version.TryGetInt32(out int number) && number != UserSettings.CurrentVersion)
                {
                    notice = $"Settings file is version {number}, which this napkin does not read; using defaults.";
                    return new UserSettings();
                }
            }

            UserSettings? read = JsonSerializer.Deserialize<UserSettings>(text, Options);
            if (read is null)
            {
                notice = "Settings file was empty; using defaults.";
                return new UserSettings();
            }

            if (read.Version != UserSettings.CurrentVersion)
            {
                notice = $"Settings file is version {read.Version}, which this napkin does not read; using defaults.";
                return new UserSettings();
            }

            return read;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            notice = "Settings file could not be read; using defaults.";
            return new UserSettings();
        }
    }

    void Save()
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Current, Options));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The preference is kept for this run; there is nowhere to keep it longer.
        }
    }
}

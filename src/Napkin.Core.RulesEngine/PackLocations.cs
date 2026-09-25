namespace Napkin.Core.RulesEngine;

/// <summary>Where napkin looks for packs roots: the one shipped beside the executable, then the user's own.</summary>
public static class PackLocations
{
    /// <summary>The shipped packs root and the per-user one, in that order. The folders may not exist.</summary>
    public static IReadOnlyList<string> All() => All(
        AppContext.BaseDirectory,
        OperatingSystem.IsWindows() ? Platform.Windows : OperatingSystem.IsMacOS() ? Platform.MacOS : Platform.Other,
        Environment.GetEnvironmentVariable,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Which platform's config-directory convention applies.</summary>
    public enum Platform
    {
        /// <summary>%APPDATA%\napkin.</summary>
        Windows,

        /// <summary>~/Library/Application Support/napkin.</summary>
        MacOS,

        /// <summary>$XDG_CONFIG_HOME/napkin, else ~/.config/napkin.</summary>
        Other,
    }

    /// <summary>The same two folders for a stated platform (same convention as napkin's settings file).</summary>
    public static IReadOnlyList<string> All(string appBaseDirectory, Platform platform, Func<string, string?> environment, string home)
    {
        ArgumentNullException.ThrowIfNull(environment);
        string config = platform switch
        {
            Platform.Windows => Path.Combine(environment("APPDATA") is { Length: > 0 } a ? a : Path.Combine(home, "AppData", "Roaming"), "napkin"),
            Platform.MacOS => Path.Combine(home, "Library", "Application Support", "napkin"),
            _ => Path.Combine(environment("XDG_CONFIG_HOME") is { Length: > 0 } x ? x : Path.Combine(home, ".config"), "napkin"),
        };
        return [Path.Combine(appBaseDirectory, "packs"), Path.Combine(config, "packs")];
    }
}

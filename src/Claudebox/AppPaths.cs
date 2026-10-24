namespace Claudebox;

/// <summary>Locations of claudebox's own files. CLAUDEBOX_HOME overrides the root (used by tests).</summary>
internal static class AppPaths
{
    public static string Root =>
        Environment.GetEnvironmentVariable("CLAUDEBOX_HOME") is { Length: > 0 } home
            ? home
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "claudebox");

    public static string Sessions => Path.Combine(Root, "sessions.json");

    public static string Config => Path.Combine(Root, "config.json");

    public static string Hooks => Path.Combine(Root, "hooks.json");

    public static string Log => Path.Combine(Root, "cb.log");

    public static string ClaudeProjects =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
}

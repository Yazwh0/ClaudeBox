using System.Text.Json;

namespace Claudebox.Launch;

/// <summary>
/// Claude Code settings passed with --settings, so cb's hooks are added without touching the
/// user's own settings files. Each hook calls back into `cb hook &lt;event&gt;`.
/// </summary>
internal static class HooksFile
{
    public static readonly string[] Events = ["SessionStart", "UserPromptSubmit", "Stop", "Notification", "SessionEnd"];

    public static string Write(string cbPath)
    {
        var json = Build(cbPath);
        if (File.Exists(AppPaths.Hooks) && File.ReadAllText(AppPaths.Hooks) == json)
            return AppPaths.Hooks;

        // Several cb processes may write this at once; a unique temp file keeps each write atomic.
        Directory.CreateDirectory(AppPaths.Root);
        var temp = $"{AppPaths.Hooks}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, AppPaths.Hooks, overwrite: true);
        return AppPaths.Hooks;
    }

    /// <summary>Forward slashes so the command works whether Claude runs hooks through bash or cmd.</summary>
    internal static string Build(string cbPath)
    {
        var command = $"\"{cbPath.Replace('\\', '/')}\" hook";
        var hooks = Events.ToDictionary(
            e => e,
            e => new[] { new { hooks = new[] { new { type = "command", command = $"{command} {e}", timeout = 10 } } } });

        return JsonSerializer.Serialize(new { hooks }, Config.JsonOptions);
    }
}

using System.Text.Json;

namespace Claudebox.Launch;

/// <summary>
/// Claude Code settings passed with --settings, so cb's hooks are added without touching the
/// user's own settings files. Each hook calls back into `cb hook &lt;event&gt;`.
/// </summary>
internal static class HooksFile
{
    public static readonly string[] Events = ["SessionStart", "UserPromptSubmit", "Stop", "Notification", "SessionEnd"];

    /// <summary>
    /// Kept beside the copy of cb it calls, so a session's hooks always point at the copy that
    /// session has pinned, whatever version of cb is installed later.
    /// </summary>
    public static string Write(IReadOnlyList<string> cbCommand)
    {
        var path = Path.Combine(AppCopy.Folder(cbCommand), "hooks.json");
        var json = Build(cbCommand);
        if (File.Exists(path) && File.ReadAllText(path) == json)
            return path;

        // Several cb processes may write this at once; a unique temp file keeps each write atomic.
        var temp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
        return path;
    }

    /// <summary>Forward slashes so the command works whether Claude runs hooks through bash or cmd.</summary>
    internal static string Build(IReadOnlyList<string> cbCommand)
    {
        var command = string.Join(" ", cbCommand.Select(part => $"\"{part.Replace('\\', '/')}\"")) + " hook";
        var hooks = Events.ToDictionary(
            e => e,
            e => new[] { new { hooks = new[] { new { type = "command", command = $"{command} {e}", timeout = 10 } } } });

        return JsonSerializer.Serialize(new { hooks }, Config.JsonOptions);
    }
}

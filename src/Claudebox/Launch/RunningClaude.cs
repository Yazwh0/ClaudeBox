using System.Diagnostics;
using System.Text.Json;

namespace Claudebox.Launch;

/// <summary>A Claude Code process that's running now, and the conversation it has open.</summary>
internal sealed record RunningSession(string SessionId, int Pid, DateTimeOffset StartedAt, string? Cwd, string? Name);

/// <summary>
/// Claude Code writes ~/.claude/sessions/&lt;pid&gt;.json while it runs. That's Claude's internal
/// file, not a documented one, so anything unexpected is ignored: if it changes, cb just can't
/// tell which outside sessions are running.
/// </summary>
internal static class RunningClaude
{
    public static List<RunningSession> All()
    {
        if (!Directory.Exists(AppPaths.ClaudeSessions))
            return [];

        List<RunningSession> running = [];
        foreach (var path in Directory.EnumerateFiles(AppPaths.ClaudeSessions, "*.json"))
        {
            try
            {
                if (Parse(File.ReadAllText(path)) is { } file && StartTime(file.Pid) is { } started
                    && (file.ProcStart is not { } procStart || Math.Abs((started - procStart).TotalSeconds) < 1))
                    running.Add(file.Session with { StartedAt = started });
            }
            catch (IOException)
            {
                // Being written or removed as we read it.
            }
        }
        return running;
    }

    /// <summary>The session in the file, and the process start time it recorded (a FILETIME), if any.</summary>
    internal static (RunningSession Session, int Pid, DateTimeOffset? ProcStart)? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var id)
                || String(root, "sessionId") is not { } sessionId)
                return null;

            // Only a name you gave it (with /rename or --name); Claude's own is just the folder.
            var name = String(root, "nameSource") == "user" ? String(root, "name") : null;
            DateTimeOffset? procStart = long.TryParse(String(root, "procStart"), out var fileTime) && fileTime > 0
                ? DateTimeOffset.FromFileTime(fileTime)
                : null;
            return (new RunningSession(sessionId, id, default, String(root, "cwd"), name), id, procStart);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? String(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Null if the process has gone, so a file left behind by a crash doesn't count.</summary>
    private static DateTimeOffset? StartTime(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited ? null : new DateTimeOffset(process.StartTime);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}

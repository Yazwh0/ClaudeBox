using System.Text.Json;
using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Commands;

/// <summary>
/// `cb hook &lt;event&gt;`: called by Claude Code with the hook payload on stdin. It keeps the
/// record's session ID in step (it changes on /clear and /resume) and tracks busy/waiting.
/// Runs inside Claude, so it must be quick and never fail.
/// </summary>
internal static class HookCommand
{
    public static int Handle(string eventName)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("CLAUDEBOX_ID") is not { Length: > 0 } id)
                return 0;

            using var payload = JsonDocument.Parse(Console.In.ReadToEnd());
            var sessionId = GetString(payload.RootElement, "session_id");
            var transcriptPath = GetString(payload.RootElement, "transcript_path");
            var source = GetString(payload.RootElement, "source");
            var notificationType = GetString(payload.RootElement, "notification_type");

            string? title = null;
            SessionStore.Default.UpdateRecord(id, r =>
            {
                Apply(r, eventName, sessionId, transcriptPath, source, notificationType);
                title = TerminalTitle.WithStatus(r.Name, eventName == "SessionEnd" ? SessionStatus.Running : r.Status);
            });

            if (title is not null && int.TryParse(Environment.GetEnvironmentVariable("CLAUDEBOX_CONSOLE_PID"), out var consolePid))
                TerminalTitle.SetOnConsoleOf(consolePid, title);
        }
        catch (Exception e)
        {
            Log.Write($"hook {eventName} failed: {e}");
        }
        return 0;
    }

    internal static void Apply(SessionRecord record, string eventName, string? sessionId, string? transcriptPath,
        string? source = null, string? notificationType = null)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            // /clear starts a new conversation; remember the old one so cb rm --forget-transcript deletes it too.
            // Not on /resume, which can switch to a conversation that isn't this session's.
            if (source == "clear" && !sessionId.Equals(record.SessionId, StringComparison.OrdinalIgnoreCase)
                && !record.PreviousSessionIds.Contains(record.SessionId, StringComparer.OrdinalIgnoreCase))
                record.PreviousSessionIds.Add(record.SessionId);
            record.SessionId = sessionId;
        }
        if (!string.IsNullOrEmpty(transcriptPath))
            record.TranscriptPath = transcriptPath;

        record.LastActiveAt = DateTimeOffset.Now;
        record.Status = eventName switch
        {
            "SessionStart" or "Stop" => SessionStatus.Waiting,
            "UserPromptSubmit" => SessionStatus.Busy,
            "Notification" when NeedsYou(notificationType) => SessionStatus.Attention,
            _ => record.Status,
        };
    }

    /// <summary>
    /// Claude also notifies when it's been idle a while, or a login worked; neither needs you.
    /// Older Claude versions don't send a type, so those still count.
    /// </summary>
    internal static bool NeedsYou(string? notificationType) => notificationType is not ("idle_prompt" or "auth_success");

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

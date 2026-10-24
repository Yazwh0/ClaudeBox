using System.Text.Json;
using Claudebox.Registry;

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

            SessionStore.Default.UpdateRecord(id, r => Apply(r, eventName, sessionId, transcriptPath));
        }
        catch (Exception e)
        {
            Log.Write($"hook {eventName} failed: {e}");
        }
        return 0;
    }

    internal static void Apply(SessionRecord record, string eventName, string? sessionId, string? transcriptPath)
    {
        if (!string.IsNullOrEmpty(sessionId))
            record.SessionId = sessionId;
        if (!string.IsNullOrEmpty(transcriptPath))
            record.TranscriptPath = transcriptPath;

        record.LastActiveAt = DateTimeOffset.Now;
        record.Status = eventName switch
        {
            "SessionStart" or "Stop" => SessionStatus.Waiting,
            "UserPromptSubmit" => SessionStatus.Busy,
            "Notification" => SessionStatus.Attention,
            _ => record.Status,
        };
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

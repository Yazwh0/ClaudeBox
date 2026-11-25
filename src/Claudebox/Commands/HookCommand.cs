using System.Diagnostics;
using System.Text.Json;
using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Commands;

/// <summary>What cb uses from a hook's payload.</summary>
internal sealed record HookInput(string Event)
{
    public string? SessionId { get; init; }
    public string? TranscriptPath { get; init; }
    public string? Source { get; init; }
    public string? NotificationType { get; init; }
    public string? ToolName { get; init; }
    public string? Trigger { get; init; }
    public string? Reason { get; init; }
    /// <summary>When Claude ran the hook. Async hooks can finish out of order, so older ones mustn't win.</summary>
    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;
}

/// <summary>
/// `cb hook &lt;event&gt;`: called by Claude Code with the hook payload on stdin. It keeps the
/// record's session ID in step (it changes on /clear and /resume) and tracks what Claude is doing.
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
            var root = payload.RootElement;
            var input = new HookInput(eventName)
            {
                SessionId = GetString(root, "session_id"),
                TranscriptPath = GetString(root, "transcript_path"),
                Source = GetString(root, "source"),
                NotificationType = GetString(root, "notification_type"),
                ToolName = GetString(root, "tool_name"),
                Trigger = GetString(root, "trigger"),
                Reason = GetString(root, "reason"),
                // Claude starts hooks in the order things happen, even when they finish out of order.
                At = new DateTimeOffset(Process.GetCurrentProcess().StartTime),
            };

            string? title = null;
            SessionStore.Default.UpdateRecord(id, r =>
            {
                Apply(r, input);
                title = Title(r, input);
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

    /// <summary>The tab's title after this hook, or null to leave it alone.</summary>
    internal static string? Title(SessionRecord record, HookInput input) => input.Event switch
    {
        // /clear and /resume end one conversation and start another straight away.
        "SessionEnd" when input.Reason is "clear" or "resume" => null,
        "SessionEnd" => TerminalTitle.Ended(record.Name),
        _ => TerminalTitle.WithStatus(record.Name, record.Status, record.Subagents),
    };

    internal static void Apply(SessionRecord record, HookInput input)
    {
        if (!string.IsNullOrEmpty(input.SessionId))
        {
            // /clear starts a new conversation; remember the old one so cb rm --forget-transcript deletes it too.
            // Not on /resume, which can switch to a conversation that isn't this session's.
            if (input.Source == "clear" && !input.SessionId.Equals(record.SessionId, StringComparison.OrdinalIgnoreCase)
                && !record.PreviousSessionIds.Contains(record.SessionId, StringComparer.OrdinalIgnoreCase))
                record.PreviousSessionIds.Add(record.SessionId);
            record.SessionId = input.SessionId;
        }
        if (!string.IsNullOrEmpty(input.TranscriptPath))
            record.TranscriptPath = input.TranscriptPath;
        record.LastActiveAt = DateTimeOffset.Now;

        // Counting doesn't depend on order, so these always apply.
        record.Subagents = input.Event switch
        {
            "SessionStart" or "SessionEnd" => 0,
            "SubagentStart" => record.Subagents + 1,
            "SubagentStop" => Math.Max(0, record.Subagents - 1),
            _ => record.Subagents,
        };

        if (record.StatusAt is { } statusAt && input.At < statusAt)
            return;
        var status = NewStatus(record.Status, input);
        if (status != record.Status)
        {
            record.Status = status;
            record.StatusAt = input.At;
        }
    }

    private static string NewStatus(string current, HookInput input) => input.Event switch
    {
        "SessionStart" or "Stop" => SessionStatus.Waiting,
        "UserPromptSubmit" or "PostToolUse" or "PostToolUseFailure" => SessionStatus.Busy,
        "StopFailure" => SessionStatus.Failed,
        "PreToolUse" => ToolStatus(input.ToolName),
        "PermissionRequest" => IsQuestion(input.ToolName) ? SessionStatus.Asking : SessionStatus.Attention,
        "PreCompact" => SessionStatus.Compacting,
        // A /compact you typed goes back to waiting for you; one Claude needed carries on working.
        "PostCompact" => input.Trigger == "manual" ? SessionStatus.Waiting : SessionStatus.Busy,
        "Notification" => input.NotificationType switch
        {
            "idle_prompt" when current == SessionStatus.Waiting => SessionStatus.Idle,
            "elicitation_dialog" or "elicitation_url_dialog" => SessionStatus.Asking,
            // Older Claude versions don't send a type. Keep a question showing as one.
            "permission_prompt" or null when current != SessionStatus.Asking => SessionStatus.Attention,
            _ => current,
        },
        _ => current,
    };

    private static string ToolStatus(string? toolName) => toolName switch
    {
        _ when IsQuestion(toolName) => SessionStatus.Asking,
        "Bash" or "PowerShell" => SessionStatus.Command,
        "Edit" or "Write" or "NotebookEdit" => SessionStatus.Editing,
        _ => SessionStatus.Busy,
    };

    /// <summary>Tools that stop to ask you something rather than for permission.</summary>
    private static bool IsQuestion(string? toolName) => toolName is "AskUserQuestion" or "ExitPlanMode";

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

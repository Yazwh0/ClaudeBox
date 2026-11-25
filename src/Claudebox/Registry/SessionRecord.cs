using System.Text.Json.Serialization;

namespace Claudebox.Registry;

internal sealed class SessionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string Name { get; set; }
    public required string Cwd { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string? TranscriptPath { get; set; }
    /// <summary>Earlier conversations in this session, from before a /clear.</summary>
    public List<string> PreviousSessionIds { get; set; } = [];
    public List<string> ClaudeArgs { get; set; } = [];
    public string? Work { get; set; }
    public string? Colour { get; set; }
    public string? Scheme { get; set; }
    public string Status { get; set; } = SessionStatus.New;
    /// <summary>When the hook that set Status ran, so a slower, older hook can't overwrite it.</summary>
    public DateTimeOffset? StatusAt { get; set; }
    /// <summary>Subagents Claude has running.</summary>
    public int Subagents { get; set; }
    public int? Pid { get; set; }
    public DateTimeOffset? ProcessStartedAt { get; set; }
    /// <summary>The cb process running the session in its tab, whose console the tab title belongs to.</summary>
    public int? ConsolePid { get; set; }
    public bool Open { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? ClosedAt { get; set; }

    [JsonIgnore]
    public string ShortId => Id[..8];
}

internal static class SessionStatus
{
    public const string New = "new";
    public const string Running = "running";
    public const string Waiting = "waiting";
    public const string Busy = "busy";
    public const string Idle = "idle";
    public const string Command = "command";
    public const string Editing = "editing";
    public const string Compacting = "compacting";
    public const string Attention = "attention";
    public const string Asking = "asking";
    public const string Failed = "failed";
    public const string Exited = "exited";
    public const string Closed = "closed";
    public const string Interrupted = "interrupted";

    public static readonly string[] All =
        [New, Running, Waiting, Idle, Busy, Command, Editing, Compacting, Attention, Asking, Failed, Exited, Closed, Interrupted];
}

internal sealed class SessionFile
{
    public int Version { get; set; } = 1;
    public List<SessionRecord> Sessions { get; set; } = [];
}

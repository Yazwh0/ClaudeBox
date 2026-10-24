using System.Text.Json.Serialization;

namespace Claudebox.Registry;

internal sealed class SessionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string Name { get; set; }
    public required string Cwd { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string? TranscriptPath { get; set; }
    public List<string> ClaudeArgs { get; set; } = [];
    public string? Work { get; set; }
    public string? Colour { get; set; }
    public string? Scheme { get; set; }
    public string Status { get; set; } = SessionStatus.New;
    public int? Pid { get; set; }
    public DateTimeOffset? ProcessStartedAt { get; set; }
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
    public const string Attention = "attention";
    public const string Exited = "exited";
    public const string Closed = "closed";
    public const string Interrupted = "interrupted";
}

internal sealed class SessionFile
{
    public int Version { get; set; } = 1;
    public List<SessionRecord> Sessions { get; set; } = [];
}

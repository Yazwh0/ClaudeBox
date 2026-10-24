using Claudebox.Commands;
using Claudebox.Launch;
using Claudebox.Registry;

namespace Claudebox.Tests;

public class LifecycleTests
{
    private static readonly DateTimeOffset Shutdown = new(2026, 10, 24, 18, 0, 0, TimeSpan.Zero);

    private static SessionRecord Record(bool open = false, string status = SessionStatus.Exited, DateTimeOffset? closedAt = null) =>
        new() { Name = "s", Cwd = "C:\\", Open = open, Status = status, ClosedAt = closedAt };

    [Fact]
    public void Restorable_OpenAndNotRunning()
    {
        Assert.True(RestorePlanner.IsRestorable(Record(open: true), all: false, alive: false, Shutdown));
    }

    [Fact]
    public void NotRestorable_WhenRunning()
    {
        Assert.False(RestorePlanner.IsRestorable(Record(open: true), all: true, alive: true, Shutdown));
    }

    [Fact]
    public void NotRestorable_ExitedNormally_UnlessAll()
    {
        var record = Record(closedAt: Shutdown.AddSeconds(-10));
        Assert.False(RestorePlanner.IsRestorable(record, all: false, alive: false, Shutdown));
        Assert.True(RestorePlanner.IsRestorable(record, all: true, alive: false, Shutdown));
    }

    [Fact]
    public void Restorable_ClosedJustBeforeShutdown()
    {
        var record = Record(status: SessionStatus.Closed, closedAt: Shutdown.AddSeconds(-20));
        Assert.True(RestorePlanner.IsRestorable(record, all: false, alive: false, Shutdown));
    }

    [Fact]
    public void NotRestorable_ClosedLongBeforeShutdown()
    {
        var record = Record(status: SessionStatus.Closed, closedAt: Shutdown.AddMinutes(-10));
        Assert.False(RestorePlanner.IsRestorable(record, all: false, alive: false, Shutdown));
    }

    [Fact]
    public void NotRestorable_ClosedWithUnknownShutdown()
    {
        var record = Record(status: SessionStatus.Closed, closedAt: Shutdown);
        Assert.False(RestorePlanner.IsRestorable(record, all: false, alive: false, lastShutdown: null));
    }

    [Theory]
    [InlineData(false, false, null, true)]   // exited, no filters
    [InlineData(true, false, null, false)]   // running: never removed
    [InlineData(false, false, "HOTFIX", true)]
    [InlineData(false, false, "research", false)]
    public void Removable_ByWork(bool alive, bool exitedOnly, string? work, bool expected)
    {
        var record = Record();
        record.Work = "hotfix";
        Assert.Equal(expected, SessionFilter.IsRemovable(record, alive, exitedOnly, work));
    }

    [Fact]
    public void Removable_ExitedOnly_KeepsInterrupted()
    {
        var interrupted = Record(open: true);
        Assert.True(SessionFilter.IsRemovable(interrupted, alive: false, exitedOnly: false, work: null));
        Assert.False(SessionFilter.IsRemovable(interrupted, alive: false, exitedOnly: true, work: null));
    }

    [Fact]
    public void MatchesWork_NoFilter_MatchesSessionsWithoutWork()
    {
        Assert.True(SessionFilter.MatchesWork(Record(), null));
        Assert.False(SessionFilter.MatchesWork(Record(), "hotfix"));
    }

    [Theory]
    [InlineData("SessionStart", SessionStatus.Waiting)]
    [InlineData("UserPromptSubmit", SessionStatus.Busy)]
    [InlineData("Stop", SessionStatus.Waiting)]
    [InlineData("Notification", SessionStatus.Attention)]
    [InlineData("SessionEnd", SessionStatus.Running)]
    public void Hook_SetsStatus(string eventName, string expected)
    {
        var record = Record(status: SessionStatus.Running);
        HookCommand.Apply(record, eventName, null, null);
        Assert.Equal(expected, record.Status);
    }

    [Fact]
    public void Hook_TracksNewSessionIdAfterClear()
    {
        var record = Record();
        HookCommand.Apply(record, "SessionStart", "new-id", @"C:\t\new-id.jsonl");

        Assert.Equal("new-id", record.SessionId);
        Assert.Equal(@"C:\t\new-id.jsonl", record.TranscriptPath);
    }
}

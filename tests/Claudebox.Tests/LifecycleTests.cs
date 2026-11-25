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

    [Fact]
    public void Restorable_ClosedWithItsWindow()
    {
        var record = Record(status: SessionStatus.Closed, closedAt: Shutdown.AddDays(-1));
        Assert.True(RestorePlanner.IsRestorable(record, all: false, alive: false, Shutdown, closedWithOthers: true));
        Assert.False(RestorePlanner.IsRestorable(record, all: false, alive: false, Shutdown, closedWithOthers: false));
    }

    [Fact]
    public void NotRestorable_ExitedWithOthers()
    {
        var record = Record(status: SessionStatus.Exited, closedAt: Shutdown.AddDays(-1));
        Assert.False(RestorePlanner.IsRestorable(record, all: false, alive: false, Shutdown, closedWithOthers: true));
    }

    [Fact]
    public void ClosedTogether_ChainsClosesUnderTwoSecondsApart()
    {
        var t = Shutdown.AddDays(-1);
        SessionRecord Closed(string id, double seconds, string status = SessionStatus.Closed)
        {
            var r = Record(status: status, closedAt: t.AddSeconds(seconds));
            r.Id = id;
            return r;
        }

        var together = RestorePlanner.ClosedTogether(
        [
            Closed("window1", 0), Closed("window2", 1.5), Closed("window3", 3),  // a chain: each within 2s of the last
            Closed("alone", 60),                                                // one tab closed by hand
            Closed("exited", 61, SessionStatus.Exited),                         // exits never count
            Closed("later", 120),
        ]);

        Assert.Equal(["window1", "window2", "window3"], together.Order());
    }

    [Fact]
    public void Removable_ExitedOnly_KeepsClosedWithItsWindow()
    {
        var record = Record(status: SessionStatus.Closed, closedAt: Shutdown);
        Assert.True(SessionFilter.IsRemovable(record, alive: false, exitedOnly: true, work: null));
        Assert.False(SessionFilter.IsRemovable(record, alive: false, exitedOnly: true, work: null, closedWithOthers: true));
        Assert.True(SessionFilter.IsRemovable(record, alive: false, exitedOnly: false, work: null, closedWithOthers: true));
    }

    [Fact]
    public void EffectiveStatus_ClosedWithItsWindow_IsInterrupted()
    {
        var record = Record(status: SessionStatus.Closed, closedAt: Shutdown);
        Assert.Equal(SessionStatus.Interrupted, ProcessLiveness.EffectiveStatus(record, closedWithOthers: true));
        Assert.Equal(SessionStatus.Closed, ProcessLiveness.EffectiveStatus(record));
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
    [InlineData("StopFailure", SessionStatus.Failed)]
    [InlineData("Notification", SessionStatus.Attention)]
    [InlineData("PermissionRequest", SessionStatus.Attention)]
    [InlineData("PostToolUse", SessionStatus.Busy)]
    [InlineData("PostToolUseFailure", SessionStatus.Busy)]
    [InlineData("PreCompact", SessionStatus.Compacting)]
    [InlineData("SessionEnd", SessionStatus.Running)]
    [InlineData("SubagentStart", SessionStatus.Running)]
    public void Hook_SetsStatus(string eventName, string expected)
    {
        var record = Record(status: SessionStatus.Running);
        HookCommand.Apply(record, new HookInput(eventName));
        Assert.Equal(expected, record.Status);
    }

    [Theory]
    [InlineData("Bash", SessionStatus.Command)]
    [InlineData("PowerShell", SessionStatus.Command)]
    [InlineData("Edit", SessionStatus.Editing)]
    [InlineData("Write", SessionStatus.Editing)]
    [InlineData("AskUserQuestion", SessionStatus.Asking)]
    [InlineData("ExitPlanMode", SessionStatus.Asking)]
    [InlineData("Read", SessionStatus.Busy)]
    public void Hook_PreToolUse_ShowsWhatKindOfTool(string tool, string expected)
    {
        var record = Record(status: SessionStatus.Busy);
        HookCommand.Apply(record, new HookInput("PreToolUse") { ToolName = tool });
        Assert.Equal(expected, record.Status);
    }

    [Fact]
    public void Hook_PermissionForAQuestion_IsAsking()
    {
        var record = Record(status: SessionStatus.Busy);
        HookCommand.Apply(record, new HookInput("PermissionRequest") { ToolName = "AskUserQuestion" });
        Assert.Equal(SessionStatus.Asking, record.Status);
    }

    [Theory]
    [InlineData("manual", SessionStatus.Waiting)]
    [InlineData("auto", SessionStatus.Busy)]
    public void Hook_PostCompact_GoesBackToWhatItWasDoing(string trigger, string expected)
    {
        var record = Record(status: SessionStatus.Compacting);
        HookCommand.Apply(record, new HookInput("PostCompact") { Trigger = trigger });
        Assert.Equal(expected, record.Status);
    }

    [Fact]
    public void Hook_OlderAsyncHook_DoesntOverwriteNewerStatus()
    {
        var record = Record(status: SessionStatus.Busy);
        var now = DateTimeOffset.Now;
        HookCommand.Apply(record, new HookInput("Stop") { At = now });
        HookCommand.Apply(record, new HookInput("PostToolUse") { At = now.AddMilliseconds(-50) });
        Assert.Equal(SessionStatus.Waiting, record.Status);
    }

    [Fact]
    public void Hook_CountsSubagents()
    {
        var record = Record(status: SessionStatus.Busy);
        HookCommand.Apply(record, new HookInput("SubagentStart"));
        HookCommand.Apply(record, new HookInput("SubagentStart"));
        HookCommand.Apply(record, new HookInput("SubagentStop"));
        Assert.Equal(1, record.Subagents);

        HookCommand.Apply(record, new HookInput("SubagentStop"));
        HookCommand.Apply(record, new HookInput("SubagentStop"));
        Assert.Equal(0, record.Subagents);
    }

    [Theory]
    [InlineData("clear", false)]
    [InlineData("resume", false)]
    [InlineData("prompt_input_exit", true)]
    [InlineData(null, true)]
    public void Hook_SessionEnd_ShowsEndedUnlessAnotherConversationStarts(string? reason, bool ended)
    {
        var record = Record(status: SessionStatus.Waiting);
        var title = HookCommand.Title(record, new HookInput("SessionEnd") { Reason = reason });
        Assert.Equal(ended ? "✕ s" : null, title);
    }

    [Fact]
    public void Hook_TracksNewSessionIdAfterClear()
    {
        var record = Record();
        HookCommand.Apply(record, new HookInput("SessionStart") { SessionId = "new-id", TranscriptPath = @"C:\t\new-id.jsonl" });

        Assert.Equal("new-id", record.SessionId);
        Assert.Equal(@"C:\t\new-id.jsonl", record.TranscriptPath);
    }

    [Theory]
    [InlineData("permission_prompt", SessionStatus.Waiting, SessionStatus.Attention)]
    [InlineData(null, SessionStatus.Waiting, SessionStatus.Attention)]
    [InlineData("permission_prompt", SessionStatus.Asking, SessionStatus.Asking)]
    [InlineData("elicitation_dialog", SessionStatus.Busy, SessionStatus.Asking)]
    [InlineData("elicitation_url_dialog", SessionStatus.Busy, SessionStatus.Asking)]
    [InlineData("idle_prompt", SessionStatus.Waiting, SessionStatus.Idle)]
    [InlineData("idle_prompt", SessionStatus.Busy, SessionStatus.Busy)]
    [InlineData("auth_success", SessionStatus.Waiting, SessionStatus.Waiting)]
    public void Hook_Notification_DependsOnType(string? type, string before, string expected)
    {
        var record = Record(status: before);
        HookCommand.Apply(record, new HookInput("Notification") { NotificationType = type });
        Assert.Equal(expected, record.Status);
    }

    [Fact]
    public void Hook_Clear_RemembersPreviousConversation()
    {
        var record = Record();
        var old = record.SessionId;
        HookCommand.Apply(record, new HookInput("SessionStart") { SessionId = "new-id", Source = "clear" });
        HookCommand.Apply(record, new HookInput("SessionStart") { SessionId = "new-id", Source = "clear" });

        Assert.Equal([old], record.PreviousSessionIds);
    }

    [Theory]
    [InlineData("resume")]
    [InlineData("startup")]
    [InlineData(null)]
    public void Hook_OtherSessionStarts_DontRememberPreviousConversation(string? source)
    {
        var record = Record();
        HookCommand.Apply(record, new HookInput("SessionStart") { SessionId = "other-id", Source = source });

        Assert.Empty(record.PreviousSessionIds);
    }
}

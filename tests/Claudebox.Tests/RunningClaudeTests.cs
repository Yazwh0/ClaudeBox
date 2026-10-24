using System.Diagnostics;
using Claudebox.Launch;
using Claudebox.Registry;

namespace Claudebox.Tests;

public class RunningClaudeTests
{
    [Fact]
    public void Parse_ReadsSessionAndOnlyAUserGivenName()
    {
        var parsed = RunningClaude.Parse("""
            {"pid":27280,"sessionId":"293c0546-8bd2-4391-aac3-84ed44fad5af","cwd":"c:\\src\\app",
             "procStart":"134373210211466180","name":"app-0e","nameSource":"derived","status":"busy"}
            """);

        Assert.NotNull(parsed);
        var (session, pid, procStart) = parsed.Value;
        Assert.Equal(27280, pid);
        Assert.Equal("293c0546-8bd2-4391-aac3-84ed44fad5af", session.SessionId);
        Assert.Equal(@"c:\src\app", session.Cwd);
        Assert.Null(session.Name);
        Assert.Equal(DateTimeOffset.FromFileTime(134373210211466180), procStart);
    }

    [Fact]
    public void Parse_KeepsYourName()
    {
        var parsed = RunningClaude.Parse("""{"pid":1,"sessionId":"x","name":"BitMagic","nameSource":"user"}""");
        Assert.Equal("BitMagic", parsed?.Session.Name);
    }

    [Theory]
    [InlineData("""{"sessionId":"x"}""")]
    [InlineData("""{"pid":1}""")]
    [InlineData("not json")]
    public void Parse_IgnoresFilesItDoesNotUnderstand(string json)
    {
        Assert.Null(RunningClaude.Parse(json));
    }

    [Fact]
    public void EffectiveStatus_AdoptedWhileRunning_ShowsRunning()
    {
        using var self = Process.GetCurrentProcess();
        var record = new SessionRecord
        {
            Name = "outside",
            Cwd = @"C:\src",
            Status = SessionStatus.Exited,
            Pid = self.Id,
            ProcessStartedAt = self.StartTime,
        };

        Assert.Equal(SessionStatus.Running, ProcessLiveness.EffectiveStatus(record));

        record.Pid = null;
        Assert.Equal(SessionStatus.Exited, ProcessLiveness.EffectiveStatus(record));
    }
}

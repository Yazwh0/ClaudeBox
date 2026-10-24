using System.Diagnostics;
using System.Runtime.InteropServices;
using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Launch;

/// <summary>
/// Runs Claude in the current console. Claude inherits the console directly (no redirection),
/// so cb never sees or touches its output; cb only records the session's lifecycle.
/// </summary>
internal sealed class ClaudeLauncher(SessionStore store)
{
    public int Run(SessionRecord record)
    {
        var info = new ProcessStartInfo(ClaudeLocator.Find())
        {
            UseShellExecute = false,
            WorkingDirectory = record.Cwd,
        };
        foreach (var arg in BuildArguments(record, Transcripts.Find(record.SessionId, record.TranscriptPath) is not null))
            info.ArgumentList.Add(arg);
        info.Environment["CLAUDEBOX_ID"] = record.Id;

        TerminalTitle.Set(record.Name);

        // Ctrl+C reaches every process on the console; let Claude handle it and keep cb alive.
        using var ctrlC = PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx => ctx.Cancel = true);
        // .NET maps CTRL_CLOSE_EVENT (tab or window closed) to SIGHUP: a deliberate close.
        using var close = PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ => MarkClosed(record.Id, SessionStatus.Closed));
        // Shutdown and logoff map to SIGTERM: leave the session open so it is restored.

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Failed to start claude.");

        store.UpdateRecord(record.Id, r =>
        {
            r.Open = true;
            r.Status = SessionStatus.Running;
            r.Pid = process.Id;
            r.ProcessStartedAt = new DateTimeOffset(process.StartTime);
            r.LastActiveAt = DateTimeOffset.Now;
            r.ClosedAt = null;
        });

        process.WaitForExit();
        MarkClosed(record.Id, SessionStatus.Exited);
        return process.ExitCode;
    }

    /// <summary>A transcript only exists once the session has had a message, so first launches use --session-id.</summary>
    internal static List<string> BuildArguments(SessionRecord record, bool transcriptExists)
    {
        List<string> args = transcriptExists
            ? ["--resume", record.SessionId]
            : ["--session-id", record.SessionId];
        args.AddRange(["--name", record.Name]);
        args.AddRange(record.ClaudeArgs);
        return args;
    }

    private void MarkClosed(string id, string status) => store.UpdateRecord(id, r =>
    {
        r.Open = false;
        r.Status = status;
        r.Pid = null;
        r.ProcessStartedAt = null;
        r.ClosedAt = DateTimeOffset.Now;
        r.LastActiveAt = DateTimeOffset.Now;
        r.TranscriptPath = Transcripts.Find(r.SessionId, r.TranscriptPath);
    });
}

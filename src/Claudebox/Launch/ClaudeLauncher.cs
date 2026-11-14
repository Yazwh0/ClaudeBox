using System.Diagnostics;
using System.Runtime.InteropServices;
using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Launch;

/// <summary>
/// Runs Claude in the current console. Claude inherits the console directly (no redirection),
/// so cb never sees or touches its output; cb only records the session's lifecycle.
/// </summary>
internal sealed class ClaudeLauncher(SessionStore store, Config config)
{
    private volatile bool _closing;

    public int Run(SessionRecord record)
    {
        var info = new ProcessStartInfo(ClaudeLocator.Find(config.ClaudePath))
        {
            UseShellExecute = false,
            WorkingDirectory = record.Cwd,
        };
        var cbCommand = AppCopy.Command();
        using var pin = AppCopy.Pin(cbCommand);
        var transcriptExists = Transcripts.Find(record.SessionId, record.TranscriptPath) is not null;
        foreach (var arg in BuildArguments(record, transcriptExists, HooksFile.Write(cbCommand), ConfigArgs(config, record)))
            info.ArgumentList.Add(arg);
        info.Environment["CLAUDEBOX_ID"] = record.Id;
        // cb's hooks keep the title as the session's name plus a status icon, so Claude mustn't set
        // its own. Hooks have their own hidden console, so they reach the tab through this process's.
        info.Environment["CLAUDE_CODE_DISABLE_TERMINAL_TITLE"] = "1";
        info.Environment["CLAUDEBOX_CONSOLE_PID"] = Environment.ProcessId.ToString();

        TerminalTitle.Set(record.Name);

        // Ctrl+C reaches every process on the console; let Claude handle it and keep cb alive.
        using var ctrlC = PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx => ctx.Cancel = true);
        // .NET maps CTRL_CLOSE_EVENT (tab or window closed) to SIGHUP: a deliberate close.
        using var close = PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ =>
        {
            _closing = true;
            MarkClosed(record.Id, SessionStatus.Closed);
        });
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
        // Closing the tab kills Claude too, so WaitForExit can return while the close handler runs.
        if (_closing)
            return process.ExitCode;

        // Drop the status icon, since Claude isn't running any more.
        TerminalTitle.Set(store.ReadAll().Find(s => s.Id == record.Id)?.Name ?? record.Name);

        // A failed exit is a crash rather than you exiting: leave the session open so it's restored.
        if (process.ExitCode != 0)
            MarkInterrupted(record.Id);
        else
            MarkClosed(record.Id, SessionStatus.Exited);
        return process.ExitCode;
    }

    /// <summary>The config's claudeArgs, then the session's work type's, so the more specific comes later.</summary>
    internal static List<string> ConfigArgs(Config config, SessionRecord record) =>
        [.. config.ClaudeArgs, .. (record.Work is { } work ? config.FindWork(work)?.ClaudeArgs : null) ?? []];

    /// <summary>
    /// A transcript only exists once the session has had a message, so first launches use --session-id.
    /// The config's arguments (see ConfigArgs) go on every launch, before the session's own (from cb new -- ...).
    /// </summary>
    internal static List<string> BuildArguments(SessionRecord record, bool transcriptExists, string settingsPath,
        IReadOnlyList<string>? configArgs = null)
    {
        List<string> args = transcriptExists
            ? ["--resume", record.SessionId]
            : ["--session-id", record.SessionId];
        args.AddRange(["--name", record.Name, "--settings", settingsPath]);
        args.AddRange(configArgs ?? []);
        args.AddRange(record.ClaudeArgs);
        return args;
    }

    private void MarkInterrupted(string id) => store.UpdateRecord(id, r =>
    {
        r.Status = SessionStatus.Interrupted;
        r.LastActiveAt = DateTimeOffset.Now;
        r.TranscriptPath = Transcripts.Find(r.SessionId, r.TranscriptPath);
    });

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

using Claudebox.Launch;
using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Commands;

internal static class SessionCommands
{
    public static int New(string name, DirectoryInfo? dir, string? colour, bool here, string[] claudeArgs)
    {
        var cwd = (dir ?? new DirectoryInfo(Environment.CurrentDirectory)).FullName;
        if (!Directory.Exists(cwd))
            return Fail($"Folder not found: {cwd}");

        var store = SessionStore.Default;
        var record = store.Update(file =>
        {
            if (file.Sessions.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return null;

            var created = new SessionRecord { Name = name, Cwd = cwd, Colour = colour, ClaudeArgs = [.. claudeArgs] };
            file.Sessions.Add(created);
            return created;
        });

        if (record is null)
            return Fail($"A session called '{name}' already exists. Use 'cb resume \"{name}\"'.");

        return Launch(store, record, here);
    }

    public static int Resume(string query, bool here)
    {
        var store = SessionStore.Default;
        var record = SessionResolver.Resolve(store.ReadAll(), query);

        if (ProcessLiveness.IsAlive(record))
            return Fail($"'{record.Name}' is already running (pid {record.Pid}).");
        if (Transcripts.Find(record.SessionId, record.TranscriptPath) is null && record.Status != SessionStatus.New)
            Console.Error.WriteLine($"Warning: no transcript found for '{record.Name}'; Claude will start a fresh conversation.");

        return Launch(store, record, here);
    }

    /// <summary>Internal: the command a new Windows Terminal tab runs.</summary>
    public static int Run(string id)
    {
        var store = SessionStore.Default;
        var record = store.ReadAll().Find(s => s.Id == id);
        if (record is null)
            return Fail($"Unknown session id {id}.");

        return new ClaudeLauncher(store).Run(record);
    }

    public static int List(bool all)
    {
        var sessions = SessionStore.Default.ReadAll();
        if (sessions.Count == 0)
        {
            Console.WriteLine("No sessions yet. Start one with: cb new \"<name>\"");
            return 0;
        }

        var nameWidth = Math.Max(4, sessions.Max(s => s.Name.Length));
        Console.WriteLine($"{"#",3}  {"Name".PadRight(nameWidth)}  {"Status",-12}  {"Last active",-16}  Folder");
        for (var i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            var status = ProcessLiveness.EffectiveStatus(s);
            if (!all && status is SessionStatus.Exited or SessionStatus.Closed && s.LastActiveAt < DateTimeOffset.Now.AddDays(-14))
                continue;

            Console.WriteLine($"{i + 1,3}  {s.Name.PadRight(nameWidth)}  {status,-12}  {s.LastActiveAt:yyyy-MM-dd HH:mm}  {s.Cwd}");
        }
        return 0;
    }

    private static int Launch(SessionStore store, SessionRecord record, bool here)
    {
        if (here || !WindowsTerminal.IsHosting)
            return new ClaudeLauncher(store).Run(record);

        WindowsTerminal.OpenTab(record);
        Console.WriteLine($"Opened '{record.Name}' in a new tab.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}

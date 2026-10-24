using Claudebox.Launch;
using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Commands;

internal sealed record NewOptions(
    string Name,
    DirectoryInfo? Dir,
    string? Work,
    string? Colour,
    bool Here,
    string[] ClaudeArgs,
    ClashAction? OnClash,
    string? RenamePattern);

internal static class SessionCommands
{
    public static int New(NewOptions options)
    {
        var config = Config.Load();
        var name = NameClash.CleanName(options.Name);
        if (name.Length == 0)
            return Fail("The name can't be empty.");

        var cwd = (options.Dir ?? new DirectoryInfo(Environment.CurrentDirectory)).FullName;
        if (!Directory.Exists(cwd))
            return Fail($"Folder not found: {cwd}");

        var work = options.Work ?? config.DefaultWork;
        var workType = work is null ? null : config.FindWork(work);
        if (options.Work is not null && workType is null)
            return Fail($"Unknown work type '{options.Work}'. Configured: {string.Join(", ", config.Work.Keys)}.");
        var colour = options.Colour ?? workType?.Colour;

        var action = NameClash.Choose(options.OnClash, config.NameClash, interactive: !Console.IsInputRedirected);
        var pattern = options.RenamePattern ?? config.NameClash.RenamePattern;
        var store = SessionStore.Default;

        while (store.ReadAll() is var sessions && FindByName(sessions, name) is { } existing)
        {
            var running = ProcessLiveness.IsAlive(existing);
            var choice = action == ClashAction.Ask ? AskAboutClash(existing) : action;
            if (choice is null)
                return 1;

            if (running && choice is ClashAction.Resume or ClashAction.Replace)
            {
                if (action != ClashAction.Ask)
                    return Fail($"'{existing.Name}' is already running, so it can't be resumed or replaced.");
                Console.WriteLine($"'{existing.Name}' is already running. Pick another option.");
                continue;
            }

            switch (choice)
            {
                case ClashAction.Fail:
                    return Fail($"A session called '{existing.Name}' already exists. Use 'cb resume \"{existing.Name}\"' or --on-clash.");
                case ClashAction.Resume:
                    return Launch(store, config, existing, options.Here);
                case ClashAction.Replace:
                    store.Update(f => f.Sessions.RemoveAll(s => s.Id == existing.Id));
                    break;
                case ClashAction.Rename:
                    var suggestion = NameClash.NextFreeName(name, pattern, sessions.Select(s => s.Name));
                    name = action == ClashAction.Ask ? NameClash.CleanName(Prompt.Ask("New name", suggestion)) : suggestion;
                    break;
            }
        }

        var record = store.Update(file =>
        {
            // Re-checked under the lock in case another cb created the same name meanwhile.
            if (FindByName(file.Sessions, name) is not null)
                return null;

            var created = new SessionRecord
            {
                Name = name,
                Cwd = cwd,
                Work = workType is null ? null : work,
                Colour = colour is null ? null : Colours.Normalise(colour),
                Scheme = workType?.Scheme,
                ClaudeArgs = [.. options.ClaudeArgs],
            };
            file.Sessions.Add(created);
            return created;
        });

        return record is null
            ? Fail($"A session called '{name}' was just created elsewhere.")
            : Launch(store, config, record, options.Here);
    }

    public static int Resume(string query, bool here)
    {
        var store = SessionStore.Default;
        var record = SessionResolver.Resolve(store.ReadAll(), query);

        if (ProcessLiveness.IsAlive(record))
            return Fail($"'{record.Name}' is already running (pid {record.Pid}).");

        return Launch(store, Config.Load(), record, here);
    }

    /// <summary>Internal: the command a new Windows Terminal tab runs.</summary>
    public static int Run(string id)
    {
        var store = SessionStore.Default;
        var record = store.ReadAll().Find(s => s.Id == id);
        if (record is null)
            return Fail($"Unknown session id {id}.");

        return new ClaudeLauncher(store, Config.Load()).Run(record);
    }

    public static int Restore(bool all, bool newWindow, bool dryRun)
    {
        var lastShutdown = SystemTimes.LastShutdown();
        var toRestore = SessionStore.Default.ReadAll()
            .Where(s => RestorePlanner.IsRestorable(s, all, ProcessLiveness.IsAlive(s), lastShutdown))
            .ToList();

        if (toRestore.Count == 0)
        {
            Console.WriteLine(all ? "Nothing to restore: every session is already running." : "Nothing to restore. Use --all to reopen closed sessions too.");
            return 0;
        }

        foreach (var s in toRestore)
        {
            var missing = s.Status != SessionStatus.New && Transcripts.Find(s.SessionId, s.TranscriptPath) is null;
            Console.WriteLine($"  {s.Name}{(missing ? " (no transcript found; starts fresh)" : "")}");
        }

        if (dryRun)
            return 0;

        WindowsTerminal.OpenTabs(toRestore, newWindow || !WindowsTerminal.IsHosting);
        Console.WriteLine($"Restored {toRestore.Count} session{(toRestore.Count == 1 ? "" : "s")}.");
        return 0;
    }

    public static int List(bool all)
    {
        var sessions = SessionStore.Default.ReadAll();
        if (sessions.Count == 0)
        {
            Console.WriteLine("No sessions yet. Start one with: cb new \"<name>\"");
            return 0;
        }

        var useColour = !Console.IsOutputRedirected;
        var nameWidth = Math.Max(4, sessions.Max(s => s.Name.Length));
        var workWidth = Math.Max(4, sessions.Max(s => s.Work?.Length ?? 0));
        Console.WriteLine($"{"#",3}  {"Name".PadRight(nameWidth)}  {"Work".PadRight(workWidth)}  {"Status",-12}  {"Last active",-16}  Folder");
        for (var i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            var status = ProcessLiveness.EffectiveStatus(s);
            if (!all && status is SessionStatus.Exited or SessionStatus.Closed && s.LastActiveAt < DateTimeOffset.Now.AddDays(-14))
                continue;

            var work = (s.Work ?? "").PadRight(workWidth);
            if (useColour && s.Colour is { } colour)
                work = Colours.Ansi(colour) + work + Colours.Reset;

            Console.WriteLine($"{i + 1,3}  {s.Name.PadRight(nameWidth)}  {work}  {status,-12}  {s.LastActiveAt:yyyy-MM-dd HH:mm}  {s.Cwd}");
        }
        return 0;
    }

    public static int Rename(string query, string newName)
    {
        newName = NameClash.CleanName(newName);
        if (newName.Length == 0)
            return Fail("The name can't be empty.");

        var store = SessionStore.Default;
        var target = SessionResolver.Resolve(store.ReadAll(), query);
        var error = store.Update(file =>
        {
            if (FindByName(file.Sessions, newName) is { } other && other.Id != target.Id)
                return $"A session called '{other.Name}' already exists.";
            if (file.Sessions.Find(s => s.Id == target.Id) is not { } record)
                return $"'{target.Name}' has just been removed.";
            record.Name = newName;
            return null;
        });

        if (error is not null)
            return Fail(error);

        Console.WriteLine(ProcessLiveness.IsAlive(target)
            ? $"Renamed to '{newName}'. The tab title changes next time it's resumed."
            : $"Renamed to '{newName}'.");
        return 0;
    }

    public static int Remove(string query, bool forgetTranscript)
    {
        var store = SessionStore.Default;
        var record = SessionResolver.Resolve(store.ReadAll(), query);
        if (ProcessLiveness.IsAlive(record))
            return Fail($"'{record.Name}' is running. Exit it first.");

        store.Update(f => f.Sessions.RemoveAll(s => s.Id == record.Id));

        if (forgetTranscript && Transcripts.Find(record.SessionId, record.TranscriptPath) is { } transcript)
        {
            File.Delete(transcript);
            Console.WriteLine($"Removed '{record.Name}' and deleted its transcript.");
        }
        else
        {
            Console.WriteLine($"Removed '{record.Name}'. Its Claude transcript is kept.");
        }
        return 0;
    }

    private static ClashAction? AskAboutClash(SessionRecord existing)
    {
        Console.WriteLine($"A session called '{existing.Name}' already exists " +
                          $"({ProcessLiveness.EffectiveStatus(existing)}, last active {existing.LastActiveAt:yyyy-MM-dd HH:mm}, {existing.Cwd}).");
        return Prompt.Choose(
            ('R', "Resume it"),
            ('D', "Delete it and start a new one"),
            ('N', "Start a new one with a different name"),
            ('C', "Cancel")) switch
        {
            'R' => ClashAction.Resume,
            'D' => ClashAction.Replace,
            'N' => ClashAction.Rename,
            _ => null,
        };
    }

    private static SessionRecord? FindByName(IEnumerable<SessionRecord> sessions, string name) =>
        sessions.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static int Launch(SessionStore store, Config config, SessionRecord record, bool here)
    {
        if (here || !WindowsTerminal.IsHosting)
            return new ClaudeLauncher(store, config).Run(record);

        WindowsTerminal.OpenTabs([record]);
        Console.WriteLine($"Opened '{record.Name}' in a new tab.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}

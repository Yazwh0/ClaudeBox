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

/// <summary>What `cb restore` does with sessions whose Claude transcript has gone.</summary>
internal enum MissingAction { Fresh, Skip, Remove }

internal static class SessionCommands
{
    public static MissingAction ParseMissing(string value) => value.Trim().ToLowerInvariant() switch
    {
        "fresh" => MissingAction.Fresh,
        "skip" => MissingAction.Skip,
        "remove" => MissingAction.Remove,
        _ => throw new ConfigException($"Unknown --missing action '{value}'. Use fresh, skip or remove."),
    };

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
                Colour = colour is null ? null : Colours.Validate(colour),
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

    /// <summary>Reopens interrupted sessions; with all, or a work type, every session that isn't running (of that type).</summary>
    public static int Restore(bool all, string? work, bool newWindow, bool dryRun, MissingAction? onMissing)
    {
        var lastShutdown = SystemTimes.LastShutdown();
        var everything = all || work is not null;
        var toRestore = SessionStore.Default.ReadAll()
            .Where(s => SessionFilter.MatchesWork(s, work)
                     && RestorePlanner.IsRestorable(s, everything, ProcessLiveness.IsAlive(s), lastShutdown))
            .ToList();

        if (toRestore.Count == 0)
        {
            Console.WriteLine(work is not null ? $"Nothing to restore: no '{work}' sessions that aren't already running."
                : all ? "Nothing to restore: every session is already running."
                : "Nothing to restore. Use --all to reopen closed sessions too.");
            return 0;
        }

        // A session that never had a message has no transcript yet, which is fine.
        var missing = toRestore
            .Where(s => s.Status != SessionStatus.New && Transcripts.Find(s.SessionId, s.TranscriptPath) is null)
            .ToHashSet();
        foreach (var s in toRestore)
            Console.WriteLine($"  {s.Name}{(missing.Contains(s) ? " (no transcript found)" : "")}");

        if (dryRun)
            return 0;

        if (missing.Count > 0)
        {
            var choice = onMissing ?? (Console.IsInputRedirected ? MissingAction.Fresh : AskAboutMissing(missing.Count));
            switch (choice)
            {
                case null:
                    return 1;
                case MissingAction.Skip:
                    toRestore.RemoveAll(missing.Contains);
                    break;
                case MissingAction.Remove:
                    var ids = missing.Select(s => s.Id).ToHashSet();
                    SessionStore.Default.Update(f => f.Sessions.RemoveAll(s => ids.Contains(s.Id)));
                    toRestore.RemoveAll(missing.Contains);
                    Console.WriteLine($"Removed {missing.Count} session{(missing.Count == 1 ? "" : "s")} without a transcript.");
                    break;
            }

            if (toRestore.Count == 0)
            {
                Console.WriteLine("Nothing else to restore.");
                return 0;
            }
        }

        WindowsTerminal.OpenTabs(toRestore, newWindow || !WindowsTerminal.IsHosting);
        Console.WriteLine($"Restored {toRestore.Count} session{(toRestore.Count == 1 ? "" : "s")}.");
        return 0;
    }

    public static int List(bool all)
    {
        var sessions = SessionStore.Default.ReadAll();
        if (sessions.Count == 0)
        {
            Console.WriteLine("No sessions yet. Run cb to start one here, or cb new \"<name>\".");
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

    /// <summary>
    /// Adds a Claude session started outside cb, by session ID (or prefix), or picked from this
    /// folder's recent ones, then resumes it in a tab unless noOpen.
    /// </summary>
    public static int Adopt(string? sessionId, string? name, string? work, string? colour, bool here, bool noOpen)
    {
        var config = Config.Load();
        var store = SessionStore.Default;
        var sessions = store.ReadAll();
        var known = sessions.Select(s => s.SessionId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        TranscriptInfo? transcript;
        if (sessionId is null)
        {
            transcript = PickTranscript(known);
            if (transcript is null)
                return 1;
        }
        else
        {
            var matches = Transcripts.FindByPrefix(sessionId.Trim());
            if (matches.Count == 0)
                return Fail($"No Claude session found with ID '{sessionId}'.");
            if (matches.Count > 1)
                return Fail($"'{sessionId}' matches {matches.Count} sessions. Give more of the ID.");
            transcript = Transcripts.Read(matches[0]);
        }

        if (sessions.FirstOrDefault(s => s.SessionId.Equals(transcript.SessionId, StringComparison.OrdinalIgnoreCase)) is { } existing)
            return Fail($"That session is already in cb as '{existing.Name}'. Use cb resume \"{existing.Name}\".");
        if (transcript.Cwd is null)
            return Fail("The transcript doesn't say which folder the session ran in.");
        if (!Directory.Exists(transcript.Cwd))
            return Fail($"The session's folder no longer exists: {transcript.Cwd}");

        work ??= config.DefaultWork;
        var workType = work is null ? null : config.FindWork(work);
        if (work is not null && workType is null)
            return Fail($"Unknown work type '{work}'. Configured: {string.Join(", ", config.Work.Keys)}.");

        var wanted = NameClash.CleanName(name ?? Shorten(transcript.Title) ?? config.DefaultName.Replace("{folder}", Path.GetFileName(transcript.Cwd)));
        if (wanted.Length == 0)
            return Fail("The name can't be empty.");

        var record = store.Update(file =>
        {
            if (file.Sessions.Any(s => s.SessionId.Equals(transcript.SessionId, StringComparison.OrdinalIgnoreCase)))
                return null;

            var free = FindByName(file.Sessions, wanted) is null
                ? wanted
                : NameClash.NextFreeName(wanted, config.NameClash.RenamePattern, file.Sessions.Select(s => s.Name));
            var adopted = new SessionRecord
            {
                Name = free,
                Cwd = transcript.Cwd,
                SessionId = transcript.SessionId,
                TranscriptPath = transcript.Path,
                Work = workType is null ? null : work,
                Colour = (colour ?? workType?.Colour) is { } c ? Colours.Validate(c) : null,
                Scheme = workType?.Scheme,
                Status = SessionStatus.Exited,
                LastActiveAt = transcript.LastWritten,
                ClosedAt = transcript.LastWritten,
            };
            file.Sessions.Add(adopted);
            return adopted;
        });

        if (record is null)
            return Fail("That session was just added to cb elsewhere.");

        Console.WriteLine($"Added '{record.Name}' ({record.Cwd}).");
        return noOpen ? 0 : Launch(store, config, record, here);
    }

    /// <summary>Offers this folder's most recent Claude sessions that cb doesn't know about.</summary>
    private static TranscriptInfo? PickTranscript(HashSet<string> known)
    {
        var cwd = Environment.CurrentDirectory;
        var candidates = Transcripts.ForFolder(cwd)
            .Where(path => !known.Contains(Path.GetFileNameWithoutExtension(path)))
            .Take(9)
            .Select(Transcripts.Read)
            .ToList();

        if (candidates.Count == 0)
        {
            Console.Error.WriteLine($"No Claude sessions in {cwd} that cb doesn't already have. Give a session ID to adopt one from elsewhere.");
            return null;
        }
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Give the session ID to adopt (there's no one to ask).");
            return null;
        }

        Console.WriteLine($"Claude sessions in {cwd}:");
        var options = candidates
            .Select((t, i) => ((char)('1' + i), $"{t.LastWritten:yyyy-MM-dd HH:mm}  {Shorten(t.Title) ?? "(untitled)"}  {t.SessionId[..8]}"))
            .Append(('C', "Cancel"))
            .ToArray();
        var key = Prompt.Choose(options);
        return key == 'C' ? null : candidates[key - '1'];
    }

    private static string? Shorten(string? title) =>
        string.IsNullOrWhiteSpace(title) ? null : title.Length <= 50 ? title.Trim() : title[..47].Trim() + "...";

    /// <summary>`cb` on its own: a new session in this folder, named from config ("Claude", "Claude 2", ...).</summary>
    public static int Quick(bool here)
    {
        var config = Config.Load();
        var folder = new DirectoryInfo(Environment.CurrentDirectory).Name.TrimEnd('\\', ':');
        var name = config.DefaultName.Replace("{folder}", folder.Length > 0 ? folder : "session");

        return New(new NewOptions(name, null, null, null, here, [], ClashAction.Rename, null));
    }

    public static int Set(string query, SessionChanges changes)
    {
        if (changes.IsEmpty)
            return Fail("Nothing to change. Use --name, --work, --colour or --scheme.");

        var config = Config.Load();
        var store = SessionStore.Default;
        var target = SessionResolver.Resolve(store.ReadAll(), query);
        if (store.Update(file => changes.ApplyTo(file, target.Id, config)) is { } error)
            return Fail(error);

        var name = changes.Name is null ? target.Name : NameClash.CleanName(changes.Name);
        Console.WriteLine(ProcessLiveness.IsAlive(target)
            ? $"Updated '{name}'. Its tab changes next time it's opened."
            : $"Updated '{name}'.");
        return 0;
    }

    /// <summary>
    /// Removes one session, or in bulk: with exited, every session that was deliberately ended;
    /// with a work type, every session of that type that isn't running. Both together narrow it.
    /// </summary>
    public static int Remove(string? query, bool exited, string? work, bool yes, bool forgetTranscript)
    {
        var bulk = exited || work is not null;
        if (query is null == !bulk)
            return Fail("Give either a session to remove, or --exited and/or --work.");

        var store = SessionStore.Default;
        var sessions = store.ReadAll();
        List<SessionRecord> targets;

        if (bulk)
        {
            targets = sessions.Where(s => SessionFilter.IsRemovable(s, ProcessLiveness.IsAlive(s), exited, work)).ToList();
            if (targets.Count == 0)
            {
                Console.WriteLine(work is null ? "No exited sessions." : $"No '{work}' sessions to remove.");
                return 0;
            }

            foreach (var s in targets)
                Console.WriteLine($"  {s.Name}  ({ProcessLiveness.EffectiveStatus(s)}, last active {s.LastActiveAt:yyyy-MM-dd HH:mm})");

            if (!yes)
            {
                if (Console.IsInputRedirected)
                    return Fail("Add --yes to remove these without asking.");
                Console.WriteLine($"Remove {targets.Count} session{(targets.Count == 1 ? "" : "s")}?");
                if (Prompt.Choose(('Y', "Yes"), ('N', "No")) != 'Y')
                    return 1;
            }
        }
        else
        {
            var record = SessionResolver.Resolve(sessions, query!);
            if (ProcessLiveness.IsAlive(record))
                return Fail($"'{record.Name}' is running. Exit it first.");
            targets = [record];
        }

        var ids = targets.Select(s => s.Id).ToHashSet();
        store.Update(f => f.Sessions.RemoveAll(s => ids.Contains(s.Id)));

        var deleted = 0;
        if (forgetTranscript)
        {
            foreach (var s in targets)
            {
                if (Transcripts.Find(s.SessionId, s.TranscriptPath) is { } transcript)
                {
                    File.Delete(transcript);
                    deleted++;
                }
            }
        }

        var what = targets.Count == 1 ? $"'{targets[0].Name}'" : $"{targets.Count} sessions";
        Console.WriteLine(forgetTranscript
            ? $"Removed {what} and deleted {deleted} transcript{(deleted == 1 ? "" : "s")}."
            : $"Removed {what}. Claude's transcripts are kept.");
        return 0;
    }

    private static MissingAction? AskAboutMissing(int count)
    {
        Console.WriteLine(count == 1
            ? "Claude's transcript for 1 session is gone, so it can't carry on where it left off."
            : $"Claude's transcripts for {count} sessions are gone, so they can't carry on where they left off.");
        return Prompt.Choose(
            ('F', "Start them as new conversations"),
            ('S', "Skip them this time"),
            ('R', "Remove them from cb"),
            ('C', "Cancel")) switch
        {
            'F' => MissingAction.Fresh,
            'S' => MissingAction.Skip,
            'R' => MissingAction.Remove,
            _ => null,
        };
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

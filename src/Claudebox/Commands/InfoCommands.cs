using System.Diagnostics;
using System.Globalization;
using Claudebox.Launch;
using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Commands;

/// <summary>`cb info` (everything about one session) and `cb doctor` (is everything cb needs working).</summary>
internal static class InfoCommands
{
    public static int Info(string query)
    {
        var config = Config.Load();
        var sessions = SessionStore.Default.ReadAll();
        var s = SessionResolver.Resolve(sessions, query);
        var status = ProcessLiveness.EffectiveStatus(s, RestorePlanner.ClosedTogether(sessions).Contains(s.Id));
        var transcriptPath = Transcripts.Find(s.SessionId, s.TranscriptPath);
        var title = transcriptPath is null ? null : Transcripts.Read(transcriptPath).Title;
        var useColour = !Console.IsOutputRedirected;
        var now = DateTimeOffset.Now;

        Row("Name", s.Name);
        Row("Number", $"{sessions.IndexOf(s) + 1} (id {s.ShortId})");
        Row("Status", status + (ProcessLiveness.IsAlive(s) ? $" (pid {s.Pid})" : ""));
        Row("Title", title ?? "-");
        Row("Work", s.Work ?? "none");
        Row("Colour", s.Colour is null ? "none" : useColour ? $"{Colours.Ansi(s.Colour)}■{Colours.Reset} {s.Colour}" : s.Colour);
        Row("Scheme", s.Scheme ?? "none");
        Row("Folder", s.Cwd + (Directory.Exists(s.Cwd) ? "" : " (no longer exists)"));
        Row("Session ID", s.SessionId);
        if (s.PreviousSessionIds.Count > 0)
            Row("Before /clear", string.Join(", ", s.PreviousSessionIds));
        Row("Transcript", transcriptPath ?? (s.Status == SessionStatus.New ? "none yet (no messages)" : "not found"));

        // In the order they're passed to claude, so the later ones win.
        List<string> args =
        [
            .. Labelled(config.ClaudeArgs, "config"),
            .. Labelled((s.Work is { } work ? config.FindWork(work)?.ClaudeArgs : null) ?? [], "work type"),
            .. Labelled(s.ClaudeArgs, "this session"),
        ];
        Row("Claude args", args.Count == 0 ? "none" : args[0]);
        foreach (var line in args.Skip(1))
            Row("", line);

        Row("Created", $"{s.CreatedAt:yyyy-MM-dd HH:mm}");
        Row("Last active", $"{s.LastActiveAt:yyyy-MM-dd HH:mm} ({TimeText.Ago(s.LastActiveAt, now)})");
        if (s.ClosedAt is { } closed)
            Row("Closed", $"{closed:yyyy-MM-dd HH:mm}");
        return 0;

        static IEnumerable<string> Labelled(List<string> args, string from) =>
            args.Count == 0 ? [] : [$"{string.Join(" ", args.Select(Quote))}  ({from})"];
    }

    private static void Row(string label, string value) => Console.WriteLine($"{label,-14}{value}");

    private static string Quote(string arg) => arg.Contains(' ') ? $"\"{arg}\"" : arg;

    public static int Doctor()
    {
        var useColour = !Console.IsOutputRedirected;
        var problems = 0;

        Ok($".NET {Environment.Version}");

        Config? config = null;
        try
        {
            config = Config.Load();
            Ok($"Settings load ({AppPaths.Config})");
        }
        catch (ConfigException e)
        {
            Problem(e.Message);
        }

        try
        {
            var claude = ClaudeLocator.Find(config?.ClaudePath);
            var version = ClaudeVersion(claude);
            Check(version is not null, $"Claude {version} at {claude}", $"Couldn't run {claude} --version");
        }
        catch (FileNotFoundException e)
        {
            Problem(e.Message);
        }

        Check(OnPath("wt.exe"), "Windows Terminal (wt.exe) is on PATH", "Windows Terminal (wt.exe) isn't on PATH, so cb can't open tabs");
        if (!WindowsTerminal.IsHosting)
            Note("This console isn't Windows Terminal, so cb runs sessions here rather than in new tabs");

        List<SessionRecord> sessions = [];
        try
        {
            sessions = SessionStore.Default.ReadAll();
            Ok($"{sessions.Count} session{Plural(sessions.Count)} ({AppPaths.Sessions})");
        }
        catch (SessionStoreException e)
        {
            Problem(e.Message);
        }

        var gone = sessions.Where(s => !Directory.Exists(s.Cwd)).Select(s => s.Name).ToList();
        Check(gone.Count == 0, "Every session's folder exists",
            $"{gone.Count} session{Plural(gone.Count)} in folders that no longer exist: {string.Join(", ", gone)}");

        var noTranscript = sessions
            .Where(s => s.Status != SessionStatus.New && Transcripts.Find(s.SessionId, s.TranscriptPath) is null)
            .Select(s => s.Name).ToList();
        Check(noTranscript.Count == 0, "Every session's transcript is there",
            $"{noTranscript.Count} session{Plural(noTranscript.Count)} without a transcript, so they'd start a new conversation: {string.Join(", ", noTranscript)}");

        Check(Directory.Exists(AppPaths.ClaudeProjects), $"Claude's transcripts are where cb expects ({AppPaths.ClaudeProjects})",
            $"Claude's transcripts folder isn't there ({AppPaths.ClaudeProjects})");

        // An undocumented Claude file (see RunningClaude), so this is the check most likely to break.
        if (Directory.Exists(AppPaths.ClaudeSessions))
        {
            var files = Directory.GetFiles(AppPaths.ClaudeSessions, "*.json");
            var understood = files.Count(f => ReadOrNull(f) is { } json && RunningClaude.Parse(json) is not null);
            Check(files.Length == 0 || understood > 0, $"cb understands Claude's running-session files ({understood} of {files.Length})",
                "cb doesn't understand any of Claude's running-session files, so cb adopt --running and spotting sessions running outside cb won't work");
        }

        if (OperatingSystem.IsWindows())
            Note(StartupCommands.Current() is null ? "cb restore doesn't run when you log in (cb startup enable)" : "cb restore runs when you log in");

        if (Directory.Exists(AppPaths.Bin))
        {
            var copies = Directory.GetDirectories(AppPaths.Bin).Length;
            var bytes = Directory.EnumerateFiles(AppPaths.Bin, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            Note($"{copies} private cop{(copies == 1 ? "y" : "ies")} of cb for open tabs ({bytes / 1024 / 1024} MB in {AppPaths.Bin})");
        }

        var failures = RecentLogProblems(TimeSpan.FromDays(7));
        Check(failures.Count == 0, "No hook or cleanup problems logged in the last week",
            $"{failures.Count} problem{Plural(failures.Count)} logged in the last week, most recently: {Shorten(failures.LastOrDefault())} (see {AppPaths.Log})");

        Console.WriteLine();
        Console.WriteLine(problems == 0 ? "No problems found." : $"{problems} problem{Plural(problems)} found.");
        return problems == 0 ? 0 : 1;

        void Check(bool good, string ok, string problem)
        {
            if (good)
                Ok(ok);
            else
                Problem(problem);
        }

        void Ok(string message) => Line("ok", "\e[32m", message);
        void Note(string message) => Line("-", "\e[90m", message);

        void Problem(string message)
        {
            problems++;
            Line("!!", "\e[33m", message);
        }

        void Line(string mark, string ansi, string message) =>
            Console.WriteLine(useColour ? $"  {ansi}{mark,-3}{Colours.Reset}{message}" : $"  {mark,-3}{message}");
    }

    private static string? Shorten(string? text) => text is { Length: > 150 } ? text[..147] + "..." : text;

    private static string Plural(int count) => count == 1 ? "" : "s";

    private static string? ClaudeVersion(string claude)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(claude, "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
                return null;
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(TimeSpan.FromSeconds(20)))
            {
                process.Kill();
                return null;
            }
            return process.ExitCode == 0 ? output.Result.Trim() : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static bool OnPath(string file) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(dir => File.Exists(Path.Combine(dir, file)));

    private static string? ReadOrNull(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Log entries within the window. Everything cb logs is a problem of some kind.</summary>
    internal static List<string> RecentLogProblems(TimeSpan window)
    {
        if (!File.Exists(AppPaths.Log))
            return [];

        var since = DateTime.Now - window;
        return File.ReadLines(AppPaths.Log)
            .Where(line => line.Length > 19
                && DateTime.TryParseExact(line[..19], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
                && at >= since)
            // Drop the "[pid] " after the time.
            .Select(line => line[20..] is var rest && rest.StartsWith('[') && rest.IndexOf("] ", StringComparison.Ordinal) is > 0 and var end ? rest[(end + 2)..] : rest)
            .ToList();
    }
}

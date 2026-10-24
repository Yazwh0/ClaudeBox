using System.Text.Json;

namespace Claudebox.Launch;

/// <summary>What `cb adopt` needs from a transcript: where it ran and what it's about.</summary>
internal sealed record TranscriptInfo(string SessionId, string Path, string? Cwd, string? Title, DateTime LastWritten);

/// <summary>Claude Code keeps each conversation at ~/.claude/projects/&lt;encoded cwd&gt;/&lt;session id&gt;.jsonl.</summary>
internal static class Transcripts
{
    /// <summary>Searches every project folder rather than re-deriving Claude's path encoding.</summary>
    public static string? Find(string sessionId, string? knownPath = null)
    {
        if (knownPath is not null && File.Exists(knownPath))
            return knownPath;

        if (!Directory.Exists(AppPaths.ClaudeProjects))
            return null;

        var fileName = sessionId + ".jsonl";
        return Directory.EnumerateDirectories(AppPaths.ClaudeProjects)
            .Select(dir => Path.Combine(dir, fileName))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>Every transcript whose session ID starts with the prefix, in any project folder.</summary>
    public static List<string> FindByPrefix(string prefix) =>
        Directory.Exists(AppPaths.ClaudeProjects)
            ? Directory.EnumerateDirectories(AppPaths.ClaudeProjects)
                .SelectMany(dir => Directory.EnumerateFiles(dir, prefix + "*.jsonl"))
                .ToList()
            : [];

    /// <summary>Transcripts for one folder, newest first. Folder names differ only in case, so all matches are used.</summary>
    public static List<string> ForFolder(string cwd)
    {
        if (!Directory.Exists(AppPaths.ClaudeProjects))
            return [];

        var encoded = ProjectFolderName(cwd);
        return Directory.EnumerateDirectories(AppPaths.ClaudeProjects)
            .Where(dir => Path.GetFileName(dir).Equals(encoded, StringComparison.OrdinalIgnoreCase))
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.jsonl"))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    /// <summary>Claude's encoding: everything but letters and digits becomes '-', so C:\src\app is C--src-app.</summary>
    internal static string ProjectFolderName(string cwd) =>
        string.Concat(Path.TrimEndingDirectorySeparator(cwd).Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-'));

    /// <summary>
    /// The first cwd in the transcript and its latest title (one you gave with /rename, else
    /// Claude's own). Only lines that mention those are parsed, as transcripts get large.
    /// </summary>
    public static TranscriptInfo Read(string path)
    {
        string? cwd = null, customTitle = null, aiTitle = null;
        foreach (var line in File.ReadLines(path))
        {
            var wanted = (cwd is null && line.Contains("\"cwd\"")) || line.Contains("-title\"");
            if (!wanted)
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (cwd is null && root.TryGetProperty("cwd", out var c) && c.ValueKind == JsonValueKind.String)
                    cwd = c.GetString();
                if (root.TryGetProperty("customTitle", out var t) && t.ValueKind == JsonValueKind.String)
                    customTitle = t.GetString();
                if (root.TryGetProperty("aiTitle", out var a) && a.ValueKind == JsonValueKind.String)
                    aiTitle = a.GetString();
            }
            catch (JsonException)
            {
                // A line being written as we read, or one we don't understand.
            }
        }

        return new TranscriptInfo(Path.GetFileNameWithoutExtension(path), path, cwd,
            customTitle ?? aiTitle, File.GetLastWriteTime(path));
    }
}

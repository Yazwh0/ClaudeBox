namespace Claudebox.Launch;

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
}

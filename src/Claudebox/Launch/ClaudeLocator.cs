namespace Claudebox.Launch;

/// <summary>Finds the claude executable: CLAUDEBOX_CLAUDE override first, then PATH.</summary>
internal static class ClaudeLocator
{
    private static readonly string[] Extensions = [".exe", ".cmd", ".bat"];

    public static string Find()
    {
        if (Environment.GetEnvironmentVariable("CLAUDEBOX_CLAUDE") is { Length: > 0 } overridePath)
            return overridePath;

        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var ext in Extensions)
        foreach (var dir in dirs)
        {
            var candidate = Path.Combine(dir, "claude" + ext);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Could not find 'claude' on PATH. Set CLAUDEBOX_CLAUDE to its full path.");
    }
}

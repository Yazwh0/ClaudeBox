namespace Claudebox.Launch;

/// <summary>Finds the claude executable: CLAUDEBOX_CLAUDE, then config's claudePath, then PATH.</summary>
internal static class ClaudeLocator
{
    private static readonly string[] Extensions = [".exe", ".cmd", ".bat"];

    public static string Find(string? configuredPath)
    {
        if (Environment.GetEnvironmentVariable("CLAUDEBOX_CLAUDE") is { Length: > 0 } overridePath)
            return overridePath;
        if (!string.IsNullOrEmpty(configuredPath))
            return configuredPath;

        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var ext in Extensions)
        foreach (var dir in dirs)
        {
            var candidate = Path.Combine(dir, "claude" + ext);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Couldn't find 'claude' on PATH. Set claudePath in config.json.");
    }
}

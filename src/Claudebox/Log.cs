namespace Claudebox;

/// <summary>Best-effort log for things that mustn't fail loudly (hooks run inside Claude).</summary>
internal static class Log
{
    /// <summary>Past this, the log becomes cb.log.old (replacing the last one) and a new one starts.</summary>
    private const long MaxBytes = 1024 * 1024;

    /// <summary>To cb.log, or another log (e.g. beside a test's own files).</summary>
    public static void Write(string message, string? path = null)
    {
        path ??= AppPaths.Log;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var log = new FileInfo(path);
            if (log.Exists && log.Length > MaxBytes)
                log.MoveTo(path + ".old", overwrite: true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}

namespace Claudebox;

/// <summary>Best-effort log for things that mustn't fail loudly (hooks run inside Claude).</summary>
internal static class Log
{
    /// <summary>Past this, the log becomes cb.log.old (replacing the last one) and a new one starts.</summary>
    private const long MaxBytes = 1024 * 1024;

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            var log = new FileInfo(AppPaths.Log);
            if (log.Exists && log.Length > MaxBytes)
                log.MoveTo(AppPaths.Log + ".old", overwrite: true);
            File.AppendAllText(AppPaths.Log, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}

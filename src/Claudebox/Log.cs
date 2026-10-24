namespace Claudebox;

/// <summary>Best-effort log for things that mustn't fail loudly (hooks run inside Claude).</summary>
internal static class Log
{
    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.AppendAllText(AppPaths.Log, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }
}

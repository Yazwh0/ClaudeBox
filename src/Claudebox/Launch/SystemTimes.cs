namespace Claudebox.Launch;

internal static class SystemTimes
{
    /// <summary>When Windows last shut down, as recorded in HKLM\...\Control\Windows\ShutdownTime (a FILETIME).</summary>
    public static DateTimeOffset? LastShutdown()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Windows");
            return key?.GetValue("ShutdownTime") is byte[] { Length: 8 } bytes
                ? DateTimeOffset.FromFileTime(BitConverter.ToInt64(bytes))
                : null;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}

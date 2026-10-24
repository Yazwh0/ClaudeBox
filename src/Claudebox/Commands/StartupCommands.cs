using System.Runtime.Versioning;

namespace Claudebox.Commands;

/// <summary>
/// `cb startup`: runs `cb restore` when you log in, from the per-user Run key (it shows in Task
/// Manager's Startup apps). The restore runs inside a new Windows Terminal window, so the
/// sessions open as tabs there and cb's own tab closes when it's done.
/// </summary>
internal static class StartupCommands
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudeBox";

    [SupportedOSPlatform("windows")]
    public static int Show()
    {
        Console.WriteLine(Current() is { } command
            ? $"cb restore runs when you log in:\n  {command}\nTurn it off with cb startup disable."
            : "cb restore doesn't run when you log in. Turn it on with cb startup enable.");
        return 0;
    }

    [SupportedOSPlatform("windows")]
    public static int Enable()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
        var command = BuildCommand(InstalledCb());
        key.SetValue(ValueName, command);
        Console.WriteLine($"cb restore will run when you log in:\n  {command}");
        return 0;
    }

    [SupportedOSPlatform("windows")]
    public static int Disable()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is null)
        {
            Console.WriteLine("cb restore wasn't set to run when you log in.");
            return 0;
        }

        key.DeleteValue(ValueName);
        Console.WriteLine("cb restore won't run when you log in any more.");
        return 0;
    }

    [SupportedOSPlatform("windows")]
    private static string? Current()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string;
    }

    internal static string BuildCommand(string cb) => $"wt.exe -w new \"{cb}\" restore";

    /// <summary>
    /// The installed tool's shim, which stays put across updates; the running copy may be a
    /// private copy or a build folder that goes away. Falls back to cb on PATH.
    /// </summary>
    private static string InstalledCb()
    {
        var shim = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "cb.exe");
        return File.Exists(shim) ? shim : "cb";
    }
}

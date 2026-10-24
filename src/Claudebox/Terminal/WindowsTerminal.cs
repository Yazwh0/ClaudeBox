using System.Diagnostics;
using Claudebox.Registry;

namespace Claudebox.Terminal;

internal static class WindowsTerminal
{
    /// <summary>Windows Terminal sets WT_SESSION in every shell it hosts.</summary>
    public static bool IsHosting => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION"));

    /// <summary>Opens a tab in the current window that runs `cb run &lt;id&gt;` for the session.</summary>
    public static void OpenTab(SessionRecord record)
    {
        var info = new ProcessStartInfo("wt.exe") { UseShellExecute = false };
        foreach (var arg in BuildNewTabArguments(record, Environment.ProcessPath!))
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Failed to start Windows Terminal (wt.exe).");
    }

    internal static List<string> BuildNewTabArguments(SessionRecord record, string cbPath)
    {
        List<string> args = ["-w", "0", "new-tab", "--title", Escape(record.Name), "--suppressApplicationTitle"];
        if (record.Colour is { } colour)
            args.AddRange(["--tabColor", colour]);
        if (record.Scheme is { } scheme)
            args.AddRange(["--colorScheme", Escape(scheme)]);
        args.AddRange(["-d", Escape(record.Cwd), Escape(cbPath), "run", record.Id]);
        return args;
    }

    /// <summary>wt treats a bare ';' as a separator between its subcommands.</summary>
    internal static string Escape(string value) => value.Replace(";", "\\;");
}

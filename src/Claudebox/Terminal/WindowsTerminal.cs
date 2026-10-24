using System.Diagnostics;
using Claudebox.Registry;

namespace Claudebox.Terminal;

internal static class WindowsTerminal
{
    /// <summary>Windows Terminal sets WT_SESSION in every shell it hosts.</summary>
    public static bool IsHosting => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION"));

    /// <summary>Opens one tab per session, each running `cb run &lt;id&gt;`, in a single wt call.</summary>
    public static void OpenTabs(IReadOnlyList<SessionRecord> records, bool newWindow = false)
    {
        var info = new ProcessStartInfo("wt.exe") { UseShellExecute = false };
        foreach (var arg in BuildArguments(records, Environment.ProcessPath!, newWindow))
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Failed to start Windows Terminal (wt.exe).");
    }

    internal static List<string> BuildArguments(IReadOnlyList<SessionRecord> records, string cbPath, bool newWindow)
    {
        List<string> args = ["-w", newWindow ? "new" : "0"];
        for (var i = 0; i < records.Count; i++)
        {
            if (i > 0)
                args.Add(";");
            args.AddRange(BuildTabArguments(records[i], cbPath));
        }
        return args;
    }

    internal static List<string> BuildTabArguments(SessionRecord record, string cbPath)
    {
        List<string> args = ["new-tab", "--title", Escape(record.Name), "--suppressApplicationTitle"];
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

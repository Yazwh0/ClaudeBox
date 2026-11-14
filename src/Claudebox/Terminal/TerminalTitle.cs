using System.Runtime.InteropServices;
using Claudebox.Registry;

namespace Claudebox.Terminal;

internal static class TerminalTitle
{
    /// <summary>OSC 0 sets the tab/window title in Windows Terminal and most other terminals.</summary>
    public static void Set(string title)
    {
        if (!Console.IsOutputRedirected)
            Console.Write(Sequence(title));
    }

    /// <summary>The session's name with an icon for its status, as hooks show it in the tab.</summary>
    public static string WithStatus(string name, string status) => status switch
    {
        SessionStatus.Busy => $"● {name}",
        SessionStatus.Waiting => $"○ {name}",
        SessionStatus.Attention => $"! {name}",
        _ => name,
    };

    /// <summary>
    /// Sets the title of another process's console. Claude runs hooks with a hidden console of their
    /// own, so a hook has to attach to the tab's console (the cb process running there) to reach it.
    /// A command run from a shell passes reattach to go back to the shell's console afterwards.
    /// </summary>
    public static void SetOnConsoleOf(int pid, string title, bool reattach = false)
    {
        FreeConsole();
        try
        {
            if (!AttachConsole((uint)pid))
                throw new IOException($"Couldn't attach to the console of process {pid} (error {Marshal.GetLastPInvokeError()}).");
            try
            {
                using var console = File.OpenWrite("CONOUT$");
                console.Write(System.Text.Encoding.UTF8.GetBytes(Sequence(title)));
            }
            finally
            {
                FreeConsole();
            }
        }
        finally
        {
            if (reattach)
                AttachConsole(AttachParentProcess);
        }
    }

    private const uint AttachParentProcess = unchecked((uint)-1);

    private static string Sequence(string title) => $"\e]0;{title}\a";

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);
}

namespace Claudebox.Terminal;

internal static class TerminalTitle
{
    /// <summary>OSC 0 sets the tab/window title in Windows Terminal and most other terminals.</summary>
    public static void Set(string title)
    {
        if (!Console.IsOutputRedirected)
            Console.Write($"\e]0;{title}\a");
    }
}

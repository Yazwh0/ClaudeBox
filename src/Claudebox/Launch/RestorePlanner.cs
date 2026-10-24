using Claudebox.Registry;

namespace Claudebox.Launch;

/// <summary>Decides which sessions `cb restore` brings back.</summary>
internal static class RestorePlanner
{
    /// <summary>
    /// Windows Terminal may close its tabs as Windows shuts down, which looks like a deliberate
    /// close. Closes this close to the recorded shutdown time count as interrupted.
    /// </summary>
    internal static readonly TimeSpan ShutdownWindow = TimeSpan.FromSeconds(60);

    public static bool IsRestorable(SessionRecord record, bool all, bool alive, DateTimeOffset? lastShutdown)
    {
        if (alive)
            return false;
        if (all || record.Open)
            return true;

        return record.Status == SessionStatus.Closed
            && record.ClosedAt is { } closedAt
            && lastShutdown is { } shutdown
            && closedAt <= shutdown + TimeSpan.FromSeconds(5)
            && closedAt >= shutdown - ShutdownWindow;
    }
}

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

    /// <summary>
    /// Closing a Windows Terminal window (or the terminal crashing) closes all its tabs at once,
    /// while closing tabs by hand is never this quick. Closes this close together count as one.
    /// </summary>
    internal static readonly TimeSpan TogetherWindow = TimeSpan.FromSeconds(2);

    public static bool IsRestorable(SessionRecord record, bool all, bool alive, DateTimeOffset? lastShutdown,
        bool closedWithOthers = false)
    {
        if (alive)
            return false;
        if (all || record.Open)
            return true;
        if (record.Status != SessionStatus.Closed || record.ClosedAt is not { } closedAt)
            return false;

        return closedWithOthers
            || lastShutdown is { } shutdown
               && closedAt <= shutdown + TimeSpan.FromSeconds(5)
               && closedAt >= shutdown - ShutdownWindow;
    }

    /// <summary>
    /// Ids of closed sessions that went with at least one other, i.e. their whole window was closed
    /// or the terminal crashed. Each close counts if it's within TogetherWindow of the one before.
    /// </summary>
    public static HashSet<string> ClosedTogether(IEnumerable<SessionRecord> records)
    {
        var closed = records
            .Where(r => r.Status == SessionStatus.Closed && r.ClosedAt is not null)
            .OrderBy(r => r.ClosedAt)
            .ToList();

        var together = new HashSet<string>();
        for (var i = 1; i < closed.Count; i++)
        {
            if (closed[i].ClosedAt!.Value - closed[i - 1].ClosedAt!.Value <= TogetherWindow)
            {
                together.Add(closed[i - 1].Id);
                together.Add(closed[i].Id);
            }
        }
        return together;
    }
}

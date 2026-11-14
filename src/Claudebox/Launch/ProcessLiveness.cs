using System.Diagnostics;
using Claudebox.Registry;

namespace Claudebox.Launch;

internal static class ProcessLiveness
{
    /// <summary>
    /// True if the record's Claude process is still running. The start time is compared too,
    /// because Windows reuses process IDs (especially after a reboot).
    /// </summary>
    public static bool IsAlive(SessionRecord record)
    {
        if (record.Pid is not { } pid || record.ProcessStartedAt is not { } startedAt)
            return false;

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited
                && Math.Abs((process.StartTime - startedAt.LocalDateTime).TotalSeconds) < 1;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException
                                     or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The status to show: a session marked open whose process has gone was interrupted. One
    /// that isn't marked open but is alive was adopted while running outside cb. One closed with its
    /// whole window (see RestorePlanner.ClosedTogether) shows as interrupted, since restore brings it back.
    /// </summary>
    public static string EffectiveStatus(SessionRecord record, bool closedWithOthers = false) =>
        (record.Open, IsAlive(record)) switch
        {
            (true, false) => SessionStatus.Interrupted,
            (false, true) => SessionStatus.Running,
            (false, false) when closedWithOthers && record.Status == SessionStatus.Closed => SessionStatus.Interrupted,
            _ => record.Status,
        };
}

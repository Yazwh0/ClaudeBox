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

    /// <summary>The status to show: a session marked open whose process has gone was interrupted.</summary>
    public static string EffectiveStatus(SessionRecord record) =>
        record.Open && !IsAlive(record) ? SessionStatus.Interrupted : record.Status;
}

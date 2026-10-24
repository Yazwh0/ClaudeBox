namespace Claudebox.Registry;

/// <summary>Which sessions the bulk forms of `cb rm` act on.</summary>
internal static class SessionFilter
{
    public static bool MatchesWork(SessionRecord record, string? work) =>
        work is null || string.Equals(record.Work, work, StringComparison.OrdinalIgnoreCase);

    /// <summary>Never a running session. With exitedOnly, only ones deliberately ended (interrupted ones stay restorable).</summary>
    public static bool IsRemovable(SessionRecord record, bool alive, bool exitedOnly, string? work) =>
        !alive && MatchesWork(record, work) && (!exitedOnly || !record.Open);
}

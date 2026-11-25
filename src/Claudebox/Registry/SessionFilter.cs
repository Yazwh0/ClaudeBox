namespace Claudebox.Registry;

/// <summary>Which sessions cb ls shows and the bulk forms of cb rm act on.</summary>
internal static class SessionFilter
{
    public static bool MatchesWork(SessionRecord record, string? work) =>
        work is null || string.Equals(record.Work, work, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Never a running session. With exitedOnly, only ones deliberately ended: interrupted ones, and
    /// ones closed with their whole window, stay restorable.
    /// </summary>
    public static bool IsRemovable(SessionRecord record, bool alive, bool exitedOnly, string? work,
        bool closedWithOthers = false) =>
        !alive && MatchesWork(record, work) && (!exitedOnly || !record.Open && !closedWithOthers);

    /// <summary>True if the session runs in the folder or one inside it.</summary>
    public static bool IsUnder(SessionRecord record, string folder)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var cwd = Path.TrimEndingDirectorySeparator(Path.GetFullPath(record.Cwd));
        return cwd.Equals(root, StringComparison.OrdinalIgnoreCase)
            || cwd.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

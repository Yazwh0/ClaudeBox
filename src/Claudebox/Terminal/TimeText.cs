namespace Claudebox.Terminal;

internal static class TimeText
{
    /// <summary>How long ago, for lists: "just now", "5m ago", "3h ago", "2d ago", then the date.</summary>
    public static string Ago(DateTimeOffset when, DateTimeOffset now)
    {
        var age = now - when;
        return age.TotalMinutes < 1 ? "just now"
            : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m ago"
            : age.TotalDays < 1 ? $"{(int)age.TotalHours}h ago"
            : age.TotalDays < 30 ? $"{(int)age.TotalDays}d ago"
            : when.ToString("yyyy-MM-dd");
    }
}

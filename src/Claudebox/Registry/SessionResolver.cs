namespace Claudebox.Registry;

internal sealed class SessionNotFoundException(string message) : Exception(message);

/// <summary>Finds a session by exact name, list index, unique name prefix or id prefix.</summary>
internal static class SessionResolver
{
    public static SessionRecord Resolve(IReadOnlyList<SessionRecord> sessions, string query)
    {
        var exact = sessions.Where(s => s.Name.Equals(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
            return exact[0];

        if (int.TryParse(query, out var index) && index >= 1 && index <= sessions.Count)
            return sessions[index - 1];

        var byPrefix = sessions
            .Where(s => s.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                     || s.Id.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return byPrefix.Count switch
        {
            1 => byPrefix[0],
            0 => throw new SessionNotFoundException($"No session matches '{query}'. Run 'cb ls' to see them."),
            _ => throw new SessionNotFoundException(
                $"'{query}' matches several sessions: {string.Join(", ", byPrefix.Select(s => s.Name))}"),
        };
    }
}

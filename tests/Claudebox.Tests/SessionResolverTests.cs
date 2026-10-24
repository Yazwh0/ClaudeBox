using Claudebox.Registry;

namespace Claudebox.Tests;

public class SessionResolverTests
{
    private static readonly List<SessionRecord> Sessions =
    [
        new() { Id = "aaaa1111bbbb", Name = "API refactor", Cwd = "C:\\" },
        new() { Id = "cccc2222dddd", Name = "API docs", Cwd = "C:\\" },
        new() { Id = "eeee3333ffff", Name = "Login hotfix", Cwd = "C:\\" },
    ];

    [Theory]
    [InlineData("api refactor", "API refactor")]
    [InlineData("2", "API docs")]
    [InlineData("login", "Login hotfix")]
    [InlineData("eeee", "Login hotfix")]
    public void Resolve_FindsSession(string query, string expected)
    {
        Assert.Equal(expected, SessionResolver.Resolve(Sessions, query).Name);
    }

    [Fact]
    public void Resolve_AmbiguousPrefix_Throws()
    {
        var e = Assert.Throws<SessionNotFoundException>(() => SessionResolver.Resolve(Sessions, "API"));
        Assert.Contains("API refactor", e.Message);
    }

    [Fact]
    public void Resolve_NoMatch_Throws()
    {
        Assert.Throws<SessionNotFoundException>(() => SessionResolver.Resolve(Sessions, "nothing"));
    }
}

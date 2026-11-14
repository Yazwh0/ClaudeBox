using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Tests;

public sealed class ListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(20, "just now")]
    [InlineData(5 * 60, "5m ago")]
    [InlineData(3 * 3600 + 59 * 60, "3h ago")]
    [InlineData(2 * 86400, "2d ago")]
    [InlineData(29 * 86400, "29d ago")]
    [InlineData(40 * 86400, "2026-10-05")]
    public void Ago_ShowsHowLongAgo(int secondsAgo, string expected)
    {
        Assert.Equal(expected, TimeText.Ago(Now.AddSeconds(-secondsAgo), Now));
    }

    [Theory]
    [InlineData(@"C:\src\app", @"C:\src\app", true)]
    [InlineData(@"C:\src\app\api", @"C:\src\app", true)]
    [InlineData(@"c:\SRC\App", @"C:\src\app\", true)]
    [InlineData(@"C:\src\application", @"C:\src\app", false)]
    [InlineData(@"C:\src", @"C:\src\app", false)]
    public void IsUnder_MatchesFolderAndSubfolders(string cwd, string folder, bool expected)
    {
        Assert.Equal(expected, SessionFilter.IsUnder(new SessionRecord { Name = "x", Cwd = cwd }, folder));
    }
}

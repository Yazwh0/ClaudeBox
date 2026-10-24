namespace Claudebox.Tests;

public class AliasTests
{
    private static readonly HashSet<string> Commands = new(["new", "rm", "ls", "alias"], StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Defined = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c"] = "rm --exited --yes",
        ["bug"] = "new \"Bug fix\" --work hotfix",
    };

    [Fact]
    public void Expand_ReplacesTheAliasAndKeepsTheRest()
    {
        Assert.Equal(["rm", "--exited", "--yes", "--forget-transcript"],
            Aliases.Expand(["C", "--forget-transcript"], Defined));
    }

    [Fact]
    public void Expand_KeepsQuotedPartsTogether()
    {
        Assert.Equal(["new", "Bug fix", "--work", "hotfix"], Aliases.Expand(["bug"], Defined));
    }

    [Fact]
    public void Expand_LeavesOtherCommandsAlone()
    {
        Assert.Equal(["ls", "-a"], Aliases.Expand(["ls", "-a"], Defined));
        Assert.Empty(Aliases.Expand([], Defined));
    }

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "-?" }, false)]
    [InlineData(new[] { "RM", "x" }, false)]
    [InlineData(new[] { "c" }, true)]
    public void MightBeAlias_OnlyForUnknownWords(string[] args, bool expected)
    {
        Assert.Equal(expected, Aliases.MightBeAlias(args, Commands));
    }

    [Fact]
    public void Split_HandlesExtraSpacesAndEmptyQuotes()
    {
        Assert.Equal(["new", "", "-w", "x"], Aliases.Split("  new \"\"   -w x "));
    }

    [Theory]
    [InlineData("rm", "ls")]
    [InlineData("two words", "ls")]
    [InlineData("-x", "ls")]
    [InlineData("x", "")]
    [InlineData("x", "c")]
    [InlineData("x", "new \"oops")]
    public void Check_RejectsBadAliases(string name, string command)
    {
        Assert.Throws<ConfigException>(() => Aliases.Check(name, command, Commands));
    }

    [Fact]
    public void SetAlias_AddsThenUpdates()
    {
        var config = new Config();

        Assert.True(config.SetAlias(" c ", " rm --exited --yes ", Commands));
        Assert.False(config.SetAlias("C", "rm --exited", Commands));

        var (name, command) = Assert.Single(config.Aliases);
        Assert.Equal("C", name);
        Assert.Equal("rm --exited", command);
    }
}

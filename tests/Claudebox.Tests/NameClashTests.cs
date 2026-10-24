using Claudebox.Registry;

namespace Claudebox.Tests;

public class NameClashTests
{
    [Theory]
    [InlineData("'foo'", "foo")]
    [InlineData("  foo  ", "foo")]
    [InlineData("it's", "it's")]
    [InlineData("'", "'")]
    public void CleanName_StripsOnePairOfSingleQuotes(string input, string expected)
    {
        Assert.Equal(expected, NameClash.CleanName(input));
    }

    [Fact]
    public void NextFreeName_SkipsTakenNames()
    {
        Assert.Equal("foo 4", NameClash.NextFreeName("foo", "{name} {n}", ["foo", "foo 2", "FOO 3"]));
    }

    [Fact]
    public void NextFreeName_UsesPattern()
    {
        Assert.Equal("foo-2", NameClash.NextFreeName("foo", "{name}-{n}", ["foo"]));
    }

    [Fact]
    public void NextFreeName_PatternWithoutNumber_Throws()
    {
        Assert.Throws<ConfigException>(() => NameClash.NextFreeName("foo", "{name} copy", []));
    }

    [Fact]
    public void Choose_FlagBeatsConfig()
    {
        var config = new NameClashConfig { Action = "fail" };
        Assert.Equal(ClashAction.Rename, NameClash.Choose(ClashAction.Rename, config, interactive: true));
    }

    [Fact]
    public void Choose_UsesConfigAction()
    {
        var config = new NameClashConfig { Action = "resume" };
        Assert.Equal(ClashAction.Resume, NameClash.Choose(null, config, interactive: false));
    }

    [Fact]
    public void Choose_AskWithoutTerminal_FallsBackToNonInteractive()
    {
        var config = new NameClashConfig { Action = "ask", NonInteractive = "rename" };
        Assert.Equal(ClashAction.Rename, NameClash.Choose(null, config, interactive: false));
    }

    [Fact]
    public void Choose_ExplicitAskWithoutTerminal_Throws()
    {
        Assert.Throws<ConfigException>(() => NameClash.Choose(ClashAction.Ask, new NameClashConfig(), interactive: false));
    }

    [Fact]
    public void Parse_UnknownAction_Throws()
    {
        Assert.Throws<ConfigException>(() => NameClash.Parse("explode"));
    }
}

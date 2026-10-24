namespace Claudebox.Tests;

public sealed class ConfigSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "claudebox-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("defaultName", " Work ", "Work")]
    [InlineData("DEFAULTWORK", "HOTFIX", "hotfix")]
    [InlineData("nameClash.action", "Rename", "rename")]
    [InlineData("nameclash.nonInteractive", "resume", "resume")]
    [InlineData("nameClash.renamePattern", "{name}-{n}", "{name}-{n}")]
    [InlineData("claudeArgs", "  --remote-control   --append-system-prompt \"be brief\" ", "--remote-control --append-system-prompt \"be brief\"")]
    public void Set_StoresCheckedValue(string key, string value, string stored)
    {
        var config = new Config();
        Assert.Equal(stored, ConfigSettings.Set(config, key, value));
        Assert.Equal(stored, ConfigSettings.List(config).Single(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value);
    }

    [Theory]
    [InlineData("defaultName", "  ")]
    [InlineData("defaultWork", "nope")]
    [InlineData("nameClash.action", "explode")]
    [InlineData("nameClash.nonInteractive", "ask")]
    [InlineData("nameClash.renamePattern", "{name} copy")]
    [InlineData("claudePath", @"C:\definitely\not\here\claude.exe")]
    [InlineData("claudeArgs", "--model \"opus")]
    [InlineData("colour", "red")]
    [InlineData("work.hotfix", "red")]
    public void Set_RejectsBadValuesAndKeys(string key, string value)
    {
        var config = new Config();
        Assert.Throws<ConfigException>(() => ConfigSettings.Set(config, key, value));
        Assert.Equal("Claude", config.DefaultName);
        Assert.Equal("ask", config.NameClash.Action);
    }

    [Fact]
    public void Unset_RestoresDefault()
    {
        var config = new Config();
        ConfigSettings.Set(config, "defaultName", "Work");
        Assert.Equal("Claude", ConfigSettings.Unset(config, "defaultName"));
        Assert.Equal("Claude", config.DefaultName);
    }

    [Fact]
    public void SetWork_AddsThenUpdates()
    {
        var config = new Config();
        Assert.True(config.SetWork("bug", "orange", null));
        Assert.Equal("orange", config.FindWork("BUG")?.Colour);

        Assert.False(config.SetWork("Bug", null, "Red Alert"));
        Assert.Equal("orange", config.FindWork("bug")?.Colour);
        Assert.Equal("Red Alert", config.FindWork("bug")?.Scheme);

        config.SetWork("bug", "none", "none");
        Assert.Null(config.FindWork("bug")?.Colour);
        Assert.Null(config.FindWork("bug")?.Scheme);
    }

    [Fact]
    public void SetWork_ClaudeArgs_SetKeptAndCleared()
    {
        var config = new Config();
        config.SetWork("research", null, null, "--permission-mode plan --append-system-prompt \"be brief\"");
        Assert.Equal(["--permission-mode", "plan", "--append-system-prompt", "be brief"], config.FindWork("research")?.ClaudeArgs);

        config.SetWork("research", "teal", null);
        Assert.Equal(4, config.FindWork("research")?.ClaudeArgs?.Count);

        config.SetWork("research", null, null, "none");
        Assert.Null(config.FindWork("research")?.ClaudeArgs);
    }

    [Fact]
    public void SetWork_BadColour_Throws()
    {
        Assert.Throws<ConfigException>(() => new Config().SetWork("bug", "blurple", null));
    }

    [Fact]
    public void RemoveWork_RefusesDefaultAndUnknown()
    {
        var config = new Config();
        Assert.Throws<ConfigException>(() => config.RemoveWork("general"));
        Assert.Throws<ConfigException>(() => config.RemoveWork("nope"));

        config.RemoveWork("HOTFIX");
        Assert.Null(config.FindWork("hotfix"));
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var path = Path.Combine(_dir, "config.json");
        var config = new Config();
        ConfigSettings.Set(config, "nameClash.action", "rename");
        config.SetWork("bug", "#123456", null);
        config.Save(path);

        var loaded = Config.Load(path);
        Assert.Equal("rename", loaded.NameClash.Action);
        Assert.Equal("#123456", loaded.FindWork("Bug")?.Colour);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}

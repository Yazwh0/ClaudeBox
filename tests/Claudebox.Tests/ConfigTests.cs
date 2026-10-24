using Claudebox.Terminal;

namespace Claudebox.Tests;

public sealed class ConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "claudebox-tests", Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(_dir, "config.json");

    [Fact]
    public void Load_WhenMissing_WritesDefaults()
    {
        var config = Config.Load(ConfigPath);

        Assert.True(File.Exists(ConfigPath));
        Assert.Equal("ask", config.NameClash.Action);
        Assert.Equal("Claude", config.DefaultName);
        Assert.Equal("general", config.DefaultWork);
        Assert.NotNull(config.FindWork("General"));
        Assert.NotNull(config.FindWork("HOTFIX"));
    }

    [Fact]
    public void Load_ReadsUserValues_AndReplacesDefaultWorkTypes()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, """
            {
              // comments are allowed
              "nameClash": { "action": "rename", "renamePattern": "{name}-{n}" },
              "work": { "Urgent": { "colour": "#FF0000", "scheme": "Red" } },
            }
            """);

        var config = Config.Load(ConfigPath);

        Assert.Equal("rename", config.NameClash.Action);
        Assert.Equal("fail", config.NameClash.NonInteractive);
        Assert.Equal("Red", config.FindWork("urgent")?.Scheme);
        Assert.Null(config.FindWork("hotfix"));
    }

    [Fact]
    public void Load_InvalidJson_ThrowsConfigException()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "{ nope");
        Assert.Throws<ConfigException>(() => Config.Load(ConfigPath));
    }

    [Theory]
    [InlineData("red", "#C62828")]
    [InlineData("#abc", "#AABBCC")]
    [InlineData("#c62828", "#C62828")]
    public void Colours_Normalise(string input, string expected)
    {
        Assert.Equal(expected, Colours.Normalise(input));
    }

    [Fact]
    public void Colours_Invalid_Throws()
    {
        Assert.Throws<ConfigException>(() => Colours.Normalise("blurple"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}

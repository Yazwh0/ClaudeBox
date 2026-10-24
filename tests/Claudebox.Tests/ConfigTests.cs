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
    [InlineData("Red", "red")]
    [InlineData("Dark Orange", "darkorange")]
    [InlineData("crimson", "crimson")]
    [InlineData("#abc", "#AABBCC")]
    [InlineData("#c62828", "#C62828")]
    public void Colours_Validate_KeepsNamesAndTidiesHex(string input, string stored)
    {
        Assert.Equal(stored, Colours.Validate(input));
    }

    [Theory]
    [InlineData("red", "#C62828")]        // ours beats the web's #FF0000
    [InlineData("crimson", "#DC143C")]
    [InlineData("navy", "#000080")]
    [InlineData("darkorange", "#FF8C00")]
    [InlineData("#abc", "#AABBCC")]
    public void Colours_ToHex(string colour, string hex)
    {
        Assert.Equal(hex, Colours.ToHex(colour));
    }

    [Theory]
    [InlineData("blurple")]
    [InlineData("Control")]      // a Windows system colour, not a web one
    [InlineData("transparent")]
    [InlineData("#12345")]
    public void Colours_Invalid_Throws(string colour)
    {
        Assert.Throws<ConfigException>(() => Colours.Validate(colour));
    }

    [Fact]
    public void Colours_Ansi_IgnoresInvalid()
    {
        Assert.Equal("", Colours.Ansi("blurple"));
        Assert.Equal("\e[38;2;220;20;60m", Colours.Ansi("crimson"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}

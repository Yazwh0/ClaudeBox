using System.Text.Json;
using Claudebox.Launch;
using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Tests;

public class LaunchArgumentTests
{
    private static SessionRecord Record(string name = "Login hotfix") => new()
    {
        Id = "abc123def456",
        Name = name,
        Cwd = @"C:\src\app",
        SessionId = "8c2e0000-0000-0000-0000-000000000000",
        ClaudeArgs = ["--model", "opus"],
    };

    [Fact]
    public void ClaudeArguments_FirstLaunch_UsesSessionId()
    {
        Assert.Equal(
            ["--session-id", "8c2e0000-0000-0000-0000-000000000000", "--name", "Login hotfix",
             "--settings", "hooks.json", "--model", "opus"],
            ClaudeLauncher.BuildArguments(Record(), transcriptExists: false, "hooks.json"));
    }

    [Fact]
    public void ClaudeArguments_WithTranscript_Resumes()
    {
        Assert.Equal(
            ["--resume", "8c2e0000-0000-0000-0000-000000000000", "--name", "Login hotfix",
             "--settings", "hooks.json", "--model", "opus"],
            ClaudeLauncher.BuildArguments(Record(), transcriptExists: true, "hooks.json"));
    }

    [Fact]
    public void TabArguments_IncludeTitleFolderAndRunCommand()
    {
        Assert.Equal(
            ["new-tab", "--title", "Login hotfix", "--suppressApplicationTitle",
             "-d", @"C:\src\app", @"C:\tools\cb.exe", "run", "abc123def456"],
            WindowsTerminal.BuildTabArguments(Record(), @"C:\tools\cb.exe"));
    }

    [Fact]
    public void TabArguments_IncludeColourSchemeAndEscapeSemicolons()
    {
        var record = Record("a;b");
        record.Colour = "#C62828";
        record.Scheme = "Red Alert";

        var args = WindowsTerminal.BuildTabArguments(record, "cb.exe");

        Assert.Equal("a\\;b", args[args.IndexOf("--title") + 1]);
        Assert.Equal("#C62828", args[args.IndexOf("--tabColor") + 1]);
        Assert.Equal("Red Alert", args[args.IndexOf("--colorScheme") + 1]);
    }

    [Fact]
    public void Arguments_SeveralTabs_AreSeparatedBySemicolons()
    {
        var args = WindowsTerminal.BuildArguments([Record("one"), Record("two")], "cb.exe", newWindow: false);

        Assert.Equal(["-w", "0", "new-tab"], args[..3]);
        Assert.Single(args, a => a == ";");
        Assert.Equal(2, args.Count(a => a == "new-tab"));
    }

    [Fact]
    public void Arguments_NewWindow_TargetsNewWindow()
    {
        var args = WindowsTerminal.BuildArguments([Record()], "cb.exe", newWindow: true);
        Assert.Equal(["-w", "new"], args[..2]);
    }

    [Fact]
    public void HooksFile_CallsCbForEachEventWithForwardSlashes()
    {
        using var doc = JsonDocument.Parse(HooksFile.Build(@"C:\tools\cb.exe"));
        var hooks = doc.RootElement.GetProperty("hooks");

        foreach (var name in HooksFile.Events)
        {
            var command = hooks.GetProperty(name)[0].GetProperty("hooks")[0].GetProperty("command").GetString();
            Assert.Equal($"\"C:/tools/cb.exe\" hook {name}", command);
        }
    }
}

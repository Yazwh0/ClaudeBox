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
    public void ClaudeArguments_ConfigArgsComeBeforeTheSessionsOwn()
    {
        Assert.Equal(
            ["--session-id", "8c2e0000-0000-0000-0000-000000000000", "--name", "Login hotfix",
             "--settings", "hooks.json", "--remote-control", "--model", "opus"],
            ClaudeLauncher.BuildArguments(Record(), transcriptExists: false, "hooks.json", ["--remote-control"]));
    }

    [Fact]
    public void ConfigArgs_GlobalThenWorkType()
    {
        var config = new Config { ClaudeArgs = ["--remote-control"] };
        config.SetWork("research", null, null, "--permission-mode plan");
        var record = Record();

        record.Work = "Research";
        Assert.Equal(["--remote-control", "--permission-mode", "plan"], ClaudeLauncher.ConfigArgs(config, record));

        record.Work = "gone";
        Assert.Equal(["--remote-control"], ClaudeLauncher.ConfigArgs(config, record));
    }

    [Fact]
    public void TabArguments_IncludeTitleFolderAndRunCommand()
    {
        Assert.Equal(
            ["new-tab", "--title", "Login hotfix", "--suppressApplicationTitle",
             "-d", @"C:\src\app", @"C:\tools\cb.exe", "run", "abc123def456"],
            WindowsTerminal.BuildTabArguments(Record(), [@"C:\tools\cb.exe"]));
    }

    [Fact]
    public void TabArguments_DotnetCommand_RunsTheDll()
    {
        var args = WindowsTerminal.BuildTabArguments(Record(), ["dotnet", @"C:\copy\cb.dll"]);
        Assert.Equal(["dotnet", @"C:\copy\cb.dll", "run", "abc123def456"], args[^4..]);
    }

    [Fact]
    public void TabArguments_IncludeColourSchemeAndEscapeSemicolons()
    {
        var record = Record("a;b");
        record.Colour = "crimson";
        record.Scheme = "Red Alert";

        var args = WindowsTerminal.BuildTabArguments(record, ["cb.exe"]);

        Assert.Equal("a\\;b", args[args.IndexOf("--title") + 1]);
        Assert.Equal("#DC143C", args[args.IndexOf("--tabColor") + 1]);
        Assert.Equal("Red Alert", args[args.IndexOf("--colorScheme") + 1]);
    }

    [Fact]
    public void Arguments_WithProfile_PassItToEachTab()
    {
        var args = WindowsTerminal.BuildArguments([Record("one"), Record("two")], ["cb.exe"], newWindow: false, "{abc}");
        Assert.Equal(2, args.Count(a => a == "--profile"));
        Assert.Equal("{abc}", args[args.IndexOf("--profile") + 1]);
    }

    [Fact]
    public void Arguments_SeveralTabs_AreSeparatedBySemicolons()
    {
        var args = WindowsTerminal.BuildArguments([Record("one"), Record("two")], ["cb.exe"], newWindow: false);

        Assert.Equal(["-w", "0", "new-tab"], args[..3]);
        Assert.Single(args, a => a == ";");
        Assert.Equal(2, args.Count(a => a == "new-tab"));
    }

    [Fact]
    public void Arguments_NewWindow_TargetsNewWindow()
    {
        var args = WindowsTerminal.BuildArguments([Record()], ["cb.exe"], newWindow: true);
        Assert.Equal(["-w", "new"], args[..2]);
    }

    [Fact]
    public void HooksFile_CallsCbForEachEventWithForwardSlashes()
    {
        using var doc = JsonDocument.Parse(HooksFile.Build([@"C:\tools\cb.exe"]));
        var hooks = doc.RootElement.GetProperty("hooks");

        foreach (var name in HooksFile.Events)
        {
            var command = hooks.GetProperty(name)[0].GetProperty("hooks")[0].GetProperty("command").GetString();
            Assert.Equal($"\"C:/tools/cb.exe\" hook {name}", command);
        }
    }

    [Fact]
    public void HooksFile_DotnetCommand_QuotesEachPart()
    {
        using var doc = JsonDocument.Parse(HooksFile.Build(["dotnet", @"C:\copy dir\cb.dll"]));
        var command = doc.RootElement.GetProperty("hooks").GetProperty("Stop")[0]
            .GetProperty("hooks")[0].GetProperty("command").GetString();
        Assert.Equal("\"dotnet\" \"C:/copy dir/cb.dll\" hook Stop", command);
    }

    [Theory]
    [InlineData(@"C:\a\bin\copy\", @"C:\a\bin\copy")]
    [InlineData(@"C:\a\bin\copy", @"c:\A\BIN\COPY\")]
    public void AppCopy_SamePath_IgnoresTrailingSeparatorAndCase(string a, string b)
    {
        Assert.True(AppCopy.SamePath(a, b));
    }

    [Fact]
    public void AppCopy_SamePath_DifferentFolders()
    {
        Assert.False(AppCopy.SamePath(@"C:\a\bin\copy1", @"C:\a\bin\copy2"));
    }

    [Fact]
    public void AppCopy_BuildKey_ChangesWithDllTimeAndIsPathSafe()
    {
        var time = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        var key = AppCopy.BuildKey("0.1.1-local.1+abc123", time);

        Assert.DoesNotContain('+', key);
        Assert.NotEqual(key, AppCopy.BuildKey("0.1.1-local.1+abc123", time.AddSeconds(1)));
    }
}

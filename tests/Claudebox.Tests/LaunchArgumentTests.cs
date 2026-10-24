using Claudebox.Launch;
using Claudebox.Registry;
using Claudebox.Terminal;

namespace Claudebox.Tests;

public class LaunchArgumentTests
{
    private static SessionRecord Record() => new()
    {
        Id = "abc123def456",
        Name = "Login hotfix",
        Cwd = @"C:\src\app",
        SessionId = "8c2e0000-0000-0000-0000-000000000000",
        ClaudeArgs = ["--model", "opus"],
    };

    [Fact]
    public void ClaudeArguments_FirstLaunch_UsesSessionId()
    {
        Assert.Equal(
            ["--session-id", "8c2e0000-0000-0000-0000-000000000000", "--name", "Login hotfix", "--model", "opus"],
            ClaudeLauncher.BuildArguments(Record(), transcriptExists: false));
    }

    [Fact]
    public void ClaudeArguments_WithTranscript_Resumes()
    {
        Assert.Equal(
            ["--resume", "8c2e0000-0000-0000-0000-000000000000", "--name", "Login hotfix", "--model", "opus"],
            ClaudeLauncher.BuildArguments(Record(), transcriptExists: true));
    }

    [Fact]
    public void NewTabArguments_IncludeTitleFolderAndRunCommand()
    {
        Assert.Equal(
            ["-w", "0", "new-tab", "--title", "Login hotfix", "--suppressApplicationTitle",
             "-d", @"C:\src\app", @"C:\tools\cb.exe", "run", "abc123def456"],
            WindowsTerminal.BuildNewTabArguments(Record(), @"C:\tools\cb.exe"));
    }

    [Fact]
    public void NewTabArguments_IncludeColourAndEscapeSemicolons()
    {
        var record = Record();
        record.Name = "a;b";
        record.Colour = "#C62828";

        var args = WindowsTerminal.BuildNewTabArguments(record, "cb.exe");

        Assert.Equal("a\\;b", args[args.IndexOf("--title") + 1]);
        Assert.Equal("#C62828", args[args.IndexOf("--tabColor") + 1]);
    }
}

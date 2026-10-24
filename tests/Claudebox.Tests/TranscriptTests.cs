using Claudebox.Commands;
using Claudebox.Launch;

namespace Claudebox.Tests;

public sealed class TranscriptTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "claudebox-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(@"C:\Documents\Source\claudebox", "C--Documents-Source-claudebox")]
    [InlineData(@"C:\src\my app.v2\", "C--src-my-app-v2")]
    public void ProjectFolderName_MatchesClaudesEncoding(string cwd, string expected)
    {
        Assert.Equal(expected, Transcripts.ProjectFolderName(cwd));
    }

    [Fact]
    public void Read_TakesFirstCwdAndPrefersYourTitle()
    {
        var path = Write("8c2e0000-0000-0000-0000-000000000000.jsonl", """
            {"type":"mode","mode":"normal"}
            {"type":"user","cwd":"C:\\src\\app","message":{"content":"hi"}}
            {"type":"ai-title","aiTitle":"First idea"}
            not json at all, "cwd" mentioned
            {"type":"user","cwd":"C:\\elsewhere"}
            {"type":"custom-title","customTitle":"Login bug"}
            {"type":"ai-title","aiTitle":"Later idea"}
            """);

        var info = Transcripts.Read(path);

        Assert.Equal("8c2e0000-0000-0000-0000-000000000000", info.SessionId);
        Assert.Equal(@"C:\src\app", info.Cwd);
        Assert.Equal("Login bug", info.Title);
    }

    [Fact]
    public void Read_WithoutTitles_HasNoTitle()
    {
        var info = Transcripts.Read(Write("a.jsonl", """{"type":"user","cwd":"C:\\x"}"""));
        Assert.Null(info.Title);
    }

    [Fact]
    public void StartupCommand_RunsRestoreInANewWindow()
    {
        Assert.Equal("wt.exe -w new \"C:\\tools\\cb.exe\" restore", StartupCommands.BuildCommand(@"C:\tools\cb.exe"));
    }

    private string Write(string name, string content)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}

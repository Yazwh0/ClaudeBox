using Claudebox.Registry;

namespace Claudebox.Tests;

public class SessionChangesTests
{
    private static readonly Config Config = new()
    {
        Work = new(StringComparer.OrdinalIgnoreCase)
        {
            ["hotfix"] = new() { Colour = "red", Scheme = "Red Alert" },
            ["research"] = new() { Colour = "#1565C0" },
        },
    };

    private static SessionFile File() => new()
    {
        Sessions =
        [
            new() { Id = "a", Name = "API refactor", Cwd = "C:\\", Work = "research", Colour = "#1565C0" },
            new() { Id = "b", Name = "Login bug", Cwd = "C:\\" },
        ],
    };

    [Fact]
    public void Rename_ChangesName()
    {
        var file = File();
        Assert.Null(new SessionChanges(Name: "'API v2'").ApplyTo(file, "a", Config));
        Assert.Equal("API v2", file.Sessions[0].Name);
    }

    [Fact]
    public void Rename_ToTakenName_Fails()
    {
        var file = File();
        Assert.NotNull(new SessionChanges(Name: "login BUG").ApplyTo(file, "a", Config));
        Assert.Equal("API refactor", file.Sessions[0].Name);
    }

    [Fact]
    public void Rename_ToOwnNameInOtherCase_IsAllowed()
    {
        var file = File();
        Assert.Null(new SessionChanges(Name: "api refactor").ApplyTo(file, "a", Config));
        Assert.Equal("api refactor", file.Sessions[0].Name);
    }

    [Fact]
    public void Work_BringsItsColourAndScheme()
    {
        var file = File();
        Assert.Null(new SessionChanges(Work: "hotfix").ApplyTo(file, "a", Config));

        var record = file.Sessions[0];
        Assert.Equal("hotfix", record.Work);
        Assert.Equal("#C62828", record.Colour);
        Assert.Equal("Red Alert", record.Scheme);
    }

    [Fact]
    public void ExplicitColour_BeatsWorkColour()
    {
        var file = File();
        Assert.Null(new SessionChanges(Work: "hotfix", Colour: "amber").ApplyTo(file, "a", Config));
        Assert.Equal("#FFB300", file.Sessions[0].Colour);
    }

    [Fact]
    public void None_ClearsColourAndScheme()
    {
        var file = File();
        Assert.Null(new SessionChanges(Work: "hotfix", Colour: "none", Scheme: "None").ApplyTo(file, "a", Config));
        Assert.Null(file.Sessions[0].Colour);
        Assert.Null(file.Sessions[0].Scheme);
    }

    [Fact]
    public void UnknownWork_Fails()
    {
        Assert.Contains("Unknown work type", new SessionChanges(Work: "nope").ApplyTo(File(), "a", Config));
    }

    [Fact]
    public void RemovedSession_Fails()
    {
        Assert.NotNull(new SessionChanges(Name: "x").ApplyTo(File(), "gone", Config));
    }
}

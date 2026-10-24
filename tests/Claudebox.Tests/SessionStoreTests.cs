using Claudebox.Registry;

namespace Claudebox.Tests;

public sealed class SessionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "claudebox-tests", Guid.NewGuid().ToString("N"));
    private string StorePath => Path.Combine(_dir, "sessions.json");

    [Fact]
    public void ReadAll_WhenFileMissing_ReturnsEmpty()
    {
        Assert.Empty(new SessionStore(StorePath).ReadAll());
    }

    [Fact]
    public void Update_PersistsRecords()
    {
        var store = new SessionStore(StorePath);
        store.Update(f => f.Sessions.Add(new SessionRecord { Name = "API refactor", Cwd = @"C:\src\api" }));

        var record = Assert.Single(new SessionStore(StorePath).ReadAll());
        Assert.Equal("API refactor", record.Name);
        Assert.Equal(@"C:\src\api", record.Cwd);
    }

    [Fact]
    public void Update_FromManyThreads_LosesNoWrites()
    {
        var store = new SessionStore(StorePath);

        Parallel.For(0, 50, i =>
            new SessionStore(StorePath).Update(f => f.Sessions.Add(new SessionRecord { Name = $"s{i}", Cwd = "C:\\" })));

        Assert.Equal(50, store.ReadAll().Count);
    }

    [Fact]
    public void UpdateRecord_IgnoresRemovedRecord()
    {
        var store = new SessionStore(StorePath);
        store.UpdateRecord("missing", r => r.Name = "x");
        Assert.Empty(store.ReadAll());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}

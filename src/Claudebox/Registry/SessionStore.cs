using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claudebox.Registry;

/// <summary>
/// The sessions.json registry. Several cb processes (and hooks) touch it at once, so every
/// access holds an exclusive lock file and writes go through a temp file and an atomic move.
/// </summary>
internal sealed class SessionStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    public static SessionStore Default => new(AppPaths.Sessions);

    public List<SessionRecord> ReadAll()
    {
        using var _ = AcquireLock();
        return Load().Sessions;
    }

    public T Update<T>(Func<SessionFile, T> change)
    {
        using var _ = AcquireLock();
        var file = Load();
        var result = change(file);
        Save(file);
        return result;
    }

    public void Update(Action<SessionFile> change) => Update(file => { change(file); return 0; });

    /// <summary>Applies a change to one record; does nothing if the record has since been removed.</summary>
    public void UpdateRecord(string id, Action<SessionRecord> change) => Update(file =>
    {
        if (file.Sessions.Find(s => s.Id == id) is { } record)
            change(record);
    });

    private SessionFile Load()
    {
        if (!File.Exists(path))
            return new SessionFile();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SessionFile>(json, JsonOptions) ?? new SessionFile();
    }

    private void Save(SessionFile file)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(file, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    private FileStream AcquireLock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lockPath = path + ".lock";
        var deadline = DateTime.UtcNow + LockTimeout;
        var delay = 5;

        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(delay);
                delay = Math.Min(delay * 2, 100);
            }
        }
    }
}

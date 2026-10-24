namespace Claudebox.Launch;

/// <summary>
/// Session tabs run `cb run` for as long as Claude is open, and hooks call cb constantly. Running
/// those from the installed tool would lock its shim (cb.exe), so `dotnet tool update` / uninstall would
/// fail while any session is open. Instead they run a private copy of cb, keyed by build, and
/// copies that nothing is using any more are removed.
/// </summary>
internal static class AppCopy
{
    private const string AppDll = "cb.dll";

    /// <summary>The command (exe, or dotnet + dll) that runs the private copy of this build.</summary>
    public static IReadOnlyList<string> Command()
    {
        var source = AppContext.BaseDirectory;
        var target = IsCopy(source) ? source : EnsureCopy(source);
        RemoveUnused(keep: target);

        // A dotnet tool package has no apphost (the tool shim is generated on install).
        var exe = Path.Combine(target, "cb.exe");
        return File.Exists(exe) ? [exe] : ["dotnet", Path.Combine(target, AppDll)];
    }

    /// <summary>The copy's folder, for files that belong with it (e.g. its hooks.json).</summary>
    public static string Folder(IReadOnlyList<string> command) => Path.GetDirectoryName(command[^1])!;

    /// <summary>
    /// Marks the copy as in use for as long as the session runs: its lock file is held open
    /// without delete sharing, so cleanup can't delete it (see RemoveUnused).
    /// </summary>
    public static IDisposable Pin(IReadOnlyList<string> command) =>
        new FileStream(Path.Combine(Folder(command), LockFile), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

    private const string LockFile = "in-use.lock";

    /// <summary>Written last when making a copy, so a partial or damaged copy is never used.</summary>
    private const string CompleteFile = "copy-complete";

    private static bool IsCopy(string directory) =>
        Normalise(directory).StartsWith(Normalise(AppPaths.Bin) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>AppContext.BaseDirectory ends with a separator and enumerated folders don't.</summary>
    internal static string Normalise(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    internal static bool SamePath(string a, string b) => string.Equals(Normalise(a), Normalise(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>The key includes the dll's write time, so rebuilding from source without a version change still gets a fresh copy.</summary>
    internal static string BuildKey(string version, DateTime dllWrittenUtc)
    {
        var safe = string.Concat(version.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '+' ? '_' : c));
        return $"{safe}_{dllWrittenUtc.Ticks:x}";
    }

    private static string EnsureCopy(string source)
    {
        var version = typeof(AppCopy).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "0.0.0";
        var target = Path.Combine(AppPaths.Bin, BuildKey(version, File.GetLastWriteTimeUtc(Path.Combine(source, AppDll))));
        if (File.Exists(Path.Combine(target, CompleteFile)))
            return target;
        if (Directory.Exists(target))
            TryDelete(target);

        // Copy beside the target then rename, so a half-written copy is never used.
        var temp = $"{target}.{Environment.ProcessId}.tmp";
        CopyDirectory(source, temp);
        File.WriteAllText(Path.Combine(temp, CompleteFile), version);
        try
        {
            Directory.Move(temp, target);
        }
        catch (IOException) when (File.Exists(Path.Combine(target, CompleteFile)))
        {
            // Another cb made the same copy first.
            TryDelete(temp);
        }
        return target;
    }

    /// <summary>
    /// Removes copies nothing is using. A copy is in use if a session holds its lock file (see
    /// Pin) or a process is running from it (its cb.dll can't be deleted then). Those two files
    /// go first, and the rest only if both could be deleted, so a copy in use is never damaged.
    /// </summary>
    private static void RemoveUnused(string keep)
    {
        if (!Directory.Exists(AppPaths.Bin))
            return;

        foreach (var dir in Directory.EnumerateDirectories(AppPaths.Bin))
        {
            if (SamePath(dir, keep))
                continue;
            // Another cb may be part way through making this copy.
            if (dir.EndsWith(".tmp") && Directory.GetCreationTimeUtc(dir) > DateTime.UtcNow.AddMinutes(-10))
                continue;

            try
            {
                File.Delete(Path.Combine(dir, LockFile));
                File.Delete(Path.Combine(dir, AppDll));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            TryDelete(dir);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Couldn't remove {directory}: {e.Message}");
        }
    }
}

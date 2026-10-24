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

    private static bool IsCopy(string directory) =>
        Path.GetFullPath(directory).StartsWith(Path.GetFullPath(AppPaths.Bin), StringComparison.OrdinalIgnoreCase);

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
        if (File.Exists(Path.Combine(target, AppDll)))
            return target;

        // Copy beside the target then rename, so a half-written copy is never used.
        var temp = $"{target}.{Environment.ProcessId}.tmp";
        CopyDirectory(source, temp);
        try
        {
            Directory.Move(temp, target);
        }
        catch (IOException) when (File.Exists(Path.Combine(target, AppDll)))
        {
            // Another cb made the same copy first.
            TryDelete(temp);
        }
        return target;
    }

    /// <summary>
    /// Windows lets loaded .NET dlls be deleted, so being run from isn't enough to protect a copy.
    /// A copy is in use while any session holds its lock file (see Pin); deleting the lock file
    /// fails in that case, and only then is the copy kept.
    /// </summary>
    private static void RemoveUnused(string keep)
    {
        if (!Directory.Exists(AppPaths.Bin))
            return;

        foreach (var dir in Directory.EnumerateDirectories(AppPaths.Bin))
        {
            if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(keep), StringComparison.OrdinalIgnoreCase))
                continue;
            // Another cb may be part way through making this copy.
            if (dir.EndsWith(".tmp") && Directory.GetCreationTimeUtc(dir) > DateTime.UtcNow.AddMinutes(-10))
                continue;

            try
            {
                File.Delete(Path.Combine(dir, LockFile));
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

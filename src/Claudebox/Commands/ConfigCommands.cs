using System.ComponentModel;
using System.Diagnostics;
using Claudebox.Terminal;

namespace Claudebox.Commands;

/// <summary>`cb config` and `cb work`: change config.json from the command line.</summary>
internal static class ConfigCommands
{
    public static int Show()
    {
        var config = Config.Load();
        Console.WriteLine(AppPaths.Config);
        Console.WriteLine();

        var settings = ConfigSettings.List(config).ToList();
        var width = settings.Max(s => s.Key.Length);
        foreach (var (key, value, description) in settings)
            Console.WriteLine($"  {key.PadRight(width)}  {(value ?? "(not set)").PadRight(14)}  {description}");

        Console.WriteLine();
        Console.WriteLine("Work types:");
        WriteWorkTypes(config);
        Console.WriteLine();
        Console.WriteLine("Change with: cb config set <setting> <value>, cb config unset <setting>, cb work add|rm, or cb config edit");
        return 0;
    }

    public static int Set(string key, string value)
    {
        var config = Config.Load();
        var stored = ConfigSettings.Set(config, key, value);
        config.Save();
        Console.WriteLine($"{key} = {stored}");
        return 0;
    }

    public static int Unset(string key)
    {
        var config = Config.Load();
        var builtIn = ConfigSettings.Unset(config, key);
        config.Save();
        Console.WriteLine($"{key} = {builtIn ?? "(not set)"} (the default)");
        return 0;
    }

    /// <summary>Opens config.json in $VISUAL / $EDITOR, else whatever Windows opens .json files with, else Notepad.</summary>
    public static int Edit()
    {
        Config.Load(); // writes the defaults if there's no file yet
        var editor = Environment.GetEnvironmentVariable("VISUAL") ?? Environment.GetEnvironmentVariable("EDITOR");

        try
        {
            if (!string.IsNullOrEmpty(editor))
                Process.Start(new ProcessStartInfo(editor, $"\"{AppPaths.Config}\"") { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(AppPaths.Config) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            Process.Start("notepad.exe", $"\"{AppPaths.Config}\"");
        }

        Console.WriteLine($"Opened {AppPaths.Config}");
        return 0;
    }

    public static int ListWork()
    {
        WriteWorkTypes(Config.Load());
        return 0;
    }

    public static int AddWork(string name, string? colour, string? scheme)
    {
        var config = Config.Load();
        var added = config.SetWork(name, colour, scheme);
        config.Save();

        Console.WriteLine(added ? $"Added work type '{name}'." : $"Updated work type '{name}'.");
        if (!added)
            Console.WriteLine("Existing sessions keep their colours; change one with cb set <session> --work " + name + ".");
        return 0;
    }

    public static int RemoveWork(string name)
    {
        var config = Config.Load();
        config.RemoveWork(name);
        config.Save();
        Console.WriteLine($"Removed work type '{name}'. Sessions that used it keep their colours.");
        return 0;
    }

    private static void WriteWorkTypes(Config config)
    {
        var useColour = !Console.IsOutputRedirected;
        var width = config.Work.Keys.Max(k => k.Length);
        foreach (var (name, work) in config.Work)
        {
            var label = name.PadRight(width);
            if (useColour && work.Colour is { } colour)
                label = Colours.Ansi(Colours.Normalise(colour)) + label + Colours.Reset;

            var isDefault = string.Equals(name, config.DefaultWork, StringComparison.OrdinalIgnoreCase) ? "  (default)" : "";
            var scheme = work.Scheme is null ? "" : $"  scheme: {work.Scheme}";
            Console.WriteLine($"  {label}  {work.Colour ?? "no colour",-9}{scheme}{isDefault}");
        }
    }
}

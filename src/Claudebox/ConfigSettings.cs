using Claudebox.Registry;

namespace Claudebox;

/// <summary>
/// The settings `cb config` can show, set and unset, by their config.json names (nested ones
/// with a dot). Values are checked before they're stored, so a typo can't leave a config that
/// won't load. Work types have their own command (`cb work`).
/// </summary>
internal static class ConfigSettings
{
    private sealed record Setting(
        string Key,
        string Description,
        Func<Config, string?> Get,
        Action<Config, string?> Set,
        Func<Config, string, string> Check);

    private static readonly Setting[] All =
    [
        new("claudePath", "Full path to claude, if it isn't on PATH",
            c => c.ClaudePath, (c, v) => c.ClaudePath = v,
            (_, v) => File.Exists(v) ? Path.GetFullPath(v) : throw new ConfigException($"File not found: {v}")),

        new("claudeArgs", "Extra arguments for claude on every launch, e.g. --remote-control",
            c => c.ClaudeArgs.Count == 0 ? null : Aliases.Join(c.ClaudeArgs), (c, v) => c.ClaudeArgs = v is null ? [] : Aliases.Split(v),
            (_, v) => Aliases.Join(Aliases.Split(v))),

        new("defaultName", "Name for cb on its own; {folder} is the folder's name",
            c => c.DefaultName, (c, v) => c.DefaultName = v ?? new Config().DefaultName,
            (_, v) => v.Trim().Length > 0 ? v.Trim() : throw new ConfigException("The name can't be empty.")),

        new("defaultWork", "Work type when --work isn't given",
            c => c.DefaultWork, (c, v) => c.DefaultWork = v,
            (c, v) => c.Work.Keys.FirstOrDefault(k => k.Equals(v, StringComparison.OrdinalIgnoreCase))
                ?? throw new ConfigException($"Unknown work type '{v}'. Configured: {string.Join(", ", c.Work.Keys)}. Add one with cb work add.")),

        new("nameClash.action", "What cb new does when the name is taken",
            c => c.NameClash.Action, (c, v) => c.NameClash.Action = v ?? new NameClashConfig().Action,
            (_, v) => NameClash.Parse(v).ToString().ToLowerInvariant()),

        new("nameClash.nonInteractive", "The same, when there's no one to ask",
            c => c.NameClash.NonInteractive, (c, v) => c.NameClash.NonInteractive = v ?? new NameClashConfig().NonInteractive,
            (_, v) => NameClash.Parse(v) is not ClashAction.Ask and var action
                ? action.ToString().ToLowerInvariant()
                : throw new ConfigException("nameClash.nonInteractive can't be 'ask'.")),

        new("nameClash.renamePattern", "How a free name is made; needs {n}",
            c => c.NameClash.RenamePattern, (c, v) => c.NameClash.RenamePattern = v ?? new NameClashConfig().RenamePattern,
            (_, v) => v.Contains("{n}") ? v : throw new ConfigException("The pattern needs {n} in it, e.g. \"{name} {n}\".")),
    ];

    public static IEnumerable<(string Key, string? Value, string Description)> List(Config config) =>
        All.Select(s => (s.Key, s.Get(config), s.Description));

    /// <summary>Checks and stores the value; returns it as stored (e.g. a work type in its configured case).</summary>
    public static string Set(Config config, string key, string value)
    {
        var setting = Find(key);
        var stored = setting.Check(config, value);
        setting.Set(config, stored);
        return stored;
    }

    /// <summary>Back to the built-in default.</summary>
    public static string? Unset(Config config, string key)
    {
        var setting = Find(key);
        var builtIn = setting.Get(new Config());
        setting.Set(config, builtIn);
        return builtIn;
    }

    private static Setting Find(string key) =>
        All.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
        ?? throw new ConfigException(key.StartsWith("work", StringComparison.OrdinalIgnoreCase)
            ? "Work types are changed with cb work add and cb work rm."
            : $"Unknown setting '{key}'. Settings: {string.Join(", ", All.Select(s => s.Key))}.");
}

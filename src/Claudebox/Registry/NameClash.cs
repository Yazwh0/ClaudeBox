namespace Claudebox.Registry;

internal enum ClashAction { Ask, Resume, Replace, Rename, Fail }

/// <summary>What `cb new` does when the name is already taken.</summary>
internal static class NameClash
{
    public static ClashAction Parse(string value) => value.Trim().ToLowerInvariant() switch
    {
        "ask" => ClashAction.Ask,
        "resume" => ClashAction.Resume,
        "replace" => ClashAction.Replace,
        "rename" => ClashAction.Rename,
        "fail" => ClashAction.Fail,
        _ => throw new ConfigException($"Unknown name clash action '{value}'. Use ask, resume, replace, rename or fail."),
    };

    /// <summary>Flag beats config. Without a terminal to ask in, config's nonInteractive action is used instead.</summary>
    public static ClashAction Choose(ClashAction? flag, NameClashConfig config, bool interactive)
    {
        if (flag is { } chosen)
        {
            if (chosen == ClashAction.Ask && !interactive)
                throw new ConfigException("--on-clash ask needs an interactive terminal.");
            return chosen;
        }

        var action = Parse(config.Action);
        if (action != ClashAction.Ask || interactive)
            return action;

        action = Parse(config.NonInteractive);
        if (action == ClashAction.Ask)
            throw new ConfigException("nameClash.nonInteractive can't be 'ask'.");
        return action;
    }

    /// <summary>First name from the pattern (e.g. "{name} {n}" gives "foo 2", "foo 3", ...) that isn't taken.</summary>
    public static string NextFreeName(string name, string pattern, IEnumerable<string> existing)
    {
        if (!pattern.Contains("{n}"))
            throw new ConfigException($"Rename pattern '{pattern}' needs {{n}} in it.");

        var taken = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var n = 2; ; n++)
        {
            var candidate = pattern.Replace("{name}", name).Replace("{n}", n.ToString());
            if (!taken.Contains(candidate))
                return candidate;
        }
    }

    /// <summary>cmd passes single quotes through literally, so `cb new 'foo'` arrives as 'foo'.</summary>
    public static string CleanName(string name)
    {
        name = name.Trim();
        if (name.Length >= 2 && name[0] == '\'' && name[^1] == '\'')
            name = name[1..^1].Trim();
        return name;
    }
}

namespace Claudebox;

/// <summary>
/// Shortcuts from config.json, e.g. "c": "rm --exited --yes" makes `cb c` run `cb rm --exited --yes`.
/// Only the first word is expanded, once, and an alias can't hide a built-in command.
/// </summary>
internal static class Aliases
{
    /// <summary>True if the first word could be an alias, so config.json only needs loading then.</summary>
    public static bool MightBeAlias(string[] args, ISet<string> commands) =>
        args.Length > 0 && !args[0].StartsWith('-') && !commands.Contains(args[0]);

    /// <summary>Replaces an alias with its command; anything after it is passed on.</summary>
    public static string[] Expand(string[] args, IReadOnlyDictionary<string, string> aliases) =>
        args.Length > 0 && aliases.TryGetValue(args[0], out var command)
            ? [.. Split(command), .. args[1..]]
            : args;

    /// <summary>Splits on spaces, keeping "quoted parts" together.</summary>
    public static List<string> Split(string command)
    {
        List<string> words = [];
        var word = new System.Text.StringBuilder();
        var quoted = false;
        var inWord = false;

        foreach (var c in command)
        {
            if (c == '"')
            {
                quoted = !quoted;
                inWord = true;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (inWord)
                    words.Add(word.ToString());
                word.Clear();
                inWord = false;
            }
            else
            {
                word.Append(c);
                inWord = true;
            }
        }

        if (quoted)
            throw new ConfigException($"The alias '{command}' has an unclosed quote.");
        if (inWord)
            words.Add(word.ToString());
        return words;
    }

    /// <summary>Checks a new alias, returning its name and command tidied up.</summary>
    public static (string Name, string Command) Check(string name, string command, ISet<string> commands)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Any(char.IsWhiteSpace) || name.StartsWith('-') || name.Contains('"'))
            throw new ConfigException($"'{name}' can't be an alias: use a single word that doesn't start with '-'.");
        if (commands.Contains(name))
            throw new ConfigException($"'{name}' is already a cb command.");

        var words = Split(command);
        if (words.Count == 0)
            throw new ConfigException("The alias needs a command, e.g. \"rm --exited --yes\".");
        if (!words[0].StartsWith('-') && !commands.Contains(words[0]))
            throw new ConfigException($"'{words[0]}' isn't a cb command. Aliases can't use other aliases.");
        return (name, command.Trim());
    }
}

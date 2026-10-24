using System.Text.RegularExpressions;

namespace Claudebox.Terminal;

internal static partial class Colours
{
    private static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["red"] = "#C62828",
        ["orange"] = "#EF6C00",
        ["amber"] = "#FFB300",
        ["yellow"] = "#FDD835",
        ["green"] = "#2E7D32",
        ["teal"] = "#00897B",
        ["blue"] = "#1565C0",
        ["purple"] = "#6A1B9A",
        ["pink"] = "#D81B60",
        ["grey"] = "#616161",
        ["gray"] = "#616161",
    };

    /// <summary>Accepts #RRGGBB, #RGB or a colour name; returns #RRGGBB.</summary>
    public static string Normalise(string colour)
    {
        if (Named.TryGetValue(colour.Trim(), out var hex))
            return hex;

        var value = colour.Trim();
        if (ShortHex().IsMatch(value))
            value = $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}";
        if (LongHex().IsMatch(value))
            return value.ToUpperInvariant();

        throw new ConfigException($"'{colour}' isn't a colour. Use #RRGGBB or one of: {string.Join(", ", Named.Keys)}.");
    }

    /// <summary>24-bit ANSI foreground escape for showing a colour in the console.</summary>
    public static string Ansi(string hex) =>
        $"\e[38;2;{Convert.ToInt32(hex[1..3], 16)};{Convert.ToInt32(hex[3..5], 16)};{Convert.ToInt32(hex[5..7], 16)}m";

    public const string Reset = "\e[0m";

    [GeneratedRegex("^#[0-9A-Fa-f]{3}$")]
    private static partial Regex ShortHex();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex LongHex();
}

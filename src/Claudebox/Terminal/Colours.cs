using System.Drawing;
using System.Text.RegularExpressions;

namespace Claudebox.Terminal;

/// <summary>
/// Colours are stored as given (a name like "red", or hex) and only turned into hex when a tab
/// is opened or a colour is shown. Names are ours first, picked to look good as tab colours, then
/// any standard web colour name (crimson, navy, darkorange, ...).
/// </summary>
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

    /// <summary>Checks a colour and returns it as it should be stored: a lowercase name, or #RRGGBB.</summary>
    public static string Validate(string colour)
    {
        var value = colour.Trim();
        if (value.StartsWith('#'))
            return ExpandHex(value) ?? throw Invalid(colour);

        var name = value.Replace(" ", "").ToLowerInvariant();
        return Named.ContainsKey(name) || WebColour(name) is not null ? name : throw Invalid(colour);
    }

    /// <summary>#RRGGBB for any stored colour (Windows Terminal and the console need hex).</summary>
    public static string ToHex(string colour)
    {
        var stored = Validate(colour);
        if (stored.StartsWith('#'))
            return stored;
        return Named.TryGetValue(stored, out var hex) ? hex : WebColour(stored)!;
    }

    /// <summary>24-bit ANSI foreground escape for showing a colour; empty for one that isn't valid (e.g. a hand-edited typo).</summary>
    public static string Ansi(string colour)
    {
        string hex;
        try
        {
            hex = ToHex(colour);
        }
        catch (ConfigException)
        {
            return "";
        }
        return $"\e[38;2;{Convert.ToInt32(hex[1..3], 16)};{Convert.ToInt32(hex[3..5], 16)};{Convert.ToInt32(hex[5..7], 16)}m";
    }

    public const string Reset = "\e[0m";

    /// <summary>A web colour name's hex, leaving out Windows system colours (Control, Window, ...) and Transparent.</summary>
    private static string? WebColour(string name)
    {
        var colour = Color.FromName(name);
        return colour.IsKnownColor && !colour.IsSystemColor && colour.A == 255
            ? $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}"
            : null;
    }

    private static string? ExpandHex(string value)
    {
        if (ShortHex().IsMatch(value))
            value = $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}";
        return LongHex().IsMatch(value) ? value.ToUpperInvariant() : null;
    }

    private static ConfigException Invalid(string colour) => new(
        $"'{colour}' isn't a colour. Use #RRGGBB, one of {string.Join(", ", Named.Keys.Where(k => k != "gray"))}, " +
        "or any web colour name (e.g. crimson, navy, darkorange).");

    [GeneratedRegex("^#[0-9A-Fa-f]{3}$")]
    private static partial Regex ShortHex();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex LongHex();
}

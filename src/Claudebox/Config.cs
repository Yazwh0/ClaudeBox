using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claudebox;

internal sealed class ConfigException(string message) : Exception(message);

/// <summary>config.json: user defaults. Command-line flags override these.</summary>
internal sealed class Config
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string? ClaudePath { get; set; }
    public string? DefaultWork { get; set; } = "general";

    /// <summary>Name for `cb` on its own; {folder} is the current folder's name. Clashes get a number.</summary>
    public string DefaultName { get; set; } = "Claude";
    public NameClashConfig NameClash { get; set; } = new();

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public Dictionary<string, WorkType> Work { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["general"] = new(),
        ["hotfix"] = new() { Colour = "#C62828" },
        ["feature"] = new() { Colour = "#2E7D32" },
        ["research"] = new() { Colour = "#1565C0" },
    };

    /// <summary>Loads config.json, writing the defaults on first run so there is something to edit.</summary>
    public static Config Load() => Load(AppPaths.Config);

    internal static Config Load(string path)
    {
        if (!File.Exists(path))
        {
            var defaults = new Config();
            defaults.Save(path);
            return defaults;
        }

        try
        {
            var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(path), JsonOptions) ?? new Config();
            config.Work = new Dictionary<string, WorkType>(config.Work, StringComparer.OrdinalIgnoreCase);
            return config;
        }
        catch (JsonException e)
        {
            throw new ConfigException($"Couldn't read {path}: {e.Message}");
        }
    }

    public void Save() => Save(AppPaths.Config);

    /// <summary>Via a temp file so other cb processes never read a half-written config.</summary>
    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    public WorkType? FindWork(string name) => Work.GetValueOrDefault(name);

    /// <summary>Adds a work type, or changes the one with that name. Returns true if it was added.</summary>
    public bool SetWork(string name, string? colour, string? scheme)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ConfigException("The work type needs a name.");

        var added = !Work.TryGetValue(name, out var workType);
        workType ??= Work[name.Trim()] = new WorkType();
        if (colour is not null)
            workType.Colour = IsNone(colour) ? null : Terminal.Colours.Normalise(colour);
        if (scheme is not null)
            workType.Scheme = IsNone(scheme) ? null : scheme;
        return added;
    }

    public void RemoveWork(string name)
    {
        if (!Work.ContainsKey(name))
            throw new ConfigException($"There's no work type called '{name}'.");
        if (string.Equals(DefaultWork, name, StringComparison.OrdinalIgnoreCase))
            throw new ConfigException($"'{name}' is the default work type. Change defaultWork first (cb config set defaultWork <type>).");
        Work.Remove(name);
    }

    internal static bool IsNone(string value) => value.Equals("none", StringComparison.OrdinalIgnoreCase);
}

internal sealed class NameClashConfig
{
    public string Action { get; set; } = "ask";
    public string NonInteractive { get; set; } = "fail";
    public string RenamePattern { get; set; } = "{name} {n}";
}

internal sealed class WorkType
{
    public string? Colour { get; set; }
    public string? Scheme { get; set; }
}

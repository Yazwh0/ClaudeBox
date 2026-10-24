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
    public string? DefaultWork { get; set; } = "feature";

    /// <summary>Name for `cb` on its own; {folder} is the current folder's name. Clashes get a number.</summary>
    public string DefaultName { get; set; } = "{folder}";
    public NameClashConfig NameClash { get; set; } = new();

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public Dictionary<string, WorkType> Work { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
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
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonOptions));
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

    public WorkType? FindWork(string name) => Work.GetValueOrDefault(name);
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

using Claudebox.Terminal;

namespace Claudebox.Registry;

/// <summary>Changes for `cb set`. Null means leave as is; "none" clears a colour or scheme.</summary>
internal sealed record SessionChanges(string? Name = null, string? Work = null, string? Colour = null, string? Scheme = null)
{
    public bool IsEmpty => Name is null && Work is null && Colour is null && Scheme is null;

    /// <summary>Applies the changes to one record in the file; returns an error message, or null on success.</summary>
    public string? ApplyTo(SessionFile file, string id, Config config)
    {
        if (file.Sessions.Find(s => s.Id == id) is not { } record)
            return "That session has just been removed.";

        if (Name is not null)
        {
            var name = NameClash.CleanName(Name);
            if (name.Length == 0)
                return "The name can't be empty.";
            if (file.Sessions.Find(s => s.Id != id && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } other)
                return $"A session called '{other.Name}' already exists.";
            record.Name = name;
        }

        // A new work type brings its colour and scheme, unless they're given explicitly too.
        if (Work is not null)
        {
            if (config.FindWork(Work) is not { } workType)
                return $"Unknown work type '{Work}'. Configured: {string.Join(", ", config.Work.Keys)}.";
            record.Work = Work;
            record.Colour = workType.Colour is null ? null : Colours.Validate(workType.Colour);
            record.Scheme = workType.Scheme;
        }

        if (Colour is not null)
            record.Colour = IsNone(Colour) ? null : Colours.Validate(Colour);
        if (Scheme is not null)
            record.Scheme = IsNone(Scheme) ? null : Scheme;

        return null;
    }

    private static bool IsNone(string value) => value.Equals("none", StringComparison.OrdinalIgnoreCase);
}

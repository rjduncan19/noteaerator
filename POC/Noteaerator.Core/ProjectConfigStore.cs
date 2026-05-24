// On-disk schema and round-tripping for projects.json (the file that lists the
// project folders the viewer should open, plus per-project settings).
//
// Design goals (per issue #4):
//
//   * Backwards-compatible reader. The original v0.1.0 schema was a plain
//     JSON array of folder paths (strings). v0.1.3 introduced an object form
//     with { path, groupByPrefix }. Both shapes still load.
//
//   * Forwards-compatible reader. If a future version adds new properties to
//     a project object, an older build must still load the file without
//     erroring, must keep the properties it understands, and must NOT drop
//     the unrecognized properties when it saves the file back. We achieve
//     that by stashing every unknown property in a per-project Extra dict
//     and re-emitting it on save.
//
//   * Forwards-compatible reader for the top-level array. If a future
//     version adds a non-string, non-object element to the root array (e.g.
//     a settings object), an older build preserves the raw JsonElement and
//     re-emits it on save (appended after the known projects).
//
//   * Minimal diff on save. Writer always uses the same key names and
//     property order (path, groupByPrefix, then any extras in their original
//     insertion order). Old string-form entries are upgraded to objects on
//     save (that's the price of having any settings at all), but otherwise
//     the file is rewritten without reshuffling user data.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Noteaerator.Core;

/// <summary>
/// One project entry in <c>projects.json</c>. <see cref="Extra"/> holds any
/// JSON properties the reader didn't recognize — they are round-tripped
/// verbatim on save so newer-format data is never lost when an older build
/// rewrites the file.
/// </summary>
public sealed class ProjectConfig
{
    public string Path { get; set; } = "";
    public bool GroupByPrefix { get; set; } = true;

    /// <summary>
    /// Unknown JSON properties found on this project's object on load.
    /// Preserved on save (with their original key names and JSON values).
    /// </summary>
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// Result of <see cref="ProjectConfigStore.Parse"/>. <see cref="Projects"/>
/// is the list the app cares about; <see cref="UnknownRootItems"/> holds any
/// top-level array elements that weren't strings or recognizable objects
/// (forward-compat slot for future schema versions).
/// </summary>
public sealed class ProjectConfigFile
{
    public List<ProjectConfig> Projects { get; } = new();

    /// <summary>
    /// Top-level array elements we couldn't classify (i.e. neither a string
    /// nor an object). Preserved on save and appended after the known
    /// projects so future schema additions round-trip cleanly.
    /// </summary>
    public List<JsonElement> UnknownRootItems { get; } = new();
}

public static class ProjectConfigStore
{
    private const string KeyPath = "path";
    private const string KeyGroupByPrefix = "groupByPrefix";

    /// <summary>
    /// Parse a projects.json payload. Never throws on unrecognized shapes;
    /// instead, returns whatever it could classify and preserves the rest in
    /// <see cref="ProjectConfigFile.UnknownRootItems"/> / per-project
    /// <see cref="ProjectConfig.Extra"/>.
    /// </summary>
    /// <remarks>
    /// Throws only if the input is not valid JSON at all (caller decides how
    /// to surface that — typically by treating the file as missing).
    /// </remarks>
    public static ProjectConfigFile Parse(string json)
    {
        var result = new ProjectConfigFile();
        if (string.IsNullOrWhiteSpace(json)) return result;

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.String:
                    // Legacy v0.1.0 form: bare folder path.
                    result.Projects.Add(new ProjectConfig
                    {
                        Path = el.GetString() ?? "",
                        GroupByPrefix = true
                    });
                    break;

                case JsonValueKind.Object:
                    result.Projects.Add(ParseProjectObject(el));
                    break;

                default:
                    // Unknown top-level item shape. Preserve verbatim for
                    // forward-compat. Clone() detaches from the JsonDocument
                    // so the element is safe to use after disposal.
                    result.UnknownRootItems.Add(el.Clone());
                    break;
            }
        }

        return result;
    }

    private static ProjectConfig ParseProjectObject(JsonElement obj)
    {
        var cfg = new ProjectConfig { GroupByPrefix = true };
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, KeyPath,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    cfg.Path = prop.Value.GetString() ?? "";
                // Unexpected shape for a known key: silently drop it. We
                // can't round-trip a string-typed slot as something else
                // without colliding with our own writer.
            }
            else if (string.Equals(prop.Name, KeyGroupByPrefix,
                         System.StringComparison.OrdinalIgnoreCase))
            {
                if (prop.Value.ValueKind == JsonValueKind.True
                    || prop.Value.ValueKind == JsonValueKind.False)
                {
                    cfg.GroupByPrefix = prop.Value.GetBoolean();
                }
            }
            else
            {
                cfg.Extra ??= new Dictionary<string, JsonElement>();
                // Last-write-wins on duplicate keys, matching JsonSerializer
                // behavior. Clone() detaches from the JsonDocument.
                cfg.Extra[prop.Name] = prop.Value.Clone();
            }
        }
        return cfg;
    }

    /// <summary>
    /// Serialize the file model back to JSON. Property order per project is
    /// stable (path, groupByPrefix, then any extras in their insertion
    /// order) so the file diff stays minimal across saves.
    /// </summary>
    public static string Serialize(ProjectConfigFile file)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            foreach (var cfg in file.Projects)
                WriteProject(w, cfg);
            // Forward-compat: re-emit unknown root items after the known
            // projects so future schema additions don't get lost.
            foreach (var el in file.UnknownRootItems)
                el.WriteTo(w);
            w.WriteEndArray();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteProject(Utf8JsonWriter w, ProjectConfig cfg)
    {
        w.WriteStartObject();
        w.WriteString(KeyPath, cfg.Path);
        w.WriteBoolean(KeyGroupByPrefix, cfg.GroupByPrefix);
        if (cfg.Extra != null)
        {
            foreach (var kv in cfg.Extra)
            {
                // Guard against a future version's writer accidentally
                // duplicating a known key in Extra. (Parser doesn't put
                // known keys in Extra, but a hand-edited file could.)
                if (string.Equals(kv.Key, KeyPath,
                        System.StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(kv.Key, KeyGroupByPrefix,
                        System.StringComparison.OrdinalIgnoreCase)) continue;
                w.WritePropertyName(kv.Key);
                kv.Value.WriteTo(w);
            }
        }
        w.WriteEndObject();
    }
}

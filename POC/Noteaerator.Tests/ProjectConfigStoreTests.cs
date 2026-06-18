using System.Text.Json;
using Noteaerator.Core;

namespace Noteaerator.Tests;

public sealed class ProjectConfigStoreTests
{
    // ---------------- Backwards compat ----------------

    [Fact]
    public void Parses_legacy_string_array_form_with_groupByPrefix_defaulting_true()
    {
        var file = ProjectConfigStore.Parse("[\"C:/work\",\"C:/home\"]");
        Assert.Equal(2, file.Projects.Count);
        Assert.Equal("C:/work", file.Projects[0].Path);
        Assert.True(file.Projects[0].GroupByPrefix);
        Assert.Equal("C:/home", file.Projects[1].Path);
        Assert.True(file.Projects[1].GroupByPrefix);
    }

    [Fact]
    public void Parses_current_object_form_with_path_and_groupByPrefix()
    {
        var file = ProjectConfigStore.Parse(
            "[{\"path\":\"C:/a\",\"groupByPrefix\":false}]");
        Assert.Single(file.Projects);
        Assert.Equal("C:/a", file.Projects[0].Path);
        Assert.False(file.Projects[0].GroupByPrefix);
    }

    [Fact]
    public void Property_names_are_case_insensitive_for_known_keys()
    {
        var file = ProjectConfigStore.Parse(
            "[{\"Path\":\"C:/a\",\"GroupByPrefix\":false}]");
        Assert.Equal("C:/a", file.Projects[0].Path);
        Assert.False(file.Projects[0].GroupByPrefix);
    }

    [Fact]
    public void Empty_or_whitespace_input_returns_empty_file()
    {
        Assert.Empty(ProjectConfigStore.Parse("").Projects);
        Assert.Empty(ProjectConfigStore.Parse("   ").Projects);
    }

    [Fact]
    public void Non_array_root_returns_empty_file_without_throwing()
    {
        // E.g. someone hand-edited the file into an object. We don't crash;
        // we just treat it as having no projects.
        var file = ProjectConfigStore.Parse("{\"projects\":[]}");
        Assert.Empty(file.Projects);
        Assert.Empty(file.UnknownRootItems);
    }

    // ---------------- Forwards compat: per-project Extra ----------------

    [Fact]
    public void Unknown_properties_are_captured_in_Extra()
    {
        var file = ProjectConfigStore.Parse(
            "[{\"path\":\"C:/a\",\"groupByPrefix\":true," +
            "\"theme\":\"dark\",\"pinned\":true}]");
        var cfg = file.Projects[0];
        Assert.NotNull(cfg.Extra);
        Assert.Equal(2, cfg.Extra!.Count);
        Assert.Equal("dark", cfg.Extra["theme"].GetString());
        Assert.True(cfg.Extra["pinned"].GetBoolean());
    }

    [Fact]
    public void Unknown_properties_round_trip_through_serialize()
    {
        var input = "[{\"path\":\"C:/a\",\"groupByPrefix\":true," +
                    "\"future\":{\"nested\":[1,2,3]},\"flag\":false}]";
        var file = ProjectConfigStore.Parse(input);
        var output = ProjectConfigStore.Serialize(file);

        // Re-parse the output and verify the unknowns survived intact.
        var roundtripped = ProjectConfigStore.Parse(output);
        var cfg = roundtripped.Projects[0];
        Assert.Equal("C:/a", cfg.Path);
        Assert.True(cfg.GroupByPrefix);
        Assert.NotNull(cfg.Extra);
        Assert.Equal(JsonValueKind.Object, cfg.Extra!["future"].ValueKind);
        Assert.Equal(3, cfg.Extra["future"].GetProperty("nested").GetArrayLength());
        Assert.False(cfg.Extra["flag"].GetBoolean());
    }

    [Fact]
    public void Unknown_property_with_complex_value_is_emitted_verbatim()
    {
        var input = "[{\"path\":\"C:/a\",\"groupByPrefix\":true," +
                    "\"tags\":[\"red\",\"blue\"]}]";
        var output = ProjectConfigStore.Serialize(ProjectConfigStore.Parse(input));
        Assert.Contains("\"tags\":[\"red\",\"blue\"]", output);
    }

    // ---------------- Forwards compat: unknown root items ----------------

    [Fact]
    public void Non_string_non_object_root_items_are_preserved_as_UnknownRootItems()
    {
        // A future version might add a top-level settings object or a
        // version-marker number. An older reader keeps them around.
        var input = "[{\"path\":\"C:/a\",\"groupByPrefix\":true},42,[\"nested\"]]";
        var file = ProjectConfigStore.Parse(input);
        Assert.Single(file.Projects);
        Assert.Equal(2, file.UnknownRootItems.Count);
        Assert.Equal(42, file.UnknownRootItems[0].GetInt32());
        Assert.Equal(JsonValueKind.Array, file.UnknownRootItems[1].ValueKind);
    }

    [Fact]
    public void Unknown_root_items_are_appended_after_projects_on_save()
    {
        var input = "[{\"path\":\"C:/a\",\"groupByPrefix\":true},42]";
        var output = ProjectConfigStore.Serialize(ProjectConfigStore.Parse(input));
        // The known project comes first, then the unknown numeric element.
        Assert.Equal(
            "[{\"path\":\"C:/a\",\"groupByPrefix\":true},42]",
            output);
    }

    // ---------------- Save shape ----------------

    [Fact]
    public void Serialize_emits_path_then_groupByPrefix_in_stable_order()
    {
        var file = new ProjectConfigFile();
        file.Projects.Add(new ProjectConfig { Path = "C:/x", GroupByPrefix = false });
        var output = ProjectConfigStore.Serialize(file);
        Assert.Equal("[{\"path\":\"C:/x\",\"groupByPrefix\":false}]", output);
    }

    [Fact]
    public void Saving_a_legacy_string_entry_upgrades_it_to_object_form_with_defaults()
    {
        // This is the one schema change we can't avoid — turning bare strings
        // into objects with the per-project settings. Document the behavior.
        var output = ProjectConfigStore.Serialize(
            ProjectConfigStore.Parse("[\"C:/legacy\"]"));
        Assert.Equal("[{\"path\":\"C:/legacy\",\"groupByPrefix\":true}]", output);
    }

    [Fact]
    public void Extra_entries_never_shadow_known_keys_on_save()
    {
        // Defensive: if a hand-edited file (or a future writer bug) stuffs a
        // known key into Extra, the writer drops it rather than duplicating
        // it next to the canonical emission.
        var cfg = new ProjectConfig
        {
            Path = "C:/x",
            GroupByPrefix = true,
            Extra = new Dictionary<string, JsonElement>
            {
                ["path"] = JsonDocument.Parse("\"WRONG\"").RootElement.Clone(),
                ["custom"] = JsonDocument.Parse("1").RootElement.Clone(),
            }
        };
        var file = new ProjectConfigFile();
        file.Projects.Add(cfg);
        var output = ProjectConfigStore.Serialize(file);
        Assert.Equal(
            "[{\"path\":\"C:/x\",\"groupByPrefix\":true,\"custom\":1}]",
            output);
    }

    // ---------------- showFolders + hidden (view mode + hide feature) -------

    [Fact]
    public void ShowFolders_defaults_false_when_absent()
    {
        var file = ProjectConfigStore.Parse("[{\"path\":\"C:/a\"}]");
        Assert.False(file.Projects[0].ShowFolders);
    }

    [Fact]
    public void Parses_showFolders_and_hidden()
    {
        var file = ProjectConfigStore.Parse(
            "[{\"path\":\"C:/a\",\"groupByPrefix\":false,\"showFolders\":true," +
            "\"hidden\":[\"secret.md\",\"drafts\"]}]");
        var cfg = file.Projects[0];
        Assert.True(cfg.ShowFolders);
        Assert.Equal(2, cfg.Hidden.Count);
        Assert.Contains("secret.md", cfg.Hidden);
        Assert.Contains("drafts", cfg.Hidden);
    }

    [Fact]
    public void Hidden_defaults_empty_when_absent()
    {
        var file = ProjectConfigStore.Parse("[{\"path\":\"C:/a\"}]");
        Assert.Empty(file.Projects[0].Hidden);
    }

    [Fact]
    public void Serialize_omits_showFolders_when_false_and_hidden_when_empty()
    {
        // Backward compat: a project with neither feature in use serializes
        // exactly like it did before these fields existed.
        var file = new ProjectConfigFile();
        file.Projects.Add(new ProjectConfig { Path = "C:/x", GroupByPrefix = true });
        var output = ProjectConfigStore.Serialize(file);
        Assert.Equal("[{\"path\":\"C:/x\",\"groupByPrefix\":true}]", output);
    }

    [Fact]
    public void Serialize_emits_showFolders_and_hidden_when_set()
    {
        var file = new ProjectConfigFile();
        file.Projects.Add(new ProjectConfig
        {
            Path = "C:/x",
            GroupByPrefix = false,
            ShowFolders = true,
            Hidden = new List<string> { "a.md", "sub" }
        });
        var output = ProjectConfigStore.Serialize(file);
        Assert.Equal(
            "[{\"path\":\"C:/x\",\"groupByPrefix\":false,\"showFolders\":true," +
            "\"hidden\":[\"a.md\",\"sub\"]}]",
            output);
    }

    [Fact]
    public void ShowFolders_and_hidden_round_trip()
    {
        var file = new ProjectConfigFile();
        file.Projects.Add(new ProjectConfig
        {
            Path = "C:/x",
            GroupByPrefix = false,
            ShowFolders = true,
            Hidden = new List<string> { "secret.md", "private/notes.md" }
        });
        var output = ProjectConfigStore.Serialize(file);
        var reparsed = ProjectConfigStore.Parse(output).Projects[0];
        Assert.True(reparsed.ShowFolders);
        Assert.Equal(new[] { "secret.md", "private/notes.md" }, reparsed.Hidden);
    }

    [Fact]
    public void Unknown_keys_alongside_showFolders_still_round_trip()
    {
        // Forward compat: a future build's extra key survives a save by an
        // older build that also understands showFolders/hidden.
        var input = "[{\"path\":\"C:/a\",\"groupByPrefix\":true,\"showFolders\":true," +
                    "\"futureKey\":{\"x\":1}}]";
        var output = ProjectConfigStore.Serialize(ProjectConfigStore.Parse(input));
        var reparsed = ProjectConfigStore.Parse(output).Projects[0];
        Assert.True(reparsed.ShowFolders);
        Assert.NotNull(reparsed.Extra);
        Assert.True(reparsed.Extra!.ContainsKey("futureKey"));
    }
}

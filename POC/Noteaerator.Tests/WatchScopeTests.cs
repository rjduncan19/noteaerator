using Noteaerator.Core;

namespace Noteaerator.Tests;

/// <summary>
/// Tests for the watch-relevance decision that drives auto-refresh. The key
/// behavior: in the default (prefix/flat) modes only the project's own folder
/// and its archive matter, but in "Show folders" mode changes anywhere beneath
/// the project folder must also trigger a refresh.
/// </summary>
public sealed class WatchScopeTests
{
    private const string Root = @"C:\proj";
    private const string Archive = @"C:\proj\archive";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Top_level_md_is_relevant_in_any_mode(bool showFolders)
    {
        Assert.True(WatchScope.IsRelevant(Root, Archive, @"C:\proj\note.md", showFolders));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Top_level_sidecar_is_relevant_in_any_mode(bool showFolders)
    {
        Assert.True(WatchScope.IsRelevant(Root, Archive,
            @"C:\proj\note-comments.json", showFolders));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Archive_file_is_relevant_in_any_mode(bool showFolders)
    {
        Assert.True(WatchScope.IsRelevant(Root, Archive,
            @"C:\proj\archive\old.md", showFolders));
    }

    [Fact]
    public void Nested_file_is_NOT_relevant_without_show_folders()
    {
        Assert.False(WatchScope.IsRelevant(Root, Archive,
            @"C:\proj\sub\child.md", showFolders: false));
    }

    [Fact]
    public void Nested_file_IS_relevant_with_show_folders()
    {
        Assert.True(WatchScope.IsRelevant(Root, Archive,
            @"C:\proj\sub\child.md", showFolders: true));
    }

    [Fact]
    public void Deeply_nested_file_is_relevant_only_with_show_folders()
    {
        var deep = @"C:\proj\a\b\c\deep.md";
        Assert.False(WatchScope.IsRelevant(Root, Archive, deep, showFolders: false));
        Assert.True(WatchScope.IsRelevant(Root, Archive, deep, showFolders: true));
    }

    [Fact]
    public void Nested_sidecar_is_relevant_with_show_folders()
    {
        Assert.True(WatchScope.IsRelevant(Root, Archive,
            @"C:\proj\sub\child-comments.json", showFolders: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Path_outside_project_is_never_relevant(bool showFolders)
    {
        Assert.False(WatchScope.IsRelevant(Root, Archive,
            @"C:\other\note.md", showFolders));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sibling_folder_sharing_a_name_prefix_is_not_relevant(bool showFolders)
    {
        // "C:\proj2" must not be treated as inside "C:\proj".
        Assert.False(WatchScope.IsRelevant(Root, Archive,
            @"C:\proj2\note.md", showFolders));
    }

    [Fact]
    public void Trailing_separator_on_root_is_tolerated()
    {
        Assert.True(WatchScope.IsRelevant(@"C:\proj\", Archive,
            @"C:\proj\note.md", showFolders: false));
        Assert.True(WatchScope.IsRelevant(@"C:\proj\", Archive,
            @"C:\proj\sub\child.md", showFolders: true));
    }

    [Fact]
    public void Forward_slash_paths_are_handled()
    {
        Assert.True(WatchScope.IsRelevant("C:/proj", "C:/proj/archive",
            "C:/proj/note.md", showFolders: false));
        Assert.True(WatchScope.IsRelevant("C:/proj", "C:/proj/archive",
            "C:/proj/sub/child.md", showFolders: true));
        Assert.False(WatchScope.IsRelevant("C:/proj", "C:/proj/archive",
            "C:/proj/sub/child.md", showFolders: false));
    }

    [Fact]
    public void Null_archive_is_tolerated()
    {
        Assert.True(WatchScope.IsRelevant(Root, null, @"C:\proj\note.md", false));
        Assert.False(WatchScope.IsRelevant(Root, null, @"C:\proj\sub\x.md", false));
    }

    // ---- Hidden folders are not monitored (unless Show hidden files is on) ----

    // Treats anything under "C:\proj\node_modules" (or the folder itself) as hidden.
    private static bool HiddenNodeModules(string p) =>
        p.StartsWith(@"C:\proj\node_modules\", System.StringComparison.OrdinalIgnoreCase)
        || string.Equals(p, @"C:\proj\node_modules", System.StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Change_inside_hidden_folder_is_not_relevant_by_default()
    {
        // Show-folders mode is on, so structurally this nested file would match —
        // but it lives under a hidden folder and Show hidden files is off.
        Assert.False(WatchScope.IsRelevant(Root, Archive,
            @"C:\proj\node_modules\pkg\readme.md",
            showFolders: true, isHidden: HiddenNodeModules, showHidden: false));
    }

    [Fact]
    public void Change_inside_hidden_folder_is_relevant_when_showing_hidden()
    {
        Assert.True(WatchScope.IsRelevant(Root, Archive,
            @"C:\proj\node_modules\pkg\readme.md",
            showFolders: true, isHidden: HiddenNodeModules, showHidden: true));
    }

    [Fact]
    public void Hidden_top_level_file_is_not_relevant_by_default()
    {
        bool isHidden(string p) => string.Equals(p, @"C:\proj\secret.md",
            System.StringComparison.OrdinalIgnoreCase);
        Assert.False(WatchScope.IsRelevant(Root, Archive, @"C:\proj\secret.md",
            showFolders: false, isHidden: isHidden, showHidden: false));
        // ...but a normal sibling still is.
        Assert.True(WatchScope.IsRelevant(Root, Archive, @"C:\proj\note.md",
            showFolders: false, isHidden: isHidden, showHidden: false));
    }

    [Fact]
    public void Non_hidden_nested_file_is_still_relevant_with_predicate_present()
    {
        Assert.True(WatchScope.IsRelevant(Root, Archive,
            @"C:\proj\docs\guide.md",
            showFolders: true, isHidden: HiddenNodeModules, showHidden: false));
    }
}

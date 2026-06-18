using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Noteaerator.Core;

namespace Noteaerator.Tests;

/// <summary>
/// Tests for the "Show folders" (sub-directory) view-mode builder. Uses a real
/// temp directory tree so the on-disk enumeration is exercised end to end.
/// </summary>
public sealed class SubDirListingTests : IDisposable
{
    private readonly string _root;

    public SubDirListingTests()
    {
        _root = Path.Combine(Path.GetTempPath(),
            "naerator-subdir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void File_(string relPath)
    {
        var full = Path.Combine(_root, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "# test");
    }

    private List<FileListRow> Build(ISet<string>? expanded = null,
        Func<string, bool>? isHidden = null, bool showHidden = false)
        => SubDirListing.Build(
            _root,
            expanded ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            isHidden ?? (_ => false),
            showHidden).ToList();

    [Fact]
    public void Top_level_files_are_flat_at_depth_zero()
    {
        File_("alpha.md");
        File_("beta.md");

        var rows = Build();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(0, r.Depth));
        Assert.All(rows, r => Assert.True(r.IsFile));
        Assert.Contains(rows, r => r.Display == "alpha.md");
        Assert.Contains(rows, r => r.Display == "beta.md");
    }

    [Fact]
    public void Subdirectory_appears_as_collapsed_folder_row()
    {
        File_("top.md");
        File_("sub/child.md");

        var rows = Build();

        // top.md + the "sub" folder row (collapsed → child not listed).
        Assert.Equal(2, rows.Count);
        var folder = rows.Single(r => r.IsFolder);
        Assert.Contains("sub", folder.Display);
        Assert.True(folder.HasChildren);
        Assert.False(folder.IsExpanded);
        Assert.DoesNotContain(rows, r => r.Display == "child.md");
    }

    [Fact]
    public void Expanded_subdirectory_reveals_children_at_next_depth()
    {
        File_("top.md");
        File_("sub/child.md");
        var subDir = Path.Combine(_root, "sub");

        var rows = Build(expanded: new HashSet<string>(
            new[] { subDir }, StringComparer.OrdinalIgnoreCase));

        var child = rows.Single(r => r.IsFile && r.Display == "child.md");
        Assert.Equal(1, child.Depth);
        var folder = rows.Single(r => r.IsFolder);
        Assert.True(folder.IsExpanded);
    }

    [Fact]
    public void Nested_subdirectories_recurse_to_any_depth()
    {
        File_("a/b/c/deep.md");
        var a = Path.Combine(_root, "a");
        var b = Path.Combine(a, "b");
        var c = Path.Combine(b, "c");

        var rows = Build(expanded: new HashSet<string>(
            new[] { a, b, c }, StringComparer.OrdinalIgnoreCase));

        var deep = rows.Single(r => r.IsFile && r.Display == "deep.md");
        Assert.Equal(3, deep.Depth);
    }

    [Fact]
    public void Empty_folders_are_omitted()
    {
        File_("keep.md");
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        // A folder whose only descendant is another empty folder is also gone.
        Directory.CreateDirectory(Path.Combine(_root, "outer", "inner"));

        var rows = Build();

        Assert.DoesNotContain(rows, r => r.IsFolder);
        Assert.Single(rows);
    }

    [Fact]
    public void Top_level_archive_folder_is_skipped()
    {
        File_("note.md");
        File_("archive/old.md");

        var rows = Build();

        Assert.DoesNotContain(rows, r => r.IsFolder);
        Assert.Single(rows);
        Assert.Equal("note.md", rows[0].Display);
    }

    [Fact]
    public void Hidden_files_are_excluded_by_default()
    {
        File_("visible.md");
        File_("secret.md");
        var secret = Path.Combine(_root, "secret.md");

        var rows = Build(isHidden: p => string.Equals(p, secret,
            StringComparison.OrdinalIgnoreCase));

        Assert.Single(rows);
        Assert.Equal("visible.md", rows[0].Display);
    }

    [Fact]
    public void Hidden_files_are_shown_and_marked_when_showHidden()
    {
        File_("visible.md");
        File_("secret.md");
        var secret = Path.Combine(_root, "secret.md");

        var rows = Build(
            isHidden: p => string.Equals(p, secret, StringComparison.OrdinalIgnoreCase),
            showHidden: true);

        Assert.Equal(2, rows.Count);
        var secretRow = rows.Single(r => r.Display == "secret.md");
        Assert.True(secretRow.IsHidden);
        var visibleRow = rows.Single(r => r.Display == "visible.md");
        Assert.False(visibleRow.IsHidden);
    }

    [Fact]
    public void Hidden_folder_is_excluded_by_default()
    {
        File_("top.md");
        File_("private/inside.md");
        var priv = Path.Combine(_root, "private");

        var rows = Build(isHidden: p => string.Equals(p, priv,
            StringComparison.OrdinalIgnoreCase));

        Assert.Single(rows);
        Assert.Equal("top.md", rows[0].Display);
    }

    [Fact]
    public void Agents_md_sorts_after_other_top_level_files()
    {
        File_("AGENTS.md");
        File_("readme.md");
        File_("zeta.md");

        var rows = Build().Where(r => r.IsFile).ToList();

        Assert.Equal("AGENTS.md", rows.Last().Display);
    }
}

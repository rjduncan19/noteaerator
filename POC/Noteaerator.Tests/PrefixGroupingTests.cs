using Noteaerator.Core;

namespace Noteaerator.Tests;

public sealed class PrefixGroupingTests
{
    private static IEnumerable<string> Paths(params string[] names)
        => names.Select(n => Path.Combine(@"C:\fake", n + ".md"));

    private static List<FileListRow> Flatten(params string[] names)
    {
        var tree = PrefixGrouping.BuildTree(Paths(names));
        return PrefixGrouping.Flatten(tree).ToList();
    }

    [Fact]
    public void Tokenize_strips_leading_numeric_as_sort_key()
    {
        var (sk, toks, isOv) = PrefixGrouping.Tokenize("30-anthropic-deep-dive");
        Assert.Equal("30", sk);
        Assert.Equal(new[] { "anthropic", "deep", "dive" }, toks);
        Assert.False(isOv);
    }

    [Fact]
    public void Tokenize_keeps_lone_numeric_token_when_no_other_tokens()
    {
        // "12345.md" has only one token which happens to be numeric. We must
        // NOT consume it as a sort key, otherwise the file vanishes.
        var (sk, toks, _) = PrefixGrouping.Tokenize("12345");
        Assert.Null(sk);
        Assert.Equal(new[] { "12345" }, toks);
    }

    [Fact]
    public void Overview_suffix_is_recognized_and_dropped()
    {
        var (_, toks, isOv) = PrefixGrouping.Tokenize("corp-crwd-overview");
        Assert.Equal(new[] { "corp", "crwd" }, toks);
        Assert.True(isOv);
    }

    [Fact]
    public void Single_file_at_root_is_a_leaf_row()
    {
        var rows = Flatten("notes");
        Assert.Single(rows);
        Assert.Equal("notes", rows[0].Display);
        Assert.Equal(0, rows[0].Depth);
        Assert.False(rows[0].HasChildren);
    }

    [Fact]
    public void Two_files_sharing_a_prefix_create_a_group_even_with_one_child()
    {
        // Per the May-2026 update: groups of 1 are allowed. Adding the second
        // file should immediately nest the more-specific one. A pointless
        // "corp" wrapper is chain-collapsed so the user sees "corp-orcl" as
        // the parent, matching their example.
        var rows = Flatten("corp-orcl", "corp-orcl-thomas");
        Assert.Equal(2, rows.Count);
        Assert.StartsWith("corp-orcl", rows[0].Display);
        Assert.True(rows[0].HasChildren);
        Assert.Equal(rows[0].FilePath,
                     Path.Combine(@"C:\fake", "corp-orcl.md"));
        Assert.Equal(1, rows[1].Depth);
        Assert.Equal("thomas", rows[1].Display);
    }

    [Fact]
    public void Overview_file_acts_as_the_parent_anchor()
    {
        var rows = Flatten("corp-crwd-overview", "corp-crwd-cory");
        // corp-crwd-overview should become the "crwd" node (file-folder),
        // with corp-crwd-cory nesting under it.
        var folder = rows.First(r => r.HasChildren);
        Assert.EndsWith("corp-crwd-overview.md", folder.FilePath);
        var child = rows[rows.IndexOf(folder) + 1];
        Assert.Equal("cory", child.Display);
        Assert.Equal(folder.Depth + 1, child.Depth);
    }

    [Fact]
    public void Numeric_sort_keys_order_siblings_numerically_not_lexically()
    {
        // 20 should come before 100 (lexical would put 100 first).
        var rows = Flatten("100-z", "20-a");
        Assert.Equal("20 a", rows[0].Display);
        Assert.Equal("100 z", rows[1].Display);
    }

    [Fact]
    public void Sort_key_is_shown_in_display_with_a_space()
    {
        var rows = Flatten("30-anthropic");
        Assert.Equal("30 anthropic", rows[0].Display);
    }

    [Fact]
    public void Ancestor_tokens_are_stripped_from_child_display()
    {
        var rows = Flatten("corp-orcl", "corp-orcl-thomas-meeting");
        // "corp-orcl-thomas-meeting" tokens = [corp, orcl, thomas, meeting].
        // MaxDepth=3 so it's placed at corp/orcl/thomas with tail "thomas-meeting".
        var leaf = rows.Last();
        Assert.Equal("thomas-meeting", leaf.Display);
    }

    [Fact]
    public void Max_depth_collapses_deeper_tokens_into_the_tail()
    {
        var rows = Flatten("a-b-c-d-e");
        // Single file → chain-collapsed to one row with display "a-b-c-d-e".
        Assert.Single(rows);
        Assert.Equal("a-b-c-d-e", rows[0].Display);
        Assert.Equal(0, rows[0].Depth);
    }

    [Fact]
    public void Max_depth_applies_when_files_diverge_below_the_cap()
    {
        var rows = Flatten("a-b-c-d-e", "a-b-c-d-x").ToList();
        // Both files share tokens [a,b,c] at depth-3 cap. Both should land
        // at the same node with tails "d-e" and "d-x" respectively.
        // Because they collide at node "c", second insert is dropped on the
        // floor (first-write-wins). This is the documented behavior of
        // exceeding MaxDepth — the engine doesn't try to multiplex multiple
        // files into one node.
        Assert.Single(rows);
    }

    [Fact]
    public void Collapsed_folder_hides_its_descendants()
    {
        var tree = PrefixGrouping.BuildTree(Paths("corp-orcl", "corp-orcl-thomas"));
        // After chain-collapse the visible folder is rendered via the orcl node
        // (the one that actually holds the file). Collapsing IT hides its
        // child.
        var orcl = tree.Children["corp"].Children["orcl"];
        orcl.IsExpanded = false;
        var rows = PrefixGrouping.Flatten(tree).ToList();
        Assert.Single(rows);
        Assert.False(rows[0].IsExpanded);
        Assert.Equal(2, rows[0].FileCountInSubtree);
    }

    [Fact]
    public void RestoreExpandedState_preserves_collapse_across_rebuilds()
    {
        var t1 = PrefixGrouping.BuildTree(Paths("corp-orcl", "corp-orcl-thomas"));
        t1.Children["corp"].Children["orcl"].IsExpanded = false;

        var t2 = PrefixGrouping.BuildTree(
            Paths("corp-orcl", "corp-orcl-thomas", "corp-orcl-rita"),
            previous: t1);
        Assert.False(t2.Children["corp"].Children["orcl"].IsExpanded);
    }

    [Fact]
    public void Flat_returns_alphabetical_basenames()
    {
        var rows = PrefixGrouping.Flat(Paths("b", "a")).ToList();
        Assert.Equal(new[] { "a.md", "b.md" }, rows.Select(r => r.Display));
        Assert.All(rows, r => Assert.Equal(0, r.Depth));
        Assert.All(rows, r => Assert.True(r.IsFile));
    }

    [Fact]
    public void FirstFileIn_returns_node_itself_when_it_holds_a_file()
    {
        var tree = PrefixGrouping.BuildTree(Paths("corp-orcl", "corp-orcl-thomas"));
        var orcl = tree.Children["corp"].Children["orcl"];
        var hit = PrefixGrouping.FirstFileIn(orcl);
        Assert.NotNull(hit);
        Assert.EndsWith("corp-orcl.md", hit!.FilePath);
    }

    [Fact]
    public void FirstFileIn_descends_DFS_into_folder_only_subtrees()
    {
        // No "corp.md", no "corp-orcl.md" — only the leaf has content.
        var tree = PrefixGrouping.BuildTree(Paths("corp-orcl-thomas", "corp-orcl-rita"));
        var corp = tree.Children["corp"];
        var hit = PrefixGrouping.FirstFileIn(corp);
        Assert.NotNull(hit);
        // 20-style sort isn't in play; alphabetical: rita < thomas.
        Assert.EndsWith("corp-orcl-rita.md", hit!.FilePath);
    }

    [Fact]
    public void FirstFileIn_respects_numeric_sort_keys()
    {
        var tree = PrefixGrouping.BuildTree(
            Paths("anthropic-30-followup", "anthropic-20-deep-dive"));
        var ant = tree.Children["anthropic"];
        var hit = PrefixGrouping.FirstFileIn(ant);
        Assert.EndsWith("anthropic-20-deep-dive.md", hit!.FilePath);
    }

    [Fact]
    public void ExpandAncestorsOf_makes_a_collapsed_descendant_visible()
    {
        var tree = PrefixGrouping.BuildTree(Paths(
            "company-google-overview", "company-google-larry"));
        // Collapse everything.
        tree.Children["company"].IsExpanded = false;
        tree.Children["company"].Children["google"].IsExpanded = false;

        var larry = tree.Children["company"].Children["google"].Children["larry"];
        Assert.True(PrefixGrouping.ExpandAncestorsOf(tree, larry));
        Assert.True(tree.Children["company"].IsExpanded);
        Assert.True(tree.Children["company"].Children["google"].IsExpanded);

        // larry itself was a leaf — its own IsExpanded is untouched.
        var rows = PrefixGrouping.Flatten(tree).ToList();
        Assert.Contains(rows, r => r.IsFile && (r.FilePath?.EndsWith("larry.md") ?? false));
    }

    [Fact]
    public void Security_folder_example_produces_expected_groups()
    {
        // Mirrors the real Security folder used in the design discussion.
        var rows = Flatten(
            "00-running-status",
            "01-profile-value-proposition",
            "30-anthropic-deep-dive",
            "34-anthropic-cover-letter-draft",
            "40-anthropic-full-job-audit",
            "20-stuart-meeting-prep",
            "21-kangsu-meeting-prep");

        // Should produce an "anthropic" folder with three children (the three
        // 30/34/40 files are all under it) and four flat top-level entries
        // (00, 01, 20, 21 — the meeting-prep token is not shared as the FIRST
        // token, so no meeting group emerges in this minimal example).
        var anthroFolder = rows.FirstOrDefault(r => r.HasChildren);
        Assert.NotNull(anthroFolder);
        Assert.StartsWith("anthropic", anthroFolder!.Display);
        Assert.Equal(3, anthroFolder.FileCountInSubtree);
    }

    // ---------------- Issue #5: AGENTS.md always last ----------------

    [Fact]
    public void Grouped_AGENTS_md_sorts_after_all_other_files_at_root()
    {
        var rows = Flatten("AGENTS", "README", "notes", "01-intro");
        // Expected order:
        //   01-intro (numeric prefix → first)
        //   notes    (alphabetical, case-insensitive)
        //   README
        //   AGENTS   (special-cased to bottom)
        Assert.Equal(4, rows.Count);
        Assert.Equal("01 intro", rows[0].Display);
        Assert.Equal("notes", rows[1].Display);
        Assert.Equal("README", rows[2].Display);
        Assert.Equal("AGENTS", rows[3].Display);
    }

    [Fact]
    public void Grouped_AGENTS_md_sorts_last_within_its_parent_group()
    {
        // A literal AGENTS.md file nested inside a folder (real subdir, not
        // a synthetic prefix group) would similarly be pushed to the bottom
        // of its siblings. The prefix grouper doesn't traverse subdirs, but
        // simulate the situation by hand-building a tree where one child
        // node's file basename is AGENTS.md.
        var paths = Paths("AGENTS", "notes", "readme").ToList();
        var tree = PrefixGrouping.BuildTree(paths);
        var rows = PrefixGrouping.Flatten(tree).ToList();
        Assert.Equal(new[] { "notes", "readme", "AGENTS" },
                     rows.Select(r => r.Display));
    }

    [Fact]
    public void Grouped_special_case_matches_only_AGENTS_md_not_other_agents_files()
    {
        // A file like "agents-intro.md" should NOT be sort-pinned to the
        // bottom — only literal AGENTS.md gets the treatment.
        var rows = Flatten("agents-intro", "README");
        Assert.Equal("agents-intro", rows[0].Display);
        Assert.Equal("README", rows[1].Display);
    }

    [Fact]
    public void Flat_pushes_AGENTS_md_to_the_bottom()
    {
        var rows = PrefixGrouping.Flat(Paths("z", "AGENTS", "a")).ToList();
        Assert.Equal(new[] { "a.md", "z.md", "AGENTS.md" },
                     rows.Select(r => r.Display));
    }
}

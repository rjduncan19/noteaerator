// Prefix-based file grouping for Note Aerator's file list pane.
//
// Files are grouped by leading dash-separated tokens. A purely numeric leading
// token (e.g. "30-" in "30-anthropic-deep-dive.md") is treated as a sort key
// only — it is never used as a grouping token.
//
// Rules (per Decisions D1-D5 in POC/file-list-grouping-proposals.md, with the
// May 2026 update lowering the minimum group size to 1):
//
//   D1 Leading numeric token is a sort key, not a group.
//   D2 Recurse, but cap nesting at MaxDepth (default 3). Tokens beyond the
//      cap collapse into the deepest node's tail.
//   D3 1-member groups are allowed (file always lives at its prefix path).
//   D4 Inside a group, the redundant ancestor tokens are stripped from the
//      file's display name.
//   D5 Expand/collapse state is owned by the caller (it lives in PrefixNode
//      and is meant to be persisted by the host).
//
// Plus the "overview" alias rule:
//
//   `<prefix>-overview.md` is treated as `<prefix>.md` — the file represents
//   its parent prefix node and other `<prefix>-*` files nest under it.

using System.Collections.Generic;
using System.Linq;

namespace Noteaerator.Core;

public sealed class PrefixNode
{
    /// <summary>The token at this node (e.g. "corp"). Empty for the synthetic root.</summary>
    public string Token { get; }

    /// <summary>1 for top-level groups; root is 0.</summary>
    public int Depth { get; }

    /// <summary>Absolute path of the file that lives AT this node, if any.</summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// The leading numeric token of the file at this node (e.g. "30") when it
    /// was used as a sort key. Null if the file had no numeric prefix.
    /// </summary>
    public string? FileSortKey { get; private set; }

    /// <summary>
    /// Display label for the file at this node, after stripping ancestor tokens
    /// and the sort-key numeric prefix. Kept for back-compat; the flattener
    /// renders from Token + FileExtraTail + FileSortKey directly.
    /// </summary>
    public string? FileTail { get; private set; }

    /// <summary>True when this file came in as "<prefix>-overview.md".</summary>
    public bool IsOverviewAlias { get; private set; }

    public Dictionary<string, PrefixNode> Children { get; } =
        new(System.StringComparer.OrdinalIgnoreCase);

    public bool HasFile => FilePath != null;
    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// Caller-owned expand/collapse state. The grouping engine itself never
    /// flips this — only reads it when flattening.
    /// </summary>
    public bool IsExpanded { get; set; } = true;

    public PrefixNode(string token, int depth)
    {
        Token = token;
        Depth = depth;
    }

    internal void AttachFile(string path, string? sortKey, string extraTail, bool isOverview)
    {
        // First write wins, with one exception: an "-overview" file overrides
        // a previously-attached non-overview file at the same node, because
        // semantically the overview IS meant to represent the parent prefix.
        if (FilePath == null || (isOverview && !IsOverviewAlias))
        {
            FilePath = path;
            FileSortKey = sortKey;
            FileExtraTail = extraTail;
            IsOverviewAlias = isOverview;
        }
    }

    /// <summary>
    /// Tokens beyond MaxDepth that were collapsed into this leaf, joined with
    /// '-'. Empty string when no tokens were dropped.
    /// </summary>
    public string FileExtraTail { get; private set; } = "";
}

public sealed class FileListRow
{
    public int Depth { get; init; }

    /// <summary>Visible label.</summary>
    public string Display { get; init; } = "";

    /// <summary>Tooltip / full path; null for synthetic folder rows.</summary>
    public string? FilePath { get; init; }

    /// <summary>True if clicking this row should open a file.</summary>
    public bool IsFile => FilePath != null;

    /// <summary>True if this row has visible children (chevron should render).</summary>
    public bool HasChildren { get; init; }

    /// <summary>Current expand/collapse state (only meaningful when HasChildren).</summary>
    public bool IsExpanded { get; init; }

    /// <summary>
    /// Number of file descendants under this row's node (including the node's
    /// own file). Used by the UI to render a "(N)" badge on collapsed folders.
    /// </summary>
    public int FileCountInSubtree { get; init; }

    /// <summary>
    /// Back-pointer to the node, so the UI can flip IsExpanded and re-flatten
    /// when the user clicks a chevron.
    /// </summary>
    public PrefixNode? Node { get; init; }

    /// <summary>UI glyph: ▾ when expanded, ▸ when collapsed, blank otherwise.</summary>
    public string ChevronGlyph =>
        HasChildren ? (IsExpanded ? "\u25BE" : "\u25B8") : " ";
}

public static class PrefixGrouping
{
    public const int DefaultMaxDepth = 3;

    /// <summary>
    /// Build a prefix tree from the given .md file paths. Existing
    /// IsExpanded state from <paramref name="previous"/> is preserved when a
    /// node at the same path existed before (so re-enumerating a folder
    /// doesn't collapse everything).
    /// </summary>
    public static PrefixNode BuildTree(
        IEnumerable<string> filePaths,
        PrefixNode? previous = null,
        int maxDepth = DefaultMaxDepth)
    {
        var root = new PrefixNode("", 0);

        foreach (var path in filePaths)
        {
            InsertFile(root, path, maxDepth);
        }

        if (previous != null)
            RestoreExpandedState(root, previous);

        return root;
    }

    /// <summary>
    /// Flatten the tree into the rows a ListBox should show, honoring each
    /// node's IsExpanded state. Children are alphabetically sorted with
    /// numeric sort keys (e.g. "20" before "30") respected when present.
    /// </summary>
    public static IEnumerable<FileListRow> Flatten(PrefixNode root)
    {
        foreach (var row in FlattenChildren(root))
            yield return row;
    }

    private static IEnumerable<FileListRow> FlattenChildren(PrefixNode parent, int displayDepth = 0)
    {
        foreach (var child in SortedChildren(parent))
        {
            // Chain-collapse: a chain of file-less single-child nodes is
            // meaningless as a hierarchy ("corp > orcl" when there is no
            // corp.md and orcl is the only thing under corp). Walk down the
            // chain and render as one combined token (e.g. "corp-orcl"). This
            // is also what makes the user's example (corp-orcl.md +
            // corp-orcl-thomas.md => corp-orcl with thomas under it) work.
            var effective = child;
            var tokenChain = new List<string> { child.Token };
            while (!effective.HasFile && effective.Children.Count == 1)
            {
                var only = effective.Children.Values.First();
                tokenChain.Add(only.Token);
                effective = only;
            }

            var renderedToken = string.Join('-', tokenChain);
            var fileCount = CountFiles(effective);

            if (effective.HasFile && effective.HasChildren)
            {
                yield return new FileListRow
                {
                    Depth = displayDepth,
                    Display = RenderFileLabel(effective, renderedToken),
                    FilePath = effective.FilePath,
                    HasChildren = true,
                    IsExpanded = effective.IsExpanded,
                    FileCountInSubtree = fileCount,
                    Node = effective
                };
            }
            else if (effective.HasChildren)
            {
                yield return new FileListRow
                {
                    Depth = displayDepth,
                    Display = $"{renderedToken}  ({fileCount})",
                    FilePath = null,
                    HasChildren = true,
                    IsExpanded = effective.IsExpanded,
                    FileCountInSubtree = fileCount,
                    Node = effective
                };
            }
            else
            {
                yield return new FileListRow
                {
                    Depth = displayDepth,
                    Display = RenderFileLabel(effective, renderedToken),
                    FilePath = effective.FilePath,
                    HasChildren = false,
                    IsExpanded = false,
                    FileCountInSubtree = 1,
                    Node = effective
                };
            }

            if (effective.HasChildren && effective.IsExpanded)
            {
                foreach (var grand in FlattenChildren(effective, displayDepth + 1))
                    yield return grand;
            }
        }
    }

    private static string RenderFileLabel(PrefixNode node, string renderedTokenChain)
    {
        var tail = string.IsNullOrEmpty(node.FileExtraTail)
            ? renderedTokenChain
            : renderedTokenChain + "-" + node.FileExtraTail;
        return node.FileSortKey != null ? $"{node.FileSortKey} {tail}" : tail;
    }

    /// <summary>
    /// Flat ordering — used when grouping is OFF. Mirrors today's behavior:
    /// alphabetical by filename (case-insensitive), display = basename.
    /// </summary>
    public static IEnumerable<FileListRow> Flat(IEnumerable<string> filePaths)
    {
        foreach (var p in filePaths.OrderBy(System.IO.Path.GetFileName,
                                            System.StringComparer.OrdinalIgnoreCase))
        {
            yield return new FileListRow
            {
                Depth = 0,
                Display = System.IO.Path.GetFileName(p),
                FilePath = p,
                HasChildren = false,
                IsExpanded = false,
                FileCountInSubtree = 1
            };
        }
    }

    /// <summary>
    /// Returns the first file in DFS order under (or at) <paramref name="node"/>.
    /// Used by the UI to navigate when the user clicks the *label* of a
    /// synthetic folder row: they expect to land somewhere with content.
    /// Returns null if the subtree contains no files (shouldn't happen for
    /// any node that came from BuildTree, but safe to handle).
    /// </summary>
    public static PrefixNode? FirstFileIn(PrefixNode node)
    {
        if (node.HasFile) return node;
        foreach (var c in SortedChildren(node))
        {
            var hit = FirstFileIn(c);
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>
    /// Expand every ancestor of <paramref name="target"/> within
    /// <paramref name="root"/> so it becomes visible after flattening.
    /// Returns true when target was found (and expansion applied).
    /// </summary>
    public static bool ExpandAncestorsOf(PrefixNode root, PrefixNode target)
    {
        return Walk(root, new List<PrefixNode>());

        bool Walk(PrefixNode node, List<PrefixNode> ancestors)
        {
            if (ReferenceEquals(node, target))
            {
                foreach (var a in ancestors) a.IsExpanded = true;
                return true;
            }
            ancestors.Add(node);
            try
            {
                foreach (var child in node.Children.Values)
                    if (Walk(child, ancestors)) return true;
                return false;
            }
            finally { ancestors.RemoveAt(ancestors.Count - 1); }
        }
    }

    // -------------------- Internals --------------------

    private static void InsertFile(PrefixNode root, string path, int maxDepth)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var (sortKey, tokens, isOverview) = Tokenize(name);

        if (tokens.Count == 0)
        {
            tokens = new List<string> { name };
            sortKey = null;
        }

        var pathTokens = tokens.Count <= maxDepth
            ? tokens
            : tokens.Take(maxDepth).ToList();

        var node = root;
        for (int i = 0; i < pathTokens.Count - 1; i++)
        {
            node = GetOrCreateChild(node, pathTokens[i]);
        }
        var leaf = GetOrCreateChild(node, pathTokens[^1]);

        var extraTail = tokens.Count > maxDepth
            ? string.Join('-', tokens.Skip(maxDepth))
            : "";

        leaf.AttachFile(path, sortKey, extraTail, isOverview);
    }

    private static PrefixNode GetOrCreateChild(PrefixNode parent, string token)
    {
        if (!parent.Children.TryGetValue(token, out var child))
        {
            child = new PrefixNode(token, parent.Depth + 1);
            parent.Children[token] = child;
        }
        return child;
    }

    public static (string? sortKey, List<string> tokens, bool isOverview)
        Tokenize(string nameWithoutExt)
    {
        var parts = nameWithoutExt
            .Split('-', System.StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        string? sortKey = null;
        if (parts.Count > 1 && parts[0].All(char.IsDigit))
        {
            sortKey = parts[0];
            parts.RemoveAt(0);
        }

        bool isOverview = false;
        if (parts.Count > 1 &&
            parts[^1].Equals("overview", System.StringComparison.OrdinalIgnoreCase))
        {
            isOverview = true;
            parts.RemoveAt(parts.Count - 1);
        }

        return (sortKey, parts, isOverview);
    }

    private static string RenderDisplay(PrefixNode node, bool includeChildrenCount)
    {
        var sortKey = node.FileSortKey;
        var tail = string.IsNullOrEmpty(node.FileExtraTail)
            ? node.Token
            : node.Token + "-" + node.FileExtraTail;
        var label = sortKey != null ? $"{sortKey} {tail}" : tail;
        if (includeChildrenCount && node.HasChildren)
            label += $"  ({CountFiles(node)})";
        return label;
    }

    private static int CountFiles(PrefixNode node)
    {
        var n = node.HasFile ? 1 : 0;
        foreach (var c in node.Children.Values) n += CountFiles(c);
        return n;
    }

    private static IEnumerable<PrefixNode> SortedChildren(PrefixNode parent)
    {
        // Sort by (numeric sort key if present, then token) so 20-foo files come
        // before 30-bar files inside the same parent.
        return parent.Children.Values
            .OrderBy(c => SortRank(c))
            .ThenBy(c => c.Token, System.StringComparer.OrdinalIgnoreCase);
    }

    private static int SortRank(PrefixNode node)
    {
        // Files with explicit numeric sort key sort first (by key); everything
        // else gets int.MaxValue so it falls to the alphabetical tiebreaker.
        if (node.FileSortKey != null && int.TryParse(node.FileSortKey, out var n))
            return n;
        return int.MaxValue;
    }

    private static void RestoreExpandedState(PrefixNode current, PrefixNode previous)
    {
        current.IsExpanded = previous.IsExpanded;
        foreach (var kv in current.Children)
        {
            if (previous.Children.TryGetValue(kv.Key, out var prevChild))
                RestoreExpandedState(kv.Value, prevChild);
        }
    }
}

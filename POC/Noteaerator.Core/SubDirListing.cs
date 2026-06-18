// Sub-directory ("Show folders") view mode for Note Aerator's file pane.
//
// Unlike prefix grouping, this mode mirrors the folder structure on disk: the
// project's own .md files are listed flat at the top, and each sub-directory
// appears as an expandable chevron row that, when expanded, reveals its own
// .md files and nested sub-directories (recursively, to any depth).
//
// The output is the same FileListRow model the prefix grouping flattener
// produces, so the UI renders both modes through one ListBox + template.
//
// Visibility rules:
//   * The project's top-level "archive" folder is never shown here (it has its
//     own pane). Nested folders that merely happen to be named "archive" are
//     treated as normal content.
//   * Hidden files/folders are skipped entirely unless showHidden is true, in
//     which case they are emitted with IsHidden = true for dimmed rendering.
//   * A folder with no visible .md descendants is omitted (an empty shell adds
//     nothing but noise).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Noteaerator.Core;

public static class SubDirListing
{
    /// <summary>
    /// Build the flattened rows for the sub-directory view of
    /// <paramref name="rootDir"/>.
    /// </summary>
    /// <param name="rootDir">The project folder.</param>
    /// <param name="expandedDirs">
    /// Absolute paths of sub-directories currently expanded. Membership is
    /// caller-owned (transient UI state); the builder only reads it.
    /// </param>
    /// <param name="isHidden">
    /// Predicate returning true if an absolute file/dir path is user-hidden.
    /// </param>
    /// <param name="showHidden">When true, hidden items are shown (dimmed).</param>
    /// <param name="archiveSubdir">Name of the top-level archive folder to skip.</param>
    public static IEnumerable<FileListRow> Build(
        string rootDir,
        ISet<string> expandedDirs,
        Func<string, bool> isHidden,
        bool showHidden,
        string archiveSubdir = "archive")
    {
        var rows = new List<FileListRow>();
        AppendDir(rows, rootDir, 0, expandedDirs, isHidden, showHidden,
            archiveSubdir, isRoot: true);
        return rows;
    }

    private static void AppendDir(
        List<FileListRow> rows,
        string dir,
        int depth,
        ISet<string> expandedDirs,
        Func<string, bool> isHidden,
        bool showHidden,
        string archiveSubdir,
        bool isRoot)
    {
        // Files directly in this directory (flat, AGENTS.md last).
        foreach (var f in EnumMdSorted(dir))
        {
            bool hidden = isHidden(f);
            if (hidden && !showHidden) continue;
            rows.Add(new FileListRow
            {
                Depth = depth,
                Display = Path.GetFileName(f),
                FilePath = f,
                HasChildren = false,
                IsExpanded = false,
                FileCountInSubtree = 1,
                IsHidden = hidden
            });
        }

        // Sub-directories (alphabetical), each an expandable folder row.
        foreach (var sub in EnumDirsSorted(dir))
        {
            var name = Path.GetFileName(sub);
            if (isRoot && string.Equals(name, archiveSubdir,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            bool hidden = isHidden(sub);
            if (hidden && !showHidden) continue;

            int count = CountVisibleMd(sub, isHidden, showHidden);
            if (count == 0) continue; // nothing the user can see in here

            bool expanded = expandedDirs.Contains(sub);
            rows.Add(new FileListRow
            {
                Depth = depth,
                Display = $"\U0001F4C1 {name}  ({count})",
                DirPath = sub,
                HasChildren = true,
                IsExpanded = expanded,
                FileCountInSubtree = count,
                IsHidden = hidden
            });

            if (expanded)
                AppendDir(rows, sub, depth + 1, expandedDirs, isHidden,
                    showHidden, archiveSubdir, isRoot: false);
        }
    }

    /// <summary>
    /// Count .md files visible under <paramref name="dir"/> (recursively),
    /// honoring the hidden filter. Used both to decide whether to show a folder
    /// and to render its "(N)" badge.
    /// </summary>
    private static int CountVisibleMd(string dir, Func<string, bool> isHidden,
        bool showHidden)
    {
        int n = 0;
        foreach (var f in SafeFiles(dir))
            if (showHidden || !isHidden(f)) n++;
        foreach (var sub in SafeDirs(dir))
        {
            if (!showHidden && isHidden(sub)) continue;
            n += CountVisibleMd(sub, isHidden, showHidden);
        }
        return n;
    }

    private static IEnumerable<string> EnumMdSorted(string dir)
    {
        bool IsAgents(string p) => string.Equals(
            Path.GetFileName(p), "AGENTS.md", StringComparison.OrdinalIgnoreCase);

        return SafeFiles(dir)
            .OrderBy(IsAgents)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumDirsSorted(string dir)
        => SafeDirs(dir).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> SafeFiles(string dir)
    {
        try { return Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly).ToList(); }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> SafeDirs(string dir)
    {
        try { return Directory.EnumerateDirectories(dir).ToList(); }
        catch { return Array.Empty<string>(); }
    }
}

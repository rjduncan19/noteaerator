// Decides whether a file-system change should trigger a file-pane refresh.
//
// Note Aerator watches a project folder for .md and sidecar (*-comments.json)
// changes so the list and the open document stay live. Watching is kept
// non-recursive by default because recursively watching a synced folder
// (OneDrive, etc.) produces a flood of events that can overflow the watcher's
// buffer and drop the ones that matter.
//
// The "Show folders" view mode, however, lets the user open files that live in
// sub-directories. For those changes to refresh, sub-directory events must be
// treated as relevant — so relevance depends on whether that mode is active.
//
// This logic lives in Core (no WPF dependency) so it can be unit-tested.

using System;

namespace Noteaerator.Core;

public static class WatchScope
{
    /// <summary>
    /// True if a changed path (an .md file or a *-comments.json sidecar) should
    /// trigger a refresh for a project rooted at <paramref name="folderPath"/>.
    /// </summary>
    /// <param name="folderPath">Absolute project folder path.</param>
    /// <param name="archiveDir">Absolute path of the project's archive subdir.</param>
    /// <param name="fullPath">Absolute path of the changed file.</param>
    /// <param name="showFolders">
    /// When true (the "Show folders" view mode), changes anywhere beneath the
    /// project folder are relevant. When false, only files directly in the
    /// project folder or its archive count.
    /// </param>
    /// <param name="isHidden">
    /// Optional predicate: returns true if a path is user-hidden (or lives under
    /// a hidden folder). Hidden items aren't shown, so changes to them don't
    /// warrant a refresh — unless <paramref name="showHidden"/> is on. This
    /// avoids churn from busy hidden folders like node_modules.
    /// </param>
    /// <param name="showHidden">When true, hidden items are visible (so relevant).</param>
    public static bool IsRelevant(string folderPath, string? archiveDir,
        string fullPath, bool showFolders,
        Func<string, bool>? isHidden = null, bool showHidden = false)
    {
        if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fullPath))
            return false;

        bool structural =
            // Top-level file directly in the project folder.
            IsDirectChild(folderPath, fullPath)
            // File directly in the archive folder (the archive pane is flat).
            || (!string.IsNullOrEmpty(archiveDir) && IsDirectChild(archiveDir!, fullPath))
            // Sub-directory mode: any descendant of the project folder.
            || (showFolders && IsDescendant(folderPath, fullPath));

        if (!structural) return false;

        // Don't refresh for changes to hidden items (e.g. a hidden node_modules
        // folder) while they're not being shown — they aren't on screen anyway.
        if (!showHidden && isHidden != null && isHidden(fullPath)) return false;

        return true;
    }

    /// <summary>True if <paramref name="fullPath"/> sits directly inside
    /// <paramref name="dir"/> (no intervening sub-directory).</summary>
    private static bool IsDirectChild(string dir, string fullPath)
    {
        if (!IsDescendant(dir, fullPath)) return false;
        var rest = fullPath.Substring(TrimEnd(dir).Length + 1);
        return rest.IndexOf('\\') < 0 && rest.IndexOf('/') < 0;
    }

    /// <summary>True if <paramref name="fullPath"/> is anywhere beneath
    /// <paramref name="ancestorDir"/>.</summary>
    private static bool IsDescendant(string ancestorDir, string fullPath)
    {
        var anc = TrimEnd(ancestorDir);
        if (fullPath.Length <= anc.Length) return false;
        if (!fullPath.StartsWith(anc, StringComparison.OrdinalIgnoreCase)) return false;
        return IsSeparator(fullPath[anc.Length]);
    }

    private static bool IsSeparator(char c) => c == '\\' || c == '/';

    private static string TrimEnd(string p) => p.TrimEnd('\\', '/');
}

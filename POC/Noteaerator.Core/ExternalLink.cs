// External-link classification + "Reveal in Explorer" arg construction
// for file:// links. Lives in Core so it's testable without a WPF host.
//
// Background: Note Aerator's viewer shell-launches every external link via
// Process.Start(uri, UseShellExecute = true). For http(s) / mailto / app
// protocols that's exactly what we want — the OS routes them to the right
// handler. But file:// URLs would also be routed to their default file
// association: clicking [Click here](file:///c:/foo/script.exe) would run
// script.exe. Not OK for a viewer that renders Markdown from arbitrary
// sources (synced folders, AI-generated content, etc.).
//
// Option A from POC/file-link-rendering-options.md: route every file:// URL
// through Explorer instead — never through the file's default handler.
// - File path  -> "explorer.exe /select,<path>"  (reveals file in folder)
// - Folder path -> "explorer.exe <path>"          (opens folder)
//
// Worst case is "Explorer shows you a file." Never executes the file.

using System;

namespace Noteaerator.Core;

public enum ExternalLinkKind
{
    /// <summary>Pass to ShellExecute as-is — http(s), mailto, etc.</summary>
    Default,

    /// <summary>file:// URL pointing at an existing folder.</summary>
    FileFolder,

    /// <summary>file:// URL pointing at a file (or path that doesn't exist
    /// — we treat unknown as file so we still get a useful Reveal).</summary>
    FileItem,

    /// <summary>file:// URL we refuse to act on (malformed, UNC, etc).</summary>
    FileRejected
}

public sealed record ExternalLinkPlan(
    ExternalLinkKind Kind,
    string Target,            // for Default: the raw URI; for File*: explorer.exe path
    string? Arguments,        // for File*: the arg string; null for Default
    string? LocalPath);       // for File*: the parsed local path (for status text)

public static class ExternalLink
{
    /// <summary>
    /// Classify a URL into how it should be launched, never throwing.
    /// Caller is responsible for the actual process start.
    /// </summary>
    public static ExternalLinkPlan Classify(string uri, Func<string, bool>? directoryExists = null)
    {
        directoryExists ??= System.IO.Directory.Exists;

        if (string.IsNullOrWhiteSpace(uri))
            return new ExternalLinkPlan(ExternalLinkKind.FileRejected, "", null, null);

        if (!IsFileScheme(uri))
            return new ExternalLinkPlan(ExternalLinkKind.Default, uri, null, null);

        // file:// — parse to a local path. We deliberately use Uri here so
        // percent-encoding (spaces, etc) gets decoded for the explorer arg.
        Uri parsed;
        try
        {
            parsed = new Uri(uri);
        }
        catch
        {
            return new ExternalLinkPlan(ExternalLinkKind.FileRejected, uri, null, null);
        }

        // UNC paths (\\server\share\...) come back with a non-empty Host.
        // We could support these later, but for now reject — server shares
        // are often a different trust boundary.
        if (!string.IsNullOrEmpty(parsed.Host))
            return new ExternalLinkPlan(ExternalLinkKind.FileRejected, uri, null, null);

        string localPath;
        try
        {
            localPath = parsed.LocalPath;
        }
        catch
        {
            return new ExternalLinkPlan(ExternalLinkKind.FileRejected, uri, null, null);
        }

        if (string.IsNullOrWhiteSpace(localPath))
            return new ExternalLinkPlan(ExternalLinkKind.FileRejected, uri, null, null);

        // Explorer requires backslashes for /select to highlight correctly.
        var normalized = localPath.Replace('/', '\\');

        if (directoryExists(normalized))
        {
            return new ExternalLinkPlan(
                ExternalLinkKind.FileFolder,
                "explorer.exe",
                Quote(normalized),
                normalized);
        }

        // File (or non-existent path — still safe to ask Explorer to /select;
        // it falls back to opening the parent folder if the leaf is missing).
        return new ExternalLinkPlan(
            ExternalLinkKind.FileItem,
            "explorer.exe",
            "/select," + Quote(normalized),
            normalized);
    }

    private static bool IsFileScheme(string uri)
    {
        // Case-insensitive "file:" check; tolerates leading whitespace.
        var trimmed = uri.TrimStart();
        if (trimmed.Length < 5) return false;
        return string.Equals(trimmed.Substring(0, 5), "file:",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string path)
    {
        // Always quote — paths may contain spaces (e.g. "Note Aerator" folder).
        // Internal quotes shouldn't appear on a Windows path, but escape them
        // defensively for completeness.
        return "\"" + path.Replace("\"", "\\\"") + "\"";
    }
}

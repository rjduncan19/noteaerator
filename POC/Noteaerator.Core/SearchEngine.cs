namespace Noteaerator.Core;

public sealed class SearchHit
{
    public string FilePath { get; init; } = "";
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public bool IsComment { get; init; }
    public int LineNumber { get; init; }    // 1-based; 0 if not applicable
    public string? CommentId { get; init; } // for comment hits
    public string Snippet { get; init; } = "";
    public string Term { get; init; } = "";

    public string Display1 => IsComment
        ? $"💬  {FileName}  ·  comment"
        : $"{FileName}  ·  line {LineNumber}";
    public string Display2 => Snippet;
}

public static class SearchEngine
{
    public const int MaxHits = 200;

    /// <summary>
    /// Either provide <paramref name="folderPath"/> for project-wide scan, or
    /// <paramref name="singleFile"/> to limit to one file. Exactly one should
    /// be non-null.
    /// </summary>
    public static List<SearchHit> Search(string query, string? folderPath, string? singleFile)
    {
        var results = new List<SearchHit>();
        if (string.IsNullOrWhiteSpace(query)) return results;
        var cmp = StringComparison.OrdinalIgnoreCase;

        IEnumerable<string> files;
        if (singleFile != null)
            files = new[] { singleFile };
        else if (folderPath != null)
            files = EnumerateProjectMd(folderPath);
        else
            return results;

        foreach (var file in files)
        {
            if (results.Count >= MaxHits) break;
            ScanFile(file, query, cmp, results);
            if (results.Count >= MaxHits) break;
            ScanSidecar(file, query, cmp, results);
        }
        return results;
    }

    private static IEnumerable<string> EnumerateProjectMd(string folderPath)
    {
        IEnumerable<string> SafeEnum(string dir)
        {
            try   { return Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly); }
            catch { return Array.Empty<string>(); }
        }
        foreach (var f in SafeEnum(folderPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            yield return f;
        var arch = Path.Combine(folderPath, "archive");
        if (Directory.Exists(arch))
            foreach (var f in SafeEnum(arch).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                yield return f;
    }

    private static void ScanFile(string path, string query, StringComparison cmp, List<SearchHit> results)
    {
        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch { return; }
        for (int i = 0; i < lines.Length; i++)
        {
            if (results.Count >= MaxHits) return;
            if (lines[i].IndexOf(query, cmp) >= 0)
            {
                results.Add(new SearchHit
                {
                    FilePath = path,
                    LineNumber = i + 1,
                    Snippet = MakeSnippet(lines[i], query, cmp),
                    Term = query
                });
            }
        }
    }

    private static void ScanSidecar(string mdPath, string query, StringComparison cmp, List<SearchHit> results)
    {
        var data = CommentStore.Load(mdPath);
        foreach (var c in data.Comments)
        {
            if (results.Count >= MaxHits) return;
            if (!string.IsNullOrEmpty(c.Body) && c.Body.IndexOf(query, cmp) >= 0)
            {
                results.Add(new SearchHit
                {
                    FilePath = mdPath,
                    IsComment = true,
                    CommentId = c.Id,
                    Snippet = MakeSnippet(c.Body, query, cmp),
                    Term = query
                });
            }
        }
    }

    internal static string MakeSnippet(string line, string query, StringComparison cmp)
    {
        const int radius = 60;
        var idx = line.IndexOf(query, cmp);
        if (idx < 0) return line.Length > 120 ? line.Substring(0, 120) + "…" : line;
        var start = Math.Max(0, idx - radius);
        var end = Math.Min(line.Length, idx + query.Length + radius);
        var prefix = start > 0 ? "…" : "";
        var suffix = end < line.Length ? "…" : "";
        return prefix + line.Substring(start, end - start).Replace("\t", "  ") + suffix;
    }
}

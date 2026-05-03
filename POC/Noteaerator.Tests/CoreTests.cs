using Noteaerator.Core;

namespace Noteaerator.Tests;

public sealed class TempProject : IDisposable
{
    public string Path { get; }
    public TempProject()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "noteaerator-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }
    public string Md(string name, string content)
    {
        var p = System.IO.Path.Combine(Path, name);
        File.WriteAllText(p, content);
        return p;
    }
    public string Archived(string name, string content)
    {
        var dir = System.IO.Path.Combine(Path, "archive");
        Directory.CreateDirectory(dir);
        var p = System.IO.Path.Combine(dir, name);
        File.WriteAllText(p, content);
        return p;
    }
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}

public class TimeFormatTests
{
    private static readonly DateTime Now = new(2026, 5, 3, 12, 0, 0);

    [Fact] public void JustNow_under_60s()    => Assert.Equal("just now",       TimeFormat.Relative(Now.AddSeconds(-10), Now));
    [Fact] public void OneMinute()            => Assert.Equal("1 minute ago",   TimeFormat.Relative(Now.AddMinutes(-1),  Now));
    [Fact] public void Minutes_plural()       => Assert.Equal("5 minutes ago",  TimeFormat.Relative(Now.AddMinutes(-5),  Now));
    [Fact] public void OneHour()              => Assert.Equal("1 hour ago",     TimeFormat.Relative(Now.AddHours(-1),    Now));
    [Fact] public void Hours_plural()         => Assert.Equal("3 hours ago",    TimeFormat.Relative(Now.AddHours(-3),    Now));
    [Fact] public void Yesterday()            => Assert.Equal("yesterday",      TimeFormat.Relative(Now.AddHours(-30),   Now));
    [Fact] public void Days_plural()          => Assert.Equal("5 days ago",     TimeFormat.Relative(Now.AddDays(-5),     Now));
    [Fact] public void Months()               => Assert.Equal("2 months ago",   TimeFormat.Relative(Now.AddDays(-65),    Now));
    [Fact] public void Years()                => Assert.Equal("2 years ago",    TimeFormat.Relative(Now.AddDays(-800),   Now));
}

public class CommentStoreTests
{
    [Fact]
    public void SidecarPath_appends_dash_comments_json()
    {
        using var p = new TempProject();
        var md = p.Md("welcome.md", "# hi");
        var sidecar = CommentStore.SidecarPath(md);
        Assert.EndsWith(Path.DirectorySeparatorChar + "welcome-comments.json", sidecar);
    }

    [Fact]
    public void Load_missing_returns_empty_default_file()
    {
        using var p = new TempProject();
        var md = p.Md("a.md", "x");
        var data = CommentStore.Load(md);
        Assert.NotNull(data);
        Assert.Equal(1, data.Version);
        Assert.Empty(data.Comments);
        Assert.Contains("DELETE this file when done", data.Purpose);
    }

    [Fact]
    public void AddComment_then_Load_roundtrips()
    {
        using var p = new TempProject();
        var md = p.Md("a.md", "# hi");
        var entry = new CommentEntry
        {
            Body = "please tighten this",
            Anchor = new CommentAnchor { HeadingSlug = "hi", BlockIndex = 0, SubPath = "tr:2", TextQuote = "hi" }
        };
        CommentStore.AddComment(md, entry);

        var data = CommentStore.Load(md);
        Assert.Single(data.Comments);
        Assert.Equal("please tighten this", data.Comments[0].Body);
        Assert.Equal("tr:2", data.Comments[0].Anchor.SubPath);
        Assert.True(File.Exists(CommentStore.SidecarPath(md)));
    }

    [Fact]
    public void DeletingLastComment_removes_sidecar_file()
    {
        using var p = new TempProject();
        var md = p.Md("a.md", "# hi");
        var entry = new CommentEntry { Body = "x" };
        CommentStore.AddComment(md, entry);
        Assert.True(File.Exists(CommentStore.SidecarPath(md)));

        CommentStore.DeleteComment(md, entry.Id);
        Assert.False(File.Exists(CommentStore.SidecarPath(md)));
    }

    [Fact]
    public void Save_with_two_comments_writes_atomically_and_no_tmp_left()
    {
        using var p = new TempProject();
        var md = p.Md("a.md", "# hi");
        CommentStore.AddComment(md, new CommentEntry { Body = "one" });
        CommentStore.AddComment(md, new CommentEntry { Body = "two" });

        var sidecar = CommentStore.SidecarPath(md);
        Assert.True(File.Exists(sidecar));
        Assert.False(File.Exists(sidecar + ".tmp"));
        var data = CommentStore.Load(md);
        Assert.Equal(2, data.Comments.Count);
    }
}

public class SearchEngineTests
{
    [Fact]
    public void Empty_query_returns_no_hits()
    {
        using var p = new TempProject();
        p.Md("a.md", "anything");
        Assert.Empty(SearchEngine.Search("", p.Path, null));
        Assert.Empty(SearchEngine.Search("   ", p.Path, null));
    }

    [Fact]
    public void Project_scope_finds_matches_across_files_and_archive()
    {
        using var p = new TempProject();
        p.Md("a.md", "line one\nfoo bar baz\nthird");
        p.Md("b.md", "no match here");
        p.Archived("old.md", "archived FOO content");

        var hits = SearchEngine.Search("foo", p.Path, null);

        // Two MD content hits (a.md line 2, archive/old.md line 1). No comments here.
        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, h => h.FileName == "a.md"   && h.LineNumber == 2 && !h.IsComment);
        Assert.Contains(hits, h => h.FileName == "old.md" && h.LineNumber == 1 && !h.IsComment);
    }

    [Fact]
    public void File_scope_only_scans_one_file_and_its_sidecar()
    {
        using var p = new TempProject();
        var a = p.Md("a.md", "alpha keyword line\nsecond");
        p.Md("b.md", "beta keyword line"); // would match in project scope

        var hits = SearchEngine.Search("keyword", null, a);
        Assert.Single(hits);
        Assert.Equal("a.md", hits[0].FileName);
        Assert.Equal(1, hits[0].LineNumber);
    }

    [Fact]
    public void Search_is_case_insensitive()
    {
        using var p = new TempProject();
        p.Md("a.md", "MixedCase Word here");
        var hits = SearchEngine.Search("mixedcase", p.Path, null);
        Assert.Single(hits);
    }

    [Fact]
    public void Sidecar_comment_bodies_are_searched()
    {
        using var p = new TempProject();
        var a = p.Md("a.md", "no match in body");
        CommentStore.AddComment(a, new CommentEntry { Body = "this comment has the keyword in it" });

        var hits = SearchEngine.Search("keyword", p.Path, null);
        Assert.Single(hits);
        Assert.True(hits[0].IsComment);
        Assert.Equal("a.md", hits[0].FileName);
        Assert.False(string.IsNullOrEmpty(hits[0].CommentId));
    }

    [Fact]
    public void Snippet_truncates_around_match_with_ellipses()
    {
        // Long line, match in the middle. Snippet should have leading + trailing ellipsis.
        var line = new string('a', 200) + " NEEDLE " + new string('b', 200);
        var snip = typeof(SearchEngine).GetMethod("MakeSnippet",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { line, "NEEDLE", StringComparison.OrdinalIgnoreCase }) as string;
        Assert.NotNull(snip);
        Assert.StartsWith("…", snip);
        Assert.EndsWith("…", snip);
        Assert.Contains("NEEDLE", snip);
    }

    [Fact]
    public void Hit_count_is_capped_at_MaxHits()
    {
        using var p = new TempProject();
        // Generate one file with way more than MaxHits matching lines.
        var lines = string.Join("\n", Enumerable.Range(0, SearchEngine.MaxHits + 50)
            .Select(i => $"line {i} match"));
        p.Md("big.md", lines);

        var hits = SearchEngine.Search("match", p.Path, null);
        Assert.Equal(SearchEngine.MaxHits, hits.Count);
    }

    [Fact]
    public void Display_strings_format_for_content_and_comment_hits()
    {
        var contentHit = new SearchHit { FilePath = "/x/welcome.md", LineNumber = 5, Snippet = "hi" };
        Assert.Equal("welcome.md  ·  line 5", contentHit.Display1);

        var commentHit = new SearchHit { FilePath = "/x/welcome.md", IsComment = true, Snippet = "hi" };
        Assert.Contains("welcome.md", commentHit.Display1);
        Assert.Contains("comment",    commentHit.Display1);
    }
}

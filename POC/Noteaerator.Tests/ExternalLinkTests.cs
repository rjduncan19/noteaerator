using Noteaerator.Core;

namespace Noteaerator.Tests;

public sealed class ExternalLinkTests
{
    private static ExternalLinkPlan Classify(string uri, Func<string, bool>? dirExists = null)
        => ExternalLink.Classify(uri, dirExists);

    // ---- Default (non-file) URLs pass through untouched ----

    [Theory]
    [InlineData("https://example.com/foo")]
    [InlineData("http://example.com")]
    [InlineData("mailto:user@example.com")]
    [InlineData("ms-windows-store://pdp/?productid=9N5DTC0FZP7M")]
    [InlineData("vscode://file/c:/foo.md")]
    public void Non_file_schemes_route_to_default_launcher(string uri)
    {
        var plan = Classify(uri);
        Assert.Equal(ExternalLinkKind.Default, plan.Kind);
        Assert.Equal(uri, plan.Target);
        Assert.Null(plan.Arguments);
    }

    [Fact]
    public void Empty_uri_is_rejected()
    {
        var plan = Classify("");
        Assert.Equal(ExternalLinkKind.FileRejected, plan.Kind);
    }

    // ---- file:// URLs go through Explorer ----

    [Fact]
    public void File_url_to_existing_folder_uses_explorer_with_path_arg()
    {
        var folder = @"C:\Users\me\Documents\Notes";
        var plan = Classify(
            "file:///C:/Users/me/Documents/Notes",
            dirExists: p => p == folder);

        Assert.Equal(ExternalLinkKind.FileFolder, plan.Kind);
        Assert.Equal("explorer.exe", plan.Target);
        Assert.Equal($"\"{folder}\"", plan.Arguments);
        Assert.Equal(folder, plan.LocalPath);
    }

    [Fact]
    public void File_url_to_file_uses_explorer_select_arg()
    {
        var plan = Classify(
            "file:///C:/Users/me/notes.md",
            dirExists: _ => false);

        Assert.Equal(ExternalLinkKind.FileItem, plan.Kind);
        Assert.Equal("explorer.exe", plan.Target);
        Assert.Equal(@"/select,""C:\Users\me\notes.md""", plan.Arguments);
    }

    [Fact]
    public void Forward_slashes_in_file_url_become_backslashes_for_explorer()
    {
        var plan = Classify(
            "file:///C:/Users/richardd/source-rjduncan19/noteaerator/packaging/store/dist",
            dirExists: _ => true);
        Assert.Contains(@"C:\Users\richardd\source-rjduncan19\noteaerator\packaging\store\dist",
            plan.Arguments);
        Assert.DoesNotContain("/", plan.Arguments);
    }

    [Fact]
    public void Spaces_in_path_are_percent_decoded_and_quoted()
    {
        var plan = Classify(
            "file:///C:/Users/me/Documents/Note%20Aerator/01-Welcome.md",
            dirExists: _ => false);
        Assert.Equal(ExternalLinkKind.FileItem, plan.Kind);
        // Should contain "Note Aerator" decoded and the whole path quoted.
        Assert.Contains(@"""C:\Users\me\Documents\Note Aerator\01-Welcome.md""",
            plan.Arguments);
    }

    [Fact]
    public void File_scheme_check_is_case_insensitive()
    {
        var plan = Classify("FILE:///C:/foo.txt", dirExists: _ => false);
        Assert.Equal(ExternalLinkKind.FileItem, plan.Kind);
    }

    [Fact]
    public void Unc_file_urls_are_rejected()
    {
        // \\server\share comes back as file://server/share with Host="server".
        var plan = Classify("file://server/share/note.md", dirExists: _ => false);
        Assert.Equal(ExternalLinkKind.FileRejected, plan.Kind);
    }

    [Fact]
    public void Malformed_file_url_is_rejected_not_thrown()
    {
        var plan = Classify("file:", dirExists: _ => false);
        Assert.Equal(ExternalLinkKind.FileRejected, plan.Kind);
    }

    // ---- The key safety property ----

    [Theory]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("file:///C:/foo/script.bat")]
    [InlineData("file:///C:/foo/macro.docm")]
    [InlineData("file:///C:/foo/installer.msi")]
    public void Dangerous_file_extensions_still_route_through_explorer_not_shell(string uri)
    {
        // The whole point of Option A: file:// of any extension goes through
        // explorer.exe /select, never through ShellExecute on the file itself.
        // No matter how scary the extension, the worst case is "Explorer
        // highlights the file in its folder."
        var plan = Classify(uri, dirExists: _ => false);
        Assert.Equal(ExternalLinkKind.FileItem, plan.Kind);
        Assert.Equal("explorer.exe", plan.Target);
        Assert.StartsWith("/select,", plan.Arguments);
    }
}

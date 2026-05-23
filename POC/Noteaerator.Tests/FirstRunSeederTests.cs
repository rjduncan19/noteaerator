using Noteaerator.Core;

namespace Noteaerator.Tests;

public sealed class FirstRunSeederTests
{
    private static string MakeTempDir(string label)
    {
        var p = Path.Combine(Path.GetTempPath(),
            $"nra-firstrun-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(p);
        return p;
    }

    [Fact]
    public void Seed_copies_markdown_files_into_dest()
    {
        var src = MakeTempDir("src");
        var dst = MakeTempDir("dst");
        File.WriteAllText(Path.Combine(src, "01-Welcome.md"), "# Welcome");
        File.WriteAllText(Path.Combine(src, "02-Tips.md"), "# Tips");
        try
        {
            var result = FirstRunSeeder.Seed(src, dst);
            Assert.Equal(dst, result);
            Assert.True(File.Exists(Path.Combine(dst, "01-Welcome.md")));
            Assert.True(File.Exists(Path.Combine(dst, "02-Tips.md")));
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(dst, true);
        }
    }

    [Fact]
    public void Seed_does_not_overwrite_user_modified_files()
    {
        var src = MakeTempDir("src");
        var dst = MakeTempDir("dst");
        File.WriteAllText(Path.Combine(src, "01-Welcome.md"), "# Bundled");
        File.WriteAllText(Path.Combine(dst, "01-Welcome.md"), "user's notes!");
        try
        {
            FirstRunSeeder.Seed(src, dst);
            Assert.Equal("user's notes!", File.ReadAllText(Path.Combine(dst, "01-Welcome.md")));
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(dst, true);
        }
    }

    [Fact]
    public void Seed_creates_dest_dir_if_missing()
    {
        var src = MakeTempDir("src");
        var parent = MakeTempDir("parent");
        var dst = Path.Combine(parent, "Note Aerator");
        File.WriteAllText(Path.Combine(src, "01-Welcome.md"), "hi");
        try
        {
            Assert.False(Directory.Exists(dst));
            var result = FirstRunSeeder.Seed(src, dst);
            Assert.Equal(dst, result);
            Assert.True(Directory.Exists(dst));
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void Seed_returns_null_when_source_missing()
    {
        var dst = MakeTempDir("dst");
        var bogus = Path.Combine(Path.GetTempPath(), "nra-doesnotexist-" + Guid.NewGuid());
        try
        {
            var result = FirstRunSeeder.Seed(bogus, dst);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(dst, true);
        }
    }

    [Fact]
    public void Seed_is_idempotent_across_repeated_calls()
    {
        var src = MakeTempDir("src");
        var dst = MakeTempDir("dst");
        File.WriteAllText(Path.Combine(src, "01-Welcome.md"), "# Welcome");
        try
        {
            FirstRunSeeder.Seed(src, dst);
            // Mutate the seeded file as if the user had edited it.
            File.WriteAllText(Path.Combine(dst, "01-Welcome.md"), "edited");
            FirstRunSeeder.Seed(src, dst);
            Assert.Equal("edited", File.ReadAllText(Path.Combine(dst, "01-Welcome.md")));
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(dst, true);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Noteaerator.Core;

/// <summary>
/// Drops a getting-started project folder onto the user's machine the very
/// first time they launch Note Aerator (detected by the absence of
/// <c>projects.json</c>). Idempotent: if the destination already contains a
/// file of the same name we leave the user's copy alone.
/// </summary>
public static class FirstRunSeeder
{
    public const string DefaultProjectFolderName = "Note Aerator";

    /// <summary>
    /// Source dir holding the bundled getting-started .md files. Lives next
    /// to the app's <c>viewer.html</c> as a <c>Content</c> item; both dev
    /// runs and the MSIX install resolve through <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    private static string DefaultSourceDir =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "FirstRun");

    private static string DefaultDestDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            DefaultProjectFolderName);

    /// <summary>
    /// Seed the default getting-started project. Returns the destination
    /// folder path on success, or null if nothing could be seeded (e.g. the
    /// bundled source dir is missing from this install).
    /// </summary>
    public static string? TrySeed()
        => Seed(DefaultSourceDir, DefaultDestDir);

    /// <summary>
    /// Testable core: copy every *.md from <paramref name="sourceDir"/> into
    /// <paramref name="destDir"/>, skipping any file the user already has.
    /// Returns the destination path on success, null if the source dir does
    /// not exist or copying failed irrecoverably.
    /// </summary>
    public static string? Seed(string sourceDir, string destDir)
    {
        try
        {
            if (!Directory.Exists(sourceDir)) return null;
            Directory.CreateDirectory(destDir);
            foreach (var srcFile in Directory.EnumerateFiles(sourceDir, "*.md"))
            {
                var name = Path.GetFileName(srcFile);
                var dstFile = Path.Combine(destDir, name);
                if (!File.Exists(dstFile))
                    File.Copy(srcFile, dstFile);
            }
            return destDir;
        }
        catch
        {
            return null;
        }
    }
}

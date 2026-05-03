using System.Text.Json;

namespace Noteaerator.Core;

public static class CommentStore
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string SidecarPath(string mdPath)
    {
        var dir = Path.GetDirectoryName(mdPath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(mdPath);
        return Path.Combine(dir, baseName + "-comments.json");
    }

    public static CommentFile Load(string mdPath)
    {
        var path = SidecarPath(mdPath);
        if (!File.Exists(path)) return new CommentFile();
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<CommentFile>(fs) ?? new CommentFile();
        }
        catch
        {
            return new CommentFile();
        }
    }

    public static void Save(string mdPath, CommentFile data)
    {
        var path = SidecarPath(mdPath);
        if (data.Comments.Count == 0)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            return;
        }
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Opts));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else                   File.Move(tmp, path);
    }

    public static void AddComment(string mdPath, CommentEntry entry)
    {
        var data = Load(mdPath);
        data.Comments.Add(entry);
        Save(mdPath, data);
    }

    public static void DeleteComment(string mdPath, string id)
    {
        var data = Load(mdPath);
        data.Comments.RemoveAll(c => c.Id == id);
        Save(mdPath, data);
    }
}

using System.Text.Json.Serialization;

namespace Noteaerator.Core;

public sealed class CommentAnchor
{
    [JsonPropertyName("headingSlug")] public string? HeadingSlug { get; set; }
    [JsonPropertyName("blockIndex")]  public int BlockIndex { get; set; }
    [JsonPropertyName("subPath")]     public string? SubPath { get; set; }
    [JsonPropertyName("textQuote")]   public string? TextQuote { get; set; }
}

public sealed class CommentEntry
{
    [JsonPropertyName("id")]        public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("createdAt")] public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    [JsonPropertyName("anchor")]    public CommentAnchor Anchor { get; set; } = new();
    [JsonPropertyName("body")]      public string Body { get; set; } = "";
}

public sealed class CommentFile
{
    [JsonPropertyName("_purpose")]
    public string Purpose { get; set; } =
        "Human comments on the sibling .md file, written by the Note Aerator viewer. " +
        "Agents are expected to read these, act on them, and DELETE this file when done. " +
        "Removing all entries also auto-deletes this file.";

    [JsonPropertyName("version")]  public int Version { get; set; } = 1;
    [JsonPropertyName("comments")] public List<CommentEntry> Comments { get; set; } = new();
}

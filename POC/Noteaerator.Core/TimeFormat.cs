namespace Noteaerator.Core;

public static class TimeFormat
{
    /// <summary>
    /// Render a DateTime as a friendly relative-time string ("just now",
    /// "5 minutes ago", "yesterday", "3 days ago", etc.). Compares against
    /// <paramref name="now"/> so it's deterministic in tests.
    /// </summary>
    public static string Relative(DateTime when, DateTime? now = null)
    {
        var diff = (now ?? DateTime.Now) - when;
        if (diff.TotalSeconds < 60)  return "just now";
        if (diff.TotalMinutes < 60)  return $"{(int)diff.TotalMinutes} minute{((int)diff.TotalMinutes == 1 ? "" : "s")} ago";
        if (diff.TotalHours   < 24)  return $"{(int)diff.TotalHours} hour{((int)diff.TotalHours == 1 ? "" : "s")} ago";
        if (diff.TotalDays    < 2)   return "yesterday";
        if (diff.TotalDays    < 30)  return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays    < 365) return $"{(int)(diff.TotalDays / 30)} months ago";
        return $"{(int)(diff.TotalDays / 365)} years ago";
    }
}

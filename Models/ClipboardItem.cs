namespace clipboard.Models;

public class ClipboardItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastUsedAt { get; set; } = DateTime.Now;
    public bool IsPinned { get; set; }
    public string? GroupId { get; set; }
    public string ContentType { get; set; } = "Text"; // Text, Image, etc.
}


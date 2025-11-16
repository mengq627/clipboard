using System.Text.Json.Serialization;

namespace clipboard.Models;

public class ClipboardGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    [JsonIgnore]
    public int ItemCount => Items?.Count ?? 0;
    
    [JsonIgnore]
    public List<ClipboardItem> Items { get; set; } = new();
}


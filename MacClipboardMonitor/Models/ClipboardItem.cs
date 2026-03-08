namespace MacClipboardMonitor.Models;

using System;

public class ClipboardItem
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
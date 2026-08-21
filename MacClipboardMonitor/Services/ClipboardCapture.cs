using System;
using System.Collections.Generic;
using MacClipboardMonitor.Models;

namespace MacClipboardMonitor.Services;

public sealed class ClipboardCapture
{
    public ClipboardItemType Type { get; init; } = ClipboardItemType.Texto;
    public string? Text { get; init; }
    public byte[]? ImageBytes { get; init; }
    public string? ImageHash { get; init; }
    public IReadOnlyList<string>? FilePaths { get; init; }

    public string Fingerprint => Type switch
    {
        ClipboardItemType.Texto => "T:" + Text,
        ClipboardItemType.Imagen => "I:" + (ImageHash ?? string.Empty),
        ClipboardItemType.Archivo => "F:" + string.Join("\n", FilePaths ?? Array.Empty<string>()),
        _ => "?"
    };
}

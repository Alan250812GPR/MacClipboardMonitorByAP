using System;
using System.Collections.Generic;
using System.Linq;
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
        ClipboardItemType.Archivo => "F:" + JoinFilePaths(FilePaths),
        _ => "?"
    };

    // Normaliza rutas: recorta, elimina vacíos/duplicados y ordena para que el
    // mismo conjunto de archivos no genere otra entrada si cambia el orden.
    public static string JoinFilePaths(IEnumerable<string>? paths)
    {
        if (paths is null) return string.Empty;

        return string.Join("\n", paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal));
    }
}

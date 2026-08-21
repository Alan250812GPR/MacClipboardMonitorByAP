using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;

namespace MacClipboardMonitor.Models;

public enum ClipboardItemType
{
    Texto = 0,
    Imagen = 1,
    Archivo = 2
}

public class ClipboardItem
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;

    // Guardamos los bytes en SQLite
    public byte[]? ImageBytes { get; set; }
    public DateTime CreatedAt { get; set; }

    // Tipo de contenido (texto, imagen o archivo). Por defecto texto.
    public ClipboardItemType Type { get; set; } = ClipboardItemType.Texto;

    // Rutas de archivos copiados, serializadas una por línea.
    public string? FilePaths { get; set; }

    // Hash SHA256 de la imagen para deduplicar.
    public string? ImageHash { get; set; }

    // Propiedades exclusivas para la interfaz gráfica (No se guardan en DB)
    [NotMapped]
    public bool IsImage => Type == ClipboardItemType.Imagen;

    [NotMapped]
    public bool IsFile => Type == ClipboardItemType.Archivo;

    [NotMapped]
    public bool IsText => Type == ClipboardItemType.Texto;

    [NotMapped]
    public IReadOnlyList<string> FileList =>
        string.IsNullOrEmpty(FilePaths)
            ? Array.Empty<string>()
            : FilePaths.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [NotMapped]
    public string FileDisplay =>
        string.Join(", ", FileList.Select(p =>
        {
            try { return Path.GetFileName(p); }
            catch { return p; }
        }));

    [NotMapped]
    public Bitmap? AvaloniaImage 
    {
        get 
        {
            if (!IsImage) return null;
            try 
            {
                using var stream = new MemoryStream(ImageBytes!);
                return new Bitmap(stream);
            }
            catch { return null; }
        }
    }
}

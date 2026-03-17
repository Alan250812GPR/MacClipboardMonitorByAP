using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using Avalonia.Media.Imaging;

namespace MacClipboardMonitor.Models;

public class ClipboardItem
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    
    // Guardamos los bytes en SQLite
    public byte[]? ImageBytes { get; set; } 
    public DateTime CreatedAt { get; set; }

    // Propiedades exclusivas para la interfaz gráfica (No se guardan en DB)
    [NotMapped]
    public bool IsImage => ImageBytes != null && ImageBytes.Length > 0;

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
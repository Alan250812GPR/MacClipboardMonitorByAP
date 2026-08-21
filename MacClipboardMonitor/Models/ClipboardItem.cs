using System;
using System.Collections.Generic;
using System.ComponentModel;
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

public class ClipboardItem : INotifyPropertyChanged
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

    // Entrada encriptada (texto): el contenido vive solo en CipherText.
    private bool _isEncrypted;
    public bool IsEncrypted
    {
        get => _isEncrypted;
        set
        {
            if (_isEncrypted == value) return;
            _isEncrypted = value;

            // El contenido cambió: invalidar caché de detección y refrescar la UI.
            _codeLanguage = null;
            _codeLanguageResolved = false;

            OnPropertyChanged(nameof(IsEncrypted));
            OnPropertyChanged(nameof(IsSecret));
            OnPropertyChanged(nameof(IsPlainText));
            OnPropertyChanged(nameof(HasCodeBadge));
            OnPropertyChanged(nameof(CanEncrypt));
        }
    }

    // Payload cifrado en Base64.
    public string? CipherText { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Propiedades exclusivas para la interfaz gráfica (No se guardan en DB)
    [NotMapped]
    public bool IsImage => Type == ClipboardItemType.Imagen;

    [NotMapped]
    public bool IsFile => Type == ClipboardItemType.Archivo;

    [NotMapped]
    public bool IsText => Type == ClipboardItemType.Texto;

    // Lenguaje detectado en textos con aspecto de código (solo UI, se calcula una vez).
    private string? _codeLanguage;
    private bool _codeLanguageResolved;

    [NotMapped]
    public string? CodeLanguage
    {
        get
        {
            if (Type != ClipboardItemType.Texto) return null;
            if (!_codeLanguageResolved)
            {
                _codeLanguage = Services.CodeDetectionService.DetectLanguage(Content);
                _codeLanguageResolved = true;
            }
            return _codeLanguage;
        }
    }

    [NotMapped]
    public bool HasCodeBadge => !string.IsNullOrEmpty(CodeLanguage);

    // Entrada encriptada: la UI muestra solo una máscara.
    [NotMapped]
    public bool IsSecret => IsEncrypted;

    // Texto plano sin detección de código y sin encriptar.
    [NotMapped]
    public bool IsPlainText => IsText && !HasCodeBadge && !IsEncrypted;

    // Se puede encriptar solo el texto que aún no está encriptado.
    [NotMapped]
    public bool CanEncrypt => IsText && !IsEncrypted;

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

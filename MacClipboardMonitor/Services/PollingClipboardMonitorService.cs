using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using MacClipboardMonitor.Models;

namespace MacClipboardMonitor.Services;

public class PollingClipboardMonitorService : IClipboardMonitorService
{
    private static readonly string[] ImageFormats = { "public.png", "public.tiff", "public.jpeg", "public.jpg", "public.image" };

    private readonly IClipboard _clipboard;

    public IObservable<ClipboardCapture> ClipboardChanged { get; }

    public PollingClipboardMonitorService(IClipboard clipboard)
    {
        _clipboard = clipboard;

        ClipboardChanged = Observable.Interval(TimeSpan.FromMilliseconds(750))
            .SelectMany(async _ => await CaptureAsync())
            .Where(capture => capture != null)
            .Select(capture => capture!)
            .DistinctUntilChanged(capture => capture.Fingerprint)
            .Publish()
            .RefCount();
    }

    private async Task<ClipboardCapture?> CaptureAsync()
    {
        try
        {
            var formats = await _clipboard.GetFormatsAsync();
            var formatSet = new HashSet<string>(formats, StringComparer.OrdinalIgnoreCase);

            // 1. Archivos copiados
            if (formatSet.Contains(DataFormats.FileNames) ||
                formatSet.Contains(DataFormats.Files) ||
                formatSet.Contains("NSFilenamesPboardType"))
            {
                var paths = await TryGetFilePathsAsync();
                if (paths is { Count: > 0 })
                {
                    return new ClipboardCapture
                    {
                        Type = ClipboardItemType.Archivo,
                        FilePaths = paths
                    };
                }
            }

            // 2. Imágenes copiadas
            if (formatSet.Overlaps(ImageFormats))
            {
                var bytes = await TryGetImageBytesAsync(formatSet);
                if (bytes is { Length: > 0 })
                {
                    var hash = Convert.ToHexString(SHA256.HashData(bytes));
                    return new ClipboardCapture
                    {
                        Type = ClipboardItemType.Imagen,
                        ImageBytes = bytes,
                        ImageHash = hash
                    };
                }
            }

            // 3. Texto
            var text = await _clipboard.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return new ClipboardCapture
                {
                    Type = ClipboardItemType.Texto,
                    Text = text
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<string>?> TryGetFilePathsAsync()
    {
        var data = await _clipboard.GetDataAsync(DataFormats.FileNames);
        if (data is IEnumerable<string> paths)
        {
            var list = paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return list.Count > 0 ? list : null;
        }

        return null;
    }

    private async Task<byte[]?> TryGetImageBytesAsync(HashSet<string> formatSet)
    {
        foreach (var format in ImageFormats)
        {
            if (!formatSet.Contains(format)) continue;

            var data = await _clipboard.GetDataAsync(format);
            if (data is byte[] bytes && bytes.Length > 0)
            {
                return bytes;
            }
        }

        return null;
    }

    public async void SetText(string text)
    {
        await _clipboard.SetTextAsync(text);
    }

    public async void SetImage(byte[] imageBytes)
    {
        var dataObject = new DataObject();
        dataObject.Set("public.png", imageBytes);
        await _clipboard.SetDataObjectAsync(dataObject);
    }

    public async void SetFiles(IReadOnlyList<string> filePaths)
    {
        var dataObject = new DataObject();
        dataObject.Set(DataFormats.FileNames, filePaths.ToList());
        await _clipboard.SetDataObjectAsync(dataObject);
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MacClipboardMonitor.Services;

public interface IClipboardMonitorService
{
    IObservable<ClipboardCapture> ClipboardChanged { get; }
    Task SetTextAsync(string text);
    Task SetImageAsync(byte[] imageBytes);
    Task SetFilesAsync(IReadOnlyList<string> filePaths);
}

using System;
using System.Collections.Generic;
using MacClipboardMonitor.Models;

namespace MacClipboardMonitor.Services;

public interface IClipboardMonitorService
{
    IObservable<ClipboardCapture> ClipboardChanged { get; }
    void SetText(string text);
    void SetImage(byte[] imageBytes);
    void SetFiles(IReadOnlyList<string> filePaths);
}

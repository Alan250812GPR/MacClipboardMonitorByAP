using System;

namespace MacClipboardMonitor.Services;

public interface IClipboardMonitorService
{
    IObservable<string> ClipboardTextChanged { get; }
    void SetText(string text);
    
    
}
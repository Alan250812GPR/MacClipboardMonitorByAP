using System;
using System.Reactive.Linq;
using Avalonia.Input.Platform;

namespace MacClipboardMonitor.Services;

public class PollingClipboardMonitorService : IClipboardMonitorService
{
    private readonly IClipboard _clipboard;

    public IObservable<string> ClipboardTextChanged { get; }

    public PollingClipboardMonitorService(IClipboard clipboard)
    {
        _clipboard = clipboard;

        ClipboardTextChanged = Observable.Interval(TimeSpan.FromMilliseconds(750))
            .SelectMany(async _ => 
            {
                try 
                {
                    return await _clipboard.GetTextAsync() ?? string.Empty;
                }
                catch 
                {
                    return string.Empty; 
                }
            })
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .DistinctUntilChanged() 
            .Publish()
            .RefCount();
    }

    public async void SetText(string text)
    {
        await _clipboard.SetTextAsync(text);
    }
}
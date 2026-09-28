using Avalonia;
using Avalonia.ReactiveUI;
using System;

namespace MacClipboardMonitor;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Guard de versión única: solo la última M.M.M puede correr (silencioso).
        MacClipboardMonitor.Services.VersionGuard.Enforce();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}

/*
agregar vista previa y tags para identificar mas facilmente lo encriptado
*/
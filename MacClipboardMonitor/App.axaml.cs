using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MacClipboardMonitor.Data;
using MacClipboardMonitor.Repositories;
using MacClipboardMonitor.Services;
using MacClipboardMonitor.ViewModels;
using MacClipboardMonitor.Views;

namespace MacClipboardMonitor;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            
            var dbContext = new AppDbContext();
            var repository = new ClipboardRepository(dbContext);
            var clipboardService = new PollingClipboardMonitorService(mainWindow.Clipboard!);
            
            mainWindow.DataContext = new MainWindowViewModel(clipboardService, repository);
            desktop.MainWindow = mainWindow;
            
            AutoStartManager.RegisterAutoStart();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
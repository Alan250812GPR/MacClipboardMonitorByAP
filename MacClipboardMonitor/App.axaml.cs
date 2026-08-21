using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using MacClipboardMonitor.Data;
using MacClipboardMonitor.Repositories;
using MacClipboardMonitor.Services;
using MacClipboardMonitor.ViewModels;
using MacClipboardMonitor.Views;

namespace MacClipboardMonitor;

public partial class App : Application
{
    private TrayIcon? _trayIcon;

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
            
            var viewModel = new MainWindowViewModel(clipboardService, repository);
            mainWindow.DataContext = viewModel;
            desktop.MainWindow = mainWindow;

            SetupTrayIcon(desktop, mainWindow, viewModel);
            
            AutoStartManager.RegisterAutoStart();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window, MainWindowViewModel viewModel)
    {
        var toggleItem = new NativeMenuItem { Header = "Mostrar / Ocultar" };
        toggleItem.Click += (_, _) => ToggleWindow(window);

        var clearItem = new NativeMenuItem { Header = "Borrar historial" };
        clearItem.Click += (_, _) =>
        {
            if (viewModel.ClearHistoryCommand.CanExecute(null))
            {
                viewModel.ClearHistoryCommand.Execute(null);
            }
        };

        var quitItem = new NativeMenuItem { Header = "Salir" };
        quitItem.Click += (_, _) => desktop.Shutdown();

        var menu = new NativeMenu();
        menu.Items.Add(toggleItem);
        menu.Items.Add(clearItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "MacClipboardMonitor",
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://MacClipboardMonitor/Assets/tray.png"))),
            Menu = menu,
            IsVisible = true
        };

        // Ícono de plantilla para que se adapte a la barra de menú clara/oscura en macOS.
        MacOSProperties.SetIsTemplateIcon(_trayIcon, true);

        _trayIcon.Clicked += (_, _) => ToggleWindow(window);
    }

    private void ToggleWindow(MainWindow window)
    {
        if (window.IsVisible)
        {
            window.Hide();
        }
        else
        {
            window.Show();
            window.Activate();
        }
    }
}

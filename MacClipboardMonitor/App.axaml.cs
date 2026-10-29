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
    private MainWindowViewModel? _viewModel;
    private AppDbContext? _dbContext;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Segunda barrera silenciosa por si el guard de Program fue sorteado (ej. design preview)
        try
        {
            var cur = MacClipboardMonitor.Services.AppVersion.Current;
            var cfgCheck = AppConfigService.Load();
            if (!string.IsNullOrWhiteSpace(cfgCheck.LatestVersion) &&
                MacClipboardMonitor.Services.AppVersion.TryParse(cfgCheck.LatestVersion, out var latest) &&
                MacClipboardMonitor.Services.AppVersion.Compare(cur, latest) < 0)
            {
                Environment.Exit(0);
            }
        }
        catch { Environment.Exit(0); }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var config = AppConfigService.Load();
            _mainWindow = new MainWindow(config);

            _dbContext = new AppDbContext();
            var repository = new ClipboardRepository(_dbContext);
            var clipboardService = new PollingClipboardMonitorService(_mainWindow.Clipboard!);
            var pasteService = new MacPasteService();

            _viewModel = new MainWindowViewModel(clipboardService, repository, pasteService, config);
            _mainWindow.DataContext = _viewModel;
            _viewModel.OnViewInitialized(); // abrir modal de bienvenida si corresponde
            desktop.MainWindow = _mainWindow;

            // La ventana recarga el hook global cuando cambia el atajo desde Ajustes.
            _viewModel.HotkeyChanged += _mainWindow.ReloadHotkeyConfig;

            SetupTrayIcon(desktop, _mainWindow, _viewModel);

            AutoStartManager.RegisterAutoStart();

            desktop.Exit += (_, _) => ReleaseRuntime();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ReleaseRuntime()
    {
        try
        {
            if (_viewModel is not null && _mainWindow is not null)
            {
                _viewModel.HotkeyChanged -= _mainWindow.ReloadHotkeyConfig;
            }

            _viewModel?.Dispose();
            _viewModel = null;

            _dbContext?.Dispose();
            _dbContext = null;

            _trayIcon?.Dispose();
            _trayIcon = null;

            VersionGuard.Release();
        }
        catch
        {
            // Cierre: no impedir la salida.
        }
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
            if (window.DataContext is MainWindowViewModel vm)
                vm.RefreshPermissionState();
        }
    }
}

using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MacClipboardMonitor.Services;
using MacClipboardMonitor.ViewModels;
using SharpHook;
using SharpHook.Data;
using SharpHook.Native;

namespace MacClipboardMonitor.Views;

public partial class MainWindow : Window
{
    private readonly TaskPoolGlobalHook _globalHook;
    private readonly AppConfigService _config;

    private bool _isCtrlPressed;
    private bool _isCmdPressed;
    private bool _isShiftPressed;
    private bool _isAltPressed;

    // Atajo global actual (se recarga desde la configuración).
    private KeyCode _hotkeyCode = KeyCode.VcV;
    private bool _needCtrl = true;
    private bool _needCmd = true;
    private bool _needShift;
    private bool _needAlt;

    // Constructor sin parámetros requerido por el cargador de XAML compilado.
    public MainWindow() : this(AppConfigService.Load())
    {
    }

    public MainWindow(AppConfigService config)
    {
        _config = config;

        InitializeComponent();

        // Teclado local en modo túnel: prioridad sobre el ListBox para navegar el historial.
        AddHandler(KeyDownEvent, OnPreviewKeyDownTunnel, RoutingStrategies.Tunnel);

        // Rueda del mouse en modo túnel: zoom de la vista previa antes del ScrollViewer.
        AddHandler(PointerWheelChangedEvent, OnPreviewWheelTunnel, RoutingStrategies.Tunnel);

        ReloadHotkeyConfig();

        _globalHook = new TaskPoolGlobalHook();
        
        _globalHook.KeyPressed += OnGlobalKeyPressed;
        _globalHook.KeyReleased += OnGlobalKeyReleased;
        
        _globalHook.RunAsync();
    }

    // Recarga el atajo global desde la configuración persistida.
    public void ReloadHotkeyConfig()
    {
        var parts = (_config.HotkeyModifiers ?? string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _needCtrl = parts.Contains("Control");
        _needCmd = parts.Contains("Meta");
        _needShift = parts.Contains("Shift");
        _needAlt = parts.Contains("Alt");

        // Nunca permitir un atajo sin modificadores.
        if (!_needCtrl && !_needCmd && !_needShift && !_needAlt)
        {
            _needCtrl = true;
            _needCmd = true;
        }

        // La tecla se guarda como letra (A-Z) o función (F1-F12): prefijo "Vc" del enum de SharpHook.
        _hotkeyCode = Enum.TryParse<KeyCode>($"Vc{_config.HotkeyKey}", ignoreCase: false, out var keyCode)
            ? keyCode
            : KeyCode.VcV;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnHideClick(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    // Doble clic en una tarjeta: oculta la ventana y pega directo en la app activa.
    private async void OnCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border { DataContext: MacClipboardMonitor.Models.ClipboardItem item }) return;
        if (DataContext is not MainWindowViewModel vm) return;

        Hide();
        await vm.PasteItemAsync(item);
    }

    private bool MatchesModifiers() =>
        (!_needCtrl || _isCtrlPressed) &&
        (!_needCmd || _isCmdPressed) &&
        (!_needShift || _isShiftPressed) &&
        (!_needAlt || _isAltPressed);

    private void OnGlobalKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcLeftControl || e.Data.KeyCode == KeyCode.VcRightControl) 
            _isCtrlPressed = true;

        if (e.Data.KeyCode == KeyCode.VcLeftMeta || e.Data.KeyCode == KeyCode.VcRightMeta) 
            _isCmdPressed = true;

        if (e.Data.KeyCode == KeyCode.VcLeftShift || e.Data.KeyCode == KeyCode.VcRightShift) 
            _isShiftPressed = true;

        if (e.Data.KeyCode == KeyCode.VcLeftAlt || e.Data.KeyCode == KeyCode.VcRightAlt) 
            _isAltPressed = true;
        
        if (e.Data.KeyCode == _hotkeyCode && MatchesModifiers())
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (IsVisible)
                {
                    Hide();
                }
                else
                {
                    Show();
                    Activate();
                }
            });
        }
    }

    private void OnGlobalKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcLeftControl || e.Data.KeyCode == KeyCode.VcRightControl) 
            _isCtrlPressed = false;
            
        if (e.Data.KeyCode == KeyCode.VcLeftMeta || e.Data.KeyCode == KeyCode.VcRightMeta) 
            _isCmdPressed = false;

        if (e.Data.KeyCode == KeyCode.VcLeftShift || e.Data.KeyCode == KeyCode.VcRightShift) 
            _isShiftPressed = false;

        if (e.Data.KeyCode == KeyCode.VcLeftAlt || e.Data.KeyCode == KeyCode.VcRightAlt) 
            _isAltPressed = false;
    }
    
    // Navegación por teclado: ↑↓ mueven la selección, Enter copia, Esc cierra vistas u oculta.
    private void OnPreviewKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Captura de un nuevo atajo global: consume la tecla antes que nada.
        if (vm.IsRecordingHotkey)
        {
            if (vm.TryCaptureHotkey(e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                return;
            }
        }

        switch (e.Key)
        {
            case Key.Down:
                vm.NavigateHistory(1);
                e.Handled = true;
                break;

            case Key.Up:
                vm.NavigateHistory(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                if (vm.SelectedItem is { } item)
                {
                    vm.CopyItem(item);
                }
                e.Handled = true;
                break;

            case Key.Escape:
                if (vm.IsImagePreviewOpen)
                {
                    vm.CloseImagePreview();
                }
                else if (vm.IsSettingsOpen)
                {
                    vm.CloseSettings();
                }
                else
                {
                    Hide();
                }
                e.Handled = true;
                break;
        }
    }

    // Ctrl + rueda ajusta el zoom de la vista previa de imágenes.
    private void OnPreviewWheelTunnel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.IsImagePreviewOpen) return;
        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;

        vm.ZoomPreview(e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _globalHook.Dispose();
        base.OnClosed(e);
    }
}

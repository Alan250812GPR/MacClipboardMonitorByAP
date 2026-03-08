using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SharpHook;
using SharpHook.Data;
using SharpHook.Native;

namespace MacClipboardMonitor.Views;

public partial class MainWindow : Window
{
    private readonly TaskPoolGlobalHook _globalHook;
    
    private bool _isCtrlPressed;
    private bool _isCmdPressed;

    public MainWindow()
    {
        InitializeComponent();
        
        _globalHook = new TaskPoolGlobalHook();
        
        _globalHook.KeyPressed += OnGlobalKeyPressed;
        _globalHook.KeyReleased += OnGlobalKeyReleased;
        
        _globalHook.RunAsync();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnHideClick(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnGlobalKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcLeftControl || e.Data.KeyCode == KeyCode.VcRightControl) 
            _isCtrlPressed = true;
            
        if (e.Data.KeyCode == KeyCode.VcLeftMeta || e.Data.KeyCode == KeyCode.VcRightMeta) 
            _isCmdPressed = true;
        
        if (e.Data.KeyCode == KeyCode.VcV && _isCtrlPressed && _isCmdPressed)
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
    }
    
    protected override void OnClosed(EventArgs e)
    {
        _globalHook.Dispose();
        base.OnClosed(e);
    }
}
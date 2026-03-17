using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using MacClipboardMonitor.Models;
using MacClipboardMonitor.Repositories;
using MacClipboardMonitor.Services;
using ReactiveUI;
using DynamicData;
using System.Windows.Input;

namespace MacClipboardMonitor.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IClipboardMonitorService _clipboardService;
    private readonly IClipboardRepository _repository;
    private readonly IDisposable _clipboardSubscription;
    private readonly SourceList<ClipboardItem> _historyList = new SourceList<ClipboardItem>();
    
    public ICommand ClearHistoryCommand { get; }
    public ICommand DeleteItemCommand { get; }

    private readonly ReadOnlyObservableCollection<ClipboardItem> _history;
    public ReadOnlyObservableCollection<ClipboardItem> History => _history;

    private ClipboardItem? _selectedItem;
    public ClipboardItem? SelectedItem
    {
        get => _selectedItem;
        set 
        {
            this.RaiseAndSetIfChanged(ref _selectedItem, value);
            if (value != null)
            {
                OnItemSelected(value);
            }
        }
    }

    public MainWindowViewModel(IClipboardMonitorService clipboardService, IClipboardRepository repository)
    {
        _clipboardService = clipboardService;
        _repository = repository;

        _historyList.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _history)
            .Subscribe();

        LoadHistoryAsync();

        DeleteItemCommand = ReactiveCommand.CreateFromTask<ClipboardItem>(async item =>
        {
            if (item == null) return;
            await _repository.DeleteItemAsync(item.Id);
            _historyList.Remove(item);
        });

        ClearHistoryCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await _repository.ClearAllAsync();
            _historyList.Clear();
        });

        // Vuelve a usar ClipboardTextChanged que emite un simple string
        _clipboardSubscription = _clipboardService.ClipboardTextChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async newText => 
            {
                var newItem = new ClipboardItem 
                { 
                    Content = newText, 
                    CreatedAt = DateTime.Now 
                };

                // 1. BLOQUEO DE DUPLICADOS EN MEMORIA (Ignorando mayúsculas)
                bool isDuplicate = _historyList.Items.Any(x => 
                    string.Equals(x.Content, newItem.Content, StringComparison.OrdinalIgnoreCase));

                if (isDuplicate) return;

                // 2. Guardamos en SQLite
                await _repository.AddItemAsync(newItem);
                
                // 3. Mostramos en la UI
                _historyList.Insert(0, newItem);
                
                if (_historyList.Count > 50)
                {
                    _historyList.RemoveAt(_historyList.Count - 1);
                }
            });
    }

    private async void LoadHistoryAsync()
    {
        var items = await _repository.GetRecentItemsAsync(50);
        _historyList.AddRange(items); 
    }

    private void OnItemSelected(ClipboardItem item)
    {
        // Solo inyectamos texto
        _clipboardService.SetText(item.Content);
        SelectedItem = null;
    }

    public void Dispose()
    {
        _clipboardSubscription?.Dispose();
        _historyList?.Dispose();
    }
}
using System;
using System.Collections.ObjectModel;
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

        _clipboardSubscription = _clipboardService.ClipboardTextChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async newText => 
            {
                var newItem = new ClipboardItem 
                { 
                    Content = newText, 
                    CreatedAt = DateTime.Now 
                };

                await _repository.AddItemAsync(newItem);
                
                _historyList.Insert(0, newItem);
                
                if (_historyList.Count > 50)
                {
                    _historyList.RemoveAt(_historyList.Count - 1);
                }
            });
        
        ClearHistoryCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await _repository.ClearAllAsync();
            _historyList.Clear();
        });
    }

    private async void LoadHistoryAsync()
    {
        var items = await _repository.GetRecentItemsAsync(50);
        _historyList.AddRange(items); 
    }

    private void OnItemSelected(ClipboardItem item)
    {
        _clipboardService.SetText(item.Content);
        SelectedItem = null;
    }

    public void Dispose()
    {
        _clipboardSubscription?.Dispose();
        _historyList?.Dispose();
    }
}
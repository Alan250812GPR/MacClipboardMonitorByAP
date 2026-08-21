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
    private readonly IDisposable _purgeSubscription;
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

        _clipboardSubscription = _clipboardService.ClipboardChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async capture =>
            {
                var newItem = new ClipboardItem
                {
                    Type = capture.Type,
                    Content = capture.Text ?? string.Empty,
                    ImageBytes = capture.ImageBytes,
                    ImageHash = capture.ImageHash,
                    FilePaths = capture.FilePaths is null ? null : string.Join("\n", capture.FilePaths),
                    CreatedAt = DateTime.Now
                };

                // 1. BLOQUEO DE DUPLICADOS EN MEMORIA según el tipo
                bool isDuplicate = capture.Type switch
                {
                    ClipboardItemType.Texto =>
                        _historyList.Items.Any(x => x.Type == ClipboardItemType.Texto &&
                                                    string.Equals(x.Content, newItem.Content, StringComparison.OrdinalIgnoreCase)),
                    ClipboardItemType.Imagen =>
                        !string.IsNullOrEmpty(newItem.ImageHash) &&
                        _historyList.Items.Any(x => x.Type == ClipboardItemType.Imagen && x.ImageHash == newItem.ImageHash),
                    ClipboardItemType.Archivo =>
                        !string.IsNullOrEmpty(newItem.FilePaths) &&
                        _historyList.Items.Any(x => x.Type == ClipboardItemType.Archivo && x.FilePaths == newItem.FilePaths),
                    _ => false
                };

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

        // Limpieza periódica: elimina lo caducado de memoria y de la DB.
        _purgeSubscription = Observable.Interval(TimeSpan.FromMinutes(1))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ =>
            {
                var now = DateTime.Now;
                var textCutoff = now.AddHours(-48);
                var fileCutoff = now.AddHours(-1);

                var expired = _historyList.Items
                    .Where(x => (x.Type == ClipboardItemType.Texto && x.CreatedAt < textCutoff) ||
                                (x.Type != ClipboardItemType.Texto && x.CreatedAt < fileCutoff))
                    .ToList();

                foreach (var item in expired)
                {
                    _historyList.Remove(item);
                }

                await _repository.PurgeExpiredAsync();
            });
    }

    private async void LoadHistoryAsync()
    {
        var items = await _repository.GetRecentItemsAsync(50);
        _historyList.AddRange(items); 
    }

    private void OnItemSelected(ClipboardItem item)
    {
        switch (item.Type)
        {
            case ClipboardItemType.Texto:
                _clipboardService.SetText(item.Content);
                break;

            case ClipboardItemType.Imagen:
                if (item.ImageBytes is { Length: > 0 })
                {
                    _clipboardService.SetImage(item.ImageBytes);
                }
                break;

            case ClipboardItemType.Archivo:
                if (item.FileList.Count > 0)
                {
                    _clipboardService.SetFiles(item.FileList);
                }
                break;
        }

        SelectedItem = null;
    }

    public void Dispose()
    {
        _clipboardSubscription?.Dispose();
        _purgeSubscription?.Dispose();
        _historyList?.Dispose();
    }
}

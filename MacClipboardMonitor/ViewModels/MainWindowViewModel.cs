using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Media.Imaging;
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
    private readonly IPasteService _pasteService;
    private readonly IDisposable _clipboardSubscription;
    private readonly IDisposable _purgeSubscription;
    private readonly IDisposable? _filterSubscription;
    private readonly SourceList<ClipboardItem> _historyList = new SourceList<ClipboardItem>();
    
    public ICommand ClearHistoryCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand OpenImagePreviewCommand { get; }
    public ICommand CloseImagePreviewCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand RestoreDefaultHotkeyCommand { get; }
    public ICommand EncryptItemCommand { get; }

    // Huella del último secreto copiado, para evitar que el monitor lo re-capture en texto plano.
    private string? _suppressedSecretFingerprint;

    // Texto de búsqueda para filtrar el historial.
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            this.RaisePropertyChanged(nameof(HasSearchText));
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    private readonly ReadOnlyObservableCollection<ClipboardItem> _history;
    public ReadOnlyObservableCollection<ClipboardItem> History => _history;

    // Vista previa ampliada de imágenes.
    private bool _isImagePreviewOpen;
    public bool IsImagePreviewOpen
    {
        get => _isImagePreviewOpen;
        set => this.RaiseAndSetIfChanged(ref _isImagePreviewOpen, value);
    }

    private Bitmap? _previewBitmap;
    private Bitmap? _previewImageSource;
    public Bitmap? PreviewImageSource
    {
        get => _previewImageSource;
        private set => this.RaiseAndSetIfChanged(ref _previewImageSource, value);
    }

    private double _previewZoom = 1.0;
    public double PreviewZoom
    {
        get => _previewZoom;
        set => this.RaiseAndSetIfChanged(ref _previewZoom, Math.Clamp(value, 0.5, 4.0));
    }

    // Ajustes (atajo global configurable).
    private readonly AppConfigService _config;

    // Notifica a la ventana que debe recargar el atajo del hook global.
    public event Action? HotkeyChanged;

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
    }

    private bool _isRecordingHotkey;
    public bool IsRecordingHotkey
    {
        get => _isRecordingHotkey;
        private set
        {
            if (this.RaiseAndSetIfChanged(ref _isRecordingHotkey, value))
            {
                this.RaisePropertyChanged(nameof(RecordButtonText));
            }
        }
    }

    public string HotkeyDisplay => FormatHotkey(_config.HotkeyModifiers, _config.HotkeyKey);

    public string RecordButtonText => IsRecordingHotkey ? "Presiona la combinación..." : "Cambiar atajo";

    // Evita que el cambio de selección por teclado dispare el copiado automático.
    private bool _suppressCopyOnSelect;

    private ClipboardItem? _selectedItem;
    public ClipboardItem? SelectedItem
    {
        get => _selectedItem;
        set 
        {
            this.RaiseAndSetIfChanged(ref _selectedItem, value);
            if (value != null && !_suppressCopyOnSelect)
            {
                OnItemSelected(value);
            }
        }
    }

    public MainWindowViewModel(IClipboardMonitorService clipboardService, IClipboardRepository repository, IPasteService pasteService, AppConfigService config)
    {
        _clipboardService = clipboardService;
        _repository = repository;
        _pasteService = pasteService;
        _config = config;

        // Filtro de búsqueda: se recalcula al escribir (con debounce de 250 ms).
        var filterPredicate = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250), RxApp.MainThreadScheduler)
            .Select(BuildFilter)
            .StartWith(BuildFilter(string.Empty));

        _filterSubscription = _historyList.Connect()
            .Filter(filterPredicate)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _history)
            .Subscribe();

        LoadHistoryAsync();

        ClearSearchCommand = ReactiveCommand.Create(() => SearchText = string.Empty);

        OpenImagePreviewCommand = ReactiveCommand.Create<ClipboardItem>(OpenImagePreview);
        CloseImagePreviewCommand = ReactiveCommand.Create(CloseImagePreview);

        OpenSettingsCommand = ReactiveCommand.Create(() => IsSettingsOpen = true);
        CloseSettingsCommand = ReactiveCommand.Create(CloseSettings);
        StartRecordingCommand = ReactiveCommand.Create(() => IsRecordingHotkey = true);
        RestoreDefaultHotkeyCommand = ReactiveCommand.Create(RestoreDefaultHotkey);

        EncryptItemCommand = ReactiveCommand.CreateFromTask<ClipboardItem>(EncryptItemAsync);

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
                // Supresión de re-captura: si acabamos de copiar un secreto descifrado,
                // no lo volvemos a guardar como texto plano.
                if (capture.Type == ClipboardItemType.Texto && _suppressedSecretFingerprint is not null)
                {
                    var suppressed = _suppressedSecretFingerprint;
                    _suppressedSecretFingerprint = null;

                    if (HashText(capture.Text ?? string.Empty) == suppressed) return;
                }

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
                
                if (_historyList.Count > IClipboardRepository.MaxItems)
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
                    .Where(x => !x.IsEncrypted &&
                                ((x.Type == ClipboardItemType.Texto && x.CreatedAt < textCutoff) ||
                                 (x.Type != ClipboardItemType.Texto && x.CreatedAt < fileCutoff)))
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
        var items = await _repository.GetRecentItemsAsync(IClipboardRepository.MaxItems);
        _historyList.AddRange(items); 
    }

    private void OnItemSelected(ClipboardItem item)
    {
        CopyItem(item);
        SelectedItem = null;
    }

    // Copia el elemento al portapapeles según su tipo (sin alterar la selección).
    public void CopyItem(ClipboardItem item)
    {
        switch (item.Type)
        {
            case ClipboardItemType.Texto:
                if (item.IsEncrypted)
                {
                    // Descifra al vuelo y evita que el monitor re-capture el secreto.
                    var plain = Decrypt(item.CipherText);
                    if (plain is null) return;

                    _clipboardService.SetText(plain);
                    _suppressedSecretFingerprint = HashText(plain);
                }
                else
                {
                    _clipboardService.SetText(item.Content);
                }
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
    }

    // Encripta una entrada de texto: el contenido queda solo en CipherText.
    private async Task EncryptItemAsync(ClipboardItem item)
    {
        if (item is null || !item.IsText || item.IsEncrypted) return;
        if (string.IsNullOrEmpty(item.Content)) return;

        var cipher = EncryptionService.Encrypt(item.Content);
        if (cipher is null) return;

        item.CipherText = cipher;
        item.Content = string.Empty;
        item.IsEncrypted = true;

        await _repository.MarkEncryptedAsync(item);
    }

    private static string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return null;
        try
        {
            return EncryptionService.Decrypt(cipherText);
        }
        catch
        {
            return null;
        }
    }

    private static string HashText(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    // Indica si falta el permiso de Accesibilidad para pegar directo.
    public bool ShowPasteWarning => !_pasteService.CanPaste();

    // Copia el elemento y envía Cmd+V a la app activa (la ventana debe ocultarse antes).
    public async Task PasteItemAsync(ClipboardItem item)
    {
        if (item is null) return;

        CopyItem(item);

        // Margen para que la copia llegue al portapapeles antes de simular Cmd+V.
        await Task.Delay(100);
        await _pasteService.PasteToActiveAppAsync();
    }

    // Abre la vista previa ampliada de una imagen (zoom inicial 1x).
    private void OpenImagePreview(ClipboardItem item)
    {
        if (!item.IsImage || item.ImageBytes is not { Length: > 0 }) return;

        try
        {
            using var stream = new MemoryStream(item.ImageBytes);
            _previewBitmap?.Dispose();
            _previewBitmap = new Bitmap(stream);
            PreviewImageSource = _previewBitmap;
            PreviewZoom = 1.0;
            IsImagePreviewOpen = true;
        }
        catch
        {
            // Imagen corrupta: no abrir la vista previa.
        }
    }

    public void CloseImagePreview()
    {
        IsImagePreviewOpen = false;
    }

    public void CloseSettings()
    {
        IsSettingsOpen = false;
        IsRecordingHotkey = false;
    }

    // Restaura el atajo por defecto (Ctrl+Cmd+V).
    private void RestoreDefaultHotkey()
    {
        _config.HotkeyModifiers = "Control|Meta";
        _config.HotkeyKey = "V";
        _config.Save();

        IsRecordingHotkey = false;
        RaiseHotkeyChanged();
    }

    // Captura la siguiente combinación de teclas como nuevo atajo global.
    // Devuelve true si la tecla fue consumida (estamos grabando).
    public bool TryCaptureHotkey(Key key, KeyModifiers mods)
    {
        if (!IsRecordingHotkey) return false;

        // Esc cancela la grabación.
        if (key == Key.Escape)
        {
            IsRecordingHotkey = false;
            return true;
        }

        // Solo letras y teclas de función F1-F12.
        bool isLetter = key is >= Key.A and <= Key.Z;
        bool isFunction = key is >= Key.F1 and <= Key.F12;
        if (!isLetter && !isFunction) return true;

        var parts = new List<string>();
        if (mods.HasFlag(KeyModifiers.Control)) parts.Add("Control");
        if (mods.HasFlag(KeyModifiers.Meta)) parts.Add("Meta");
        if (mods.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (mods.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");

        // Exigir al menos Ctrl o Cmd para no interferir con la escritura normal.
        if (!parts.Contains("Control") && !parts.Contains("Meta")) return true;

        _config.HotkeyModifiers = string.Join("|", parts);
        _config.HotkeyKey = key.ToString();
        _config.Save();

        IsRecordingHotkey = false;
        RaiseHotkeyChanged();
        return true;
    }

    private void RaiseHotkeyChanged()
    {
        this.RaisePropertyChanged(nameof(HotkeyDisplay));
        this.RaisePropertyChanged(nameof(RecordButtonText));
        HotkeyChanged?.Invoke();
    }

    private static string FormatHotkey(string modifiers, string key)
    {
        var labels = modifiers
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(m => m switch
            {
                "Control" => "Ctrl",
                "Meta" => "Cmd",
                _ => m
            });

        return string.Join(" + ", labels) + " + " + key;
    }

    // Ajusta el zoom de la vista previa (delta > 0 acerca, < 0 aleja).
    public void ZoomPreview(double delta)
    {
        if (!IsImagePreviewOpen) return;
        PreviewZoom += delta > 0 ? 0.2 : -0.2;
    }

    // Navega el historial con las flechas del teclado (delta: +1 abajo, -1 arriba).
    public void NavigateHistory(int delta)
    {
        var items = History;
        if (items.Count == 0) return;

        int current = _selectedItem is null ? -1 : items.IndexOf(_selectedItem);
        int next = current < 0
            ? (delta > 0 ? 0 : items.Count - 1)
            : Math.Clamp(current + delta, 0, items.Count - 1);

        if (next == current) return;

        _suppressCopyOnSelect = true;
        try
        {
            SelectedItem = items[next];
        }
        finally
        {
            _suppressCopyOnSelect = false;
        }
    }

    // Construye el predicado de filtro a partir del texto de búsqueda.
    private static Func<ClipboardItem, bool> BuildFilter(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return _ => true;
        }

        var term = searchText.Trim();

        return item => item.Type switch
        {
            ClipboardItemType.Texto =>
                !item.IsEncrypted &&
                !string.IsNullOrEmpty(item.Content) &&
                item.Content.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0,

            ClipboardItemType.Archivo =>
                !string.IsNullOrEmpty(item.FilePaths) &&
                item.FilePaths.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0,

            // Las imágenes no tienen texto que buscar: se ocultan al filtrar.
            _ => false
        };
    }

    public void Dispose()
    {
        _clipboardSubscription?.Dispose();
        _purgeSubscription?.Dispose();
        _filterSubscription?.Dispose();
        _previewBitmap?.Dispose();
        _historyList?.Dispose();
    }
}

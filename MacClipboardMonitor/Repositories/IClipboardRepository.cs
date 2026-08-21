using System.Collections.Generic;
using System.Threading.Tasks;
using MacClipboardMonitor.Models;

namespace MacClipboardMonitor.Repositories;

public interface IClipboardRepository
{
    // Límite global de elementos conservados en el historial.
    public const int MaxItems = 100;

    Task<List<ClipboardItem>> GetRecentItemsAsync(int limit = MaxItems);
    Task AddItemAsync(ClipboardItem item);
    Task DeleteItemAsync(int id);
    Task ClearAllAsync();
    Task PurgeExpiredAsync();
    Task MarkEncryptedAsync(ClipboardItem item);
}

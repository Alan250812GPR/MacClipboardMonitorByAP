using System.Collections.Generic;
using System.Threading.Tasks;
using MacClipboardMonitor.Models;

namespace MacClipboardMonitor.Repositories;

public interface IClipboardRepository
{
    Task<List<ClipboardItem>> GetRecentItemsAsync(int limit = 50);
    Task AddItemAsync(ClipboardItem item);
    Task DeleteItemAsync(int id);
    Task ClearAllAsync();
}
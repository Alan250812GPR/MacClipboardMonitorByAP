using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MacClipboardMonitor.Data;
using MacClipboardMonitor.Models;
using Microsoft.EntityFrameworkCore;

namespace MacClipboardMonitor.Repositories;

public class ClipboardRepository : IClipboardRepository
{
    private readonly AppDbContext _dbContext;

    public ClipboardRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ClipboardItem>> GetRecentItemsAsync(int limit = 50)
    {
        return await _dbContext.ClipboardItems
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task AddItemAsync(ClipboardItem item)
    {
        var lastItem = await _dbContext.ClipboardItems
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastItem?.Content == item.Content)
            return;

        _dbContext.ClipboardItems.Add(item);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(int id)
    {
        var item = await _dbContext.ClipboardItems.FindAsync(id);
        if (item != null)
        {
            _dbContext.ClipboardItems.Remove(item);
            await _dbContext.SaveChangesAsync();
        }
    }
    
    public async Task ClearAllAsync()
    {
        await _dbContext.ClipboardItems.ExecuteDeleteAsync();
    }
}
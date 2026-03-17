using System;
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
        // 1. Anti-duplicados estricto para texto
        bool exists = await _dbContext.ClipboardItems
            .AnyAsync(x => x.Content.ToLower() == item.Content.ToLower());

        if (exists) return;

        // 2. Regla de 48 horas
        var cutoffDate = DateTime.Now.AddHours(-48);
        await _dbContext.ClipboardItems
            .Where(x => x.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync();

        // 3. Guardar el nuevo elemento
        _dbContext.ClipboardItems.Add(item);
        await _dbContext.SaveChangesAsync();

        // 4. Límite de seguridad
        var count = await _dbContext.ClipboardItems.CountAsync();
        if (count > 50)
        {
            var oldItems = await _dbContext.ClipboardItems
                .OrderBy(x => x.CreatedAt)
                .Take(count - 50)
                .ToListAsync();
                
            _dbContext.ClipboardItems.RemoveRange(oldItems);
            await _dbContext.SaveChangesAsync();
        }
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
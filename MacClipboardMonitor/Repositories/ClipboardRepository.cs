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
        await PurgeExpiredAsync();

        return await _dbContext.ClipboardItems
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task AddItemAsync(ClipboardItem item)
    {
        // 1. Anti-duplicados según el tipo de contenido
        switch (item.Type)
        {
            case ClipboardItemType.Texto:
                bool textExists = await _dbContext.ClipboardItems
                    .AnyAsync(x => x.Type == ClipboardItemType.Texto &&
                                   x.Content.ToLower() == item.Content.ToLower());
                if (textExists) return;
                break;

            case ClipboardItemType.Imagen:
                if (!string.IsNullOrEmpty(item.ImageHash))
                {
                    bool imageExists = await _dbContext.ClipboardItems
                        .AnyAsync(x => x.Type == ClipboardItemType.Imagen &&
                                       x.ImageHash == item.ImageHash);
                    if (imageExists) return;
                }
                break;

            case ClipboardItemType.Archivo:
                if (!string.IsNullOrEmpty(item.FilePaths))
                {
                    bool fileExists = await _dbContext.ClipboardItems
                        .AnyAsync(x => x.Type == ClipboardItemType.Archivo &&
                                       x.FilePaths == item.FilePaths);
                    if (fileExists) return;
                }
                break;
        }

        // 2. Limpieza por caducidad: texto >48h, imagen/archivo >1h
        await PurgeExpiredAsync();

        // 3. Guardar el nuevo elemento
        _dbContext.ClipboardItems.Add(item);
        await _dbContext.SaveChangesAsync();

        // 4. Límite de seguridad global
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

    public async Task PurgeExpiredAsync()
    {
        var now = DateTime.Now;
        var textCutoff = now.AddHours(-48);
        var fileCutoff = now.AddHours(-1);

        await _dbContext.ClipboardItems
            .Where(x => (x.Type == ClipboardItemType.Texto && x.CreatedAt < textCutoff) ||
                        (x.Type != ClipboardItemType.Texto && x.CreatedAt < fileCutoff))
            .ExecuteDeleteAsync();
    }
}

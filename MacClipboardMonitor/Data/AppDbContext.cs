using System;
using System.IO;
using MacClipboardMonitor.Models;
using Microsoft.EntityFrameworkCore;

namespace MacClipboardMonitor.Data;

public class AppDbContext : DbContext
{
    public DbSet<ClipboardItem> ClipboardItems { get; set; }

    public AppDbContext()
    {
        Database.EnsureCreated();
        EnsureSchemaColumns();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MacClipboardMonitor.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    // Como no usamos migraciones, agregamos las columnas nuevas si la DB ya existía.
    private void EnsureSchemaColumns()
    {
        try { Database.ExecuteSqlRaw("ALTER TABLE ClipboardItems ADD COLUMN Type INTEGER NOT NULL DEFAULT 0"); } catch { /* ya existe */ }
        try { Database.ExecuteSqlRaw("ALTER TABLE ClipboardItems ADD COLUMN FilePaths TEXT NULL"); } catch { /* ya existe */ }
        try { Database.ExecuteSqlRaw("ALTER TABLE ClipboardItems ADD COLUMN ImageHash TEXT NULL"); } catch { /* ya existe */ }
    }
}

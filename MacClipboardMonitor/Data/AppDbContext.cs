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
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MacClipboardMonitor.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }
}
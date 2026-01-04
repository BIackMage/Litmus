using System;
using System.Diagnostics;
using System.IO;
using Litmus.Data;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Services;

public static class DatabaseService
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Litmus");

    public static string AttachmentsPath => Path.Combine(AppDataPath, "Attachments");

    public static void Initialize()
    {
        Debug.WriteLine("[DatabaseService] Initializing database...");

        // Ensure directories exist
        Directory.CreateDirectory(AppDataPath);
        Directory.CreateDirectory(AttachmentsPath);

        Debug.WriteLine($"[DatabaseService] App data path: {AppDataPath}");
        Debug.WriteLine($"[DatabaseService] Attachments path: {AttachmentsPath}");

        using var context = new LitmusDbContext();

        // Create database and apply any pending migrations
        context.Database.EnsureCreated();

        // Apply manual migrations for schema changes
        ApplyMigrations(context);

        Debug.WriteLine("[DatabaseService] Database initialized successfully.");
    }

    private static void ApplyMigrations(LitmusDbContext context)
    {
        // Migration: Add IsAutomated column to Tests table
        try
        {
            context.Database.ExecuteSqlRaw(@"
                ALTER TABLE Tests ADD COLUMN IsAutomated INTEGER NOT NULL DEFAULT 0;
            ");
            Debug.WriteLine("[DatabaseService] Migration: Added IsAutomated column to Tests table.");
        }
        catch
        {
            // Column already exists, ignore
            Debug.WriteLine("[DatabaseService] Migration: IsAutomated column already exists.");
        }
    }

    public static LitmusDbContext CreateContext()
    {
        return new LitmusDbContext();
    }
}

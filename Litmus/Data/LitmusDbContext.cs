using Microsoft.EntityFrameworkCore;
using Litmus.Models;
using System;
using System.IO;

namespace Litmus.Data;

public class LitmusDbContext : DbContext
{
    public DbSet<Project> Projects { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Test> Tests { get; set; }
    public DbSet<TestRun> TestRuns { get; set; }
    public DbSet<TestResult> TestResults { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<FailureTemplate> FailureTemplates { get; set; }

    private static string DbPath
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var litmusPath = Path.Combine(appDataPath, "Litmus");
            Directory.CreateDirectory(litmusPath);
            return Path.Combine(litmusPath, "litmus.db");
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasIndex(e => e.Name);
        });

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Categories)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Test configuration
        modelBuilder.Entity<Test>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.Command).HasMaxLength(4000);
            entity.Property(e => e.ExpectedResult).HasMaxLength(4000);
            entity.Property(e => e.PrepSteps).HasMaxLength(4000);
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Tests)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TestRun configuration
        modelBuilder.Entity<TestRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(4000);
            entity.Ignore(e => e.BuildVersion); // Computed property
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.TestRuns)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TestResult configuration
        modelBuilder.Entity<TestResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(4000);
            entity.HasOne(e => e.TestRun)
                  .WithMany(tr => tr.TestResults)
                  .HasForeignKey(e => e.TestRunId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Test)
                  .WithMany(t => t.TestResults)
                  .HasForeignKey(e => e.TestId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.TestRunId, e.TestId }).IsUnique();
        });

        // Attachment configuration
        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.HasOne(e => e.TestResult)
                  .WithMany(tr => tr.Attachments)
                  .HasForeignKey(e => e.TestResultId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // FailureTemplate configuration
        modelBuilder.Entity<FailureTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Seed default failure templates
        modelBuilder.Entity<FailureTemplate>().HasData(
            new FailureTemplate { Id = 1, Name = "Timeout", Description = "Operation timed out", SortOrder = 1 },
            new FailureTemplate { Id = 2, Name = "Crash", Description = "Application crashed", SortOrder = 2 },
            new FailureTemplate { Id = 3, Name = "Wrong Output", Description = "Output did not match expected result", SortOrder = 3 },
            new FailureTemplate { Id = 4, Name = "Connection Failed", Description = "Failed to establish connection", SortOrder = 4 },
            new FailureTemplate { Id = 5, Name = "Permission Denied", Description = "Insufficient permissions", SortOrder = 5 },
            new FailureTemplate { Id = 6, Name = "File Not Found", Description = "Required file was not found", SortOrder = 6 },
            new FailureTemplate { Id = 7, Name = "Exception Thrown", Description = "Unhandled exception occurred", SortOrder = 7 },
            new FailureTemplate { Id = 8, Name = "Memory Error", Description = "Out of memory or memory corruption", SortOrder = 8 }
        );
    }
}

using System;
using System.Diagnostics;
using System.Linq;
using Litmus.Data;
using Litmus.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Litmus.Services;

public static class PdfExportService
{
    public static void ExportReport(string filePath, int projectId, string projectName)
    {
        Debug.WriteLine($"[PdfExportService] Exporting report for project: {projectName}");

        using var context = DatabaseService.CreateContext();

        // Get data
        var resultsQuery = context.TestResults
            .Include(r => r.Test)
            .ThenInclude(t => t.Category)
            .Include(r => r.TestRun)
            .AsQueryable();

        if (projectId > 0)
        {
            resultsQuery = resultsQuery.Where(r => r.TestRun.ProjectId == projectId);
        }

        var latestResults = resultsQuery
            .GroupBy(r => r.TestId)
            .Select(g => g.OrderByDescending(r => r.TestRun.CreatedDate).First())
            .ToList();

        var passedCount = latestResults.Count(r => r.Status == TestStatus.Pass);
        var failedCount = latestResults.Count(r => r.Status == TestStatus.Fail);
        var blockedCount = latestResults.Count(r => r.Status == TestStatus.Blocked);
        var notRunCount = latestResults.Count(r => r.Status == TestStatus.NotRun);
        var totalCount = latestResults.Count;
        var passRate = totalCount > 0 ? (double)passedCount / totalCount * 100 : 0;

        var failedTests = latestResults
            .Where(r => r.Status == TestStatus.Fail)
            .OrderBy(r => r.Test.Category.Name)
            .ThenBy(r => r.Test.Name)
            .ToList();

        // Generate PDF
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, projectName));
                page.Content().Element(c => ComposeContent(c, passedCount, failedCount, blockedCount, notRunCount, passRate, failedTests));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf(filePath);

        Debug.WriteLine($"[PdfExportService] PDF exported to: {filePath}");
    }

    private static void ComposeHeader(IContainer container, string projectName)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("LITMUS TEST REPORT")
                    .FontSize(24).Bold().FontColor(Colors.Blue.Medium);
                column.Item().Text(projectName)
                    .FontSize(14).FontColor(Colors.Grey.Darken2);
                column.Item().Text($"Generated: {DateTime.Now:MMMM dd, yyyy HH:mm}")
                    .FontSize(10).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private static void ComposeContent(IContainer container, int passed, int failed, int blocked, int notRun, double passRate, System.Collections.Generic.List<TestResult> failedTests)
    {
        container.PaddingVertical(20).Column(column =>
        {
            // Summary Section
            column.Item().Text("Summary").FontSize(16).Bold();
            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                {
                    c.Item().Text("Passed").FontColor(Colors.Grey.Medium);
                    c.Item().Text(passed.ToString()).FontSize(24).Bold().FontColor(Colors.Green.Medium);
                });
                row.ConstantItem(10);
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                {
                    c.Item().Text("Failed").FontColor(Colors.Grey.Medium);
                    c.Item().Text(failed.ToString()).FontSize(24).Bold().FontColor(Colors.Red.Medium);
                });
                row.ConstantItem(10);
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                {
                    c.Item().Text("Blocked").FontColor(Colors.Grey.Medium);
                    c.Item().Text(blocked.ToString()).FontSize(24).Bold().FontColor(Colors.Orange.Medium);
                });
                row.ConstantItem(10);
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                {
                    c.Item().Text("Not Run").FontColor(Colors.Grey.Medium);
                    c.Item().Text(notRun.ToString()).FontSize(24).Bold().FontColor(Colors.Grey.Medium);
                });
                row.ConstantItem(10);
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                {
                    c.Item().Text("Pass Rate").FontColor(Colors.Grey.Medium);
                    c.Item().Text($"{passRate:F1}%").FontSize(24).Bold().FontColor(Colors.Blue.Medium);
                });
            });

            // Failed Tests Section
            if (failedTests.Count > 0)
            {
                column.Item().PaddingTop(30).Text("Failed Tests").FontSize(16).Bold();
                column.Item().Text($"{failedTests.Count} test(s) currently failing")
                    .FontSize(10).FontColor(Colors.Red.Medium);

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); // Test Name
                        columns.RelativeColumn(2); // Category
                        columns.RelativeColumn(4); // Notes
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Test Name").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Category").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Notes").Bold();
                    });

                    // Rows
                    foreach (var result in failedTests)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5)
                            .Text(result.Test.Name);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5)
                            .Text(result.Test.Category.Name);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5)
                            .Text(string.IsNullOrEmpty(result.Notes) ? "-" : result.Notes);
                    }
                });
            }
            else
            {
                column.Item().PaddingTop(30).Text("All tests are passing!")
                    .FontSize(14).FontColor(Colors.Green.Medium);
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("Generated by Litmus Test Manager")
                .FontSize(8).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium)).Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        });
    }
}

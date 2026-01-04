using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Litmus.Data;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Services;

public static class JsonExportService
{
    public static string ExportProject(int projectId, bool includeDescription, bool indented)
    {
        Debug.WriteLine($"[JsonExportService] Exporting project: {projectId}");

        using var context = DatabaseService.CreateContext();
        var project = context.Projects
            .Include(p => p.Categories)
            .ThenInclude(c => c.Tests)
            .FirstOrDefault(p => p.Id == projectId);

        if (project == null)
        {
            throw new System.Exception("Project not found.");
        }

        var exportData = new
        {
            project = new
            {
                name = project.Name,
                description = includeDescription ? project.Description : null
            },
            categories = project.Categories
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new
                {
                    name = c.Name,
                    tests = c.Tests
                        .OrderBy(t => t.SortOrder)
                        .ThenBy(t => t.Name)
                        .Select(t => new
                        {
                            name = t.Name,
                            description = t.Description,
                            command = t.Command,
                            expectedResult = t.ExpectedResult,
                            prepSteps = t.PrepSteps,
                            priority = t.Priority.ToString().ToLower(),
                            isAutomated = t.IsAutomated
                        })
                        .ToList()
                })
                .ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(exportData, options);

        Debug.WriteLine($"[JsonExportService] Export complete.");
        return json;
    }

    public static void ExportToFile(string filePath, int projectId, bool includeDescription, bool indented)
    {
        Debug.WriteLine($"[JsonExportService] Exporting to file: {filePath}");

        var json = ExportProject(projectId, includeDescription, indented);
        File.WriteAllText(filePath, json);

        Debug.WriteLine($"[JsonExportService] File saved.");
    }
}

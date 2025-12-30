using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Litmus.Data;
using Litmus.Models;

namespace Litmus.Services;

public class ImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
}

public static class JsonImportService
{
    public static ImportResult ImportFromFile(string filePath, bool overwriteExisting, bool mergeCategories)
    {
        Debug.WriteLine($"[JsonImportService] Importing from: {filePath}");

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            return ImportFromJson(jsonContent, overwriteExisting, mergeCategories);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[JsonImportService] Error reading file: {ex.Message}");
            return new ImportResult { Success = false, Message = $"Error reading file: {ex.Message}" };
        }
    }

    public static ImportResult ImportFromJson(string jsonContent, bool overwriteExisting, bool mergeCategories)
    {
        Debug.WriteLine("[JsonImportService] Parsing JSON...");

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Get project info
            var projectName = root.TryGetProperty("project", out var projectElement)
                ? projectElement.GetProperty("name").GetString() ?? "Imported Project"
                : root.GetProperty("name").GetString() ?? "Imported Project";

            var projectDescription = "";
            if (projectElement.ValueKind != JsonValueKind.Undefined &&
                projectElement.TryGetProperty("description", out var descElement))
            {
                projectDescription = descElement.GetString() ?? "";
            }

            using var context = DatabaseService.CreateContext();

            // Check for existing project
            var existingProject = context.Projects.FirstOrDefault(p => p.Name == projectName);

            Project project;
            if (existingProject != null)
            {
                if (!overwriteExisting)
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = $"Project '{projectName}' already exists. Enable 'Overwrite existing' to replace it."
                    };
                }

                project = existingProject;
                project.Description = projectDescription;

                if (!mergeCategories)
                {
                    // Remove existing categories and tests
                    var categoriesToRemove = context.Categories.Where(c => c.ProjectId == project.Id).ToList();
                    context.Categories.RemoveRange(categoriesToRemove);
                }
            }
            else
            {
                project = new Project
                {
                    Name = projectName,
                    Description = projectDescription,
                    CreatedDate = DateTime.UtcNow
                };
                context.Projects.Add(project);
            }

            context.SaveChanges();

            // Import categories and tests
            var categoriesElement = root.GetProperty("categories");
            var categoryCount = 0;
            var testCount = 0;

            var categoryOrder = context.Categories
                .Where(c => c.ProjectId == project.Id)
                .Max(c => (int?)c.SortOrder) ?? 0;

            foreach (var categoryElement in categoriesElement.EnumerateArray())
            {
                var categoryName = categoryElement.GetProperty("name").GetString() ?? "Unnamed Category";

                // Check for existing category if merging
                var existingCategory = mergeCategories
                    ? context.Categories.FirstOrDefault(c => c.ProjectId == project.Id && c.Name == categoryName)
                    : null;

                Category category;
                if (existingCategory != null)
                {
                    category = existingCategory;
                }
                else
                {
                    category = new Category
                    {
                        ProjectId = project.Id,
                        Name = categoryName,
                        SortOrder = ++categoryOrder
                    };
                    context.Categories.Add(category);
                    context.SaveChanges();
                    categoryCount++;
                }

                // Import tests
                if (categoryElement.TryGetProperty("tests", out var testsElement))
                {
                    var testOrder = context.Tests
                        .Where(t => t.CategoryId == category.Id)
                        .Max(t => (int?)t.SortOrder) ?? 0;

                    foreach (var testElement in testsElement.EnumerateArray())
                    {
                        var testName = testElement.GetProperty("name").GetString() ?? "Unnamed Test";

                        // Check for existing test
                        var existingTest = context.Tests
                            .FirstOrDefault(t => t.CategoryId == category.Id && t.Name == testName);

                        if (existingTest != null && !overwriteExisting)
                        {
                            continue; // Skip existing test
                        }

                        var test = existingTest ?? new Test { CategoryId = category.Id, SortOrder = ++testOrder };

                        test.Name = testName;
                        test.Description = testElement.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                        test.Command = testElement.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
                        test.ExpectedResult = testElement.TryGetProperty("expectedResult", out var e) ? e.GetString() ?? "" : "";
                        test.PrepSteps = testElement.TryGetProperty("prepSteps", out var p) ? p.GetString() ?? "" : "";

                        if (testElement.TryGetProperty("priority", out var priorityElement))
                        {
                            var priorityStr = priorityElement.GetString()?.ToLower() ?? "medium";
                            test.Priority = priorityStr switch
                            {
                                "critical" => Priority.Critical,
                                "high" => Priority.High,
                                "low" => Priority.Low,
                                _ => Priority.Medium
                            };
                        }

                        if (existingTest == null)
                        {
                            context.Tests.Add(test);
                            testCount++;
                        }
                    }
                }
            }

            context.SaveChanges();

            Debug.WriteLine($"[JsonImportService] Import complete: {categoryCount} categories, {testCount} tests");

            return new ImportResult
            {
                Success = true,
                Message = $"Successfully imported {categoryCount} categories and {testCount} tests into '{projectName}'.",
                ProjectId = project.Id
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[JsonImportService] Error parsing JSON: {ex.Message}");
            return new ImportResult { Success = false, Message = $"Error parsing JSON: {ex.Message}" };
        }
    }

    public static ImportResult AppendToProject(string filePath, int projectId, bool mergeCategories, bool skipDuplicates)
    {
        Debug.WriteLine($"[JsonImportService] Appending to project {projectId} from: {filePath}");

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            return AppendToProjectFromJson(jsonContent, projectId, mergeCategories, skipDuplicates);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[JsonImportService] Error reading file: {ex.Message}");
            return new ImportResult { Success = false, Message = $"Error reading file: {ex.Message}" };
        }
    }

    public static ImportResult AppendToProjectFromJson(string jsonContent, int projectId, bool mergeCategories, bool skipDuplicates)
    {
        Debug.WriteLine($"[JsonImportService] Parsing JSON to append to project {projectId}...");

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            using var context = DatabaseService.CreateContext();

            var project = context.Projects.Find(projectId);
            if (project == null)
            {
                return new ImportResult { Success = false, Message = "Project not found." };
            }

            // Get categories array - support both formats
            JsonElement categoriesElement;
            if (root.TryGetProperty("categories", out categoriesElement))
            {
                // Standard format with categories array
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                // Array of categories directly
                categoriesElement = root;
            }
            else
            {
                return new ImportResult { Success = false, Message = "Invalid JSON format: no categories found." };
            }

            var categoryCount = 0;
            var testCount = 0;
            var skippedCount = 0;

            var categoryOrder = context.Categories
                .Where(c => c.ProjectId == projectId)
                .Max(c => (int?)c.SortOrder) ?? 0;

            foreach (var categoryElement in categoriesElement.EnumerateArray())
            {
                var categoryName = categoryElement.GetProperty("name").GetString() ?? "Unnamed Category";

                // Check for existing category if merging
                var existingCategory = mergeCategories
                    ? context.Categories.FirstOrDefault(c => c.ProjectId == projectId && c.Name == categoryName)
                    : null;

                Category category;
                if (existingCategory != null)
                {
                    category = existingCategory;
                    Debug.WriteLine($"[JsonImportService] Merging into existing category: {categoryName}");
                }
                else
                {
                    category = new Category
                    {
                        ProjectId = projectId,
                        Name = categoryName,
                        SortOrder = ++categoryOrder
                    };
                    context.Categories.Add(category);
                    context.SaveChanges();
                    categoryCount++;
                    Debug.WriteLine($"[JsonImportService] Created new category: {categoryName}");
                }

                // Import tests
                if (categoryElement.TryGetProperty("tests", out var testsElement))
                {
                    var testOrder = context.Tests
                        .Where(t => t.CategoryId == category.Id)
                        .Max(t => (int?)t.SortOrder) ?? 0;

                    foreach (var testElement in testsElement.EnumerateArray())
                    {
                        var testName = testElement.GetProperty("name").GetString() ?? "Unnamed Test";

                        // Check for existing test
                        var existingTest = context.Tests
                            .FirstOrDefault(t => t.CategoryId == category.Id && t.Name == testName);

                        if (existingTest != null)
                        {
                            if (skipDuplicates)
                            {
                                skippedCount++;
                                Debug.WriteLine($"[JsonImportService] Skipping duplicate test: {testName}");
                                continue;
                            }
                            // Update existing test
                            existingTest.Description = testElement.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                            existingTest.Command = testElement.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
                            existingTest.ExpectedResult = testElement.TryGetProperty("expectedResult", out var er) ? er.GetString() ?? "" : "";
                            existingTest.PrepSteps = testElement.TryGetProperty("prepSteps", out var p) ? p.GetString() ?? "" : "";

                            if (testElement.TryGetProperty("priority", out var priorityElement))
                            {
                                var priorityStr = priorityElement.GetString()?.ToLower() ?? "medium";
                                existingTest.Priority = ParsePriority(priorityStr);
                            }
                            Debug.WriteLine($"[JsonImportService] Updated existing test: {testName}");
                        }
                        else
                        {
                            var test = new Test
                            {
                                CategoryId = category.Id,
                                SortOrder = ++testOrder,
                                Name = testName,
                                Description = testElement.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                                Command = testElement.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "",
                                ExpectedResult = testElement.TryGetProperty("expectedResult", out var er) ? er.GetString() ?? "" : "",
                                PrepSteps = testElement.TryGetProperty("prepSteps", out var p) ? p.GetString() ?? "" : ""
                            };

                            if (testElement.TryGetProperty("priority", out var priorityElement))
                            {
                                var priorityStr = priorityElement.GetString()?.ToLower() ?? "medium";
                                test.Priority = ParsePriority(priorityStr);
                            }

                            context.Tests.Add(test);
                            testCount++;
                            Debug.WriteLine($"[JsonImportService] Added new test: {testName}");
                        }
                    }
                }
            }

            context.SaveChanges();

            var message = $"Added {testCount} tests";
            if (categoryCount > 0)
            {
                message += $" in {categoryCount} new categories";
            }
            if (skippedCount > 0)
            {
                message += $" ({skippedCount} duplicates skipped)";
            }
            message += $" to '{project.Name}'.";

            Debug.WriteLine($"[JsonImportService] Append complete: {message}");

            return new ImportResult
            {
                Success = true,
                Message = message,
                ProjectId = projectId
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[JsonImportService] Error parsing JSON: {ex.Message}");
            return new ImportResult { Success = false, Message = $"Error parsing JSON: {ex.Message}" };
        }
    }

    private static Priority ParsePriority(string priorityStr)
    {
        return priorityStr switch
        {
            "critical" => Priority.Critical,
            "high" => Priority.High,
            "low" => Priority.Low,
            _ => Priority.Medium
        };
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public class CategorySelection : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TestCount { get; set; }

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class NewTestRunPage : Page
{
    private int? _preselectedProjectId;
    private List<CategorySelection> _categories = new();

    public NewTestRunPage(int? projectId = null)
    {
        InitializeComponent();
        _preselectedProjectId = projectId;
        Debug.WriteLine($"[NewTestRunPage] Initializing with project: {projectId?.ToString() ?? "None"}");
        Loaded += NewTestRunPage_Loaded;

        FullRunRadio.Checked += RunType_Changed;
        CopyPreviousRadio.Checked += RunType_Changed;
        RetestFailedRadio.Checked += RunType_Changed;

        AllTestsRadio.Checked += AutoFilter_Changed;
        AutomatedOnlyRadio.Checked += AutoFilter_Changed;
        ManualOnlyRadio.Checked += AutoFilter_Changed;
    }

    private void AutoFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateUI();
    }

    private bool? GetAutomatedFilter()
    {
        if (AutomatedOnlyRadio.IsChecked == true) return true;
        if (ManualOnlyRadio.IsChecked == true) return false;
        return null; // All tests
    }

    private string GetAutoFilterLabel()
    {
        if (AutomatedOnlyRadio.IsChecked == true) return " (automated only)";
        if (ManualOnlyRadio.IsChecked == true) return " (manual only)";
        return "";
    }

    private void NewTestRunPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProjects();
    }

    private void LoadProjects()
    {
        using var context = DatabaseService.CreateContext();
        var projects = context.Projects
            .Where(p => !p.IsArchived)
            .Include(p => p.Categories)
            .ThenInclude(c => c.Tests)
            .OrderBy(p => p.Name)
            .ToList();

        ProjectComboBox.ItemsSource = projects;

        if (_preselectedProjectId.HasValue)
        {
            var selected = projects.FirstOrDefault(p => p.Id == _preselectedProjectId);
            if (selected != null)
            {
                ProjectComboBox.SelectedItem = selected;
            }
        }
    }

    private void ProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadCategories();
        UpdateUI();
    }

    private void LoadCategories()
    {
        if (ProjectComboBox.SelectedItem is not Project project)
        {
            _categories.Clear();
            CategoriesList.ItemsSource = null;
            return;
        }

        using var context = DatabaseService.CreateContext();
        _categories = context.Categories
            .Where(c => c.ProjectId == project.Id)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategorySelection
            {
                Id = c.Id,
                Name = c.Name,
                TestCount = c.Tests.Count,
                IsSelected = true
            })
            .ToList();

        CategoriesList.ItemsSource = _categories;
        Debug.WriteLine($"[NewTestRunPage] Loaded {_categories.Count} categories for project {project.Name}");
    }

    private void SelectAllCategories_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cat in _categories)
        {
            cat.IsSelected = true;
        }
        UpdateUI();
    }

    private void SelectNoneCategories_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cat in _categories)
        {
            cat.IsSelected = false;
        }
        UpdateUI();
    }

    private void Category_CheckChanged(object sender, RoutedEventArgs e)
    {
        UpdateUI();
    }

    private void RunType_Changed(object sender, RoutedEventArgs e)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (ProjectComboBox.SelectedItem is not Project project)
        {
            StartButton.IsEnabled = false;
            SummaryText.Text = "Select a project to continue.";
            PreviousRunPanel.Visibility = Visibility.Collapsed;
            return;
        }

        using var context = DatabaseService.CreateContext();

        // Get selected category IDs
        var selectedCategoryIds = _categories.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        var selectedCategoryCount = selectedCategoryIds.Count;
        var totalCategoryCount = _categories.Count;

        // Get automated filter
        var autoFilter = GetAutomatedFilter();
        var autoLabel = GetAutoFilterLabel();

        var testQuery = context.Tests.Where(t => selectedCategoryIds.Contains(t.CategoryId));
        if (autoFilter.HasValue)
        {
            testQuery = testQuery.Where(t => t.IsAutomated == autoFilter.Value);
        }
        var testCount = testQuery.Count();

        var previousRuns = context.TestRuns
            .Where(r => r.ProjectId == project.Id)
            .OrderByDescending(r => r.CreatedDate)
            .Take(10)
            .ToList();

        if (CopyPreviousRadio.IsChecked == true || RetestFailedRadio.IsChecked == true)
        {
            PreviousRunPanel.Visibility = Visibility.Visible;
            PreviousRunComboBox.ItemsSource = previousRuns.Select(r => new
            {
                r.Id,
                DisplayText = $"Build {r.BuildVersion} - {r.CreatedDate:MMM dd, yyyy}"
            }).ToList();

            if (previousRuns.Count > 0)
            {
                PreviousRunComboBox.SelectedIndex = 0;
            }
        }
        else
        {
            PreviousRunPanel.Visibility = Visibility.Collapsed;
        }

        // Category info string
        var categoryInfo = selectedCategoryCount == totalCategoryCount
            ? "all categories"
            : $"{selectedCategoryCount} of {totalCategoryCount} categories";

        // Update summary
        if (FullRunRadio.IsChecked == true)
        {
            SummaryText.Text = $"Will create a new test run with {testCount} tests{autoLabel} from {categoryInfo}. All tests will start as 'Not Run'.";
            StartButton.IsEnabled = testCount > 0;
        }
        else if (CopyPreviousRadio.IsChecked == true)
        {
            if (previousRuns.Count == 0)
            {
                SummaryText.Text = "No previous runs available. Use 'Full Run' instead.";
                StartButton.IsEnabled = false;
            }
            else
            {
                SummaryText.Text = $"Will create a new test run with {testCount} tests{autoLabel} from {categoryInfo}, copying results from the selected previous run.";
                StartButton.IsEnabled = testCount > 0;
            }
        }
        else if (RetestFailedRadio.IsChecked == true)
        {
            if (previousRuns.Count == 0)
            {
                SummaryText.Text = "No previous runs available. Use 'Full Run' instead.";
                StartButton.IsEnabled = false;
            }
            else
            {
                var prevRunId = previousRuns.First().Id;
                var failedQuery = context.TestResults
                    .Where(r => r.TestRunId == prevRunId && r.Status == TestStatus.Fail && selectedCategoryIds.Contains(r.Test.CategoryId));
                if (autoFilter.HasValue)
                {
                    failedQuery = failedQuery.Where(r => r.Test.IsAutomated == autoFilter.Value);
                }
                var failedCount = failedQuery.Count();
                SummaryText.Text = $"Will create a new test run with {failedCount} failed tests{autoLabel} from {categoryInfo}.";
                StartButton.IsEnabled = failedCount > 0;
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        AppNavigationService.Instance.GoBack();
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectComboBox.SelectedItem is not Project project)
        {
            return;
        }

        if (!int.TryParse(MajorTextBox.Text, out int major)) major = 1;
        if (!int.TryParse(MinorTextBox.Text, out int minor)) minor = 0;
        if (!int.TryParse(PatchTextBox.Text, out int patch)) patch = 0;

        // Get selected category IDs
        var selectedCategoryIds = _categories.Where(c => c.IsSelected).Select(c => c.Id).ToList();

        Debug.WriteLine($"[NewTestRunPage] Creating test run for project {project.Id}, version {major}.{minor}.{patch}, categories: {selectedCategoryIds.Count}");

        using var context = DatabaseService.CreateContext();

        var testRun = new TestRun
        {
            ProjectId = project.Id,
            MajorVersion = major,
            MinorVersion = minor,
            PatchVersion = patch,
            CreatedDate = DateTime.UtcNow
        };

        context.TestRuns.Add(testRun);
        context.SaveChanges();

        // Get automated filter
        var autoFilter = GetAutomatedFilter();

        // Get tests for selected categories and automated filter
        var testsQuery = context.Tests.Where(t => selectedCategoryIds.Contains(t.CategoryId));
        if (autoFilter.HasValue)
        {
            testsQuery = testsQuery.Where(t => t.IsAutomated == autoFilter.Value);
        }
        var tests = testsQuery.ToList();

        int? previousRunId = null;
        if ((CopyPreviousRadio.IsChecked == true || RetestFailedRadio.IsChecked == true)
            && PreviousRunComboBox.SelectedItem != null)
        {
            previousRunId = (int)PreviousRunComboBox.SelectedItem.GetType().GetProperty("Id")!.GetValue(PreviousRunComboBox.SelectedItem)!;
        }

        var previousResults = previousRunId.HasValue
            ? context.TestResults
                .Where(r => r.TestRunId == previousRunId.Value)
                .ToDictionary(r => r.TestId)
            : null;

        foreach (var test in tests)
        {
            var shouldInclude = true;
            var status = TestStatus.NotRun;
            var notes = string.Empty;

            if (RetestFailedRadio.IsChecked == true)
            {
                // Only include failed tests
                if (previousResults == null || !previousResults.TryGetValue(test.Id, out var prev) || prev.Status != TestStatus.Fail)
                {
                    shouldInclude = false;
                }
            }
            else if (CopyPreviousRadio.IsChecked == true && previousResults != null)
            {
                // Copy previous results
                if (previousResults.TryGetValue(test.Id, out var prev))
                {
                    status = prev.Status;
                    notes = prev.Notes;
                }
            }

            if (shouldInclude)
            {
                context.TestResults.Add(new TestResult
                {
                    TestRunId = testRun.Id,
                    TestId = test.Id,
                    Status = status,
                    Notes = notes
                });
            }
        }

        context.SaveChanges();
        Debug.WriteLine($"[NewTestRunPage] Created test run: {testRun.Id} with {tests.Count} tests");

        // Navigate to execution mode
        AppNavigationService.Instance.NavigateTo(new ExecutionPage(testRun.Id), "Execution");
    }
}

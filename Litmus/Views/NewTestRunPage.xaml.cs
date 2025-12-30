using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public partial class NewTestRunPage : Page
{
    private int? _preselectedProjectId;

    public NewTestRunPage(int? projectId = null)
    {
        InitializeComponent();
        _preselectedProjectId = projectId;
        Debug.WriteLine($"[NewTestRunPage] Initializing with project: {projectId?.ToString() ?? "None"}");
        Loaded += NewTestRunPage_Loaded;

        FullRunRadio.Checked += RunType_Changed;
        CopyPreviousRadio.Checked += RunType_Changed;
        RetestFailedRadio.Checked += RunType_Changed;
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

        var testCount = context.Tests
            .Count(t => t.Category.ProjectId == project.Id);

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

        // Update summary
        if (FullRunRadio.IsChecked == true)
        {
            SummaryText.Text = $"Will create a new test run with {testCount} tests for {project.Name}. All tests will start as 'Not Run'.";
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
                SummaryText.Text = $"Will create a new test run with {testCount} tests, copying results from the selected previous run.";
                StartButton.IsEnabled = true;
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
                var failedCount = context.TestResults
                    .Count(r => r.TestRunId == prevRunId && r.Status == TestStatus.Fail);
                SummaryText.Text = $"Will create a new test run with {failedCount} failed tests from the selected previous run.";
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

        Debug.WriteLine($"[NewTestRunPage] Creating test run for project {project.Id}, version {major}.{minor}.{patch}");

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

        // Get tests for this project
        var tests = context.Tests
            .Where(t => t.Category.ProjectId == project.Id)
            .ToList();

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
        Debug.WriteLine($"[NewTestRunPage] Created test run: {testRun.Id}");

        // Navigate to execution mode
        AppNavigationService.Instance.NavigateTo(new ExecutionPage(testRun.Id), "Execution");
    }
}

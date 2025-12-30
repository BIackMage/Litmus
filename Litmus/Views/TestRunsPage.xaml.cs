using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public partial class TestRunsPage : Page
{
    public TestRunsPage()
    {
        InitializeComponent();
        Debug.WriteLine("[TestRunsPage] Initializing...");
        Loaded += TestRunsPage_Loaded;
    }

    private void TestRunsPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadFilters();
        LoadTestRuns();
    }

    private void LoadFilters()
    {
        Debug.WriteLine("[TestRunsPage] Loading filters...");

        using var context = DatabaseService.CreateContext();
        var projects = context.Projects
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.Name)
            .ToList();

        projects.Insert(0, new Project { Id = 0, Name = "All Projects" });
        ProjectFilter.ItemsSource = projects;
        ProjectFilter.SelectedIndex = 0;
    }

    private void LoadTestRuns()
    {
        Debug.WriteLine("[TestRunsPage] Loading test runs...");

        using var context = DatabaseService.CreateContext();

        var query = context.TestRuns
            .Include(r => r.Project)
            .Include(r => r.TestResults)
            .AsQueryable();

        // Apply project filter
        if (ProjectFilter.SelectedItem is Project selectedProject && selectedProject.Id > 0)
        {
            query = query.Where(r => r.ProjectId == selectedProject.Id);
        }

        var runs = query
            .OrderByDescending(r => r.CreatedDate)
            .Select(r => new
            {
                r.Id,
                r.ProjectId,
                ProjectName = r.Project.Name,
                r.BuildVersion,
                r.CreatedDate,
                PassCount = r.TestResults.Count(tr => tr.Status == TestStatus.Pass),
                FailCount = r.TestResults.Count(tr => tr.Status == TestStatus.Fail),
                NotRunCount = r.TestResults.Count(tr => tr.Status == TestStatus.NotRun),
                TotalCount = r.TestResults.Count,
                CompletionPercent = r.TestResults.Count > 0
                    ? (double)r.TestResults.Count(tr => tr.Status != TestStatus.NotRun) / r.TestResults.Count * 100
                    : 0
            })
            .ToList();

        // Apply status filter
        var statusIndex = StatusFilter.SelectedIndex;
        if (statusIndex == 1) // Has Failures
        {
            runs = runs.Where(r => r.FailCount > 0).ToList();
        }
        else if (statusIndex == 2) // All Passed
        {
            runs = runs.Where(r => r.FailCount == 0 && r.NotRunCount == 0).ToList();
        }
        else if (statusIndex == 3) // In Progress
        {
            runs = runs.Where(r => r.NotRunCount > 0).ToList();
        }

        if (runs.Count > 0)
        {
            TestRunsGrid.ItemsSource = runs;
            TestRunsGrid.Visibility = Visibility.Visible;
            NoRunsText.Visibility = Visibility.Collapsed;
        }
        else
        {
            TestRunsGrid.ItemsSource = null;
            TestRunsGrid.Visibility = Visibility.Collapsed;
            NoRunsText.Visibility = Visibility.Visible;
        }

        Debug.WriteLine($"[TestRunsPage] Loaded {runs.Count} test runs.");
    }

    private void ProjectFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            LoadTestRuns();
        }
    }

    private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            LoadTestRuns();
        }
    }

    private void NewTestRun_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[TestRunsPage] New test run button clicked.");
        AppNavigationService.Instance.NavigateTo(new NewTestRunPage(), "NewTestRun");
    }

    private void TestRunsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewSelectedRun();
    }

    private void ContinueRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext != null)
        {
            var runId = (int)button.DataContext.GetType().GetProperty("Id")!.GetValue(button.DataContext)!;
            Debug.WriteLine($"[TestRunsPage] Continuing test run: {runId}");
            AppNavigationService.Instance.NavigateTo(new ExecutionPage(runId), "Execution");
        }
    }

    private void ViewRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext != null)
        {
            var runId = (int)button.DataContext.GetType().GetProperty("Id")!.GetValue(button.DataContext)!;
            Debug.WriteLine($"[TestRunsPage] Viewing test run: {runId}");
            AppNavigationService.Instance.NavigateTo(new TestRunDetailPage(runId), "TestRunDetail");
        }
    }

    private void DeleteRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext != null)
        {
            var runId = (int)button.DataContext.GetType().GetProperty("Id")!.GetValue(button.DataContext)!;

            var result = MessageBox.Show(
                "Are you sure you want to delete this test run? This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                using var context = DatabaseService.CreateContext();
                var run = context.TestRuns.Find(runId);
                if (run != null)
                {
                    context.TestRuns.Remove(run);
                    context.SaveChanges();
                    Debug.WriteLine($"[TestRunsPage] Deleted test run: {runId}");
                    LoadTestRuns();
                }
            }
        }
    }

    private void ViewSelectedRun()
    {
        if (TestRunsGrid.SelectedItem != null)
        {
            var runId = (int)TestRunsGrid.SelectedItem.GetType().GetProperty("Id")!.GetValue(TestRunsGrid.SelectedItem)!;
            Debug.WriteLine($"[TestRunsPage] Opening test run: {runId}");
            AppNavigationService.Instance.NavigateTo(new TestRunDetailPage(runId), "TestRunDetail");
        }
    }
}

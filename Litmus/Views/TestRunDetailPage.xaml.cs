using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public partial class TestRunDetailPage : Page
{
    private readonly int _testRunId;
    private List<dynamic>? _allResults;

    public TestRunDetailPage(int testRunId)
    {
        InitializeComponent();
        _testRunId = testRunId;
        Debug.WriteLine($"[TestRunDetailPage] Initializing for run: {testRunId}");
        Loaded += TestRunDetailPage_Loaded;
    }

    private void TestRunDetailPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadTestRun();
    }

    private void LoadTestRun()
    {
        Debug.WriteLine($"[TestRunDetailPage] Loading test run: {_testRunId}");

        using var context = DatabaseService.CreateContext();
        var testRun = context.TestRuns
            .Include(r => r.Project)
            .Include(r => r.TestResults)
            .ThenInclude(tr => tr.Test)
            .ThenInclude(t => t.Category)
            .FirstOrDefault(r => r.Id == _testRunId);

        if (testRun == null)
        {
            MessageBox.Show("Test run not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            AppNavigationService.Instance.GoBack();
            return;
        }

        ProjectNameText.Text = testRun.Project.Name;
        BuildVersionText.Text = testRun.BuildVersion;
        DateText.Text = testRun.CreatedDate.ToString("MMMM dd, yyyy HH:mm");

        var passed = testRun.TestResults.Count(r => r.Status == TestStatus.Pass);
        var failed = testRun.TestResults.Count(r => r.Status == TestStatus.Fail);
        var notRun = testRun.TestResults.Count(r => r.Status == TestStatus.NotRun);
        var total = testRun.TestResults.Count;
        var passRate = total > 0 ? (double)passed / total * 100 : 0;

        PassedText.Text = passed.ToString();
        FailedText.Text = failed.ToString();
        NotRunText.Text = notRun.ToString();
        PassRateText.Text = $"{passRate:F0}%";

        _allResults = testRun.TestResults
            .OrderBy(r => r.Test.Category.Name)
            .ThenBy(r => r.Test.Name)
            .Select(r => new
            {
                r.Id,
                r.TestId,
                TestName = r.Test.Name,
                CategoryName = r.Test.Category.Name,
                r.Status,
                r.Notes,
                r.ExecutedDate,
                StatusColor = r.Status == TestStatus.Pass
                    ? (Brush)FindResource("SuccessBrush")
                    : r.Status == TestStatus.Fail
                        ? (Brush)FindResource("ErrorBrush")
                        : (Brush)FindResource("ForegroundDimBrush")
            })
            .Cast<dynamic>()
            .ToList();

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (_allResults == null) return;

        var filtered = FilterComboBox.SelectedIndex switch
        {
            1 => _allResults.Where(r => r.Status == TestStatus.Pass).ToList(),
            2 => _allResults.Where(r => r.Status == TestStatus.Fail).ToList(),
            3 => _allResults.Where(r => r.Status == TestStatus.NotRun).ToList(),
            _ => _allResults
        };

        ResultsGrid.ItemsSource = filtered;
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilter();
        }
    }

    private void ContinueTesting_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[TestRunDetailPage] Continue testing: {_testRunId}");
        AppNavigationService.Instance.NavigateTo(new ExecutionPage(_testRunId), "Execution");
    }

    private void CompareRuns_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[TestRunDetailPage] Compare runs from: {_testRunId}");
        AppNavigationService.Instance.NavigateTo(new DiffPage(_testRunId), "Diff");
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem != null)
        {
            var testId = (int)ResultsGrid.SelectedItem.GetType().GetProperty("TestId")!.GetValue(ResultsGrid.SelectedItem)!;
            Debug.WriteLine($"[TestRunDetailPage] Opening test: {testId}");
            AppNavigationService.Instance.NavigateTo(new TestDetailPage(testId), "TestDetail");
        }
    }
}

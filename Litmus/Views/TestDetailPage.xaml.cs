using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public partial class TestDetailPage : Page
{
    private readonly int _testId;
    private int _categoryId;

    public TestDetailPage(int testId)
    {
        InitializeComponent();
        _testId = testId;
        Debug.WriteLine($"[TestDetailPage] Initializing for test: {testId}");
        Loaded += TestDetailPage_Loaded;
    }

    private void TestDetailPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadTest();
    }

    private void LoadTest()
    {
        Debug.WriteLine($"[TestDetailPage] Loading test: {_testId}");

        using var context = DatabaseService.CreateContext();
        var test = context.Tests
            .Include(t => t.Category)
            .ThenInclude(c => c.Project)
            .Include(t => t.TestResults)
            .ThenInclude(r => r.TestRun)
            .FirstOrDefault(t => t.Id == _testId);

        if (test == null)
        {
            MessageBox.Show("Test not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            AppNavigationService.Instance.GoBack();
            return;
        }

        _categoryId = test.CategoryId;

        TestNameText.Text = test.Name;
        ProjectNameText.Text = test.Category.Project.Name;
        CategoryNameText.Text = test.Category.Name;
        DescriptionText.Text = string.IsNullOrEmpty(test.Description) ? "(No description)" : test.Description;
        CommandText.Text = test.Command;
        ExpectedResultText.Text = string.IsNullOrEmpty(test.ExpectedResult) ? "(None specified)" : test.ExpectedResult;
        PrepStepsText.Text = string.IsNullOrEmpty(test.PrepSteps) ? "(None)" : test.PrepSteps;
        PriorityText.Text = test.Priority.ToString();

        // Load history
        var history = test.TestResults
            .OrderByDescending(r => r.TestRun.CreatedDate)
            .Select(r => new
            {
                r.TestRun.BuildVersion,
                Date = r.ExecutedDate ?? r.TestRun.CreatedDate,
                r.Status,
                r.Notes,
                StatusColor = r.Status == TestStatus.Pass
                    ? (Brush)FindResource("SuccessBrush")
                    : r.Status == TestStatus.Fail
                        ? (Brush)FindResource("ErrorBrush")
                        : (Brush)FindResource("ForegroundDimBrush"),
                NotesVisibility = string.IsNullOrEmpty(r.Notes) ? Visibility.Collapsed : Visibility.Visible
            })
            .ToList();

        if (history.Count > 0)
        {
            HistoryList.ItemsSource = history;
            NoHistoryText.Visibility = Visibility.Collapsed;
        }
        else
        {
            HistoryList.ItemsSource = null;
            NoHistoryText.Visibility = Visibility.Visible;
        }
    }

    private void EditTest_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[TestDetailPage] Edit test: {_testId}");
        AppNavigationService.Instance.NavigateTo(new TestEditPage(_categoryId, _testId), "EditTest");
    }
}

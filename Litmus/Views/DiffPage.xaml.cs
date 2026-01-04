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

public partial class DiffPage : Page
{
    private readonly int? _initialRunId;

    public DiffPage(int? initialRunId = null)
    {
        InitializeComponent();
        _initialRunId = initialRunId;
        Debug.WriteLine($"[DiffPage] Initializing with run: {initialRunId?.ToString() ?? "None"}");
        Loaded += DiffPage_Loaded;
    }

    private void DiffPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadRuns();
    }

    private void LoadRuns()
    {
        Debug.WriteLine("[DiffPage] Loading runs...");

        using var context = DatabaseService.CreateContext();
        var runs = context.TestRuns
            .Include(r => r.Project)
            .OrderByDescending(r => r.CreatedDate)
            .Select(r => new
            {
                r.Id,
                DisplayText = $"{r.Project.Name} - Build {r.BuildVersion} ({r.CreatedDate:MMM dd})"
            })
            .ToList();

        LeftRunComboBox.ItemsSource = runs;
        RightRunComboBox.ItemsSource = runs;

        if (_initialRunId.HasValue && runs.Count > 1)
        {
            // Set initial run as right (compare), and previous as left (baseline)
            var initialIndex = runs.FindIndex(r => r.Id == _initialRunId);
            if (initialIndex >= 0 && initialIndex < runs.Count - 1)
            {
                RightRunComboBox.SelectedIndex = initialIndex;
                LeftRunComboBox.SelectedIndex = initialIndex + 1;
            }
        }
    }

    private void RunSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Auto-compare when both are selected
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (LeftRunComboBox.SelectedItem == null || RightRunComboBox.SelectedItem == null)
        {
            MessageBox.Show("Please select two runs to compare.", "Selection Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var leftId = (int)LeftRunComboBox.SelectedItem.GetType().GetProperty("Id")!.GetValue(LeftRunComboBox.SelectedItem)!;
        var rightId = (int)RightRunComboBox.SelectedItem.GetType().GetProperty("Id")!.GetValue(RightRunComboBox.SelectedItem)!;

        if (leftId == rightId)
        {
            MessageBox.Show("Please select two different runs to compare.", "Same Run Selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Debug.WriteLine($"[DiffPage] Comparing run {leftId} vs {rightId}");
        PerformComparison(leftId, rightId);
    }

    private void PerformComparison(int leftRunId, int rightRunId)
    {
        using var context = DatabaseService.CreateContext();

        // Get runs for labels
        var leftRun = context.TestRuns.Include(r => r.Project).FirstOrDefault(r => r.Id == leftRunId);
        var rightRun = context.TestRuns.Include(r => r.Project).FirstOrDefault(r => r.Id == rightRunId);

        if (leftRun != null)
        {
            LeftRunLabel.Text = $"{leftRun.Project.Name} - Build {leftRun.BuildVersion}";
        }
        if (rightRun != null)
        {
            RightRunLabel.Text = $"{rightRun.Project.Name} - Build {rightRun.BuildVersion}";
        }

        var leftResultsList = context.TestResults
            .Include(r => r.Test)
            .ThenInclude(t => t.Category)
            .Where(r => r.TestRunId == leftRunId)
            .ToList();

        var leftResults = leftResultsList.ToDictionary(r => r.TestId);

        var rightResults = context.TestResults
            .Include(r => r.Test)
            .ThenInclude(t => t.Category)
            .Where(r => r.TestRunId == rightRunId)
            .ToList();

        // Calculate summary stats
        var leftPassed = leftResultsList.Count(r => r.Status == TestStatus.Pass);
        var leftFailed = leftResultsList.Count(r => r.Status == TestStatus.Fail);
        var leftBlocked = leftResultsList.Count(r => r.Status == TestStatus.Blocked);
        var leftTotal = leftResultsList.Count;
        var leftPassRate = leftTotal > 0 ? (double)leftPassed / leftTotal * 100 : 0;

        var rightPassed = rightResults.Count(r => r.Status == TestStatus.Pass);
        var rightFailed = rightResults.Count(r => r.Status == TestStatus.Fail);
        var rightBlocked = rightResults.Count(r => r.Status == TestStatus.Blocked);
        var rightTotal = rightResults.Count;
        var rightPassRate = rightTotal > 0 ? (double)rightPassed / rightTotal * 100 : 0;

        LeftPassedText.Text = leftPassed.ToString();
        LeftFailedText.Text = leftFailed.ToString();
        LeftBlockedText.Text = leftBlocked.ToString();
        LeftPassRateText.Text = $"{leftPassRate:F0}%";

        RightPassedText.Text = rightPassed.ToString();
        RightFailedText.Text = rightFailed.ToString();
        RightBlockedText.Text = rightBlocked.ToString();
        RightPassRateText.Text = $"{rightPassRate:F0}%";

        SummaryPanel.Visibility = Visibility.Visible;

        var regressions = new System.Collections.Generic.List<dynamic>();
        var fixes = new System.Collections.Generic.List<dynamic>();
        var unchanged = new System.Collections.Generic.List<dynamic>();

        foreach (var rightResult in rightResults)
        {
            if (!leftResults.TryGetValue(rightResult.TestId, out var leftResult))
            {
                // New test, skip
                continue;
            }

            var item = new
            {
                rightResult.TestId,
                TestName = rightResult.Test.Name,
                CategoryName = rightResult.Test.Category.Name,
                LeftStatus = leftResult.Status,
                RightStatus = rightResult.Status,
                StatusColor = rightResult.Status switch
                {
                    TestStatus.Pass => (Brush)FindResource("SuccessBrush"),
                    TestStatus.Fail => (Brush)FindResource("ErrorBrush"),
                    TestStatus.Blocked => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC7000")),
                    _ => (Brush)FindResource("ForegroundDimBrush")
                }
            };

            if (leftResult.Status == TestStatus.Pass && rightResult.Status == TestStatus.Fail)
            {
                regressions.Add(item);
            }
            else if (leftResult.Status == TestStatus.Fail && rightResult.Status == TestStatus.Pass)
            {
                fixes.Add(item);
            }
            else
            {
                unchanged.Add(item);
            }
        }

        // Update UI
        RegressionsCount.Text = $" ({regressions.Count})";
        FixesCount.Text = $" ({fixes.Count})";
        UnchangedCount.Text = $" ({unchanged.Count})";

        if (regressions.Count > 0)
        {
            RegressionsList.ItemsSource = regressions;
            RegressionsList.Visibility = Visibility.Visible;
            NoRegressionsText.Visibility = Visibility.Collapsed;
        }
        else
        {
            RegressionsList.ItemsSource = null;
            RegressionsList.Visibility = Visibility.Collapsed;
            NoRegressionsText.Visibility = Visibility.Visible;
        }

        if (fixes.Count > 0)
        {
            FixesList.ItemsSource = fixes;
            FixesList.Visibility = Visibility.Visible;
            NoFixesText.Visibility = Visibility.Collapsed;
        }
        else
        {
            FixesList.ItemsSource = null;
            FixesList.Visibility = Visibility.Collapsed;
            NoFixesText.Visibility = Visibility.Visible;
        }

        UnchangedList.ItemsSource = unchanged;

        Debug.WriteLine($"[DiffPage] Comparison complete: {regressions.Count} regressions, {fixes.Count} fixes, {unchanged.Count} unchanged");
    }
}

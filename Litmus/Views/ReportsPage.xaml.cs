using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Litmus.Views;

public partial class ReportsPage : Page
{
    private PieChart? _pieChart;
    private CartesianChart? _trendChart;

    public ReportsPage()
    {
        InitializeComponent();
        Debug.WriteLine("[ReportsPage] Initializing...");
        Loaded += ReportsPage_Loaded;
    }

    private void ReportsPage_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeCharts();
        LoadFilters();
        LoadReportData();
    }

    private void InitializeCharts()
    {
        _pieChart = new PieChart
        {
            MinHeight = 200,
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden
        };
        PieChartHost.Content = _pieChart;

        _trendChart = new CartesianChart
        {
            MinHeight = 200
        };
        TrendChartHost.Content = _trendChart;
    }

    private void LoadFilters()
    {
        Debug.WriteLine("[ReportsPage] Loading filters...");

        using var context = DatabaseService.CreateContext();
        var projects = context.Projects
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.Name)
            .ToList();

        projects.Insert(0, new Project { Id = 0, Name = "All Projects" });
        ProjectFilter.ItemsSource = projects;
        ProjectFilter.SelectedIndex = 0;
    }

    private void LoadReportData()
    {
        Debug.WriteLine("[ReportsPage] Loading report data...");

        using var context = DatabaseService.CreateContext();

        var selectedProjectId = 0;
        if (ProjectFilter.SelectedItem is Project selectedProject)
        {
            selectedProjectId = selectedProject.Id;
        }

        // Get latest result for each test
        var resultsQuery = context.TestResults
            .Include(r => r.Test)
            .ThenInclude(t => t.Category)
            .Include(r => r.TestRun)
            .AsQueryable();

        if (selectedProjectId > 0)
        {
            resultsQuery = resultsQuery.Where(r => r.TestRun.ProjectId == selectedProjectId);
        }

        var latestResults = resultsQuery
            .GroupBy(r => r.TestId)
            .Select(g => g.OrderByDescending(r => r.TestRun.CreatedDate).First())
            .ToList();

        // Calculate status counts
        var passedCount = latestResults.Count(r => r.Status == TestStatus.Pass);
        var failedCount = latestResults.Count(r => r.Status == TestStatus.Fail);
        var notRunCount = latestResults.Count(r => r.Status == TestStatus.NotRun);

        PassedCountText.Text = $"Passed: {passedCount}";
        FailedCountText.Text = $"Failed: {failedCount}";
        NotRunCountText.Text = $"Not Run: {notRunCount}";

        // Pie chart
        if (_pieChart != null)
        {
            var pieSeries = new List<ISeries>();
            if (passedCount > 0)
            {
                pieSeries.Add(new PieSeries<int>
                {
                    Values = new[] { passedCount },
                    Name = "Passed",
                    Fill = new SolidColorPaint(SKColor.Parse("#4EC9B0"))
                });
            }
            if (failedCount > 0)
            {
                pieSeries.Add(new PieSeries<int>
                {
                    Values = new[] { failedCount },
                    Name = "Failed",
                    Fill = new SolidColorPaint(SKColor.Parse("#F14C4C"))
                });
            }
            if (notRunCount > 0)
            {
                pieSeries.Add(new PieSeries<int>
                {
                    Values = new[] { notRunCount },
                    Name = "Not Run",
                    Fill = new SolidColorPaint(SKColor.Parse("#808080"))
                });
            }
            _pieChart.Series = pieSeries;
        }

        // Trend chart - get pass rate over time for the last 10 runs
        var runsQuery = context.TestRuns
            .Include(r => r.TestResults)
            .AsQueryable();

        if (selectedProjectId > 0)
        {
            runsQuery = runsQuery.Where(r => r.ProjectId == selectedProjectId);
        }

        var recentRuns = runsQuery
            .OrderByDescending(r => r.CreatedDate)
            .Take(10)
            .ToList()
            .OrderBy(r => r.CreatedDate)
            .Select(r => new
            {
                r.BuildVersion,
                r.CreatedDate,
                PassRate = r.TestResults.Count > 0
                    ? (double)r.TestResults.Count(tr => tr.Status == TestStatus.Pass) / r.TestResults.Count * 100
                    : 0
            })
            .ToList();

        if (_trendChart != null && recentRuns.Count > 0)
        {
            _trendChart.Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = recentRuns.Select(r => r.PassRate).ToArray(),
                    Name = "Pass Rate %",
                    Stroke = new SolidColorPaint(SKColor.Parse("#007ACC")) { StrokeThickness = 3 },
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#007ACC")) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#1E1E1E")),
                    Fill = null
                }
            };

            _trendChart.XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = recentRuns.Select(r => r.BuildVersion).ToArray(),
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#808080")),
                    TextSize = 10
                }
            };

            _trendChart.YAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 100,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#808080"))
                }
            };
        }

        // Failed tests list
        var failedTests = latestResults
            .Where(r => r.Status == TestStatus.Fail)
            .Select(r => new
            {
                r.TestId,
                TestName = r.Test.Name,
                CategoryName = r.Test.Category.Name,
                LastRunDate = r.ExecutedDate ?? r.TestRun.CreatedDate,
                r.Notes
            })
            .OrderByDescending(r => r.LastRunDate)
            .ToList();

        FailedTestsCount.Text = $" ({failedTests.Count})";

        if (failedTests.Count > 0)
        {
            FailedTestsGrid.ItemsSource = failedTests;
            FailedTestsGrid.Visibility = Visibility.Visible;
            NoFailedTestsText.Visibility = Visibility.Collapsed;
        }
        else
        {
            FailedTestsGrid.ItemsSource = null;
            FailedTestsGrid.Visibility = Visibility.Collapsed;
            NoFailedTestsText.Visibility = Visibility.Visible;
        }

        Debug.WriteLine("[ReportsPage] Report data loaded.");
    }

    private void ProjectFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            LoadReportData();
        }
    }

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ReportsPage] Export PDF clicked.");

        var selectedProjectId = 0;
        var projectName = "All Projects";
        if (ProjectFilter.SelectedItem is Project selectedProject && selectedProject.Id > 0)
        {
            selectedProjectId = selectedProject.Id;
            projectName = selectedProject.Name;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"Litmus_Report_{projectName}_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".pdf",
            Filter = "PDF Documents (.pdf)|*.pdf"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                PdfExportService.ExportReport(dialog.FileName, selectedProjectId, projectName);
                MessageBox.Show($"Report exported successfully to:\n{dialog.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReportsPage] Export failed: {ex.Message}");
                MessageBox.Show($"Failed to export report:\n{ex.Message}", "Export Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ViewTest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext != null)
        {
            var testId = (int)button.DataContext.GetType().GetProperty("TestId")!.GetValue(button.DataContext)!;
            Debug.WriteLine($"[ReportsPage] Viewing test: {testId}");
            AppNavigationService.Instance.NavigateTo(new TestDetailPage(testId), "TestDetail");
        }
    }
}

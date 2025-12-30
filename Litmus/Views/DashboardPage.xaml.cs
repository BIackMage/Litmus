using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Litmus.Data;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Debug.WriteLine("[DashboardPage] Initializing...");
        Loaded += DashboardPage_Loaded;
    }

    private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadDashboardData();
    }

    private void LoadDashboardData()
    {
        Debug.WriteLine("[DashboardPage] Loading dashboard data...");

        using var context = DatabaseService.CreateContext();

        // Get stats
        var projects = context.Projects.Where(p => !p.IsArchived).ToList();
        var totalTests = context.Tests.Count();
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var recentRuns = context.TestRuns.Count(r => r.CreatedDate >= thirtyDaysAgo);

        // Calculate overall pass rate from most recent runs
        var latestResults = context.TestResults
            .Include(r => r.TestRun)
            .GroupBy(r => r.TestId)
            .Select(g => g.OrderByDescending(r => r.TestRun.CreatedDate).First())
            .ToList();

        var passRate = latestResults.Count > 0
            ? (double)latestResults.Count(r => r.Status == Models.TestStatus.Pass) / latestResults.Count * 100
            : 0;

        // Update UI
        TotalProjectsText.Text = projects.Count.ToString();
        TotalTestsText.Text = totalTests.ToString();
        PassRateText.Text = $"{passRate:F0}%";
        RecentRunsText.Text = recentRuns.ToString();

        // Load recent projects
        var recentProjects = projects.OrderByDescending(p => p.CreatedDate).Take(5).ToList();
        if (recentProjects.Count > 0)
        {
            RecentProjectsList.ItemsSource = recentProjects;
            NoProjectsText.Visibility = Visibility.Collapsed;
        }
        else
        {
            RecentProjectsList.ItemsSource = null;
            NoProjectsText.Visibility = Visibility.Visible;
        }

        // Load recent test runs with project names
        var recentTestRuns = context.TestRuns
            .Include(r => r.Project)
            .Include(r => r.TestResults)
            .OrderByDescending(r => r.CreatedDate)
            .Take(5)
            .Select(r => new
            {
                r.Id,
                ProjectName = r.Project.Name,
                r.BuildVersion,
                r.CreatedDate,
                PassCount = r.TestResults.Count(tr => tr.Status == Models.TestStatus.Pass),
                TotalCount = r.TestResults.Count
            })
            .ToList();

        if (recentTestRuns.Count > 0)
        {
            RecentRunsList.ItemsSource = recentTestRuns;
            NoRunsText.Visibility = Visibility.Collapsed;
        }
        else
        {
            RecentRunsList.ItemsSource = null;
            NoRunsText.Visibility = Visibility.Visible;
        }

        Debug.WriteLine("[DashboardPage] Dashboard data loaded successfully.");
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[DashboardPage] New project button clicked.");
        AppNavigationService.Instance.NavigateTo(new ProjectEditPage(null), "NewProject");
    }

    private void RecentProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentProjectsList.SelectedItem is Models.Project project)
        {
            Debug.WriteLine($"[DashboardPage] Selected project: {project.Name}");
            AppNavigationService.Instance.NavigateTo(new ProjectDetailPage(project.Id), "ProjectDetail");
            RecentProjectsList.SelectedItem = null;
        }
    }

    private void RecentRunsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentRunsList.SelectedItem != null)
        {
            var runId = (int)RecentRunsList.SelectedItem.GetType().GetProperty("Id")!.GetValue(RecentRunsList.SelectedItem)!;
            Debug.WriteLine($"[DashboardPage] Selected test run: {runId}");
            AppNavigationService.Instance.NavigateTo(new TestRunDetailPage(runId), "TestRunDetail");
            RecentRunsList.SelectedItem = null;
        }
    }
}

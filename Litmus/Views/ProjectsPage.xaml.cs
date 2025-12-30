using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Litmus.Data;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public partial class ProjectsPage : Page
{
    public ProjectsPage()
    {
        InitializeComponent();
        Debug.WriteLine("[ProjectsPage] Initializing...");
        Loaded += ProjectsPage_Loaded;
    }

    private void ProjectsPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProjects();
    }

    private void LoadProjects()
    {
        Debug.WriteLine("[ProjectsPage] Loading projects...");

        using var context = DatabaseService.CreateContext();

        var showArchived = ShowArchivedCheckbox.IsChecked == true;

        var projects = context.Projects
            .Include(p => p.Categories)
            .ThenInclude(c => c.Tests)
            .Where(p => showArchived || !p.IsArchived)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.IsArchived,
                p.CreatedDate,
                CategoryCount = p.Categories.Count,
                TestCount = p.Categories.Sum(c => c.Tests.Count),
                ArchiveButtonText = p.IsArchived ? "Restore" : "Archive"
            })
            .ToList();

        if (projects.Count > 0)
        {
            ProjectsGrid.ItemsSource = projects;
            ProjectsGrid.Visibility = Visibility.Visible;
            NoProjectsText.Visibility = Visibility.Collapsed;
        }
        else
        {
            ProjectsGrid.ItemsSource = null;
            ProjectsGrid.Visibility = Visibility.Collapsed;
            NoProjectsText.Visibility = Visibility.Visible;
        }

        Debug.WriteLine($"[ProjectsPage] Loaded {projects.Count} projects.");
    }

    private void ShowArchivedCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        LoadProjects();
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ProjectsPage] New project button clicked.");
        AppNavigationService.Instance.NavigateTo(new ProjectEditPage(null), "NewProject");
    }

    private void ProjectsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProjectsGrid.SelectedItem != null)
        {
            var projectId = (int)ProjectsGrid.SelectedItem.GetType().GetProperty("Id")!.GetValue(ProjectsGrid.SelectedItem)!;
            Debug.WriteLine($"[ProjectsPage] Opening project: {projectId}");
            AppNavigationService.Instance.NavigateTo(new ProjectDetailPage(projectId), "ProjectDetail");
        }
    }

    private void EditProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext != null)
        {
            var projectId = (int)button.DataContext.GetType().GetProperty("Id")!.GetValue(button.DataContext)!;
            Debug.WriteLine($"[ProjectsPage] Editing project: {projectId}");
            AppNavigationService.Instance.NavigateTo(new ProjectEditPage(projectId), "EditProject");
        }
    }

    private void ArchiveProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext != null)
        {
            var projectId = (int)button.DataContext.GetType().GetProperty("Id")!.GetValue(button.DataContext)!;
            var isArchived = (bool)button.DataContext.GetType().GetProperty("IsArchived")!.GetValue(button.DataContext)!;

            using var context = DatabaseService.CreateContext();
            var project = context.Projects.Find(projectId);
            if (project != null)
            {
                project.IsArchived = !isArchived;
                context.SaveChanges();
                Debug.WriteLine($"[ProjectsPage] {(isArchived ? "Restored" : "Archived")} project: {projectId}");
                LoadProjects();
            }
        }
    }
}

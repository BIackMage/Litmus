using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;

namespace Litmus.Views;

public partial class ProjectEditPage : Page
{
    private readonly int? _projectId;

    public ProjectEditPage(int? projectId)
    {
        InitializeComponent();
        _projectId = projectId;
        Debug.WriteLine($"[ProjectEditPage] Initializing for project: {projectId?.ToString() ?? "New"}");
        Loaded += ProjectEditPage_Loaded;
    }

    private void ProjectEditPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_projectId.HasValue)
        {
            LoadProject();
        }
    }

    private void LoadProject()
    {
        Debug.WriteLine($"[ProjectEditPage] Loading project: {_projectId}");

        using var context = DatabaseService.CreateContext();
        var project = context.Projects.Find(_projectId);

        if (project != null)
        {
            PageTitle.Text = "Edit Project";
            NameTextBox.Text = project.Name;
            DescriptionTextBox.Text = project.Description;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ProjectEditPage] Cancel clicked.");
        AppNavigationService.Instance.GoBack();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter a project name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Debug.WriteLine($"[ProjectEditPage] Saving project: {name}");

        using var context = DatabaseService.CreateContext();

        Project project;
        if (_projectId.HasValue)
        {
            project = context.Projects.Find(_projectId)!;
            project.Name = name;
            project.Description = DescriptionTextBox.Text.Trim();
        }
        else
        {
            project = new Project
            {
                Name = name,
                Description = DescriptionTextBox.Text.Trim(),
                CreatedDate = DateTime.UtcNow
            };
            context.Projects.Add(project);
        }

        context.SaveChanges();

        Debug.WriteLine($"[ProjectEditPage] Project saved: {project.Id}");

        AppNavigationService.Instance.NavigateTo(new ProjectDetailPage(project.Id), "ProjectDetail");
    }
}

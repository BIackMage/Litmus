using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Microsoft.Win32;

namespace Litmus.Views;

public partial class ExportPage : Page
{
    public ExportPage()
    {
        InitializeComponent();
        Debug.WriteLine("[ExportPage] Initializing...");
        Loaded += ExportPage_Loaded;
    }

    private void ExportPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProjects();
    }

    private void LoadProjects()
    {
        Debug.WriteLine("[ExportPage] Loading projects...");

        using var context = DatabaseService.CreateContext();
        var projects = context.Projects
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.Name)
            .ToList();

        ProjectComboBox.ItemsSource = projects;
    }

    private void ProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectComboBox.SelectedItem is Project project)
        {
            ExportButton.IsEnabled = true;
            UpdatePreview(project.Id);
        }
        else
        {
            ExportButton.IsEnabled = false;
            PreviewTextBox.Text = string.Empty;
        }
    }

    private void UpdatePreview(int projectId)
    {
        try
        {
            var json = JsonExportService.ExportProject(
                projectId,
                IncludeDescription.IsChecked == true,
                IndentedJson.IsChecked == true);

            PreviewTextBox.Text = json;
        }
        catch (Exception ex)
        {
            PreviewTextBox.Text = $"Error generating preview: {ex.Message}";
            Debug.WriteLine($"[ExportPage] Preview error: {ex.Message}");
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectComboBox.SelectedItem is not Project project)
        {
            return;
        }

        Debug.WriteLine($"[ExportPage] Exporting project: {project.Name}");

        var dialog = new SaveFileDialog
        {
            FileName = $"{project.Name.Replace(" ", "_")}_TestPlan",
            DefaultExt = ".json",
            Filter = "JSON Files (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                JsonExportService.ExportToFile(
                    dialog.FileName,
                    project.Id,
                    IncludeDescription.IsChecked == true,
                    IndentedJson.IsChecked == true);

                ExportStatusText.Text = $"Exported successfully to: {dialog.FileName}";
                ExportStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");

                MessageBox.Show($"Test plan exported to:\n{dialog.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                Debug.WriteLine($"[ExportPage] Export successful: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                ExportStatusText.Text = $"Export failed: {ex.Message}";
                ExportStatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
                Debug.WriteLine($"[ExportPage] Export failed: {ex.Message}");
            }
        }
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(PreviewTextBox.Text))
        {
            Clipboard.SetText(PreviewTextBox.Text);
            Debug.WriteLine("[ExportPage] Copied to clipboard.");
        }
    }

    private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ExportPage] Download template clicked.");

        var dialog = new SaveFileDialog
        {
            FileName = "Litmus_TestPlan_Template",
            DefaultExt = ".json",
            Filter = "JSON Files (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                TemplateService.SaveTemplate(dialog.FileName, includeExamples: true);
                ExportStatusText.Text = $"Template saved to: {dialog.FileName}";
                ExportStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");

                MessageBox.Show(
                    $"Template saved to:\n{dialog.FileName}\n\nYou can fill this in manually or feed it to an AI to generate tests.",
                    "Template Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Debug.WriteLine($"[ExportPage] Template saved: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                ExportStatusText.Text = $"Failed to save template: {ex.Message}";
                ExportStatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
                Debug.WriteLine($"[ExportPage] Template save failed: {ex.Message}");
            }
        }
    }

    private void PreviewTemplate_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ExportPage] Preview template clicked.");
        PreviewTextBox.Text = TemplateService.GenerateTemplate(includeExamples: true);
    }
}

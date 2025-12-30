using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Litmus.Models;
using Litmus.Services;
using Microsoft.Win32;

namespace Litmus.Views;

public partial class ImportPage : Page
{
    private string? _selectedFilePath;
    private string? _appendFilePath;

    public ImportPage()
    {
        InitializeComponent();
        Debug.WriteLine("[ImportPage] Initializing...");
        Loaded += ImportPage_Loaded;
    }

    private void ImportPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProjects();
    }

    private void LoadProjects()
    {
        Debug.WriteLine("[ImportPage] Loading projects for append dropdown...");
        using var context = DatabaseService.CreateContext();
        var projects = context.Projects.OrderBy(p => p.Name).ToList();
        AppendProjectComboBox.ItemsSource = projects;

        if (projects.Any())
        {
            AppendProjectComboBox.SelectedIndex = 0;
        }
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Select Test Plan JSON File"
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedFilePath = dialog.FileName;
            SelectedFileText.Text = _selectedFilePath;
            ImportButton.IsEnabled = true;

            Debug.WriteLine($"[ImportPage] Selected file: {_selectedFilePath}");

            // Show preview
            ShowPreview(_selectedFilePath, PreviewTextBox);
        }
    }

    private void BrowseAppendFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Select Test Plan JSON File to Append"
        };

        if (dialog.ShowDialog() == true)
        {
            _appendFilePath = dialog.FileName;
            AppendSelectedFileText.Text = _appendFilePath;
            UpdateAppendButtonState();

            Debug.WriteLine($"[ImportPage] Selected append file: {_appendFilePath}");

            // Show preview
            ShowPreview(_appendFilePath, AppendPreviewTextBox);
        }
    }

    private void UpdateAppendButtonState()
    {
        AppendButton.IsEnabled = !string.IsNullOrEmpty(_appendFilePath) &&
                                  AppendProjectComboBox.SelectedItem != null;
    }

    private void ShowPreview(string filePath, TextBox previewBox)
    {
        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var formattedJson = JsonSerializer.Serialize(
                JsonDocument.Parse(jsonContent).RootElement,
                new JsonSerializerOptions { WriteIndented = true });
            previewBox.Text = formattedJson;
        }
        catch (Exception ex)
        {
            previewBox.Text = $"Error reading file: {ex.Message}";
            Debug.WriteLine($"[ImportPage] Error reading file: {ex.Message}");
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath))
        {
            return;
        }

        Debug.WriteLine($"[ImportPage] Importing from: {_selectedFilePath}");

        try
        {
            var result = JsonImportService.ImportFromFile(
                _selectedFilePath,
                OverwriteExisting.IsChecked == true,
                MergeCategories.IsChecked == true);

            ImportStatusText.Text = result.Message;
            ImportStatusText.Foreground = result.Success
                ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
                : (System.Windows.Media.Brush)FindResource("ErrorBrush");

            if (result.Success)
            {
                Debug.WriteLine($"[ImportPage] Import successful: {result.Message}");
                MessageBox.Show(result.Message, "Import Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh projects list
                LoadProjects();

                // Navigate to the imported project
                if (result.ProjectId.HasValue)
                {
                    AppNavigationService.Instance.NavigateTo(
                        new ProjectDetailPage(result.ProjectId.Value), "ProjectDetail");
                }
            }
        }
        catch (Exception ex)
        {
            ImportStatusText.Text = $"Import failed: {ex.Message}";
            ImportStatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            Debug.WriteLine($"[ImportPage] Import failed: {ex.Message}");
        }
    }

    private void Append_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_appendFilePath) || AppendProjectComboBox.SelectedItem is not Project selectedProject)
        {
            return;
        }

        Debug.WriteLine($"[ImportPage] Appending to project {selectedProject.Name} from: {_appendFilePath}");

        try
        {
            var result = JsonImportService.AppendToProject(
                _appendFilePath,
                selectedProject.Id,
                AppendMergeCategories.IsChecked == true,
                AppendSkipDuplicates.IsChecked == true);

            AppendStatusText.Text = result.Message;
            AppendStatusText.Foreground = result.Success
                ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
                : (System.Windows.Media.Brush)FindResource("ErrorBrush");

            if (result.Success)
            {
                Debug.WriteLine($"[ImportPage] Append successful: {result.Message}");
                MessageBox.Show(result.Message, "Tests Added",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Navigate to the project
                AppNavigationService.Instance.NavigateTo(
                    new ProjectDetailPage(selectedProject.Id), "ProjectDetail");
            }
        }
        catch (Exception ex)
        {
            AppendStatusText.Text = $"Append failed: {ex.Message}";
            AppendStatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            Debug.WriteLine($"[ImportPage] Append failed: {ex.Message}");
        }
    }
}

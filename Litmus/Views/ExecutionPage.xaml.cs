using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Litmus.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace Litmus.Views;

public partial class ExecutionPage : Page
{
    private readonly int _testRunId;
    private List<TestResult> _results = new();
    private int _currentIndex;
    private TestResult? _currentResult;

    public ExecutionPage(int testRunId)
    {
        InitializeComponent();
        _testRunId = testRunId;
        Debug.WriteLine($"[ExecutionPage] Initializing for run: {testRunId}");
        Loaded += ExecutionPage_Loaded;
    }

    private void ExecutionPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadTestRun();
            LoadFailureTemplates();
            this.Focus();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExecutionPage] Error loading: {ex.Message}");
            MessageBox.Show($"Error loading test run: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadFailureTemplates()
    {
        using var context = DatabaseService.CreateContext();
        var templates = context.FailureTemplates
            .OrderBy(t => t.SortOrder)
            .ToList();

        templates.Insert(0, new FailureTemplate { Id = 0, Name = "Quick fail reason..." });
        FailTemplateComboBox.ItemsSource = templates;
        FailTemplateComboBox.SelectedIndex = 0;
    }

    private void LoadTestRun()
    {
        Debug.WriteLine($"[ExecutionPage] Loading test run: {_testRunId}");

        using var context = DatabaseService.CreateContext();
        var testRun = context.TestRuns
            .Include(r => r.Project)
            .Include(r => r.TestResults)
            .ThenInclude(tr => tr.Test)
            .ThenInclude(t => t.Category)
            .Include(r => r.TestResults)
            .ThenInclude(tr => tr.Attachments)
            .FirstOrDefault(r => r.Id == _testRunId);

        if (testRun == null)
        {
            MessageBox.Show("Test run not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            AppNavigationService.Instance.GoBack();
            return;
        }

        ProjectNameText.Text = testRun.Project.Name;
        BuildVersionText.Text = testRun.BuildVersion;

        // Sort by priority (Critical first), then by category, then by name
        _results = testRun.TestResults
            .OrderByDescending(r => r.Test.Priority)
            .ThenBy(r => r.Test.Category.Name)
            .ThenBy(r => r.Test.Name)
            .ToList();

        TotalTestsText.Text = _results.Count.ToString();

        // Find first not-run test or start at beginning
        _currentIndex = _results.FindIndex(r => r.Status == TestStatus.NotRun);
        if (_currentIndex < 0) _currentIndex = 0;

        DisplayCurrentTest();
        UpdateProgress();
    }

    private void DisplayCurrentTest()
    {
        if (_results.Count == 0 || _currentIndex >= _results.Count)
        {
            return;
        }

        // Save current notes before moving
        SaveCurrentNotes();

        _currentResult = _results[_currentIndex];
        var test = _currentResult.Test;

        CurrentTestIndexText.Text = (_currentIndex + 1).ToString();
        TestNameText.Text = test.Name;
        CategoryText.Text = test.Category.Name;
        DescriptionText.Text = string.IsNullOrEmpty(test.Description) ? "(No description)" : test.Description;
        CommandText.Text = test.Command;
        ExpectedResultText.Text = string.IsNullOrEmpty(test.ExpectedResult) ? "(None specified)" : test.ExpectedResult;
        PrepStepsText.Text = string.IsNullOrEmpty(test.PrepSteps) ? "(None)" : test.PrepSteps;
        NotesTextBox.Text = _currentResult.Notes;

        // Priority badge
        PriorityText.Text = test.Priority.ToString().ToUpper();
        PriorityBadge.Background = test.Priority switch
        {
            Priority.Critical => (Brush)FindResource("ErrorBrush"),
            Priority.High => (Brush)FindResource("WarningBrush"),
            Priority.Medium => (Brush)FindResource("AccentBrush"),
            _ => (Brush)FindResource("ForegroundDimBrush")
        };

        // Current status highlighting
        UpdateStatusButtons();

        // Load previous result
        LoadPreviousResult(test.Id);

        // Load attachments
        AttachmentsList.ItemsSource = _currentResult.Attachments.ToList();

        // Navigation buttons
        PrevButton.IsEnabled = _currentIndex > 0;

        Debug.WriteLine($"[ExecutionPage] Displaying test {_currentIndex + 1}: {test.Name}");
    }

    private void LoadPreviousResult(int testId)
    {
        using var context = DatabaseService.CreateContext();

        var previousResult = context.TestResults
            .Include(r => r.TestRun)
            .Where(r => r.TestId == testId && r.TestRunId != _testRunId)
            .OrderByDescending(r => r.TestRun.CreatedDate)
            .FirstOrDefault();

        if (previousResult != null)
        {
            PreviousStatusText.Text = $"{previousResult.Status} (Build {previousResult.TestRun.BuildVersion})";
            PreviousStatusIndicator.Fill = previousResult.Status switch
            {
                TestStatus.Pass => (Brush)FindResource("SuccessBrush"),
                TestStatus.Fail => (Brush)FindResource("ErrorBrush"),
                TestStatus.Blocked => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CC7000")),
                _ => (Brush)FindResource("ForegroundDimBrush")
            };
        }
        else
        {
            PreviousStatusText.Text = "No previous result";
            PreviousStatusIndicator.Fill = (Brush)FindResource("ForegroundDimBrush");
        }
    }

    private void UpdateStatusButtons()
    {
        if (_currentResult == null) return;

        PassButton.BorderThickness = _currentResult.Status == TestStatus.Pass
            ? new Thickness(3) : new Thickness(1);
        FailButton.BorderThickness = _currentResult.Status == TestStatus.Fail
            ? new Thickness(3) : new Thickness(1);
        BlockedButton.BorderThickness = _currentResult.Status == TestStatus.Blocked
            ? new Thickness(3) : new Thickness(1);
    }

    private void UpdateProgress()
    {
        var passed = _results.Count(r => r.Status == TestStatus.Pass);
        var failed = _results.Count(r => r.Status == TestStatus.Fail);
        var blocked = _results.Count(r => r.Status == TestStatus.Blocked);
        var notRun = _results.Count(r => r.Status == TestStatus.NotRun);

        PassedCountText.Text = passed.ToString();
        FailedCountText.Text = failed.ToString();
        BlockedCountText.Text = blocked.ToString();
        NotRunCountText.Text = notRun.ToString();

        var completed = passed + failed + blocked;
        ProgressBar.Maximum = _results.Count;
        ProgressBar.Value = completed;
    }

    private void SaveCurrentNotes()
    {
        if (_currentResult != null && NotesTextBox.Text != _currentResult.Notes)
        {
            _currentResult.Notes = NotesTextBox.Text;
            SaveResult(_currentResult);
        }
    }

    private void SaveResult(TestResult result)
    {
        using var context = DatabaseService.CreateContext();
        var dbResult = context.TestResults.Find(result.Id);
        if (dbResult != null)
        {
            dbResult.Status = result.Status;
            dbResult.Notes = result.Notes;
            dbResult.ExecutedDate = DateTime.UtcNow;
            context.SaveChanges();
            Debug.WriteLine($"[ExecutionPage] Saved result for test {result.TestId}: {result.Status}");
        }
    }

    private void MarkAs(TestStatus status)
    {
        if (_currentResult == null) return;

        _currentResult.Status = status;
        _currentResult.Notes = NotesTextBox.Text;
        _currentResult.ExecutedDate = DateTime.UtcNow;

        SaveResult(_currentResult);
        UpdateStatusButtons();
        UpdateProgress();
    }

    private void Page_KeyDown(object sender, KeyEventArgs e)
    {
        // Don't handle if focus is in a text box
        if (e.OriginalSource is TextBox) return;

        switch (e.Key)
        {
            case Key.P:
                MarkAs(TestStatus.Pass);
                e.Handled = true;
                break;
            case Key.F:
                MarkAs(TestStatus.Fail);
                e.Handled = true;
                break;
            case Key.B:
                MarkAs(TestStatus.Blocked);
                e.Handled = true;
                break;
            case Key.Left:
                GoToPrevious();
                e.Handled = true;
                break;
            case Key.Right:
                SaveAndGoNext();
                e.Handled = true;
                break;
            case Key.S when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                CaptureScreenshot();
                e.Handled = true;
                break;
            case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                SaveCurrentNotes();
                e.Handled = true;
                break;
            case Key.S when Keyboard.Modifiers == ModifierKeys.None:
                SkipTest();
                e.Handled = true;
                break;
            case Key.N:
                JumpToNextNotRun();
                e.Handled = true;
                break;
        }
    }

    private void Pass_Click(object sender, RoutedEventArgs e) => MarkAs(TestStatus.Pass);
    private void Fail_Click(object sender, RoutedEventArgs e) => MarkAs(TestStatus.Fail);
    private void Blocked_Click(object sender, RoutedEventArgs e) => MarkAs(TestStatus.Blocked);
    private void Skip_Click(object sender, RoutedEventArgs e) => SkipTest();

    private void FailTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FailTemplateComboBox.SelectedItem is FailureTemplate template && template.Id > 0)
        {
            NotesTextBox.Text = template.Description;
            MarkAs(TestStatus.Fail);
            FailTemplateComboBox.SelectedIndex = 0;
        }
    }

    private void GoToPrevious()
    {
        if (_currentIndex > 0)
        {
            SaveCurrentNotes();
            _currentIndex--;
            DisplayCurrentTest();
        }
    }

    private void SaveAndGoNext()
    {
        SaveCurrentNotes();
        if (_currentIndex < _results.Count - 1)
        {
            _currentIndex++;
            DisplayCurrentTest();
        }
        else
        {
            // At the end
            var notRun = _results.Count(r => r.Status == TestStatus.NotRun);
            if (notRun == 0)
            {
                var result = MessageBox.Show(
                    "All tests completed! Would you like to view the results?",
                    "Testing Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    AppNavigationService.Instance.NavigateTo(new TestRunDetailPage(_testRunId), "TestRunDetail");
                }
            }
            else
            {
                MessageBox.Show(
                    $"End of list. {notRun} test(s) still not run.\nUse 'Jump to' or Previous to go back.",
                    "End of Tests",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    private void SkipTest()
    {
        // Save notes but don't change status - just move to next test
        SaveCurrentNotes();
        if (_currentIndex < _results.Count - 1)
        {
            _currentIndex++;
            DisplayCurrentTest();
            Debug.WriteLine($"[ExecutionPage] Skipped to test {_currentIndex + 1}");
        }
        else
        {
            // At the end - find first not-run test to loop back
            var firstNotRun = _results.FindIndex(r => r.Status == TestStatus.NotRun);
            if (firstNotRun >= 0 && firstNotRun != _currentIndex)
            {
                _currentIndex = firstNotRun;
                DisplayCurrentTest();
                Debug.WriteLine($"[ExecutionPage] Looped back to first not-run test: {_currentIndex + 1}");
            }
            else
            {
                var notRun = _results.Count(r => r.Status == TestStatus.NotRun);
                MessageBox.Show(
                    notRun > 0
                        ? $"End of list. {notRun} test(s) still not run."
                        : "All tests have been marked!",
                    "End of Tests",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    private void Previous_Click(object sender, RoutedEventArgs e) => GoToPrevious();
    private void SaveAndNext_Click(object sender, RoutedEventArgs e) => SaveAndGoNext();
    private void JumpToNotRun_Click(object sender, RoutedEventArgs e) => JumpToNextNotRun();

    private void JumpToNextNotRun()
    {
        SaveCurrentNotes();

        // Find next not-run test starting from current position
        var nextNotRun = -1;
        for (int i = _currentIndex + 1; i < _results.Count; i++)
        {
            if (_results[i].Status == TestStatus.NotRun)
            {
                nextNotRun = i;
                break;
            }
        }

        // If not found after current, wrap around from beginning
        if (nextNotRun < 0)
        {
            for (int i = 0; i < _currentIndex; i++)
            {
                if (_results[i].Status == TestStatus.NotRun)
                {
                    nextNotRun = i;
                    break;
                }
            }
        }

        if (nextNotRun >= 0)
        {
            _currentIndex = nextNotRun;
            DisplayCurrentTest();
            Debug.WriteLine($"[ExecutionPage] Jumped to not-run test: {_currentIndex + 1}");
        }
        else
        {
            MessageBox.Show("No more tests marked as 'Not Run'.", "All Tests Marked",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void JumpTo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            JumpToTest();
        }
    }

    private void JumpTo_Click(object sender, RoutedEventArgs e) => JumpToTest();

    private void JumpToTest()
    {
        if (int.TryParse(JumpToTextBox.Text, out int index) && index >= 1 && index <= _results.Count)
        {
            SaveCurrentNotes();
            _currentIndex = index - 1;
            DisplayCurrentTest();
        }
        JumpToTextBox.Clear();
    }

    private void CopyCommand_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(CommandText.Text))
        {
            Clipboard.SetText(CommandText.Text);
            Debug.WriteLine("[ExecutionPage] Command copied to clipboard.");
        }
    }

    private void AddAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult == null) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All Files|*.*",
            Title = "Select Attachment"
        };

        if (dialog.ShowDialog() == true)
        {
            Debug.WriteLine($"[ExecutionPage] Adding attachment: {dialog.FileName}");

            var fileName = Path.GetFileName(dialog.FileName);
            var destPath = Path.Combine(
                DatabaseService.AttachmentsPath,
                $"{_testRunId}_{_currentResult.TestId}_{DateTime.Now.Ticks}_{fileName}");

            File.Copy(dialog.FileName, destPath);

            using var context = DatabaseService.CreateContext();
            var attachment = new Attachment
            {
                TestResultId = _currentResult.Id,
                FileName = fileName,
                FilePath = destPath,
                ContentType = GetContentType(fileName),
                FileSize = new FileInfo(destPath).Length,
                CreatedDate = DateTime.UtcNow
            };

            context.Attachments.Add(attachment);
            context.SaveChanges();

            _currentResult.Attachments.Add(attachment);
            AttachmentsList.ItemsSource = _currentResult.Attachments.ToList();
        }
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is Attachment attachment)
        {
            using var context = DatabaseService.CreateContext();
            var dbAttachment = context.Attachments.Find(attachment.Id);
            if (dbAttachment != null)
            {
                if (File.Exists(dbAttachment.FilePath))
                {
                    File.Delete(dbAttachment.FilePath);
                }
                context.Attachments.Remove(dbAttachment);
                context.SaveChanges();

                _currentResult?.Attachments.Remove(attachment);
                AttachmentsList.ItemsSource = _currentResult?.Attachments.ToList();
            }
        }
    }

    private string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLower();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentNotes();
        AppNavigationService.Instance.NavigateTo(new TestRunDetailPage(_testRunId), "TestRunDetail");
    }

    private void CaptureScreenshot_Click(object sender, RoutedEventArgs e) => CaptureScreenshot();

    private void CaptureScreenshot()
    {
        if (_currentResult == null) return;

        try
        {
            Debug.WriteLine("[ExecutionPage] Capturing screenshot...");

            // Get virtual screen bounds (all monitors)
            var screenWidth = (int)SystemParameters.VirtualScreenWidth;
            var screenHeight = (int)SystemParameters.VirtualScreenHeight;
            var screenLeft = (int)SystemParameters.VirtualScreenLeft;
            var screenTop = (int)SystemParameters.VirtualScreenTop;

            using var bitmap = new System.Drawing.Bitmap(screenWidth, screenHeight);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(screenLeft, screenTop, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));

            var fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var destPath = Path.Combine(
                DatabaseService.AttachmentsPath,
                $"{_testRunId}_{_currentResult.TestId}_{DateTime.Now.Ticks}_{fileName}");

            bitmap.Save(destPath, System.Drawing.Imaging.ImageFormat.Png);

            using var context = DatabaseService.CreateContext();
            var attachment = new Attachment
            {
                TestResultId = _currentResult.Id,
                FileName = fileName,
                FilePath = destPath,
                ContentType = "image/png",
                FileSize = new FileInfo(destPath).Length,
                CreatedDate = DateTime.UtcNow
            };

            context.Attachments.Add(attachment);
            context.SaveChanges();

            _currentResult.Attachments.Add(attachment);
            AttachmentsList.ItemsSource = _currentResult.Attachments.ToList();

            Debug.WriteLine($"[ExecutionPage] Screenshot captured: {fileName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExecutionPage] Screenshot capture failed: {ex.Message}");
            MessageBox.Show($"Failed to capture screenshot: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditTest_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult?.Test == null) return;

        Debug.WriteLine($"[ExecutionPage] Opening edit dialog for test: {_currentResult.Test.Name}");

        var editWindow = new TestEditWindow(_currentResult.Test);
        editWindow.Owner = Window.GetWindow(this);
        var result = editWindow.ShowDialog();

        if (result == true && editWindow.TestUpdated)
        {
            // Refresh the display with updated test data
            DisplayCurrentTest();
            Debug.WriteLine("[ExecutionPage] Test updated, display refreshed.");
        }
    }

    private void MoveToEnd_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult?.Test == null) return;

        var test = _currentResult.Test;
        Debug.WriteLine($"[ExecutionPage] Moving test to end: {test.Name}");

        using var context = DatabaseService.CreateContext();

        // Get max sort order across ALL tests in the project (not just category)
        var maxSortOrder = context.Tests
            .Where(t => t.Category.ProjectId == test.Category.ProjectId)
            .Max(t => (int?)t.SortOrder) ?? 0;

        // Update the test's sort order to be at the end
        var dbTest = context.Tests.Find(test.Id);
        if (dbTest != null)
        {
            dbTest.SortOrder = maxSortOrder + 1000; // Add buffer for future ordering
            context.SaveChanges();
            test.SortOrder = dbTest.SortOrder;
        }

        // Move this result to the end of our list
        var currentResult = _currentResult;
        _results.Remove(currentResult);
        _results.Add(currentResult);

        // Stay at current index (which now shows the next test) or adjust if at end
        if (_currentIndex >= _results.Count)
        {
            _currentIndex = _results.Count - 1;
        }

        DisplayCurrentTest();
        UpdateProgress();

        Debug.WriteLine($"[ExecutionPage] Test moved to end. New position: {_results.Count}");
    }
}

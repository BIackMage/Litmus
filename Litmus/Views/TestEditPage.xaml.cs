using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public partial class TestEditPage : Page
{
    private readonly int _categoryId;
    private readonly int? _testId;
    private int _projectId;

    public TestEditPage(int categoryId, int? testId)
    {
        InitializeComponent();
        _categoryId = categoryId;
        _testId = testId;
        Debug.WriteLine($"[TestEditPage] Category: {categoryId}, Test: {testId?.ToString() ?? "New"}");
        Loaded += TestEditPage_Loaded;
    }

    private void TestEditPage_Loaded(object sender, RoutedEventArgs e)
    {
        using var context = DatabaseService.CreateContext();
        var category = context.Categories.Find(_categoryId);
        if (category != null)
        {
            _projectId = category.ProjectId;
        }

        if (_testId.HasValue)
        {
            LoadTest();
        }
    }

    private void LoadTest()
    {
        using var context = DatabaseService.CreateContext();
        var test = context.Tests.Find(_testId);

        if (test != null)
        {
            PageTitle.Text = "Edit Test";
            NameTextBox.Text = test.Name;
            DescriptionTextBox.Text = test.Description;
            CommandTextBox.Text = test.Command;
            ExpectedResultTextBox.Text = test.ExpectedResult;
            PrepStepsTextBox.Text = test.PrepSteps;
            PriorityComboBox.SelectedIndex = (int)test.Priority;
            IsAutomatedCheckBox.IsChecked = test.IsAutomated;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        AppNavigationService.Instance.GoBack();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter a test name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var context = DatabaseService.CreateContext();

        Test test;
        if (_testId.HasValue)
        {
            test = context.Tests.Find(_testId)!;
        }
        else
        {
            var maxOrder = context.Tests
                .Where(t => t.CategoryId == _categoryId)
                .Max(t => (int?)t.SortOrder) ?? 0;

            test = new Test
            {
                CategoryId = _categoryId,
                SortOrder = maxOrder + 1
            };
            context.Tests.Add(test);
        }

        test.Name = name;
        test.Description = DescriptionTextBox.Text.Trim();
        test.Command = CommandTextBox.Text.Trim();
        test.ExpectedResult = ExpectedResultTextBox.Text.Trim();
        test.PrepSteps = PrepStepsTextBox.Text.Trim();
        test.Priority = (Priority)((PriorityComboBox.SelectedItem as ComboBoxItem)?.Tag ?? 1);
        test.IsAutomated = IsAutomatedCheckBox.IsChecked ?? false;

        context.SaveChanges();
        Debug.WriteLine($"[TestEditPage] Test saved: {test.Id}");

        AppNavigationService.Instance.NavigateTo(new ProjectDetailPage(_projectId), "ProjectDetail");
    }
}

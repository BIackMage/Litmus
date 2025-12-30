using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public partial class ProjectDetailPage : Page
{
    private readonly int _projectId;
    private int? _selectedCategoryId;
    private int? _selectedTestId;

    public ProjectDetailPage(int projectId)
    {
        InitializeComponent();
        _projectId = projectId;
        Debug.WriteLine($"[ProjectDetailPage] Initializing for project: {projectId}");
        Loaded += ProjectDetailPage_Loaded;
    }

    private void ProjectDetailPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProject();
    }

    private void LoadProject()
    {
        Debug.WriteLine($"[ProjectDetailPage] Loading project: {_projectId}");

        using var context = DatabaseService.CreateContext();
        var project = context.Projects
            .Include(p => p.Categories)
            .ThenInclude(c => c.Tests)
            .FirstOrDefault(p => p.Id == _projectId);

        if (project == null)
        {
            MessageBox.Show("Project not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            AppNavigationService.Instance.GoBack();
            return;
        }

        ProjectNameText.Text = project.Name;
        ProjectDescriptionText.Text = project.Description;

        var categories = project.Categories
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                TestCount = c.Tests.Count,
                Tests = c.Tests.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToList()
            })
            .ToList();

        CategoriesTree.ItemsSource = categories;
    }

    private void CategoriesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue == null)
        {
            return;
        }

        var type = e.NewValue.GetType();

        if (type.GetProperty("TestCount") != null) // It's a category
        {
            _selectedCategoryId = (int)type.GetProperty("Id")!.GetValue(e.NewValue)!;
            _selectedTestId = null;
            ShowCategoryTests(_selectedCategoryId.Value);
        }
        else if (e.NewValue is Test test)
        {
            _selectedTestId = test.Id;
            ShowTestDetails(test);
        }
    }

    private void ShowCategoryTests(int categoryId)
    {
        Debug.WriteLine($"[ProjectDetailPage] Showing tests for category: {categoryId}");

        using var context = DatabaseService.CreateContext();
        var category = context.Categories
            .Include(c => c.Tests)
            .FirstOrDefault(c => c.Id == categoryId);

        if (category != null)
        {
            DetailTitleText.Text = category.Name;
            AddTestButton.Visibility = Visibility.Visible;
            EditCategoryButton.Visibility = Visibility.Visible;
            DeleteCategoryButton.Visibility = Visibility.Visible;
            DeleteSelectedTestsButton.Visibility = Visibility.Collapsed;
            TestsGrid.ItemsSource = category.Tests.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToList();
            TestsGrid.Visibility = Visibility.Visible;
            TestDetailView.Visibility = Visibility.Collapsed;
            PlaceholderText.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowTestDetails(Test test)
    {
        Debug.WriteLine($"[ProjectDetailPage] Showing test details: {test.Id}");

        DetailTitleText.Text = test.Name;
        AddTestButton.Visibility = Visibility.Collapsed;
        EditCategoryButton.Visibility = Visibility.Collapsed;
        DeleteCategoryButton.Visibility = Visibility.Collapsed;
        DeleteSelectedTestsButton.Visibility = Visibility.Collapsed;
        TestCommandText.Text = test.Command;
        TestExpectedText.Text = test.ExpectedResult;
        TestPrepText.Text = string.IsNullOrEmpty(test.PrepSteps) ? "(None)" : test.PrepSteps;

        TestsGrid.Visibility = Visibility.Collapsed;
        TestDetailView.Visibility = Visibility.Visible;
        PlaceholderText.Visibility = Visibility.Collapsed;
    }

    private void EditProject_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ProjectDetailPage] Edit project clicked.");
        AppNavigationService.Instance.NavigateTo(new ProjectEditPage(_projectId), "EditProject");
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ProjectDetailPage] Add category clicked.");
        AppNavigationService.Instance.NavigateTo(new CategoryEditPage(_projectId, null), "NewCategory");
    }

    private void AddTest_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCategoryId.HasValue)
        {
            Debug.WriteLine($"[ProjectDetailPage] Add test to category: {_selectedCategoryId}");
            AppNavigationService.Instance.NavigateTo(new TestEditPage(_selectedCategoryId.Value, null), "NewTest");
        }
    }

    private void StartTestRun_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[ProjectDetailPage] Start test run for project: {_projectId}");
        AppNavigationService.Instance.NavigateTo(new NewTestRunPage(_projectId), "NewTestRun");
    }

    private void TestsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TestsGrid.SelectedItem is Test test)
        {
            _selectedTestId = test.Id;
            ShowTestDetails(test);
        }
    }

    private void EditTest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is Test test)
        {
            Debug.WriteLine($"[ProjectDetailPage] Edit test: {test.Id}");
            AppNavigationService.Instance.NavigateTo(new TestEditPage(test.CategoryId, test.Id), "EditTest");
        }
    }

    private void DeleteTest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is Test test)
        {
            DeleteTest(test.Id);
        }
    }

    private void EditSelectedTest_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTestId.HasValue && _selectedCategoryId.HasValue)
        {
            AppNavigationService.Instance.NavigateTo(
                new TestEditPage(_selectedCategoryId.Value, _selectedTestId.Value), "EditTest");
        }
    }

    private void DeleteSelectedTest_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTestId.HasValue)
        {
            DeleteTest(_selectedTestId.Value);
        }
    }

    private void DeleteTest(int testId)
    {
        var result = MessageBox.Show(
            "Are you sure you want to delete this test?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            using var context = DatabaseService.CreateContext();
            var test = context.Tests.Find(testId);
            if (test != null)
            {
                context.Tests.Remove(test);
                context.SaveChanges();
                Debug.WriteLine($"[ProjectDetailPage] Deleted test: {testId}");
                LoadProject();
            }
        }
    }

    private void TestsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedCount = TestsGrid.SelectedItems.Count;
        DeleteSelectedTestsButton.Visibility = selectedCount > 1
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (selectedCount > 1)
        {
            DeleteSelectedTestsButton.Content = $"Delete Selected ({selectedCount})";
        }
    }

    private void DeleteSelectedTests_Click(object sender, RoutedEventArgs e)
    {
        var selectedTests = TestsGrid.SelectedItems.Cast<Test>().ToList();
        if (selectedTests.Count == 0) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete {selectedTests.Count} selected tests?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            using var context = DatabaseService.CreateContext();
            var testIds = selectedTests.Select(t => t.Id).ToList();
            var testsToDelete = context.Tests.Where(t => testIds.Contains(t.Id)).ToList();
            context.Tests.RemoveRange(testsToDelete);
            context.SaveChanges();
            Debug.WriteLine($"[ProjectDetailPage] Deleted {testsToDelete.Count} tests");

            if (_selectedCategoryId.HasValue)
            {
                ShowCategoryTests(_selectedCategoryId.Value);
            }
            LoadProject();
        }
    }

    private void EditCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCategoryId.HasValue)
        {
            Debug.WriteLine($"[ProjectDetailPage] Edit category: {_selectedCategoryId}");
            AppNavigationService.Instance.NavigateTo(
                new CategoryEditPage(_projectId, _selectedCategoryId.Value), "EditCategory");
        }
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (!_selectedCategoryId.HasValue) return;

        using var context = DatabaseService.CreateContext();
        var category = context.Categories
            .Include(c => c.Tests)
            .FirstOrDefault(c => c.Id == _selectedCategoryId.Value);

        if (category == null) return;

        var testCount = category.Tests.Count;
        var message = testCount > 0
            ? $"Are you sure you want to delete the category '{category.Name}' and all {testCount} tests in it?"
            : $"Are you sure you want to delete the category '{category.Name}'?";

        var result = MessageBox.Show(
            message,
            "Confirm Delete Category",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            context.Categories.Remove(category);
            context.SaveChanges();
            Debug.WriteLine($"[ProjectDetailPage] Deleted category: {_selectedCategoryId} with {testCount} tests");

            _selectedCategoryId = null;
            EditCategoryButton.Visibility = Visibility.Collapsed;
            DeleteCategoryButton.Visibility = Visibility.Collapsed;
            DeleteSelectedTestsButton.Visibility = Visibility.Collapsed;
            AddTestButton.Visibility = Visibility.Collapsed;
            TestsGrid.Visibility = Visibility.Collapsed;
            PlaceholderText.Visibility = Visibility.Visible;
            DetailTitleText.Text = "Select a category or test";

            LoadProject();
        }
    }
}

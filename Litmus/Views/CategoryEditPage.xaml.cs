using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Litmus.Data;
using Litmus.Models;
using Litmus.Services;

namespace Litmus.Views;

public partial class CategoryEditPage : Page
{
    private readonly int _projectId;
    private readonly int? _categoryId;

    public CategoryEditPage(int projectId, int? categoryId)
    {
        InitializeComponent();
        _projectId = projectId;
        _categoryId = categoryId;
        Debug.WriteLine($"[CategoryEditPage] Project: {projectId}, Category: {categoryId?.ToString() ?? "New"}");
        Loaded += CategoryEditPage_Loaded;
    }

    private void CategoryEditPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_categoryId.HasValue)
        {
            LoadCategory();
        }
    }

    private void LoadCategory()
    {
        using var context = DatabaseService.CreateContext();
        var category = context.Categories.Find(_categoryId);

        if (category != null)
        {
            PageTitle.Text = "Edit Category";
            NameTextBox.Text = category.Name;
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
            MessageBox.Show("Please enter a category name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var context = DatabaseService.CreateContext();

        Category category;
        if (_categoryId.HasValue)
        {
            category = context.Categories.Find(_categoryId)!;
            category.Name = name;
        }
        else
        {
            var maxOrder = context.Categories
                .Where(c => c.ProjectId == _projectId)
                .Max(c => (int?)c.SortOrder) ?? 0;

            category = new Category
            {
                ProjectId = _projectId,
                Name = name,
                SortOrder = maxOrder + 1
            };
            context.Categories.Add(category);
        }

        context.SaveChanges();
        Debug.WriteLine($"[CategoryEditPage] Category saved: {category.Id}");

        AppNavigationService.Instance.NavigateTo(new ProjectDetailPage(_projectId), "ProjectDetail");
    }
}

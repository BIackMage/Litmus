using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Litmus.Data;
using Litmus.Services;
using Microsoft.EntityFrameworkCore;

namespace Litmus.Views;

public partial class SearchResultsPage : Page
{
    private readonly string _searchQuery;

    public SearchResultsPage(string searchQuery)
    {
        InitializeComponent();
        _searchQuery = searchQuery;
        Debug.WriteLine($"[SearchResultsPage] Searching for: {searchQuery}");
        Loaded += SearchResultsPage_Loaded;
    }

    private void SearchResultsPage_Loaded(object sender, RoutedEventArgs e)
    {
        SearchQueryText.Text = $"\"{_searchQuery}\"";
        PerformSearch();
    }

    private void PerformSearch()
    {
        Debug.WriteLine($"[SearchResultsPage] Performing search...");

        using var context = DatabaseService.CreateContext();

        var query = _searchQuery.ToLowerInvariant();

        var results = context.Tests
            .Include(t => t.Category)
            .ThenInclude(c => c.Project)
            .Where(t => !t.Category.Project.IsArchived)
            .Where(t => t.Name.ToLower().Contains(query) ||
                        t.Description.ToLower().Contains(query) ||
                        t.Command.ToLower().Contains(query))
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                t.Priority,
                CategoryName = t.Category.Name,
                ProjectName = t.Category.Project.Name,
                ProjectId = t.Category.Project.Id
            })
            .OrderBy(t => t.ProjectName)
            .ThenBy(t => t.CategoryName)
            .ThenBy(t => t.Name)
            .ToList();

        ResultCountText.Text = $"Found {results.Count} test(s)";

        if (results.Count > 0)
        {
            ResultsGrid.ItemsSource = results;
            ResultsGrid.Visibility = Visibility.Visible;
            NoResultsText.Visibility = Visibility.Collapsed;
        }
        else
        {
            ResultsGrid.ItemsSource = null;
            ResultsGrid.Visibility = Visibility.Collapsed;
            NoResultsText.Visibility = Visibility.Visible;
        }

        Debug.WriteLine($"[SearchResultsPage] Found {results.Count} results.");
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem != null)
        {
            var testId = (int)ResultsGrid.SelectedItem.GetType().GetProperty("Id")!.GetValue(ResultsGrid.SelectedItem)!;
            Debug.WriteLine($"[SearchResultsPage] Opening test: {testId}");
            AppNavigationService.Instance.NavigateTo(new TestDetailPage(testId), "TestDetail");
        }
    }
}

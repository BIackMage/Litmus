using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Litmus.Services;
using Litmus.Views;
using Litmus.Windows;

namespace Litmus;

public partial class MainWindow : Window
{
    private Button? _activeNavButton;

    public MainWindow()
    {
        InitializeComponent();
        Debug.WriteLine("[MainWindow] Initializing...");

        // Initialize navigation service
        AppNavigationService.Instance.Initialize(MainFrame);
        AppNavigationService.Instance.NavigationChanged += OnNavigationChanged;

        // Set initial page
        _activeNavButton = NavDashboard;
        NavigateToDashboard();

        // Setup search box placeholder
        SearchBox.TextChanged += SearchBox_TextChanged;

        // Check license on load
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[MainWindow] Loaded, checking license...");
        SettingsService.Load();

        if (!SettingsService.Settings.LicenseAccepted)
        {
            Debug.WriteLine("[MainWindow] License not accepted, showing dialog");
            var licenseWindow = new LicenseAgreementWindow();
            licenseWindow.Owner = this;
            var result = licenseWindow.ShowDialog();

            if (result != true || !licenseWindow.LicenseAccepted)
            {
                Debug.WriteLine("[MainWindow] License declined, closing app");
                Application.Current.Shutdown();
            }
        }
        else
        {
            Debug.WriteLine("[MainWindow] License already accepted");
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            Debug.WriteLine($"[MainWindow] Searching for: {SearchBox.Text}");
            AppNavigationService.Instance.NavigateTo(new SearchResultsPage(SearchBox.Text), "Search");
            UpdateActiveNav(null);
        }
    }

    private void OnNavigationChanged(object? sender, string pageName)
    {
        Debug.WriteLine($"[MainWindow] Navigation changed to: {pageName}");
    }

    private void SetActiveNav(Button button)
    {
        if (_activeNavButton != null)
        {
            _activeNavButton.Tag = null;
        }

        button.Tag = "Active";
        _activeNavButton = button;
    }

    private void UpdateActiveNav(Button? button)
    {
        if (_activeNavButton != null)
        {
            _activeNavButton.Tag = null;
        }

        if (button != null)
        {
            button.Tag = "Active";
        }
        _activeNavButton = button;
    }

    private void NavigateToDashboard()
    {
        AppNavigationService.Instance.NavigateTo(new DashboardPage(), "Dashboard");
    }

    private void NavDashboard_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav(NavDashboard);
        NavigateToDashboard();
    }

    private void NavProjects_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav(NavProjects);
        AppNavigationService.Instance.NavigateTo(new ProjectsPage(), "Projects");
    }

    private void NavTestRuns_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav(NavTestRuns);
        AppNavigationService.Instance.NavigateTo(new TestRunsPage(), "TestRuns");
    }

    private void NavReports_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav(NavReports);
        AppNavigationService.Instance.NavigateTo(new ReportsPage(), "Reports");
    }

    private void NavImport_Click(object sender, RoutedEventArgs e)
    {
        UpdateActiveNav(null);
        AppNavigationService.Instance.NavigateTo(new ImportPage(), "Import");
    }

    private void NavExport_Click(object sender, RoutedEventArgs e)
    {
        UpdateActiveNav(null);
        AppNavigationService.Instance.NavigateTo(new ExportPage(), "Export");
    }
}

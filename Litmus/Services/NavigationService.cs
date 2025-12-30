using System;
using System.Diagnostics;
using System.Windows.Controls;

namespace Litmus.Services;

public class AppNavigationService
{
    private Frame? _mainFrame;
    private static AppNavigationService? _instance;

    public static AppNavigationService Instance => _instance ??= new AppNavigationService();

    public event EventHandler<string>? NavigationChanged;

    public void Initialize(Frame frame)
    {
        _mainFrame = frame;
        Debug.WriteLine("[AppNavigationService] Initialized with main frame.");
    }

    public void NavigateTo(Page page, string pageName)
    {
        if (_mainFrame == null)
        {
            Debug.WriteLine("[AppNavigationService] Error: Frame not initialized.");
            return;
        }

        Debug.WriteLine($"[AppNavigationService] Navigating to: {pageName}");
        _mainFrame.Navigate(page);
        NavigationChanged?.Invoke(this, pageName);
    }

    public void GoBack()
    {
        if (_mainFrame?.CanGoBack == true)
        {
            Debug.WriteLine("[AppNavigationService] Going back.");
            _mainFrame.GoBack();
        }
    }
}

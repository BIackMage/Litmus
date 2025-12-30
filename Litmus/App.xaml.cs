using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Litmus.Services;
using QuestPDF.Infrastructure;

namespace Litmus;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Debug.WriteLine("[App] OnStartup called");

        // Setup exception handlers FIRST before anything else
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var fullError = GetFullExceptionDetails(ex);
            Debug.WriteLine($"[App] Unhandled exception: {fullError}");
            LogError(fullError);
            MessageBox.Show($"Fatal error:\n{fullError}", "Litmus Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            var fullError = GetFullExceptionDetails(args.Exception);
            Debug.WriteLine($"[App] Dispatcher exception: {fullError}");
            LogError(fullError);
            MessageBox.Show($"Error:\n{fullError}", "Litmus Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            Debug.WriteLine("[App] Calling base.OnStartup");
            base.OnStartup(e);

            Debug.WriteLine("[App] Configuring QuestPDF");
            // Configure QuestPDF license (Community license for open source)
            QuestPDF.Settings.License = LicenseType.Community;

            Debug.WriteLine("[App] Initializing database");
            // Initialize database
            DatabaseService.Initialize();

            Debug.WriteLine("[App] Startup complete");
        }
        catch (Exception ex)
        {
            var fullError = GetFullExceptionDetails(ex);
            Debug.WriteLine($"[App] Startup error: {fullError}");
            LogError(fullError);
            MessageBox.Show($"Startup error:\n{fullError}", "Litmus Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string GetFullExceptionDetails(Exception? ex)
    {
        if (ex == null) return "Unknown error";

        var details = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
        if (ex.InnerException != null)
        {
            details += $"\n\n--- Inner Exception ---\n{GetFullExceptionDetails(ex.InnerException)}";
        }
        return details;
    }

    private static void LogError(string error)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Litmus", "error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"\n\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{error}");
        }
        catch { /* Ignore logging errors */ }
    }
}

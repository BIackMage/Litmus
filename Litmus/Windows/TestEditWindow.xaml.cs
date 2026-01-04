using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Litmus.Models;
using Litmus.Services;

namespace Litmus.Windows;

public partial class TestEditWindow : Window
{
    private readonly Test _test;
    public bool TestUpdated { get; private set; }

    public TestEditWindow(Test test)
    {
        InitializeComponent();
        Debug.WriteLine($"[TestEditWindow] Opening editor for test: {test.Name}");

        _test = test;
        LoadTestData();
    }

    private void LoadTestData()
    {
        NameTextBox.Text = _test.Name;
        DescriptionTextBox.Text = _test.Description;
        CommandTextBox.Text = _test.Command;
        ExpectedResultTextBox.Text = _test.ExpectedResult;
        PrepStepsTextBox.Text = _test.PrepSteps;

        // Set priority
        foreach (ComboBoxItem item in PriorityComboBox.Items)
        {
            if (item.Tag?.ToString() == _test.Priority.ToString())
            {
                PriorityComboBox.SelectedItem = item;
                break;
            }
        }

        // Set automated flag
        IsAutomatedCheckBox.IsChecked = _test.IsAutomated;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[TestEditWindow] Saving test changes...");

        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Test name is required.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var context = DatabaseService.CreateContext();
        var test = context.Tests.Find(_test.Id);

        if (test == null)
        {
            MessageBox.Show("Test not found.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        test.Name = NameTextBox.Text.Trim();
        test.Description = DescriptionTextBox.Text.Trim();
        test.Command = CommandTextBox.Text.Trim();
        test.ExpectedResult = ExpectedResultTextBox.Text.Trim();
        test.PrepSteps = PrepStepsTextBox.Text.Trim();

        if (PriorityComboBox.SelectedItem is ComboBoxItem selectedPriority)
        {
            test.Priority = selectedPriority.Tag?.ToString() switch
            {
                "Critical" => Priority.Critical,
                "High" => Priority.High,
                "Low" => Priority.Low,
                _ => Priority.Medium
            };
        }

        test.IsAutomated = IsAutomatedCheckBox.IsChecked ?? false;

        context.SaveChanges();

        // Update the original test object so the UI reflects changes
        _test.Name = test.Name;
        _test.Description = test.Description;
        _test.Command = test.Command;
        _test.ExpectedResult = test.ExpectedResult;
        _test.PrepSteps = test.PrepSteps;
        _test.Priority = test.Priority;
        _test.IsAutomated = test.IsAutomated;

        TestUpdated = true;
        Debug.WriteLine("[TestEditWindow] Test saved successfully.");

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[TestEditWindow] Edit cancelled.");
        DialogResult = false;
        Close();
    }
}

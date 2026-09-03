using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Alpha6Ops.Desktop;

public partial class LogDatabaseWindow : Window
{
    private readonly LogFileDatabase database;
    internal LogDatabaseWindow(LogFileDatabase database)
    {
        InitializeComponent();
        this.database = database;
        DatabaseText.Text = database.DatabasePath;
        RefreshRows();
    }
    private void RefreshRows() => FilesGrid.ItemsSource = database.ReadRecent();
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshRows();
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (FilesGrid.SelectedItem is not DiagnosticFile file || !File.Exists(file.Path)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file.Path}\"") { UseShellExecute = true });
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

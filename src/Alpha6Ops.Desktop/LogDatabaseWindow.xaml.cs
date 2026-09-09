using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Alpha6Ops.Desktop;

public partial class LogDatabaseWindow : Window
{
    private readonly ProgramMonitor monitor;
    private bool reading;
    private bool closed;
    internal LogDatabaseWindow(ProgramMonitor monitor, IReadOnlyList<DiagnosticFile> rows)
    {
        InitializeComponent();
        this.monitor = monitor;
        DatabaseText.Text = monitor.Database.DatabasePath;
        FilesGrid.ItemsSource = rows;
        Closed += (_, _) => closed = true;
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshRowsAsync();
    internal async Task RefreshRowsAsync()
    {
        if (reading) return;
        reading = true;
        try
        {
            var rows = await monitor.ReadRecentAsync();
            if (!closed) FilesGrid.ItemsSource = rows;
        }
        finally { reading = false; }
    }
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (FilesGrid.SelectedItem is not DiagnosticFile file || !File.Exists(file.Path)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file.Path}\"") { UseShellExecute = true });
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

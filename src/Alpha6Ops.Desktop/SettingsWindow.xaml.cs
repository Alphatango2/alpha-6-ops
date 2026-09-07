using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

public partial class SettingsWindow : Window
{
    private readonly Action openFlightTools;
    private readonly Action openFlightHistory;
    private readonly Action openLogDatabase;
    private readonly Action exportLog;
    private readonly Action minimizeToTray;
    private readonly Action<GeneralSettings> applyGeneralSettings;
    private static string DataDirectory => CrashReporter.RootDirectory;

    internal SettingsWindow(string simulatorStatus, string pilotName, Action showFlightTools,
        Action? showFlightHistory = null, Action? showLogDatabase = null, Action? exportTestLog = null,
        Action? minimizeOps = null, string? programHealth = null, string? loggingStatus = null,
        GeneralSettings? currentGeneralSettings = null, Action<GeneralSettings>? saveGeneralSettings = null)
    {
        InitializeComponent();
        openFlightTools = showFlightTools;
        openFlightHistory = showFlightHistory ?? (() => { });
        openLogDatabase = showLogDatabase ?? (() => { });
        exportLog = exportTestLog ?? (() => { });
        minimizeToTray = minimizeOps ?? (() => { });
        applyGeneralSettings = saveGeneralSettings ?? (_ => { });
        LoadGeneralSettings(currentGeneralSettings ?? GeneralSettingsStore.Load());
        var simBriefUser = SimBriefImporter.LoadUsername();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        SimulatorStatusText.Text = simulatorStatus.ToUpperInvariant();
        SimConnectPluginStatusText.Text = simulatorStatus.Contains("CONNECTED", StringComparison.OrdinalIgnoreCase) &&
                                             !simulatorStatus.Contains("DISCONNECTED", StringComparison.OrdinalIgnoreCase)
            ? "CONNECTED" : "BUILT IN";
        PilotSummaryText.Text = string.IsNullOrWhiteSpace(pilotName) ? "No pilot display name is set." : $"Display name: {pilotName.Trim()}";
        SimBriefStatusText.Text = string.IsNullOrWhiteSpace(simBriefUser) ? "NOT CONFIGURED" : $"CONFIGURED • {simBriefUser}";
        SimBriefPluginStatusText.Text = string.IsNullOrWhiteSpace(simBriefUser) ? "SETUP NEEDED" : "CONFIGURED";
        DataPathText.Text = DataDirectory;
        ProgramHealthText.Text = string.IsNullOrWhiteSpace(programHealth) ? "PROGRAM MONITOR STARTING" : programHealth;
        LoggingStatusText.Text = string.IsNullOrWhiteSpace(loggingStatus) ? "Test logs save automatically when you connect." : loggingStatus;
        VersionText.Text = $"ALPHA 6 OPS v{version}";
        StateChanged += (_, _) => UpdateWindowStateIcon();
    }

    private void UpdateWindowStateIcon()
    {
        MaximizeIcon.Visibility = WindowState == WindowState.Maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = WindowState == WindowState.Maximized ? Visibility.Visible : Visibility.Collapsed;
    }
    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void ToggleMaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
    private void LoadGeneralSettings(GeneralSettings settings)
    {
        MinimizeToTrayToggle.IsChecked = settings.MinimizeToTray;
        FlashNotificationToggle.IsChecked = settings.FlashOnNotification;
        NotificationSoundToggle.IsChecked = settings.NotificationSound;
        AdvancedControlsToggle.IsChecked = settings.ShowAdvancedControls;
        SelectUnit(WeightUnitBox, settings.WeightUnit);
        SelectUnit(AltitudeUnitBox, settings.AltitudeUnit);
        SelectUnit(LandingDistanceUnitBox, settings.LandingDistanceUnit);
    }
    private GeneralSettings ReadGeneralSettings() => new(
        MinimizeToTrayToggle.IsChecked == true,
        FlashNotificationToggle.IsChecked == true,
        NotificationSoundToggle.IsChecked == true,
        SelectedUnit(WeightUnitBox, "LBS"), SelectedUnit(AltitudeUnitBox, "FT"),
        SelectedUnit(LandingDistanceUnitBox, "FT"), AdvancedControlsToggle.IsChecked == true);
    private static void SelectUnit(ComboBox box, string value)
    {
        foreach (ComboBoxItem item in box.Items)
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) { box.SelectedItem = item; return; }
        box.SelectedIndex = 0;
    }
    private static string SelectedUnit(ComboBox box, string fallback) =>
        (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;
    private void SaveGeneral_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadGeneralSettings();
        GeneralSettingsStore.Save(settings);
        applyGeneralSettings(settings);
        GeneralStatusText.Text = "SETTINGS SAVED";
    }
    private void ResetGeneral_Click(object sender, RoutedEventArgs e)
    {
        LoadGeneralSettings(GeneralSettings.Defaults);
        GeneralStatusText.Text = "DEFAULTS RESTORED — SELECT SAVE SETTINGS TO APPLY";
    }
    private void OpenFlightTools_Click(object sender, RoutedEventArgs e) { Close(); openFlightTools(); }
    private void OpenFlightHistory_Click(object sender, RoutedEventArgs e) => ContinueInMainWindow(openFlightHistory);
    private void OpenLogDatabase_Click(object sender, RoutedEventArgs e) => ContinueInMainWindow(openLogDatabase);
    private void ExportLog_Click(object sender, RoutedEventArgs e) => ContinueInMainWindow(exportLog);
    private void MinimizeToTray_Click(object sender, RoutedEventArgs e) { Close(); minimizeToTray(); }
    private void ContinueInMainWindow(Action action)
    {
        Close();
        Application.Current.Dispatcher.BeginInvoke(action);
    }
    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(DataDirectory);
    private void OpenCrashReports_Click(object sender, RoutedEventArgs e) => OpenFolder(Path.Combine(DataDirectory, "CrashReports"));
    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }
}

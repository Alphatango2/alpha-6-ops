using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Alpha6Ops.Identity;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    private readonly DesktopAccountSession? account;
    private readonly bool pilotPreview;
    internal bool IsPilotWorkspace => pilotPreview || account?.Bootstrap is not null;
    private Button? accountButton;
    private Button? pilotLogbookButton;
    private bool changingAccount;
    private bool MayStartAccountFlight()
    {
        if (account is null || account.MayStartNewFlight(DateTimeOffset.UtcNow)) return true;
        OpsNoticeWindow.Show(this, "Sign-in required", "Your local authorization has expired. Open Account and reconnect before starting a new flight. Recorded flights are preserved.");
        return false;
    }
    private void InitializeAccountWorkspace()
    {
        if (diagnosticMode && account is null && !pilotPreview) return;
        accountButton = AccountHeaderButton;
        accountButton.Content = pilotPreview ? "PILOT / ISOLATED PREVIEW" : account is null ? "ACCOUNT · LOCAL PREVIEW" : account.WorkspaceName + (account.IsOffline ? " · OFFLINE" : " ▾");
        if (account?.Bootstrap is null && !pilotPreview) return;
        var bootstrap = account?.Bootstrap;
        AccountWorkspaceText.Text = account?.WorkspaceName ?? "Flying as a Pilot";
        AccountSessionStatusText.Text = account?.IsOffline == true ? "OFFLINE · LOCAL FLIGHT TOOLS" : pilotPreview ? "ISOLATED PREVIEW" : "SIGNED IN";
        if (bootstrap is not null) { PilotNameBox.Text = bootstrap.Account.DisplayName; PilotNameBox.IsReadOnly = true; }
        Title += " · " + AccountWorkspaceText.Text;
        // Free is a feature profile of the existing dashboard, not a different visual shell.
        ModuleTiles.ItemsSource = PilotFeatureProfile.Tiles;
        foreach (var button in NavigationButtons.Children.OfType<Button>())
            if (button.Tag is string tag && !PilotFeatureProfile.Allows(tag)) button.Visibility = Visibility.Collapsed;
        var logbookContent = new StackPanel { Orientation = Orientation.Horizontal };
        logbookContent.Children.Add(new TextBlock { Text = "\uE8F1", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 21, Width = 43 });
        logbookContent.Children.Add(new TextBlock { Text = "PILOT LOGBOOK", VerticalAlignment = VerticalAlignment.Center });
        var logbook = new Button { Content = logbookContent, Tag = "PilotLogbook", Style = (Style)FindResource("OpsNav") };
        System.Windows.Automation.AutomationProperties.SetName(logbook, "PILOT LOGBOOK");
        logbook.Click += Module_Click;
        pilotLogbookButton = logbook;
        NavigationButtons.Children.Insert(1, logbook);
        NavigationTagline.Text = "YOUR FLIGHT.\nYOUR FLIGHT DECK.";
        FooterBrand.Text = "ALPHA 6 OPS  •  PILOT WORKSPACE";
        OperationsHeading.Text = "YOUR FLIGHT";
        AssignedFlightsTab.Visibility = WatchlistTab.Visibility = WatchFlightButton.Visibility = Visibility.Collapsed;
        DashboardFlightsGrid.Columns[0].Width = 150;
        DashboardFlightsGrid.Columns[1].MinWidth = 220;
        DashboardFlightsGrid.Columns[2].Width = DashboardFlightsGrid.Columns[3].Width = 130;
        DashboardFlightsGrid.Columns[4].Width = 160;
        ConnectFlightLabButton.Visibility = Visibility.Collapsed;
        AdvancedButton.Visibility = Visibility.Collapsed;
        SetAdvanced(false);
        ApplyPilotDashboardLayout();
        RefreshLiveTracker(liveAircraft, liveLast, liveRecorder?.Phase, activePlan is null ? "Set a flight or import SimBrief to get started." : "Assignment ready. Connect to begin tracking.");
        if (liveCancellation is null) TrackerModeText.Text = "PERSONAL FLIGHT TOOLS";
        ShowDashboard();
    }

    private void ApplyPilotDashboardLayout()
    {
        if (!IsPilotWorkspace) return;
        AlertsPanel.Visibility = NetworkPanel.Visibility = FleetPanel.Visibility = MessagesPanel.Visibility = Visibility.Collapsed;
        SetGrid(HeroAlertsLayout, [1], 1); Place(HeroPanel, 0, 0);
        SetGrid(ModulesMapLayout, [1], 1); Place(ModuleTiles, 0, 0);
        SetGrid(SummaryLayout, [1], 1); Place(OperationsPanel, 0, 0);
    }

    private void SelectPilotLogbook(bool selected)
    {
        if (pilotLogbookButton is null) return;
        if (selected)
        {
            pilotLogbookButton.Background = OpsUi.Brush("#FFDA00");
            pilotLogbookButton.Foreground = OpsUi.Brush("#080C0F");
        }
        else
        {
            pilotLogbookButton.ClearValue(Button.BackgroundProperty);
            pilotLogbookButton.ClearValue(Button.ForegroundProperty);
        }
    }
    private async void Account_Click(object sender, RoutedEventArgs e)
    {
        if (changingAccount) return;
        if (running || liveCancellation is not null || liveRecorder is not null && liveRecorder.Phase != Alpha6Ops.Core.FlightPhase.Complete)
        { OpsNoticeWindow.Show(this, "Flight in progress", "Finish the current flight and disconnect before changing account or workspace. Your recorder stays attached to its original pilot and workspace."); return; }
        changingAccount = true;
        IsEnabled = false;
        try
        {
        var previous = account?.Workspace;
        var previousUser = account?.Bootstrap?.Account.Id;
        var previousBootstrap = account?.Bootstrap;
        var previouslyOffline = account?.IsOffline;
        if (account is not null)
        {
            // Refresh before exposing a workspace selector. Existing recording is already protected above.
            try { await account.RestoreAsync(lifetime.Token); }
            catch (Exception) { /* The selector shows sign-in or retained local access; never display raw provider errors. */ }
        }
        if (running || liveCancellation is not null) return;
        var chooser = new AccountWindow(account) { Owner = this }; chooser.ShowDialog();
        if (chooser.SignedOut)
        {
            Hide();
            var login = new AccountWindow(account); login.ShowDialog();
            if (!login.Accepted) { ExitApplication(); return; }
        }
        if (account is not null && account.Bootstrap is null) { ExitApplication(); return; }
        if (chooser.SignedOut || account?.Workspace != previous || account?.Bootstrap?.Account.Id != previousUser
            || !ReferenceEquals(account?.Bootstrap, previousBootstrap) || account?.IsOffline != previouslyOffline)
        {
            var next = new MainWindow(null, account);
            Application.Current.MainWindow = next; next.Show();
            CloseForWorkspaceChange();
        }
        }
        finally { changingAccount = false; IsEnabled = true; }
    }
    internal void CloseForWorkspaceChange()
    {
        exiting = true; dashboardClock.Stop();
        UserPreferencesStore.Save(new UserPreferences(Width, Height, WindowState == WindowState.Maximized, false, SelectedFixture, PilotName), stateDirectory);
        programMonitor?.Stop("workspace_change"); flightHistory?.Dispose(); lifetime.Cancel();
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); trayIcon.Dispose(); Close();
    }
}

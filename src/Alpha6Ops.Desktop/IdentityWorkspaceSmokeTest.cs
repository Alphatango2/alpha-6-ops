using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Alpha6Ops.Identity;

namespace Alpha6Ops.Desktop;

// Isolated UI fixture, invoked only by the explicit desktop diagnostic run. No network identity is issued.
internal static class IdentityWorkspaceSmokeTest
{
    internal static async Task RunAsync(string outputDirectory)
    {
        var design = new AccountWindow(null, preview: true);
        design.Show(); design.UpdateLayout();
        DashboardSmokeTest.Capture(design, Path.Combine(outputDirectory, "identity-design-login.png"));
        design.ContinueButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (!design.ShowingWorkspaces || design.AirlineCards.Children.Count != 2)
            throw new InvalidOperationException("Login preview must lead to sample workspaces.");
        ((System.Windows.Controls.Button)design.AirlineCards.Children[1]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (design.SelectedWorkspace.AirlineId is null) throw new InvalidOperationException("Airline card selection failed.");
        DashboardSmokeTest.Capture(design, Path.Combine(outputDirectory, "identity-design-workspaces.png"));
        design.OpenWorkspaceButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (design.Accepted) throw new InvalidOperationException("Design preview must never authorize a real workspace.");
        design.Width = 960; design.Height = 680; design.UpdateLayout();
        DashboardSmokeTest.Capture(design, Path.Combine(outputDirectory, "identity-design-workspaces-compact.png"));
        design.BackButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (design.ShowingWorkspaces) throw new InvalidOperationException("Preview back navigation failed.");
        DashboardSmokeTest.Capture(design, Path.Combine(outputDirectory, "identity-design-login-compact.png"));
        design.Close();
        var previewOpens = 0;
        var launchPortal = new AccountWindow(null, preview: true, openPilotPreview: () => previewOpens++);
        launchPortal.Show();
        launchPortal.ContinueButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        ((System.Windows.Controls.Button)launchPortal.AirlineCards.Children[0]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        launchPortal.OpenWorkspaceButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (previewOpens != 0 || launchPortal.Accepted) throw new InvalidOperationException("Launcher must not authorize or open a sample airline.");
        launchPortal.PersonalCard.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        launchPortal.OpenWorkspaceButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (previewOpens != 1 || launchPortal.Accepted || launchPortal.IsVisible)
            throw new InvalidOperationException("Launcher must close login and enter only the isolated pilot preview without authorization.");
        var root = Path.Combine(outputDirectory, "identity-ui");
        var config = new IdentityConfiguration("https://test.invalid", "diagnostic-client", "https://api.test.invalid", "https://api.test.invalid/", "https://test.invalid/");
        var account = new DesktopAccountSession(config, root);
        var welcome = new AccountWindow(account);
        welcome.Show(); welcome.UpdateLayout();
        if (!OpsUi.UsesWindowTheme(welcome)) throw new InvalidOperationException("Account window must use shared Alpha 6 chrome.");
        DashboardSmokeTest.Capture(welcome, Path.Combine(outputDirectory, "identity-welcome.png"));
        welcome.Close();

        var now = DateTimeOffset.UtcNow;
        var bootstrap = new BootstrapResponse(new(Guid.NewGuid(), "Identity Test Pilot", "pilot@example.invalid", true, AccountStatus.Active),
            new(PersonalPlan.Free, "active", null), Capabilities.Personal,
            [new(Guid.NewGuid(), "test-air", "Test Airline", "TEST", AirlinePlan.Community, "active", MembershipStatus.Active,
                [AirlineRole.Pilot], false, [Capabilities.AirlineRead])], WorkspaceSelection.Personal, now, now.AddDays(30));
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config))));
        var saved = new SavedAccountSession(key, null, bootstrap, now, now, WorkspaceSelection.Personal);
        Directory.CreateDirectory(Path.Combine(root, "Identity"));
        File.WriteAllBytes(Path.Combine(root, "Identity", "session.bin"), ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(saved), null, DataProtectionScope.CurrentUser));
        if (!await account.RestoreAsync(default)) throw new InvalidOperationException("Diagnostic offline session could not be restored.");
        var chooser = new AccountWindow(account);
        chooser.Show(); chooser.UpdateLayout();
        if (chooser.AirlineCards.Children.OfType<System.Windows.Controls.Button>().Any(button => button.IsEnabled))
            throw new InvalidOperationException("Offline personal workspace must not enable another airline card.");
        DashboardSmokeTest.Capture(chooser, Path.Combine(outputDirectory, "identity-workspaces.png"));
        chooser.Close();

        var checks = 9;
        void Check(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); checks++; }
        var cacheDirectory = Path.Combine(account.DataDirectory, "SimBrief");
        Directory.CreateDirectory(cacheDirectory);
        File.WriteAllText(Path.Combine(cacheDirectory, "username.txt"), "isolated-pilot");
        File.WriteAllText(Path.Combine(cacheDirectory, "ofp-isolation.txt"), "PILOT RELEASE\nATC FLIGHT PLAN\nTEST ROUTE");
        File.WriteAllText(Path.Combine(cacheDirectory, "ofp-isolation.pdf"), "%PDF-test-fixture");
        var otherWorkspace = Path.Combine(root, "other-workspace");
        Check(SimBriefImporter.OfpTextPath("isolation", account.DataDirectory) is not null &&
            SimBriefImporter.OfpPdfPath("isolation", account.DataDirectory) is not null &&
            SimBriefImporter.OfpTextPath("isolation", otherWorkspace) is null &&
            SimBriefImporter.OfpPdfPath("isolation", otherWorkspace) is null,
            "OFP text and PDF must remain isolated to the account workspace.");
        var pilot = new MainWindow(account.DataDirectory, account);
        try
        {
            pilot.Show(); pilot.Width = 1440; pilot.Height = 960; pilot.UpdateLayout();
            Check(pilot.PilotNameBox.IsReadOnly && pilot.PilotNameBox.Text == bootstrap.Account.DisplayName, "Authenticated pilot identity must be read-only.");
            Check(pilot.IsPilotWorkspace && pilot.DashboardViewport.IsVisible && pilot.DashboardScroll.IsVisible, "Free must use the existing dashboard shell.");
            Check(pilot.DashboardFlights.Count == 0 && !pilot.HeroAircraftTypeText.Text.Contains("SAMPLE"), "New pilot dashboard must not display demo assignments.");
            Check(pilot.AlertsPanel.Visibility == Visibility.Collapsed && pilot.NetworkPanel.Visibility == Visibility.Collapsed && pilot.FleetPanel.Visibility == Visibility.Collapsed && pilot.MessagesPanel.Visibility == Visibility.Collapsed, "Airline panels must be removed from Free.");
            Check(pilot.ModuleTiles.Items.Cast<DashboardTile>().Select(t => t.Name).SequenceEqual(new[] { "PilotLogbook", "Dispatch", "FlightTracking", "Weather" }), "Free must retain only the four personal photo tiles.");
            Check(pilot.NavigationButtons.Children.OfType<System.Windows.Controls.Button>().All(b => b.Tag is not string tag || PilotFeatureProfile.Allows(tag) || b.Visibility == Visibility.Collapsed), "Airline navigation must be hidden.");
            Check(!PilotFeatureProfile.Allows("Aircraft") && !PilotFeatureProfile.Allows("OCC"), "Pilot module routing must exclude fleet and operations.");
            Check(pilot.FlightHistory?.ReadRecentFlights().Count == 0, "New account must not inherit another account's history.");
            Check(pilot.DispatchView.SimBriefUsernameBox.Text == "isolated-pilot", "Dispatch must load only the current workspace's SimBrief username.");
            DashboardSmokeTest.Capture(pilot, Path.Combine(outputDirectory, "identity-pilot-home.png"));
            var logbookNav = pilot.NavigationButtons.Children.OfType<System.Windows.Controls.Button>().Single(b => b.Tag as string == "PilotLogbook");
            logbookNav.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); pilot.UpdateLayout();
            Check(pilot.PilotLogbookView.IsVisible && pilot.PilotLogbookView.VisibleFlightCount == 0 && pilot.DashboardRoot.Children.Contains(pilot.PilotLogbookView), "Logbook must use the original embedded view and account data.");
            DashboardSmokeTest.Capture(pilot, Path.Combine(outputDirectory, "pilot-logbook-empty.png"));
            pilot.ShowFlightTracking(); pilot.UpdateLayout();
            Check(pilot.FlightTrackingView.IsVisible && pilot.DashboardRoot.Children.Contains(pilot.FlightTrackingView), "Tracking must retain its original instrument layout.");
            DashboardSmokeTest.Capture(pilot, Path.Combine(outputDirectory, "pilot-tracking-empty.png"));
            pilot.ShowDashboard();
            pilot.HeaderDispatchButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); pilot.UpdateLayout();
            Check(pilot.DispatchView.IsVisible && !pilot.DispatchView.HasActiveRelease && !pilot.ToolsOverlay.IsVisible, "Dispatch must use the integrated release workflow with an honest empty assignment.");
            Check(!pilot.PilotLogbookView.IsVisible && !pilot.FlightTrackingView.IsVisible, "Dispatch must hide the previous embedded workspace.");
            DashboardSmokeTest.Capture(pilot, Path.Combine(outputDirectory, "pilot-dispatch-empty.png"));
            pilot.ShowDashboard();
            Check(pilot.LocalWeatherButton.IsVisible && pilot.WeatherConditionText.Text.Contains("TEST"), "Existing weather header must remain available with diagnostic status.");
            pilot.Width = 960; pilot.Height = 680; pilot.UpdateLayout();
            Check(pilot.HeroPanel.ActualWidth > 1600 && pilot.ModuleTiles.ActualWidth > 1600 && pilot.OperationsPanel.ActualWidth > 1600, "Remaining dashboard panels must fill space left by airline panels.");
            DashboardSmokeTest.Capture(pilot, Path.Combine(outputDirectory, "pilot-home-compact.png"));
            var plan = new ActiveFlightPlan("TEST 101", "N101", "KORD", "KMSP", now, now.AddHours(2), Route: "TEST ROUTE", AircraftType: "A320");
            var viewer = new OfpViewerWindow(plan with { OfpCacheKey = "isolation" }, account.DataDirectory);
            viewer.Show(); viewer.UpdateLayout();
            Check(viewer.OpenPdfButton.IsEnabled && !viewer.DocumentStatusText.Text.Contains("UNAVAILABLE"), "OFP viewer must find the current workspace's cached release.");
            viewer.Close();
            Check(File.Exists(Path.Combine(account.DataDirectory, "ofp-viewer-state.json")) && !File.Exists(Path.Combine(otherWorkspace, "ofp-viewer-state.json")), "OFP viewer preferences must remain in the current workspace.");
            ActiveFlightPlanStore.Save(plan, account.DataDirectory);
            var id = pilot.FlightHistory!.BeginFlight(bootstrap.Account.DisplayName, "diagnostic", "Isolated UI fixture", "A320", "KORD", "KMSP", "TEST 101", "test");
            pilot.FlightHistory.EndFlight(id, "Complete");
        }
        finally { pilot.CloseForWorkspaceChange(); }
        var restored = new MainWindow(account.DataDirectory, account);
        try
        {
            restored.Show(); restored.Width = 1440; restored.Height = 960; restored.UpdateLayout();
            await restored.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Check(restored.DashboardFlightsGrid.Columns[1].ActualWidth >= 220, "Pilot flight table must keep its route column readable.");
            Check(restored.DashboardFlights.Count == 1 && restored.HeroFlightText.Text == "TEST 101" && restored.OriginCodeText.Text == "ORD", "Existing hero and flight table must render only the pilot's saved plan.");
            DashboardSmokeTest.Capture(restored, Path.Combine(outputDirectory, "pilot-home-planned.png"));
            restored.OpenTools(); restored.UpdateLayout();
            Check(restored.ClearFlightButton.IsEnabled && restored.RouteText.Text.Contains("KORD"), "Dispatch must load the account's assignment.");
            DashboardSmokeTest.Capture(restored, Path.Combine(outputDirectory, "pilot-dispatch-planned.png"));
            restored.ShowFlightTracking(); restored.UpdateLayout();
            DashboardSmokeTest.Capture(restored, Path.Combine(outputDirectory, "pilot-tracking-planned.png"));
            restored.OpenTools();
            restored.ClearFlightButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(restored.ActivePlanForTest is null && ActiveFlightPlanStore.Load(account.DataDirectory) is null && restored.DashboardScroll.IsVisible && restored.DashboardFlights.Count == 0, "Clearing a plan must return to an empty pilot dashboard.");
            restored.ShowPilotLogbook();
            Check(restored.PilotLogbookView.VisibleFlightCount == 1, "Clearing an assignment must preserve recorded history.");
        }
        finally { restored.CloseForWorkspaceChange(); }
        var preview = new MainWindow(Path.Combine(root, "pilot-preview"), null, pilotPreview: true);
        try
        {
            Check(preview.IsPilotWorkspace && preview.FlightHistory?.ReadRecentFlights().Count == 0 && preview.ActivePlanForTest is null && preview.DashboardFlights.Count == 0, "Preview must use the restricted dashboard and isolated empty data.");
        }
        finally { preview.CloseForWorkspaceChange(); }
        File.WriteAllText(Path.Combine(outputDirectory, "identity-ui-smoke.json"), JsonSerializer.Serialize(new { passed = true, checks }));
    }
}

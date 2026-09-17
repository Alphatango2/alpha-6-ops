using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Alpha6Ops.Identity;

namespace Alpha6Ops.Desktop;

internal sealed partial class AccountWindow : Window
{
    private readonly DesktopAccountSession? account;
    private readonly bool preview;
    private readonly Action? openPilotPreview;
    private readonly CancellationTokenSource lifetime = new();
    private WorkspaceSelection selection = WorkspaceSelection.Personal;
    private string selectedName = "Flying as a Pilot";
    private bool busy;
    internal bool Accepted { get; private set; }
    internal bool SignedOut { get; private set; }
    internal bool ShowingWorkspaces => WorkspacePage.Visibility == Visibility.Visible;
    internal WorkspaceSelection SelectedWorkspace => selection;

    internal AccountWindow(DesktopAccountSession? account, bool restore = false, bool preview = false, Action? openPilotPreview = null)
    {
        this.account = account;
        this.preview = preview;
        this.openPilotPreview = preview ? openPilotPreview : null;
        InitializeComponent();
        OpsUi.Configure(this, preview ? "Account portal - Design preview" : "Account portal", 1240, 820);
        MinWidth = 960; MinHeight = 680;
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        PreviewBanner.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
        PreviewWorkspaceBanner.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
        Closed += (_, _) => lifetime.Cancel();
        SizeChanged += (_, _) => UpdateCompactLayout();
        if (account?.Bootstrap is not null) ShowWorkspaces(); else ShowLogin();
        Loaded += (_, _) => { if (!ShowingWorkspaces) EmailInput.Focus(); };
        if (restore && account is not null) Loaded += async (_, _) => await RunAsync(async () =>
        {
            SetStatus("Checking your saved session…");
            if (await account.RestoreAsync(lifetime.Token)) { Accepted = true; Close(); }
            else ShowLogin();
        });
    }

    private void UpdateCompactLayout()
    {
        var compact = ActualHeight < 760;
        LoginFrame.Margin = compact ? new Thickness(40, 24, 40, 20) : new Thickness(56, 34, 56, 26);
        LoginBody.MinHeight = compact ? 0 : 540;
        LoginForm.Margin = new Thickness(0, compact ? 12 : 28, 0, compact ? 12 : 28);
        LoginHeading.FontSize = compact ? 30 : 36;
        LoginHeading.LineHeight = compact ? 36 : 44;
        LoginDescription.Margin = new Thickness(0, 0, 0, compact ? 18 : 30);
        LoginDivider.Margin = new Thickness(0, compact ? 16 : 22, 0, compact ? 16 : 22);
        LoginOptionsHint.Margin = new Thickness(0, compact ? 8 : 10, 0, compact ? 16 : 25);
    }

    private void ShowLogin()
    {
        LoginPage.Visibility = Visibility.Visible;
        WorkspacePage.Visibility = Visibility.Collapsed;
        ContinueButton.IsDefault = true; OpenWorkspaceButton.IsDefault = false;
        SetStatus(account is null && !preview ? "This installation is in Local Preview. Account sign-in has not been connected yet." : "");
        ContinueButton.IsEnabled = OtherLoginButton.IsEnabled = RegisterButton.IsEnabled = preview || account is not null;
        ContinueButton.Content = preview ? "Preview your workspaces    →" : "Continue with email    →";
    }

    private async void Continue_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (preview) { ShowWorkspaces(); return; }
        var email = EmailInput.Text.Trim();
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        { SetStatus("Enter a valid email address to continue."); EmailInput.Focus(); return; }
        await SignInAsync(email);
    });

    private async void OtherLogin_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (preview) { ShowWorkspaces(); return; }
        await SignInAsync(null);
    });

    private async Task SignInAsync(string? email)
    {
        if (account is null) return;
        SetStatus("Complete sign-in in your browser. You can return here when you’re done.");
        await account.LoginAsync(lifetime.Token, email);
        ShowWorkspaces();
    }

    internal void ShowWorkspaces()
    {
        if (!preview && account?.Bootstrap is null) return;
        LoginPage.Visibility = Visibility.Collapsed;
        WorkspacePage.Visibility = Visibility.Visible;
        ContinueButton.IsDefault = false; OpenWorkspaceButton.IsDefault = true;
        var bootstrap = account?.Bootstrap;
        var offline = account?.IsOffline == true;
        AccountNameText.Text = preview ? "Alex Morgan" : bootstrap!.Account.DisplayName;
        AccountEmailText.Text = preview ? "Design preview · sample memberships" : bootstrap!.Account.Email;
        AccountInitialsText.Text = string.Concat(AccountNameText.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
        ConnectionText.Text = preview ? "DESIGN PREVIEW" : offline ? "OFFLINE ACCESS" : "SIGNED IN";
        PlanText.Text = preview ? "FREE" : bootstrap!.PersonalEntitlement.Plan.ToString().ToUpperInvariant();
        SetStatus(offline ? "You’re offline. Continue with your current workspace or fly personally. Reconnect to switch airlines." : "");
        ReconnectButton.Visibility = offline ? Visibility.Visible : Visibility.Collapsed;
        ManageButton.IsEnabled = JoinButton.IsEnabled = CreateButton.IsEnabled = !offline;
        selection = account?.Workspace ?? WorkspaceSelection.Personal;
        var airlines = preview ? PreviewAirlines : bootstrap!.Airlines;
        AirlineCountText.Text = $"YOUR VIRTUAL AIRLINES  /  {airlines.Length:00}";
        AirlineCards.Children.Clear();
        foreach (var airline in airlines)
        {
            var workspace = new WorkspaceSelection(airline.Id);
            var card = AirlineCard(airline, workspace);
            card.IsEnabled = airline.MembershipStatus == MembershipStatus.Active && (!offline || workspace == account!.Workspace);
            AirlineCards.Children.Add(card);
        }
        EmptyAirlines.Visibility = airlines.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        selectedName = selection.AirlineId is { } id ? airlines.FirstOrDefault(a => a.Id == id)?.Name ?? "Flying as a Pilot" : "Flying as a Pilot";
        UpdateSelection();
        OpenWorkspaceButton.Focus();
    }

    private Button AirlineCard(AirlineWorkspace airline, WorkspaceSelection workspace)
    {
        var card = new Button { Style = (Style)FindResource("WorkspaceCard"), Tag = workspace, Margin = new Thickness(0, 0, 16, 16), MinHeight = 166, Padding = new Thickness(22) };
        System.Windows.Automation.AutomationProperties.SetName(card, airline.Name + ", " + string.Join(", ", airline.Roles));
        var content = new Grid();
        for (var i = 0; i < 3; i++) content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var header = new Grid();
        var monogram = Copy(airline.Callsign.Length > 3 ? airline.Callsign[..3] : airline.Callsign, 12, "#D5E2ED", FontWeights.SemiBold);
        monogram.HorizontalAlignment = HorizontalAlignment.Center; monogram.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(new Border { Background = OpsUi.Brush("#233443"), CornerRadius = new CornerRadius(8), Width = 42, Height = 42, HorizontalAlignment = HorizontalAlignment.Left, Child = monogram });
        var tier = Copy(airline.Plan.ToString().ToUpperInvariant(), 10, "#A2B0BD"); tier.HorizontalAlignment = HorizontalAlignment.Right; tier.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(tier); content.Children.Add(header);
        var name = Copy(airline.Name, 19, "#E8EDF2", FontWeights.SemiBold); name.Margin = new Thickness(0, 17, 0, 8); Grid.SetRow(name, 1); content.Children.Add(name);
        var roles = Copy(string.Join(" · ", airline.Roles) + (airline.IsFounder ? " · Founder" : ""), 12, "#A2B0BD"); Grid.SetRow(roles, 2); content.Children.Add(roles);
        card.Content = content;
        card.Click += (_, _) => { selection = workspace; selectedName = airline.Name; UpdateSelection(); };
        return card;
    }

    private static TextBlock Copy(string text, double size, string color, FontWeight? weight = null) => new()
    { Text = text, FontFamily = new FontFamily("Segoe UI"), FontSize = size, Foreground = OpsUi.Brush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };

    private void Personal_Click(object sender, RoutedEventArgs e)
    { selection = WorkspaceSelection.Personal; selectedName = "Flying as a Pilot"; UpdateSelection(); }

    private void UpdateSelection()
    {
        PersonalCard.BorderBrush = OpsUi.Brush(selection.AirlineId is null ? "#C3A94E" : "#304050");
        PersonalCard.Background = OpsUi.Brush(selection.AirlineId is null ? "#242820" : "#121F2A");
        PersonalSelected.Text = selection.AirlineId is null ? "●  SELECTED" : "SELECT  ↗";
        foreach (var card in AirlineCards.Children.OfType<Button>())
        {
            var selected = (WorkspaceSelection)card.Tag == selection;
            card.BorderBrush = OpsUi.Brush(selected ? "#C3A94E" : "#304050");
            card.Background = OpsUi.Brush(selected ? "#242820" : "#121F2A");
        }
        SelectedNameText.Text = selectedName;
        SelectedTypeText.Text = selection.AirlineId is null ? "PERSONAL WORKSPACE" : "VIRTUAL AIRLINE WORKSPACE";
    }

    private async void OpenWorkspace_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (preview)
        {
            if (selection.AirlineId is null && openPilotPreview is not null) { openPilotPreview(); Close(); return; }
            SetStatus(openPilotPreview is not null ? "Choose Flying as a Pilot to explore the current app. Airline workspaces are not available in this preview."
                : $"“{selectedName}” selected. This design preview ends here; signing in to a configured account opens your workspace.");
            return;
        }
        await account!.SelectWorkspaceAsync(selection, lifetime.Token); Accepted = true; Close();
    });

    private void Back_Click(object sender, RoutedEventArgs e) { if (preview) ShowLogin(); else Close(); }

    private void Portal_Click(object sender, RoutedEventArgs e)
    {
        if (preview) { SetStatus("In the connected app, this opens the account website for registration and airline management."); return; }
        if (account is null) { SetStatus("Account services have not been configured for this installation."); return; }
        var path = (sender as Button)?.Tag as string ?? "Account";
        try { Process.Start(new ProcessStartInfo(new Uri(new Uri(account.Configuration.PortalUrl.TrimEnd('/') + "/"), path).AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception) { SetStatus("The website could not be opened. Check your default browser and try again."); }
    }

    private async void SignOut_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (preview) { ShowLogin(); return; }
        await account!.SignOutAsync(); SignedOut = true; Close();
    });

    private async void Reconnect_Click(object sender, RoutedEventArgs e) => await RunAsync(() => SignInAsync(null));

    private void SetStatus(string message)
    {
        LoginStatus.Text = WorkspaceStatus.Text = message;
        LoginStatus.Visibility = WorkspaceStatus.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true; InteractionRoot.IsEnabled = false;
        try { await action(); }
        catch (OperationCanceledException) { SetStatus("Sign-in canceled. You can try again."); }
        catch (Exception) { SetStatus("We couldn’t complete that request. Check your connection and try again."); }
        finally { busy = false; InteractionRoot.IsEnabled = true; }
    }

    // Presentation-only samples; never create a session or enter an authenticated workspace.
    private static readonly AirlineWorkspace[] PreviewAirlines =
    [
        new(new Guid("11111111-1111-1111-1111-111111111111"), "northstar", "Northstar Virtual", "NSV", AirlinePlan.Community, "active", MembershipStatus.Active, [AirlineRole.Owner], true, []),
        new(new Guid("22222222-2222-2222-2222-222222222222"), "meridian", "Meridian Air", "MVA", AirlinePlan.Pro, "active", MembershipStatus.Active, [AirlineRole.Pilot], false, [])
    ];
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Alpha6Ops.Desktop;

public partial class SierraShell : UserControl
{
    public static readonly DependencyProperty WorkspaceProperty=DependencyProperty.Register(nameof(Workspace),typeof(object),typeof(SierraShell));
    public static readonly DependencyProperty UtilitiesProperty=DependencyProperty.Register(nameof(Utilities),typeof(object),typeof(SierraShell));
    public static readonly DependencyProperty IsCompactProperty=DependencyProperty.Register(nameof(IsCompact),typeof(bool),typeof(SierraShell));
    public object Workspace { get=>(object)GetValue(WorkspaceProperty);set=>SetValue(WorkspaceProperty,value); }
    public object Utilities { get=>(object)GetValue(UtilitiesProperty);set=>SetValue(UtilitiesProperty,value); }
    public bool IsCompact { get=>(bool)GetValue(IsCompactProperty);private set=>SetValue(IsCompactProperty,value); }
    internal ShellNavigation Navigation { get; }=new();
    internal event EventHandler? ConnectionRequested;
    internal event EventHandler? ExitRequested;
    private IInputElement? priorFocus;

    public SierraShell()
    {
        InitializeComponent();
        PrimaryNavigation.ItemsSource=Navigation.Destinations.Where(d=>d.Primary).ToArray();
        SecondaryNavigation.ItemsSource=Navigation.Destinations.Where(d=>d.Section is ShellSection.Logbook or ShellSection.Settings or ShellSection.Help).ToArray();
        Navigation.Changed+=(_,_)=>WorkspaceTitle.Text=Navigation.Destinations.First(d=>d.Section==Navigation.Current).Title;
        SizeChanged+=(_,_)=>ApplyViewport();
        PreviewKeyDown+=OnShellKeyDown;
    }

    private void ApplyViewport()
    {
        IsCompact=ActualWidth<1100;
        SearchResults.MaxHeight=Math.Max(60, Math.Min(320, ActualHeight-250));
        UtilityScroll.MaxHeight=Math.Max(60, Math.Min(400, ActualHeight-210));
        RailColumn.Width=new GridLength((double)FindResource(IsCompact?"S4.NavigationCompactWidth":"S4.NavigationWidth"));
        NavigationDock.Margin=IsCompact?new Thickness(2,14,0,8):new Thickness(8,14,8,8);
        BrandIdentity.Visibility=IsCompact?Visibility.Collapsed:Visibility.Visible;
        RailTagline.Visibility=!IsCompact&&ActualHeight>850?Visibility.Visible:Visibility.Collapsed;
        SearchPrompt.Visibility=SearchShortcut.Visibility=ActualWidth<1500?Visibility.Collapsed:Visibility.Visible;
        WorkspaceSubtitle.Visibility=ActualWidth<1000?Visibility.Collapsed:Visibility.Visible;
        ClockButton.Visibility=ActualWidth<900?Visibility.Collapsed:Visibility.Visible;
        SearchButton.Margin=new Thickness(0,0,ActualWidth<900?4:16,0);
        ConnectionButton.Margin=new Thickness(0,0,ActualWidth<900?4:12,0);
        FooterBrand.Visibility=ActualWidth<750?Visibility.Collapsed:Visibility.Visible;
    }

    internal void PresentConnection(FlightSessionSnapshot snapshot)
    {
        ConnectionLabel.Text=snapshot.Connection switch {
            SimulatorConnectionState.Launching=>"Launching",SimulatorConnectionState.Connecting=>"Connecting",
            SimulatorConnectionState.Connected=>"Connected",SimulatorConnectionState.Reconnecting=>"Reconnecting",
            SimulatorConnectionState.Failed=>"Connection failed",_=>"Disconnected" };
        var color=snapshot.Connection switch {
            SimulatorConnectionState.Connected=>"S4.Success",SimulatorConnectionState.Failed=>"S4.Critical",
            SimulatorConnectionState.Launching or SimulatorConnectionState.Connecting or SimulatorConnectionState.Reconnecting=>"S4.Warning",_=>"S4.TextMuted" };
        ConnectionIndicator.Fill=(Brush)FindResource(color);
        ConnectionButton.ToolTip=snapshot.ConnectionMessage+"\nOpen simulator controls";
        AutomationPropertiesBridge.ConnectionName(ConnectionButton,ConnectionLabel.Text);
    }
    internal void PresentProfile(string name)
    {
        ProfileInitials.Text=string.IsNullOrWhiteSpace(name)?"P":string.Concat(name.Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p=>char.ToUpperInvariant(p[0])));
        ProfileButton.ToolTip=string.IsNullOrWhiteSpace(name)?"Pilot profile and flight tools":name+" · Profile and flight tools";
    }
    internal void PresentMessages(int unread)
    {
        UnreadDot.Visibility=unread>0?Visibility.Visible:Visibility.Collapsed;
        MessagesButton.ToolTip=$"Local product messages · {unread} unread";
    }
    private void Navigate_Click(object sender,RoutedEventArgs e) { if(((Button)sender).DataContext is ShellDestination d)Navigation.Request(d.Section); }
    private void Connection_Click(object sender,RoutedEventArgs e)=>ConnectionRequested?.Invoke(this,EventArgs.Empty);
    private void Exit_Click(object sender,RoutedEventArgs e)=>ExitRequested?.Invoke(this,EventArgs.Empty);
    private void Messages_Click(object sender,RoutedEventArgs e)=>Navigation.Request(ShellSection.Messages);
    private void Profile_Click(object sender,RoutedEventArgs e)=>Navigation.Request(ShellSection.Tools);
    private void Status_Click(object sender,RoutedEventArgs e)=>ShowUtilities();
    private void Search_Click(object sender,RoutedEventArgs e)=>OpenSearch();
    internal void OpenSearch()
    {
        if(SearchOverlay.Visibility==Visibility.Visible){WorkspaceSearch.Focus();return;}
        RememberExternalFocus();UtilityOverlay.Visibility=Visibility.Collapsed;SearchOverlay.Visibility=Visibility.Visible;
        WorkspaceSearch.Clear();RefreshSearch();WorkspaceSearch.Focus();
    }
    internal void ShowUtilities()
    {
        RememberExternalFocus();SearchOverlay.Visibility=Visibility.Collapsed;UtilityOverlay.Visibility=Visibility.Visible;
        CloseUtilityButton.Focus();
    }
    private void RememberExternalFocus()
    {
        if(SearchOverlay.Visibility!=Visibility.Visible&&UtilityOverlay.Visibility!=Visibility.Visible)
            priorFocus=Keyboard.FocusedElement;
    }
    internal void CloseOverlays()
    {
        if(SearchOverlay.Visibility!=Visibility.Visible&&UtilityOverlay.Visibility!=Visibility.Visible)return;
        SearchOverlay.Visibility=UtilityOverlay.Visibility=Visibility.Collapsed;
        if(priorFocus is UIElement target&&target.IsVisible&&target.IsEnabled)target.Focus();else SearchButton.Focus();
        priorFocus=null;
    }
    private void CloseOverlay_Click(object sender,RoutedEventArgs e)=>CloseOverlays();
    private void Search_Changed(object sender,TextChangedEventArgs e){if(SearchResults is not null)RefreshSearch();}
    private void RefreshSearch()
    {
        var results=Navigation.Search(WorkspaceSearch.Text);SearchResults.ItemsSource=results;SearchResults.SelectedIndex=results.Count>0?0:-1;
        SearchEmpty.Visibility=results.Count==0?Visibility.Visible:Visibility.Collapsed;
    }
    internal void ActivateSearchResult()
    {
        if(SearchResults.SelectedItem is not ShellDestination destination)return;
        CloseOverlays();Navigation.Request(destination.Section);
    }
    private void Search_KeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Enter){ActivateSearchResult();e.Handled=true;}
        else if(e.Key==Key.Down){SearchResults.Focus();if(SearchResults.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem item)item.Focus();e.Handled=true;}
    }
    private void Results_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){ActivateSearchResult();e.Handled=true;}}
    private void Results_DoubleClick(object sender,MouseButtonEventArgs e)=>ActivateSearchResult();
    private void OnShellKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.K&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){OpenSearch();e.Handled=true;}
        else if(e.Key==Key.Escape&&(SearchOverlay.Visibility==Visibility.Visible||UtilityOverlay.Visibility==Visibility.Visible)){CloseOverlays();e.Handled=true;}
    }
    private static class AutomationPropertiesBridge
    {
        internal static void ConnectionName(Button button,string status)=>System.Windows.Automation.AutomationProperties.SetName(button,"Simulator connection: "+status);
    }
}

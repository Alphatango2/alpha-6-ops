using System;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    // Existing lifecycle/diagnostic callers retain their controls through the new frame.
    internal TextBlock VersionText => Shell.VersionText;
    internal TextBlock FooterBrand => Shell.FooterBrand;
    internal Button ExitOpsButton => Shell.ExitButton;

    private void InitializeShell()
    {
        // Compose once after XAML creates the legacy names in MainWindow's scope.
        ShellComposition.Children.Remove(LegacyWorkspaceContent);
        ShellComposition.Children.Remove(HeaderStatusGrid);
        NameScope.SetNameScope(LegacyWorkspaceContent,NameScope.GetNameScope(this));
        NameScope.SetNameScope(HeaderStatusGrid,NameScope.GetNameScope(this));
        Shell.Workspace=LegacyWorkspaceContent;
        Shell.Utilities=HeaderStatusGrid;
        Shell.Navigation.Requested+=NavigateShell;
        Shell.ConnectionRequested+=(_,_)=>QuickConnect_Click(Shell.ConnectionButton,new RoutedEventArgs());
        Shell.ExitRequested+=(_,_)=>Exit_Click(Shell.ExitButton,new RoutedEventArgs());
    }

    private void NavigateShell(ShellSection section)
    {
        Shell.CloseOverlays();
        switch(section)
        {
            case ShellSection.Operations:
            case ShellSection.Flights:
            case ShellSection.Dispatch:
            case ShellSection.Logbook:
            case ShellSection.Airports:
                PresentWorkspace(section);return;
            case ShellSection.Tools:OpenTools();return;
            case ShellSection.Status:Shell.ShowUtilities();return;
            case ShellSection.Help:
                OpsNoticeWindow.Show(this,"Sierra 4",$"Airline operations platform\n{VersionText.Text}\n\nFlights and Dispatch use your local simulator session. Pilot logbook holds recorded flights.\n\nFlight tools contains simulator controls, your pilot profile and diagnostics. Status & clocks contains local weather and dated UTC/local clocks.\n\nCrew and Maintenance remain demonstration workspaces; their labels identify sample data.");return;
            default:OpenLegacyModule(section switch {
                ShellSection.Crew=>"Crews",ShellSection.Fleet=>"Aircraft",ShellSection.OperationsDemo=>"Operations",
                ShellSection.Occ=>"OCC",_=>section.ToString() });return;
        }
    }

    // The only content-visibility router. It has no operational commands or ownership.
    private void PresentWorkspace(ShellSection section)
    {
        ToolsOverlay.Visibility=Visibility.Collapsed;
        Shell.Navigation.SelectContent(section);
        ShellUnavailable.Visibility=section==ShellSection.Airports?Visibility.Visible:Visibility.Collapsed;
        LegacyViewport.Visibility=section is ShellSection.Airports or ShellSection.Operations?Visibility.Collapsed:Visibility.Visible;
        OperationsView.Visibility=section==ShellSection.Operations?Visibility.Visible:Visibility.Collapsed;
        DashboardScroll.Visibility=OperationsView.Visibility;
        FlightTrackingView.Visibility=section==ShellSection.Flights?Visibility.Visible:Visibility.Collapsed;
        DispatchView.Visibility=section==ShellSection.Dispatch?Visibility.Visible:Visibility.Collapsed;
        PilotLogbookView.Visibility=section==ShellSection.Logbook?Visibility.Visible:Visibility.Collapsed;
        LegacyViewport.ScrollToTop();LegacyViewport.ScrollToHorizontalOffset(0);
        if(section==ShellSection.Operations){RefreshOperations();DashboardScroll.ScrollToTop();}
        if(section==ShellSection.Flights)RefreshFlightTrackingWorkspace(liveLast,flightState.Phase,StatusText.Text);
        if(section==ShellSection.Dispatch)UpdateDispatchState();
        if(section==ShellSection.Logbook)PilotLogbookView.Render(flightHistory);
    }
}

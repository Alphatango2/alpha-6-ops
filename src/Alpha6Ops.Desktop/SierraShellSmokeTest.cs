using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop;

internal static class SierraShellSmokeTest
{
    internal static async Task RunAsync(MainWindow window,string outputDirectory,Action<bool,string> check)
    {
        var shell=window.Shell;
        foreach(var key in new[]{"AppBackground","SurfacePrimary","SurfaceSecondary","SurfaceElevated","TextPrimary","TextSecondary","TextMuted","SierraYellow","Divider","Success","Warning","Critical","Information"})
            check(window.FindResource("S4."+key) is SolidColorBrush,$"Sierra semantic color {key} is available");
        foreach(var key in new[]{"ApplicationTitle","PageTitle","SectionHeader","Metric","Body","Secondary","Table","Metadata","Label","Status","Caption","PrimaryButton","SecondaryButton","IconButton","NavigationItem","SearchInput","StatusIndicator","DataPanel","OperationalAlert","Tabs","Tab","Dropdown","Tooltip","Dialog","State"})
        {
            try { check(window.FindResource("S4."+key) is Style,$"Sierra reusable style {key} is available"); }
            catch(Exception error) { throw new InvalidOperationException("Sierra style failed to resolve: "+key,error); }
        }
        check(shell.Navigation.Destinations.Select(d=>d.Section).Distinct().Count()==shell.Navigation.Destinations.Count,"Navigation destinations are unique");
        foreach(var (query,target) in new[]{(" FLIGHTS ",ShellSection.Flights),("dispatch",ShellSection.Dispatch),("preferences",ShellSection.Settings),("diagnostics",ShellSection.Tools)})
            check(shell.Navigation.Search(query).Any(d=>d.Section==target),$"Workspace search resolves {query.Trim()}");
        shell.SearchButton.Focus();shell.OpenSearch();window.UpdateLayout();
        check(shell.WorkspaceSearch.IsKeyboardFocused,"Search begins at the keyboard input");
        shell.WorkspaceSearch.Text="no-such-workspace";
        check(shell.SearchResults.Items.Count==0&&shell.SearchEmpty.Visibility==Visibility.Visible,"Search has an honest empty state");
        shell.WorkspaceSearch.Text="Flights";shell.ActivateSearchResult();window.UpdateLayout();
        check(shell.SearchOverlay.Visibility==Visibility.Collapsed&&window.FlightTrackingView.Visibility==Visibility.Visible&&shell.WorkspaceTitle.Text=="Flights","Search activates the existing tracking page through typed navigation");
        var tracking=window.FlightTrackingView;var dispatch=window.DispatchView;
        foreach(var target in new[]{ShellSection.Operations,ShellSection.Dispatch,ShellSection.Logbook,ShellSection.Airports,ShellSection.Flights})
        {
            shell.Navigation.Request(target);window.UpdateLayout();
            check(shell.Navigation.Current==target&&shell.Navigation.Destinations.Count(d=>d.IsSelected)==1,$"Shell selects only {target}");
        }
        check(ReferenceEquals(tracking,window.FlightTrackingView)&&ReferenceEquals(dispatch,window.DispatchView),"Navigation preserves existing workspace instances");
        shell.Navigation.Request(ShellSection.Airports);window.UpdateLayout();
        check(window.ShellUnavailable.Kind==SierraStateKind.Unavailable&&window.LegacyViewport.Visibility==Visibility.Collapsed,"Airports is explicitly unavailable without fabricated operations");
        DashboardSmokeTest.Capture(window,Path.Combine(outputDirectory,"sierra-unavailable.png"));
        shell.SearchButton.Focus();shell.OpenSearch();shell.CloseOverlays();
        check(shell.SearchButton.IsKeyboardFocused,"Closing search restores prior keyboard focus");
        shell.ProfileButton.Focus();shell.OpenSearch();shell.WorkspaceSearch.Text="Dispatch";shell.OpenSearch();
        check(shell.WorkspaceSearch.Text=="Dispatch","Repeated search shortcut preserves the entered query");
        shell.CloseOverlays();check(shell.ProfileButton.IsKeyboardFocused,"Repeated search shortcut preserves original external focus");
        shell.ProfileButton.Focus();shell.ShowUtilities();shell.OpenSearch();shell.CloseOverlays();
        check(shell.ProfileButton.IsKeyboardFocused,"Switching overlays returns focus to the original external control");
        window.ShowDashboard();
        await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        await GalleryAsync(window,outputDirectory,check);
    }

    private static async Task GalleryAsync(Window owner,string directory,Action<bool,string> check)
    {
        // Diagnostic-only fixture. Never added to production navigation or operational data.
        Style Style(string key)=>(Style)owner.FindResource("S4."+key);
        TextBlock Text(string text,string style="Body")=>new(){Text=text,Style=Style(style),Margin=new Thickness(0,0,0,12)};
        var body=new StackPanel{Margin=new Thickness(28)};
        body.Children.Add(Text("Sierra 4 · component review","PageTitle"));
        body.Children.Add(Text("Diagnostic specimens · no live or sample airline operations","Secondary"));
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,18)};
        foreach(var key in new[]{"PrimaryButton","SecondaryButton","IconButton"})buttons.Children.Add(new Button{Style=Style(key),Content=key=="IconButton"?"+":key=="PrimaryButton"?"Primary action":"Secondary action",Margin=new Thickness(0,0,12,0)});
        buttons.Children.Add(new Button{Style=Style("SecondaryButton"),Content="Unavailable action",IsEnabled=false});body.Children.Add(buttons);
        body.Children.Add(new TextBox{Style=Style("Input"),Text="Search or enter a value",Margin=new Thickness(0,0,0,16)});
        body.Children.Add(new ComboBox{Style=Style("Dropdown"),ItemsSource=new[]{"Dropdown selection","Alternative"},SelectedIndex=0,Margin=new Thickness(0,0,0,16)});
        var tabs=new TabControl{Style=Style("Tabs"),Margin=new Thickness(0,0,0,16)};
        tabs.Items.Add(new TabItem{Style=Style("Tab"),Header="Current",Content=Text("Active tab content")});tabs.Items.Add(new TabItem{Style=Style("Tab"),Header="History"});body.Children.Add(tabs);
        var table=new DataGrid{Style=Style("Table"),ItemsSource=new[]{new{Field="Example field",State="Available"},new{Field="Another field",State="Unavailable"}},Height=130,Margin=new Thickness(0,0,0,16)};
        table.Columns.Add(new DataGridTextColumn{Header="FIELD",Binding=new System.Windows.Data.Binding("Field"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});table.Columns.Add(new DataGridTextColumn{Header="STATE",Binding=new System.Windows.Data.Binding("State"),Width=220});body.Children.Add(table);
        var states=new WrapPanel();
        foreach(var kind in Enum.GetValues<SierraStateKind>())states.Children.Add(new SierraStatePanel{Style=Style("State"),Kind=kind,Heading=kind+" state",Message="Clear context and a next action belong here.",Width=260});
        body.Children.Add(states);
        var gallery=new Window{Owner=owner,Title="Sierra 4 component review",Width=980,Height=1050,Background=(Brush)owner.FindResource("S4.AppBackground"),Content=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};
        gallery.Show();gallery.UpdateLayout();await gallery.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        check(table.Items.Count==2&&table.ActualHeight>0&&states.Children.Cast<SierraStatePanel>().All(s=>s.Template.FindName("StateLabel",s) is TextBlock),"Sierra component specimens render including all five data states");
        DashboardSmokeTest.Capture(gallery,Path.Combine(directory,"sierra-components.png"));gallery.Close();owner.Activate();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Alpha6Ops.Desktop;

internal static class OpsUi
{
    internal static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    internal static TextBlock Text(string value, double size=13, string color="#D1DFE9") => new()
    { Text=value, FontSize=size, Foreground=Brush(color), TextWrapping=TextWrapping.Wrap, FontFamily=new FontFamily("Bahnschrift SemiCondensed") };
    internal static Button Button(string label, Action action, bool primary=false)
    {
        var button=new Button { Content=label, Style=(Style)Application.Current.FindResource(primary?"OpsPrimary":"OpsButton"), Margin=new Thickness(8,0,0,0) };
        button.Click+=(_,_)=>action(); return button;
    }
    internal static void Configure(Window window, string title, double width=1100, double height=820)
    {
        window.Title="Alpha 6 OPS — "+title;window.Width=width;window.Height=height;window.MinWidth=740;window.MinHeight=560;
        window.WindowStartupLocation=WindowStartupLocation.CenterOwner;window.Background=Brush("#071019");window.Foreground=Brush("#F2F6FA");
        window.UseLayoutRounding=true;
        window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){window.Close();e.Handled=true;}};
    }
}

internal sealed class OperationsWorkspaceWindow : Window
{
    private readonly OpsModule module;
    private readonly DataGrid rows = new();
    private readonly TextBox search = new();
    private readonly ComboBox filter = new();
    private readonly TextBlock count = OpsUi.Text("",11,"#91AABC");
    private readonly TextBlock detailTitle = OpsUi.Text("",21);
    private readonly TextBlock detail = OpsUi.Text("",13);
    private readonly Button? actionButton;
    internal OpsRow? Selected => rows.SelectedItem as OpsRow;
    internal int VisibleRowCount => rows.Items.Count;
    internal double RecordColumnWidth => rows.Columns[1].ActualWidth;
    internal OperationsWorkspaceWindow(OpsModule definition, string? actionLabel=null, Action<OpsRow>? action=null)
    {
        module=definition; OpsUi.Configure(this,definition.Title);
        var root=new DockPanel { Margin=new Thickness(28) };
        var header=new Grid { Margin=new Thickness(0,0,0,22) };header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(190) });
        var heading=new StackPanel();heading.Children.Add(OpsUi.Text(definition.Source,10,"#E3C645"));heading.Children.Add(new TextBlock { Text=definition.Title,FontFamily=new FontFamily("Bahnschrift SemiCondensed"),FontSize=33,Foreground=Brushes.White,Margin=new Thickness(0,10,0,4) });heading.Children.Add(OpsUi.Text(definition.Subtitle,14,"#9EB4C5"));header.Children.Add(heading);
        var photograph=new Border { CornerRadius=new CornerRadius(4),ClipToBounds=true,Height=92,Margin=new Thickness(15,0,0,0),Background=new ImageBrush(new BitmapImage(new Uri($"pack://application:,,,/Assets/Dashboard/{definition.Image}.png"))) { Stretch=Stretch.UniformToFill } };Grid.SetColumn(photograph,1);header.Children.Add(photograph);
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var metrics=new System.Windows.Controls.Primitives.UniformGrid { Columns=3,Margin=new Thickness(0,0,-12,18) };
        foreach(var metric in definition.Metrics)
        {
            var content=new StackPanel();content.Children.Add(OpsUi.Text(metric.Label,11,"#92A9BB"));content.Children.Add(new TextBlock { Text=metric.Value,FontSize=29,Foreground=OpsUi.Brush("#FFDB17"),FontFamily=new FontFamily("Bahnschrift SemiCondensed"),Margin=new Thickness(0,8,0,4) });content.Children.Add(OpsUi.Text(metric.Note,11,"#9AB0C1"));
            metrics.Children.Add(new Border { Child=content,Style=(Style)FindResource("OpsPanel"),Padding=new Thickness(17),Margin=new Thickness(0,0,12,0) });
        }
        DockPanel.SetDock(metrics,Dock.Top);root.Children.Add(metrics);
        var tools=new Grid{Margin=new Thickness(0,0,0,14)};tools.ColumnDefinitions.Add(new ColumnDefinition());tools.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(165)});tools.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        search.Style=(Style)FindResource("OpsInput");search.ToolTip="Search reference, route, status or details";AutomationProperties.SetName(search,"Search workspace records");search.TextChanged+=(_,_)=>ApplyFilter();tools.Children.Add(search);
        filter.ItemsSource=new[]{"All states"}.Concat(definition.Rows.Select(r=>r.State).Distinct()).ToArray();filter.SelectedIndex=0;filter.Margin=new Thickness(12,0,0,0);filter.Padding=new Thickness(9);filter.SelectionChanged+=(_,_)=>ApplyFilter();AutomationProperties.SetName(filter,"Filter workspace status");Grid.SetColumn(filter,1);tools.Children.Add(filter);
        var clear=OpsUi.Button("CLEAR",()=>{search.Clear();filter.SelectedIndex=0;});Grid.SetColumn(clear,2);tools.Children.Add(clear);DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        var footer=new Grid{Margin=new Thickness(0,17,0,0)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});count.VerticalAlignment=VerticalAlignment.Center;footer.Children.Add(count);
        var actions=new StackPanel {Orientation=Orientation.Horizontal};
        if(actionLabel is not null && action is not null)
        {
            actionButton=OpsUi.Button(actionLabel,()=>{if(Selected is {} selected){action(selected);ApplyFilter();}},true);actions.Children.Add(actionButton);
        }
        actions.Children.Add(OpsUi.Button("EXPORT SNAPSHOT",Export));actions.Children.Add(OpsUi.Button("CLOSE",Close));Grid.SetColumn(actions,1);footer.Children.Add(actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var detailContent=new StackPanel();detailContent.Children.Add(OpsUi.Text("SELECTED RECORD",10,"#E3C645"));detailTitle.Margin=new Thickness(0,7,0,8);detailContent.Children.Add(detailTitle);detail.LineHeight=21;detailContent.Children.Add(detail);
        var detailBorder=new Border {Child=new ScrollViewer {Content=detailContent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Height=157,Style=(Style)FindResource("OpsPanel"),Padding=new Thickness(17),Margin=new Thickness(0,14,0,0)};DockPanel.SetDock(detailBorder,Dock.Bottom);root.Children.Add(detailBorder);
        rows.Style=(Style)FindResource("OpsGrid"); rows.Columns.Add(new DataGridTextColumn {Header="REFERENCE",Binding=new Binding("Reference"),Width=130});rows.Columns.Add(new DataGridTextColumn {Header="FLIGHT / ITEM",Binding=new Binding("Item"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});rows.Columns.Add(new DataGridTextColumn {Header="TIMING / CONTEXT",Binding=new Binding("Timing"),Width=175});rows.Columns.Add(new DataGridTextColumn {Header="STATUS",Binding=new Binding("State"),Width=130});
        rows.SelectionChanged+=(_,_)=>UpdateDetail();root.Children.Add(rows);Content=root;ApplyFilter();
        Loaded+=(_,_)=>search.Focus();
    }
    private void UpdateDetail()
    {
        detailTitle.Text=Selected?.Item??"No matching records";
        detail.Text=Selected?.Detail??"Try a different search or select All states to see the complete desk.";
        if(actionButton is not null) actionButton.IsEnabled=Selected is not null;
    }
    internal int Search(string value) {search.Text=value;return VisibleRowCount;}
    internal int FilterState(string value) {filter.SelectedItem=value;return VisibleRowCount;}
    private void ApplyFilter()
    {
        if(rows is null || filter.SelectedItem is null)return;
        var selected=Selected?.Reference;var term=search.Text.Trim();var state=(string)filter.SelectedItem;
        var visible=module.Rows.Where(r=>(state=="All states"||r.State==state) && (term.Length==0||$"{r.Reference} {r.Item} {r.State} {r.Timing} {r.Detail}".Contains(term,StringComparison.OrdinalIgnoreCase))).ToArray();
        rows.ItemsSource=visible;rows.SelectedItem=visible.FirstOrDefault(r=>r.Reference==selected)??visible.FirstOrDefault();count.Text=$"{visible.Length} OF {module.Rows.Count} RECORDS";UpdateDetail();
    }
    private void Export()
    {
        var dialog=new Microsoft.Win32.SaveFileDialog {Title="Export desk snapshot",Filter="JSON snapshot (*.json)|*.json",FileName="Alpha6OPS-"+module.Title.Replace(' ','-')+".json"};
        if(dialog.ShowDialog(this)!=true)return;
        try {File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new{exportedUtc=DateTimeOffset.UtcNow,module.Source,module.Title,records=rows.Items.Cast<OpsRow>().ToArray()},new JsonSerializerOptions{WriteIndented=true}));count.Text="SNAPSHOT SAVED";}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException){count.Text="Could not save: "+error.Message;}
    }
}

internal sealed class FlightPreparationWindow : Window
{
    internal static readonly string[] Checklist =
    [
        "Review flight number, aircraft registration and route",
        "Review the SimBrief operational flight plan",
        "Review weather, notices and alternate planning in your briefing",
        "Confirm simulator aircraft and planned UTC times",
        "Complete aircraft-specific preparation and performance checks",
        "Connect OPS at the gate before block-out"
    ];
    private readonly List<CheckBox> boxes=[];
    private readonly TextBlock progress=OpsUi.Text("",13,"#FFDA00");
    private readonly DashboardState state;
    private readonly DashboardFlightRow flight;
    private readonly Action save;
    internal int CompletedCount=>boxes.Count(b=>b.IsChecked==true);
    internal FlightPreparationWindow(DashboardFlightRow selected, DashboardState localState, Action persist, bool preflight, Action? editAssignment=null)
    {
        flight=selected;state=localState;save=persist;OpsUi.Configure(this,preflight?"Preflight preparation":"Flight details",830,800);
        var root=new DockPanel{Margin=new Thickness(28)};
        var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};
        if(editAssignment is not null)footer.Children.Add(OpsUi.Button("EDIT ASSIGNMENT",()=>{Close();editAssignment();}));
        footer.Children.Add(OpsUi.Button("DONE",Close,true));DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var body=new StackPanel();body.Children.Add(OpsUi.Text("FLIGHT BRIEFING  /  "+selected.Leg.ScheduledOut.ToString("dd MMM yyyy").ToUpperInvariant(),11,"#B6A849"));
        body.Children.Add(new TextBlock{Text=$"{selected.Id}    {selected.Route}",FontFamily=new FontFamily("Bahnschrift SemiCondensed"),FontSize=40,Foreground=Brushes.White,Margin=new Thickness(0,12,0,5)});
        body.Children.Add(OpsUi.Text($"{selected.Aircraft}  •  {selected.Status}  •  ALL TIMES UTC",13,"#A6BBCB"));
        var leg=selected.Leg;
        var summary=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Margin=new Thickness(0,23,0,20)};
        foreach(var (label,value) in new[]{("SCHEDULED OUT",leg.ScheduledOut.UtcDateTime.ToString("HH:mm'Z'")),("SCHEDULED IN",leg.ScheduledIn.UtcDateTime.ToString("HH:mm'Z'")),("PLANNED BLOCK",$"{(leg.ScheduledIn-leg.ScheduledOut).TotalMinutes:0} min"),("ESTIMATED / ACTUAL OUT",leg.EstimatedOut.UtcDateTime.ToString("HH:mm'Z'")),("ESTIMATED / ACTUAL IN",leg.EstimatedIn.UtcDateTime.ToString("HH:mm'Z'")),("DEPARTURE / ARRIVAL DELAY",$"{leg.DepartureDelayMinutes:+0;-0;0} / {leg.ArrivalDelayMinutes:+0;-0;0} min")})
        {var item=new StackPanel{Margin=new Thickness(0,0,12,17)};item.Children.Add(OpsUi.Text(label,10,"#9AB0C1"));item.Children.Add(new TextBlock{Text=value,FontSize=22,FontFamily=new FontFamily("Bahnschrift SemiCondensed"),Foreground=Brushes.White,Margin=new Thickness(0,6,0,0)});summary.Children.Add(item);}
        body.Children.Add(summary);body.Children.Add(OpsUi.Text("PERSONAL PREFLIGHT CHECKLIST",20));body.Children.Add(OpsUi.Text("Saved for this flight on this computer. Checklist completion does not release an aircraft or validate performance.",12,"#9AB0C1"));
        if(!state.PreflightChecks.TryGetValue(selected.Key,out var saved)||saved is null)state.PreflightChecks[selected.Key]=saved=[];
        foreach(var label in Checklist)
        {
            var checkbox=new CheckBox{Content=label,IsChecked=saved.Contains(label),Foreground=OpsUi.Brush("#D4E0E8"),FontSize=14,Padding=new Thickness(8,6,0,6),Margin=new Thickness(0,7,0,0)};
            checkbox.Checked+=(_,_)=>Changed();checkbox.Unchecked+=(_,_)=>Changed();boxes.Add(checkbox);body.Children.Add(checkbox);
        }
        progress.Margin=new Thickness(0,18,0,0);body.Children.Add(progress);UpdateProgress();
        root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Content=root;
    }
    internal void SetCheck(int index,bool value)=>boxes[index].IsChecked=value;
    private void UpdateProgress()=>progress.Text=$"{CompletedCount} OF {Checklist.Length} CHECKS COMPLETE";
    private void Changed()
    {
        state.PreflightChecks[flight.Key]=boxes.Where(b=>b.IsChecked==true).Select(b=>(string)b.Content).ToHashSet();
        save();UpdateProgress();
    }
}

internal sealed class NetworkWindow : Window
{
    internal NetworkMap Map { get; }=new();
    internal NetworkWindow()
    {
        OpsUi.Configure(this,"Network overview",1100,720);
        var root=new DockPanel{Margin=new Thickness(25)};
        var heading=new StackPanel{Margin=new Thickness(0,0,0,18)};heading.Children.Add(OpsUi.Text("NETWORK OVERVIEW",30));heading.Children.Add(OpsUi.Text("SCHEMATIC SCENARIO • select a station to focus its connections • drag to pan",12,"#C9B841"));DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var bottom=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,15,0,0)};bottom.Children.Add(OpsUi.Button("RESET VIEW",Map.ResetView));bottom.Children.Add(OpsUi.Button("CLOSE",Close));DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        root.Children.Add(Map);Content=root;
    }
}

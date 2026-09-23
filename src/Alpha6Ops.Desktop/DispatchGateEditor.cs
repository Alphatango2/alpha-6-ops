using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

internal sealed record SavedGate(string Airport,string Gate,string Airline,string Aircraft,string Use,DateTimeOffset LastUsed,int UseCount=1,string? LastFlightKey=null);
internal sealed record GateRecommendation(string Gate,int UseCount,string Reason);

internal sealed class GateHistoryStore(string directory)
{
    private string FilePath=>Path.Combine(directory,"gate-history.json");
    internal List<SavedGate> Load(){try{return File.Exists(FilePath)?JsonSerializer.Deserialize<List<SavedGate>>(File.ReadAllText(FilePath))??[]:[];}catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){return [];}}
    internal void Save(IEnumerable<SavedGate> entries)
    {
        Directory.CreateDirectory(directory);var temp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(entries));
        try
        {
            for(var attempt=0;;attempt++)
            {
                try{File.Move(temp,FilePath,true);return;}
                catch(Exception error)when((error is IOException or UnauthorizedAccessException)&&attempt<4){Thread.Sleep(40*(attempt+1));}
            }
        }
        finally{try{if(File.Exists(temp))File.Delete(temp);}catch(Exception error)when(error is IOException or UnauthorizedAccessException){}}
    }
    internal static string Airline(ActiveFlightPlan plan)=>new string(plan.FlightNumber.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
    internal static string AircraftFamily(string? value)
    {
        var aircraft=AircraftIdentity.CanonicalType(value).ToUpperInvariant();
        if(aircraft.Contains("A220")||aircraft.Contains("A22"))return "AIRBUS A220";
        if(aircraft.Contains("A319")||aircraft.Contains("A320")||aircraft.Contains("A321")||aircraft.Contains("A32"))return "AIRBUS A32X";
        if(aircraft.Contains("A350"))return "AIRBUS A350";if(aircraft.Contains("A380"))return "AIRBUS A380";
        if(aircraft.Contains("737"))return "BOEING 737";if(aircraft.Contains("777"))return "BOEING 777";if(aircraft.Contains("787"))return "BOEING 787";
        if(aircraft.Contains("E170")||aircraft.Contains("E175")||aircraft.Contains("E190")||aircraft.Contains("E195"))return "EMBRAER E-JET";
        if(aircraft.Contains("CRJ"))return "BOMBARDIER CRJ";
        return aircraft;
    }
    internal void Remember(ActiveFlightPlan plan)
    {
        var entries=Load();
        void Add(string airport,string? value,string use)
        {
            var gate=GateAssignmentResolver.Normalize(value);if(gate is null)return;
            var airline=Airline(plan);var aircraft=AircraftIdentity.CanonicalType(plan.AircraftType);var flightKey=$"{plan.FlightNumber}|{plan.Origin}|{plan.Destination}|{plan.PlannedDepartureUtc:O}";
            var index=entries.FindIndex(x=>x.Airport==airport&&x.Gate==gate&&x.Airline==airline&&x.Aircraft==aircraft&&x.Use==use);
            if(index>=0){var current=entries[index];entries[index]=current with{LastUsed=DateTimeOffset.UtcNow,UseCount=current.LastFlightKey==flightKey?Math.Max(1,current.UseCount):Math.Max(1,current.UseCount)+1,LastFlightKey=flightKey};}
            else entries.Add(new(airport,gate,airline,aircraft,use,DateTimeOffset.UtcNow,1,flightKey));
        }
        Add(plan.Origin,plan.DepartureGate,"Departure");Add(plan.Destination,plan.ArrivalGate,"Arrival");Save(entries);
    }
    internal GateRecommendation? Recommend(ActiveFlightPlan plan,string airport,string use)
    {
        var airline=Airline(plan);if(string.IsNullOrWhiteSpace(airline))return null;
        var aircraft=AircraftIdentity.CanonicalType(plan.AircraftType);var family=AircraftFamily(aircraft);
        var candidate=Load().Where(x=>x.Airport.Equals(airport,StringComparison.OrdinalIgnoreCase)&&x.Use.Equals(use,StringComparison.OrdinalIgnoreCase)&&x.Airline.Equals(airline,StringComparison.OrdinalIgnoreCase))
            .GroupBy(x=>x.Gate,StringComparer.OrdinalIgnoreCase)
            .Select(group=>new{Gate=group.Key,Uses=group.Sum(x=>Math.Max(1,x.UseCount)),AircraftUses=group.Where(x=>AircraftFamily(x.Aircraft)==family).Sum(x=>Math.Max(1,x.UseCount)),LastUsed=group.Max(x=>x.LastUsed)})
            .Where(x=>x.Uses>=2)
            .OrderByDescending(x=>x.AircraftUses).ThenByDescending(x=>x.Uses).ThenByDescending(x=>x.LastUsed).FirstOrDefault();
        if(candidate is null)return null;
        var aircraftReason=candidate.AircraftUses>0?$" • {candidate.AircraftUses} with {family}":"";
        return new(candidate.Gate,candidate.Uses,$"LOCAL HISTORY • {airline} used gate {candidate.Gate} {candidate.Uses} times{aircraftReason}");
    }
}

public sealed class DispatchGateEditor : UserControl
{
    private readonly TextBox departure=new(){Width=150,MinHeight=34,MaxLength=6,Padding=new Thickness(9,6,9,6)},arrival=new(){Width=150,MinHeight=34,MaxLength=6,Padding=new Thickness(9,6,9,6)};
    private readonly TextBlock departureLabel=new(),arrivalLabel=new(),departureRecommendation=new(),arrivalRecommendation=new();
    private readonly Button departureUse=new(){Content="USE",Padding=new Thickness(8,3,8,3),Margin=new Thickness(6,0,0,0)},arrivalUse=new(){Content="USE",Padding=new Thickness(8,3,8,3),Margin=new Thickness(6,0,0,0)};
    private readonly StackPanel fields=new(){Orientation=Orientation.Horizontal},left=new(),right=new();
    private readonly Button save=new(){Content="SAVE ASSIGNMENTS",Padding=new Thickness(12,6,12,6)};
    private ActiveFlightPlan? plan;private string? identity;private GateRecommendation? recommendedDeparture,recommendedArrival;
    internal GateHistoryStore? Store {get;set;}
    internal event EventHandler<ActiveFlightPlan>? Saved;
    internal string Departure {get=>departure.Text;set=>departure.Text=value;}
    internal string Arrival {get=>arrival.Text;set=>arrival.Text=value;}
    internal string? RecommendedDeparture=>recommendedDeparture?.Gate;
    internal string? RecommendedArrival=>recommendedArrival?.Gate;

    public DispatchGateEditor()
    {
        var panel=new StackPanel{Margin=new Thickness(0,20,0,0)};
        var heading=new StackPanel();
        heading.Children.Add(new Border{BorderBrush=OpsUi.Brush("#FFDA00"),BorderThickness=new Thickness(3,0,0,0),Padding=new Thickness(10,0,0,0),Child=new TextBlock{Text="GATE ASSIGNMENTS",Foreground=OpsUi.Brush("#F2F6FA"),FontSize=17,FontWeight=FontWeights.SemiBold}});
        heading.Children.Add(new TextBlock{Text="OPTIONAL • LOCAL HISTORY IS NOT LIVE AVAILABILITY",Foreground=OpsUi.Brush("#9FB2C0"),FontSize=9,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(13,5,0,0)});panel.Children.Add(heading);
        foreach(var label in new[]{departureLabel,arrivalLabel}){label.Foreground=OpsUi.Brush("#9BC8E8");label.FontSize=11;label.Margin=new Thickness(0,0,0,6);}
        foreach(var label in new[]{departureRecommendation,arrivalRecommendation}){label.Foreground=OpsUi.Brush("#91A4B4");label.FontSize=10;label.Margin=new Thickness(0,6,0,0);label.TextWrapping=TextWrapping.Wrap;}
        FrameworkElement Input(TextBox box){var row=new StackPanel{Orientation=Orientation.Horizontal};var choices=new Button{Content="▼",Width=38,ToolTip="Select a previously used gate",Margin=new Thickness(6,0,0,0)};choices.Click+=(_,_)=>{if(box.ContextMenu is{} menu){menu.PlacementTarget=choices;menu.IsOpen=true;}};row.Children.Add(box);row.Children.Add(choices);return row;}
        FrameworkElement Recommendation(TextBlock text,Button use){var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(text);row.Children.Add(use);return row;}
        left.Children.Add(departureLabel);left.Children.Add(Input(departure));left.Children.Add(Recommendation(departureRecommendation,departureUse));right.Children.Add(arrivalLabel);right.Children.Add(Input(arrival));right.Children.Add(Recommendation(arrivalRecommendation,arrivalUse));fields.Margin=new Thickness(0,8,0,0);fields.Children.Add(left);fields.Children.Add(right);panel.Children.Add(fields);
        departureUse.Click+=(_,_)=>ConfirmDepartureRecommendation();arrivalUse.Click+=(_,_)=>ConfirmArrivalRecommendation();
        var actions=new WrapPanel{Margin=new Thickness(0,10,0,0)};save.Click+=(_,_)=>{if(plan is not null){try{Saved?.Invoke(this,Apply(plan));}catch(Exception e)when(e is IOException or ArgumentException or UnauthorizedAccessException){OpsNoticeWindow.Show(Window.GetWindow(this),"Dispatch gates",e.Message,true);}}};actions.Children.Add(save);
        var history=new Button{Content="MANAGE SAVED GATES",Padding=new Thickness(12,6,12,6),Margin=new Thickness(8,0,0,0)};history.Click+=(_,_)=>ManageHistory();actions.Children.Add(history);panel.Children.Add(actions);
        Content=panel;SizeChanged+=(_,_)=>LayoutFields();LayoutFields();
    }
    private void LayoutFields(){var narrow=ActualWidth is>0 and<500;fields.Orientation=narrow?Orientation.Vertical:Orientation.Horizontal;left.Width=right.Width=narrow?Math.Max(210,ActualWidth):250;right.Margin=narrow?new Thickness(0,14,0,0):new Thickness(24,0,0,0);}
    internal void Present(ActiveFlightPlan value,bool canSave,bool force=false)
    {
        var key=$"{value.FlightNumber}|{value.Origin}|{value.Destination}|{value.PlannedDepartureUtc:O}";
        if(force||identity!=key){Departure=value.DepartureGate??"";Arrival=value.ArrivalGate??"";identity=key;}
        plan=value;save.Visibility=canSave?Visibility.Visible:Visibility.Collapsed;departureLabel.Text=$"DEPARTURE · {value.Origin}";arrivalLabel.Text=$"ARRIVAL · {value.Destination}";
        var dep=Departure;var arr=Arrival;
        string[] Suggestions(string airport)=>(Store?.Load()??[]).Where(x=>x.Airport==airport).OrderByDescending(x=>x.Airline==GateHistoryStore.Airline(value)).ThenByDescending(x=>x.Aircraft==AircraftIdentity.CanonicalType(value.AircraftType)).ThenByDescending(x=>x.LastUsed).Select(x=>x.Gate).Distinct().ToArray();
        void Choices(TextBox box,string airport){var menu=new ContextMenu();foreach(var suggestion in Suggestions(airport)){var item=new MenuItem{Header=suggestion};item.Click+=(_,_)=>box.Text=suggestion;menu.Items.Add(item);}if(menu.Items.Count==0)menu.Items.Add(new MenuItem{Header="No saved gates yet",IsEnabled=false});box.ContextMenu=menu;}
        Choices(departure,value.Origin);Choices(arrival,value.Destination);Departure=dep;Arrival=arr;
        recommendedDeparture=Store?.Recommend(value,value.Origin,"Departure");recommendedArrival=Store?.Recommend(value,value.Destination,"Arrival");
        PresentRecommendation(departureRecommendation,departureUse,recommendedDeparture);PresentRecommendation(arrivalRecommendation,arrivalUse,recommendedArrival);
        departure.ToolTip=arrival.ToolTip="Optional • type a gate or select a previously used gate. Availability is not verified.";
    }
    private static void PresentRecommendation(TextBlock text,Button button,GateRecommendation? value){text.Text=value?.Reason??"NO LOCAL RECOMMENDATION";button.Visibility=value is null?Visibility.Collapsed:Visibility.Visible;}
    internal void ConfirmDepartureRecommendation(){if(recommendedDeparture is{} value)Departure=value.Gate;}
    internal void ConfirmArrivalRecommendation(){if(recommendedArrival is{} value)Arrival=value.Gate;}
    internal ActiveFlightPlan Apply(ActiveFlightPlan value)
    {
        string? Parse(string text){var normalized=GateAssignmentResolver.Normalize(text);if(normalized is null&&!string.IsNullOrWhiteSpace(text)&&!new[]{"NA","N/A","—"}.Contains(text.Trim().ToUpperInvariant()))throw new ArgumentException("Use up to six letters or numbers for each gate, or leave blank.");return normalized;}
        return value with{DepartureGate=Parse(Departure),ArrivalGate=Parse(Arrival),GateAssignmentSource="Dispatch",GateAssignmentConfidence="Assigned"};
    }
    private void ManageHistory()
    {
        if(Store is null)return;var entries=Store.Load();var window=new Window{Title="Saved gates",Width=620,Height=430,Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner,Style=(Style)FindResource("OpsWindow"),Background=OpsUi.Brush("#071019"),Foreground=OpsUi.Brush("#F2F6FA")};
        var panel=new DockPanel{Margin=new Thickness(22)};window.Content=panel;var tools=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(tools,Dock.Bottom);panel.Children.Add(tools);
        var list=new ListBox();panel.Children.Add(list);void Refresh()=>list.ItemsSource=entries.Select(x=>$"{x.Airport} · {x.Gate}   {x.Airline} · {x.Aircraft} · {x.Use} · {Math.Max(1,x.UseCount)} uses").ToArray();Refresh();
        var gate=new TextBox{Width=70,MaxLength=6,Margin=new Thickness(0,0,8,0)};tools.Children.Add(gate);list.SelectionChanged+=(_,_)=>gate.Text=list.SelectedIndex>=0?entries[list.SelectedIndex].Gate:"";
        void Persist(){try{Store.Save(entries);Refresh();}catch(Exception e)when(e is IOException or UnauthorizedAccessException){OpsNoticeWindow.Show(window,"Saved gates",e.Message,true);}}
        var update=new Button{Content="UPDATE",Margin=new Thickness(0,0,8,0)};update.Click+=(_,_)=>{if(list.SelectedIndex is var i&&i>=0&&GateAssignmentResolver.Normalize(gate.Text) is{} g){entries[i]=entries[i] with{Gate=g};Persist();}};tools.Children.Add(update);
        var delete=new Button{Content="DELETE"};delete.Click+=(_,_)=>{if(list.SelectedIndex>=0){entries.RemoveAt(list.SelectedIndex);Persist();}};tools.Children.Add(delete);window.ShowDialog();if(plan is not null)Present(plan,save.Visibility==Visibility.Visible);
    }
}

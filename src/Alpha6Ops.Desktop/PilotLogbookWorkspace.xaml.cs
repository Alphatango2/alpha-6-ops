using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

public partial class PilotLogbookWorkspace:UserControl
{
    private PilotLogbookRow[] allRows=[];
    private FlightHistoryDatabase? database;
    private bool settingFilters;
    internal event EventHandler? DashboardRequested;
    internal int VisibleFlightCount=>FlightsGrid.Items.Count;
    public PilotLogbookWorkspace(){InitializeComponent();}

    internal void Render(FlightHistoryDatabase? database)
    {
        this.database=database;
        ErrorText.Visibility=Visibility.Collapsed;
        try
        {
            var flights=database?.ReadRecentFlights(1000)??[];
            allRows=flights.Select(ToRow).ToArray();SetFilterChoices();ApplyFilters();
            TotalText.Text=allRows.Length.ToString("N0");CompletedText.Text=flights.Count(IsComplete).ToString("N0");OpenText.Text=flights.Count(f=>!IsComplete(f)).ToString("N0");
            HoursText.Text=FormatTotalHours(allRows.Sum(row=>row.DurationMinutes));
        }
        catch(Exception error){FlightsGrid.Visibility=EmptyState.Visibility=Visibility.Collapsed;ErrorText.Text="The local pilot logbook could not be opened. "+error.GetBaseException().Message;ErrorText.Visibility=Visibility.Visible;}
    }

    private void SetFilterChoices()
    {
        settingFilters=true;
        var aircraft=AircraftFilter.SelectedItem as string??"ALL AIRCRAFT";var year=YearFilter.SelectedItem as string??"ALL YEARS";var source=SourceFilter.SelectedItem as string??"ALL SOURCES";var status=StatusFilter.SelectedItem as string??"ALL STATUSES";
        SetItems(AircraftFilter,"ALL AIRCRAFT",allRows.Select(row=>row.Aircraft));SetItems(YearFilter,"ALL YEARS",allRows.Where(row=>row.StartedUtc!=DateTimeOffset.MinValue).Select(row=>row.StartedUtc.Year.ToString()).OrderByDescending(value=>value));SetItems(SourceFilter,"ALL SOURCES",allRows.Select(row=>row.Source));SetItems(StatusFilter,"ALL STATUSES",allRows.Select(row=>row.Status));
        SelectOrFirst(AircraftFilter,aircraft);SelectOrFirst(YearFilter,year);SelectOrFirst(SourceFilter,source);SelectOrFirst(StatusFilter,status);settingFilters=false;
    }

    private static void SetItems(ComboBox box,string first,IEnumerable<string> values){box.Items.Clear();box.Items.Add(first);foreach(var value in values.Where(value=>!string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value=>value))box.Items.Add(value);}
    private static void SelectOrFirst(ComboBox box,string value)=>box.SelectedItem=box.Items.Cast<string>().FirstOrDefault(item=>string.Equals(item,value,StringComparison.OrdinalIgnoreCase))??box.Items[0];
    private void Filter_Changed(object sender,RoutedEventArgs e){if(!settingFilters)ApplyFilters();}

    private void ApplyFilters()
    {
        if(FlightsGrid is null)return;
        var query=SearchBox.Text.Trim();var aircraft=AircraftFilter.SelectedItem as string;var year=YearFilter.SelectedItem as string;var source=SourceFilter.SelectedItem as string;var status=StatusFilter.SelectedItem as string;
        var filtered=allRows.Where(row=>(query.Length==0||row.SearchText.Contains(query,StringComparison.OrdinalIgnoreCase))&&(aircraft is null||aircraft=="ALL AIRCRAFT"||row.Aircraft==aircraft)&&(year is null||year=="ALL YEARS"||row.StartedUtc.Year.ToString()==year)&&(source is null||source=="ALL SOURCES"||row.Source==source)&&(status is null||status=="ALL STATUSES"||row.Status==status)).ToArray();
        FlightsGrid.ItemsSource=filtered;FlightsGrid.Visibility=filtered.Length==0?Visibility.Collapsed:Visibility.Visible;EmptyState.Visibility=filtered.Length==0?Visibility.Visible:Visibility.Collapsed;
        if(filtered.Length==0)FlightsGrid.SelectedItem=null;else if(FlightsGrid.SelectedItem is not PilotLogbookRow selected||!filtered.Contains(selected))FlightsGrid.SelectedItem=filtered[0];
        EmptyTitle.Text=allRows.Length==0?"NO FLIGHTS RECORDED YET":"NO FLIGHTS MATCH THESE FILTERS";EmptyDetail.Text=allRows.Length==0?"Completed and interrupted flight sessions will appear here.":"Clear or adjust the filters to see more flights.";ResultsText.Text=$"SHOWING {filtered.Length:N0} OF {allRows.Length:N0} FLIGHTS";
    }

    private void ClearFilters_Click(object sender,RoutedEventArgs e){settingFilters=true;SearchBox.Clear();AircraftFilter.SelectedIndex=YearFilter.SelectedIndex=SourceFilter.SelectedIndex=StatusFilter.SelectedIndex=0;settingFilters=false;ApplyFilters();}
    private void Back_Click(object sender,RoutedEventArgs e)=>DashboardRequested?.Invoke(this,EventArgs.Empty);

    private void FlightsGrid_SizeChanged(object sender,SizeChangedEventArgs e)
    {
        if(e.NewSize.Width<=0||FlightsGrid.Columns.Count!=7)return;
        var available=Math.Max(620,e.NewSize.Width-14);double[] weights=[.9,.9,.9,1.8,1.35,.95,1.2];var total=weights.Sum();
        for(var index=0;index<weights.Length;index++)FlightsGrid.Columns[index].Width=new DataGridLength(available*weights[index]/total,DataGridLengthUnitType.Pixel);
    }

    private void FlightSelection_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(FlightsGrid.SelectedItem is not PilotLogbookRow row){DetailsPanel.Visibility=Visibility.Collapsed;NoSelectionState.Visibility=Visibility.Visible;return;}
        NoSelectionState.Visibility=Visibility.Collapsed;DetailsPanel.Visibility=Visibility.Visible;
        DetailCallsignText.Text=row.Callsign;DetailOriginText.Text=row.Departure;DetailDestinationText.Text=row.Arrival;DetailAircraftText.Text=row.Aircraft;DetailSourceText.Text=row.Source;DetailDateText.Text=row.Date;DetailDurationText.Text=row.Duration;DetailStatusText.Text=row.Status;
        DetailStatusText.Foreground=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(row.StatusColor)!;DetailStatusBadge.Background=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(row.StatusBackground)!;DetailStatusBadge.BorderBrush=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(row.StatusBorder)!;
        var rawEvents=database?.ReadFlightEvents(row.Id)??[];var events=rawEvents.Select(ToEventRow).ToArray();DetailEventsList.ItemsSource=events;NoEventsNotice.Visibility=events.Length==0?Visibility.Visible:Visibility.Collapsed;
        if(events.Length==0)NoEventsText.Text=row.Source=="FLYVIRTUAL IMPORT"?"FlyVirtual preserved the flight summary, duration, and acceptance result. Detailed simulator telemetry was not included in the historical PIREP.":"No detailed operational events were recorded for this flight.";
        DetailTimesText.Text=OperationalTimes(row,events);DetailPerformanceText.Text=PerformanceSummary(row,events);DetailRouteText.Text=string.IsNullOrWhiteSpace(row.Route)?$"{row.Departure} → {row.Arrival}":row.Route;
    }

    private static string OperationalTimes(PilotLogbookRow row,IReadOnlyList<PilotLogbookEventRow> events)
    {
        if(row.Source=="FLYVIRTUAL IMPORT")return "Not recorded by FlyVirtual";
        string At(params string[] names)=>events.FirstOrDefault(item=>names.Any(name=>item.Title.Contains(name,StringComparison.OrdinalIgnoreCase)))?.Time??"—";
        return $"OUT {At("Block-out")}  •  OFF {At("Liftoff","Takeoff")}\nON {At("Touchdown")}  •  IN {At("Block-in","completed")}";
    }

    private static string PerformanceSummary(PilotLogbookRow row,IReadOnlyList<PilotLogbookEventRow> events)
    {
        if(row.Source=="FLYVIRTUAL IMPORT")return "Not recorded by FlyVirtual";
        var relevant=events.Where(item=>item.Title.Contains("Touchdown",StringComparison.OrdinalIgnoreCase)||item.Detail.Contains("FUEL",StringComparison.OrdinalIgnoreCase)).Select(item=>item.Detail).LastOrDefault();
        return relevant??"No fuel or landing figures recorded";
    }

    private static PilotLogbookRow ToRow(FlightHistoryEntry flight)
    {
        var source=Source(flight.Source);var started=ParseDate(flight.StartedUtc);var ended=ParseDate(flight.EndedUtc);var minutes=started.HasValue&&ended.HasValue?Math.Max(0,(ended.Value-started.Value).TotalMinutes):0;
        return new(flight.Id,flight.FlightNumber??"—",flight.Origin??"—",flight.Destination??"—",AircraftName(flight.Aircraft),FormatDate(flight.StartedUtc),FormatShortDate(flight.StartedUtc),FormatDuration(minutes,ended.HasValue),source,Status(flight),StatusColor(flight),StatusBackground(flight),StatusBorder(flight),source=="FLYVIRTUAL IMPORT"?"#9BC8E8":"#D8E3EB",source=="FLYVIRTUAL IMPORT"?"#102232":"#151E24",source=="FLYVIRTUAL IMPORT"?"#315C78":"#40515D",started??DateTimeOffset.MinValue,minutes,$"{flight.FlightNumber} {flight.Origin} {flight.Destination} {flight.Route} {flight.Aircraft} {AircraftName(flight.Aircraft)}",flight.Route,flight.SourceDetail);
    }

    private static PilotLogbookEventRow ToEventRow(FlightHistoryEventEntry entry)
    {
        var time=ParseDate(entry.SimulatorUtc)??ParseDate(entry.RecordedUtc);var title=EventTitle(entry);var detail=EventDetail(entry);
        if(entry.Kind=="tracking_event")try{using var document=JsonDocument.Parse(entry.DetailJson);var root=document.RootElement;if(root.TryGetProperty("Title",out var eventTitle))title=eventTitle.GetString()??title;if(root.TryGetProperty("Detail",out var eventDetail))detail=eventDetail.GetString()??detail;}catch(JsonException){}
        return new(time?.UtcDateTime.ToString("HH:mm:ss'Z'")??"—",title,detail);
    }

    private static string EventTitle(FlightHistoryEventEntry entry)
    {
        if(entry.Kind=="phase")try{using var document=JsonDocument.Parse(entry.DetailJson);if(document.RootElement.TryGetProperty("label",out var label)&&!string.IsNullOrWhiteSpace(label.GetString()))return label.GetString()!;}catch(JsonException){}
        return entry.Kind.Replace('_',' ').ToLowerInvariant() switch{"pirep import"=>"FlyVirtual PIREP imported","session start"=>"Flight session started","session end"=>"Flight session completed","phase change"=>"Flight phase changed",var value=>System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value)};
    }

    private static string EventDetail(FlightHistoryEventEntry entry)
    {
        try
        {
            using var document=JsonDocument.Parse(entry.DetailJson);var root=document.RootElement;
            if(entry.Kind=="pirep_import")
            {
                var status=root.TryGetProperty("status",out var s)?s.GetString():"Accepted";var duration=root.TryGetProperty("flightTime",out var d)?d.GetString():null;var submitted=root.TryGetProperty("submitted",out var date)?date.GetString():null;
                return string.Join(" • ",new[]{status,duration is null?null:$"{duration} flight time",submitted is null?null:$"submitted {submitted}"}.Where(value=>value is not null));
            }
            if(entry.Kind=="phase"&&root.TryGetProperty("phase",out var phase))return $"Flight phase: {SplitWords(phase.GetString()??phase.ToString())}.";
            var parts=new List<string>();foreach(var property in root.EnumerateObject()){if(parts.Count==3)break;if(property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)parts.Add($"{FriendlyLabel(property.Name)} {property.Value.ToString()}");}
            return parts.Count>0?string.Join(" • ",parts):"Operational event recorded by Alpha 6 OPS.";
        }
        catch(JsonException){return "Operational event recorded by Alpha 6 OPS.";}
    }

    private static string FriendlyLabel(string value)=>string.Concat(value.Select((character,index)=>index>0&&char.IsUpper(character)?" "+char.ToLowerInvariant(character):char.ToLowerInvariant(character).ToString()));
    private static string SplitWords(string value)=>string.Concat(value.Select((character,index)=>index>0&&char.IsUpper(character)?" "+character:character.ToString()));

    private static DateTimeOffset? ParseDate(string? value)=>DateTimeOffset.TryParse(value,out var date)?date:null;
    private static string FormatDate(string value)=>ParseDate(value)?.UtcDateTime.ToString("dd MMM yyyy • HH:mm'Z'")??value;
    private static string FormatShortDate(string value)=>ParseDate(value)?.UtcDateTime.ToString("dd MMM yyyy")??value;
    private static string FormatDuration(double minutes,bool hasEnd){var rounded=(int)Math.Round(minutes);return !hasEnd?"—":$"{rounded/60}H {rounded%60:00}M";}
    private static string FormatTotalHours(double minutes)=>$"{(int)Math.Floor(minutes/60):N0} HOURS";
    private static bool IsComplete(FlightHistoryEntry flight)=>string.Equals(flight.FinalPhase,"Complete",StringComparison.OrdinalIgnoreCase);
    private static string Source(string? source)=>source?.ToLowerInvariant() switch{"flyvirtual"=>"FLYVIRTUAL IMPORT","live"=>"ALPHA 6 LIVE","replay" or "flight_lab"=>"FLIGHT LAB",null or ""=>"UNKNOWN",_=>source.ToUpperInvariant()};
    private static string Status(FlightHistoryEntry flight)=>flight.FinalPhase switch{"Complete" when flight.Source.Equals("FlyVirtual",StringComparison.OrdinalIgnoreCase)=>"ACCEPTED","Complete"=>"COMPLETED",null or ""=>"IN PROGRESS","Error" or "Cancelled"=>"INTERRUPTED",_=>flight.FinalPhase.ToUpperInvariant()};
    private static string StatusColor(FlightHistoryEntry flight)=>StatusPalette(Status(flight)).Text;
    private static string StatusBackground(FlightHistoryEntry flight)=>StatusPalette(Status(flight)).Background;
    private static string StatusBorder(FlightHistoryEntry flight)=>StatusPalette(Status(flight)).Border;
    private static (string Text,string Background,string Border) StatusPalette(string status)=>status switch
    {
        "ACCEPTED" or "COMPLETED"=>("#75D88A","#102019","#356443"),"IN PROGRESS" or "ACTIVE"=>("#FFDA00","#28230B","#776A18"),
        "PENDING REVIEW"=>("#74BFFF","#102235","#315E83"),"DELAYED" or "ATTENTION REQUIRED"=>("#FFB04A","#2B1D0E","#805426"),"DIVERTED"=>("#C89BFF","#21152E","#654785"),
        "INTERRUPTED" or "CANCELLED" or "REJECTED" or "INVALID"=>("#FF9690","#281315","#7D363A"),_=>("#B6C0C8","#171E23","#46535C")
    };

    private static string AircraftName(string? aircraft)
    {
        if(string.IsNullOrWhiteSpace(aircraft))return "AIRCRAFT NOT RECORDED";var value=aircraft.Trim().ToUpperInvariant();if(value.Contains(' ')||value.Contains('-'))return value;return AircraftNames.TryGetValue(value,out var name)?name:value;
    }

    private static readonly IReadOnlyDictionary<string,string> AircraftNames=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        ["A20N"]="AIRBUS A320NEO",["A21N"]="AIRBUS A321NEO",["A319"]="AIRBUS A319",["A320"]="AIRBUS A320",["A321"]="AIRBUS A321",["A332"]="AIRBUS A330-200",["A333"]="AIRBUS A330-300",["A338"]="AIRBUS A330-800",["A339"]="AIRBUS A330-900",["A343"]="AIRBUS A340-300",["A346"]="AIRBUS A340-600",["A35K"]="AIRBUS A350-1000",["A359"]="AIRBUS A350-900",["A388"]="AIRBUS A380-800",
        ["B38M"]="BOEING 737 MAX 8",["B39M"]="BOEING 737 MAX 9",["B738"]="BOEING 737-800",["B739"]="BOEING 737-900",["B744"]="BOEING 747-400",["B748"]="BOEING 747-8",["B752"]="BOEING 757-200",["B763"]="BOEING 767-300",["B772"]="BOEING 777-200",["B77L"]="BOEING 777-200LR",["B77W"]="BOEING 777-300ER",["B788"]="BOEING 787-8",["B789"]="BOEING 787-9",["B78X"]="BOEING 787-10",
        ["BCS1"]="AIRBUS A220-100",["BCS3"]="AIRBUS A220-300",["CRJ7"]="BOMBARDIER CRJ-700",["CRJ9"]="BOMBARDIER CRJ-900",["E170"]="EMBRAER E170",["E175"]="EMBRAER E175",["E190"]="EMBRAER E190",["E195"]="EMBRAER E195"
    };
}

internal sealed record PilotLogbookRow(string Id,string Callsign,string Departure,string Arrival,string Aircraft,string Date,string ShortDate,string Duration,string Source,string Status,string StatusColor,string StatusBackground,string StatusBorder,string SourceColor,string SourceBackground,string SourceBorder,DateTimeOffset StartedUtc,double DurationMinutes,string SearchText,string Route,string? SourceDetail);
internal sealed record PilotLogbookEventRow(string Time,string Title,string Detail);

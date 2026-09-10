using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

public partial class PilotLogbookWorkspace:UserControl
{
    internal event EventHandler? DashboardRequested;
    internal int VisibleFlightCount=>FlightsGrid.Items.Count;
    public PilotLogbookWorkspace(){InitializeComponent();}
    internal void Render(FlightHistoryDatabase? database)
    {
        ErrorText.Visibility=Visibility.Collapsed;
        try
        {
            var flights=database?.ReadRecentFlights()??[];
            var rows=flights.Select(f=>new PilotLogbookRow(f.FlightNumber??"—",f.Origin??"—",f.Destination??"—",f.Aircraft,
                FormatDate(f.StartedUtc),f.Source,Status(f.FinalPhase),StatusColor(f.FinalPhase),StatusBackground(f.FinalPhase),StatusBorder(f.FinalPhase))).ToArray();
            FlightsGrid.ItemsSource=rows;FlightsGrid.Visibility=rows.Length==0?Visibility.Collapsed:Visibility.Visible;EmptyState.Visibility=rows.Length==0?Visibility.Visible:Visibility.Collapsed;
            TotalText.Text=rows.Length.ToString();CompletedText.Text=flights.Count(f=>f.FinalPhase=="Complete").ToString();OpenText.Text=flights.Count(f=>f.FinalPhase!="Complete").ToString();
        }
        catch(Exception error)
        {
            FlightsGrid.Visibility=EmptyState.Visibility=Visibility.Collapsed;ErrorText.Text="The local pilot logbook could not be opened. "+error.GetBaseException().Message;ErrorText.Visibility=Visibility.Visible;
        }
    }
    private void Back_Click(object sender,RoutedEventArgs e)=>DashboardRequested?.Invoke(this,EventArgs.Empty);
    private static string FormatDate(string value)=>DateTimeOffset.TryParse(value,out var date)?date.UtcDateTime.ToString("dd MMM yyyy • HH:mm'Z'"):value;
    private static string Status(string? phase)=>phase switch{"Complete"=>"RECORDED",null or ""=>"IN PROGRESS","Error" or "Cancelled"=>"INTERRUPTED",_=>phase.ToUpperInvariant()};
    private static string StatusColor(string? phase)=>phase=="Complete"?"#75D88A":phase is "Error" or "Cancelled"?"#FF9690":"#FFDA00";
    private static string StatusBackground(string? phase)=>phase=="Complete"?"#102019":phase is "Error" or "Cancelled"?"#281315":"#28230B";
    private static string StatusBorder(string? phase)=>phase=="Complete"?"#356443":phase is "Error" or "Cancelled"?"#7D363A":"#776A18";
}

internal sealed record PilotLogbookRow(string Callsign,string Departure,string Arrival,string Aircraft,string Date,string Source,string Status,string StatusColor,string StatusBackground,string StatusBorder);

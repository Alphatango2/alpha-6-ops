using System;using System.Globalization;using System.Text.RegularExpressions;using System.Windows;
namespace Alpha6Ops.Desktop;
public partial class ActiveFlightWindow:Window
{
 internal ActiveFlightPlan? Plan{get;private set;}
 internal ActiveFlightWindow(ActiveFlightPlan? current)
 {
  InitializeComponent();var now=DateTimeOffset.UtcNow;FlightNumberBox.Text=current?.FlightNumber??"";RegistrationBox.Text=current?.Registration??"";OriginBox.Text=current?.Origin??"";DestinationBox.Text=current?.Destination??"";DepartureBox.Text=(current?.PlannedDepartureUtc??now).UtcDateTime.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture);ArrivalBox.Text=(current?.PlannedArrivalUtc??now.AddHours(2)).UtcDateTime.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture);DepartureGateBox.Text=current?.DepartureGate??"";ArrivalGateBox.Text=current?.ArrivalGate??"";RouteBox.Text=current?.Route??"";
 }
 void Save_Click(object sender,RoutedEventArgs e)
 {
  var flight=FlightNumberBox.Text.Trim().ToUpperInvariant();var registration=RegistrationBox.Text.Trim().ToUpperInvariant();var origin=OriginBox.Text.Trim().ToUpperInvariant();var destination=DestinationBox.Text.Trim().ToUpperInvariant();var departureGate=GateAssignmentResolver.Normalize(DepartureGateBox.Text);var arrivalGate=GateAssignmentResolver.Normalize(ArrivalGateBox.Text);var route=NormalizeRoute(RouteBox.Text);
  const DateTimeStyles styles=DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal;
  if(flight.Length is <2 or >10||!Alpha6Ops.Core.FlightIdentity.IsAirportId(origin)||!Alpha6Ops.Core.FlightIdentity.IsAirportId(destination)||!DateTimeOffset.TryParseExact(DepartureBox.Text.Trim(),"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,styles,out var departure)||!DateTimeOffset.TryParseExact(ArrivalBox.Text.Trim(),"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,styles,out var arrival)||arrival<=departure){ErrorText.Text="Enter a flight number, valid 3–8 character airport identifiers, and an arrival later than departure in UTC. Local round trips may use the same airport.";return;}
  Plan=new(flight,registration,origin,destination,departure,arrival,"Pilot entry",DepartureGate:departureGate,ArrivalGate:arrivalGate,GateAssignmentSource:"Pilot entry",GateAssignmentConfidence:"Confirmed",Route:route);DialogResult=true;
 }
 internal static string? NormalizeRoute(string? value){if(string.IsNullOrWhiteSpace(value))return null;return Regex.Replace(value.Trim(),@"\s+"," ").ToUpperInvariant();}
 void PasteRoute_Click(object sender,RoutedEventArgs e){if(Clipboard.ContainsText())RouteBox.Text=Clipboard.GetText();}
 void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}

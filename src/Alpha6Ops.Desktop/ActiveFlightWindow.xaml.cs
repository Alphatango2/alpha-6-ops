using System;using System.Globalization;using System.Text.RegularExpressions;using System.Windows;
namespace Alpha6Ops.Desktop;
public partial class ActiveFlightWindow:Window
{
 internal ActiveFlightPlan? Plan{get;private set;}
 internal ActiveFlightWindow(ActiveFlightPlan? current)
 {
  InitializeComponent();var now=DateTimeOffset.UtcNow;FlightNumberBox.Text=current?.FlightNumber??"";RegistrationBox.Text=current?.Registration??"";OriginBox.Text=current?.Origin??"";DestinationBox.Text=current?.Destination??"";DepartureBox.Text=(current?.PlannedDepartureUtc??now).UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'",CultureInfo.InvariantCulture);ArrivalBox.Text=(current?.PlannedArrivalUtc??now.AddHours(2)).UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'",CultureInfo.InvariantCulture);DepartureGateBox.Text=current?.DepartureGate??"";ArrivalGateBox.Text=current?.ArrivalGate??"";RouteBox.Text=current?.Route??"";
 }
 void Save_Click(object sender,RoutedEventArgs e)
 {
  var flight=FlightNumberBox.Text.Trim().ToUpperInvariant();var registration=RegistrationBox.Text.Trim().ToUpperInvariant();var origin=OriginBox.Text.Trim().ToUpperInvariant();var destination=DestinationBox.Text.Trim().ToUpperInvariant();var departureGate=GateAssignmentResolver.Normalize(DepartureGateBox.Text);var arrivalGate=GateAssignmentResolver.Normalize(ArrivalGateBox.Text);var route=NormalizeRoute(RouteBox.Text);
  if(flight.Length is <2 or >10||!Alpha6Ops.Core.FlightIdentity.IsAirportId(origin)||!Alpha6Ops.Core.FlightIdentity.IsAirportId(destination)||!TryFlightTime(DepartureBox.Text,out var departure)||!TryFlightTime(ArrivalBox.Text,out var arrival)||arrival<=departure){ErrorText.Text="Enter valid airports and dated times with Z or an explicit UTC offset. Arrival must be later than departure; overnight flights need the correct arrival date.";return;}
  Plan=new(flight,registration,origin,destination,departure.ToUniversalTime(),arrival.ToUniversalTime(),"Pilot entry",DepartureGate:departureGate,ArrivalGate:arrivalGate,GateAssignmentSource:"Pilot entry",GateAssignmentConfidence:"Confirmed",Route:route,DepartureUtcOffsetMinutes:(int)departure.Offset.TotalMinutes,ArrivalUtcOffsetMinutes:(int)arrival.Offset.TotalMinutes);DialogResult=true;
 }
 internal static bool TryFlightTime(string value,out DateTimeOffset result)
 {
  var text=value.Trim();var formats=new[]{"yyyy-MM-dd HH:mm'Z'","yyyy-MM-dd HH:mm zzz"};
  var styles=text.EndsWith('Z')?DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal:DateTimeStyles.None;
  return DateTimeOffset.TryParseExact(text,formats,CultureInfo.InvariantCulture,styles,out result);
 }
 internal static string? NormalizeRoute(string? value){if(string.IsNullOrWhiteSpace(value))return null;return Regex.Replace(value.Trim(),@"\s+"," ").ToUpperInvariant();}
 void PasteRoute_Click(object sender,RoutedEventArgs e){if(Clipboard.ContainsText())RouteBox.Text=Clipboard.GetText();}
 void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}

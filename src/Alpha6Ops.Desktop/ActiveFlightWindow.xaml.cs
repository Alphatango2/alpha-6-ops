using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace Alpha6Ops.Desktop;

public partial class ActiveFlightWindow : Window
{
    internal ActiveFlightPlan? Plan { get; private set; }
    internal ActiveFlightWindow(ActiveFlightPlan? current)
    {
        InitializeComponent();
        var now = DateTimeOffset.UtcNow;
        FlightNumberBox.Text = current?.FlightNumber ?? "";
        RegistrationBox.Text = current?.Registration ?? "";
        OriginBox.Text = current?.Origin ?? "";
        DestinationBox.Text = current?.Destination ?? "";
        DepartureBox.Text = (current?.PlannedDepartureUtc ?? now).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ArrivalBox.Text = (current?.PlannedArrivalUtc ?? now.AddHours(2)).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        SimBriefUsernameBox.Text = current?.SimBriefUsername ?? SimBriefImporter.LoadUsername();
    }
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        ImportButton.IsEnabled = false; ImportStatusText.Text = "Downloading latest SimBrief briefing…";
        try
        {
            var imported = await SimBriefImporter.ImportAsync(SimBriefUsernameBox.Text);
            var plan = imported.Plan;
            FlightNumberBox.Text = plan.FlightNumber; RegistrationBox.Text = plan.Registration;
            OriginBox.Text = plan.Origin; DestinationBox.Text = plan.Destination;
            DepartureBox.Text = plan.PlannedDepartureUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            ArrivalBox.Text = plan.PlannedArrivalUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            Plan = plan;
            var age = DateTimeOffset.UtcNow - imported.GeneratedUtc;
            var warning = age > TimeSpan.FromHours(24) ? " Warning: this briefing is more than 24 hours old." : "";
            ImportStatusText.Text = $"{(imported.FromCache ? "Offline cache" : "Imported")} • {imported.AircraftType} • generated {imported.GeneratedUtc.UtcDateTime:dd MMM HH:mm}Z.{warning}";
        }
        catch (Exception error) when (error is IOException or HttpRequestException or JsonException or ArgumentException)
        { ImportStatusText.Text = error.GetBaseException().Message; }
        finally { ImportButton.IsEnabled = true; }
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var flight = FlightNumberBox.Text.Trim().ToUpperInvariant();
        var registration = RegistrationBox.Text.Trim().ToUpperInvariant();
        var origin = OriginBox.Text.Trim().ToUpperInvariant();
        var destination = DestinationBox.Text.Trim().ToUpperInvariant();
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (flight.Length is < 2 or > 10 || origin.Length != 4 || destination.Length != 4 || origin == destination ||
            !DateTimeOffset.TryParseExact(DepartureBox.Text.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, styles, out var departure) ||
            !DateTimeOffset.TryParseExact(ArrivalBox.Text.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, styles, out var arrival) || arrival <= departure)
        { ErrorText.Text = "Enter a flight number, different four-letter ICAO airports, and an arrival later than departure using the shown UTC format."; return; }
        Plan = Plan is { Source: "SimBrief" } imported && imported.FlightNumber == flight && imported.Origin == origin && imported.Destination == destination
            ? imported with { Registration = registration, PlannedDepartureUtc = departure, PlannedArrivalUtc = arrival }
            : new(flight, registration, origin, destination, departure, arrival);
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

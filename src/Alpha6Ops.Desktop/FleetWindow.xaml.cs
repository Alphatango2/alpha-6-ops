using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;
public partial class FleetWindow : Window
{
    private IReadOnlyList<FleetAircraft> aircraft = Array.Empty<FleetAircraft>();
    private bool ready;
    public FleetWindow()
    {
        InitializeComponent();
        try
        {
            aircraft = FleetDatabase.Read();
            SummaryText.Text = $"{aircraft.Count:N0} aircraft • {aircraft.Count(a=>a.Status=="Active"):N0} active • {aircraft.Count(a=>a.Status=="Parked")} parked • Observed {aircraft[0].ObservedDate}";
            FamilyFilter.ItemsSource = new[] { "All families" }.Concat(aircraft.Select(a=>a.Family).Distinct().OrderBy(x=>x)).ToArray();
            StatusFilter.ItemsSource = new[] { "All statuses", "Active", "Parked" };
            FamilyFilter.SelectedIndex = StatusFilter.SelectedIndex = 0;
            ready = true;
            Filter();
        }
        catch (Exception e) when (e is IOException or DllNotFoundException or EntryPointNotFoundException)
        { SummaryText.Text = "Fleet database unavailable: " + e.Message; }
    }
    private void Filter()
    {
        if (!ready) return;
        var search = SearchBox.Text.Trim();
        var filtered = aircraft.Where(a =>
            (FamilyFilter.SelectedIndex == 0 || a.Family == FamilyFilter.SelectedItem?.ToString()) &&
            (StatusFilter.SelectedIndex == 0 || a.Status == StatusFilter.SelectedItem?.ToString()) &&
            (a.Registration.Contains(search,StringComparison.OrdinalIgnoreCase) || a.Model.Contains(search,StringComparison.OrdinalIgnoreCase) || a.SerialNumber.Contains(search,StringComparison.OrdinalIgnoreCase))).ToArray();
        FleetGrid.ItemsSource = filtered;
        CountText.Text = $"{filtered.Length:N0} of {aircraft.Count:N0} aircraft";
        DetailText.Text = "Select an aircraft to inspect its source. Delivery dates are source text (day/month/year when supplied).";
    }
    internal int Search(string text) { SearchBox.Text = text; return FleetGrid.Items.Count; }
    internal int FilterStatus(string status) { StatusFilter.SelectedItem = status; return FleetGrid.Items.Count; }
    private void Filter_Changed(object sender, TextChangedEventArgs e) => Filter();
    private void Selection_Changed(object sender, SelectionChangedEventArgs e) => Filter();
    private void Aircraft_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (FleetGrid.SelectedItem is FleetAircraft a) DetailText.Text = $"{a.Registration} • {a.Family} {a.Model} • MSN {a.SerialNumber} • {a.Status} as observed {a.ObservedDate}. Airfleets delivery date may describe entry into Delta's fleet, not manufacture.";
    }
    private void Source_Click(object sender, RoutedEventArgs e)
    {
        if (FleetGrid.SelectedItem is not FleetAircraft a) { DetailText.Text = "Select an aircraft first."; return; }
        if (Uri.TryCreate(a.SourceUrl,UriKind.Absolute,out var url) && url.Scheme=="https" && url.Host=="www.airfleets.net")
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute=true });
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

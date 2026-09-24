using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Alpha6Ops.Desktop;

internal enum ShellSection { Operations, Flights, Dispatch, Crew, Maintenance, Fleet, Airports, Network, Reports, Logbook, Settings, Help, Messages, Tools, Status, OperationsDemo, Passengers, Weather, Occ }

internal sealed class ShellDestination(ShellSection section, string title, string glyph, string detail, bool primary = true) : INotifyPropertyChanged
{
    public ShellSection Section { get; } = section;
    public string Title { get; } = title;
    public string Glyph { get; } = glyph;
    public string Detail { get; } = detail;
    public bool Primary { get; } = primary;
    private bool selected;
    public bool IsSelected { get => selected; internal set { if(selected == value)return;selected=value;PropertyChanged?.Invoke(this,new(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

// Presentation state only. No operational service, stored flight or simulator reference.
internal sealed class ShellNavigation
{
    public IReadOnlyList<ShellDestination> Destinations { get; } = Array.AsReadOnly(new ShellDestination[] {
        new(ShellSection.Operations,"Operations","\uE80F","Existing operations dashboard"),
        new(ShellSection.Flights,"Flights","\uE709","Live flight tracking"),
        new(ShellSection.Dispatch,"Dispatch","\uE8F1","Review and accept a flight release"),
        new(ShellSection.Crew,"Crew","\uE716","Existing demonstration workspace · opens separately"),
        new(ShellSection.Maintenance,"Maintenance","\uE90F","Existing demonstration workspace · opens separately"),
        new(ShellSection.Fleet,"Fleet","\uE7F4","Existing aircraft catalog · opens separately"),
        new(ShellSection.Airports,"Airports","\uE707","Airport workspace is not yet available"),
        new(ShellSection.Network,"Network","\uE774","Existing network view · opens separately"),
        new(ShellSection.Reports,"Reports","\uE9D9","Existing local flight reports · opens separately"),
        new(ShellSection.Logbook,"Pilot logbook","\uE82D","Recorded flights and PIREPs",false),
        new(ShellSection.Settings,"Settings","\uE713","Application preferences",false),
        new(ShellSection.Help,"Help / About","\uE897","About Sierra 4 and available tools",false),
        new(ShellSection.Messages,"Messages","\uE715","Bundled product notes with local read status",false),
        new(ShellSection.Tools,"Flight tools","\uE115","Simulator controls, profile and diagnostics",false),
        new(ShellSection.Status,"Status & clocks","\uE823","Simulator, local weather and dated clocks",false)
    });
    public ShellSection Current { get; private set; } = ShellSection.Operations;
    public event Action<ShellSection>? Requested;
    public event EventHandler? Changed;
    public ShellNavigation() => SelectContent(ShellSection.Operations);
    public void Request(ShellSection section) => Requested?.Invoke(section);
    public void SelectContent(ShellSection section)
    {
        Current=section;
        foreach(var destination in Destinations)destination.IsSelected=destination.Section==section;
        Changed?.Invoke(this,EventArgs.Empty);
    }
    public IReadOnlyList<ShellDestination> Search(string query) => Destinations.Where(d =>
        d.Title.Contains(query.Trim(),StringComparison.OrdinalIgnoreCase)||d.Detail.Contains(query.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
    public static ShellSection FromLegacy(string name) => name switch {
        "FlightTracking"=>ShellSection.Flights,"PilotLogbook"=>ShellSection.Logbook,"Dispatch"=>ShellSection.Dispatch,
        "Crews"=>ShellSection.Crew,"Maintenance"=>ShellSection.Maintenance,"Aircraft"=>ShellSection.Fleet,
        "Network"=>ShellSection.Network,"Reports"=>ShellSection.Reports,"Settings"=>ShellSection.Settings,
        "Messages"=>ShellSection.Messages,"Operations"=>ShellSection.OperationsDemo,"Passengers"=>ShellSection.Passengers,
        "Weather"=>ShellSection.Weather,"OCC"=>ShellSection.Occ,_=>ShellSection.Help };
}

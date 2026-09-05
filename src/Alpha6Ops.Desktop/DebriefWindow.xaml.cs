using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public partial class DebriefWindow : Window
{
    internal DebriefWindow(FlightPhase phase, IReadOnlyList<FlightEvent> events, IReadOnlyList<PhaseSegment> segments, IReadOnlyList<LegProjection> legs)
    {
        InitializeComponent();
        ResultText.Text = PhaseLabel(phase);
        var leg = legs.FirstOrDefault();
        DelayTagText.Text = leg is null ? "" :
            $"{(leg.DepartureDelayMinutes == 0 ? "On-time out" : $"{leg.DepartureDelayMinutes:+0;-0} min out")} · {(leg.ArrivalDelayMinutes == 0 ? "On-time in" : $"{leg.ArrivalDelayMinutes:+0;-0} min in")}";
        BlockTimeText.Text = segments.Count == 0 ? "Block time unavailable." :
            $"Block time (taxi-out to block-in): {(segments[^1].EndedAt - segments[0].StartedAt).TotalMinutes:0} min";
        SegmentsGrid.ItemsSource = segments.Select(s => new
        {
            Phase = PhaseLabel(s.Phase),
            Started = $"{s.StartedAt.UtcDateTime:HH:mm:ss}Z",
            Ended = $"{s.EndedAt.UtcDateTime:HH:mm:ss}Z",
            Duration = $"{(s.EndedAt - s.StartedAt).TotalMinutes:0} min"
        }).ToArray();
        EventsList.ItemsSource = events.Select(e => $"{PhaseLabel(e.Phase)}   {e.At.UtcDateTime:HH:mm:ss}Z").ToArray();
    }

    private static string PhaseLabel(FlightPhase phase) => phase switch
    {
        FlightPhase.AtGate => "At gate", FlightPhase.TaxiOut => "Block-out / taxi out",
        FlightPhase.Airborne => "Takeoff / airborne", FlightPhase.TaxiIn => "Landing / taxi in",
        FlightPhase.Complete => "Block-in / complete", _ => phase.ToString()
    };

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

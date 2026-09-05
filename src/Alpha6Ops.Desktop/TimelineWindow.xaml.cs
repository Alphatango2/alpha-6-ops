using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public partial class TimelineWindow : Window
{
    private readonly FlightTimeline timeline;
    private readonly ObservableCollection<string> firedEvents = new();

    internal TimelineWindow(FlightTimeline timeline)
    {
        InitializeComponent();
        this.timeline = timeline;
        EventsList.ItemsSource = firedEvents;
        ScrubSlider.Maximum = timeline.Snapshots.Count - 1;
        Render(0);
    }

    private void Scrub_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => Render((int)e.NewValue);

    private void Render(int index)
    {
        var snapshot = timeline.Snapshots[index];
        SampleText.Text = $"Sample {index + 1} of {timeline.Snapshots.Count}";
        PhaseValueText.Text = PhaseLabel(snapshot.Phase);
        TimeTagText.Text = $"{snapshot.Sample.At.UtcDateTime:HH:mm:ss}Z";
        SpeedText.Text = $"{snapshot.Sample.GroundSpeedKnots:0.#} kt";
        GroundText.Text = snapshot.Sample.OnGround ? "Yes" : "No";
        BrakeText.Text = snapshot.Sample.ParkingBrake ? "Set" : "Released";
        EnginesText.Text = snapshot.Sample.EnginesRunning ? "Running" : "Off";
        firedEvents.Clear();
        foreach (var e in timeline.Events.Take(snapshot.EventsFiredCount))
            firedEvents.Add($"{PhaseLabel(e.Phase)}   {e.At.UtcDateTime:HH:mm:ss}Z");
    }

    private static string PhaseLabel(FlightPhase phase) => phase switch
    {
        FlightPhase.AtGate => "At gate", FlightPhase.TaxiOut => "Block-out / taxi out",
        FlightPhase.Airborne => "Takeoff / airborne", FlightPhase.TaxiIn => "Landing / taxi in",
        FlightPhase.Complete => "Block-in / complete", _ => phase.ToString()
    };

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

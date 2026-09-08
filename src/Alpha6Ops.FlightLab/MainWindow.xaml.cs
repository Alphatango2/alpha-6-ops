using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.FlightLab;

public partial class MainWindow : Window
{
    private readonly FlightLabServer server;
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    private DateTimeOffset simulatorUtc=DateTimeOffset.UtcNow;
    private int rate=1,automaticIndex=-1,automaticTicks;
    private bool frozen;
    private string aircraft="Alpha 6 Test A321",phase="AT GATE";
    private string? scenarioEvent;
    private FlightState state=new(true,0,true,false,false,false);
    private readonly (string Phase,int Seconds)[] automatic=
    [
        ("AtGate",5),("EngineStart",4),("Pushback",5),("TaxiOut",8),("Takeoff",5),("Climb",8),
        ("Cruise",15),("Descend",8),("Approach",6),("Land",5),("TaxiIn",8),("Complete",5)
    ];

    public MainWindow()
    {
        InitializeComponent();
        server=new FlightLabServer(message=>Dispatcher.BeginInvoke(()=>ServerStatusText.Text=message));
        server.Start();timer.Tick+=Tick;timer.Start();ApplyPhase("AtGate");
        Closed+=(_,_)=>server.Dispose();
    }

    private void Tick(object? sender,EventArgs e)
    {
        simulatorUtc=simulatorUtc.AddSeconds(rate);
        if(automaticIndex>=0)
        {
            automaticTicks++;
            if(automaticTicks>=automatic[automaticIndex].Seconds)
            {
                automaticIndex++;
                if(automaticIndex>=automatic.Length){automaticIndex=-1;ScenarioText.Text="Automatic flight complete";}
                else{automaticTicks=0;ApplyPhase(automatic[automaticIndex].Phase);ScenarioText.Text=$"Quick flight running • step {automaticIndex+1}/{automatic.Length}";}
            }
        }
        Publish();scenarioEvent=null;
    }

    private void Publish()
    {
        SimulatorTimeText.Text=simulatorUtc.UtcDateTime.ToString("HH:mm:ss'Z'");
        TelemetryText.Text=$"{state.Speed:0} kt • Brake {(state.Brake?"set":"released")} • Engines {(state.Engines?"running":"off")} • {(state.OnGround?"on ground":"airborne")}";
        server.Update(new FlightLabFrame(FlightLabProtocol.SchemaVersion,aircraft,simulatorUtc,state.OnGround,state.Speed,state.Brake,state.Engines,state.Paused,state.Slewing,phase,scenarioEvent));
    }

    private void ApplyPhase(string value)
    {
        (phase,state)=value switch
        {
            "AtGate"=>("AT GATE",new(true,0,true,false,false,false)),
            "EngineStart"=>("ENGINE START",new(true,0,true,true,false,false)),
            "Pushback"=>("PUSHBACK",new(true,4,false,true,false,false)),
            "TaxiOut"=>("TAXI OUT",new(true,18,false,true,false,false)),
            "Takeoff"=>("TAKEOFF",new(false,175,false,true,false,false)),
            "Climb"=>("CLIMB",new(false,285,false,true,false,false)),
            "Cruise"=>("CRUISE",new(false,450,false,true,false,false)),
            "Descend"=>("DESCENT",new(false,300,false,true,false,false)),
            "Approach"=>("APPROACH",new(false,165,false,true,false,false)),
            "Land"=>("LANDING",new(true,128,false,true,false,false)),
            "TaxiIn"=>("TAXI TO GATE",new(true,14,false,true,false,false)),
            "Complete"=>("PARKED",new(true,0,true,false,false,false)),
            "GoAround"=>("GO-AROUND",new(false,175,false,true,false,false)),
            _=>(phase,state)
        };
        PhaseText.Text=phase;Publish();
    }

    private void Phase_Click(object sender,RoutedEventArgs e){automaticIndex=-1;ApplyPhase((string)((Button)sender).Tag);ScenarioText.Text="Manual phase selected";}
    private void RunAutomatic_Click(object sender,RoutedEventArgs e){automaticIndex=0;automaticTicks=0;ApplyPhase(automatic[0].Phase);scenarioEvent="QUICK_FLIGHT_STARTED";ScenarioText.Text=$"Quick flight running • step 1/{automatic.Length}";}
    private void StopAutomatic_Click(object sender,RoutedEventArgs e){automaticIndex=-1;ScenarioText.Text="Automatic flight stopped";}
    private void Diversion_Click(object sender,RoutedEventArgs e){scenarioEvent="DIVERSION_DECLARED_"+simulatorUtc.ToUnixTimeSeconds();ScenarioText.Text="Diversion declared • event sent to Alpha 6 OPS journal";Publish();}
    private void Freeze_Click(object sender,RoutedEventArgs e){frozen=!frozen;server.Frozen=frozen;FreezeButton.Content=frozen?"RESUME DATA":"FREEZE DATA";ScenarioText.Text=frozen?"Telemetry frozen":"Telemetry resumed";}
    private void DropLink_Click(object sender,RoutedEventArgs e){server.DropClient();ScenarioText.Text="Local telemetry link dropped • OPS should reconnect";}
    private void ClockJump_Click(object sender,RoutedEventArgs e){simulatorUtc=simulatorUtc.AddMinutes(10);scenarioEvent="CLOCK_JUMP_10_MINUTES";Publish();ScenarioText.Text="Simulator clock advanced 10 minutes";}
    private void ChangeAircraft_Click(object sender,RoutedEventArgs e){aircraft=aircraft.Contains("A321",StringComparison.Ordinal)?"Alpha 6 Test B738":"Alpha 6 Test A321";AircraftBox.Text=aircraft;scenarioEvent="AIRCRAFT_CHANGED";Publish();ScenarioText.Text="Aircraft identity changed";}
    private void CrashSimulator_Click(object sender,RoutedEventArgs e){server.SetOffline(true);ScenarioText.Text="Simulator crash simulated • local link offline";}
    private void RestartLink_Click(object sender,RoutedEventArgs e){server.SetOffline(false);ScenarioText.Text="Flight Lab link restarted • OPS may reconnect";}
    private void Aircraft_Changed(object sender,TextChangedEventArgs e){if(AircraftBox is null)return;aircraft=string.IsNullOrWhiteSpace(AircraftBox.Text)?"Alpha 6 Test Aircraft":AircraftBox.Text.Trim();}
    private void Rate_Changed(object sender,SelectionChangedEventArgs e){if(RateBox?.SelectedItem is ComboBoxItem item&&int.TryParse(item.Tag?.ToString(),out var selected))rate=selected;}
    private sealed record FlightState(bool OnGround,double Speed,bool Brake,bool Engines,bool Paused,bool Slewing);
}

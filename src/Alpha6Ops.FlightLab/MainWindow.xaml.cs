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
    private int rate=1,automaticIndex=-1,automaticTicks,playbackRate=1;
    private double routeProgress;
    private bool frozen,automaticPaused;
    private string aircraft="Alpha 6 Test A321",phase="AT GATE";
    private string? scenarioEvent;
    private FlightState state=new(true,0,true,false,false,false,0,0,0,1);
    private readonly (string Phase,int Seconds,double Progress)[] automatic=
    [
        ("AtGate",5,0),("EngineStart",4,.005),("Pushback",5,.01),("TaxiOut",8,.03),("Takeoff",5,.06),("Climb",8,.20),
        ("Cruise",15,.58),("Descend",8,.82),("Approach",6,.92),("Land",5,.97),("TaxiIn",8,.99),("Complete",5,1)
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
        if(automaticIndex>=0&&!automaticPaused)
        {
            automaticTicks+=playbackRate;
            if(automaticTicks>=automatic[automaticIndex].Seconds)
            {
                automaticIndex++;
                if(automaticIndex>=automatic.Length){automaticIndex=-1;playbackRate=1;ScenarioText.Text="Automatic flight complete";UpdatePlaybackStatus();}
                else{automaticTicks=0;ApplyPhase(automatic[automaticIndex].Phase);ScenarioText.Text=$"Automatic flight running • step {automaticIndex+1}/{automatic.Length}";UpdatePlaybackStatus();}
            }
        }
        Publish();scenarioEvent=null;
    }

    private void Publish()
    {
        SimulatorTimeText.Text=simulatorUtc.UtcDateTime.ToString("HH:mm:ss'Z'");
        var progress=routeProgress;
        if(automaticIndex>=0&&automaticIndex<automatic.Length-1)progress=Math.Clamp(automatic[automaticIndex].Progress+(automatic[automaticIndex+1].Progress-automatic[automaticIndex].Progress)*automaticTicks/automatic[automaticIndex].Seconds,0,1);
        TelemetryText.Text=$"{state.Speed:0} kt • Route {progress:P0} • Brake {(state.Brake?"set":"released")} • Engines {(state.Engines?"running":"off")} • {(state.OnGround?"on ground":"airborne")}";
        var flaps=phase switch{"TAKEOFF"=>.25,"APPROACH"=>.5,"LANDING"=>1,"TAXI TO GATE"=>1,_=>0};
        var pitch=phase switch{"TAKEOFF"=>10,"CLIMB"=>6,"DESCENT"=>-3,"APPROACH"=>-2,"LANDING"=>-1,_=>0};
        var bank=phase is "CLIMB" or "DESCENT"?2:0;
        var fuel=Math.Max(0,34000-9000*progress);
        server.Update(new FlightLabFrame(FlightLabProtocol.SchemaVersion,aircraft,simulatorUtc,state.OnGround,state.Speed,state.Brake,state.Engines,state.Paused,state.Slewing,phase,scenarioEvent,progress,state.Altitude,state.IndicatedSpeed,state.VerticalSpeed,state.Gear,state.Altitude,flaps,pitch,bank,fuel,state.Engines?2:0,state.Engines?3:0));
    }

    private void ApplyPhase(string value)
    {
        routeProgress=value switch{"AtGate"=>0,"EngineStart"=>.005,"Pushback"=>.01,"TaxiOut"=>.03,"Takeoff"=>.06,"Climb"=>.20,"Cruise"=>.58,"Descend"=>.82,"Approach"=>.92,"Land"=>.97,"TaxiIn"=>.99,"Complete"=>1,"GoAround"=>.88,_=>routeProgress};
        (phase,state)=value switch
        {
            "AtGate"=>("AT GATE",new(true,0,true,false,false,false,0,0,0,1)),
            "EngineStart"=>("ENGINE START",new(true,0,true,true,false,false,0,0,0,1)),
            "Pushback"=>("PUSHBACK",new(true,4,false,true,false,false,0,4,0,1)),
            "TaxiOut"=>("TAXI OUT",new(true,18,false,true,false,false,0,18,0,1)),
            "Takeoff"=>("TAKEOFF",new(false,175,false,true,false,false,400,180,1800,1)),
            "Climb"=>("CLIMB",new(false,285,false,true,false,false,12000,290,2200,0)),
            "Cruise"=>("CRUISE",new(false,450,false,true,false,false,35000,290,0,0)),
            "Descend"=>("DESCENT",new(false,300,false,true,false,false,18000,280,-1800,0)),
            "Approach"=>("APPROACH",new(false,165,false,true,false,false,2500,165,-700,1)),
            "Land"=>("LANDING",new(true,128,false,true,false,false,0,125,-300,1)),
            "TaxiIn"=>("TAXI TO GATE",new(true,14,false,true,false,false,0,14,0,1)),
            "Complete"=>("PARKED",new(true,0,true,false,false,false,0,0,0,1)),
            "GoAround"=>("GO-AROUND",new(false,175,false,true,false,false,1800,175,1800,1)),
            _=>(phase,state)
        };
        PhaseText.Text=phase;Publish();
    }

    private void Phase_Click(object sender,RoutedEventArgs e){automaticIndex=-1;automaticPaused=false;playbackRate=1;ApplyPhase((string)((Button)sender).Tag);ScenarioText.Text="Manual phase selected";UpdatePlaybackStatus();}
    private void RunAutomatic_Click(object sender,RoutedEventArgs e)=>StartOrResumeAutomatic();
    private void StopAutomatic_Click(object sender,RoutedEventArgs e){automaticIndex=-1;automaticPaused=false;playbackRate=1;ScenarioText.Text=$"Automatic flight stopped at {phase}";UpdatePlaybackStatus();PlaybackStatusText.Text=$"STOPPED • {phase}";}
    private void Play_Click(object sender,RoutedEventArgs e)=>StartOrResumeAutomatic();
    private void Pause_Click(object sender,RoutedEventArgs e)
    {
        if(automaticIndex<0){ScenarioText.Text="Start automatic flight before pausing";return;}
        automaticPaused=true;ScenarioText.Text=$"Automatic flight paused at {phase}";UpdatePlaybackStatus();
    }
    private void FastForward_Click(object sender,RoutedEventArgs e)
    {
        if(automaticIndex<0)StartAutomatic();
        automaticPaused=false;playbackRate=playbackRate==4?1:4;
        ScenarioText.Text=$"Automatic flight running at {playbackRate}× • step {automaticIndex+1}/{automatic.Length}";UpdatePlaybackStatus();
    }
    private void StartOrResumeAutomatic()
    {
        if(automaticIndex<0)StartAutomatic();
        else{automaticPaused=false;ScenarioText.Text=$"Automatic flight running • step {automaticIndex+1}/{automatic.Length}";UpdatePlaybackStatus();}
    }
    private void StartAutomatic()
    {
        automaticIndex=0;automaticTicks=0;automaticPaused=false;ApplyPhase(automatic[0].Phase);scenarioEvent="QUICK_FLIGHT_STARTED";ScenarioText.Text=$"Automatic flight running • step 1/{automatic.Length}";UpdatePlaybackStatus();Publish();
    }
    private void UpdatePlaybackStatus()
    {
        PlaybackStatusText.Text=automaticIndex<0?"READY • 1× PLAYBACK":automaticPaused?$"PAUSED • STEP {automaticIndex+1}/{automatic.Length}":$"PLAYING • {playbackRate}× • STEP {automaticIndex+1}/{automatic.Length}";
        FastForwardButton.Background=playbackRate==4?new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,218,0)):new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20,34,44));
        FastForwardButton.Foreground=playbackRate==4?System.Windows.Media.Brushes.Black:System.Windows.Media.Brushes.White;
    }
    private void Diversion_Click(object sender,RoutedEventArgs e){scenarioEvent="DIVERSION_DECLARED_"+simulatorUtc.ToUnixTimeSeconds();ScenarioText.Text="Diversion declared • event sent to Alpha 6 OPS journal";Publish();}
    private void Freeze_Click(object sender,RoutedEventArgs e){frozen=!frozen;server.Frozen=frozen;FreezeButton.Content=frozen?"RESUME DATA":"FREEZE DATA";ScenarioText.Text=frozen?"Telemetry frozen":"Telemetry resumed";}
    private void DropLink_Click(object sender,RoutedEventArgs e){server.DropClient();ScenarioText.Text="Local telemetry link dropped • OPS should reconnect";}
    private void ClockJump_Click(object sender,RoutedEventArgs e){simulatorUtc=simulatorUtc.AddMinutes(10);scenarioEvent="CLOCK_JUMP_10_MINUTES";Publish();ScenarioText.Text="Simulator clock advanced 10 minutes";}
    private void ChangeAircraft_Click(object sender,RoutedEventArgs e){aircraft=aircraft.Contains("A321",StringComparison.Ordinal)?"Alpha 6 Test B738":"Alpha 6 Test A321";AircraftBox.Text=aircraft;scenarioEvent="AIRCRAFT_CHANGED";Publish();ScenarioText.Text="Aircraft identity changed";}
    private void CrashSimulator_Click(object sender,RoutedEventArgs e){server.SetOffline(true);ScenarioText.Text="Simulator crash simulated • local link offline";}
    private void RestartLink_Click(object sender,RoutedEventArgs e){server.SetOffline(false);ScenarioText.Text="Flight Lab link restarted • OPS may reconnect";}
    private void Aircraft_Changed(object sender,TextChangedEventArgs e){if(AircraftBox is null)return;aircraft=string.IsNullOrWhiteSpace(AircraftBox.Text)?"Alpha 6 Test Aircraft":AircraftBox.Text.Trim();}
    private void Rate_Changed(object sender,SelectionChangedEventArgs e){if(RateBox?.SelectedItem is ComboBoxItem item&&int.TryParse(item.Tag?.ToString(),out var selected))rate=selected;}
    private void MinimizeWindow_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void ToggleMaximizeWindow_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void Exit_Click(object sender,RoutedEventArgs e)=>Close();
    private sealed record FlightState(bool OnGround,double Speed,bool Brake,bool Engines,bool Paused,bool Slewing,double Altitude,double IndicatedSpeed,double VerticalSpeed,double Gear);
}

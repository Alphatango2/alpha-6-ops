using System;
using System.Collections.Immutable;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop.MapV2;

internal static class FlightTrackPresentationChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var at=DateTimeOffset.Parse("2026-09-30T23:20:00Z");
        var state=new FlightSessionSnapshot
        {
            Connected=true,DataCurrent=true,Aircraft="A350-900 (Default Cabin)",ConnectionGeneration=1,
            ObservedSessionStart=at,Assignment=new ActiveFlightPlan("DAL40","G-NXWB","YSSY","KLAX",at,at.AddHours(14),AircraftType:"A359"),
            Telemetry=new(at,false,480,false,true,LatitudeDegrees:-33.946,LongitudeDegrees:151.177,AltitudeFeet:35000),
            Reading=new LiveReading("A350-900 (Default Cabin)",new(at,false,480,false,true,LatitudeDegrees:-33.946,LongitudeDegrees:151.177,AltitudeFeet:35000),new FlightEvidence(SimulationRunning:true))
        };
        static int Count(FlightTrackPresentation value)=>value.MaterializeSegments().Sum(segment=>segment.Length);

        var endurance=new FlightTrackPresentationHistory();
        for(var second=0;second<=15*60*60;second++)
        {
            var fraction=second/(15d*60*60);
            var longitude=(151.177+90*fraction+180)%360-180;
            var sample=state.Telemetry! with
            {
                At=at.AddSeconds(second),LatitudeDegrees=-33.946+67*fraction+Math.Sin(fraction*Math.PI*12)*.35,
                LongitudeDegrees=longitude
            };
            endurance.Observe(state with{Telemetry=sample,Reading=state.Reading! with{Telemetry=sample}});
        }
        var longTrack=endurance.Capture();var longSegments=longTrack.MaterializeSegments();
        check(Count(longTrack)<=FlightTrackPresentationHistory.MaxPoints,"Fifteen-hour one-second flown track remains inside its hard point budget");
        check(Math.Abs(longSegments[0][0].Longitude-151.177)<1e-9&&Math.Abs(longSegments[0][0].Latitude+33.946)<1e-9,"Fifteen-hour flown track preserves its departure endpoint");
        var expectedLast=state.Telemetry! with{At=at.AddHours(15),LatitudeDegrees=-33.946+67,LongitudeDegrees=(151.177+90+180)%360-180};
        check(longSegments[^1][^1]==new GeoPoint(expectedLast.LongitudeDegrees,expectedLast.LatitudeDegrees),"Fifteen-hour flown track preserves its current endpoint");
        check(Count(longTrack)>1000,"Thirty-second sampling preserves meaningful long-haul route shape");

        var stationary=new FlightTrackPresentationHistory();
        for(var second=0;second<2*60*60;second++)
        {
            var sample=state.Telemetry! with{At=at.AddSeconds(second),OnGround=true,GroundSpeedKnots=0,AltitudeFeet=20};
            stationary.Observe(state with{Telemetry=sample,Reading=state.Reading! with{Telemetry=sample}});
        }
        check(Count(stationary.Capture())==1,"Two hours of stationary preflight consumes one track point");

        var hidden=new FlightTrackPresentationHistory();
        for(var second=0;second<=700;second++)
        {
            var sample=state.Telemetry! with{At=at.AddSeconds(second),LongitudeDegrees=151.177+second*.002};
            hidden.Observe(state with{Telemetry=sample,Reading=state.Reading! with{Telemetry=sample}});
        }
        var hiddenTrack=hidden.Capture();
        check(Count(hiddenTrack)>20&&hiddenTrack.MaterializeSegments()[0][0].Longitude==151.177&&hiddenTrack.MaterializeSegments()[^1][^1].Longitude>152.5,
            "Presentation history accumulates through a workspace absence longer than five minutes");
        var firstRenderer=new MapProjectionService().Project(state with{Telemetry=state.Telemetry! with{At=at.AddSeconds(700),LongitudeDegrees=152.577}},hiddenTrack,false,1,1,at.AddSeconds(700));
        var secondRenderer=new MapProjectionService().Project(state with{Telemetry=state.Telemetry! with{At=at.AddSeconds(700),LongitudeDegrees=152.577}},hiddenTrack,false,2,1,at.AddSeconds(700));
        check(firstRenderer.Routes.Single(route=>route.Kind==MapRouteKind.ObservedTrack).Segments.SequenceEqual(
            secondRenderer.Routes.Single(route=>route.Kind==MapRouteKind.ObservedTrack).Segments,ImmutableArraySequenceComparer.Instance),
            "Renderer recreation reconstructs the same complete flight-scoped trail");

        var interrupted=new FlightTrackPresentationHistory();
        interrupted.Observe(state);
        interrupted.Observe(state with{Telemetry=state.Telemetry! with{At=at.AddSeconds(1),LongitudeDegrees=151.2},Reading=state.Reading! with{Telemetry=state.Telemetry! with{At=at.AddSeconds(1),LongitudeDegrees=151.2}}});
        interrupted.Observe(state with{DataCurrent=false,Telemetry=state.Telemetry! with{At=at.AddSeconds(2),LongitudeDegrees=151.3}});
        interrupted.Observe(state with{Telemetry=state.Telemetry! with{At=at.AddSeconds(3),LongitudeDegrees=151.4},Reading=state.Reading! with{Telemetry=state.Telemetry! with{At=at.AddSeconds(3),LongitudeDegrees=151.4}}});
        check(interrupted.Capture().MaterializeSegments().Length==2,"Invalid/stale telemetry creates a safe flown-track discontinuity");

        var dateline=new FlightTrackPresentationHistory();
        for(var second=0;second<=600;second++)
        {
            var longitude=(179+second/300d+180)%360-180;
            var sample=state.Telemetry! with{At=at.AddSeconds(second),LongitudeDegrees=longitude,LatitudeDegrees=5};
            dateline.Observe(state with{Telemetry=sample,Reading=state.Reading! with{Telemetry=sample}});
        }
        var datelineMap=new MapProjectionService().Project(state with{Telemetry=state.Telemetry! with{At=at.AddSeconds(600),LongitudeDegrees=-179,LatitudeDegrees=5}},dateline.Capture(),false,3,1,at.AddSeconds(600));
        check(datelineMap.Routes.Single(route=>route.Kind==MapRouteKind.ObservedTrack).Segments.All(segment=>segment.Zip(segment.Skip(1)).All(pair=>Math.Abs(pair.First.Longitude-pair.Second.Longitude)<=180)),
            "Flight-scoped trail remains safely split across the dateline");
    }

    private sealed class ImmutableArraySequenceComparer : System.Collections.Generic.IEqualityComparer<ImmutableArray<GeoPoint>>
    {
        internal static readonly ImmutableArraySequenceComparer Instance=new();
        public bool Equals(ImmutableArray<GeoPoint> x,ImmutableArray<GeoPoint> y)=>x.SequenceEqual(y);
        public int GetHashCode(ImmutableArray<GeoPoint> obj)=>obj.Length;
    }
}

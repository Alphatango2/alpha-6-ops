using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop.MapV2;

// Application-presentation lifetime only. This history is derived from immutable
// flight snapshots and is never consulted by lifecycle, persistence or completion.
internal sealed record FlightTrackPresentation(
    ImmutableArray<ImmutableArray<GeoPoint>> CommittedSegments,
    int ActiveSegmentIndex,
    GeoPoint? LiveEndpoint)
{
    internal ImmutableArray<ImmutableArray<GeoPoint>> MaterializeSegments()
    {
        if(LiveEndpoint is not{} endpoint||ActiveSegmentIndex<0||ActiveSegmentIndex>=CommittedSegments.Length)return CommittedSegments;
        var current=CommittedSegments[ActiveSegmentIndex];
        if(current.Length>0&&current[^1]==endpoint)return CommittedSegments;
        var builder=CommittedSegments.ToBuilder();builder[ActiveSegmentIndex]=current.Add(endpoint);return builder.MoveToImmutable();
    }
}

internal sealed class FlightTrackPresentationHistory
{
    internal const int MaxPoints=4096;
    internal static readonly TimeSpan SampleInterval=TimeSpan.FromSeconds(30);
    private const double MinimumMovementNm=.05;
    private const double MaximumSampleDistanceNm=5;
    private readonly List<List<GeoPoint>> segments=[];
    private ImmutableArray<ImmutableArray<GeoPoint>> frozen=[];
    private bool frozenDirty;
    private string? identity;
    private long connectionGeneration=-1;
    private DateTimeOffset? previousAt,lastCommittedAt;
    private GeoPoint? previousPosition,liveEndpoint,lastCommittedPosition;
    private int activeSegment=-1;
    private bool continuityBroken=true,previousAllowed;

    internal void Observe(FlightSessionSnapshot state)
    {
        var currentIdentity=Identity(state);
        if(identity!=currentIdentity){Reset();identity=currentIdentity;}
        if(connectionGeneration>=0&&connectionGeneration!=state.ConnectionGeneration)BreakContinuity();
        connectionGeneration=state.ConnectionGeneration;
        if(state.SuppressObservedFlight){Reset();identity=currentIdentity;connectionGeneration=state.ConnectionGeneration;return;}
        var sample=state.Telemetry;
        var available=sample is{HasPosition:true}&&state.Reading?.Evidence?.SimulationRunning!=false;
        var allowed=available&&state.DataCurrent&&state.Connected&&!sample!.Paused&&!sample.Slewing;
        if(!allowed){if(previousAllowed)BreakContinuity();previousAllowed=false;return;}

        var position=new GeoPoint(sample!.LongitudeDegrees,sample.LatitudeDegrees);
        if(previousAllowed&&previousAt==sample.At&&previousPosition==position){liveEndpoint=position;return;}
        var discontinuity=continuityBroken||!previousAllowed;
        if(previousAt is{} prior)
        {
            var elapsed=sample.At-prior;
            discontinuity|=elapsed<=TimeSpan.Zero||elapsed>TimeSpan.FromSeconds(15);
            if(previousPosition is{} before)
                discontinuity|=DistanceNm(before,position)>2+Math.Max(0,elapsed.TotalHours)*1500;
        }
        if(discontinuity)StartSegment(position,sample.At);
        else UpdateSegment(position,sample.At);
        previousAllowed=true;continuityBroken=false;previousAt=sample.At;previousPosition=position;liveEndpoint=position;
        CompactIfNeeded();
    }

    internal FlightTrackPresentation Capture()
    {
        if(frozenDirty)
        {
            frozen=segments.Select(segment=>segment.ToImmutableArray()).ToImmutableArray();
            frozenDirty=false;
        }
        return new(frozen,activeSegment,liveEndpoint);
    }

    internal void Reset()
    {
        segments.Clear();frozen=[];frozenDirty=false;activeSegment=-1;liveEndpoint=null;
        previousAt=null;previousPosition=null;lastCommittedAt=null;lastCommittedPosition=null;
        continuityBroken=true;previousAllowed=false;
    }

    private void StartSegment(GeoPoint position,DateTimeOffset at)
    {
        segments.Add([position]);activeSegment=segments.Count-1;lastCommittedAt=at;lastCommittedPosition=position;
        liveEndpoint=position;frozenDirty=true;continuityBroken=false;
    }

    private void UpdateSegment(GeoPoint position,DateTimeOffset at)
    {
        if(activeSegment<0){StartSegment(position,at);return;}
        if(previousPosition is not{} previous||previousAt is not{} previousTime||lastCommittedPosition is not{} anchor||lastCommittedAt is not{} anchorTime)return;
        var moved=DistanceNm(anchor,previous);
        if(moved>=MinimumMovementNm&&
           (previousTime-anchorTime>=SampleInterval||moved>=MaximumSampleDistanceNm))
        {
            segments[activeSegment].Add(previous);lastCommittedAt=previousTime;lastCommittedPosition=previous;frozenDirty=true;
        }
        liveEndpoint=position;
    }

    private void BreakContinuity()
    {
        if(activeSegment>=0&&liveEndpoint is{} endpoint&&segments[activeSegment][^1]!=endpoint)
        {segments[activeSegment].Add(endpoint);frozenDirty=true;}
        continuityBroken=true;previousAllowed=false;previousAt=null;previousPosition=null;
        lastCommittedAt=null;lastCommittedPosition=null;liveEndpoint=null;activeSegment=-1;
    }

    private void CompactIfNeeded()
    {
        while(PointCount()>MaxPoints)
        {
            var reduced=false;
            for(var segmentIndex=0;segmentIndex<segments.Count;segmentIndex++)
            {
                var segment=segments[segmentIndex];if(segment.Count<=2)continue;
                var compacted=new List<GeoPoint>((segment.Count+2)/2){segment[0]};
                for(var index=2;index<segment.Count-1;index+=2)compacted.Add(segment[index]);
                compacted.Add(segment[^1]);segments[segmentIndex]=compacted;reduced=true;
                if(segmentIndex==activeSegment){lastCommittedPosition=compacted[^1];}
            }
            if(!reduced)
            {
                // A pathological stream of one-point discontinuities cannot retain
                // every break within a finite payload. Preserve the flight origin and
                // newest discontinuities while thinning only intermediate segments.
                if(segments.Count<=2)break;
                for(var index=segments.Count-2;index>0&&PointCount()>MaxPoints;index-=2)
                {segments.RemoveAt(index);if(activeSegment>index)activeSegment--;reduced=true;}
                if(!reduced)break;
            }
            frozenDirty=true;
        }
    }

    private int PointCount()=>segments.Sum(segment=>segment.Count)+(liveEndpoint is{} endpoint&&activeSegment>=0&&segments[activeSegment][^1]!=endpoint?1:0);
    private static string Identity(FlightSessionSnapshot state)=>(state.Aircraft??"")+"|"+(state.Reading?.Evidence?.Registration??"")+"|"+
        state.ObservedSessionStart?.ToString("O")+"|"+state.Assignment?.FlightNumber+"|"+state.Assignment?.PlannedDepartureUtc.ToString("O");
    private static double DistanceNm(GeoPoint first,GeoPoint second)=>MapGeography.Angle(first,second)*3440.065;
}

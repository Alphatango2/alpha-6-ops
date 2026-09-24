using System;
using System.Collections.Immutable;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal enum SimulatorConnectionState { Disconnected, Launching, Connecting, Connected, Reconnecting, Failed }

// All nested collections are detached immutable copies. A view can retain this
// value after the next sample or after it has unsubscribed from the session.
internal sealed record FlightSessionSnapshot
{
    public ActiveFlightPlan? Assignment { get; init; }
    public SimulatorConnectionState Connection { get; init; }
    public long ConnectionGeneration { get; init; }
    public bool ConnectionActive { get; init; }
    public bool Connected { get; init; }
    public string ConnectionMessage { get; init; } = "Disconnected.";
    public string TrackingStatus { get; init; } = "No active flight";
    public string LiveDescription { get; init; } = "";
    public string LogStatus { get; init; } = "";
    public string? LastJournal { get; init; }
    public string? Aircraft { get; init; }
    public string Source { get; init; } = "MSFS 2024";
    public Telemetry? Telemetry { get; init; }
    public LiveReading? Reading { get; init; }
    public DateTimeOffset? SimulatorUtc { get; init; }
    public FlightPhase? Phase { get; init; }
    public string PhaseLabel { get; init; } = "Ready";
    public double? RouteProgress { get; init; }
    public bool RouteReliable { get; init; }
    public double? RemainingDistanceNm { get; init; }
    public DateTimeOffset? EstimatedArrival { get; init; }
    public DateTimeOffset? ActualOut { get; init; }
    public DateTimeOffset? ActualIn { get; init; }
    public bool Complete { get; init; }
    public bool PirepEligible { get; init; }
    public bool CanConfirmBlockIn { get; init; }
    public bool JoinedInProgress { get; init; }
    public bool ScheduleTrusted { get; init; }
    public bool DataCurrent { get; init; }
    public bool CanRenderAssignment { get; init; }
    public bool CanPresentCandidate { get; init; }
    public bool SuppressObservedFlight { get; init; }
    public string? ArrivalMismatch { get; init; }
    public DateTimeOffset? ObservedSessionStart { get; init; }
    public FlightAssociation Association { get; init; } = new(false,"FREE FLIGHT",ImmutableArray<string>.Empty);
    public AircraftRotation? Rotation { get; init; }
    public string ContextSummary { get; init; } = "";
    public ImmutableArray<FlightEvent> PhaseEvents { get; init; } = [];
    public ImmutableArray<TrackingEventEntry> Events { get; init; } = [];
    public ImmutableArray<string> Milestones { get; init; } = [];
}

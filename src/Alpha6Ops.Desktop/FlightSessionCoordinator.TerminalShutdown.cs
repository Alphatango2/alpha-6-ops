using System;
using System.Linq;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal sealed partial class FlightSessionCoordinator
{
    private readonly BlockInEligibilityPolicy blockInPolicy = new();
    private BlockInContext? blockInContext;
    private string? terminalDecisionReason;
    private GeoPosition? DestinationPosition()
    {
        if (AirportCatalog.Find(activePlan?.Destination)?.Position is { IsValid: true } airport) return airport;
        var endpoint = activePlan?.RoutePoints?.LastOrDefault();
        return endpoint is null ? null : new GeoPosition(endpoint.Latitude, endpoint.Longitude);
    }
    private bool AtDestination(Telemetry sample)
    {
        var destination = DestinationPosition();
        return destination is { IsValid: true } && sample.HasPosition
            && new GeoPosition(sample.LatitudeDegrees, sample.LongitudeDegrees).DistanceNm(destination) <= 5;
    }
    private BlockInContext CurrentBlockInContext(LiveReading reading) => new(reading.Telemetry, liveRecorder?.Phase ?? FlightPhase.AtGate,
        liveRecorder?.Events.Any(e => e.Phase == FlightPhase.TaxiIn) == true, AtDestination(reading.Telemetry),
        activePlan is not null && CanPresentCandidate && liveDataCurrent && reading.Evidence?.SimulationRunning == true
            && liveCancellation is { IsCancellationRequested: false }, connectionGeneration,
        reading.ReceivedUtc ?? DateTimeOffset.UtcNow,
        reading.Telemetry.TerminalShutdown ?? TerminalShutdownEvidence.Limited(reading.Telemetry, reading.ReceivedUtc ?? DateTimeOffset.UtcNow, $"connection-{connectionGeneration}"));

    private void ResetTerminalConfirmation()
    {
        blockInPolicy.Reset(); blockInContext = null; canConfirmBlockIn = false;
        if (liveRecorder?.Phase == FlightPhase.TaxiIn) liveRecorder.ResetPendingTransition();
        if (liveRecorder?.Phase != FlightPhase.Complete) pilotConfirmedBlockIn = false;
    }
    private Telemetry ApplyTerminalPolicy(LiveReading reading)
    {
        blockInContext = CurrentBlockInContext(reading);
        var decision = blockInPolicy.Observe(blockInContext, DateTimeOffset.UtcNow);
        canConfirmBlockIn = decision.CanConfirm;
        if (liveRecorder?.Phase != FlightPhase.Complete) pilotConfirmedBlockIn = decision.PilotConfirmed;
        if (!decision.TerminalEligible) liveRecorder?.ResetPendingCompletion();
        if (terminalDecisionReason != decision.Reason)
        {
            terminalDecisionReason = decision.Reason;
            RecordLog("terminal_block_in", reading.Telemetry.At, new { policyVersion = 1, decision, evidence = blockInContext.Shutdown,
                blockInContext.HasOn, blockInContext.AtDestination, blockInContext.SessionValid, connectionGeneration });
        }
        return reading.Telemetry with { TerminalShutdown = blockInContext.Shutdown, TerminalBlockInEligible = decision.TerminalEligible,
            BlockInAllowed = blockInContext.AtDestination, PilotConfirmedBlockIn = decision.PilotConfirmed };
    }
}

namespace Alpha6Ops.Core;

public interface ISimulatorTelemetry
{
    IAsyncEnumerable<Telemetry> ReadAsync(CancellationToken cancellationToken = default);
}

// Persisted implementations must scope all reads/writes by tenant and use revision checks.
public interface IRotationRepository
{
    Task<AircraftRotation?> GetAsync(string tenantId, string aircraftId, CancellationToken cancellationToken);
    Task SaveAsync(AircraftRotation rotation, long expectedRevision, CancellationToken cancellationToken);
}

// Voice providers may describe facts or propose commands; only the operations service commits them.
public record DispatchContext(string TenantId, string AircraftId, IReadOnlyList<LegProjection> Legs);
public interface IDispatchNarrator
{
    Task<string> DescribeAsync(DispatchContext context, CancellationToken cancellationToken);
}
public record OperationalEnvelope(Guid EventId, string TenantId, string AircraftId, string FlightId,
    DateTimeOffset OccurredAt, string EventType, int SchemaVersion = 1);
public interface IOperationalEventPublisher
{
    Task PublishAsync(OperationalEnvelope envelope, CancellationToken cancellationToken);
}

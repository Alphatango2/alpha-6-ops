# Data-model sketch

Implemented records: `Telemetry`, `FlightEvent`, `FlightLeg`, `AircraftRotation`, `LegProjection`. These are in-memory domain shapes, not database entities or migrations.

Proposed durable model:

| Entity | Key and required fields | Constraints |
| --- | --- | --- |
| Tenant | tenant_id, slug, name, theme | Unique slug; validated accessible color palette |
| Membership | tenant_id, user_id, role | Unique tenant/user; server-authenticated membership |
| Aircraft | tenant_id, aircraft_id, registration, type | Registration unique within tenant |
| Rotation | tenant_id, rotation_id, aircraft_id, service_date, revision | One active timeline per aircraft; UTC timestamps plus local service date |
| FlightLeg | tenant_id, leg_id, rotation_id, sequence, origin, destination, scheduled_out/in, actual_out/in | Unique rotation/sequence; positive block duration; matching station chain |
| Assignment | tenant_id, assignment_id, leg_id, user_id, status | At most one active pilot assignment per leg |
| FlightEvent | tenant_id, event_id, leg_id, type, occurred_at, received_at, sequence, source, actor | Unique event ID; ordered source sequence; corrections append, never silently overwrite |
| Outbox | tenant_id, event_id, payload_version, payload, delivery_state | Written in same transaction as operational update |

All foreign keys include tenant_id. Repository queries require tenant scope. Projection caches are disposable and revision-tagged; actuals/event history are durable. Keep scheduled, actual and estimated values distinct. Takeoff/landing events belong in event history even though the current rotation math only uses out/in. Capture first and final touchdown semantics explicitly before calculating air time.

Future crew, MEL, passenger connection and disruption aggregates reference tenant/flight/aircraft keys rather than adding ad hoc flags to telemetry. Voice transcripts reference audited command IDs and a retention policy; audio is not core operational truth. Define retention and deletion requirements before collecting pilot identifiers or raw telemetry at scale.

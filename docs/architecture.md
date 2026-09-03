# Architecture and stack decisions

## Stack

Choose .NET 10 LTS for deterministic domain logic, an ASP.NET Core API, and a Windows pilot host. Keep domain/replay code portable. The Windows x64 WPF tray shell now runs the replay directly; add the SimConnect adapter next, with the SDK bridge isolated if managed assembly compatibility requires another target. React 19.2 / TypeScript / Vite build the browser dashboard. Vite 7 is a conservative pinned major; lockfile captures resolved patches. PostgreSQL with EF Core is the intended server persistence stack, not currently installed. SQLite is intended only for the pilot's offline outbound queue. See desktop.md for the offline private-runtime packaging and its validation limits.

Version guidance checked 2 September 2026:
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy): .NET 10 is LTS through November 2028.
- [React versions](https://react.dev/versions): React 19.2 family.
- [Vite getting started](https://vite.dev/guide/): Node 20.19+ / 22.12+; use supported Node 24 for this project.
- [MSFS 2024 SimConnect SDK](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/simconnect-sdk/): out-of-process native/managed integration.

## Dependency direction

Pilot replay / future SimConnect → normalized telemetry → Core phase detector → flight events → operations application service → rotation projection. Today the Pilot and API separately exercise Core using the same fixture; they do not communicate with one another. Web → read-only API. Future persistence, dispatch and outbound webhooks implement Core contracts at the edges. The Core library has no simulator, HTTP, database or AI dependency.

Keep this a modular monolith initially. Avoid microservices until workload or ownership boundaries justify them. Build one authoritative operations service; never calculate different delay values in JavaScript or a language model.

## Deterministic rules

Estimated out = max(scheduled out, previous estimated/actual in + minimum turn), unless actual out exists. Estimated in = actual in if known; otherwise estimated out + planned block duration. Signed delay = actual/projected minus scheduled, in minutes. Early actuals are preserved; projected departures cannot move before schedule. UTC `DateTimeOffset` arithmetic crosses midnight without local timezone assumptions.

The phase detector confirms conditions for three timestamp seconds, emits the first qualifying timestamp, and resets pending conditions on pauses, slew or gaps greater than 15 seconds. Sampling is expected about once per second. Sparse fixtures provide fresh samples around each milestone. It does not infer events during disconnection or recover a session loaded airborne. Production needs a clock-reset policy, durable state checkpoint and explicit pilot reconciliation.

## Tenant and authorization boundary (planned)

Resolve tenant from validated identity membership, not a caller-supplied header or route alone. Roles: Pilot reads assigned legs and submits telemetry only for their active assignment; Dispatcher manages rotations and reconciles events in their tenant; Administrator manages tenant users/branding. Use server-side policies on every endpoint, composite tenant keys, and cross-tenant denial tests. Current role enum is a model sketch only. Current demo route filtering is not authentication.

Before adding writes: validate assignment/aircraft ownership, enforce event IDs and monotonic sequence, commit events plus rotation revision atomically, and publish through an outbox. Optimistic concurrency must reject stale revision writes. Retries must not duplicate actuals; corrections need actor/reason/audit trail. Telemetry capture must never directly mutate another tenant's state.

## Voice and other extension points

`IDispatchNarrator` consumes calculated operational facts. Future STT produces proposed intents; server authorization and deterministic validation execute approved commands; TTS speaks the accepted result. A model cannot invent clearances, legality, fuel, delay, or maintenance decisions. Separate simulated company dispatch from ATC. Store provider configuration outside domain types and never include keys in frontend bundles. `IOperationalEventPublisher` anticipates versioned webhooks; signed delivery, retries and subscription management are future work. `IRotationRepository` is a contract, not a storage implementation.

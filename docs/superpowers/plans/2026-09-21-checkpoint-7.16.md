# Checkpoint 7.16 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a local v0.21.0 checkpoint that corrects JST760 telemetry presentation, makes PIREP closeout atomic, and cleans up Dispatch airport and gate assignment workflows.

**Architecture:** Keep aircraft-independent telemetry contracts in Core and presentation decisions in the WPF desktop. Extend existing evidence trackers and local gate history instead of introducing a new provider or database. Exercise the changes through the dependency-free core checks, desktop smoke/integration checks, recorded-flight replay, and packaged application smoke test.

**Tech Stack:** .NET 10, C#, WPF, existing JSON persistence, PowerShell packaging.

**Spec:** `docs/JST760-flight-review.md`

## Global Constraints

- Keep the release local; do not push to GitHub.
- Do not add NuGet or npm dependencies.
- Preserve generic and Boeing telemetry behavior while adding A32x-specific handling.
- Gate recommendations are learned local history, never live availability.
- Build the local release as v0.21.0.

## Review Focus

- Sparse A32x flap telemetry must keep a useful handle label without claiming surface confirmation.
- A transient missing engine channel must not invent a shutdown transition.
- Complete phase without an actual IN event must not expose Submit PIREP.
- Unknown airports and long airport names must not break Dispatch layout.
- Weak or cross-airline gate history must not prefill a recommendation.

---

### Task 1: A32x flap and shutdown evidence

**Files:**
- Modify: `tests/Alpha6Ops.Tests/EngineEvidenceTests.cs`
- Modify: `src/Alpha6Ops.Core/EngineEvidence.cs`
- Modify: `src/Alpha6Ops.Desktop/DashboardSmokeTest.cs`
- Modify: `src/Alpha6Ops.Desktop/FlightTrackingEvents.cs`

**Interfaces:**
- Consumes: `Telemetry` flap surface fields and `EngineEvidenceTracker.Observe`.
- Produces: stable A32x labels and `EngineCondition.Stopping` during confirmed spool decay.

- [ ] Add failing engine-decay and A32x flap/surface checks.
- [ ] Run the focused core and desktop smoke checks and confirm the expected failures.
- [ ] Implement post-running shutdown hysteresis, A32x six-position mapping, and bilateral surface confirmation.
- [ ] Run focused checks and the full core suite until green.

### Task 2: PIREP readiness and event cleanup

**Files:**
- Modify: `src/Alpha6Ops.Desktop/DashboardSmokeTest.cs`
- Modify: `src/Alpha6Ops.Desktop/TrackingIntegrationSmokeTest.cs`
- Modify: `src/Alpha6Ops.Desktop/FlightTrackingWorkspace.xaml.cs`
- Modify: `src/Alpha6Ops.Desktop/MainWindow.xaml.cs`
- Modify: `src/Alpha6Ops.Desktop/MainWindow.Dashboard.cs`
- Modify: `src/Alpha6Ops.Desktop/FlightTrackingEvents.cs`

**Interfaces:**
- Consumes: `actualIn`, recorder Complete phase, Dispatch render state.
- Produces: explicit PIREP readiness and atomic closeout refresh.

- [ ] Add failing checks for missing actual IN, Dispatch reset, preflight fuel omission, and neutral brake wording.
- [ ] Run desktop smoke/integration checks and confirm the expected failures.
- [ ] Gate the button on Complete plus actual IN, refresh Dispatch during closeout, and update event wording.
- [ ] Run desktop smoke/integration checks until green.

### Task 3: Dispatch identity and learned gate recommendations

**Files:**
- Modify: `src/Alpha6Ops.Desktop/DashboardSmokeTest.cs`
- Modify: `src/Alpha6Ops.Desktop/TrackingIntegrationSmokeTest.cs`
- Modify: `src/Alpha6Ops.Desktop/DispatchWorkspace.xaml`
- Modify: `src/Alpha6Ops.Desktop/DispatchWorkspace.xaml.cs`
- Modify: `src/Alpha6Ops.Desktop/DispatchGateEditor.cs`

**Interfaces:**
- Consumes: `AirportCatalog`, `GateHistoryStore.Load`, active/draft `ActiveFlightPlan`.
- Produces: Dispatch airport-name fields, compact gate controls, and confirmable `GateRecommendation` values.

- [ ] Add failing checks for airport names, compact controls, recommendation ranking, weak-match suppression, and pilot confirmation.
- [ ] Run desktop smoke/integration checks and confirm the expected failures.
- [ ] Implement the shared airport-name presentation and recommendation model/UI.
- [ ] Run responsive desktop checks at 1920, 1366, and 1100 DIP until green.

### Task 4: JST760 replay and local v0.21.0 package

**Files:**
- Modify: `src/Alpha6Ops.Desktop/RecordedFlightReplay.cs`
- Modify: `src/Alpha6Ops.Desktop/Alpha6Ops.Desktop.csproj`
- Modify: `packaging/build-desktop.ps1`
- Modify: `docs/product-roadmap.md`
- Modify: `docs/JST760-flight-review.md`
- Create: `docs/checkpoint-7.16.md`
- Create: `docs/release-0.21.0.md`

**Interfaces:**
- Consumes: supplied JST760 JSONL and existing replay manifest format.
- Produces: replay assertions, local installer, portable zip, hashes, and release record.

- [ ] Add JST760-specific replay assertions and run them against the untouched journal.
- [ ] Run the full build, 116+ core checks, desktop smoke/integration checks, and recorded replay.
- [ ] Update version/docs and build the installer plus portable archive.
- [ ] Extract the installer diagnostically and run packaged desktop smoke.
- [ ] Record hashes and final verification evidence without pushing or committing.

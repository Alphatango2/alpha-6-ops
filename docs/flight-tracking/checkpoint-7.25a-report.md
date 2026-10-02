# 7.25A — A350 Endurance Native Findings

## Baseline

Immutable baseline: `S4-7.25-TRACKING-20260930-e7ee5c18d8ae`.

The baseline package remains unchanged. Its installer SHA-256 is
`1EA6A4ED4B8C397850AF60815B0C7A3616CA09311E49C6DCAF8697106337EF65`
and its portable ZIP SHA-256 is
`24C6639621E7A5CFD4CD5BFF43D456D6D741E24F1A21078048BCE55CB6634F72`.
The original DAL40 ZIP also remained unchanged; SHA-256
`19C037E2EE3A35C9BA7E50DD13E5570C5B1E5404F7695923548AF0D5186E04DA`.

## Flown track

**Root cause:** observed-track history lived inside the Map V2 projection service.
It therefore received only snapshots offered to a visible renderer, was broken by
workspace absence or renderer disposal, and discarded the origin after its 2,048
point FIFO filled.

**New ownership:** `FlightTrackPresentationHistory` is owned by the application
presentation lifetime and observes immutable flight-session snapshots during
Operations refresh regardless of the visible workspace or renderer. An immutable
snapshot is passed into map projection. The history has no commands, persistence,
lifecycle, completion, OOOI or PIREP authority.

**Sampling and simplification:** the active endpoint is always retained. Moving
intermediate points are committed every 30 seconds or after five nautical miles,
with movement under 0.05 NM ignored. Invalid, stale, paused, slewed, reconnected,
time-gap and implausible-jump boundaries create separate segments. At the 4,096
point hard ceiling, intermediate points are progressively thinned while segment
endpoints, the flight origin and current endpoint remain. Pathological one-point
segments retain the origin and newest discontinuities while thinning only middle
segments.

**Memory and payload bound:** 4,096 coordinate points is the hard bound. A normal
24-hour flight at the 30-second cadence uses about 2,881 points plus the live
endpoint. Coordinate values occupy about 64 KiB before collection overhead; the
serialized coordinate portion remains bounded to a few hundred KiB. Growth is not
proportional to the one-second source sample count.

- 15-hour synthetic track: **PASS**
- Workspace-hidden accumulation (>5 minutes): **PASS**
- Renderer recreation: **PASS**
- Dateline and discontinuities: **PASS**
- Stationary two-hour preflight consumes one point: **PASS**

## Step climbs

**Root cause:** the old predicate required the aircraft to be both at least 750
feet above the current observed cruise and still climbing at +500 ft/min for 30
seconds. DAL40's shallow A350 climbs reduced vertical speed before that combined
condition could accumulate 30 seconds, leaving the observed cruise stale.

**Intent model:** 30 valid observed seconds at or above +500 ft/min while at
least 100 feet above and departing the current observed level creates bounded
step intent. Intent does not commit a cruise level. The existing +750-foot
displacement and 60-second stable-level requirements still commit the next level.
Intent expires after 600 observed seconds and cancels on return, descent, invalid
data, pause, slew, excessive gap, go-around or incompatible evidence. Duplicate
timestamps add no time credit and restart downtime adds none.

- FL350→FL370: **PASS**
- FL370→FL390: **PASS**
- FL390→FL410: **PASS**
- Sequential provenance: **PASS**
- Filed FL350 remains immutable: **PASS**
- Negative protections: **PASS**
- TOD from FL410: **PASS**
- Independent TOD change: **NO**

## Regression

- Release build: **PASS**, zero warnings and errors
- Combined automated acceptance: **2,878/2,878 PASS**, 0 failed, 2 documented
  native-only skips
- Active-flight recovery: **74/74 PASS**
- Map V2 browser/input/lifecycle: **208/208 PASS**
- Map scheduling: **9/9 baseline + 6/6 contract PASS**
- Map restoration/resources: **157/157 PASS**
- Direct responsiveness: **50/50 PASS**
- Terminal/Fenix/generic Block In: **203/203 PASS**
- Logbook authoritative OOOI: **40/40 PASS**
- Core: **194/194 PASS**
- Historical replays: **11/11 PASS, 100,632 samples**
- DAL40 authoritative replay: **1/1 PASS, 54,061 samples**
- Total recorded telemetry processed: **154,693 samples**
- Native-style DAL40/negative cruise profiles: **19/19 PASS**

One pre-acceptance interaction run failed because Windows had zero free disk
space while flushing its diagnostic journal. One combined-run desktop smoke
failed on a transient Windows dashboard-state replacement denial. Both original
results are retained. Evidence-preserving NTFS compression recovered space; the
unchanged interaction gate passed 13/13 and the unchanged desktop smoke passed
1,696 checks. The final combined acceptance then passed without retries inside
the gate or altered thresholds.

## Scope

Production behavior changed only in:

- `src/Alpha6Ops.Desktop/MainWindow.Operations.cs`
- `src/Alpha6Ops.Desktop/MapV2/FlightTrackPresentationHistory.cs`
- `src/Alpha6Ops.Desktop/MapV2/MapProjectionPump.cs`
- `src/Alpha6Ops.Desktop/MapV2/MapProjectionService.cs`
- `src/Alpha6Ops.Desktop/ObservedCruiseTracker.cs`
- `src/Alpha6Ops.Desktop/OperationsMapHost.cs`
- `src/Alpha6Ops.Desktop/OperationsOverview.cs`

Regression and diagnostic coverage changed in:

- `src/Alpha6Ops.Desktop/App.xaml.cs`
- `src/Alpha6Ops.Desktop/MapV2/FlightTrackPresentationChecks.cs`
- `src/Alpha6Ops.Desktop/MapV2/MapModelChecks.cs`
- `src/Alpha6Ops.Desktop/MapV2/MapPerformanceChecks.Restoration.cs`
- `src/Alpha6Ops.Desktop/ObservedCruiseNativeProfileChecks.cs`
- `src/Alpha6Ops.Desktop/RecordedFlightReplay.cs`
- `scripts/verify-checkpoint-7.25a.ps1`

Packaging text changed in `packaging/PreviewSetup.cs` and
`packaging/Desktop-Quick-Start.txt`. No Core, OOOI, PIREP, terminal-adapter,
generic A350 Block In, SimConnect, identity, recovery-authority, ETA, health-marker,
Map scheduling, responsiveness-policy, database-authority or Logbook production
file changed relative to the frozen 7.25 source manifest.

## Candidate

Frozen candidate: `S4-7.25A-A350-20261001-6020896a402b`.

- Installer SHA-256:
  `2920F198578FA7792D8703BAA8817DFE5E367DF53F55DCDFD51929591DDDA709`
- Portable ZIP / embedded payload SHA-256:
  `5EF952567F3571E7AB91BBF8F8DA21C26067DB0FEBDC9AAF63669F0BD97FD5A3`
- Source manifest SHA-256:
  `6020896A402B6F985BB06FD3A969F497B896751A24DC324E101FF0E25CEA47B8`
- Source snapshot SHA-256:
  `D8D05B90A26D2492DA64E5396159B97F077CE94D4A3C5B4236003CBB843CBF3A`
- Payload verification: **514/514 ZIP and installer files**
- Frozen portable smoke: **1,696/1,696 + activation PASS**
- Installed: **NO**

Native validation is required. The minimum defensible test uses one realistic
shallow A350 step transition plus more than 2,048 live source observations,
Operations hidden for over five minutes, explicit renderer recreation, complete
sampled-trail inspection and TOD from the new observed level. Two natural native
step climbs would add duration but would not prove a distinct property beyond the
three authoritative DAL40 profiles and one live transition.

**7.25A: GO FOR NATIVE VALIDATION.**

7.26 remains blocked until native validation passes. Premium UI work remains
blocked. The candidate was not installed.

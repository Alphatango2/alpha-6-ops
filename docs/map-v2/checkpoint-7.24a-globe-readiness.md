# Globe readiness — analysis only

No globe behavior is implemented or authorized in 7.24A. These findings come from the existing `MapContracts`, `MapProjectionService`, `MapGeography`, `WebMapRenderer`, `OperationsMapHost`, `map.mjs` and `map-math.mjs` code. They describe architecture suitability, not a verified globe renderer.

## Boundary to preserve

FlightSessionCoordinator remains authoritative for telemetry, phase, ETA, route progress, events, recovery and PIREP. Immutable WGS84 map snapshots feed the existing bounded projection/bridge queues. A future projection selector belongs to presentation state and renderer configuration; it must not change simulator connections, flight algorithms, storage or authoritative route completion.

## Inventory for a later experiment

| Area | Reusable boundary | Globe-sensitive work / regression requirement |
|---|---|---|
| Layers | GeoJSON aircraft, airports, waypoint points and planned/completed/observed line semantics; IDs, source/reliability labels, layer toggles | Verify actual pinned-renderer globe behavior for each fill, line, circle, symbol and raster layer; don't assume flat-map styling transfers unchanged. |
| Coordinates | WGS84 longitude/latitude, spherical interpolation and great-circle samples; no screen coordinates in flight state | Camera restoration currently clamps latitude to ±85°, assumes flat-world copies, fixes pitch to zero and uses flat fitBounds/world framing. Separate projection-specific camera framing from geographic data. |
| Route geometry | Preserve authoritative progress fraction and distinct inferred plan/observed track; reject ambiguous antipodal legs | Existing dateline split segments avoid a flat-map wrap line. Check globe seams, horizon clipping, near-antipodal routes and tessellation; do not join hidden-session discontinuities. |
| Aircraft symbols | Stable IDs, heading/status/source, bounded interpolation and missing-heading marker | Current icon rotation aligns to the map. Verify orientation near poles and globe limb, occlusion behind Earth, scale and selection halo. Heading remains telemetry data. |
| Airports | WGS84 point identity, name/role, readable selection text | Verify horizon occlusion, marker size, duplicate world copies and overlap with aircraft at airports. |
| Camera / follow | Read-only saved center/zoom/bearing/pitch/follow ID | Define per-projection zoom/world fit and restore policy. Follow must handle horizon/polar movement and stop on deliberate pan; changing projection must not reset the flight or invent a trail. |
| Day/night | UTC-derived unit sun vector already travels independently of projection | Current 512×256 image source spans a Mercator rectangle to ±85.051129°. Its pixel-to-latitude math and rectangular surface are not a proven globe terminator. A globe implementation needs spherical lighting and polar/limb checks with the same UTC source. |
| Dateline / poles | Geographic validation allows ±90°; spherical math normalizes longitude | Rendering is currently Mercator-limited. Test ±180° crossings, exact/near poles, back-side geometry and focus paths; keep coordinate truth separate from display clamping. |
| Labels | Bounded descriptors (64 total, limited aircraft/airport labels), canvas raster text, accessible text selection | Check density, horizon/back-side culling, rotation, collisions, DPI and screen-space scale. World-copy assumptions must not duplicate labels. |
| Selection / picking | Stable entity IDs and stale-epoch validation; browser hit-testing returns semantic selections | Verify visible-front-side picking, overlapping symbols/routes, selection persistence after projection changes, and keyboard-accessible details. Never accept a hidden back-side item because its flat coordinates overlap the pointer. |
| Performance | Bounded mailboxes, epoch lifetimes, bridge timing, disposal/fallback | Resolve measured WPF scheduling concerns before adding projection cost. Repeat full-flight isolation, CPU/memory, startup, constrained profile, input, scaling and ≥60-minute resource tests. |
| Bridge | Immutable geographic entities, schema/size validation, source/continuity fields, ACK and current-epoch rejection | If projection is added to presentation state, version/validate it explicitly and preserve older state defaults. No operational commands or mutable Core objects cross this bridge. |

## Conclusion

The boundaries can support a future globe mode as a **presentation experiment**, without an operational architecture rewrite. That is an architectural inference from the current separation, not certification of the bundled renderer or current layer set. Flat camera assumptions and the night raster need particular attention. Flight isolation, classic fallback, trustworthy source labels and discontinuity handling remain mandatory.

Checkpoint 7.25 should begin only after separately authorized readiness/native decisions. It should first prove projection/layer/input behavior behind an opt-in switch using existing neutral snapshots, then measure it before considering broader visuals. No city lights, provider change, new traffic simulation or workspace migration is implied.

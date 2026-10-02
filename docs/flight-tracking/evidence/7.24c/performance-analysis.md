# 7.24C interaction evidence — not a physical pass

The original installed JST776 drag-hitch observation remains authoritative. No hitch correction has been selected for production. This investigation narrows the problem to selective WPF input scheduling in an isolated reproduction; it does not establish the underlying native scheduling mechanism or prove that every physical hitch has the same cause.

## Full-window comparison

Both arms use the same composition host, map/route, coordinator, independent 1 Hz synthetic source, storage/presentation work, observer, and serial CDP drag driver with a 16 ms post-completion delay. Only the diagnostic driver's dispatcher priority changes. No production priority or telemetry cadence is changed.

| Metric | Input-priority baseline | Diagnostic Normal-priority arm |
|---|---:|---:|
| Duration | 120 s | 120 s |
| Pointer operations | 2,160 | 6,870 |
| Full input queue maximum | 1,362.699 ms | 29.187 ms |
| Full CDP-await maximum | 577.997 ms | 104.138 ms |
| Source samples / consumed | 120 / 120 | 120 / 120 |
| Arrival-to-publication maximum from raw timestamps | 104.859 ms | 104.450 ms |
| Unfiltered browser RAF maximum | 33.5 ms | 33.4 ms |
| Browser observer drops | 0 | 0 |
| Reported browser long tasks | 0 | 0 |

Raw evidence: `final-interaction-baseline/interaction.json`, `final-interaction-normal/interaction.json`, and their dispatcher traces. `full-window-analysis.json` recalculates from every raw input event. The earlier Normal summary used a 4,096-entry measurement ring and therefore omitted early events; full maxima above supersede that ring summary. The collector now calculates directly from all raw input rows. Percentiles in the analysis use nearest rank; the original harness uses floor-index percentiles, so label methods when comparing them.

The 1,362.699 ms maximum is the last queued pointer event (host 120704.617–122067.316 ms), after the final sample arrived. It therefore does **not** by itself establish concurrent ingestion during that interval. The previous 120-second trace contains an interior 684.479 ms wait with a real coincident synthetic sample: arrival at 85010.527 ms, coordinator entry 85010.559 ms, publication 85037.532 ms, while Input work remained queued until 85587.981 ms. That sample was processed in 27.008 ms. See `input-gap-analysis.md` for callback/GC analysis and its explicit earlier browser-coverage limitation.

The full-window baseline has 29,343 RAF records and 32,605 source/render records with no observer overflow. Projection maximum was 8.820 ms, serialization 34.051 ms, recovery-save 18.006 ms, presentation 35.772 ms, bridge post-to-ACK 37.530 ms. These measured synchronous costs do not explain the entire long Input delay. A completed managed callback trace cannot identify an untraced native wait or operating-system scheduling cause.

The Normal arm changes completed input throughput, so it is not an identical delivered-event-count comparison. CDP await includes resumption on the WPF synchronization context, not just JavaScript execution. Browser RAF continuity is not proof that the WPF-composited pixels remained responsive. CDP also bypasses the physical WPF pointer route. Instrumentation itself changes scheduling.

## What is and is not established

- Established: selective Input-priority delay exists under the composition-host test; Normal-priority tracking can continue during a measured interior delay; no synthetic sample was lost in these windows.
- Established: changing only diagnostic input priority materially changes observed queue latency. This is diagnostic evidence, not an approved production workaround.
- Not established: exact native message/wakeup/composition root cause, installed physical input timing, or real SimConnect ingestion during a physical hitch.
- No attribution to the historical 3.025-second JST776 source gap is supported: that flight has no synchronized pointer/dispatcher trace.

The new explicit `--trace-map-input <new-directory>` option observes the actual installed WPF pointer and source callback path for two minutes, records F9 user markers, and detaches afterward. It does not synthesize input, alter authoritative updates, connect automatically, or assert success. A parked physical trace on the exact verified candidate is the next gate. If managed evidence still cannot identify the cause, native scheduling-stack evidence is required before choosing a correction.

Do not close 7.24C or relabel this as a rendering-only defect. Original failing runs remain preserved. The deterministic observer-sensitivity test intentionally injects separate 650 ms UI/JS stalls; those must never be counted as production performance failures or physical evidence.

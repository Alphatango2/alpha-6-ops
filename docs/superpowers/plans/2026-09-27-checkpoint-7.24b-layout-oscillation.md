# 7.24B layout investigation plan

Goal: isolate the repeating connected Operations layout defect without altering the frozen installed validation build or native flight state.

1. Preserve the physical connected/disconnected and Classic/V2 findings, entry source/payload identity, and copied logs. Mark native gates HOLD while preserving earlier input/first-paint passes.
2. Build a separate diagnostic runner using the R2A assemblies, isolated profile, and recorded parked telemetry through the existing diagnostic simulator source. Observe the real Operations visual tree and Dispatcher without launching/connecting MSFS.
3. Capture a disconnected baseline, telemetry-driven Classic layout, and post-disconnect state. Record transient size/scrollbar changes and correlate with presentation activities. Use V2 only if needed to confirm a shared mechanism.
4. Establish one evidence-backed hypothesis. Test one narrowly scoped presentation change only after baseline reproduction. Preserve failed/negative experiments.
5. If a correction is justified, add a meaningful regression check and validate unchanged operational output, affected lifecycle/input behavior, and package provenance before any installed retest. If isolated evidence is insufficient, prepare a separate bounded diagnostic candidate rather than guessing.
6. Report measured cause or remaining uncertainty and the next safe gate. Do not resume native reconnect/flight validation or change renderer defaults.

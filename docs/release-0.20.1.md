# Checkpoint 7.15 hotfix — v0.20.1 (local)

The A220 ground test showed valid N1 and N2 data while generic fuel flow,
combustion, and starter variables remained unavailable. The previous detector
required fuel flow plus spool evidence and therefore omitted real engine events.

This hotfix:

- accepts sustained agreement between N1 and N2 as running-engine evidence;
- identifies the early dual-spool rise as an engine-start sequence;
- reads generic per-engine oil pressure as an additional corroborating signal;
- keeps all-zero and single-channel readings classified as unknown; and
- preserves debounced start, shutdown, and restart cycles.

Validation includes 116 deterministic checks. The new regression cases use the
signal pattern recorded in the 21 September A220 journal: near-zero generic fuel
flow with N1 near 19–20% and N2 near 64.5%.

Installer: `outputs/Alpha6OPS-Setup-0.20.1.exe`.

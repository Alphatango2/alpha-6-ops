# Checkpoint 7.15 event-log cleanup — v0.20.2 (local)

This hotfix organizes the per-engine start sequence captured by the A220 test.

- Per-engine evidence suppresses the older generic `Engines running` event.
- Engine events no longer display a contradictory `ENGINE DATA UNAVAILABLE`
  footer while their own N1, N2, or oil-pressure evidence is available.
- Spool-down events use the operational labels `shutdown` and
  `shut down` instead of the detector terms `stopping` and `off`.
- Other ground events describe partial transitions as `ENGINE START IN PROGRESS`
  or `ENGINE SHUTDOWN IN PROGRESS` and use an exact running-engine count when
  available.
- Legacy aircraft sources without per-engine evidence retain the aggregate
  engine event.

The regression sequence is Engine 2 starting, Engine 2 running, Engine 1
starting, and Engine 1 running. Installer:
`outputs/Alpha6OPS-Setup-0.20.2.exe`.

# Sierra 4 pre-premium-UI handoff

This handoff is the boundary before `7.27 — Premium UI Foundation`. No 7.27 implementation or Hero Globe work is included.

## Product baseline

Sierra 4 v0.23.0 retains the validated tracking authority from 7.24C through 7.25A: assignment acceptance, lifecycle phase transitions, OOOI actuals, recovery, PIREP ownership, observed cruise, shallow step-climb qualification, TOD/ETA continuity, flight-scoped map history, terminal shutdown and Block In behavior remain covered by their existing gates.

Internal `Alpha6Ops` namespaces and durable legacy filenames remain intentionally compatible. User-visible application, executable, package, shortcut, installer, uninstall and storage identities are Sierra 4. Do not rename internal database tables, journal fields, crash prefixes or compatibility namespaces as part of visual work.

## Storage and process invariants

1. `%LOCALAPPDATA%\Sierra 4` is the canonical product root.
2. Migration must keep `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPS` intact.
3. A canonical profile becomes authoritative only after staging copy and SHA-256 verification complete and the directory is atomically promoted.
4. A failed or conflicting migration uses the legacy layout without partial adoption.
5. Both Sierra 4 and legacy single-instance identities remain acquired together.
6. `Data` and active recovery are outside diagnostic retention authority.
7. The newest unresolved journal and active SimBrief references cannot be removed for quota compliance.
8. Diagnostic gzip output must verify before the source is deleted.

## Build and test split

The default development build contains diagnostic harnesses used by the full acceptance suite. Packaging must pass `IncludeDiagnosticHarnesses=false`; the shipped `Sierra4.dll` must contain none of the command entry points checked by `scripts/test-production-package.ps1`.

Required replays are materialized from the tracked gzip files in `fixtures/flight-tracking-v1`. Do not point verification back to archived checkpoint trees or an absolute path from an older workspace.

## 7.27 entry gate

Before visual work, verify the final 7.26B acceptance record identifies a clean source commit and a frozen `S4-7.26B-PRE-UI-*` candidate. Continue to treat Map V2 as session opt-in until a separate product decision changes its default. Preserve classic-map fallback, lifecycle authority boundaries and the existing recovery/retention tests while changing presentation.

The external 7.26B archive and frozen 7.25A candidate are rollback evidence. They are not development inputs and should not be edited.

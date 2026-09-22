# Checkpoint 7.16 — v0.21.0 (local)

This local release implements the JST760 A321 corrections and the approved
Dispatch airport/gate cleanup. It has not been pushed to GitHub.

Installer: `outputs/Alpha6OPS-Setup-0.21.0.exe`.

Portable package: `outputs/Alpha6OPS-Desktop-0.21.0-win-x64.zip`.

See [checkpoint-7.16.md](checkpoint-7.16.md) for delivered behavior and
validation coverage.

## Verification

- Solution release build: 0 warnings, 0 errors.
- Core checks: 117 passed.
- Extracted packaged desktop: 18 desktop checks and 446 dashboard/integration
  checks passed; embedded product version reports 0.21.0.
- Untouched JST760 replay: 10,552 samples passed with no failures.
- Installer diagnostic extraction completed without registry or shortcut writes.

SHA-256:

- `Alpha6OPS-Setup-0.21.0.exe` —
  `62F60AE835013AF4255A05AAA213EA032DFFF955F31652BEDBCDC594BAC8E92E`
- `Alpha6OPS-Desktop-0.21.0-win-x64.zip` —
  `F6D0F90A57600938ABA2E91E8AF381E79E633592B8D1F17F4D60C58EE2400365`

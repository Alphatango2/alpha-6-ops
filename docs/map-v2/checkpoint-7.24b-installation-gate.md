# 7.24B installation gate failure — 25 September 2026

**Current status: normal upgrade and installed identity PASS.** The user completed the unchanged installer's upgrade after restart; **514/514** installed payload files match the frozen build, and **216/216** saved-data files are unchanged. Initial installed startup is under validation; physical/native gates remain open. See `checkpoint-7.24b-installed-baseline.md`. The original installation failure and post-restart investigation remain recorded below. No replacement installer or production-code change was required.

## Post-restart verification — 25 September 2026, 18:48 UTC

Windows reports a fresh boot at **18:44:49.5 UTC**. PID 23988 is absent; no Alpha6OPS, Flight Lab, matching setup or MSFS processes were found. The same read-only probe against the same frozen installer now reports `HasContents: True`, `VerifyOwnedInstallation: OK`, `CheckTree: OK`, and **`EnsureNotRunning: OK`**. This clears the previously reproduced process-query obstruction without changing or bypassing the installer.

The installer and portable ZIP still match their frozen SHA-256 hashes. All **216 profile/history/settings/log files** and **490 existing installation files** match the incident-entry manifests, with no changed, missing or added files. No upgrade or application launch was performed during this recheck.

**Next:** retry the same frozen installer normally and select UPGRADE ALPHA 6 OPS. After its upgrade-complete message, return for installed payload verification before launching the application or MSFS. A passing preflight is not yet a successful upgrade. The earlier failure evidence remains intact; post-restart evidence is stored separately under `evidence/7.24b-installation-gate/post-restart-20260925-134803/`.

## Confirmed failure

The user selected UPGRADE ALPHA 6 OPS in the frozen `0.22.0 · PILOT 7.24B` installer and received “Installation could not finish: Access is denied.” Target: `%LOCALAPPDATA%\Alpha6Designs\Alpha6OPSPreview`.

A separate read-only .NET Framework probe loaded the **actual frozen installer assembly** and called only its existing inspection methods. It never invoked Main, ReplaceInstallation, Extract, Uninstall, registry writes or shortcut creation. Results:

| Inspection | Result |
|---|---|
| Existing installation has contents | True |
| Ownership marker recognized | Pass |
| Directory/reparse-point inspection | Pass |
| Running-process check `EnsureNotRunning` | **Win32Exception: Access is denied** |
| Enumerated Alpha6OPS process | **PID 23988**, 0 threads, 0 handles |
| `Process.HasExited` on that PID | **Access denied** |
| `Process.MainModule` on that PID | **Access denied** |

The frozen installer's preflight enumerates all `Alpha6OPS` processes and reads `HasExited` before resolving their executable paths. Windows denies that process query. The exception escapes the check and becomes the generic installation error. This identifies the denied operation as process inspection; it does not establish a write-permission problem in the installation directory.

The same retained PID was present in earlier diagnostics. Its zero threads/handles are consistent with a stale process entry. We have **not** established why Windows retained it or that its original image path was the installed application—the image query itself fails. No process was killed and no access check was bypassed.

`EnsureNotRunning` executes before staging extraction and before either installation-directory move. The reproduced exception therefore stops the upgrade before payload replacement. No staging/backup sibling directories were present when inspected. The original GUI does not save a stack trace; the conclusion is supported by direct reproduction of the same error in the frozen preflight, not an invented original-install log.

## Preservation

Created a local, verified recovery copy under `work/checkpoint-7.24b-installation-gate/preserved-existing/`:

- **216 profile/history/settings/log files**, 446,050,806 bytes.
- **490 existing installation files**, 205,691,462 bytes.
- Source and backup SHA-256 hashes match their incident-entry manifests; sources remained unchanged throughout this investigation.
- Frozen installer and portable ZIP hashes still match the pre-install archive; **514/514 frozen payload files** also match.

These incident-entry hashes prove that the investigation did not alter the current data; they are not presented as a pre-failure filesystem snapshot. The original pre-installation evidence, recorded failures and binary archives remain intact. Profile copies and file manifests remain local in ignored `work/`, with only compact diagnostic evidence in documentation.

## Verification gap and scope

Earlier extraction and payload identity checks verified the packaged bytes. They did **not** exercise the real installed upgrade's running-process preflight. The synthetic baseline runner tolerated the known zero-thread/zero-handle entry, while this installer does not. Consequently the previous passing application assertions cannot close this installation gate.

No broad regression suite was repeated: the application, package and installer source were not changed. One diagnostic probe compilation initially used an invalid relative compiler path, then compiled successfully using absolute paths; this did not run the installer or touch production data.

The installer also reports this process-query failure too generically. Any future installer hardening should distinguish an active installed process, a disappeared process and an inaccessible process, provide actionable diagnostics, and include upgrade-preflight tests. Silently ignoring access-denied processes would weaken the protection against replacing a running installation. No such change or bypass was made here.

## Next action

1. Save any other work, close the installer, and use **Windows Start → Power → Restart**. A Windows restart is the next controlled way to clear the retained process without changing the frozen build or weakening its preflight. It has not been performed or proven effective yet.
2. Return to this task **before reopening Alpha6OPS/Sierra 4 or MSFS**. Recheck process state and run the same read-only installer preflight. Preserve this failure evidence.
3. If preflight passes, retry the **same frozen installer** through its normal UPGRADE button. Do not uninstall, manually delete installation/profile files, change permissions or use an elevated/bypass installer as part of this procedure.
4. Require a normal upgrade-complete result and installed payload hash verification before advancing. Then verify profile/history/gates/settings, Operations and classic map startup as originally specified.
5. If the process check remains denied, retain the new evidence and investigate further before any upgrade retry or installer revision.

This is a request for the user to restart Windows, not an automatic restart or permission escalation. Both native gates remain open. 7.24B remains incomplete; Map V2 stays opt-in and classic stays default.

Evidence: `docs/map-v2/evidence/7.24b-installation-gate/` and the full local incident directory `work/checkpoint-7.24b-installation-gate/`.
